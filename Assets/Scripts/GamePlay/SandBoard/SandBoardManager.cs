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
        [Tooltip("Level ID chuẩn gốc Cocos (1001, 1002, 1003...)")]
        public int levelId = 1001;
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
        /// Nạp và sinh trực tiếp tranh cát từ Level ID chuẩn gốc (1001, 1002, ...)
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
        /// Tiêu thụ 1 hạt cát và kích hoạt hiệu ứng sạt lở (Collapse) rơi xuống mượt mà.
        /// </summary>
        public void ConsumeBead(int c, int r)
        {
            if (c < 0 || c >= columns || r < 0 || r >= rows) return;
            if (beadColorIndices == null || beadColorIndices[c, r] < 0) return;

            beadColorIndices[c, r] = -1;
            totalRemainingBeads = Mathf.Max(0, totalRemainingBeads - 1);

            CollapseColumn(c, r);
            RefreshGraphic();

            if (totalRemainingBeads <= 0)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.OnLevelVictory();
                }
            }
        }

        private void CollapseColumn(int c, int startR)
        {
            if (beadColorIndices == null) return;
            if (collapseVisualOffsets == null) collapseVisualOffsets = new Vector2[columns, rows];
            if (collapseTimers == null) collapseTimers = new float[columns, rows];

            for (int r = startR + 1; r < rows; r++)
            {
                if (beadColorIndices[c, r] >= 0)
                {
                    int targetRow = r - 1;
                    beadColorIndices[c, targetRow] = beadColorIndices[c, r];
                    beadColorIndices[c, r] = -1;

                    // Tính offset hình ảnh từ vị trí cũ rơi xuống vị trí mới
                    Vector2 oldPos = GetBeadUIPosition(c, r);
                    Vector2 newPos = GetBeadUIPosition(c, targetRow);
                    collapseVisualOffsets[c, targetRow] = oldPos - newPos;
                    collapseTimers[c, targetRow] = CollapseDuration;
                    isCollapsing = true;
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
    }
}
