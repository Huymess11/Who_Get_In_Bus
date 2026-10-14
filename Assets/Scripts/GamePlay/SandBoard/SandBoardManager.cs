using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WhoGetInBus.Data;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WhoGetInBus.GamePlay
{
    /// <summary>
    /// Quản lý Bảng Tranh Cát (Sandboard) chuẩn 1:1 theo Cocos Creator gốc (PersonView / BatchSpriteManager).
    /// Toàn bộ hiển thị chuyển sang hệ thống UGUI (SandBoardGraphic) vẽ bằng 1 Draw Call duy nhất,
    /// KHÔNG sinh 1.300+ GameObject con giúp game chạy cực mượt 60-120 FPS trên mobile.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(SandBoardGraphic))]
    [ExecuteAlways]
    public class SandBoardManager : MonoBehaviour
    {
        public static SandBoardManager Instance { get; private set; }

        [Header("ScriptableObject Cấu Hình Hạt Cát (Lưu Vĩnh Viễn Không Bị Mất)")]
        [Tooltip("File ScriptableObject chứa toàn bộ thông số hạt cát. Giúp dữ liệu không bị reset khi sửa code C#!")]
        public SandBoardConfigData config;

        [HideInInspector] public int columns = 40;
        [HideInInspector] public int rows = 40;
        [HideInInspector] public float beadRadius = 12.16f;
        [HideInInspector] public bool enableRoadCutout = true;
        [HideInInspector] public int cutoutColMin = 13;
        [HideInInspector] public int cutoutColMax = 26;
        [HideInInspector] public int cutoutRowMax = 19;
        [HideInInspector] public TextAsset levelConfigFile;
        [HideInInspector] public string sandDataString;

        [Header("Cấu Hình Màn Chơi (Level & Road)")]
        [Tooltip("Level ID chuẩn (1, 2, 3...)")]
        public int levelId = 1;
        [Tooltip("ID Tuyến đường (road trong levelNCXHCfg.json)")]
        public int currentRoadId = 5;

        [Header("Thông Số Kích Thước UI (Chuẩn Gốc Cocos)")]
        [Tooltip("Kích thước hạt cát (chuẩn Cocos: 34 x 44 pixel theo tỷ lệ ảnh 48x62, tạo độ lấp lên 20px)")]
        public Vector2 beadSize = new Vector2(34f, 44f);

        [Tooltip("Scale của các hạt cát (chuẩn 1.0)")]
        [Range(0.5f, 1.6f)]
        public float beadScale = 1.0f;

        [Tooltip("Khoảng cách giữa các cột hạt cát trên UI theo chiều ngang (trục X)")]
        [Range(10f, 40f)]
        public float beadSpacingX = 24.32f;

        [Tooltip("Khoảng cách giữa các hàng hạt cát trên UI theo chiều dọc (trục Y)")]
        [Range(10f, 40f)]
        public float beadSpacingY = 24.32f;

        [HideInInspector]
        public float beadSpacing = 24.32f;
        [HideInInspector]
        public float beadSpacingZ { get => beadSpacingY; set => beadSpacingY = value; }

        [Tooltip("Độ lệch ngang (trái/phải) của toàn bộ bảng cát")]
        [Range(-100f, 100f)]
        public float boardOffsetX = 0f;

        [Tooltip("Độ lệch dọc (lên/xuống) của toàn bộ bảng cát")]
        [Range(-100f, 100f)]
        public float boardOffsetY = 0f;

        [HideInInspector]
        public float boardPosZ = 0f;

        [Header("Lệch Zigzag Cát")]
        [Tooltip("Bật/tắt hiệu ứng xếp hạt so le hình zigzag")]
        public bool enableZigzag = true;

        [Tooltip("Độ lệch trục X giữa các hàng xen kẽ nhau (chuẩn Cocos: nửa khoảng cách cột ~12.16px)")]
        [Range(-25f, 25f)]
        public float zigzagOffset = 12.16f;

        [Header("Prefab & Material (Backward Compatibility)")]
        public GameObject beadPrefab;
        public Material beadMaterial;
        public PassengerColorData passengerColors;
        public Texture2D passengerAtlas;

        // Ma trận màu sắc: beadColorIndices[col, row] (-1: rỗng/cutout, 0..17: mã màu)
        private int[,] beadColorIndices;
        private int totalRemainingBeads = 0;

        // Trạng thái animation sạt lở (Collapse) không dùng GameObject, không sinh GC Alloc
        private Vector2[,] collapseVisualOffsets;
        private float[,] collapseTimers;
        private bool isCollapsing = false;
        private const float CollapseDuration = 0.12f;

        private SandBoardGraphic graphic;
        private Camera cachedCameraUI;
        private Camera cachedMainCamera;

        public int TotalRemainingBeads => totalRemainingBeads;
        public int[,] BeadColorIndices => beadColorIndices;

        public void EnsureConfigLoaded()
        {
            if (config == null)
            {
#if UNITY_EDITOR
                config = AssetDatabase.LoadAssetAtPath<SandBoardConfigData>("Assets/Data/SandBoardConfigData.asset");
#else
                config = Resources.Load<SandBoardConfigData>("SandBoardConfigData");
#endif
            }
            if (config != null)
            {
                SyncFromConfig();
            }
        }

        public void SyncFromConfig()
        {
            if (config == null) return;
            beadSize = config.beadSize;
            beadScale = config.beadScale;
            beadSpacingX = config.beadSpacingX;
            beadSpacingY = config.beadSpacingY;
            beadSpacing = config.beadSpacingX;
            enableZigzag = config.enableZigzag;
            zigzagOffset = config.zigzagOffset;
            boardOffsetX = config.boardOffsetX;
            boardOffsetY = config.boardOffsetY;
            columns = config.columns;
            rows = config.rows;
            if (config.passengerAtlas != null) passengerAtlas = config.passengerAtlas;
            if (config.passengerColors != null) passengerColors = config.passengerColors;

            UpdateBoardPosition();
        }

        public void UpdateBoardPosition()
        {
            if (config != null)
            {
                RectTransform rt = GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = config.boardPosition;
                }
            }
        }

        public void SyncToConfig()
        {
            if (config == null) return;
            config.beadSize = beadSize;
            config.beadScale = beadScale;
            config.beadSpacingX = beadSpacingX;
            config.beadSpacingY = beadSpacingY;
            config.enableZigzag = enableZigzag;
            config.zigzagOffset = zigzagOffset;
            config.boardOffsetX = boardOffsetX;
            config.boardOffsetY = boardOffsetY;
            config.columns = columns;
            config.rows = rows;
            if (passengerAtlas != null) config.passengerAtlas = passengerAtlas;
            if (passengerColors != null) config.passengerColors = passengerColors;

            RectTransform rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                config.boardPosition = rt.anchoredPosition;
            }
#if UNITY_EDITOR
            EditorUtility.SetDirty(config);
#endif
        }

        private void Awake()
        {
            Instance = this;
            graphic = GetComponent<SandBoardGraphic>();
            EnsureConfigLoaded();
            EnsureAtlasLoaded();

            if (beadColorIndices == null || totalRemainingBeads == 0)
            {
                int[,] pixelData = GetOrLoadPixelData();
                BuildBoard(pixelData, passengerColors, beadMaterial);
            }
        }

        private void OnEnable()
        {
            Instance = this;
            if (graphic == null) graphic = GetComponent<SandBoardGraphic>();
            EnsureConfigLoaded();
            EnsureAtlasLoaded();
        }

        private void OnValidate()
        {
            if (config != null)
            {
                SyncFromConfig();
            }
        }

        private void Update()
        {
            if (isCollapsing)
            {
                UpdateCollapseAnimation();
            }
        }

        public void EnsureAtlasLoaded()
        {
            if (passengerAtlas == null && config != null && config.passengerAtlas != null)
            {
                passengerAtlas = config.passengerAtlas;
            }
            if (passengerAtlas == null)
            {
#if UNITY_EDITOR
                passengerAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprite/PassengerAtlas_18.png");
#else
                passengerAtlas = Resources.Load<Texture2D>("PassengerAtlas_18");
#endif
            }
            if (config != null && config.passengerAtlas == null && passengerAtlas != null)
            {
                config.passengerAtlas = passengerAtlas;
            }
            if (graphic != null && graphic.passengerAtlas == null)
            {
                graphic.passengerAtlas = passengerAtlas;
            }
        }

        /// <summary>
        /// Nạp và sinh trực tiếp tranh cát từ Level ID chuẩn (1, 2, ...)
        /// </summary>
        public void LoadLevelDirect(int targetLevelId, GamePrefabData prefabData = null)
        {
            var levelData = WhoGetInBus.Data.LevelConfigLoader.LoadLevel(targetLevelId);
            if (levelData != null)
            {
                var collectData = WhoGetInBus.Data.LevelConfigLoader.GetCollect(levelData.collect);
                int picId = collectData != null ? collectData.picture : levelData.collect;
                var pixelMap = WhoGetInBus.Data.LevelConfigLoader.LoadPixelMap(picId);
                if (pixelMap != null)
                {
                    int[,] pData = new int[40, 40];
                    for (int r = 0; r < 40; r++)
                    {
                        for (int c = 0; c < 40; c++)
                        {
                            // In 2D UI: r = 0 là đáy (tiếp giáp vòng đón xe), r = 39 là đỉnh trên.
                            pData[r, c] = pixelMap.points[c, 39 - r];
                        }
                    }
                    levelId = targetLevelId;
                    currentRoadId = levelData.road;
                    BuildBoard(pData, prefabData);
                    return;
                }
            }
            Debug.LogWarning($"[SandBoardManager] Không tìm thấy dữ liệu level {targetLevelId} chuẩn gốc!");
        }

        public Vector2 GetBeadUIPosition(int col, int row)
        {
            float spX = config != null ? config.beadSpacingX : (beadSpacingX > 0f ? beadSpacingX : (beadSpacing > 0f ? beadSpacing : 24.32f));
            float spY = config != null ? config.beadSpacingY : (beadSpacingY > 0f ? beadSpacingY : 24.32f);
            float offX = config != null ? config.boardOffsetX : boardOffsetX;
            float offY = config != null ? config.boardOffsetY : boardOffsetY;
            bool zigzag = config != null ? config.enableZigzag : enableZigzag;
            float zOffset = config != null ? config.zigzagOffset : zigzagOffset;
            int totalCols = config != null ? config.columns : columns;
            int totalRows = config != null ? config.rows : rows;

            float startX = -((totalCols - 1) * spX) * 0.5f + offX;
            float startY = spY * 0.5f + offY;

            float x = startX + col * spX;
            // Số thứ tự hàng tính từ trên đỉnh tranh cát xuống (0 = hàng trên cùng, totalRows - 1 = hàng đáy cùng)
            int rowFromTop = (totalRows - 1) - row;
            if (zigzag && (rowFromTop % 2 != 0))
            {
                x += zOffset;
            }
            float y = startY + row * spY;
            return new Vector2(x, y);
        }

        public Vector2 GetCollapseOffset(int col, int row)
        {
            if (collapseVisualOffsets != null && col >= 0 && col < columns && row >= 0 && row < rows)
            {
                return collapseVisualOffsets[col, row];
            }
            return Vector2.zero;
        }

        public int GetColorAt(int col, int row)
        {
            if (beadColorIndices != null && col >= 0 && col < columns && row >= 0 && row < rows)
            {
                return beadColorIndices[col, row];
            }
            return -1;
        }

        public void BuildBoard(int[,] pixelData, GamePrefabData prefabData)
        {
            if (prefabData != null)
            {
                beadPrefab = prefabData.passengerPrefab;
                beadMaterial = prefabData.beadMaterial;
                passengerColors = prefabData.passengerColors;
            }
            BuildBoard(pixelData, passengerColors, beadMaterial);
        }

        public void BuildBoard(int[,] pixelData, PassengerColorData pColors, Material bMat)
        {
            passengerColors = pColors;
            beadMaterial = bMat;
            EnsureAtlasLoaded();

            // Xóa sạch các GameObject con rác cũ nếu có
            ClearBoardChildren();

            beadColorIndices = new int[columns, rows];
            collapseVisualOffsets = new Vector2[columns, rows];
            collapseTimers = new float[columns, rows];
            isCollapsing = false;
            totalRemainingBeads = 0;

            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    if (enableRoadCutout && IsInRoadCutout(c, r))
                    {
                        beadColorIndices[c, r] = -1;
                        sb.Append("-1,");
                        continue;
                    }

                    int colorIdx = -1;
                    if (pixelData != null && r < pixelData.GetLength(0) && c < pixelData.GetLength(1))
                    {
                        colorIdx = pixelData[r, c];
                    }

                    if (colorIdx < 0)
                    {
                        beadColorIndices[c, r] = -1;
                        sb.Append("-1,");
                        continue;
                    }

                    beadColorIndices[c, r] = colorIdx;
                    totalRemainingBeads++;
                    sb.Append(colorIdx).Append(",");
                }
            }

            if (sb.Length > 0) sb.Length--;
            sandDataString = sb.ToString();

            RefreshGraphic();

            Debug.Log($"<color=cyan>[SandBoardManager - UI]</color> Đã nạp thành công {totalRemainingBeads} hạt cát lên UI! (Scale: {beadScale:F2}, 1 Draw Call, 0 GameObject con)");
        }

        public void ClearBoard()
        {
            ClearBoardChildren();
            if (beadColorIndices != null)
            {
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < columns; c++)
                    {
                        beadColorIndices[c, r] = -1;
                    }
                }
            }
            totalRemainingBeads = 0;
            RefreshGraphic();
        }

        private void ClearBoardChildren()
        {
            List<GameObject> toDestroy = new List<GameObject>();
            for (int i = 0; i < transform.childCount; i++)
            {
                toDestroy.Add(transform.GetChild(i).gameObject);
            }
            foreach (var g in toDestroy)
            {
                if (Application.isPlaying) Destroy(g);
                else DestroyImmediate(g);
            }
        }

        public bool IsInRoadCutout(int col, int row)
        {
            var roadCfg = WhoGetInBus.Data.LevelConfigLoader.GetRoadConfig(currentRoadId);
            if (roadCfg != null)
            {
                int jsonRow = 39 - row;
                if (col >= 0 && col < 40 && jsonRow >= 0 && jsonRow < 40)
                {
                    return roadCfg.IsCutout(col, jsonRow);
                }
            }
            return false;
        }

        /// <summary>
        /// Tìm hạt cùng màu ở hàng đáy để đón lên xe bus.
        /// Chuyển đổi vị trí hạt từ 2D UI Canvas sang tọa độ 3D World Space chính xác để xe bus đón.
        /// </summary>
        /// <summary>
        /// Lấy độ cao địa hình (Terrain Elevation) từ ma trận points của RoadConfigData.
        /// Chuẩn Cocos: giá trị >= 0 là độ cao, -1 là rãnh khoét (vật cản).
        /// </summary>
        public int GetTerrainElevation(int col, int row)
        {
            if (col < 0 || col >= columns || row < 0 || row >= rows) return -1;
            var roadCfg = WhoGetInBus.Data.LevelConfigLoader.GetRoadConfig(currentRoadId);
            if (roadCfg != null && roadCfg.points != null)
            {
                int jsonRow = 39 - row;
                if (jsonRow >= 0 && jsonRow < 40)
                {
                    return roadCfg.points[col, jsonRow];
                }
            }
            return 0;
        }

        /// <summary>
        /// Kiểm tra xem ô (col, row) có phải là cổng đón khách (Boarding Gate) viền tiếp giáp đường xe chạy không.
        /// </summary>
        public bool IsBoardingGate(int col, int row)
        {
            if (col < 0 || col >= columns || row < 0 || row >= rows) return false;
            var roadCfg = WhoGetInBus.Data.LevelConfigLoader.GetRoadConfig(currentRoadId);
            if (roadCfg != null && roadCfg.gateSet != null && roadCfg.gateSet.Count > 0)
            {
                int jsonRow = 39 - row;
                return roadCfg.IsGate(col, jsonRow);
            }
            // Fallback nếu không có gateSet: hàng đáy là cổng
            return row == 0;
        }

    [System.Serializable]
    public struct PassengerAbsorbItem
    {
        public int col;
        public int row;
        public Vector3 worldPos;
    }

        /// <summary>
        /// <summary>
        /// TRÍCH XUẤT ĐÀN HÀNH KHÁCH (Batch Extract chuẩn Cocos 1:1)
        /// Khi xe chạy qua khu vực đón khách tiếp giáp viền Gate hoặc chân tranh cát:
        /// - Tìm hạt cát cùng màu gần xe nhất tại các ô Gate / mép chân tranh cát
        /// - Lan truyền BFS gom cụm đàn hạt cùng màu (tối đa maxBatchCount)
        /// - Trích xuất các hạt ngay lập tức khỏi mảng cát, cập nhật số lượng và kích hoạt sụt lở địa hình
        /// - Trả về danh sách hạt để phóng đàn hành khách bay vào nóc xe
        /// </summary>
        /// <summary>
        /// Chiếu vị trí 3D của xe lên toạ độ ô ma trận (col, row) trên bàn tranh cát (Chuẩn Cocos 1:1)
        /// Chuyển đổi 3D -> Screen -> RectTransform Local -> Grid Cell.
        /// </summary>
        public bool WorldPosToGridCell(Vector3 carWorldPos, out int col, out int row)
        {
            col = -1;
            row = -1;
            Camera cam3D = MainCamera;
            Camera camUI = CameraUI;
            RectTransform rt = GetComponent<RectTransform>();
            if (cam3D == null || rt == null) return false;

            Vector3 screenPos = cam3D.WorldToScreenPoint(carWorldPos);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, camUI, out Vector2 localPoint))
            {
                float spX = config != null ? config.beadSpacingX : (beadSpacingX > 0f ? beadSpacingX : (beadSpacing > 0f ? beadSpacing : 27f));
                float spY = config != null ? config.beadSpacingY : (beadSpacingY > 0f ? beadSpacingY : 24.32f);
                float offX = config != null ? config.boardOffsetX : boardOffsetX;
                float offY = config != null ? config.boardOffsetY : boardOffsetY;
                int totalCols = config != null ? config.columns : columns;
                int totalRows = config != null ? config.rows : rows;

                float startX = -((totalCols - 1) * spX) * 0.5f + offX;
                float startY = spY * 0.5f + offY;

                col = Mathf.RoundToInt((localPoint.x - startX) / spX);
                row = Mathf.RoundToInt((localPoint.y - startY) / spY);
                return col >= -3 && col <= totalCols + 3 && row >= -3 && row <= totalRows + 3;
            }
            return false;
        }

        /// <summary>
        /// TRÍCH XUẤT ĐÀN HÀNH KHÁCH (Batch Extract chuẩn Cocos 1:1)
        /// Khi xe chạy qua khu vực đón khách tiếp giáp viền Gate hoặc chân tranh cát:
        /// - Chiếu vị trí 3D của xe lên ma trận ô SandBoard (carCol, carRow)
        /// - Quét trong bán kính span = 5 ô quanh xe xem có ô Gate nào chứa khách cùng màu không
        /// - Lan truyền BFS gom cụm đàn hạt cùng màu (tối đa maxBatchCount)
        /// - Trích xuất các hạt ngay lập tức khỏi mảng cát, cập nhật số lượng và kích hoạt sụt lở địa hình
        /// - Trả về danh sách hạt để phóng đàn hành khách bay vào nóc xe
        /// </summary>
        public List<PassengerAbsorbItem> TryExtractPassengerBatchNearCar(Car car, Vector3 carWorldPos, float checkRadius, int maxBatchCount)
        {
            List<PassengerAbsorbItem> batch = new List<PassengerAbsorbItem>();
            if (car == null || beadColorIndices == null || maxBatchCount <= 0) return batch;

            int targetColorIdx = (int)car.carColor;
            int startGateCol = -1;
            int startGateRow = -1;

            var roadCfg = WhoGetInBus.Data.LevelConfigLoader.GetRoadConfig(currentRoadId);

            // 1. CHUẨN COCOS 1:1: Chiếu vị trí xe lên hệ lưới tranh cát 2D để tìm cổng Gate đón khách
            if (WorldPosToGridCell(carWorldPos, out int carCol, out int carRow))
            {
                int span = 5; // Bán kính quét 5 ô chuẩn selectAbsorbCandidates trong Cocos
                int colMin = Mathf.Max(0, carCol - span);
                int colMax = Mathf.Min(columns - 1, carCol + span);
                int rowMin = Mathf.Max(0, carRow - span);
                int rowMax = Mathf.Min(rows - 1, carRow + span);

                float bestGridDistSqr = float.MaxValue;

                if (roadCfg != null && roadCfg.gateSet != null && roadCfg.gateSet.Count > 0)
                {
                    foreach (var gateCoord in roadCfg.gateSet)
                    {
                        int gc = gateCoord.x;
                        int gr = 39 - gateCoord.y;

                        if (gc >= colMin && gc <= colMax && gr >= rowMin && gr <= rowMax)
                        {
                            if (beadColorIndices[gc, gr] == targetColorIdx)
                            {
                                float d2 = (gc - carCol) * (gc - carCol) + (gr - carRow) * (gr - carRow);
                                if (d2 < bestGridDistSqr)
                                {
                                    bestGridDistSqr = d2;
                                    startGateCol = gc;
                                    startGateRow = gr;
                                }
                            }
                        }
                    }
                }
            }

            // 2. Fallback nếu chưa thấy theo Grid Cell: quét theo khoảng cách 3D thực tế
            if (startGateCol < 0 && roadCfg != null && roadCfg.gateSet != null)
            {
                float bestDistSqr = checkRadius * checkRadius;
                foreach (var gateCoord in roadCfg.gateSet)
                {
                    int c = gateCoord.x;
                    int r = 39 - gateCoord.y;

                    if (c >= 0 && c < columns && r >= 0 && r < rows)
                    {
                        if (beadColorIndices[c, r] == targetColorIdx)
                        {
                            Vector3 bead3D = GetPassenger3DWorldPosition(c, r);
                            float dx = bead3D.x - carWorldPos.x;
                            float dz = bead3D.z - carWorldPos.z;
                            float distSqr = dx * dx + dz * dz;

                            if (distSqr <= bestDistSqr)
                            {
                                bestDistSqr = distSqr;
                                startGateCol = c;
                                startGateRow = r;
                            }
                        }
                    }
                }
            }

            // 3. Fallback cuối cùng: tìm ở đáy
            if (startGateCol < 0)
            {
                if (TryGetPassengerAtBottom(car.carColor, out _, out _, out int fbCol, out int fbRow))
                {
                    startGateCol = fbCol;
                    startGateRow = fbRow;
                }
            }

            if (startGateCol < 0) return batch;

            // Thu thập theo đàn (BFS Cluster Search) các hạt cát cùng màu kết nối
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            HashSet<int> visited = new HashSet<int>();

            queue.Enqueue(new Vector2Int(startGateCol, startGateRow));
            visited.Add(startGateCol * 1000 + startGateRow);

            int[,] dirs = new int[,] { { 0, 1 }, { 0, -1 }, { 1, 0 }, { -1, 0 }, { 1, 1 }, { -1, 1 }, { 1, -1 }, { -1, -1 } };

            List<Vector2Int> extractedHoles = new List<Vector2Int>();

            while (queue.Count > 0 && batch.Count < maxBatchCount)
            {
                var cur = queue.Dequeue();
                int c = cur.x;
                int r = cur.y;

                batch.Add(new PassengerAbsorbItem
                {
                    col = c,
                    row = r,
                    worldPos = GetPassenger3DWorldPosition(c, r)
                });
                extractedHoles.Add(new Vector2Int(c, r));

                if (batch.Count >= maxBatchCount) break;

                for (int d = 0; d < 8; d++)
                {
                    int nc = c + dirs[d, 0];
                    int nr = r + dirs[d, 1];
                    int key = nc * 1000 + nr;

                    if (nc >= 0 && nc < columns && nr >= 0 && nr < rows && !visited.Contains(key))
                    {
                        visited.Add(key);
                        if (beadColorIndices[nc, nr] == targetColorIdx)
                        {
                            queue.Enqueue(new Vector2Int(nc, nr));
                        }
                    }
                }
            }

            // Trích xuất ngay lập tức các hạt khỏi bàn cát và kích hoạt sụt lở cát địa hình (chuẩn Cocos 1:1)
            foreach (var hole in extractedHoles)
            {
                if (beadColorIndices[hole.x, hole.y] >= 0)
                {
                    beadColorIndices[hole.x, hole.y] = -1;
                    totalRemainingBeads = Mathf.Max(0, totalRemainingBeads - 1);
                    ApplySandTerrainCollapse(hole.x, hole.y);
                }
            }

            RefreshGraphic();

            if (totalRemainingBeads <= 0)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.OnLevelVictory();
                }
            }

            return batch;
        }

        public List<PassengerAbsorbItem> TryAbsorbPassengerBatchNearCar(Car car, Vector3 carWorldPos, float checkRadius, int maxBatchCount)
        {
            return TryExtractPassengerBatchNearCar(car, carWorldPos, checkRadius, maxBatchCount);
        }

        /// <summary>
        /// ĐÓN KHÁCH ĐƠN LẺ (Fallback tương thích).
        /// </summary>
        public bool TryAbsorbPassengerNearCar(Car car, Vector3 carWorldPos, float checkRadius, out Vector3 passengerWorldPos, out int foundCol, out int foundRow)
        {
            passengerWorldPos = Vector3.zero;
            foundCol = -1;
            foundRow = -1;

            var batch = TryAbsorbPassengerBatchNearCar(car, carWorldPos, checkRadius, 1);
            if (batch != null && batch.Count > 0)
            {
                foundCol = batch[0].col;
                foundRow = batch[0].row;
                passengerWorldPos = batch[0].worldPos;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Tìm hạt cùng màu ở hàng đáy để đón lên xe bus (Backward Compatibility Fallback).
        /// </summary>
        public bool TryGetPassengerAtBottom(GameColorType targetColor, out GameObject beadObj, out Vector3 worldPos, out int foundCol, out int foundRow)
        {
            beadObj = null;
            worldPos = Vector3.zero;
            foundCol = -1;
            foundRow = -1;

            if (beadColorIndices == null)
            {
                BuildBoard(GetOrLoadPixelData(), passengerColors, beadMaterial);
            }

            int targetIdx = (int)targetColor;

            List<int> colOrder = new List<int>();
            int center = columns / 2;
            for (int offset = 0; offset <= center; offset++)
            {
                if (center - offset >= 0) colOrder.Add(center - offset);
                if (offset > 0 && center + offset < columns) colOrder.Add(center + offset);
            }

            foreach (int c in colOrder)
            {
                for (int r = 0; r < rows; r++)
                {
                    if (beadColorIndices != null && beadColorIndices[c, r] >= 0)
                    {
                        if (beadColorIndices[c, r] == targetIdx)
                        {
                            foundCol = c;
                            foundRow = r;
                            worldPos = GetPassenger3DWorldPosition(c, r);
                            return true;
                        }
                        break;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Chuyển đổi tọa độ 1 hạt từ UI Screen Space sang tọa độ 3D World (mặt sàn Y = 0.35 đón xe bus).
        /// </summary>
        public Vector3 GetPassenger3DWorldPosition(int col, int row)
        {
            Vector2 uiPos = GetBeadUIPosition(col, row);
            Vector3 uiWorldPos = transform.TransformPoint(new Vector3(uiPos.x, uiPos.y, 0f));

            Camera camUI = CameraUI;
            Camera cam3D = MainCamera;

            if (camUI != null && cam3D != null)
            {
                Vector3 screenPos = camUI.WorldToScreenPoint(uiWorldPos);
                Ray ray = cam3D.ScreenPointToRay(screenPos);
                Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, 0.35f, 0f));
                if (groundPlane.Raycast(ray, out float enter))
                {
                    return ray.GetPoint(enter);
                }
            }
            return uiWorldPos;
        }

        private Camera CameraUI
        {
            get
            {
                if (cachedCameraUI == null)
                {
                    var canvas = GetComponentInParent<Canvas>();
                    if (canvas != null && canvas.worldCamera != null) cachedCameraUI = canvas.worldCamera;
                    else
                    {
                        var go = GameObject.Find("Camera UI 3D");
                        if (go != null) cachedCameraUI = go.GetComponent<Camera>();
                    }
                }
                return cachedCameraUI;
            }
        }

        private Camera MainCamera
        {
            get
            {
                if (cachedMainCamera == null)
                {
                    cachedMainCamera = Camera.main;
                    if (cachedMainCamera == null)
                    {
                        var go = GameObject.Find("Main Camera");
                        if (go != null) cachedMainCamera = go.GetComponent<Camera>();
                    }
                }
                return cachedMainCamera;
            }
        }

        /// <summary>
        /// Tiêu thụ 1 hạt cát và kích hoạt thuật toán sụt lở cát địa hình (SandTerrainCollapse chuẩn Cocos 1:1).
        /// </summary>
        public void ConsumeBead(int c, int r)
        {
            if (c < 0 || c >= columns || r < 0 || r >= rows) return;
            if (beadColorIndices == null || beadColorIndices[c, r] < 0) return;

            beadColorIndices[c, r] = -1;
            totalRemainingBeads = Mathf.Max(0, totalRemainingBeads - 1);

            // Kích hoạt thuật toán sụt lở địa hình tự nhiên
            ApplySandTerrainCollapse(c, r);
            RefreshGraphic();

            if (totalRemainingBeads <= 0)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.OnLevelVictory();
                }
            }
        }

        /// <summary>
        /// THUẬT TOÁN SỤT LỞ CÁT ĐỊA HÌNH CHUẨN GỐC COCOS 1:1 (SandTerrainCollapse).
        /// Cát chảy nghiêng/sụt dốc từ các ô lân cận có địa hình cao hơn lấp vào khoảng trống,
        /// lan truyền theo chuỗi thác đổ tạo dòng chảy cát lún tự nhiên.
        /// </summary>
        private void ApplySandTerrainCollapse(int holeCol, int holeRow)
        {
            if (beadColorIndices == null) return;
            if (collapseVisualOffsets == null) collapseVisualOffsets = new Vector2[columns, rows];
            if (collapseTimers == null) collapseTimers = new float[columns, rows];

            // Ma trận hướng ưu tiên dòng chảy (chuẩn Cocos: primary và secondary)
            // Lưu ý: row trong Unity tăng dần từ đáy lên đỉnh, nên row+1 là hàng trên!
            // Nửa bên trái (col < 20):
            int[,] leftPrimaryDirs = new int[,] { { 1, -1 }, { 1, 0 }, { 1, 1 } }; // chéo trên trái, thẳng trên, chéo trên phải
            int[,] leftSecondaryDirs = new int[,] { { 0, -1 }, { -1, -1 }, { -1, 0 }, { -1, 1 }, { 0, 1 } };

            // Nửa bên phải (col >= 20):
            int[,] rightPrimaryDirs = new int[,] { { 1, 1 }, { 1, 0 }, { 1, -1 } }; // chéo trên phải, thẳng trên, chéo trên trái
            int[,] rightSecondaryDirs = new int[,] { { 0, 1 }, { -1, 1 }, { -1, 0 }, { -1, -1 }, { 0, -1 } };

            int curCol = holeCol;
            int curRow = holeRow;

            // Lặp chuỗi sụt lở (cascade chain) cho đến khi không còn hạt nào trượt xuống được nữa
            for (int step = 0; step < rows; step++)
            {
                int curTerrain = GetTerrainElevation(curCol, curRow);
                bool foundSource = false;
                int srcCol = -1;
                int srcRow = -1;

                bool isLeft = curCol < (columns / 2);
                int[,] primary = isLeft ? leftPrimaryDirs : rightPrimaryDirs;
                int[,] secondary = isLeft ? leftSecondaryDirs : rightSecondaryDirs;

                // 1. Quét hướng ưu tiên chính (Primary: các ô ở hàng trên) với điều kiện terrain >= curTerrain
                for (int d = 0; d < 3; d++)
                {
                    int nc = curCol + primary[d, 1];
                    int nr = curRow + primary[d, 0];
                    if (nc >= 0 && nc < columns && nr >= 0 && nr < rows)
                    {
                        if (beadColorIndices[nc, nr] >= 0)
                        {
                            int nTerrain = GetTerrainElevation(nc, nr);
                            if (nTerrain >= 0 && nTerrain >= curTerrain)
                            {
                                srcCol = nc;
                                srcRow = nr;
                                foundSource = true;
                                break;
                            }
                        }
                    }
                }

                // 2. Quét hướng ưu tiên phụ (Secondary) với điều kiện terrain > curTerrain
                if (!foundSource)
                {
                    for (int d = 0; d < 5; d++)
                    {
                        int nc = curCol + secondary[d, 1];
                        int nr = curRow + secondary[d, 0];
                        if (nc >= 0 && nc < columns && nr >= 0 && nr < rows)
                        {
                            if (beadColorIndices[nc, nr] >= 0)
                            {
                                int nTerrain = GetTerrainElevation(nc, nr);
                                if (nTerrain >= 0 && nTerrain > curTerrain)
                                {
                                    srcCol = nc;
                                    srcRow = nr;
                                    foundSource = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                // 3. Fallback: Nếu không tìm thấy theo địa hình, hạt thẳng đứng ở hàng trên (nr = curRow + 1) rơi xuống
                if (!foundSource)
                {
                    int nr = curRow + 1;
                    if (nr < rows && beadColorIndices[curCol, nr] >= 0)
                    {
                        srcCol = curCol;
                        srcRow = nr;
                        foundSource = true;
                    }
                }

                // Nếu tìm thấy hạt sụt lở: Dời hạt từ (srcCol, srcRow) xuống (curCol, curRow)
                if (foundSource)
                {
                    beadColorIndices[curCol, curRow] = beadColorIndices[srcCol, srcRow];
                    beadColorIndices[srcCol, srcRow] = -1;

                    // Hiệu ứng animation hạt trượt
                    Vector2 oldPos = GetBeadUIPosition(srcCol, srcRow);
                    Vector2 newPos = GetBeadUIPosition(curCol, curRow);
                    collapseVisualOffsets[curCol, curRow] = oldPos - newPos;
                    collapseTimers[curCol, curRow] = CollapseDuration;
                    isCollapsing = true;

                    // Ô nguồn trở thành lỗ trống tiếp theo để tiếp tục hút hạt bên trên xuống
                    curCol = srcCol;
                    curRow = srcRow;
                }
                else
                {
                    break; // Đã lấp đầy toàn bộ chuỗi
                }
            }
        }

        private void UpdateCollapseAnimation()
        {
            if (collapseTimers == null || collapseVisualOffsets == null)
            {
                isCollapsing = false;
                return;
            }

            bool anyActive = false;
            float dt = Time.deltaTime;

            for (int c = 0; c < columns; c++)
            {
                for (int r = 0; r < rows; r++)
                {
                    if (collapseTimers[c, r] > 0f)
                    {
                        collapseTimers[c, r] -= dt;
                        if (collapseTimers[c, r] <= 0f)
                        {
                            collapseTimers[c, r] = 0f;
                            collapseVisualOffsets[c, r] = Vector2.zero;
                        }
                        else
                        {
                            float t = collapseTimers[c, r] / CollapseDuration;
                            // Easing rơi mượt mà
                            collapseVisualOffsets[c, r] *= t;
                            anyActive = true;
                        }
                    }
                }
            }

            isCollapsing = anyActive;
            RefreshGraphic();
        }

        public void RefreshGraphic()
        {
            if (graphic == null) graphic = GetComponent<SandBoardGraphic>();
            if (graphic != null)
            {
                graphic.SetVerticesDirty();
            }
        }

        public void UpdateBeadScalesOnly()
        {
            RefreshGraphic();
        }

        public void UpdateExistingBeads()
        {
            RefreshGraphic();
        }

        [ContextMenu("⚡ Áp Dụng Thông Số Chuẩn Gốc Cocos (1:1)")]
        public void ApplyCocosOriginalSettings()
        {
            if (config != null)
            {
                config.ResetToCocosDefault();
                SyncFromConfig();
            }
            else
            {
                columns = 40;
                rows = 40;
                beadSpacingX = 24.32f;
                beadSpacingY = 24.32f;
                beadSpacing = 24.32f;
                beadSize = new Vector2(34f, 44f);
                beadScale = 1.0f;
                boardOffsetX = 0f;
                boardOffsetY = 0f;
                enableZigzag = true;
                zigzagOffset = 12.16f;
            }

            EnsureAtlasLoaded();
            ClearBoardChildren();
            RefreshGraphic();

#if UNITY_EDITOR
            Undo.RecordObject(this, "Apply Cocos Original Settings");
            EditorUtility.SetDirty(gameObject);
            EditorUtility.SetDirty(this);
            if (config != null)
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
            SceneView.RepaintAll();
#endif
            Debug.Log("<color=green>[SandBoardManager - UI]</color> <b>ĐÃ ÁP DỤNG THÔNG SỐ CHUẨN GỐC COCOS 1:1 VÀ LƯU VÀO CONFIG!</b>");
        }

        [ContextMenu("🔄 Tái Tạo Toàn Bộ Tranh Cát")]
        public void RebuildBoardInEditor()
        {
#if UNITY_EDITOR
            if (passengerColors == null)
            {
                var prefabData = AssetDatabase.LoadAssetAtPath<GamePrefabData>("Assets/Data/GamePrefabData.asset");
                if (prefabData != null)
                {
                    passengerColors = prefabData.passengerColors;
                }
            }
#endif
            int[,] pixelData = GetOrLoadPixelData();
            BuildBoard(pixelData, passengerColors, beadMaterial);

#if UNITY_EDITOR
            EditorUtility.SetDirty(gameObject);
#endif
        }

        public int[,] GetOrLoadPixelData()
        {
            int[,] pixelData = new int[rows, columns];

            if (levelId > 0)
            {
                var levelData = WhoGetInBus.Data.LevelConfigLoader.LoadLevel(levelId);
                if (levelData != null)
                {
                    var collectData = WhoGetInBus.Data.LevelConfigLoader.GetCollect(levelData.collect);
                    int picId = collectData != null ? collectData.picture : levelData.collect;
                    var pixelMap = WhoGetInBus.Data.LevelConfigLoader.LoadPixelMap(picId);
                    if (pixelMap != null)
                    {
                        currentRoadId = levelData.road;
                        for (int r = 0; r < rows; r++)
                        {
                            for (int c = 0; c < columns; c++)
                            {
                                int jsonRow = (rows - 1) - r;
                                pixelData[r, c] = pixelMap.points[c, jsonRow];
                            }
                        }
                        return pixelData;
                    }
                }
            }

            if (!string.IsNullOrEmpty(sandDataString))
            {
                string[] tokens = sandDataString.Split(',');
                if (tokens.Length >= rows * columns)
                {
                    for (int r = 0; r < rows; r++)
                    {
                        for (int c = 0; c < columns; c++)
                        {
                            int idx = r * columns + c;
                            if (idx < tokens.Length && int.TryParse(tokens[idx], out int val))
                            {
                                pixelData[r, c] = val;
                            }
                            else
                            {
                                pixelData[r, c] = -1;
                            }
                        }
                    }
                    return pixelData;
                }
            }

            // Fallback Test Pattern
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    pixelData[r, c] = (r / 4 + c / 4) % 18;
                }
            }
            return pixelData;
        }

        public Color GetPaletteColor(int colorIdx)
        {
            Color[] palette = new Color[]
            {
                new Color(1.000f, 1.000f, 1.000f), // 0: White
                new Color(0.188f, 0.820f, 0.514f), // 1: Green
                new Color(0.000f, 0.878f, 1.000f), // 2: Cyan
                new Color(0.031f, 0.576f, 1.000f), // 3: Blue
                new Color(0.525f, 0.275f, 0.110f), // 4: Brown
                new Color(0.749f, 0.306f, 0.933f), // 5: Purple
                new Color(0.969f, 0.827f, 0.741f), // 6: Powder
                new Color(0.980f, 0.882f, 0.165f), // 7: Yellow
                new Color(0.980f, 0.235f, 0.235f), // 8: Red
                new Color(0.600f, 0.914f, 0.129f), // 9: EmeraldGreen
                new Color(1.000f, 0.561f, 0.000f), // 10: Orange
                new Color(0.208f, 0.208f, 0.208f), // 11: Black
                new Color(0.051f, 0.659f, 0.000f), // 12: DarkGreen
                new Color(0.725f, 0.016f, 0.369f), // 13: Burgundy
                new Color(0.525f, 0.537f, 0.820f), // 14: GrayishBlue
                new Color(0.765f, 0.753f, 0.969f), // 15: LightPurple
                new Color(0.988f, 0.416f, 0.918f), // 16: Pink
                new Color(0.729f, 0.961f, 0.851f)  // 17: Teal
            };

            if (colorIdx >= 0 && colorIdx < palette.Length) return palette[colorIdx];
            return Color.white;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            try
            {
                int roadId = currentRoadId > 0 ? currentRoadId : (levelId > 0 ? WhoGetInBus.Data.LevelConfigLoader.LoadLevel(levelId)?.road ?? 5 : 5);
                var roadCfg = WhoGetInBus.Data.LevelConfigLoader.GetRoadConfig(roadId);

                if (roadCfg != null && roadCfg.gateSet != null && roadCfg.gateSet.Count > 0)
                {
                    Gizmos.color = new Color(1f, 0.45f, 0f, 0.95f); // Màu cam rực rỡ cho cổng đón khách (Boarding Gate)
                    Vector3 prevPos = Vector3.zero;
                    bool hasPrev = false;

                    foreach (var gateCoord in roadCfg.gateSet)
                    {
                        int c = gateCoord.x;
                        int r = 39 - gateCoord.y;
                        if (c >= 0 && c < columns && r >= 0 && r < rows)
                        {
                            Vector3 pos = GetPassenger3DWorldPosition(c, r);

                            // Vẽ khối Gate 3D tại từng điểm tiếp giáp đường xe
                            Gizmos.DrawWireCube(pos, new Vector3(0.28f, 0.15f, 0.28f));
                            Gizmos.DrawSphere(pos, 0.1f);

                            if (hasPrev && Vector3.Distance(prevPos, pos) < 1.5f)
                            {
                                Gizmos.DrawLine(prevPos, pos);
                            }
                            prevPos = pos;
                            hasPrev = true;
                        }
                    }
                }
            }
            catch { }
        }
#endif
    }
}
