using System;
using System.Collections.Generic;
using UnityEngine;

namespace WhoGetInBus.GamePlay
{
    /// <summary>
    /// Một bước dịch chuyển hạt cát (Cocos: CollapseMove { occupant, fromCol, fromRow, toCol, toRow }).
    /// TỌA ĐỘ THEO HỆ COCOS: row 0 = hàng TRÊN CÙNG của tranh cát, row = rows-1 = hàng sát đường xe (đáy).
    /// fromRow &lt; 0: hạt mới sinh ra từ hàng đợi (queue) phía trên bàn cát rồi trượt xuống.
    /// </summary>
    public struct SandCollapseMove
    {
        public int occupant;
        public int fromCol;
        public int fromRow;
        public int toCol;
        public int toRow;

        public bool IsSpawn => fromRow < 0;
        public bool IsStationary => fromCol == toCol && fromRow == toRow;
    }

    /// <summary>Thời gian chờ + thời lượng trượt của 1 hạt cát (Cocos: CollapseMotionTiming).</summary>
    public struct SandMoveTiming
    {
        public float delay;
        public float duration;
    }

    /// <summary>
    /// Lưới logic của bàn cát (tách khỏi hiển thị), toạ độ chuẩn Cocos: [row, col], row 0 = đỉnh bàn cát.
    /// flags: &gt;= 0 là mã màu của hạt, FlagEmpty (-1) là ô trống, FlagBlock (-2) là ô khoá (rãnh đường xe / ô không có hạt).
    /// </summary>
    public sealed class SandBoardGrid
    {
        public readonly int cols;
        public readonly int rows;
        public readonly int[,] flags;
        public readonly int[,] occupant;
        public readonly int[,] terrain;
        public readonly bool[,] gate;
        public readonly List<Vector2Int> gateCells = new List<Vector2Int>();

        private int nextOccupantId = 1;

        public SandBoardGrid(int cols, int rows)
        {
            this.cols = cols;
            this.rows = rows;
            flags = new int[rows, cols];
            occupant = new int[rows, cols];
            terrain = new int[rows, cols];
            gate = new bool[rows, cols];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    flags[r, c] = SandTerrainCollapse.FlagEmpty;
        }

        public int NewOccupantId() => nextOccupantId++;

        public bool InBounds(int col, int row) => col >= 0 && col < cols && row >= 0 && row < rows;
    }

    /// <summary>
    /// THUẬT TOÁN CÁT CHẢY / SỤT LỞ ĐỊA HÌNH — port 1:1 từ Cocos "SandTerrainCollapse.ts".
    ///
    /// Ý tưởng: khi 1 ô (lỗ) bị trống, hạt cát từ ô lân cận có ĐỘ CAO ĐỊA HÌNH (terrain) lớn hơn/bằng sẽ trượt vào lấp lỗ,
    /// ô nguồn lại thành lỗ mới và cứ thế lan truyền thành chuỗi (chain) cho tới khi hết nguồn.
    ///  - Nửa trái bàn cát (col &lt; cols/2) ưu tiên nhận cát từ trên-trái → trên → trên-phải; nửa phải đối xứng.
    ///  - Hướng ưu tiên (primary: 3 ô hàng trên) chấp nhận terrain &gt;= ; hướng phụ (secondary: 5 ô còn lại) chỉ nhận terrain &gt; .
    ///  - Ở các hàng sát đáy (bottomRowRatio, mặc định 25%) mọi hướng chỉ nhận terrain &gt; (nghiêm ngặt).
    ///  - Các lỗ được xử lý theo thứ tự terrain GIẢM DẦN (lỗ nằm xa đường xe xử lý trước).
    ///  - Ô terrain = -1 (rãnh đường xe) không bao giờ là nguồn; ô FlagBlock không bao giờ cho cát.
    /// </summary>
    public static class SandTerrainCollapse
    {
        public const int FlagEmpty = -1;
        public const int FlagBlock = -2;

        // Mỗi phần tử: { dRow, dCol } — dRow = -1 nghĩa là hàng phía TRÊN (Cocos: row 0 là đỉnh)
        private static readonly int[][] LeftPrimary = { new[] { -1, -1 }, new[] { -1, 0 }, new[] { -1, 1 } };
        private static readonly int[][] LeftSecondary = { new[] { 0, -1 }, new[] { 1, -1 }, new[] { 1, 0 }, new[] { 1, 1 }, new[] { 0, 1 } };
        private static readonly int[][] RightPrimary = { new[] { -1, 1 }, new[] { -1, 0 }, new[] { -1, -1 } };
        private static readonly int[][] RightSecondary = { new[] { 0, 1 }, new[] { 1, 1 }, new[] { 1, 0 }, new[] { 1, -1 }, new[] { 0, -1 } };

        private static readonly List<Vector2Int> sortedHolesScratch = new List<Vector2Int>(64);
        private static readonly List<SandCollapseMove> compressScratch = new List<SandCollapseMove>(128);
        private static readonly Dictionary<int, int> compressIndex = new Dictionary<int, int>(128);
        private static readonly Dictionary<int, int> waveByFrom = new Dictionary<int, int>(128);

        #region KHỞI TẠO ĐỊA HÌNH

        /// <summary>
        /// Cocos: buildTerrainFromRoadPoints + buildGateSet.
        /// roadPoints[col, row] (hệ JSON, row 0 = đỉnh): giá trị &gt;= 0 là độ cao, -1 là rãnh đường xe.
        /// gates: danh sách ô cổng đón khách (x = col, y = row trong hệ JSON/Cocos).
        /// </summary>
        public static void BuildTerrain(SandBoardGrid g, int[,] roadPoints, IEnumerable<Vector2Int> gates)
        {
            g.gateCells.Clear();
            for (int r = 0; r < g.rows; r++)
            {
                for (int c = 0; c < g.cols; c++)
                {
                    int t = 0;
                    if (roadPoints != null && c < roadPoints.GetLength(0) && r < roadPoints.GetLength(1))
                    {
                        t = roadPoints[c, r];
                    }
                    g.terrain[r, c] = t;
                    g.gate[r, c] = false;
                    if (t == -1) g.flags[r, c] = FlagBlock;
                }
            }

            if (gates != null)
            {
                foreach (var v in gates)
                {
                    if (g.InBounds(v.x, v.y) && !g.gate[v.y, v.x])
                    {
                        g.gate[v.y, v.x] = true;
                        g.gateCells.Add(v);
                    }
                }
            }
        }

        /// <summary>Dựng ma trận "ai đang đứng ở ô nào" (Cocos: buildOccupancy). Ô trống/khoá = 0.</summary>
        public static void BuildOccupancy(SandBoardGrid g, int[,] occ)
        {
            for (int r = 0; r < g.rows; r++)
            {
                for (int c = 0; c < g.cols; c++)
                {
                    int f = g.flags[r, c];
                    occ[r, c] = (f == FlagBlock || f == FlagEmpty) ? 0 : g.occupant[r, c];
                }
            }
        }

        #endregion

        #region TÍNH TOÁN SỤT LỞ

        /// <summary>
        /// Cocos: computeBatchCollapseMoves. Với danh sách lỗ (x = col, y = row), tính toàn bộ các bước dịch chuyển.
        /// occ bị thay đổi trong lúc tính (mô phỏng chuỗi) — truyền vào bản sao dựng bằng BuildOccupancy.
        /// </summary>
        public static void ComputeBatchCollapseMoves(SandBoardGrid g, List<Vector2Int> holes, int[,] occ, float bottomRowRatio, List<SandCollapseMove> result)
        {
            result.Clear();
            if (holes == null || holes.Count == 0) return;

            // Sắp xếp ỔN ĐỊNH theo terrain giảm dần (insertion sort, giống Array.sort ổn định của JS)
            var sorted = sortedHolesScratch;
            sorted.Clear();
            for (int i = 0; i < holes.Count; i++)
            {
                var h = holes[i];
                int t = g.terrain[h.y, h.x];
                int pos = sorted.Count;
                while (pos > 0 && g.terrain[sorted[pos - 1].y, sorted[pos - 1].x] < t) pos--;
                sorted.Insert(pos, h);
            }

            for (int i = 0; i < sorted.Count; i++)
            {
                var h = sorted[i];
                if (occ[h.y, h.x] == 0)
                {
                    AppendCollapseMovesOn(g, h.y, h.x, occ, bottomRowRatio, result);
                }
            }
        }

        /// <summary>Cocos: computeCollapseMovesOn — chuỗi hạt sẽ trượt vào 1 lỗ cụ thể.</summary>
        public static void ComputeCollapseMovesOn(SandBoardGrid g, int row, int col, int[,] occ, float bottomRowRatio, List<SandCollapseMove> result)
        {
            result.Clear();
            AppendCollapseMovesOn(g, row, col, occ, bottomRowRatio, result);
        }

        private static void AppendCollapseMovesOn(SandBoardGrid g, int row, int col, int[,] occ, float bottomRowRatio, List<SandCollapseMove> result)
        {
            int u = row;
            int c = col;
            int guard = g.rows * g.cols;
            while (guard-- > 0)
            {
                if (!FindNextPersonOn(g, u, c, occ, bottomRowRatio, out int sCol, out int sRow, out int sOcc)) break;
                result.Add(new SandCollapseMove { occupant = sOcc, fromCol = sCol, fromRow = sRow, toCol = c, toRow = u });
                occ[u, c] = sOcc;
                occ[sRow, sCol] = 0;
                u = sRow;
                c = sCol;
            }
        }

        private static bool FindNextPersonOn(SandBoardGrid g, int row, int col, int[,] occ, float ratio, out int sCol, out int sRow, out int sOcc)
        {
            bool left = col < g.cols * 0.5f;
            var primary = left ? LeftPrimary : RightPrimary;
            var secondary = left ? LeftSecondary : RightSecondary;
            if (ScanDirsForPerson(g, row, col, primary, true, occ, ratio, out sCol, out sRow, out sOcc)) return true;
            return ScanDirsForPerson(g, row, col, secondary, false, occ, ratio, out sCol, out sRow, out sOcc);
        }

        private static bool ScanDirsForPerson(SandBoardGrid g, int row, int col, int[][] dirs, bool allowEqual, int[,] occ, float ratio, out int sCol, out int sRow, out int sOcc)
        {
            int f = g.terrain[row, col];
            for (int i = 0; i < dirs.Length; i++)
            {
                int p = row + dirs[i][0];
                int v = col + dirs[i][1];
                if (p < 0 || p >= g.rows || v < 0 || v >= g.cols) continue;

                int nt = g.terrain[p, v];
                if (nt == -1) continue;
                if (!MatchesTerrainRule(g, p, f, nt, allowEqual, ratio)) continue;
                if (g.flags[p, v] == FlagBlock) continue;

                int id = occ[p, v];
                if (id != 0)
                {
                    sCol = v;
                    sRow = p;
                    sOcc = id;
                    return true;
                }
            }
            sCol = sRow = sOcc = 0;
            return false;
        }

        private static bool MatchesTerrainRule(SandBoardGrid g, int sourceRow, int holeTerrain, int sourceTerrain, bool allowEqual, float ratio)
        {
            if (IsBottomRow(g, sourceRow, ratio)) return sourceTerrain > holeTerrain;
            return allowEqual ? sourceTerrain >= holeTerrain : sourceTerrain > holeTerrain;
        }

        public static bool IsBottomRow(SandBoardGrid g, int row, float ratio)
        {
            return row >= Mathf.FloorToInt(g.rows * (1f - ratio));
        }

        #endregion

        #region NÉN CHUỖI & THỜI GIAN CHUYỂN ĐỘNG

        /// <summary>
        /// Cocos: compressOccupantChainMoves — gộp các bước liên tiếp của cùng 1 hạt (A→B rồi B→C) thành A→C,
        /// bỏ các bước đứng yên. Thứ tự kết quả theo lần xuất hiện đầu tiên của từng hạt.
        /// </summary>
        public static void CompressOccupantChainMoves(List<SandCollapseMove> moves, List<SandCollapseMove> result)
        {
            result.Clear();
            if (moves.Count == 0) return;

            var entries = compressScratch;
            entries.Clear();
            compressIndex.Clear();

            for (int i = 0; i < moves.Count; i++)
            {
                var m = moves[i];
                if (compressIndex.TryGetValue(m.occupant, out int idx))
                {
                    var e = entries[idx];
                    if (e.toCol == m.fromCol && e.toRow == m.fromRow)
                    {
                        e.toCol = m.toCol;
                        e.toRow = m.toRow;
                        entries[idx] = e;
                    }
                    else
                    {
                        entries[idx] = m;
                    }
                }
                else
                {
                    compressIndex[m.occupant] = entries.Count;
                    entries.Add(m);
                }
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (!entries[i].IsStationary) result.Add(entries[i]);
            }
        }

        /// <summary>
        /// Cocos: computeCollapseWaveTimings — "sóng" sụt lở: hạt cuối chuỗi (lấp lỗ thật) đi trước (depth 0),
        /// hạt phía trên phải chờ hạt phía dưới đi khỏi chỗ (depth tăng dần).
        /// delay = min(0.09, 0.018·depth), duration = clamp(0.14 + 0.025·dist, 0.16, 0.34).
        /// </summary>
        public static void ComputeWaveTimings(List<SandCollapseMove> moves, Dictionary<int, SandMoveTiming> result, int gridCols)
        {
            result.Clear();
            waveByFrom.Clear();
            int n = moves.Count;
            for (int i = 0; i < n; i++)
            {
                var m = moves[i];
                if (m.fromRow < 0) continue;
                waveByFrom[m.fromRow * gridCols + m.fromCol] = i;
            }

            var depth = new int[n];
            var state = new byte[n]; // 0 = chưa tính, 1 = đang tính, 2 = xong
            for (int i = 0; i < n; i++)
            {
                var m = moves[i];
                if (m.fromRow < 0 || result.ContainsKey(m.occupant)) continue;

                int d = ResolveDepth(moves, i, depth, state, gridCols);
                float dx = m.toCol - m.fromCol;
                float dy = m.toRow - m.fromRow;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                result[m.occupant] = new SandMoveTiming
                {
                    delay = Mathf.Min(0.09f, 0.018f * d),
                    duration = Mathf.Max(0.16f, Mathf.Min(0.34f, 0.14f + 0.025f * dist))
                };
            }
        }

        private static int ResolveDepth(List<SandCollapseMove> moves, int i, int[] depth, byte[] state, int gridCols)
        {
            if (state[i] == 2) return depth[i];
            if (state[i] == 1)
            {
                depth[i] = 0;
                return 0;
            }

            state[i] = 1;
            var m = moves[i];
            int d = 0;
            if (waveByFrom.TryGetValue(m.toRow * gridCols + m.toCol, out int j) && j != i)
            {
                d = ResolveDepth(moves, j, depth, state, gridCols) + 1;
            }
            state[i] = 2;
            depth[i] = d;
            return d;
        }

        /// <summary>Cocos: computeTopRefillTiming — hạt mới sinh từ hàng đợi rơi vào bàn cát.</summary>
        public static SandMoveTiming ComputeTopRefillTiming(int index, float dist)
        {
            return new SandMoveTiming
            {
                delay = Mathf.Min(0.08f, index * 0.015f),
                duration = Mathf.Max(0.2f, Mathf.Min(0.48f, 0.18f + 0.018f * dist))
            };
        }

        #endregion
    }
}
