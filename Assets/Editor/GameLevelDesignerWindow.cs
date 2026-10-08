using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using DouyinGame.GamePlay;

namespace GameLevelDesign.Editor
{
    public class GameLevelDesignerWindow : EditorWindow
    {
        [MenuItem("Tools/Game Level Designer (Bộ Thiết Kế Màn Chơi Toàn Diện)")]
        public static void OpenWindow()
        {
            var window = GetWindow<GameLevelDesignerWindow>("Level Designer Pro");
            window.minSize = new Vector2(850, 950);
            window.Show();
        }

        public static void OpenWindowToTab(int tabIndex)
        {
            var window = GetWindow<GameLevelDesignerWindow>("Level Designer Pro");
            window.minSize = new Vector2(850, 950);
            window.currentTab = tabIndex;
            window.Show();
        }

        #region BẢNG 18 MÀU CHUẨN
        public static readonly Color[] StandardPalette = new Color[]
        {
            new Color(1.000f, 1.000f, 1.000f), // 0: White
            new Color(0.188f, 0.820f, 0.514f), // 1: Green
            new Color(0.275f, 0.851f, 1.000f), // 2: Cyan
            new Color(0.161f, 0.510f, 0.957f), // 3: Blue
            new Color(0.396f, 0.224f, 0.176f), // 4: Brown
            new Color(0.714f, 0.447f, 0.992f), // 5: Purple
            new Color(1.000f, 0.792f, 0.663f), // 6: Powder
            new Color(1.000f, 0.875f, 0.282f), // 7: Yellow
            new Color(1.000f, 0.322f, 0.357f), // 8: Red
            new Color(0.600f, 0.900f, 0.200f), // 9: EmeraldGreen
            new Color(1.000f, 0.500f, 0.100f), // 10: Orange
            new Color(0.150f, 0.150f, 0.150f), // 11: Black
            new Color(0.100f, 0.500f, 0.200f), // 12: DarkGreen
            new Color(0.600f, 0.100f, 0.100f), // 13: Burgundy
            new Color(0.400f, 0.500f, 0.600f), // 14: GrayishBlue
            new Color(0.800f, 0.600f, 0.900f), // 15: LightPurple
            new Color(0.950f, 0.400f, 0.700f), // 16: Pink
            new Color(0.100f, 0.850f, 0.850f)  // 17: Teal
        };

        public static readonly string[] ColorNames = new string[]
        {
            "White (0)", "Green (1)", "Cyan (2)", "Blue (3)", "Brown (4)", "Purple (5)",
            "Powder (6)", "Yellow (7)", "Red (8)", "EmeraldGreen (9)", "Orange (10)", "Black (11)",
            "DarkGreen (12)", "Burgundy (13)", "GrayishBlue (14)", "LightPurple (15)", "Pink (16)", "Teal (17)"
        };
        #endregion

        #region BIẾN TRẠNG THÁI & ĐIỀU HÀNH
        private int currentTab = 0;
        private readonly string[] tabTitles = new string[]
        {
            "🚗 1. Bãi Đỗ Xe & Cân Bằng",
            "🎨 2. Tranh Cát 40×40 (Canvas Pro)",
            "🛣️ 3. Đường Chạy Xe (Track)",
            "▶️ 4. Chơi Thử Trực Tiếp (Simulator)",
            "📐 5. Camera & Bàn Cát 3D (Cố Định & Fit)"
        };

        private int levelId = 1;
        private Texture2D inputPixelArt;

        // Bãi Đỗ Xe
        private int gridRows = 5;
        private int gridCols = 5;
        private string[,] carGrid = new string[5, 5];
        private int selectedCellRow = 0;
        private int selectedCellCol = 0;
        private bool isBrushPaintingMode = false;
        private string copiedCarData = "-1_0_0";

        // Thuộc tính xe đang chọn
        private GameColorType selectedColor = GameColorType.Yellow;
        private int selectedCapacity = 100;
        private int selectedMechanic = 0;
        private int autoBaseCapacity = 100;

        // Tranh cát 40x40
        private int[,] pixelMapData = new int[40, 40];
        private Dictionary<int, int> sandColorCounts = new Dictionary<int, int>();
        private Texture2D sandCanvasTex;
        private Color32[] sandPixels = new Color32[40 * 40];

        // Công cụ vẽ tranh cát
        public enum SandDrawTool { Pencil, BucketFill, Eraser, Eyedropper }
        private SandDrawTool currentDrawTool = SandDrawTool.Pencil;
        private int brushSize = 1; // 1, 2, 3
        private int hoveredCellX = -1;
        private int hoveredCellY = -1;
        private Stack<int[,]> undoStack = new Stack<int[,]>();

        // Thiết lập đường chạy (Track & Gate)
        private float trackLoopRadius = 5.2f;
        private float trackGateZ = 6.2f;

        private Vector2 mainScrollPos;

        #region CẤU HÌNH CAMERA CỐ ĐỊNH & THÍCH ỨNG TỈ LỆ
        private Vector3 camPosition = new Vector3(0f, 18.5f, -3.5f);
        private Vector3 camRotation = new Vector3(53f, 0f, 0f);
        private float camBaseOrthoSize = 15f;
        private Vector2 camRefResolution = new Vector2(750f, 1334f);
        private CameraResolutionAdapter.AspectFitMode camFitMode = CameraResolutionAdapter.AspectFitMode.FitAll;
        private bool camLockTransform = true;
        #endregion

        #region CẤU HÌNH BÀN CÁT 3D (SAND BOARD 40x40 & VỊ TRÍ)
        private Vector3 sandBoardPos = new Vector3(0f, 0f, 6.0f);
        private int sandColumns = 40;
        private int sandRows = 40;
        private float sandBeadSpacing = 0.5f;
        private float sandBeadRadius = 0.13f;
        private float sandBeadScale = 0.4f;
        private bool sandEnableZigzag = true;
        private float sandZigzagOffset = 0.25f;
        private bool sandEnableRoadCutout = true;
        private int sandCutoutColMin = 14;
        private int sandCutoutColMax = 25;
        private int sandCutoutRowMax = 18;
        #endregion

        #region KHÓA EDITORPREFS (LƯU TRỮ VĨNH VIỄN)
        private const string PREF_KEY_CAM_POS_X = "DGL_CamPosX";
        private const string PREF_KEY_CAM_POS_Y = "DGL_CamPosY";
        private const string PREF_KEY_CAM_POS_Z = "DGL_CamPosZ";
        private const string PREF_KEY_CAM_ROT_X = "DGL_CamRotX";
        private const string PREF_KEY_CAM_ROT_Y = "DGL_CamRotY";
        private const string PREF_KEY_CAM_ROT_Z = "DGL_CamRotZ";
        private const string PREF_KEY_CAM_ORTHO = "DGL_CamOrthoSize";
        private const string PREF_KEY_CAM_REF_W = "DGL_CamRefW";
        private const string PREF_KEY_CAM_REF_H = "DGL_CamRefH";
        private const string PREF_KEY_CAM_FIT_MODE = "DGL_CamFitMode";
        private const string PREF_KEY_CAM_LOCK = "DGL_CamLockTransform";

        private const string PREF_KEY_BOARD_POS_X = "DGL_BoardPosX";
        private const string PREF_KEY_BOARD_POS_Y = "DGL_BoardPosY";
        private const string PREF_KEY_BOARD_POS_Z = "DGL_BoardPosZ";
        private const string PREF_KEY_BEAD_SPACING = "DGL_BeadSpacing";
        private const string PREF_KEY_BEAD_RADIUS = "DGL_BeadRadius";
        private const string PREF_KEY_BEAD_SCALE = "DGL_BeadScale";
        private const string PREF_KEY_ENABLE_ZIGZAG = "DGL_EnableZigzag";
        private const string PREF_KEY_ZIGZAG_OFFSET = "DGL_ZigzagOffset";
        private const string PREF_KEY_ENABLE_CUTOUT = "DGL_EnableCutout";
        private const string PREF_KEY_CUTOUT_COL_MIN = "DGL_CutoutColMin";
        private const string PREF_KEY_CUTOUT_COL_MAX = "DGL_CutoutColMax";
        private const string PREF_KEY_CUTOUT_ROW_MAX = "DGL_CutoutRowMax";
        #endregion
        #endregion

        #region LIFECYCLE
        private void OnEnable()
        {
            wantsMouseMove = true;

            LoadSettingsFromEditorPrefs();

            if (carGrid == null || carGrid.GetLength(0) != gridRows || carGrid.GetLength(1) != gridCols)
            {
                InitGrid(gridRows, gridCols);
            }

            EnsureSandCanvasTexture();

            if (sandColorCounts.Count == 0)
            {
                // Mặc định nạp mẫu Dưa Hấu (Watermelon Level 5) chuẩn Douyin từ ảnh của người dùng!
                LoadWatermelonPreset();
            }
        }

        private void OnDisable()
        {
            SaveSettingsToEditorPrefs();

            if (sandCanvasTex != null)
            {
                DestroyImmediate(sandCanvasTex);
                sandCanvasTex = null;
            }
        }
        #endregion

        #region GIAO DIỆN CHÍNH (OnGUI)
        private void OnGUI()
        {
            DrawMainHeader();

            mainScrollPos = EditorGUILayout.BeginScrollView(mainScrollPos);
            EditorGUILayout.Space(6);

            switch (currentTab)
            {
                case 0:
                    DrawParkingLotTab();
                    break;
                case 1:
                    DrawSandCanvasProTab();
                    break;
                case 2:
                    DrawTrackEditorTab();
                    break;
                case 3:
                    DrawPlayTestTab();
                    break;
                case 4:
                    DrawCameraAndBoardTab();
                    break;
            }

            EditorGUILayout.Space(30);
            EditorGUILayout.EndScrollView();

            // Lắng nghe phím tắt hoàn tác Ctrl+Z
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.control && e.keyCode == KeyCode.Z)
            {
                PerformUndo();
                e.Use();
            }
        }

        private void DrawMainHeader()
        {
            // Banner trên cùng phong cách Hiện đại & Chuyên nghiệp
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, normal = { textColor = new Color(0.1f, 0.85f, 1.0f) } };
            GUILayout.Label("🎮 DOUYIN LEVEL DESIGNER PRO", titleStyle);

            GUILayout.FlexibleSpace();

            // Level ID Stepper
            GUILayout.Label("Level:", EditorStyles.boldLabel);
            if (GUILayout.Button("◀", GUILayout.Width(24), GUILayout.Height(22)))
            {
                levelId = Mathf.Max(1, levelId - 1);
            }
            levelId = EditorGUILayout.IntField(levelId, GUILayout.Width(40), GUILayout.Height(22));
            if (GUILayout.Button("▶", GUILayout.Width(24), GUILayout.Height(22)))
            {
                levelId++;
            }

            GUILayout.Space(6);

            // Nút Nạp Mẫu Dưa Hấu Nhanh
            GUI.backgroundColor = new Color(0.95f, 0.35f, 0.45f);
            if (GUILayout.Button("🍉 Mẫu Dưa Hấu (Màn 5)", GUILayout.Height(22), GUILayout.Width(155)))
            {
                LoadWatermelonPreset();
            }
            GUI.backgroundColor = Color.white;

            // Nút Nạp JSON
            GUI.backgroundColor = new Color(0.25f, 0.75f, 1.0f);
            if (GUILayout.Button($"📂 Nạp L{levelId}", GUILayout.Height(22), GUILayout.Width(75)))
            {
                LoadLevelConfig(levelId);
            }
            GUI.backgroundColor = Color.white;

            // Nút Xuất JSON Nhanh
            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f);
            if (GUILayout.Button($"💾 Lưu L{levelId}", GUILayout.Height(22), GUILayout.Width(75)))
            {
                ExportLevelFiles();
            }
            GUI.backgroundColor = Color.white;

            // Nút Tự Cân Bằng Xe
            GUI.backgroundColor = new Color(1.0f, 0.6f, 0.1f);
            if (GUILayout.Button("⚡ Tự Sinh Xe", GUILayout.Height(22), GUILayout.Width(95)))
            {
                AutoGenerateWinningFleet();
            }
            GUI.backgroundColor = Color.white;

            // Nút Sinh Thử Scene Không Play
            GUI.backgroundColor = new Color(0.1f, 0.9f, 0.65f);
            if (GUILayout.Button("🏗️ Sinh Thử (Không Play)", GUILayout.Height(22), GUILayout.Width(160)))
            {
                SpawnLevelPreviewToScene(enterPlayMode: false);
            }
            GUI.backgroundColor = Color.white;

            // Nút Xóa Level Scene
            GUI.backgroundColor = new Color(1.0f, 0.4f, 0.4f);
            if (GUILayout.Button("🗑️ Xóa Scene", GUILayout.Height(22), GUILayout.Width(85)))
            {
                CleanupPlayTestScene();
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("📁 Mở Folder", GUILayout.Height(22), GUILayout.Width(85)))
            {
                string folder = Path.Combine(Application.dataPath, "Scene_Data/Levels");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                EditorUtility.RevealInFinder(folder);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Thanh Chuyển Tab Cao Cấp
            GUI.backgroundColor = new Color(0.15f, 0.65f, 0.95f);
            currentTab = GUILayout.Toolbar(currentTab, tabTitles, GUILayout.Height(36));
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();
        }
        #endregion

        #region TAB 1: BÃI ĐỖ XE & CÂN BẰNG
        private void DrawParkingLotTab()
        {
            // CARD 1: TỰ ĐỘNG SINH XE
            BeginCard("1. ⚡ TỰ ĐỘNG SINH XE (HÀNG VỪA KHÍT - CÂN BẰNG 100% THẮNG)", "🚀", "Tự tính số hàng vừa đủ, không thừa ô trống");
            
            int totalSand = GetTotalSandCount();
            int estimatedCars = Mathf.CeilToInt((float)totalSand / autoBaseCapacity);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Box($"🏖️ <b>{totalSand:N0}</b> Hạt Cát", GetMetricBoxStyle(), GUILayout.Height(32));
            GUILayout.Box($"🚗 <b>~{estimatedCars}</b> Xe Cần Đón", GetMetricBoxStyle(), GUILayout.Height(32));
            GUILayout.Box($"📐 Định Cỡ: <b>{Mathf.CeilToInt((float)estimatedCars / gridCols)}</b> Hàng × <b>{gridCols}</b> Cột", GetMetricBoxStyle(), GUILayout.Height(32));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Sức chứa cơ bản:", GUILayout.Width(110));
            autoBaseCapacity = EditorGUILayout.IntPopup(autoBaseCapacity,
                new string[] { "100 Chỗ (Chuẩn Bus)", "50 Chỗ (Khách Nhỏ)", "150 Chỗ", "200 Chỗ" },
                new int[] { 100, 50, 150, 200 }, GUILayout.Width(150));

            GUILayout.FlexibleSpace();

            GUI.backgroundColor = new Color(1.0f, 0.55f, 0.1f);
            if (GUILayout.Button("🚀 SINH BÃI XE THẮNG LEVEL (TỰ CO GIÃN HÀNG)", GUILayout.Height(32), GUILayout.Width(350)))
            {
                if (sandColorCounts.Count == 0)
                {
                    EditorUtility.DisplayDialog("Thông Báo", "Vui lòng chọn hoặc nạp mẫu tranh cát ở Tab 2 trước!", "OK");
                }
                else
                {
                    AutoGenerateWinningFleet();
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            EndCard();

            // CARD 2: MA TRẬN BÃI ĐỖ XE
            BeginCard($"2. 🚗 MA TRẬN BÃI ĐỖ XE ({gridRows} Hàng × {gridCols} Cột = {gridRows * gridCols} Ô)", "🅿️", "Bấm vào từng ô để xem và chỉnh sửa thông tin xe");
            
            // Thanh công cụ bãi đỗ
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Hàng (Rows):", GUILayout.Width(80));
            if (GUILayout.Button("-", GUILayout.Width(25))) ResizeGrid(Mathf.Max(2, gridRows - 1), gridCols);
            int newRows = EditorGUILayout.IntSlider(gridRows, 2, 20, GUILayout.Width(120));
            if (GUILayout.Button("+", GUILayout.Width(25))) ResizeGrid(Mathf.Min(20, gridRows + 1), gridCols);

            GUILayout.Space(15);
            EditorGUILayout.LabelField("Cột (Cols):", GUILayout.Width(70));
            if (GUILayout.Button("-", GUILayout.Width(25))) ResizeGrid(gridRows, Mathf.Max(3, gridCols - 1));
            int newCols = EditorGUILayout.IntSlider(gridCols, 3, 8, GUILayout.Width(100));
            if (GUILayout.Button("+", GUILayout.Width(25))) ResizeGrid(gridRows, Mathf.Min(8, gridCols + 1));

            if (newRows != gridRows || newCols != gridCols) ResizeGrid(newRows, newCols);

            GUILayout.FlexibleSpace();

            // Chế độ thao tác
            if (!isBrushPaintingMode)
            {
                GUI.backgroundColor = new Color(0.2f, 0.85f, 0.35f);
                if (GUILayout.Button("🔍 Mode: Chọn & Sửa Xe", GUILayout.Height(24), GUILayout.Width(180))) { }
                GUI.backgroundColor = Color.white;
                if (GUILayout.Button("🖌️ Bật Cọ Vẽ Nhanh", GUILayout.Height(24), GUILayout.Width(140))) isBrushPaintingMode = true;
            }
            else
            {
                if (GUILayout.Button("🔍 Chuyển Sang Chọn & Sửa", GUILayout.Height(24), GUILayout.Width(180))) isBrushPaintingMode = false;
                GUI.backgroundColor = new Color(1.0f, 0.6f, 0.1f);
                if (GUILayout.Button("🖌️ Mode: Đang Bật Cọ Vẽ", GUILayout.Height(24), GUILayout.Width(160))) { }
                GUI.backgroundColor = Color.white;
            }

            if (GUILayout.Button("🔀 Tráo Xe", GUILayout.Height(24), GUILayout.Width(80))) ShuffleCars();
            if (GUILayout.Button("🗑️ Xóa Bãi", GUILayout.Height(24), GUILayout.Width(75)))
            {
                if (EditorUtility.DisplayDialog("Xác nhận", "Bạn có chắc muốn xóa sạch toàn bộ xe trên bãi?", "Xóa", "Hủy"))
                    InitGrid(gridRows, gridCols);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // Vẽ tiêu đề các cột
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("", GUILayout.Width(65));
            for (int c = 0; c < gridCols; c++)
            {
                GUIStyle colHeadStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
                GUILayout.Label($"CỘT {c + 1}", colHeadStyle, GUILayout.Width(82));
            }
            EditorGUILayout.EndHorizontal();

            // Bàn cờ trực quan
            for (int r = 0; r < gridRows; r++)
            {
                EditorGUILayout.BeginHorizontal();
                string rowLabel = (r == 0) ? "H1 (Đón):" : $"Hàng {r + 1}:";
                GUIStyle rowHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = (r == 0) ? new Color(0.2f, 0.85f, 0.35f) : Color.white }
                };
                GUILayout.Label(rowLabel, rowHeaderStyle, GUILayout.Width(65));

                for (int c = 0; c < gridCols; c++)
                {
                    DrawParkingSlot(r, c);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(2);
            }

            EndCard();

            // CARD 3: INSPECTOR XE ĐANG CHỌN
            if (selectedCellRow >= 0 && selectedCellRow < gridRows && selectedCellCol >= 0 && selectedCellCol < gridCols)
            {
                DrawVehicleInspector();
            }

            // CARD 4: KIỂM TRA CÂN BẰNG ĐIỀU KIỆN THẮNG
            DrawBalanceChecker();
        }

        private void DrawParkingSlot(int r, int c)
        {
            string cellData = carGrid[r, c];
            string[] parts = cellData.Split('_');
            int colorIdx = -1, cap = 100, mech = 0;
            if (parts.Length >= 2) int.TryParse(parts[0], out colorIdx);
            if (parts.Length >= 2) int.TryParse(parts[1], out cap);
            if (parts.Length >= 3) int.TryParse(parts[2], out mech);

            bool hasCar = (colorIdx >= 0 && colorIdx < StandardPalette.Length);
            bool isSelected = (r == selectedCellRow && c == selectedCellCol);

            Color carCol = hasCar ? StandardPalette[colorIdx] : new Color(0.2f, 0.22f, 0.26f);
            
            // Viền sáng nếu đang được chọn
            GUI.backgroundColor = isSelected ? Color.cyan : carCol;

            string labelText;
            if (hasCar)
            {
                string mechTag = "";
                if (mech == 1) mechTag = "🔒";
                else if (mech == 2) mechTag = "🧊";
                else if (mech == 5) mechTag = "🛸";
                else if (mech == 501) mechTag = "🔑";

                labelText = isSelected ? $"▶ {cap}c ◀\n{mechTag}Xe" : $"{cap}c\n{mechTag}Xe";
            }
            else
            {
                labelText = isSelected ? "▶Trống◀" : "·";
            }

            GUIStyle slotStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = hasCar && (carCol.grayscale > 0.5f) ? Color.black : Color.white }
            };

            if (GUILayout.Button(labelText, slotStyle, GUILayout.Width(82), GUILayout.Height(46)))
            {
                selectedCellRow = r;
                selectedCellCol = c;

                if (isBrushPaintingMode)
                {
                    if (hasCar && colorIdx == (int)selectedColor && cap == selectedCapacity && mech == selectedMechanic)
                        carGrid[r, c] = "-1_0_0";
                    else
                        carGrid[r, c] = $"{(int)selectedColor}_{selectedCapacity}_{selectedMechanic}";
                }
                else
                {
                    if (hasCar)
                    {
                        selectedColor = (GameColorType)colorIdx;
                        selectedCapacity = cap;
                        selectedMechanic = mech;
                    }
                }
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawVehicleInspector()
        {
            BeginCard($"3. 📦 THÔNG TIN & SỬA XE Ô [HÀNG {selectedCellRow + 1}, CỘT {selectedCellCol + 1}]", "🔍", "Chỉnh sửa loại xe, màu sắc và sức chứa");

            string cellData = carGrid[selectedCellRow, selectedCellCol];
            string[] parts = cellData.Split('_');
            int colorIdx = -1, cap = 100, mech = 0;
            if (parts.Length >= 2) int.TryParse(parts[0], out colorIdx);
            if (parts.Length >= 2) int.TryParse(parts[1], out cap);
            if (parts.Length >= 3) int.TryParse(parts[2], out mech);

            bool hasCar = (colorIdx >= 0 && colorIdx < StandardPalette.Length);

            EditorGUILayout.BeginHorizontal();
            if (hasCar)
            {
                string categoryName = GetVehicleCategoryName(cap);
                GUIStyle catStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, normal = { textColor = new Color(0.2f, 0.9f, 1f) } };
                GUILayout.Label($"🚌 Phân Khúc: {categoryName} ({cap} Ghế)", catStyle);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("📋 Sao Chép", GUILayout.Width(85), GUILayout.Height(24)))
                {
                    copiedCarData = cellData;
                }
                if (GUILayout.Button("📄 Dán Xe", GUILayout.Width(75), GUILayout.Height(24)))
                {
                    carGrid[selectedCellRow, selectedCellCol] = copiedCarData;
                }
                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("🗑️ Xóa Thành Ô Trống", GUILayout.Height(24), GUILayout.Width(140)))
                {
                    carGrid[selectedCellRow, selectedCellCol] = "-1_0_0";
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                EditorGUILayout.HelpBox($"Ô [Hàng {selectedCellRow + 1}, Cột {selectedCellCol + 1}] hiện đang ĐỂ TRỐNG.", MessageType.Info);
                GUILayout.FlexibleSpace();
                GUI.backgroundColor = new Color(0.2f, 0.85f, 0.35f);
                if (GUILayout.Button("➕ Đặt Xe Vào Ô Này", GUILayout.Height(28), GUILayout.Width(160)))
                {
                    carGrid[selectedCellRow, selectedCellCol] = $"{(int)selectedColor}_{selectedCapacity}_{selectedMechanic}";
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            if (hasCar)
            {
                EditorGUILayout.Space(6);

                // 1. BẢNG 18 MÀU CHỌN NHANH TRỰC QUAN (SWATCH GRID)
                EditorGUILayout.LabelField("Chọn Màu Sắc Xe (Click trực tiếp vào ô màu):", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                for (int i = 0; i < 9; i++) DrawCarColorChip(i, colorIdx);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(2);
                EditorGUILayout.BeginHorizontal();
                for (int i = 9; i < 18; i++) DrawCarColorChip(i, colorIdx);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(8);

                // 2. SỐ LƯỢNG / SỨC CHỨA CỦA XE (SEATS)
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Sức chứa / Số ghế:", GUILayout.Width(130));
                int newCap = EditorGUILayout.IntField(cap, GUILayout.Width(80));
                newCap = Mathf.Max(1, newCap);
                EditorGUILayout.EndHorizontal();

                // Các nút chọn số lượng ghế nhanh
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(130);
                if (GUILayout.Button("4c (Sedan)", GUILayout.Width(75))) newCap = 4;
                if (GUILayout.Button("6c (Van)", GUILayout.Width(65))) newCap = 6;
                if (GUILayout.Button("10c (Mini)", GUILayout.Width(70))) newCap = 10;
                if (GUILayout.Button("50c (Khách)", GUILayout.Width(75))) newCap = 50;
                if (GUILayout.Button("100c (Bus)", GUILayout.Width(75))) newCap = 100;
                if (GUILayout.Button("150c", GUILayout.Width(50))) newCap = 150;
                if (GUILayout.Button("200c", GUILayout.Width(50))) newCap = 200;
                EditorGUILayout.EndHorizontal();

                // 3. CƠ CHẾ XE (MECHANIC)
                EditorGUILayout.Space(6);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Cơ chế đặc biệt:", GUILayout.Width(130));
                int newMech = EditorGUILayout.IntPopup(mech,
                    new string[] { "0: Thường (Bình Thường)", "1: Bị Khóa Xích (Lock)", "2: Đóng Băng (Ice)", "5: Xe Bay (Drone Fly)", "501: Mang Chìa Khóa (Key)" },
                    new int[] { 0, 1, 2, 5, 501 });
                EditorGUILayout.EndHorizontal();

                // Cập nhật dữ liệu ô xe nếu có thay đổi
                if (newCap != cap || newMech != mech)
                {
                    carGrid[selectedCellRow, selectedCellCol] = $"{colorIdx}_{newCap}_{newMech}";
                    selectedCapacity = newCap;
                    selectedMechanic = newMech;
                }
            }

            EndCard();
        }

        private void DrawCarColorChip(int colorIdx, int currentColorIdx)
        {
            Color col = StandardPalette[colorIdx];
            bool isCurrent = (colorIdx == currentColorIdx);
            GUI.backgroundColor = col;

            string btnText = isCurrent ? "✓" : "";
            GUIStyle chipStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = (col.grayscale > 0.5f) ? Color.black : Color.white }
            };

            if (GUILayout.Button(new GUIContent(btnText, ColorNames[colorIdx]), chipStyle, GUILayout.Width(42), GUILayout.Height(26)))
            {
                string cellData = carGrid[selectedCellRow, selectedCellCol];
                string[] parts = cellData.Split('_');
                int cap = 100, mech = 0;
                if (parts.Length >= 2) int.TryParse(parts[1], out cap);
                if (parts.Length >= 3) int.TryParse(parts[2], out mech);

                carGrid[selectedCellRow, selectedCellCol] = $"{colorIdx}_{cap}_{mech}";
                selectedColor = (GameColorType)colorIdx;
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawBalanceChecker()
        {
            BeginCard("4. ⚖️ BẢNG ĐIỀU KHIỂN CÂN BẰNG & ĐIỀU KIỆN THẮNG", "🎯", "Đảm bảo 100% hạt cát có đủ số ghế xe đón");

            Dictionary<int, int> carSeatCounts = CalculateCarSeats();

            if (sandColorCounts.Count == 0)
            {
                EditorGUILayout.HelpBox("Chưa có dữ liệu tranh cát. Vui lòng nạp hoặc vẽ tranh cát tại Tab 2!", MessageType.Info);
            }
            else
            {
                int totalMissing = 0;
                int totalExcess = 0;

                foreach (var kvp in sandColorCounts)
                {
                    int cIdx = kvp.Key;
                    int sCount = kvp.Value;
                    if (sCount <= 0) continue;
                    int seatCount = carSeatCounts.ContainsKey(cIdx) ? carSeatCounts[cIdx] : 0;
                    if (seatCount < sCount) totalMissing += (sCount - seatCount);
                    if (seatCount > sCount) totalExcess += (seatCount - sCount);
                }

                if (totalMissing == 0 && totalExcess == 0)
                {
                    GUI.backgroundColor = new Color(0.2f, 0.85f, 0.35f);
                    GUILayout.Box("🎉 LEVEL ĐÃ ĐẠT CÂN BẰNG 100%! TOÀN BỘ HẠT CÁT ĐÃ KHỚP CHÍNH XÁC VỚI SỐ GHẾ XE!", GetStatusBannerStyle(), GUILayout.Height(36));
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUI.backgroundColor = new Color(1.0f, 0.45f, 0.2f);
                    GUILayout.Box($"⚠️ CHƯA CÂN BẰNG: Còn thiếu {totalMissing} ghế và thừa {totalExcess} ghế!", GetStatusBannerStyle(), GUILayout.Height(36));
                    GUI.backgroundColor = Color.white;
                }

                EditorGUILayout.Space(6);

                // Danh sách từng màu
                foreach (var kvp in sandColorCounts)
                {
                    int colorIdx = kvp.Key;
                    int sandCount = kvp.Value;
                    if (sandCount <= 0) continue;

                    int seatCount = carSeatCounts.ContainsKey(colorIdx) ? carSeatCounts[colorIdx] : 0;
                    string cName = (colorIdx >= 0 && colorIdx < ColorNames.Length) ? ColorNames[colorIdx] : $"Màu {colorIdx}";
                    Color col = (colorIdx >= 0 && colorIdx < StandardPalette.Length) ? StandardPalette[colorIdx] : Color.white;

                    EditorGUILayout.BeginHorizontal();
                    
                    // Ô vuông màu
                    GUI.backgroundColor = col;
                    GUILayout.Box("", GUILayout.Width(20), GUILayout.Height(24));
                    GUI.backgroundColor = Color.white;

                    if (sandCount == seatCount)
                    {
                        GUI.backgroundColor = new Color(0.2f, 0.85f, 0.35f);
                        GUILayout.Box($"✅ {cName}: {sandCount} hạt  ◄►  {seatCount} ghế (CÂN BẰNG 100%)", GUILayout.Height(24));
                        GUI.backgroundColor = Color.white;
                    }
                    else if (seatCount < sandCount)
                    {
                        int missing = sandCount - seatCount;
                        GUI.backgroundColor = new Color(1.0f, 0.3f, 0.3f);
                        GUILayout.Box($"❌ {cName}: {sandCount} hạt  ◄►  {seatCount} ghế (THIẾU {missing} GHẾ)", GUILayout.Height(24));
                        GUI.backgroundColor = Color.white;

                        if (GUILayout.Button($"+Xe {missing}c", GUILayout.Width(80), GUILayout.Height(24)))
                        {
                            AddVehicleSmart(colorIdx, missing, 0);
                        }
                    }
                    else
                    {
                        int excess = seatCount - sandCount;
                        GUI.backgroundColor = new Color(1.0f, 0.7f, 0.2f);
                        GUILayout.Box($"⚠️ {cName}: {sandCount} hạt  ◄►  {seatCount} ghế (THỪA {excess} GHẾ)", GUILayout.Height(24));
                        GUI.backgroundColor = Color.white;
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EndCard();
        }
        #endregion

        #region TAB 2: TRANH CÁT 40x40 (CANVAS PRO)
        private void DrawSandCanvasProTab()
        {
            // CARD 1: THANH CÔNG CỤ VẼ TRANH CÁT (TOOLBOX)
            BeginCard("1. 🛠️ THANH CÔNG CỤ VẼ TRANH CÁT PRO (TOOLBOX)", "🎨", "Giữ chuột trái để vẽ liên tục mượt mà như Photoshop");
            
            EditorGUILayout.BeginHorizontal();

            // Nhóm công cụ vẽ
            DrawToolButton(SandDrawTool.Pencil, "🖌️ Bút Chấm (Pencil)");
            DrawToolButton(SandDrawTool.BucketFill, "🪣 Đổ Sơn (Bucket Fill)");
            DrawToolButton(SandDrawTool.Eraser, "🧹 Cục Tẩy (Eraser)");
            DrawToolButton(SandDrawTool.Eyedropper, "🔍 Hút Màu (Eyedropper)");

            GUILayout.Space(15);

            // Cỡ cọ
            EditorGUILayout.LabelField("Cỡ cọ:", GUILayout.Width(45));
            if (GUILayout.Toggle(brushSize == 1, "1×1", EditorStyles.radioButton, GUILayout.Width(45))) brushSize = 1;
            if (GUILayout.Toggle(brushSize == 2, "2×2", EditorStyles.radioButton, GUILayout.Width(45))) brushSize = 2;
            if (GUILayout.Toggle(brushSize == 3, "3×3", EditorStyles.radioButton, GUILayout.Width(45))) brushSize = 3;

            GUILayout.FlexibleSpace();

            // Hoàn tác
            GUI.enabled = undoStack.Count > 0;
            if (GUILayout.Button($"↩️ Undo ({undoStack.Count})", GUILayout.Height(24), GUILayout.Width(85)))
            {
                PerformUndo();
            }
            GUI.enabled = true;

            // Xóa trắng & Khoét rãnh
            if (GUILayout.Button("🛣️ Khoét Rãnh Xe", GUILayout.Height(24), GUILayout.Width(110)))
            {
                CarveRoadCutout();
            }
            if (GUILayout.Button("🗑️ Xóa Trắng", GUILayout.Height(24), GUILayout.Width(85)))
            {
                if (EditorUtility.DisplayDialog("Xóa Canvas", "Bạn có chắc muốn xóa trắng toàn bộ tranh cát?", "Xóa", "Hủy"))
                {
                    RecordUndo();
                    ClearCanvas();
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // NẠP PRESET VÀ IMPORT PNG
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Mẫu Douyin:", EditorStyles.boldLabel, GUILayout.Width(80));
            GUI.backgroundColor = new Color(0.95f, 0.35f, 0.45f);
            if (GUILayout.Button("🍉 Màn 5: Dưa Hấu (Douyin)", GUILayout.Width(170), GUILayout.Height(24))) LoadWatermelonPreset();
            GUI.backgroundColor = new Color(0.2f, 0.8f, 1.0f);
            if (GUILayout.Button("🐬 Màn 4: Cá Heo", GUILayout.Width(110), GUILayout.Height(24))) LoadPresetImage("Assets/Level/13.png");
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("❤️ Trái Tim", GUILayout.Width(85), GUILayout.Height(24))) LoadHeartPreset();
            if (GUILayout.Button("⭐ Ngôi Sao", GUILayout.Width(85), GUILayout.Height(24))) LoadStarPreset();
            if (GUILayout.Button("😀 Mặt Cười", GUILayout.Width(85), GUILayout.Height(24))) LoadSmileyPreset();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Import File PNG:", GUILayout.Width(100));
            inputPixelArt = (Texture2D)EditorGUILayout.ObjectField(inputPixelArt, typeof(Texture2D), false, GUILayout.Width(120));
            if (GUILayout.Button("🔄 Quét Ảnh Sang Cát", GUILayout.Width(140), GUILayout.Height(20)))
            {
                if (inputPixelArt != null) ScanPixelArtTexture();
            }

            GUILayout.FlexibleSpace();

            // Nút Thao Tác Trực Tiếp Ngay Tại Tab Tranh Cát
            GUI.backgroundColor = new Color(1.0f, 0.6f, 0.1f);
            if (GUILayout.Button("⚡ Tự Cân Bằng Bãi Xe", GUILayout.Width(150), GUILayout.Height(22)))
            {
                AutoGenerateWinningFleet();
            }
            GUI.backgroundColor = new Color(0.1f, 0.9f, 0.65f);
            if (GUILayout.Button("🏗️ Sinh Thử Ra Scene", GUILayout.Width(145), GUILayout.Height(22)))
            {
                SpawnLevelPreviewToScene(enterPlayMode: false);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EndCard();

            // CARD 2: BẢNG 18 MÀU CHUẨN
            DrawColorPaletteBar();

            // CARD 3: BÀN VẼ TRANH CÁT 40x40 INTERACTIVE
            BeginCard("3. 🖼️ BÀN VẼ TRANH CÁT 40×40 (CANVAS INTERACTIVE)", "✨", "Kéo chuột để vẽ, Chuột phải để tẩy. Vùng khoét chữ U là rãnh đón xe.");



            DrawInteractiveSandCanvas();

            // HUD Con trỏ & Tọa độ
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            string hoverText = (hoveredCellX >= 0 && hoveredCellY >= 0)
                ? $"📍 Tọa Độ: [X: {hoveredCellX:D2}, Y: {hoveredCellY:D2}] | Màu Ô: {(pixelMapData[hoveredCellY, hoveredCellX] >= 0 ? ColorNames[pixelMapData[hoveredCellY, hoveredCellX]] : "Trống (-1)")}"
                : "📍 Đưa chuột vào bàn vẽ để xem tọa độ";
            GUILayout.Label(hoverText, EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();
            GUILayout.Label($"Tổng hạt cát: <b>{GetTotalSandCount():N0}</b> / 1,600 hạt", new GUIStyle(EditorStyles.label) { richText = true });
            EditorGUILayout.EndHorizontal();

            EndCard();
        }

        private void DrawToolButton(SandDrawTool tool, string label)
        {
            bool isCurrent = (currentDrawTool == tool);
            GUI.backgroundColor = isCurrent ? new Color(0.2f, 0.9f, 0.4f) : Color.white;
            if (GUILayout.Button(label, GUILayout.Height(26)))
            {
                currentDrawTool = tool;
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawColorPaletteBar()
        {
            BeginCard("2. 🎨 BẢNG 18 MÀU SẮC CHUẨN CỦA GAME (CLICK CHỌN MÀU CỌ)", "🖌️", "Khớp 100% với hạt hành khách và vật liệu xe buýt");

            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < 9; i++) DrawCanvasColorChip(i);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            for (int i = 9; i < 18; i++) DrawCanvasColorChip(i);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = StandardPalette[(int)selectedColor];
            GUILayout.Box("", GUILayout.Width(28), GUILayout.Height(20));
            GUI.backgroundColor = Color.white;
            GUILayout.Label($"Màu cọ đang chọn: <b>{ColorNames[(int)selectedColor]}</b>", new GUIStyle(EditorStyles.label) { richText = true });
            EditorGUILayout.EndHorizontal();

            EndCard();
        }

        private void DrawCanvasColorChip(int colorIdx)
        {
            Color col = StandardPalette[colorIdx];
            bool isCurrent = ((int)selectedColor == colorIdx);
            GUI.backgroundColor = col;

            string label = isCurrent ? "✓" : "";
            GUIStyle chipStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = (col.grayscale > 0.5f) ? Color.black : Color.white }
            };

            if (GUILayout.Button(new GUIContent(label, ColorNames[colorIdx]), chipStyle, GUILayout.Width(44), GUILayout.Height(26)))
            {
                selectedColor = (GameColorType)colorIdx;
                if (currentDrawTool == SandDrawTool.Eraser) currentDrawTool = SandDrawTool.Pencil;
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawInteractiveSandCanvas()
        {
            EnsureSandCanvasTexture();

            float canvasSize = 440f;
            float cellPixel = canvasSize / 40f;
            Rect totalRect = EditorGUILayout.GetControlRect(false, canvasSize);
            Rect canvasRect = new Rect(totalRect.x + (totalRect.width - canvasSize) * 0.5f, totalRect.y, canvasSize, canvasSize);

            // Viền ngoài bảng vẽ
            Rect shadowRect = new Rect(canvasRect.x - 2, canvasRect.y - 2, canvasRect.width + 4, canvasRect.height + 4);
            EditorGUI.DrawRect(shadowRect, new Color(0.12f, 0.14f, 0.18f, 1f));

            // Vẽ Canvas Texture 40x40
            GUI.DrawTexture(canvasRect, sandCanvasTex);

            // Overlay vùng rãnh khoét chữ U đón xe (Cột 14-25, Hàng 0-18)
            float cutoutX = canvasRect.x + 14 * cellPixel;
            float cutoutW = 12 * cellPixel;
            float cutoutH = 19 * cellPixel;
            float cutoutY = canvasRect.yMax - cutoutH;
            Rect cutoutRect = new Rect(cutoutX, cutoutY, cutoutW, cutoutH);

            Handles.color = new Color(1f, 0.75f, 0.2f, 0.75f);
            Handles.DrawWireCube(cutoutRect.center, cutoutRect.size);
            GUIStyle cutoutStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                normal = { textColor = new Color(1f, 0.85f, 0.4f, 0.85f) }
            };
            GUI.Label(cutoutRect, "🛣️ RÃNH XE CHẠY\n& CỔNG ĐÓN KHÁCH", cutoutStyle);

            // Xử lý sự kiện kéo vẽ liên tục
            HandleCanvasInput(canvasRect);
        }

        private void HandleCanvasInput(Rect canvasRect)
        {
            Event e = Event.current;
            int controlID = GUIUtility.GetControlID(FocusType.Passive);

            if (canvasRect.Contains(e.mousePosition))
            {
                Vector2 localPos = e.mousePosition - canvasRect.position;
                int cx = Mathf.Clamp(Mathf.FloorToInt(localPos.x / (canvasRect.width / 40f)), 0, 39);
                int cy = Mathf.Clamp(39 - Mathf.FloorToInt(localPos.y / (canvasRect.height / 40f)), 0, 39);

                hoveredCellX = cx;
                hoveredCellY = cy;

                if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 1))
                {
                    GUIUtility.hotControl = controlID;
                    RecordUndo();

                    if (e.button == 0)
                        ApplyTool(cx, cy);
                    else
                        ApplyEraser(cx, cy);

                    e.Use();
                }
                else if (e.type == EventType.MouseDrag && GUIUtility.hotControl == controlID)
                {
                    if (e.button == 0)
                        ApplyTool(cx, cy);
                    else
                        ApplyEraser(cx, cy);

                    e.Use();
                }
                else if (e.type == EventType.MouseMove)
                {
                    Repaint();
                }
            }
            else
            {
                if (hoveredCellX != -1)
                {
                    hoveredCellX = -1;
                    hoveredCellY = -1;
                    Repaint();
                }
            }

            if (e.type == EventType.MouseUp && GUIUtility.hotControl == controlID)
            {
                GUIUtility.hotControl = 0;
                e.Use();
            }
        }

        private void ApplyTool(int cx, int cy)
        {
            switch (currentDrawTool)
            {
                case SandDrawTool.Pencil:
                    PaintBrush(cx, cy, (int)selectedColor);
                    break;
                case SandDrawTool.BucketFill:
                    FloodFill(cx, cy, pixelMapData[cy, cx], (int)selectedColor);
                    break;
                case SandDrawTool.Eraser:
                    PaintBrush(cx, cy, -1);
                    break;
                case SandDrawTool.Eyedropper:
                    int picked = pixelMapData[cy, cx];
                    if (picked >= 0 && picked < StandardPalette.Length)
                    {
                        selectedColor = (GameColorType)picked;
                        currentDrawTool = SandDrawTool.Pencil;
                    }
                    break;
            }
            UpdateSandCanvasTexture();
            RecalculateSandColorCounts();
            Repaint();
        }

        private void ApplyEraser(int cx, int cy)
        {
            PaintBrush(cx, cy, -1);
            UpdateSandCanvasTexture();
            RecalculateSandColorCounts();
            Repaint();
        }

        private void PaintBrush(int cx, int cy, int colorIdx)
        {
            int r = brushSize - 1;
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    int nx = cx + dx;
                    int ny = cy + dy;
                    if (nx >= 0 && nx < 40 && ny >= 0 && ny < 40)
                    {
                        pixelMapData[ny, nx] = colorIdx;
                    }
                }
            }
        }

        private void FloodFill(int startX, int startY, int targetColor, int replacementColor)
        {
            if (targetColor == replacementColor) return;
            if (startX < 0 || startX >= 40 || startY < 0 || startY >= 40) return;
            if (pixelMapData[startY, startX] != targetColor) return;

            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(new Vector2Int(startX, startY));
            pixelMapData[startY, startX] = replacementColor;

            while (queue.Count > 0)
            {
                Vector2Int pt = queue.Dequeue();
                int x = pt.x;
                int y = pt.y;

                int[] dx = { 0, 0, 1, -1 };
                int[] dy = { 1, -1, 0, 0 };
                for (int i = 0; i < 4; i++)
                {
                    int nx = x + dx[i];
                    int ny = y + dy[i];
                    if (nx >= 0 && nx < 40 && ny >= 0 && ny < 40)
                    {
                        if (pixelMapData[ny, nx] == targetColor)
                        {
                            pixelMapData[ny, nx] = replacementColor;
                            queue.Enqueue(new Vector2Int(nx, ny));
                        }
                    }
                }
            }
        }

        private void EnsureSandCanvasTexture()
        {
            if (sandCanvasTex == null)
            {
                sandCanvasTex = new Texture2D(40, 40, TextureFormat.RGBA32, false);
                sandCanvasTex.filterMode = FilterMode.Point;
                sandCanvasTex.wrapMode = TextureWrapMode.Clamp;
                UpdateSandCanvasTexture();
            }
        }

        private void UpdateSandCanvasTexture()
        {
            if (sandCanvasTex == null) return;

            for (int y = 0; y < 40; y++)
            {
                for (int x = 0; x < 40; x++)
                {
                    int cIdx = pixelMapData[y, x];
                    bool isCutout = (y <= 18 && x >= 14 && x <= 25);
                    Color col;

                    if (isCutout)
                    {
                        col = new Color(0.18f, 0.20f, 0.25f, 1f);
                    }
                    else if (cIdx >= 0 && cIdx < StandardPalette.Length)
                    {
                        col = StandardPalette[cIdx];
                    }
                    else
                    {
                        col = new Color(0.11f, 0.12f, 0.15f, 1f);
                    }
                    sandPixels[y * 40 + x] = col;
                }
            }

            sandCanvasTex.SetPixels32(sandPixels);
            sandCanvasTex.Apply();
        }

        private void ClearCanvas()
        {
            for (int y = 0; y < 40; y++)
                for (int x = 0; x < 40; x++)
                    pixelMapData[y, x] = -1;

            UpdateSandCanvasTexture();
            RecalculateSandColorCounts();
            Repaint();
        }

        private void CarveRoadCutout()
        {
            RecordUndo();
            for (int y = 0; y <= 18; y++)
                for (int x = 14; x <= 25; x++)
                    pixelMapData[y, x] = -1;

            UpdateSandCanvasTexture();
            RecalculateSandColorCounts();
            Repaint();
        }

        private void RecordUndo()
        {
            int[,] copy = new int[40, 40];
            for (int y = 0; y < 40; y++)
                for (int x = 0; x < 40; x++)
                    copy[y, x] = pixelMapData[y, x];

            undoStack.Push(copy);
            if (undoStack.Count > 30)
            {
                // Giới hạn bộ nhớ undo 30 bước
                var list = new List<int[,]>(undoStack);
                list.RemoveAt(list.Count - 1);
                undoStack = new Stack<int[,]>(list);
            }
        }

        private void PerformUndo()
        {
            if (undoStack.Count > 0)
            {
                int[,] prev = undoStack.Pop();
                for (int y = 0; y < 40; y++)
                    for (int x = 0; x < 40; x++)
                        pixelMapData[y, x] = prev[y, x];

                UpdateSandCanvasTexture();
                RecalculateSandColorCounts();
                Repaint();
            }
        }

        #region MẪU PRESETS TRANH CÁT TỰ ĐỘNG
        private void LoadWatermelonPreset()
        {
            RecordUndo();
            ClearCanvas();

            // Tọa độ tâm cung tròn quả dưa hấu
            Vector2 center = new Vector2(19.5f, 41.0f);

            // Bố trí các cụm hạt dưa đen (Black seeds)
            HashSet<Vector2Int> seedPixels = new HashSet<Vector2Int>();
            Vector2Int[] seedBases = new Vector2Int[]
            {
                new Vector2Int(18, 36), new Vector2Int(21, 36), // 2 hạt đỉnh
                new Vector2Int(13, 32), new Vector2Int(26, 32), // 2 hạt trên
                new Vector2Int(11, 26), new Vector2Int(28, 26), // 2 hạt giữa
                new Vector2Int(14, 20), new Vector2Int(25, 20)  // 2 hạt dưới
            };

            foreach (var sc in seedBases)
            {
                seedPixels.Add(sc);
                seedPixels.Add(new Vector2Int(sc.x, sc.y - 1));
                seedPixels.Add(new Vector2Int(sc.x, sc.y + 1));
                seedPixels.Add(new Vector2Int(sc.x + 1, sc.y));
            }

            for (int y = 0; y < 40; y++)
            {
                for (int x = 0; x < 40; x++)
                {
                    // Vùng khoét rãnh đón xe
                    if (y <= 18 && x >= 14 && x <= 25) continue;

                    float dist = Vector2.Distance(new Vector2(x, y), center);

                    if (dist > 25.5f)
                    {
                        // Nền ngoài bao quanh (Trắng #0)
                        pixelMapData[y, x] = 0;
                    }
                    else if (dist >= 22.2f)
                    {
                        // Vỏ dưa xanh lục (Green #1)
                        pixelMapData[y, x] = 1;
                    }
                    else if (dist >= 19.8f)
                    {
                        // Viền cùi dưa màu trắng / kem (White #0)
                        pixelMapData[y, x] = 0;
                    }
                    else
                    {
                        // Ruột dưa đỏ (#8) hoặc hạt dưa đen (#11)
                        if (seedPixels.Contains(new Vector2Int(x, y)))
                        {
                            pixelMapData[y, x] = 11; // 11: Black (Hạt dưa)
                        }
                        else
                        {
                            pixelMapData[y, x] = 8; // 8: Red (Ruột dưa đỏ)
                        }
                    }
                }
            }

            UpdateSandCanvasTexture();
            RecalculateSandColorCounts();
            Repaint();
            Debug.Log("<color=green>[Level Designer Pro]</color> Đã nạp thành công Mẫu Dưa Hấu (Màn 5 Chuẩn Douyin)!");
        }

        private void LoadHeartPreset()
        {
            RecordUndo();
            ClearCanvas();
            for (int y = 0; y < 40; y++)
            {
                for (int x = 0; x < 40; x++)
                {
                    if (y <= 18 && x >= 14 && x <= 25) continue; // Tránh rãnh khoét
                    float nx = (x - 20) / 13f;
                    float ny = (y - 24) / 13f;
                    float f = (nx * nx + ny * ny - 1f);
                    if (f * f * f - nx * nx * ny * ny * ny <= 0)
                    {
                        pixelMapData[y, x] = (y > 25) ? 8 : 6; // Red & Powder
                    }
                }
            }
            UpdateSandCanvasTexture();
            RecalculateSandColorCounts();
            Repaint();
        }

        private void LoadStarPreset()
        {
            RecordUndo();
            ClearCanvas();
            Vector2 center = new Vector2(20, 26);
            for (int y = 0; y < 40; y++)
            {
                for (int x = 0; x < 40; x++)
                {
                    if (y <= 18 && x >= 14 && x <= 25) continue;
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float angle = Mathf.Atan2(y - center.y, x - center.x) * Mathf.Rad2Deg;
                    if (angle < 0) angle += 360f;
                    float starR = 7f + 6f * Mathf.Cos(5f * angle * Mathf.Deg2Rad);
                    if (dist <= starR)
                    {
                        pixelMapData[y, x] = (dist < 5f) ? 10 : 7; // Orange & Yellow
                    }
                }
            }
            UpdateSandCanvasTexture();
            RecalculateSandColorCounts();
            Repaint();
        }

        private void LoadSmileyPreset()
        {
            RecordUndo();
            ClearCanvas();
            Vector2 center = new Vector2(20, 25);
            for (int y = 0; y < 40; y++)
            {
                for (int x = 0; x < 40; x++)
                {
                    if (y <= 18 && x >= 14 && x <= 25) continue;
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist <= 13f)
                    {
                        // Mặt cười vàng
                        pixelMapData[y, x] = 7; // Yellow

                        // 2 mắt đen
                        if ((y == 28 || y == 29) && (x == 15 || x == 25)) pixelMapData[y, x] = 11;

                        // Nụ cười đỏ
                        if (y >= 19 && y <= 22 && dist >= 8f && dist <= 11f && y < center.y) pixelMapData[y, x] = 8;
                    }
                }
            }
            UpdateSandCanvasTexture();
            RecalculateSandColorCounts();
            Repaint();
        }
        #endregion
        #endregion

        #region TAB 3: XẾP ĐƯỜNG ĐI CHO XE (TRACK & WAYPOINTS)
        private void DrawTrackEditorTab()
        {
            // CARD 1: SƠ ĐỒ 2D HỆ THỐNG ĐƯỜNG ĐUA CHỮ U
            BeginCard("1. 🛣️ BẢN ĐỒ THIẾT KẾ ĐƯỜNG CHẠY CHỮ U (2D SCHEMATIC)", "🗺️", "Mô phỏng vị trí Cổng Đón Khách, Làn rẽ và Bãi đỗ xe");
            DrawTrackSchematic2D();
            EndCard();

            // CARD 2: CÁC THÔNG SỐ & PRESETS
            BeginCard("2. ⚙️ THIẾT LẬP THÔNG SỐ & PRESETS", "📐", "Căn chỉnh bán kính đường loop và vị trí cổng");

            trackLoopRadius = EditorGUILayout.Slider("Bán kính vòng lặp (Radius):", trackLoopRadius, 3.5f, 8.0f);
            trackGateZ = EditorGUILayout.Slider("Vị trí Cổng đón khách (Gate Z):", trackGateZ, 4.0f, 10.0f);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Thiết Lập Sẵn (Presets):", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("⭐ U-Loop Chuẩn Douyin (5.2m)", GUILayout.Height(30)))
            {
                trackLoopRadius = 5.2f;
                trackGateZ = 6.2f;
            }
            if (GUILayout.Button("🏎️ Vòng Chữ U Rộng (6.5m)", GUILayout.Height(30)))
            {
                trackLoopRadius = 6.5f;
                trackGateZ = 7.5f;
            }
            if (GUILayout.Button("🏁 Vòng Chữ U Gọn (4.2m)", GUILayout.Height(30)))
            {
                trackLoopRadius = 4.2f;
                trackGateZ = 5.5f;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(14);
            GUI.backgroundColor = new Color(0.2f, 0.85f, 1.0f);
            if (GUILayout.Button("🏗️ TẠO / CẬP NHẬT ĐƯỜNG XE CHẠY TRÊN SCENE 3D", GUILayout.Height(44)))
            {
                TrackGeneratorHelper.SetupDefaultLoopTrack(new Vector3(0, 0, trackGateZ + 2.5f));
                EditorUtility.DisplayDialog("Thành Công", "Đã tạo thành công Hệ Thống Đường Loop Chữ U và Cổng Đón Khách trên Scene!", "OK");
            }
            GUI.backgroundColor = Color.white;

            EndCard();
        }

        private void DrawTrackSchematic2D()
        {
            Rect mapRect = GUILayoutUtility.GetRect(440, 260);
            EditorGUI.DrawRect(mapRect, new Color(0.10f, 0.12f, 0.16f, 1f));

            // Lưới blueprint
            Handles.color = new Color(0.2f, 0.25f, 0.32f, 0.35f);
            for (float x = mapRect.x; x <= mapRect.xMax; x += 22)
                Handles.DrawLine(new Vector3(x, mapRect.y), new Vector3(x, mapRect.yMax));
            for (float y = mapRect.y; y <= mapRect.yMax; y += 22)
                Handles.DrawLine(new Vector3(mapRect.x, y), new Vector3(mapRect.xMax, y));

            GUIStyle centerLabel = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 10, normal = { textColor = Color.white } };

            // 1. Tranh Cát 40x40 ở trên đỉnh
            float boardW = 140, boardH = 50;
            Rect boardRect = new Rect(mapRect.center.x - boardW * 0.5f, mapRect.y + 12, boardW, boardH);
            EditorGUI.DrawRect(boardRect, new Color(0.25f, 0.4f, 0.6f, 0.85f));
            GUI.Label(boardRect, "TRANH CÁT 40×40\n(Sand Board Manager)", centerLabel);

            // 2. Cổng đón khách (Gate) ngay dưới chân tranh cát
            float gateW = 80, gateH = 22;
            Rect gateRect = new Rect(mapRect.center.x - gateW * 0.5f, boardRect.yMax - 4, gateW, gateH);
            EditorGUI.DrawRect(gateRect, new Color(1f, 0.85f, 0.2f, 0.95f));
            GUIStyle gateLabel = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 9, normal = { textColor = Color.black } };
            GUI.Label(gateRect, "CỔNG ĐÓN KHÁCH", gateLabel);

            // 3. Đường cong Loop Chữ U
            Handles.color = new Color(0.1f, 0.85f, 1f, 0.95f);
            Vector3 pStart = new Vector3(mapRect.center.x - 55, mapRect.yMax - 48);
            Vector3 pTurnL = new Vector3(mapRect.center.x - 85, mapRect.center.y);
            Vector3 pGate = new Vector3(mapRect.center.x, gateRect.center.y);
            Vector3 pTurnR = new Vector3(mapRect.center.x + 85, mapRect.center.y);
            Vector3 pExit = new Vector3(mapRect.center.x + 65, mapRect.yMax - 48);

            Handles.DrawAAPolyLine(4f, pStart, pTurnL, pGate, pTurnR, pExit);

            // Các mốc điểm (Waypoints)
            Handles.color = Color.white;
            Handles.DrawSolidDisc(pStart, Vector3.forward, 4f);
            Handles.DrawSolidDisc(pTurnL, Vector3.forward, 4f);
            Handles.DrawSolidDisc(pGate, Vector3.forward, 5f);
            Handles.DrawSolidDisc(pTurnR, Vector3.forward, 4f);
            Handles.DrawSolidDisc(pExit, Vector3.forward, 4f);

            // 4. Bãi đỗ xe ở đáy
            float parkW = 160, parkH = 34;
            Rect parkRect = new Rect(mapRect.center.x - parkW * 0.5f, mapRect.yMax - 38, parkW, parkH);
            EditorGUI.DrawRect(parkRect, new Color(0.18f, 0.55f, 0.32f, 0.9f));
            GUI.Label(parkRect, "BÃI ĐỖ XE (PARKING LOT)", centerLabel);
        }
        #endregion

        #region TAB 4: CHƠI THỬ & SINH LEVEL (SIMULATOR & LEVEL PREVIEW)
        private void DrawPlayTestTab()
        {
            // CARD 1: CHỌN VÀ NẠP LEVEL
            BeginCard("1. 📂 CHỌN LEVEL ĐỂ XEM & THỬ NGHIỆM", "🎮", "Nhập Level ID để nạp hoặc sinh thử trực tiếp");
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Level ID:", EditorStyles.boldLabel, GUILayout.Width(65));
            if (GUILayout.Button("◀", GUILayout.Width(30), GUILayout.Height(24)))
            {
                levelId = Mathf.Max(1, levelId - 1);
            }
            levelId = EditorGUILayout.IntField(levelId, GUILayout.Width(55), GUILayout.Height(24));
            if (GUILayout.Button("▶", GUILayout.Width(30), GUILayout.Height(24)))
            {
                levelId++;
            }

            GUILayout.Space(12);

            GUI.backgroundColor = new Color(0.2f, 0.75f, 1.0f);
            if (GUILayout.Button($"📂 Nạp Cấu Hình Level {levelId} (JSON)", GUILayout.Height(24), GUILayout.Width(230)))
            {
                LoadLevelConfig(levelId);
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(6);
            if (GUILayout.Button($"💾 Lưu Level {levelId}", GUILayout.Height(24), GUILayout.Width(130)))
            {
                ExportLevelFiles();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            // Bảng tóm tắt thông số
            int totalSand = GetTotalSandCount();
            int totalCars = 0;
            int totalSeats = 0;
            Dictionary<int, int> seatsMap = CalculateCarSeats();
            foreach (var v in seatsMap.Values) totalSeats += v;
            for (int r = 0; r < gridRows; r++)
                for (int c = 0; c < gridCols; c++)
                    if (!carGrid[r, c].StartsWith("-1")) totalCars++;

            bool isBalanced = (totalSand > 0 && totalSand == totalSeats);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Box($"🎯 <b>Level {levelId}</b>", GetMetricBoxStyle(), GUILayout.Height(36));
            GUILayout.Box($"🏖️ <b>{totalSand:N0}</b> Hạt Cát", GetMetricBoxStyle(), GUILayout.Height(36));
            GUILayout.Box($"🚗 <b>{totalCars}</b> Xe (<b>{totalSeats:N0}</b> Ghế)", GetMetricBoxStyle(), GUILayout.Height(36));
            if (isBalanced)
            {
                GUI.backgroundColor = new Color(0.2f, 0.85f, 0.35f);
                GUILayout.Box("✅ <b>100% CÂN BẰNG</b>", GetMetricBoxStyle(), GUILayout.Height(36));
                GUI.backgroundColor = Color.white;
            }
            else
            {
                GUI.backgroundColor = new Color(1.0f, 0.6f, 0.2f);
                GUILayout.Box("⚠️ <b>CHƯA CÂN BẰNG</b>", GetMetricBoxStyle(), GUILayout.Height(36));
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            EndCard();

            // CARD 2: SINH THỬ PREFAB TRỰC TIẾP RA SCENE (KHÔNG PLAY)
            BeginCard("2. 🏗️ SINH THỬ PREFAB RA SCENE (KHÔNG PLAY - EDIT MODE)", "🔍", "Sinh thực thể Prefab thật (Passenger & Car) để quan sát và tinh chỉnh trên Scene View");
            
            EditorGUILayout.HelpBox(
                "Chế độ Sinh Thử (Edit Mode): Toàn bộ Bàn tranh cát 40×40 nằm ngang, Bãi đỗ xe và Đường Loop chữ U " +
                "sẽ được sinh bằng các PREFAB THẬT (Passenger.prefab, Car.prefab) trực tiếp trên Scene View mà KHÔNG BẬT PLAY MODE.\n" +
                "Bạn có thể zoom xoay, click chọn từng xe, đổi vị trí, kiểm tra Transform vô cùng thuận tiện!",
                MessageType.Info);

            EditorGUILayout.Space(6);
            GUI.backgroundColor = new Color(0.1f, 0.9f, 0.6f);
            if (GUILayout.Button($"🏗️ 1-CLICK SINH THỬ LEVEL {levelId} RA SCENE (KHÔNG PLAY)", GUILayout.Height(52)))
            {
                SpawnLevelPreviewToScene(enterPlayMode: false);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(1.0f, 0.4f, 0.4f);
            if (GUILayout.Button("⏹️ DỌN DẸP / XÓA LEVEL TRÊN SCENE (CLEAN UP)", GUILayout.Height(32)))
            {
                CleanupPlayTestScene();
            }
            GUI.backgroundColor = Color.white;

            EndCard();

            // CARD 3: CHƠI THỬ TRỰC TIẾP (PLAY MODE)
            BeginCard("3. ▶️ VÀO PLAY MODE ĐỂ CHƠI THỬ GAMEPLAY", "🕹️", "Khởi động GameManager và tương tác điều khiển xe");

            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.35f);
            if (GUILayout.Button("▶️ BẮT ĐẦU CHƠI THỬ GAME (VÀO PLAY MODE)", GUILayout.Height(44)))
            {
                SpawnLevelPreviewToScene(enterPlayMode: true);
            }
            GUI.backgroundColor = Color.white;

            EndCard();

            // CARD 4: HƯỚNG DẪN ĐIỀU KHIỂN & LUẬT CHƠI
            BeginCard("4. 💡 HƯỚNG DẪN LUẬT CHƠI & ĐIỀU KHIỂN", "ℹ️", "Cơ chế chuẩn theo game gốc Douyin");
            EditorGUILayout.HelpBox(
                "• 1. Bấm chuột trái vào xe ở HÀNG 1 của Bãi đỗ xe để đưa xe vào đường đua chữ U.\n" +
                "• 2. GỘP XE: Khi có 3 xe cùng màu trên đường đua, chúng sẽ tự động bay lại gần và gộp thành 1 xe Extra Car 2 tầng (sức chứa cộng dồn)!\n" +
                "• 3. Xe chạy qua CỔNG ĐÓN KHÁCH (Gate) ở chân tranh cát: Hành khách cùng màu sẽ bay theo hình parabol (ASMR) vào xe.\n" +
                "• 4. Khi xe ĐẦY KHÁCH: Xe sẽ tự động rẽ sang làn thoát (Exit Path) rời khỏi đường loop.\n" +
                "• 5. Các xe phía sau trên cùng cột của bãi đỗ sẽ tự động tiến lên hàng trên.\n" +
                "• 6. Khi toàn bộ tranh cát được dọn sạch -> THẮNG CUỘC!",
                MessageType.Info);
            EndCard();
        }

        public void SpawnLevelPreviewToScene(bool enterPlayMode = false)
        {
            CleanupPlayTestScene();

            // Tải GamePrefabData ScriptableObject
            var prefabData = AssetDatabase.LoadAssetAtPath<GamePrefabData>("Assets/Data/GamePrefabData.asset");
            if (prefabData == null)
            {
                Debug.LogWarning("[Level Designer] Không tìm thấy GamePrefabData tại Assets/Data/GamePrefabData.asset!");
            }

            // Tạo GameObject gốc cho Level Preview
            GameObject worldRoot = new GameObject($"[Douyin_Level_{levelId}_Preview]");

            // 1. Tạo Tranh Cát 40x40 NẰM NGANG 90 ĐỘ TRÊN MẶT ĐẤT BẰNG PREFAB (Passenger.prefab)
            Vector3 boardPos = sandBoardPos;
            GameObject boardObj = new GameObject("SandBoard_Manager");
            boardObj.transform.SetParent(worldRoot.transform);
            boardObj.transform.position = boardPos;
            var sandMgr = boardObj.AddComponent<SandBoardManager>();
            sandMgr.boardPosZ = sandBoardPos.z;
            sandMgr.columns = sandColumns;
            sandMgr.rows = sandRows;
            sandMgr.beadSpacing = sandBeadSpacing;
            sandMgr.beadRadius = sandBeadRadius;
            sandMgr.beadScale = sandBeadScale;
            sandMgr.enableZigzag = sandEnableZigzag;
            sandMgr.zigzagOffset = sandZigzagOffset;
            sandMgr.enableRoadCutout = sandEnableRoadCutout;
            sandMgr.cutoutColMin = sandCutoutColMin;
            sandMgr.cutoutColMax = sandCutoutColMax;
            sandMgr.cutoutRowMax = sandCutoutRowMax;
            sandMgr.BuildBoard(pixelMapData, prefabData);

            // 2. Tạo Đường Loop Track Chữ U & Extra Car
            var trackSys = TrackGeneratorHelper.SetupDefaultLoopTrack(boardPos);
            trackSys.transform.SetParent(worldRoot.transform);
            if (prefabData != null)
            {
                trackSys.extraCarPrefab = prefabData.extraCarPrefab;
                trackSys.busColors = prefabData.busColors;
                trackSys.prefabData = prefabData;
            }

            // 3. Tạo Bãi Đỗ Xe BẰNG PREFAB THẬT (Car.prefab)
            GameObject parkingObj = new GameObject("ParkingLot_Manager");
            parkingObj.transform.SetParent(worldRoot.transform);
            parkingObj.transform.position = new Vector3(0, 0, -4.5f);
            var parkMgr = parkingObj.AddComponent<ParkingLotManager>();
            parkMgr.SpawnParkingLot(carGrid, gridRows, gridCols, prefabData);

            // 4. Cài đặt Camera góc nhìn Isometric nhìn xuống Level nằm ngang
            SetupPlayTestCamera(worldRoot.transform);

            // 5. Căn chỉnh SceneView để người dùng nhìn thấy ngay toàn bộ bàn chơi
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.Frame(new Bounds(new Vector3(0, 0, 3f), new Vector3(26f, 12f, 36f)), false);
            }

            // Đánh dấu dirty để Scene cập nhật
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            if (enterPlayMode)
            {
                var gm = worldRoot.AddComponent<DouyinGameManager>();
                gm.StartGame();
                Debug.Log($"<color=green>[Level Designer Pro]</color> Đã dựng Scene và BẮT ĐẦU CHƠI THỬ Level {levelId}!");
            }
            else
            {
                Debug.Log($"<color=cyan>[Level Designer Pro]</color> Đã sinh thử Level {levelId} ra Scene hoàn toàn bằng PREFAB trong Edit Mode (KHÔNG PLAY)! Bạn có thể quan sát, click chọn xe, và kiểm tra thông số trực tiếp.");
            }
        }

        private void SetupPlayTestCamera(Transform parent)
        {
            Camera cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                cam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
                camObj.AddComponent<AudioListener>();
            }

            cam.orthographic = true;
            cam.orthographicSize = camBaseOrthoSize;
            cam.transform.position = camPosition;
            cam.transform.rotation = Quaternion.Euler(camRotation);

            var adapter = cam.GetComponent<CameraResolutionAdapter>();
            if (adapter == null)
            {
                adapter = cam.gameObject.AddComponent<CameraResolutionAdapter>();
            }
            adapter.baseOrthoSize = camBaseOrthoSize;
            adapter.referenceResolution = camRefResolution;
            adapter.fitMode = camFitMode;
            adapter.lockTransform = camLockTransform;
            adapter.RecordFixedTransform();
            adapter.ApplyCameraFit();
        }

        public void CleanupPlayTestScene()
        {
            // CHỈ XÓA ĐÚNG CÁC OBJECT MÔ PHỎNG PREVIEW DO TOOL SINH RA, KHÔNG XÓA OBJECT GỐC TRÊN SCENE
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var r in roots)
            {
                if (r.name.StartsWith("[Douyin_"))
                {
                    DestroyImmediate(r);
                }
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            Debug.Log("<color=yellow>[Level Designer Pro]</color> Đã dọn dẹp sạch toàn bộ Object mô phỏng Level trên Scene!");
        }
        #endregion

        #region CÁC HÀM XỬ LÝ LOGIC, GRID & JSON
        private void InitGrid(int rCount, int cCount)
        {
            gridRows = Mathf.Clamp(rCount, 2, 25);
            gridCols = Mathf.Clamp(cCount, 3, 10);
            carGrid = new string[gridRows, gridCols];
            for (int r = 0; r < gridRows; r++)
                for (int c = 0; c < gridCols; c++)
                    carGrid[r, c] = "-1_0_0";
        }

        private void ResizeGrid(int newRows, int newCols)
        {
            newRows = Mathf.Clamp(newRows, 2, 25);
            newCols = Mathf.Clamp(newCols, 3, 10);

            string[,] newGrid = new string[newRows, newCols];
            for (int r = 0; r < newRows; r++)
            {
                for (int c = 0; c < newCols; c++)
                {
                    if (carGrid != null && r < gridRows && c < gridCols)
                        newGrid[r, c] = carGrid[r, c];
                    else
                        newGrid[r, c] = "-1_0_0";
                }
            }
            carGrid = newGrid;
            gridRows = newRows;
            gridCols = newCols;
        }

        private void ShuffleCars()
        {
            List<string> cars = new List<string>();
            for (int r = 0; r < gridRows; r++)
                for (int c = 0; c < gridCols; c++)
                    if (!carGrid[r, c].StartsWith("-1"))
                        cars.Add(carGrid[r, c]);

            for (int i = 0; i < cars.Count; i++)
            {
                int rnd = UnityEngine.Random.Range(i, cars.Count);
                string tmp = cars[i];
                cars[i] = cars[rnd];
                cars[rnd] = tmp;
            }

            int idx = 0;
            for (int r = 0; r < gridRows; r++)
            {
                for (int c = 0; c < gridCols; c++)
                {
                    if (!carGrid[r, c].StartsWith("-1") && idx < cars.Count)
                    {
                        carGrid[r, c] = cars[idx++];
                    }
                }
            }
        }

        private void AutoGenerateWinningFleet()
        {
            List<string> neededCars = new List<string>();

            foreach (var kvp in sandColorCounts)
            {
                int colorIdx = kvp.Key;
                int sandCount = kvp.Value;
                if (sandCount <= 0) continue;

                int remaining = sandCount;
                while (remaining > 0)
                {
                    int cap = autoBaseCapacity;
                    if (remaining >= autoBaseCapacity)
                    {
                        cap = autoBaseCapacity;
                    }
                    else if (remaining >= 50)
                    {
                        cap = 50;
                    }
                    else
                    {
                        cap = remaining;
                    }

                    neededCars.Add($"{colorIdx}_{cap}_0");
                    remaining -= cap;
                }
            }

            int totalCars = neededCars.Count;
            if (totalCars == 0) return;

            int neededRows = Mathf.Max(2, Mathf.CeilToInt((float)totalCars / gridCols));

            gridRows = neededRows;
            carGrid = new string[gridRows, gridCols];
            for (int r = 0; r < gridRows; r++)
                for (int c = 0; c < gridCols; c++)
                    carGrid[r, c] = "-1_0_0";

            for (int i = 0; i < neededCars.Count; i++)
            {
                int r = i / gridCols;
                int c = i % gridCols;
                if (r < gridRows && c < gridCols)
                {
                    carGrid[r, c] = neededCars[i];
                }
            }

            Debug.Log($"<color=green>[Level Designer Pro]</color> Đã sinh vừa đủ {totalCars} xe trên {gridRows} hàng x {gridCols} cột (Level đã cân bằng 100% điều kiện thắng)!");
        }

        private void AddVehicleSmart(int colorIdx, int cap, int mech)
        {
            for (int r = 0; r < gridRows; r++)
            {
                for (int c = 0; c < gridCols; c++)
                {
                    if (carGrid[r, c].StartsWith("-1"))
                    {
                        carGrid[r, c] = $"{colorIdx}_{cap}_{mech}";
                        return;
                    }
                }
            }

            ResizeGrid(gridRows + 1, gridCols);
            carGrid[gridRows - 1, 0] = $"{colorIdx}_{cap}_{mech}";
        }

        private string GetVehicleCategoryName(int cap)
        {
            if (cap <= 4) return "Sedan (4 Chỗ)";
            if (cap <= 6) return "Van / MPV (6 Chỗ)";
            if (cap <= 16) return "Minibus (10-16 Chỗ)";
            if (cap <= 60) return "Xe Khách Trung (50 Chỗ)";
            if (cap <= 120) return "Xe Buýt Lớn (100 Chỗ)";
            return $"Xe Siêu Trọng ({cap} Chỗ)";
        }

        private void LoadPresetImage(string path)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null)
            {
                inputPixelArt = tex;
                ScanPixelArtTexture();
            }
        }

        private void ScanPixelArtTexture()
        {
            if (inputPixelArt == null) return;

            string path = AssetDatabase.GetAssetPath(inputPixelArt);
            if (!string.IsNullOrEmpty(path))
            {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null && (!importer.isReadable || importer.textureCompression != TextureImporterCompression.Uncompressed))
                {
                    importer.isReadable = true;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
            }

            RecordUndo();
            sandColorCounts.Clear();
            int w = inputPixelArt.width;
            int h = inputPixelArt.height;

            for (int y = 0; y < 40; y++)
            {
                for (int x = 0; x < 40; x++)
                {
                    int sampleX = Mathf.Clamp(Mathf.FloorToInt((float)x / 40f * w), 0, w - 1);
                    int sampleY = Mathf.Clamp(Mathf.FloorToInt((float)y / 40f * h), 0, h - 1);

                    Color c = inputPixelArt.GetPixel(sampleX, sampleY);
                    int closestIdx = FindClosestColorIndex(c);
                    pixelMapData[y, x] = closestIdx;

                    if (closestIdx >= 0)
                    {
                        if (!sandColorCounts.ContainsKey(closestIdx)) sandColorCounts[closestIdx] = 0;
                        sandColorCounts[closestIdx]++;
                    }
                }
            }

            UpdateSandCanvasTexture();
            Repaint();
        }

        public static int FindClosestColorIndex(Color pixelColor)
        {
            if (pixelColor.a < 0.1f) return -1;

            int bestIdx = 0;
            float minDist = float.MaxValue;

            for (int i = 0; i < StandardPalette.Length; i++)
            {
                float dr = pixelColor.r - StandardPalette[i].r;
                float dg = pixelColor.g - StandardPalette[i].g;
                float db = pixelColor.b - StandardPalette[i].b;
                float dist = dr * dr + dg * dg + db * db;
                if (dist < minDist)
                {
                    minDist = dist;
                    bestIdx = i;
                }
            }
            return bestIdx;
        }

        private void RecalculateSandColorCounts()
        {
            sandColorCounts.Clear();
            for (int y = 0; y < 40; y++)
            {
                for (int x = 0; x < 40; x++)
                {
                    int c = pixelMapData[y, x];
                    if (c >= 0)
                    {
                        if (!sandColorCounts.ContainsKey(c)) sandColorCounts[c] = 0;
                        sandColorCounts[c]++;
                    }
                }
            }
        }

        private int GetTotalSandCount()
        {
            int total = 0;
            foreach (var v in sandColorCounts.Values) total += v;
            return total;
        }

        private Dictionary<int, int> CalculateCarSeats()
        {
            Dictionary<int, int> counts = new Dictionary<int, int>();
            for (int r = 0; r < gridRows; r++)
            {
                for (int c = 0; c < gridCols; c++)
                {
                    string cell = carGrid[r, c];
                    string[] parts = cell.Split('_');
                    if (parts.Length >= 2)
                    {
                        int colorIdx, cap;
                        if (int.TryParse(parts[0], out colorIdx) && int.TryParse(parts[1], out cap))
                        {
                            if (colorIdx >= 0)
                            {
                                if (!counts.ContainsKey(colorIdx)) counts[colorIdx] = 0;
                                counts[colorIdx] += cap;
                            }
                        }
                    }
                }
            }
            return counts;
        }

        public void ExportLevelFiles()
        {
            string folderPath = "Assets/Scene_Data/Levels";
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            string jsonPath = $"{folderPath}/Level_{levelId}_Config.json";

            List<string> busList = new List<string>();
            for (int r = 0; r < gridRows; r++)
            {
                for (int c = 0; c < gridCols; c++)
                {
                    if (!string.IsNullOrEmpty(carGrid[r, c]) && !carGrid[r, c].StartsWith("-1"))
                    {
                        busList.Add(carGrid[r, c]);
                    }
                }
            }

            // Nén ma trận 40x40 pixelMapData thành chuỗi phân cách bởi dấu phẩy
            System.Text.StringBuilder sbSand = new System.Text.StringBuilder(40 * 40 * 3);
            for (int y = 0; y < 40; y++)
            {
                for (int x = 0; x < 40; x++)
                {
                    if (y > 0 || x > 0) sbSand.Append(",");
                    sbSand.Append(pixelMapData[y, x]);
                }
            }

            string jsonContent = "{\n";
            jsonContent += $"  \"level_id\": {levelId},\n";
            jsonContent += $"  \"grid_rows\": {gridRows},\n";
            jsonContent += $"  \"grid_cols\": {gridCols},\n";
            jsonContent += "  \"bus\": [\n";
            for (int i = 0; i < busList.Count; i++)
            {
                jsonContent += $"    \"{busList[i]}\"{(i < busList.Count - 1 ? "," : "")}\n";
            }
            jsonContent += "  ],\n";
            jsonContent += $"  \"sand_data\": \"{sbSand.ToString()}\"\n";
            jsonContent += "}\n";

            File.WriteAllText(jsonPath, jsonContent);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Thành Công!", $"Đã xuất file Level Config JSON (Bao gồm bãi xe & tranh cát 40x40) tại:\n{jsonPath}", "OK");
        }

        public bool LoadLevelConfig(int targetLevelId)
        {
            string folderPath = "Assets/Scene_Data/Levels";
            string jsonPath = $"{folderPath}/Level_{targetLevelId}_Config.json";

            if (!File.Exists(jsonPath))
            {
                EditorUtility.DisplayDialog("Lỗi", $"Không tìm thấy file cấu hình Level tại:\n{jsonPath}\n\nVui lòng kiểm tra lại số Level!", "OK");
                return false;
            }

            try
            {
                string json = File.ReadAllText(jsonPath);

                int rows = 5, cols = 5;
                var matchRows = System.Text.RegularExpressions.Regex.Match(json, "\"grid_rows\"\\s*:\\s*(\\d+)");
                if (matchRows.Success) int.TryParse(matchRows.Groups[1].Value, out rows);

                var matchCols = System.Text.RegularExpressions.Regex.Match(json, "\"grid_cols\"\\s*:\\s*(\\d+)");
                if (matchCols.Success) int.TryParse(matchCols.Groups[1].Value, out cols);

                InitGrid(rows, cols);

                // Nạp danh sách bus
                var busMatch = System.Text.RegularExpressions.Regex.Match(json, "\"bus\"\\s*:\\s*\\[([\\s\\S]*?)\\]");
                if (busMatch.Success)
                {
                    string busContent = busMatch.Groups[1].Value;
                    var carMatches = System.Text.RegularExpressions.Regex.Matches(busContent, "\"([^\"]+)\"");
                    int idx = 0;
                    foreach (System.Text.RegularExpressions.Match m in carMatches)
                    {
                        int r = idx / cols;
                        int c = idx % cols;
                        if (r < rows && c < cols)
                        {
                            carGrid[r, c] = m.Groups[1].Value;
                        }
                        idx++;
                    }
                }

                // Nạp sand_data nếu có
                var sandMatch = System.Text.RegularExpressions.Regex.Match(json, "\"sand_data\"\\s*:\\s*\"([^\"]+)\"");
                if (sandMatch.Success)
                {
                    string[] tokens = sandMatch.Groups[1].Value.Split(',');
                    sandColorCounts.Clear();
                    for (int y = 0; y < 40; y++)
                    {
                        for (int x = 0; x < 40; x++)
                        {
                            int index = y * 40 + x;
                            int colorVal = -1;
                            if (index < tokens.Length && int.TryParse(tokens[index], out int parsed))
                            {
                                colorVal = parsed;
                            }
                            pixelMapData[y, x] = colorVal;
                            if (colorVal >= 0)
                            {
                                if (!sandColorCounts.ContainsKey(colorVal)) sandColorCounts[colorVal] = 0;
                                sandColorCounts[colorVal]++;
                            }
                        }
                    }
                    UpdateSandCanvasTexture();
                }
                else
                {
                    // Nếu JSON chưa có sand_data, tìm ảnh mẫu level nếu có
                    string presetPath = $"Assets/Level/{targetLevelId}.png";
                    if (File.Exists(presetPath))
                    {
                        LoadPresetImage(presetPath);
                    }
                    else if (File.Exists("Assets/Level/13.png") && sandColorCounts.Count == 0)
                    {
                        LoadPresetImage("Assets/Level/13.png");
                    }
                }

                levelId = targetLevelId;
                Repaint();
                Debug.Log($"<color=green>[Level Designer Pro]</color> Đã nạp thành công cấu hình Level {targetLevelId} từ file JSON!");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Level Designer Pro] Lỗi khi nạp file JSON Level {targetLevelId}: {ex.Message}");
                EditorUtility.DisplayDialog("Lỗi", $"Không thể nạp file JSON:\n{ex.Message}", "OK");
                return false;
            }
        }
        #endregion

        #region TAB 5: CAMERA & BÀN CÁT 3D (CỐ ĐỊNH & FIT TỈ LỆ)
        private void DrawCameraAndBoardTab()
        {
            EditorGUI.BeginChangeCheck();

            // CARD 1: CAMERA CỐ ĐỊNH & TỰ ĐỘNG FIT THEO TỈ LỆ
            BeginCard("1. 📸 THIẾT LẬP CAMERA CỐ ĐỊNH & ADAPTER TỈ LỆ (CAMERA ADAPTER)", "📷", "Khóa góc nhìn cố định 100%, tự động thích ứng iPhone dài & iPad ngắn");

            EditorGUILayout.LabelField("1. Tọa Độ & Góc Xoay Cố Định (Fixed Transform):", EditorStyles.boldLabel);
            camPosition = EditorGUILayout.Vector3Field("Tọa độ Camera (Position):", camPosition);
            camRotation = EditorGUILayout.Vector3Field("Góc xoay Camera (Euler Angles):", camRotation);
            camLockTransform = EditorGUILayout.Toggle("🔒 Khóa cố định 100% (Lock Transform):", camLockTransform);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("2. Kích Thước Orthographic & Chế Độ Thích Ứng (Aspect Fit):", EditorStyles.boldLabel);
            camBaseOrthoSize = EditorGUILayout.Slider("Ortho Size chuẩn (Base Ortho):", camBaseOrthoSize, 5f, 30f);
            camRefResolution = EditorGUILayout.Vector2Field("Độ phân giải chuẩn (Design Res):", camRefResolution);
            camFitMode = (CameraResolutionAdapter.AspectFitMode)EditorGUILayout.EnumPopup("Chế độ thích ứng (Fit Mode):", camFitMode);

            EditorGUILayout.Space(4);
            // Bảng tính tỉ lệ mô phỏng
            float targetAspect = camRefResolution.y > 0 ? camRefResolution.x / camRefResolution.y : (750f / 1334f);
            float iphoneAspect = 9f / 19.5f; // ~0.4615
            float ipadAspect = 3f / 4f;      // 0.75
            float iphoneOrtho = camBaseOrthoSize * (targetAspect / iphoneAspect);
            float ipadOrtho = camBaseOrthoSize;

            EditorGUILayout.HelpBox(
                $"📱 MÔ PHỎNG CO GIÃN THỰC TẾ TRÊN CÁC THIẾT BỊ:\n" +
                $"• Chuẩn thiết kế ({camRefResolution.x:0}×{camRefResolution.y:0} - 9:16): Ortho Size = {camBaseOrthoSize:F1}\n" +
                $"• Màn dài (iPhone 16 / Galaxy S24 Ultra - 19.5:9, ratio {iphoneAspect:F2}): Ortho Size = {iphoneOrtho:F1} (Mở rộng để giữ nguyên bề ngang 40 cột, không bị cắt 2 mép!)\n" +
                $"• Màn ngắn (iPad / Tablet 3:4, ratio {ipadAspect:F2}): Ortho Size = {ipadOrtho:F1} (Giữ nguyên chiều cao để lề trên Bàn Cát và lề dưới Bãi Xe luôn hiển thị trọn vẹn!)",
                MessageType.Info);

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.25f, 0.9f, 0.45f);
            if (GUILayout.Button("🎯 ÁP DỤNG VÀO CAMERA SCENE NGAY", GUILayout.Height(30)))
            {
                ApplyCameraSettingsToScene();
            }

            GUI.backgroundColor = new Color(0.2f, 0.75f, 1.0f);
            if (GUILayout.Button("🔍 LẤY TỌA ĐỘ TỪ CAMERA SCENE", GUILayout.Height(30)))
            {
                SyncCameraSettingsFromScene();
            }

            GUI.backgroundColor = new Color(1.0f, 0.85f, 0.3f);
            if (GUILayout.Button("↺ VỀ MẶC ĐỊNH CHUẨN", GUILayout.Height(30), GUILayout.Width(170)))
            {
                camPosition = new Vector3(0f, 18.5f, -3.5f);
                camRotation = new Vector3(53f, 0f, 0f);
                camBaseOrthoSize = 15f;
                camRefResolution = new Vector2(750f, 1334f);
                camFitMode = CameraResolutionAdapter.AspectFitMode.FitAll;
                camLockTransform = true;
                ApplyCameraSettingsToScene();
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            EndCard();

            // CARD 2: BÀN CÁT 3D & VỊ TRÍ (SAND BOARD 40x40)
            BeginCard("2. 🏖️ THIẾT LẬP BÀN CÁT 3D & VỊ TRÍ (SAND BOARD 40×40)", "🏖️", "Căn chỉnh vị trí Z=6.0, Scale hạt, lệch Zigzag và khoét rãnh xe (trực quan theo Inspector)");

            EditorGUILayout.LabelField("1. Vị Trí Bàn Cát (Board Position):", EditorStyles.boldLabel);
            sandBoardPos = EditorGUILayout.Vector3Field("Vị trí Bàn Cát (Position):", sandBoardPos);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("2. Kích Thước & Khoảng Cách Hạt Cát:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            sandColumns = EditorGUILayout.IntField("Số Cột (Columns):", sandColumns);
            sandRows = EditorGUILayout.IntField("Số Hàng (Rows):", sandRows);
            EditorGUILayout.EndHorizontal();

            sandBeadSpacing = EditorGUILayout.Slider("Khoảng cách hạt (Bead Spacing):", sandBeadSpacing, 0.1f, 1.0f);
            sandBeadRadius = EditorGUILayout.Slider("Bán kính hạt (Bead Radius):", sandBeadRadius, 0.05f, 0.5f);
            sandBeadScale = EditorGUILayout.Slider("Scale hạt cát (Bead Scale):", sandBeadScale, 0.1f, 2.0f);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("3. Lệch Zigzag Cát (So Le Hàng):", EditorStyles.boldLabel);
            sandEnableZigzag = EditorGUILayout.Toggle("Bật lệch Zigzag (Enable Zigzag):", sandEnableZigzag);
            sandZigzagOffset = EditorGUILayout.Slider("Độ lệch Zigzag (Offset):", sandZigzagOffset, -0.5f, 0.5f);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("4. Khoét Rãnh Đường Cong Xe Chạy:", EditorStyles.boldLabel);
            sandEnableRoadCutout = EditorGUILayout.Toggle("Khoét rãnh (Road Cutout):", sandEnableRoadCutout);
            EditorGUILayout.BeginHorizontal();
            sandCutoutColMin = EditorGUILayout.IntField("Cột bắt đầu (Col Min):", sandCutoutColMin);
            sandCutoutColMax = EditorGUILayout.IntField("Cột kết thúc (Col Max):", sandCutoutColMax);
            sandCutoutRowMax = EditorGUILayout.IntField("Hàng tối đa (Row Max):", sandCutoutRowMax);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("⚡ THAO TÁC NHANH TRỰC TIẾP TRÊN SCENE (XEM NGAY KHÔNG CẦN CHẠY GAME):", EditorStyles.boldLabel);

            // Nút 1 & Nút 2 (Row 1)
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.2f, 0.85f, 1.0f);
            if (GUILayout.Button("⚡ CẬP NHẬT SCALE & LỆCH ZIGZAG (XEM NGAY)", GUILayout.Height(36)))
            {
                ApplySandBoardSettingsToScene(updateExisting: true);
            }

            GUI.backgroundColor = new Color(0.35f, 0.95f, 0.45f);
            if (GUILayout.Button("🔄 TÁI TẠO TOÀN BỘ TRANH CÁT (REBUILD)", GUILayout.Height(36)))
            {
                RebuildSandBoardInScene();
            }
            EditorGUILayout.EndHorizontal();

            // Nút 3 & Nút 4 (Row 2)
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(1.0f, 0.9f, 0.3f);
            if (GUILayout.Button("📍 ĐẶT VỊ TRÍ Z = 6.0", GUILayout.Height(28)))
            {
                sandBoardPos = new Vector3(sandBoardPos.x, sandBoardPos.y, 6.0f);
                ApplySandBoardSettingsToScene(updateExisting: true);
                Debug.Log("<color=yellow>[Level Designer]</color> Đã đặt vị trí SandBoard tại Z = 6.0!");
            }

            GUI.backgroundColor = new Color(1.0f, 0.45f, 0.45f);
            if (GUILayout.Button("🗑️ XÓA TRANH CÁT TRÊN SCENE", GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog("Xác nhận xóa", "Bạn có chắc chắn muốn xóa toàn bộ hạt cát trên Scene không?", "Xóa", "Hủy"))
                {
                    ClearSandBoardInScene();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.2f, 0.75f, 1.0f);
            if (GUILayout.Button("🔍 LẤY THÔNG SỐ TỪ SAND BOARD TRONG SCENE", GUILayout.Height(26)))
            {
                SyncSandBoardSettingsFromScene();
            }

            GUI.backgroundColor = new Color(0.35f, 0.9f, 0.6f);
            if (GUILayout.Button("💾 GHI ĐÈ VÀO SAND BOARD TRONG SCENE", GUILayout.Height(26)))
            {
                ApplySandBoardSettingsToScene(updateExisting: true);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EndCard();

            // CARD 3: LƯU TRỮ VĨNH VIỄN & KHÔI PHỤC
            BeginCard("3. 💾 LƯU TRỮ VĨNH VIỄN & KHÔI PHỤC (PERSISTENCE)", "💾", "Mọi thay đổi được lưu vào EditorPrefs, lần sau mở tool lên sẽ giữ nguyên 100%!");

            EditorGUILayout.HelpBox("✅ Trạng thái: Toàn bộ thông số Camera & Bàn Cát đang được tự động đồng bộ và lưu vào EditorPrefs. Khi bạn tắt Unity hay mở lại Tool, các giá trị này không bao giờ bị mất!", MessageType.None);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f);
            if (GUILayout.Button("💾 LƯU THIẾT LẬP NÀY LÀM MẶC ĐỊNH NGAY", GUILayout.Height(30)))
            {
                SaveSettingsToEditorPrefs();
                Debug.Log("<color=green>[Level Designer]</color> Đã lưu toàn bộ thông số Camera & Bàn Cát vào EditorPrefs thành công!");
            }

            GUI.backgroundColor = new Color(1.0f, 0.5f, 0.5f);
            if (GUILayout.Button("↺ KHÔI PHỤC THIẾT LẬP CHUẨN GỐC BAN ĐẦU", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Xác nhận", "Khôi phục toàn bộ cấu hình Camera và Bàn Cát về giá trị gốc của trò chơi?", "Khôi phục", "Hủy"))
                {
                    ResetSettingsToDefaults();
                    ApplyCameraSettingsToScene();
                    ApplySandBoardSettingsToScene(updateExisting: true);
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EndCard();

            if (EditorGUI.EndChangeCheck())
            {
                SaveSettingsToEditorPrefs();
            }
        }

        #region XỬ LÝ LƯU TRỮ EDITORPREFS & ĐỒNG BỘ SCENE
        public void SaveSettingsToEditorPrefs()
        {
            EditorPrefs.SetFloat(PREF_KEY_CAM_POS_X, camPosition.x);
            EditorPrefs.SetFloat(PREF_KEY_CAM_POS_Y, camPosition.y);
            EditorPrefs.SetFloat(PREF_KEY_CAM_POS_Z, camPosition.z);
            EditorPrefs.SetFloat(PREF_KEY_CAM_ROT_X, camRotation.x);
            EditorPrefs.SetFloat(PREF_KEY_CAM_ROT_Y, camRotation.y);
            EditorPrefs.SetFloat(PREF_KEY_CAM_ROT_Z, camRotation.z);
            EditorPrefs.SetFloat(PREF_KEY_CAM_ORTHO, camBaseOrthoSize);
            EditorPrefs.SetFloat(PREF_KEY_CAM_REF_W, camRefResolution.x);
            EditorPrefs.SetFloat(PREF_KEY_CAM_REF_H, camRefResolution.y);
            EditorPrefs.SetInt(PREF_KEY_CAM_FIT_MODE, (int)camFitMode);
            EditorPrefs.SetBool(PREF_KEY_CAM_LOCK, camLockTransform);

            EditorPrefs.SetFloat(PREF_KEY_BOARD_POS_X, sandBoardPos.x);
            EditorPrefs.SetFloat(PREF_KEY_BOARD_POS_Y, sandBoardPos.y);
            EditorPrefs.SetFloat(PREF_KEY_BOARD_POS_Z, sandBoardPos.z);
            EditorPrefs.SetFloat(PREF_KEY_BEAD_SPACING, sandBeadSpacing);
            EditorPrefs.SetFloat(PREF_KEY_BEAD_RADIUS, sandBeadRadius);
            EditorPrefs.SetFloat(PREF_KEY_BEAD_SCALE, sandBeadScale);
            EditorPrefs.SetBool(PREF_KEY_ENABLE_ZIGZAG, sandEnableZigzag);
            EditorPrefs.SetFloat(PREF_KEY_ZIGZAG_OFFSET, sandZigzagOffset);
            EditorPrefs.SetBool(PREF_KEY_ENABLE_CUTOUT, sandEnableRoadCutout);
            EditorPrefs.SetInt(PREF_KEY_CUTOUT_COL_MIN, sandCutoutColMin);
            EditorPrefs.SetInt(PREF_KEY_CUTOUT_COL_MAX, sandCutoutColMax);
            EditorPrefs.SetInt(PREF_KEY_CUTOUT_ROW_MAX, sandCutoutRowMax);
        }

        public void LoadSettingsFromEditorPrefs()
        {
            if (EditorPrefs.HasKey(PREF_KEY_CAM_POS_X))
            {
                camPosition = new Vector3(
                    EditorPrefs.GetFloat(PREF_KEY_CAM_POS_X, 0f),
                    EditorPrefs.GetFloat(PREF_KEY_CAM_POS_Y, 18.5f),
                    EditorPrefs.GetFloat(PREF_KEY_CAM_POS_Z, -3.5f)
                );
            }
            if (EditorPrefs.HasKey(PREF_KEY_CAM_ROT_X))
            {
                camRotation = new Vector3(
                    EditorPrefs.GetFloat(PREF_KEY_CAM_ROT_X, 53f),
                    EditorPrefs.GetFloat(PREF_KEY_CAM_ROT_Y, 0f),
                    EditorPrefs.GetFloat(PREF_KEY_CAM_ROT_Z, 0f)
                );
            }
            if (EditorPrefs.HasKey(PREF_KEY_CAM_ORTHO)) camBaseOrthoSize = EditorPrefs.GetFloat(PREF_KEY_CAM_ORTHO, 15f);
            if (EditorPrefs.HasKey(PREF_KEY_CAM_REF_W))
            {
                camRefResolution = new Vector2(
                    EditorPrefs.GetFloat(PREF_KEY_CAM_REF_W, 750f),
                    EditorPrefs.GetFloat(PREF_KEY_CAM_REF_H, 1334f)
                );
            }
            if (EditorPrefs.HasKey(PREF_KEY_CAM_FIT_MODE)) camFitMode = (CameraResolutionAdapter.AspectFitMode)EditorPrefs.GetInt(PREF_KEY_CAM_FIT_MODE, 0);
            if (EditorPrefs.HasKey(PREF_KEY_CAM_LOCK)) camLockTransform = EditorPrefs.GetBool(PREF_KEY_CAM_LOCK, true);

            if (EditorPrefs.HasKey(PREF_KEY_BOARD_POS_X))
            {
                sandBoardPos = new Vector3(
                    EditorPrefs.GetFloat(PREF_KEY_BOARD_POS_X, 0f),
                    EditorPrefs.GetFloat(PREF_KEY_BOARD_POS_Y, 0f),
                    EditorPrefs.GetFloat(PREF_KEY_BOARD_POS_Z, 6f)
                );
            }
            if (EditorPrefs.HasKey(PREF_KEY_BEAD_SPACING)) sandBeadSpacing = EditorPrefs.GetFloat(PREF_KEY_BEAD_SPACING, 0.5f);
            if (EditorPrefs.HasKey(PREF_KEY_BEAD_RADIUS)) sandBeadRadius = EditorPrefs.GetFloat(PREF_KEY_BEAD_RADIUS, 0.13f);
            if (EditorPrefs.HasKey(PREF_KEY_BEAD_SCALE)) sandBeadScale = EditorPrefs.GetFloat(PREF_KEY_BEAD_SCALE, 0.4f);
            if (EditorPrefs.HasKey(PREF_KEY_ENABLE_ZIGZAG)) sandEnableZigzag = EditorPrefs.GetBool(PREF_KEY_ENABLE_ZIGZAG, true);
            if (EditorPrefs.HasKey(PREF_KEY_ZIGZAG_OFFSET)) sandZigzagOffset = EditorPrefs.GetFloat(PREF_KEY_ZIGZAG_OFFSET, 0.25f);
            if (EditorPrefs.HasKey(PREF_KEY_ENABLE_CUTOUT)) sandEnableRoadCutout = EditorPrefs.GetBool(PREF_KEY_ENABLE_CUTOUT, true);
            if (EditorPrefs.HasKey(PREF_KEY_CUTOUT_COL_MIN)) sandCutoutColMin = EditorPrefs.GetInt(PREF_KEY_CUTOUT_COL_MIN, 14);
            if (EditorPrefs.HasKey(PREF_KEY_CUTOUT_COL_MAX)) sandCutoutColMax = EditorPrefs.GetInt(PREF_KEY_CUTOUT_COL_MAX, 25);
            if (EditorPrefs.HasKey(PREF_KEY_CUTOUT_ROW_MAX)) sandCutoutRowMax = EditorPrefs.GetInt(PREF_KEY_CUTOUT_ROW_MAX, 18);
        }

        public void ResetSettingsToDefaults()
        {
            camPosition = new Vector3(0f, 18.5f, -3.5f);
            camRotation = new Vector3(53f, 0f, 0f);
            camBaseOrthoSize = 15f;
            camRefResolution = new Vector2(750f, 1334f);
            camFitMode = CameraResolutionAdapter.AspectFitMode.FitAll;
            camLockTransform = true;

            sandBoardPos = new Vector3(0f, 0f, 6.0f);
            sandColumns = 40;
            sandRows = 40;
            sandBeadSpacing = 0.5f;
            sandBeadRadius = 0.13f;
            sandBeadScale = 0.4f;
            sandEnableZigzag = true;
            sandZigzagOffset = 0.25f;
            sandEnableRoadCutout = true;
            sandCutoutColMin = 14;
            sandCutoutColMax = 25;
            sandCutoutRowMax = 18;

            SaveSettingsToEditorPrefs();
        }

        private void AutoLiveUpdateScene()
        {
            Camera cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = camBaseOrthoSize;
                cam.transform.position = camPosition;
                cam.transform.rotation = Quaternion.Euler(camRotation);

                var adapter = cam.GetComponent<CameraResolutionAdapter>();
                if (adapter != null)
                {
                    adapter.baseOrthoSize = camBaseOrthoSize;
                    adapter.referenceResolution = camRefResolution;
                    adapter.fitMode = camFitMode;
                    adapter.lockTransform = camLockTransform;
                    adapter.ApplyCameraFit();
                }
            }

            SandBoardManager sandMgr = SandBoardManager.Instance;
            if (sandMgr == null) sandMgr = UnityEngine.Object.FindFirstObjectByType<SandBoardManager>();
            if (sandMgr != null)
            {
                sandMgr.transform.position = sandBoardPos;
                sandMgr.boardPosZ = sandBoardPos.z;
                sandMgr.columns = sandColumns;
                sandMgr.rows = sandRows;
                sandMgr.beadSpacing = sandBeadSpacing;
                sandMgr.beadRadius = sandBeadRadius;
                sandMgr.beadScale = sandBeadScale;
                sandMgr.enableZigzag = sandEnableZigzag;
                sandMgr.zigzagOffset = sandZigzagOffset;
                sandMgr.enableRoadCutout = sandEnableRoadCutout;
                sandMgr.cutoutColMin = sandCutoutColMin;
                sandMgr.cutoutColMax = sandCutoutColMax;
                sandMgr.cutoutRowMax = sandCutoutRowMax;

                if (sandMgr.transform.childCount > 0)
                {
                    sandMgr.UpdateExistingBeads();
                }
            }

            SceneView.RepaintAll();
        }

        public void ApplyCameraSettingsToScene()
        {
            Camera cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                EditorUtility.DisplayDialog("Thông báo", "Không tìm thấy Camera nào trong Scene!", "OK");
                return;
            }

            Undo.RecordObject(cam.transform, "Apply Camera Settings");
            Undo.RecordObject(cam, "Apply Camera Settings");

            cam.orthographic = true;
            cam.orthographicSize = camBaseOrthoSize;
            cam.transform.position = camPosition;
            cam.transform.rotation = Quaternion.Euler(camRotation);

            var adapter = cam.GetComponent<CameraResolutionAdapter>();
            if (adapter == null)
            {
                adapter = Undo.AddComponent<CameraResolutionAdapter>(cam.gameObject);
            }

            Undo.RecordObject(adapter, "Apply Camera Settings");
            adapter.baseOrthoSize = camBaseOrthoSize;
            adapter.referenceResolution = camRefResolution;
            adapter.fitMode = camFitMode;
            adapter.lockTransform = camLockTransform;
            adapter.RecordFixedTransform();
            adapter.ApplyCameraFit();

            EditorUtility.SetDirty(cam.gameObject);
            SceneView.RepaintAll();
            Debug.Log($"<color=green>[Level Designer]</color> Đã áp dụng thiết lập vào Camera '{cam.name}' thành công!");
        }

        public void SyncCameraSettingsFromScene()
        {
            Camera cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                EditorUtility.DisplayDialog("Thông báo", "Không tìm thấy Camera nào trong Scene!", "OK");
                return;
            }

            camPosition = cam.transform.position;
            camRotation = cam.transform.eulerAngles;
            camBaseOrthoSize = cam.orthographicSize;

            var adapter = cam.GetComponent<CameraResolutionAdapter>();
            if (adapter != null)
            {
                camBaseOrthoSize = adapter.baseOrthoSize;
                camRefResolution = adapter.referenceResolution;
                camFitMode = adapter.fitMode;
                camLockTransform = adapter.lockTransform;
            }

            SaveSettingsToEditorPrefs();
            Debug.Log($"<color=cyan>[Level Designer]</color> Đã đồng bộ thông số từ Camera '{cam.name}' vào Tool!");
        }

        public void ApplySandBoardSettingsToScene(bool updateExisting = true)
        {
            SandBoardManager sandMgr = SandBoardManager.Instance;
            if (sandMgr == null) sandMgr = UnityEngine.Object.FindFirstObjectByType<SandBoardManager>();
            if (sandMgr == null)
            {
                EditorUtility.DisplayDialog("Thông báo", "Không tìm thấy SandBoardManager nào trong Scene!", "OK");
                return;
            }

            Undo.RecordObject(sandMgr.transform, "Apply Sand Board Settings");
            Undo.RecordObject(sandMgr, "Apply Sand Board Settings");

            sandMgr.transform.position = sandBoardPos;
            sandMgr.boardPosZ = sandBoardPos.z;
            sandMgr.columns = sandColumns;
            sandMgr.rows = sandRows;
            sandMgr.beadSpacing = sandBeadSpacing;
            sandMgr.beadRadius = sandBeadRadius;
            sandMgr.beadScale = sandBeadScale;
            sandMgr.enableZigzag = sandEnableZigzag;
            sandMgr.zigzagOffset = sandZigzagOffset;
            sandMgr.enableRoadCutout = sandEnableRoadCutout;
            sandMgr.cutoutColMin = sandCutoutColMin;
            sandMgr.cutoutColMax = sandCutoutColMax;
            sandMgr.cutoutRowMax = sandCutoutRowMax;

            if (updateExisting && sandMgr.transform.childCount > 0)
            {
                sandMgr.UpdateExistingBeads();
            }

            EditorUtility.SetDirty(sandMgr.gameObject);
            SceneView.RepaintAll();
            Debug.Log("<color=green>[Level Designer]</color> Đã áp dụng thông số vào SandBoardManager trong Scene!");
        }

        public void SyncSandBoardSettingsFromScene()
        {
            SandBoardManager sandMgr = SandBoardManager.Instance;
            if (sandMgr == null) sandMgr = UnityEngine.Object.FindFirstObjectByType<SandBoardManager>();
            if (sandMgr == null)
            {
                EditorUtility.DisplayDialog("Thông báo", "Không tìm thấy SandBoardManager trong Scene!", "OK");
                return;
            }

            sandBoardPos = sandMgr.transform.position;
            sandBoardPos.z = sandMgr.boardPosZ;
            sandColumns = sandMgr.columns;
            sandRows = sandMgr.rows;
            sandBeadSpacing = sandMgr.beadSpacing;
            sandBeadRadius = sandMgr.beadRadius;
            sandBeadScale = sandMgr.beadScale;
            sandEnableZigzag = sandMgr.enableZigzag;
            sandZigzagOffset = sandMgr.zigzagOffset;
            sandEnableRoadCutout = sandMgr.enableRoadCutout;
            sandCutoutColMin = sandMgr.cutoutColMin;
            sandCutoutColMax = sandMgr.cutoutColMax;
            sandCutoutRowMax = sandMgr.cutoutRowMax;

            SaveSettingsToEditorPrefs();
            Debug.Log("<color=cyan>[Level Designer]</color> Đã đồng bộ thông số từ SandBoardManager trong Scene vào Tool!");
        }

        private void RebuildSandBoardInScene()
        {
            SandBoardManager sandMgr = SandBoardManager.Instance;
            if (sandMgr == null) sandMgr = UnityEngine.Object.FindFirstObjectByType<SandBoardManager>();
            if (sandMgr == null)
            {
                EditorUtility.DisplayDialog("Thông báo", "Không tìm thấy SandBoardManager trong Scene!", "OK");
                return;
            }

            ApplySandBoardSettingsToScene(updateExisting: false);
            sandMgr.RebuildBoardInEditor();
            SceneView.RepaintAll();
            Debug.Log("<color=green>[Level Designer]</color> Đã tái tạo toàn bộ tranh cát trong Scene!");
        }

        private void ClearSandBoardInScene()
        {
            SandBoardManager sandMgr = SandBoardManager.Instance;
            if (sandMgr == null) sandMgr = UnityEngine.Object.FindFirstObjectByType<SandBoardManager>();
            if (sandMgr == null)
            {
                EditorUtility.DisplayDialog("Thông báo", "Không tìm thấy SandBoardManager trong Scene!", "OK");
                return;
            }

            sandMgr.ClearBoard();
            SceneView.RepaintAll();
            Debug.Log("<color=yellow>[Level Designer]</color> Đã xóa sạch hạt cát trên Scene!");
        }
        #endregion
        #endregion

        #region TIỆN ÍCH STYLE GIAO DIỆN HIỆN ĐẠI (UI HELPERS)
        private void BeginCard(string title, string icon = "", string subtitle = "")
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            if (!string.IsNullOrEmpty(icon))
            {
                GUIStyle iconStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
                GUILayout.Label(icon, iconStyle, GUILayout.Width(22));
            }
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, normal = { textColor = new Color(0.95f, 0.95f, 1f) } };
            GUILayout.Label(title.ToUpper(), titleStyle);
            if (!string.IsNullOrEmpty(subtitle))
            {
                GUIStyle subStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.7f, 0.75f, 0.85f) } };
                GUILayout.Label($"— {subtitle}", subStyle);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
            DrawSeparator();
            EditorGUILayout.Space(4);
        }

        private void EndCard()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);
        }

        private void DrawSeparator()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            rect.height = 1;
            EditorGUI.DrawRect(rect, new Color(0.35f, 0.40f, 0.50f, 0.4f));
        }

        private GUIStyle GetMetricBoxStyle()
        {
            return new GUIStyle(GUI.skin.box)
            {
                richText = true,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
        }

        private GUIStyle GetStatusBannerStyle()
        {
            return new GUIStyle(GUI.skin.box)
            {
                richText = true,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
        }
        #endregion
    }
}
