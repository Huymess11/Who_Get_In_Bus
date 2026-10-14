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
    /// Quản lý Bảng Tranh Cát (Sandboard) chuẩn 1:1 theo Cocos Creator gốc (PersonView / TargetController / FlowSystem / MovementSystem).
    /// Toàn bộ hiển thị chuyển sang hệ thống UGUI (SandBoardGraphic) vẽ bằng 1 Draw Call duy nhất,
    /// KHÔNG sinh 1.300+ GameObject con giúp game chạy cực mượt 60-120 FPS trên mobile.
    ///
    /// Component này chỉ lo phần "dính" với Unity (cấu hình, chiếu xe lên bàn cát, hoạt hoạ trượt, vẽ).
    /// LOGIC cát nằm ở SandBoardLogic (chọn hạt ăn, cát chảy, hàng đợi) + SandTerrainCollapse (thuật toán sụt lở địa hình):
    ///  - ĂN CÁT: xe chiếu điểm neo lên bàn cát, cần có ô cổng cùng màu trong cửa sổ ±5 ô, rồi lấy các hạt cùng màu trong cửa sổ
    ///    ưu tiên terrain THẤP nhất (gần đường nhất) → khoảng cách → hàng sâu nhất (TargetController.selectAbsorbCandidates).
    ///    Cuối màn (hết xe trong bãi) chuyển sang chế độ "relaxed" quét vòng tròn mở rộng.
    ///  - CÁT CHẢY: sụt lở theo địa hình + hàng đợi (queue) bổ sung hạt mới từ đỉnh bàn cát cột [16, 24).
    ///  - CHUYỂN ĐỘNG: mỗi hạt trượt từ ô cũ sang ô mới theo "sóng" delay/duration + smoothstep (MovementSystem), tối đa 48 hạt/đợt.
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

        [Header("Cát Chảy (Cocos SandTerrainCollapse / MovementSystem)")]
        [Tooltip("passengerCollapseBottomRowRatio: tỉ lệ các hàng sát đáy dùng luật terrain nghiêm ngặt (chuẩn 25%)")]
        [Range(0f, 1f)]
        public float collapseBottomRowRatio = 0.25f;
        [Tooltip("passengerCollapseMaxConcurrent: số hạt tối đa được trượt hoạt hoạ cùng lúc trong 1 đợt sụt lở (phần dư nhảy thẳng tới đích)")]
        public int collapseMaxConcurrent = 48;
        [Tooltip("minQueueCol: cột đầu tiên (gồm) của khe tiếp cát phía trên bàn cát")]
        public int minQueueCol = 16;
        [Tooltip("maxQueueCol: cột cuối (không gồm) của khe tiếp cát phía trên bàn cát")]
        public int maxQueueCol = 24;

        [Header("Ăn Cát (Cocos TargetController)")]
        [Tooltip("Bán kính cửa sổ quét quanh xe tính theo ô (chuẩn Cocos: 5)")]
        public int absorbGridSpan = 5;
        [Tooltip("Dùng điểm neo (nhãn số ghế) của xe để chiếu lên bàn cát như Cocos; tắt = dùng vị trí chân xe")]
        public bool useCarBoardAnchor = true;

        [Header("Prefab & Material (Backward Compatibility)")]
        public GameObject beadPrefab;
        public Material beadMaterial;
        public PassengerColorData passengerColors;
        public Texture2D passengerAtlas;

        // Ma trận màu sắc hiển thị: beadColorIndices[col, row] (row 0 = đáy sát đường xe; <0: rỗng/khoá; 0..17: mã màu)
        // Đây chỉ là bản sao hướng Unity của lưới logic (nguồn sự thật nằm ở SandBoardLogic, hướng Cocos).
        private int[,] beadColorIndices;

        private readonly SandBoardLogic logic = new SandBoardLogic();

        // Hoạt hoạ trượt: mỗi hạt đang trượt ghi offset tại ô ĐÍCH của nó
        private struct SandMotion
        {
            public int col;
            public int uRow;
            public Vector2 fromPos;
            public Vector2 toPos;
            public float elapsed;
            public float delay;
            public float duration;
        }
        private Vector2[,] collapseVisualOffsets;
        private int[,] motionIndex;
        private readonly List<SandMotion> motions = new List<SandMotion>(128);
        private readonly Dictionary<int, SandMoveTiming> timingScratch = new Dictionary<int, SandMoveTiming>(128);
        private readonly List<Vector2Int> selectedCells = new List<Vector2Int>(32);
        private static readonly Vector2Int[] noGates = new Vector2Int[0];

        private SandBoardGraphic graphic;
        private Camera cachedCameraUI;
        private Camera cachedMainCamera;

        /// <summary>Tổng số hạt còn lại = hạt trên bàn cát + hạt còn trong hàng đợi chờ rơi xuống.</summary>
        public int TotalRemainingBeads => logic.boardBeadCount + logic.queueColors.Count;
        public int BoardBeadCount => logic.boardBeadCount;
        public int QueueRemaining => logic.queueColors.Count;
        public int[,] BeadColorIndices => beadColorIndices;
        public SandBoardLogic Logic => logic;

        /// <summary>Có hạt nào đang trượt (sóng sụt lở chưa kết thúc) không.</summary>
        public bool HasActiveCollapseMotion => motions.Count > 0;

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

            if (transform.parent != null && transform.parent.childCount > 1 && transform.GetSiblingIndex() != 1)
            {
                transform.SetSiblingIndex(1);
            }

            if (beadColorIndices == null || logic.boardBeadCount == 0)
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
            if (!Application.isPlaying) return;
            if (motions.Count > 0)
            {
                TickMotions(Time.deltaTime);
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
            motionIndex = new int[columns, rows];
            for (int c = 0; c < columns; c++)
                for (int r = 0; r < rows; r++)
                    motionIndex[c, r] = -1;
            motions.Clear();

            // Tranh theo hệ Cocos [row (0 = đỉnh), col]
            int[,] pixelCocos = new int[rows, columns];
            for (int cRow = 0; cRow < rows; cRow++)
            {
                int uRow = rows - 1 - cRow;
                for (int c = 0; c < columns; c++)
                {
                    int colorIdx = -1;
                    if (pixelData != null && uRow < pixelData.GetLength(0) && c < pixelData.GetLength(1)) colorIdx = pixelData[uRow, c];
                    pixelCocos[cRow, c] = colorIdx;
                }
            }

            // Lưới logic + địa hình + cổng đón khách (Cocos: initStaticSandTerrain / initPrimaryScreenColor)
            ApplyLogicSettings();
            var roadCfg = WhoGetInBus.Data.LevelConfigLoader.GetRoadConfig(currentRoadId);
            logic.Build(columns, rows, roadCfg != null ? roadCfg.points : null,
                roadCfg != null ? (IEnumerable<Vector2Int>)roadCfg.gateSet : noGates, enableRoadCutout, pixelCocos);
            logic.isCellLocked = IsCellMoving;

            // Hàng đợi bổ sung hạt từ đỉnh bàn cát (Cocos: initRemainingColors)
            if (levelId > 0)
            {
                var level = WhoGetInBus.Data.LevelConfigLoader.LoadLevel(levelId);
                if (level != null) logic.SetupQueue(level.queue, level.exchangeRatio);
            }

            SyncMirror();

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    sb.Append(Mathf.Max(-1, beadColorIndices[c, r])).Append(",");
                }
            }
            if (sb.Length > 0) sb.Length--;
            sandDataString = sb.ToString();

            RefreshGraphic();

            Debug.Log($"<color=cyan>[SandBoardManager - UI]</color> Đã nạp thành công {logic.boardBeadCount} hạt cát + {logic.queueColors.Count} hạt trong hàng đợi lên UI! (Scale: {beadScale:F2}, 1 Draw Call, 0 GameObject con)");
        }

        private void ApplyLogicSettings()
        {
            logic.bottomRowRatio = collapseBottomRowRatio;
            logic.minQueueCol = minQueueCol;
            logic.maxQueueCol = maxQueueCol;
        }

        /// <summary>Ô (col, row hệ Cocos) có hạt đang trượt hoạt hoạ không.</summary>
        private bool IsCellMoving(int col, int cRow)
        {
            return motionIndex != null && motionIndex[col, rows - 1 - cRow] >= 0;
        }

        /// <summary>Chép lưới logic (hướng Cocos) sang ma trận hiển thị hướng Unity.</summary>
        private void SyncMirror()
        {
            var grid = logic.grid;
            if (grid == null || beadColorIndices == null) return;
            for (int cRow = 0; cRow < rows; cRow++)
            {
                int u = rows - 1 - cRow;
                for (int c = 0; c < columns; c++)
                {
                    beadColorIndices[c, u] = grid.flags[cRow, c];
                }
            }
        }

        public void ClearBoard()
        {
            ClearBoardChildren();
            motions.Clear();
            logic.ClearAllBeads();
            if (motionIndex != null)
            {
                for (int c = 0; c < columns; c++)
                    for (int r = 0; r < rows; r++)
                    {
                        motionIndex[c, r] = -1;
                        if (collapseVisualOffsets != null) collapseVisualOffsets[c, r] = Vector2.zero;
                    }
            }
            SyncMirror();
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
        /// Lấy độ cao địa hình (Terrain Elevation) từ ma trận points của RoadConfigData.
        /// Chuẩn Cocos: giá trị >= 0 là độ cao, -1 là rãnh khoét (vật cản).
        /// </summary>
        public int GetTerrainElevation(int col, int row)
        {
            if (col < 0 || col >= columns || row < 0 || row >= rows) return -1;
            if (logic.grid != null) return logic.grid.terrain[rows - 1 - row, col];

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

        #region CHIẾU XE LÊN BÀN CÁT

        private bool TryProjectToCell(Vector3 worldPos, out int col, out int row)
        {
            col = -1;
            row = -1;
            Camera cam3D = MainCamera;
            Camera camUI = CameraUI;
            RectTransform rt = GetComponent<RectTransform>();
            if (cam3D == null || rt == null) return false;

            Vector3 screenPos = cam3D.WorldToScreenPoint(worldPos);
            if (screenPos.z < 0f) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, camUI, out Vector2 localPoint)) return false;

            float spX = config != null ? config.beadSpacingX : (beadSpacingX > 0f ? beadSpacingX : (beadSpacing > 0f ? beadSpacing : 27f));
            float spY = config != null ? config.beadSpacingY : (beadSpacingY > 0f ? beadSpacingY : 24.32f);
            float offX = config != null ? config.boardOffsetX : boardOffsetX;
            float offY = config != null ? config.boardOffsetY : boardOffsetY;
            int totalCols = config != null ? config.columns : columns;

            float startX = -((totalCols - 1) * spX) * 0.5f + offX;
            float startY = spY * 0.5f + offY;

            col = Mathf.RoundToInt((localPoint.x - startX) / spX);
            row = Mathf.RoundToInt((localPoint.y - startY) / spY);
            return true;
        }

        /// <summary>
        /// Chiếu vị trí 3D của xe lên toạ độ ô ma trận (col, row) trên bàn tranh cát (Chuẩn Cocos 1:1)
        /// Chuyển đổi 3D -> Screen -> RectTransform Local -> Grid Cell.
        /// </summary>
        public bool WorldPosToGridCell(Vector3 carWorldPos, out int col, out int row)
        {
            if (!TryProjectToCell(carWorldPos, out col, out row)) return false;
            int totalCols = config != null ? config.columns : columns;
            int totalRows = config != null ? config.rows : rows;
            return col >= -3 && col <= totalCols + 3 && row >= -3 && row <= totalRows + 3;
        }

        #endregion

        #region ĂN CÁT (Cocos TargetController.extractPixelsByGridSpan)

        /// <summary>
        /// TRÍCH XUẤT KHÁCH CHO 1 XE — chuẩn Cocos TargetController.extractPixelsByGridSpan + FlowSystem.
        ///
        /// 1. Chiếu điểm neo của xe lên bàn cát → ô (cx, cy).
        /// 2. Chế độ thường: phải có ít nhất 1 ô cổng cùng màu trong cửa sổ ±absorbGridSpan; chọn các hạt cùng màu (không đang trượt)
        ///    trong cửa sổ, ưu tiên terrain THẤP → gần xe → hàng sâu → lệch cột ít → cột nhỏ.
        /// 3. Chế độ relaxed (hết xe trong bãi): quét vòng tròn mở rộng, lấy hạt đầu tiên rồi chuỗi hạt sẽ trượt vào chỗ đó.
        /// 4. Xoá các ô đã chọn, chạy cát chảy (sụt lở + bổ sung từ hàng đợi) và kích hoạt hoạt hoạ trượt.
        /// Trả về số hạt đã lấy; items chứa ô + toạ độ 3D để phóng khách bay lên xe.
        /// </summary>
        public int ExtractPassengersForCar(Car car, int maxCount, bool boardEmptyMode, bool relaxedUnlocked, List<PassengerAbsorbItem> items)
        {
            if (car == null || logic.grid == null || maxCount <= 0) return 0;

            Vector3 anchor = useCarBoardAnchor ? car.GetBoardAnchorWorldPos() : car.transform.position;
            bool anchorOk = TryProjectToCell(anchor, out int cx, out int uRow);
            int cy = rows - 1 - uRow;

            ApplyLogicSettings();
            int color = (int)car.carColor;
            selectedCells.Clear();
            if (boardEmptyMode)
            {
                if (!anchorOk) return 0;
                int span = relaxedUnlocked ? columns : absorbGridSpan * 2;
                logic.SelectRelaxedCells(color, maxCount, cx, cy, span, selectedCells);
            }
            else
            {
                if (anchorOk) logic.SelectStrictCells(color, maxCount, cx, cy, absorbGridSpan, selectedCells);

                // Dự phòng: điểm neo trên nóc xe chiếu lệch khỏi cổng thì thử lại bằng vị trí chân xe
                if (selectedCells.Count == 0 && useCarBoardAnchor && TryProjectToCell(car.transform.position, out int gx, out int gURow))
                {
                    int gy = rows - 1 - gURow;
                    if (!anchorOk || gx != cx || gy != cy) logic.SelectStrictCells(color, maxCount, gx, gy, absorbGridSpan, selectedCells);
                }
            }

            if (selectedCells.Count == 0) return 0;
            return RemoveCellsAndFlow(selectedCells, items);
        }

        /// <summary>
        /// Xoá các ô (hệ Cocos: x = col, y = row) khỏi bàn cát, chạy cát chảy và kích hoạt hoạt hoạ.
        /// Cocos: FlowSystem.clearHolesForChain + extractChainForBoarding + processPendingHoleList.
        /// </summary>
        private int RemoveCellsAndFlow(List<Vector2Int> cells, List<PassengerAbsorbItem> items)
        {
            for (int i = 0; i < cells.Count; i++) FinishMotionAt(cells[i].x, rows - 1 - cells[i].y);

            int removed = logic.RemoveCellsAndFlow(cells);
            if (removed <= 0) return 0;

            if (items != null)
            {
                var gone = logic.removedCells;
                for (int i = 0; i < gone.Count; i++)
                {
                    int uRow = rows - 1 - gone[i].y;
                    items.Add(new PassengerAbsorbItem
                    {
                        col = gone[i].x,
                        row = uRow,
                        worldPos = GetPassenger3DWorldPosition(gone[i].x, uRow)
                    });
                }
            }

            ScheduleVisualMoves(logic.visualMoves);
            SyncMirror();
            RefreshGraphic();
            CheckVictory();
            return removed;
        }

        private void CheckVictory()
        {
            if (logic.boardBeadCount <= 0 && logic.queueColors.Count == 0)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.OnLevelVictory();
                }
            }
        }

        #endregion

        #region HOẠT HOẠ TRƯỢT (Cocos MovementSystem / SandCollapseVisual)

        private void ScheduleVisualMoves(List<SandCollapseMove> moves)
        {
            if (moves.Count == 0) return;

            // Hạt đang trượt mà lại bị dịch chuyển tiếp: chốt nó về đích cũ rồi mới trượt tiếp (cancelCollapseMotionsForPersons)
            for (int i = 0; i < moves.Count; i++)
            {
                if (moves[i].fromRow >= 0) FinishMotionAt(moves[i].fromCol, rows - 1 - moves[i].fromRow);
            }

            SandTerrainCollapse.ComputeWaveTimings(moves, timingScratch, columns);

            int scheduled = 0;
            int topIndex = 0;
            int maxConcurrent = Mathf.Max(1, collapseMaxConcurrent);
            for (int i = 0; i < moves.Count; i++)
            {
                if (scheduled >= maxConcurrent) break;
                var m = moves[i];
                if (m.IsStationary) continue;

                float dx = m.toCol - m.fromCol;
                float dy = m.toRow - m.fromRow;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                SandMoveTiming timing;
                if (m.fromRow < 0) timing = SandTerrainCollapse.ComputeTopRefillTiming(topIndex++, dist);
                else if (!timingScratch.TryGetValue(m.occupant, out timing)) timing = new SandMoveTiming { delay = 0f, duration = 0.16f };

                int toU = rows - 1 - m.toRow;
                int fromU = m.fromRow < 0 ? rows : rows - 1 - m.fromRow; // hàng "-1" của Cocos = ngay phía trên đỉnh bàn cát
                AddMotion(m.toCol, toU, GetBeadUIPosition(m.fromCol, fromU), GetBeadUIPosition(m.toCol, toU), timing.delay, timing.duration);
                scheduled++;
            }
        }

        private void AddMotion(int col, int uRow, Vector2 fromPos, Vector2 toPos, float delay, float duration)
        {
            FinishMotionAt(col, uRow);

            var motion = new SandMotion
            {
                col = col,
                uRow = uRow,
                fromPos = fromPos,
                toPos = toPos,
                elapsed = 0f,
                delay = delay,
                duration = Mathf.Max(0.01f, duration)
            };
            motionIndex[col, uRow] = motions.Count;
            motions.Add(motion);
            collapseVisualOffsets[col, uRow] = fromPos - toPos;
        }

        /// <summary>Chốt hạt đang trượt tại ô đích (col, uRow) về đúng vị trí.</summary>
        private void FinishMotionAt(int col, int uRow)
        {
            if (motionIndex == null || col < 0 || col >= columns || uRow < 0 || uRow >= rows) return;
            int idx = motionIndex[col, uRow];
            if (idx < 0) return;
            RemoveMotionAt(idx);
        }

        private void RemoveMotionAt(int index)
        {
            var m = motions[index];
            collapseVisualOffsets[m.col, m.uRow] = Vector2.zero;
            motionIndex[m.col, m.uRow] = -1;

            int last = motions.Count - 1;
            if (index != last)
            {
                var moved = motions[last];
                motions[index] = moved;
                motionIndex[moved.col, moved.uRow] = index;
            }
            motions.RemoveAt(last);
        }

        private void TickMotions(float dt)
        {
            for (int i = motions.Count - 1; i >= 0; i--)
            {
                var m = motions[i];
                m.elapsed += dt;

                if (m.elapsed < m.delay)
                {
                    collapseVisualOffsets[m.col, m.uRow] = m.fromPos - m.toPos;
                    motions[i] = m;
                    continue;
                }

                float t = Mathf.Clamp01((m.elapsed - m.delay) / m.duration);
                if (t >= 1f)
                {
                    RemoveMotionAt(i);
                    continue;
                }

                float smooth = t * t * (3f - 2f * t);
                collapseVisualOffsets[m.col, m.uRow] = (m.fromPos - m.toPos) * (1f - smooth);
                motions[i] = m;
            }

            RefreshGraphic();
        }

        #endregion

        #region API TƯƠNG THÍCH NGƯỢC

        /// <summary>
        /// Trích xuất đàn hành khách cho xe (API cũ). Dùng đúng thuật toán Cocos (ExtractPassengersForCar).
        /// checkRadius không còn dùng (Cocos dùng cửa sổ ô thay vì bán kính 3D).
        /// </summary>
        public List<PassengerAbsorbItem> TryExtractPassengerBatchNearCar(Car car, Vector3 carWorldPos, float checkRadius, int maxBatchCount)
        {
            var batch = new List<PassengerAbsorbItem>();
            ExtractPassengersForCar(car, maxBatchCount, false, false, batch);
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
        /// Tiêu thụ 1 hạt cát (toạ độ hướng Unity) và kích hoạt thuật toán cát chảy (SandTerrainCollapse chuẩn Cocos 1:1).
        /// </summary>
        public void ConsumeBead(int c, int r)
        {
            if (logic.grid == null || c < 0 || c >= columns || r < 0 || r >= rows) return;
            if (beadColorIndices == null || beadColorIndices[c, r] < 0) return;

            ApplyLogicSettings();
            selectedCells.Clear();
            selectedCells.Add(new Vector2Int(c, rows - 1 - r));
            RemoveCellsAndFlow(selectedCells, null);
        }

        #endregion

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
            if (transform.parent != null && transform.parent.childCount > 1 && transform.GetSiblingIndex() != 1)
            {
                transform.SetSiblingIndex(1);
            }
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
