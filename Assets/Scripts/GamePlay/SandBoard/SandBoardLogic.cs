using System;
using System.Collections.Generic;
using UnityEngine;

namespace WhoGetInBus.GamePlay
{
    /// <summary>
    /// LOGIC THUẦN của bàn cát (không phụ thuộc MonoBehaviour/UI) — port từ Cocos TargetController + FlowSystem + PersonView.
    /// Toàn bộ toạ độ ở đây theo hệ Cocos: [row, col], row 0 = đỉnh bàn cát, row = rows-1 = hàng sát đường xe.
    ///
    ///  - Chọn hạt để ăn: SelectStrictCells (selectAbsorbCandidates) / SelectRelaxedCells (selectRelaxedAbsorbChain).
    ///  - Xoá hạt + cát chảy: RemoveCellsAndFlow → sụt lở theo địa hình (SandTerrainCollapse) → bổ sung từ hàng đợi (stabilizeTopRows)
    ///    → nén chuỗi → danh sách bước hoạt hoạ VisualMoves.
    /// </summary>
    public sealed class SandBoardLogic
    {
        public SandBoardGrid grid;

        public float bottomRowRatio = 0.25f;
        public int minQueueCol = 16;
        public int maxQueueCol = 24;

        /// <summary>Hạt trên bàn cát (cờ màu &gt;= 0).</summary>
        public int boardBeadCount;

        /// <summary>Hàng đợi hạt chờ rơi xuống từ đỉnh bàn cát; phần tử cuối là hạt rơi trước (pop).</summary>
        public readonly List<int> queueColors = new List<int>();

        /// <summary>Hạt (col, row) có đang bận trượt hoạt hoạ không → không thể bị ăn. Do lớp hiển thị gán.</summary>
        public Func<int, int, bool> isCellLocked;

        /// <summary>Các ô vừa bị lấy trong lần RemoveCellsAndFlow gần nhất (x = col, y = row, hệ Cocos).</summary>
        public readonly List<Vector2Int> removedCells = new List<Vector2Int>(32);

        /// <summary>Các bước dịch chuyển cần hoạt hoạ sau lần RemoveCellsAndFlow gần nhất (hạt mới sinh đi trước).</summary>
        public readonly List<SandCollapseMove> visualMoves = new List<SandCollapseMove>(256);

        private int[,] occScratch;
        private bool[] topRowOccupiedBefore;
        private readonly List<Vector2Int> holeScratch = new List<Vector2Int>(64);
        private readonly List<Vector2Int> columnHoleScratch = new List<Vector2Int>(40);
        private readonly List<SandCollapseMove> rawMoves = new List<SandCollapseMove>(128);
        private readonly List<SandCollapseMove> columnRawMoves = new List<SandCollapseMove>(64);
        private readonly List<SandCollapseMove> moveLog = new List<SandCollapseMove>(256);
        private readonly List<SandCollapseMove> compressedMoves = new List<SandCollapseMove>(256);
        private readonly List<int> pendingQueueCols = new List<int>(16);
        private readonly HashSet<int> pendingQueueSet = new HashSet<int>();
        private readonly List<int> affectedCols = new List<int>(16);
        private readonly HashSet<int> columnAffectedSet = new HashSet<int>();
        private readonly HashSet<int> holeColSet = new HashSet<int>();

        private struct AbsorbCandidate
        {
            public int col;
            public int row;
            public int terrain;
            public int distance;
            public int colDistance;
        }
        private readonly List<AbsorbCandidate> candidateScratch = new List<AbsorbCandidate>(128);
        private readonly HashSet<int> selectedKeys = new HashSet<int>();
        private readonly List<SandCollapseMove> relaxedMoves = new List<SandCollapseMove>(64);

        public int Cols => grid != null ? grid.cols : 0;
        public int Rows => grid != null ? grid.rows : 0;

        #region KHỞI TẠO

        /// <summary>
        /// Dựng bàn cát từ địa hình + tranh. pixelCocos[row, col] (row 0 = đỉnh): màu &gt;= 0, âm = ô không có hạt (khoá).
        /// Cocos: initStaticSandTerrain + initPrimaryScreenColor.
        /// </summary>
        public void Build(int cols, int rows, int[,] roadPointsColRow, IEnumerable<Vector2Int> gates, bool enableRoadCutout, int[,] pixelCocos)
        {
            grid = new SandBoardGrid(cols, rows);
            occScratch = new int[rows, cols];
            topRowOccupiedBefore = new bool[cols];
            boardBeadCount = 0;
            queueColors.Clear();
            removedCells.Clear();
            visualMoves.Clear();

            SandTerrainCollapse.BuildTerrain(grid, roadPointsColRow, gates);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    bool roadCutout = grid.terrain[r, c] == -1;
                    if (roadCutout)
                    {
                        if (enableRoadCutout) continue; // giữ FlagBlock
                        grid.flags[r, c] = SandTerrainCollapse.FlagEmpty;
                    }

                    int color = -1;
                    if (pixelCocos != null && r < pixelCocos.GetLength(0) && c < pixelCocos.GetLength(1)) color = pixelCocos[r, c];

                    if (color < 0)
                    {
                        grid.flags[r, c] = SandTerrainCollapse.FlagBlock;
                        continue;
                    }

                    grid.flags[r, c] = color;
                    grid.occupant[r, c] = grid.NewOccupantId();
                    boardBeadCount++;
                }
            }
        }

        /// <summary>
        /// Cocos: TargetController.initRemainingColors (chế độ globalQueueRefill).
        /// Với mỗi nhịp 2 lượt exchangeRatio, lặp toàn bộ danh sách queue và đẩy 1-2 hạt mỗi màu; sau đó đảo ngược để pop lấy từ đầu.
        /// </summary>
        public void SetupQueue(IList<int> queue, int exchangeRatio)
        {
            queueColors.Clear();
            if (queue == null || queue.Count == 0) return;

            for (int o = 0; o < exchangeRatio; o += 2)
            {
                int a = Mathf.Min(2, exchangeRatio - o);
                for (int i = 0; i < queue.Count; i++)
                {
                    for (int d = 0; d < a; d++) queueColors.Add(queue[i]);
                }
            }
            queueColors.Reverse();
        }

        public void ClearAllBeads()
        {
            if (grid == null) return;
            for (int r = 0; r < grid.rows; r++)
            {
                for (int c = 0; c < grid.cols; c++)
                {
                    if (grid.flags[r, c] >= 0) grid.flags[r, c] = SandTerrainCollapse.FlagEmpty;
                    grid.occupant[r, c] = 0;
                }
            }
            queueColors.Clear();
            boardBeadCount = 0;
        }

        #endregion

        #region CHỌN HẠT ĐỂ ĂN (Cocos TargetController)

        private bool IsAbsorbableCell(int col, int row, int color)
        {
            if (!grid.InBounds(col, row)) return false;
            if (grid.flags[row, col] != color) return false;
            if (grid.occupant[row, col] == 0) return false;
            // Hạt đang trượt (state MOVING / locked) không thể bị hút
            if (isCellLocked != null && isCellLocked(col, row)) return false;
            return true;
        }

        public bool HasBoardingEntranceOfColor(int color, int colMin, int colMax, int rowMin, int rowMax)
        {
            var gates = grid.gateCells;
            for (int i = 0; i < gates.Count; i++)
            {
                var g = gates[i];
                if (g.x < colMin || g.x > colMax || g.y < rowMin || g.y > rowMax) continue;
                if (grid.flags[g.y, g.x] == color) return true;
            }
            return false;
        }

        private static int CompareAbsorbCandidate(AbsorbCandidate a, AbsorbCandidate b)
        {
            int r = a.terrain - b.terrain;
            if (r != 0) return r;
            r = a.distance - b.distance;
            if (r != 0) return r;
            r = b.row - a.row;
            if (r != 0) return r;
            r = a.colDistance - b.colDistance;
            if (r != 0) return r;
            return a.col - b.col;
        }

        /// <summary>
        /// Cocos: selectAbsorbCandidates. Cần có ô cổng cùng màu trong cửa sổ ±span; chọn tối đa count hạt cùng màu (không đang trượt)
        /// trong cửa sổ theo thứ tự: terrain THẤP → gần xe (Chebyshev) → hàng sâu (row lớn) → lệch cột ít → cột nhỏ.
        /// (cx, cy) là ô của xe, hệ Cocos.
        /// </summary>
        public void SelectStrictCells(int color, int count, int cx, int cy, int span, List<Vector2Int> outCells)
        {
            outCells.Clear();
            int rows = grid.rows, cols = grid.cols;
            if (count <= 0) return;
            if (cy + span < 0 || cy - span >= rows || cx + span < 0 || cx - span >= cols) return;

            int colMin = Mathf.Max(0, cx - span);
            int colMax = Mathf.Min(cols - 1, cx + span);
            int rowMin = Mathf.Max(0, cy - span);
            int rowMax = Mathf.Min(rows - 1, cy + span);

            if (!HasBoardingEntranceOfColor(color, colMin, colMax, rowMin, rowMax)) return;

            candidateScratch.Clear();
            for (int r = rowMin; r <= rowMax; r++)
            {
                for (int c = colMin; c <= colMax; c++)
                {
                    if (!IsAbsorbableCell(c, r, color)) continue;
                    int terrain = grid.terrain[r, c];
                    if (terrain < 0) continue;

                    candidateScratch.Add(new AbsorbCandidate
                    {
                        col = c,
                        row = r,
                        terrain = terrain,
                        distance = Mathf.Max(Mathf.Abs(c - cx), Mathf.Abs(r - cy)),
                        colDistance = Mathf.Abs(c - cx)
                    });
                }
            }

            if (candidateScratch.Count == 0) return;
            candidateScratch.Sort(CompareAbsorbCandidate);

            int take = Mathf.Min(count, candidateScratch.Count);
            for (int i = 0; i < take; i++)
            {
                outCells.Add(new Vector2Int(candidateScratch[i].col, candidateScratch[i].row));
            }
        }

        /// <summary>
        /// Cocos: selectRelaxedAbsorbChain (cuối màn). Quét vòng tròn mở rộng quanh xe lấy hạt đầu tiên cùng màu, sau đó lấy tiếp
        /// các hạt sẽ trượt vào chỗ trống đó, rồi bù thêm quanh hạt cuối cùng.
        /// </summary>
        public void SelectRelaxedCells(int color, int count, int cx, int cy, int span, List<Vector2Int> outCells)
        {
            outCells.Clear();
            int rows = grid.rows, cols = grid.cols;
            if (count <= 0) return;
            if (cy + span < 0 || cy - span >= rows || cx + span < 0 || cx - span >= cols) return;

            if (!FindFirstRelaxedAbsorbableInSpan(color, cx, cy, span, out Vector2Int first)) return;

            selectedKeys.Clear();
            outCells.Add(first);
            selectedKeys.Add(first.y * cols + first.x);

            // Chuỗi hạt sẽ trượt vào chỗ trống nếu lấy hạt đầu tiên: lấy tiếp chừng nào còn đúng màu và không đang trượt
            SandTerrainCollapse.BuildOccupancy(grid, occScratch);
            occScratch[first.y, first.x] = 0;
            SandTerrainCollapse.ComputeCollapseMovesOn(grid, first.y, first.x, occScratch, bottomRowRatio, relaxedMoves);
            for (int i = 0; i < relaxedMoves.Count && outCells.Count < count; i++)
            {
                var m = relaxedMoves[i];
                if (!IsAbsorbableCell(m.fromCol, m.fromRow, color)) break;
                int key = m.fromRow * cols + m.fromCol;
                if (selectedKeys.Contains(key)) break;
                outCells.Add(new Vector2Int(m.fromCol, m.fromRow));
                selectedKeys.Add(key);
            }

            if (outCells.Count < count) FillRelaxedByFixedRingSearch(color, outCells, count);
        }

        private bool FindFirstRelaxedAbsorbableInSpan(int color, int cx, int cy, int span, out Vector2Int found)
        {
            int colMin = Mathf.Max(0, cx - span);
            int colMax = Mathf.Min(grid.cols - 1, cx + span);
            int rowMin = Mathf.Max(0, cy - span);
            int rowMax = Mathf.Min(grid.rows - 1, cy + span);

            for (int ring = 0; ring <= span; ring++)
            {
                for (int h = ring; h >= -ring; h--)
                {
                    int row = cy + h;
                    if (row < rowMin || row > rowMax) continue;

                    for (int v = Mathf.Abs(h) == ring ? 0 : ring; v <= ring; v++)
                    {
                        int pMax = v == 0 ? 0 : 1;
                        for (int p = 0; p <= pMax; p++)
                        {
                            int col = v == 0 ? cx : (p == 0 ? cx - v : cx + v);
                            if (col < colMin || col > colMax) continue;
                            if (IsAbsorbableCell(col, row, color))
                            {
                                found = new Vector2Int(col, row);
                                return true;
                            }
                        }
                    }
                }
            }
            found = default;
            return false;
        }

        /// <summary>Cocos: fillRelaxedAbsorbableByFixedRingSearch — bù thêm hạt cùng màu quanh hạt cuối cùng đã chọn.</summary>
        private void FillRelaxedByFixedRingSearch(int color, List<Vector2Int> outCells, int count)
        {
            if (outCells.Count == 0) return;
            var last = outCells[outCells.Count - 1];
            int a = last.x;
            int n = last.y;
            int rings = Mathf.FloorToInt(count / 2f);

            for (int s = 1; s <= rings && outCells.Count < count; s++)
            {
                for (int u = s; u >= -s && outCells.Count < count; u--)
                {
                    int row = n + u;
                    if (row < 0 || row >= grid.rows) continue;

                    for (int c = Mathf.Abs(u) == s ? 0 : s; c <= s && outCells.Count < count; c++)
                    {
                        int fMax = c == 0 ? 0 : 1;
                        for (int f = 0; f <= fMax && outCells.Count < count; f++)
                        {
                            int col = c == 0 ? a : (f == 0 ? a - c : a + c);
                            if (col < 0 || col >= grid.cols) continue;
                            int key = row * grid.cols + col;
                            if (IsAbsorbableCell(col, row, color) && !selectedKeys.Contains(key))
                            {
                                outCells.Add(new Vector2Int(col, row));
                                selectedKeys.Add(key);
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region XOÁ HẠT & CÁT CHẢY (Cocos FlowSystem + PersonView refill)

        /// <summary>
        /// Xoá các ô (x = col, y = row, hệ Cocos), chạy sụt lở địa hình, bổ sung hạt từ hàng đợi.
        /// Kết quả: removedCells (ô đã lấy thật sự) và visualMoves (các bước cần hoạt hoạ). Trả về số hạt đã lấy.
        /// Cocos: FlowSystem.clearHolesForChain → processPendingHoleList (applyCollapseForHoles + requestFlowTopRowRefill).
        /// </summary>
        public int RemoveCellsAndFlow(List<Vector2Int> cells)
        {
            removedCells.Clear();
            visualMoves.Clear();
            holeScratch.Clear();

            for (int i = 0; i < cells.Count; i++)
            {
                int col = cells[i].x;
                int row = cells[i].y;
                if (!grid.InBounds(col, row) || grid.flags[row, col] < 0) continue;

                grid.flags[row, col] = SandTerrainCollapse.FlagEmpty;
                grid.occupant[row, col] = 0;
                boardBeadCount = Mathf.Max(0, boardBeadCount - 1);
                holeScratch.Add(new Vector2Int(col, row));
                removedCells.Add(new Vector2Int(col, row));
            }

            if (holeScratch.Count == 0) return 0;

            FlowAfterHoles(holeScratch);
            return removedCells.Count;
        }

        private void FlowAfterHoles(List<Vector2Int> holes)
        {
            int cols = grid.cols;

            // Chụp trạng thái hàng trên cùng trước khi sụt lở (captureTopRowOccupied)
            for (int c = 0; c < cols; c++) topRowOccupiedBefore[c] = grid.flags[0, c] >= 0;
            holeColSet.Clear();
            for (int i = 0; i < holes.Count; i++) holeColSet.Add(holes[i].x);

            moveLog.Clear();

            // 1. Sụt lở địa hình
            SandTerrainCollapse.BuildOccupancy(grid, occScratch);
            SandTerrainCollapse.ComputeBatchCollapseMoves(grid, holes, occScratch, bottomRowRatio, rawMoves);
            ApplyMovesLogic(rawMoves, moveLog);

            // 2. Bổ sung hạt mới từ hàng đợi nếu hàng trên cùng có chỗ trống (requestFlowTopRowRefill → stabilizeTopRows)
            if (queueColors.Count > 0)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (grid.flags[0, c] == SandTerrainCollapse.FlagEmpty && (holeColSet.Contains(c) || topRowOccupiedBefore[c]))
                    {
                        StabilizeTopRows(moveLog);
                        break;
                    }
                }
            }

            // 3. Nén chuỗi + sắp xếp (hạt mới sinh đi trước)
            SandTerrainCollapse.CompressOccupantChainMoves(moveLog, compressedMoves);
            OrderMovesForVisual(compressedMoves, visualMoves);
        }

        /// <summary>Áp dụng các bước đã tính lên lưới logic (Cocos: applyCollapseData / applyRefillCollapseMove).</summary>
        private void ApplyMovesLogic(List<SandCollapseMove> moves, List<SandCollapseMove> log)
        {
            for (int i = 0; i < moves.Count; i++)
            {
                var m = moves[i];
                if (grid.occupant[m.fromRow, m.fromCol] != m.occupant) continue;
                int flag = grid.flags[m.fromRow, m.fromCol];
                if (flag < 0) continue;

                grid.flags[m.fromRow, m.fromCol] = SandTerrainCollapse.FlagEmpty;
                grid.occupant[m.fromRow, m.fromCol] = 0;
                grid.flags[m.toRow, m.toCol] = flag;
                grid.occupant[m.toRow, m.toCol] = m.occupant;
                log.Add(m);
            }
        }

        /// <summary>Cocos: TargetController.stabilizeTopRows — lấp các ô trống ở hàng trên cùng từ hàng đợi.</summary>
        public void StabilizeTopRows(List<SandCollapseMove> log)
        {
            int lo = Mathf.Max(0, minQueueCol);
            int hi = Mathf.Min(grid.cols, maxQueueCol);

            pendingQueueCols.Clear();
            pendingQueueSet.Clear();
            for (int c = lo; c < hi; c++) EnqueuePendingTopCol(c);

            int idx = 0;
            while (idx < pendingQueueCols.Count)
            {
                int col = pendingQueueCols[idx++];
                pendingQueueSet.Remove(col);
                if (queueColors.Count == 0) continue;
                if (grid.flags[0, col] != SandTerrainCollapse.FlagEmpty) continue;

                int before = queueColors.Count;
                affectedCols.Clear();
                TryFillTopForColumn(col, log);
                if (queueColors.Count >= before) continue;

                for (int i = 0; i < affectedCols.Count; i++) EnqueuePendingTopCol(affectedCols[i]);
            }
        }

        private void EnqueuePendingTopCol(int col)
        {
            if (col < minQueueCol || col >= maxQueueCol || col >= grid.cols || pendingQueueSet.Contains(col)) return;
            if (queueColors.Count == 0) return;
            if (grid.flags[0, col] != SandTerrainCollapse.FlagEmpty) return;
            pendingQueueSet.Add(col);
            pendingQueueCols.Add(col);
        }

        /// <summary>Cocos: tryFillTopForColumn.</summary>
        private void TryFillTopForColumn(int col, List<SandCollapseMove> log)
        {
            int guard = queueColors.Count + 1;
            for (int i = 0; i < guard; i++)
            {
                if (grid.flags[0, col] != SandTerrainCollapse.FlagEmpty || queueColors.Count == 0) break;

                int color = queueColors[queueColors.Count - 1];
                queueColors.RemoveAt(queueColors.Count - 1);

                int id = grid.NewOccupantId();
                grid.flags[0, col] = color;
                grid.occupant[0, col] = id;
                boardBeadCount++;
                log.Add(new SandCollapseMove { occupant = id, fromCol = col, fromRow = -1, toCol = col, toRow = 0 });

                CompactColumnAfterRefillLogic(col, log);
            }
        }

        /// <summary>Cocos: compactColumnAfterRefillLogic — hạt mới ở đỉnh cột trượt xuống lấp mọi lỗ trong cột.</summary>
        private void CompactColumnAfterRefillLogic(int col, List<SandCollapseMove> log)
        {
            columnHoleScratch.Clear();
            for (int r = 0; r < grid.rows; r++)
            {
                if (grid.flags[r, col] == SandTerrainCollapse.FlagEmpty) columnHoleScratch.Add(new Vector2Int(col, r));
            }
            if (columnHoleScratch.Count == 0) return;

            SandTerrainCollapse.BuildOccupancy(grid, occScratch);
            SandTerrainCollapse.ComputeBatchCollapseMoves(grid, columnHoleScratch, occScratch, bottomRowRatio, columnRawMoves);
            if (columnRawMoves.Count == 0) return;

            ApplyMovesLogic(columnRawMoves, log);

            columnAffectedSet.Clear();
            for (int i = 0; i < columnRawMoves.Count; i++)
            {
                columnAffectedSet.Add(columnRawMoves[i].fromCol);
                columnAffectedSet.Add(columnRawMoves[i].toCol);
            }
            columnAffectedSet.Remove(col);
            foreach (int c in columnAffectedSet) affectedCols.Add(c);
        }

        /// <summary>Hạt mới sinh (fromRow &lt; 0) đi trước, sắp theo (toRow, toCol); sau đó tới các hạt cũ (Cocos: commitFlowRefillVisuals).</summary>
        private static void OrderMovesForVisual(List<SandCollapseMove> source, List<SandCollapseMove> result)
        {
            result.Clear();
            for (int i = 0; i < source.Count; i++)
            {
                if (!source[i].IsSpawn) continue;
                var m = source[i];
                int pos = result.Count;
                while (pos > 0 && (result[pos - 1].toRow > m.toRow || (result[pos - 1].toRow == m.toRow && result[pos - 1].toCol > m.toCol))) pos--;
                result.Insert(pos, m);
            }
            for (int i = 0; i < source.Count; i++)
            {
                if (!source[i].IsSpawn) result.Add(source[i]);
            }
        }

        #endregion
    }
}
