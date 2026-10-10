using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using WhoGetInBus.Data;
using WhoGetInBus.GamePlay;

namespace WhoGetInBus.EditorTools
{
    /// <summary>
    /// Công cụ thiết kế và sinh Level thông minh, chuẩn 1:1 Cocos Creator gốc.
    /// Tối giản nút bấm, tự động tính toán nhu cầu, sinh bãi xe chống kẹt (Winnable Solver),
    /// và sinh hạt cát hành khách chuẩn kích thước/vị trí vào Scene chỉ với 1 click.
    /// </summary>
    public class GameLevelDesignerWindow : EditorWindow
    {
        #region MENU & KHỞI TẠO
        [MenuItem("Tools/Level Designer Pro", false, 1)]
        [MenuItem("Window/Level Designer", false, 20)]
        public static void OpenWindow()
        {
            var win = GetWindow<GameLevelDesignerWindow>("Level Designer Pro");
            win.minSize = new Vector2(980, 720);
            win.Show();
        }
        #endregion

        #region DỮ LIỆU CẤU HÌNH & MÀN CHƠI
        // Level hiện tại
        private int currentLevelId = 1001;
        private List<int> allLevelIds = new List<int>();
        private int selectedLevelIdx = 0;

        // Tranh Cát (Collect)
        private int currentPictureId = 10006;
        private string currentCollectName = "甜趣香蕉";
        private int currentRoadId = 5;
        private int iniCapacity = 4;
        private int maxCapacity = 4;
        private int exchangeRatio = 10;

        // Danh sách tranh gốc từ collectNCXHCfg.json
        private List<CollectData> collectPresets = new List<CollectData>();
        private string[] collectDropdownLabels = new string[0];
        private int selectedCollectIdx = 0;

        // Bàn cát 40x40 trong Editor: [y, x] (y = 0 là đáy gần đường xe, y = 39 là đỉnh)
        private int[,] sandboardGrid = new int[40, 40];
        private Texture2D sandCanvasTex;

        // Bãi đỗ xe: [row, col] (string "{color}_{capacity}_{mechanic}")
        private int parkingRows = 6;
        private int parkingCols = 4;
        private string[,] carGrid = new string[6, 4];
        private List<int> queuePassengers = new List<int>();

        // Công cụ vẽ tranh cát
        private int selectedBrushColor = 1;
        private int brushSize = 1; // 1, 2, 3
        private bool isEyedropper = false;

        // Thống kê cân bằng
        private Dictionary<int, int> boardCounts = new Dictionary<int, int>();
        private Dictionary<int, int> queueCounts = new Dictionary<int, int>();
        private Dictionary<int, int> totalDemand = new Dictionary<int, int>();
        private Dictionary<int, int> carSeatCounts = new Dictionary<int, int>();

        // Kết quả giải / kiểm tra màn chơi
        private string solverStatusMsg = "Chưa kiểm tra.";
        private MessageType solverStatusType = MessageType.Info;

        // Vị trí cuộn giao diện
        private Vector2 scrollLeftPos;
        private Vector2 scrollRightPos;
        #endregion

        #region BẢNG MÀU CHUẨN 18 MÀU COCOS
        private static readonly Color[] PaletteColors = new Color[]
        {
            new Color(0.18f, 0.18f, 0.18f), // 0: Black
            new Color(0.16f, 0.45f, 0.72f), // 1: Blue
            new Color(0.47f, 0.26f, 0.07f), // 2: Brown
            new Color(0.57f, 0.17f, 0.13f), // 3: Burgundy
            new Color(0.09f, 0.65f, 0.54f), // 4: Cyan
            new Color(0.12f, 0.52f, 0.29f), // 5: DarkGreen
            new Color(0.18f, 0.80f, 0.44f), // 6: EmeraldGreen
            new Color(0.36f, 0.43f, 0.49f), // 7: GrayishBlue
            new Color(0.07f, 0.47f, 0.39f), // 8: GreenTeal
            new Color(0.69f, 0.48f, 0.77f), // 9: LightPurple
            new Color(0.90f, 0.49f, 0.13f), // 10: Orange
            new Color(0.95f, 0.58f, 0.54f), // 11: Pink
            new Color(0.97f, 0.77f, 0.44f), // 12: Powder
            new Color(0.53f, 0.31f, 0.63f), // 13: Purple
            new Color(0.91f, 0.30f, 0.24f), // 14: Red
            new Color(0.09f, 0.63f, 0.52f), // 15: Teal
            new Color(0.93f, 0.94f, 0.95f), // 16: White
            new Color(0.95f, 0.77f, 0.06f)  // 17: Yellow
        };

        private static readonly string[] ColorNames = new string[]
        {
            "Đen", "Xanh Dương", "Nâu", "Đỏ Đô", "Xanh Lơ", "Xanh Đậm",
            "Xanh Lục", "Xám Xanh", "Xanh Ngọc", "Tím Nhạt", "Cam", "Hồng",
            "Kem", "Tím", "Đỏ", "Xanh Biển", "Trắng", "Vàng"
        };
        #endregion

        #region LIFECYCLE
        private void OnEnable()
        {
            wantsMouseMove = true;
            RefreshLevelList();
            RefreshCollectPresets();
            EnsureCanvasTexture();
            LoadLevel(currentLevelId);
        }

        private void OnDisable()
        {
            if (sandCanvasTex != null)
            {
                DestroyImmediate(sandCanvasTex);
                sandCanvasTex = null;
            }
        }

        private void RefreshLevelList()
        {
            allLevelIds = LevelConfigLoader.GetAllLevelIds();
            if (allLevelIds == null || allLevelIds.Count == 0)
            {
                allLevelIds = new List<int> { 1001 };
            }
            selectedLevelIdx = Mathf.Max(0, allLevelIds.IndexOf(currentLevelId));
        }

        private void RefreshCollectPresets()
        {
            var dict = LevelConfigLoader.GetAllCollects();
            collectPresets = new List<CollectData>(dict.Values);
            collectPresets.Sort((a, b) => a.id.CompareTo(b.id));

            collectDropdownLabels = new string[collectPresets.Count];
            for (int i = 0; i < collectPresets.Count; i++)
            {
                var c = collectPresets[i];
                collectDropdownLabels[i] = $"#{c.picture} - {c.name} (L{c.unlockLevel})";
            }
        }
        #endregion

        #region GIAO DIỆN CHÍNH (OnGUI)
        private void OnGUI()
        {
            DrawHeaderToolbar();

            EditorGUILayout.Space(4);

            // Bố cục 2 cột trực quan: Trái (Tranh cát 40x40) - Phải (Bãi đỗ xe & Cân bằng)
            EditorGUILayout.BeginHorizontal();
            {
                // CỘT TRÁI: TRANH CÁT 40x40
                EditorGUILayout.BeginVertical("box", GUILayout.Width(440));
                scrollLeftPos = EditorGUILayout.BeginScrollView(scrollLeftPos);
                DrawSandboardColumn();
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(6);

                // CỘT PHẢI: BÃI ĐỖ XE & CÂN BẰNG
                EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true));
                scrollRightPos = EditorGUILayout.BeginScrollView(scrollRightPos);
                DrawParkingLotColumn();
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Thanh điều khiển trên cùng: Nạp, Gen xe tự động, Lưu, Sinh Scene
        /// </summary>
        private void DrawHeaderToolbar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.toolbar);
            EditorGUILayout.BeginHorizontal();

            // 1. Chọn Level
            EditorGUILayout.LabelField("Level:", GUILayout.Width(40));
            int newLvl = EditorGUILayout.IntField(currentLevelId, GUILayout.Width(55));
            if (newLvl != currentLevelId)
            {
                currentLevelId = newLvl;
                selectedLevelIdx = Mathf.Max(0, allLevelIds.IndexOf(currentLevelId));
            }

            if (allLevelIds != null && allLevelIds.Count > 0)
            {
                string[] lvlOptions = new string[allLevelIds.Count];
                for (int i = 0; i < allLevelIds.Count; i++) lvlOptions[i] = $"Lvl {allLevelIds[i]}";
                int newIdx = EditorGUILayout.Popup(selectedLevelIdx, lvlOptions, GUILayout.Width(80));
                if (newIdx != selectedLevelIdx && newIdx >= 0 && newIdx < allLevelIds.Count)
                {
                    selectedLevelIdx = newIdx;
                    currentLevelId = allLevelIds[selectedLevelIdx];
                    LoadLevel(currentLevelId);
                }
            }

            // Nút Nạp
            if (GUILayout.Button("📥 Nạp Level", EditorStyles.toolbarButton, GUILayout.Width(85)))
            {
                LoadLevel(currentLevelId);
            }

            GUILayout.Space(8);

            // Thông tin tranh & tuyến đường
            EditorGUILayout.LabelField($"Tranh #{currentPictureId} '{currentCollectName}' | Tuyến #{currentRoadId}", EditorStyles.miniBoldLabel, GUILayout.Width(220));

            GUILayout.FlexibleSpace();

            // 2. Nút Gen Xe Thông Minh (Core Feature)
            GUI.backgroundColor = new Color(0.2f, 0.75f, 0.95f);
            if (GUILayout.Button("⚡ Tự Động Gen Xe Thông Minh", EditorStyles.toolbarButton, GUILayout.Width(190)))
            {
                AutoGenerateWinnableFleet();
            }

            // 3. Nút Lưu
            GUI.backgroundColor = new Color(0.4f, 0.85f, 0.4f);
            if (GUILayout.Button("💾 Lưu JSON", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                SaveLevel();
            }

            // 4. Nút Sinh Vào Scene (Core Feature)
            GUI.backgroundColor = new Color(0.95f, 0.65f, 0.2f);
            if (GUILayout.Button("🎮 Sinh Vào Scene", EditorStyles.toolbarButton, GUILayout.Width(125)))
            {
                SpawnIntoActiveScene();
            }

            // 5. Nút Xóa Scene
            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
            if (GUILayout.Button("🧹 Xóa Scene", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                ClearActiveScene();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
        #endregion

        #region CỘT TRÁI: TRANH CÁT 40x40 & VẼ CỌ
        private void DrawSandboardColumn()
        {
            EditorGUILayout.LabelField("🎨 TRANH CÁT 40x40 (SANDBOARD)", EditorStyles.boldLabel);

            // Chọn thư viện tranh gốc
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Thư viện tranh:", GUILayout.Width(95));
            if (collectDropdownLabels.Length > 0)
            {
                int newColIdx = EditorGUILayout.Popup(selectedCollectIdx, collectDropdownLabels);
                if (newColIdx != selectedCollectIdx && newColIdx >= 0 && newColIdx < collectPresets.Count)
                {
                    selectedCollectIdx = newColIdx;
                    var c = collectPresets[selectedCollectIdx];
                    LoadPixelMapDirect(c.picture, c.name);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Công cụ biến đổi tranh
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔄 Xoay 90°", GUILayout.Width(85))) RotateCanvas90();
            if (GUILayout.Button("↔ Lật Ngang", GUILayout.Width(85))) FlipCanvasHorizontal();
            if (GUILayout.Button("↕ Lật Dọc", GUILayout.Width(85))) FlipCanvasVertical();
            if (GUILayout.Button("🧹 Xóa Trắng", GUILayout.Width(80))) ClearCanvas();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            // Canvas tương tác 40x40
            Rect canvasRect = GUILayoutUtility.GetRect(400, 400, GUILayout.ExpandWidth(false));
            HandleCanvasInput(canvasRect);

            if (sandCanvasTex != null)
            {
                GUI.DrawTexture(canvasRect, sandCanvasTex);
            }

            EditorGUILayout.Space(6);

            // Bảng công cụ vẽ
            EditorGUILayout.BeginHorizontal();
            isEyedropper = GUILayout.Toggle(isEyedropper, "💉 Hút Màu", "Button", GUILayout.Width(85));
            EditorGUILayout.LabelField("Cỡ cọ:", GUILayout.Width(45));
            if (GUILayout.Toggle(brushSize == 1, "1x1", "Button", GUILayout.Width(40))) brushSize = 1;
            if (GUILayout.Toggle(brushSize == 2, "2x2", "Button", GUILayout.Width(40))) brushSize = 2;
            if (GUILayout.Toggle(brushSize == 3, "3x3", "Button", GUILayout.Width(40))) brushSize = 3;

            GUILayout.FlexibleSpace();
            // Màu đang chọn
            Color curCol = (selectedBrushColor >= 0 && selectedBrushColor < PaletteColors.Length) ? PaletteColors[selectedBrushColor] : Color.black;
            GUI.backgroundColor = curCol;
            GUILayout.Box($"Màu #{selectedBrushColor}", GUILayout.Width(90), GUILayout.Height(20));
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Bảng 18 màu chọn nhanh
            EditorGUILayout.LabelField("Bảng 18 Màu Chuẩn:", EditorStyles.miniBoldLabel);
            int colsPerRow = 9;
            for (int r = 0; r < 2; r++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int c = 0; c < colsPerRow; c++)
                {
                    int colorIdx = r * colsPerRow + c;
                    GUI.backgroundColor = PaletteColors[colorIdx];
                    string label = (selectedBrushColor == colorIdx) ? $"[{colorIdx}]" : $"{colorIdx}";
                    if (GUILayout.Button(label, GUILayout.Width(42), GUILayout.Height(24)))
                    {
                        selectedBrushColor = colorIdx;
                        isEyedropper = false;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(8);

            // Thống kê số lượng hạt tranh
            DrawSandDemandTable();
        }

        private void HandleCanvasInput(Rect canvasRect)
        {
            Event e = Event.current;
            if (!canvasRect.Contains(e.mousePosition)) return;

            if (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
            {
                float relX = (e.mousePosition.x - canvasRect.x) / canvasRect.width;
                float relY = (e.mousePosition.y - canvasRect.y) / canvasRect.height;

                int x = Mathf.Clamp(Mathf.FloorToInt(relX * 40f), 0, 39);
                // In canvas UI: top is 0, bottom is 39.
                // In sandboardGrid: y = 0 is bottom, y = 39 is top.
                int y = Mathf.Clamp(39 - Mathf.FloorToInt(relY * 40f), 0, 39);

                if (isEyedropper)
                {
                    selectedBrushColor = sandboardGrid[y, x];
                    isEyedropper = false;
                    Repaint();
                }
                else
                {
                    int half = brushSize / 1; // offset
                    for (int dy = 0; dy < brushSize; dy++)
                    {
                        for (int dx = 0; dx < brushSize; dx++)
                        {
                            int px = Mathf.Clamp(x + dx, 0, 39);
                            int py = Mathf.Clamp(y + dy, 0, 39);
                            sandboardGrid[py, px] = selectedBrushColor;
                        }
                    }
                    UpdateCanvasTexture();
                    RecalculateDemand();
                    Repaint();
                }

                e.Use();
            }
        }

        private void DrawSandDemandTable()
        {
            EditorGUILayout.LabelField("📊 CÂN BẰNG NHU CẦU TRANH vs GHẾ XE:", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical("helpbox");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Màu", EditorStyles.boldLabel, GUILayout.Width(90));
            EditorGUILayout.LabelField("Hạt Cát", EditorStyles.boldLabel, GUILayout.Width(60));
            EditorGUILayout.LabelField("Queue", EditorStyles.boldLabel, GUILayout.Width(60));
            EditorGUILayout.LabelField("Nhu Cầu", EditorStyles.boldLabel, GUILayout.Width(60));
            EditorGUILayout.LabelField("Ghế Xe", EditorStyles.boldLabel, GUILayout.Width(60));
            EditorGUILayout.LabelField("Trạng Thái", EditorStyles.boldLabel, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            bool allBalanced = true;
            foreach (var kvp in totalDemand)
            {
                int c = kvp.Key;
                int dem = kvp.Value;
                int boardC = boardCounts.ContainsKey(c) ? boardCounts[c] : 0;
                int qC = queueCounts.ContainsKey(c) ? queueCounts[c] : 0;
                int seatC = carSeatCounts.ContainsKey(c) ? carSeatCounts[c] : 0;

                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = PaletteColors[c];
                GUILayout.Box("", GUILayout.Width(14), GUILayout.Height(14));
                GUI.backgroundColor = Color.white;

                string colName = (c >= 0 && c < ColorNames.Length) ? ColorNames[c] : $"#{c}";
                EditorGUILayout.LabelField($"#{c} {colName}", GUILayout.Width(72));
                EditorGUILayout.LabelField($"{boardC}", GUILayout.Width(60));
                EditorGUILayout.LabelField($"{qC}", GUILayout.Width(60));
                EditorGUILayout.LabelField($"{dem}", GUILayout.Width(60));
                EditorGUILayout.LabelField($"{seatC}", GUILayout.Width(60));

                if (seatC == dem)
                {
                    GUI.contentColor = new Color(0.2f, 0.85f, 0.3f);
                    EditorGUILayout.LabelField("✔ Cân bằng", EditorStyles.boldLabel, GUILayout.Width(80));
                }
                else
                {
                    allBalanced = false;
                    GUI.contentColor = (seatC < dem) ? new Color(0.95f, 0.3f, 0.3f) : new Color(0.95f, 0.7f, 0.2f);
                    int diff = seatC - dem;
                    EditorGUILayout.LabelField(diff > 0 ? $"+{diff} thừa" : $"{diff} thiếu", EditorStyles.boldLabel, GUILayout.Width(80));
                }
                GUI.contentColor = Color.white;

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();

            if (!allBalanced)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox("Ghế xe và nhu cầu chưa khớp. Nhấn 'Tự Động Gen Xe' để khớp 100%!", MessageType.Warning);
                if (GUILayout.Button("⚡ Khớp Ngay", GUILayout.Width(100), GUILayout.Height(36)))
                {
                    AutoGenerateWinnableFleet();
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        #endregion

        #region CỘT PHẢI: BÃI ĐỖ XE (PARKING LOT) & THUẬT TOÁN
        private void DrawParkingLotColumn()
        {
            EditorGUILayout.LabelField("🚗 BÃI ĐỖ XE (PARKING LOT MATRIX)", EditorStyles.boldLabel);

            // Kích thước lưới xe
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Kích thước bãi:", GUILayout.Width(90));
            EditorGUILayout.LabelField("Hàng:", GUILayout.Width(40));
            int newRows = EditorGUILayout.IntSlider(parkingRows, 1, 12, GUILayout.Width(130));
            EditorGUILayout.LabelField("Cột:", GUILayout.Width(35));
            int newCols = EditorGUILayout.IntSlider(parkingCols, 1, 7, GUILayout.Width(130));

            if (newRows != parkingRows || newCols != parkingCols)
            {
                ResizeParkingGrid(newRows, newCols);
            }
            EditorGUILayout.EndHorizontal();

            // Trạng thái bộ giải (Solver Status)
            EditorGUILayout.Space(2);
            EditorGUILayout.HelpBox(solverStatusMsg, solverStatusType);

            EditorGUILayout.Space(4);

            // BẢNG MA TRẬN XE TRỰC QUAN
            // Hàng 0 là Hàng Tiền Tuyến (sát vạch xuất phát / đón khách)
            EditorGUILayout.LabelField("⬇ Vạch xuất phát / Đón khách (Hàng 0 đi trước) ⬇", EditorStyles.centeredGreyMiniLabel);

            for (int r = 0; r < parkingRows; r++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Hàng {r}:", GUILayout.Width(50));

                for (int c = 0; c < parkingCols; c++)
                {
                    DrawCarSlotCell(r, c);
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.Space(8);

            // Cấu hình Queue & Slots chờ
            EditorGUILayout.LabelField("⚙ THÔNG SỐ KHÁC:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Ô chờ ban đầu (ini):", GUILayout.Width(120));
            iniCapacity = EditorGUILayout.IntField(iniCapacity, GUILayout.Width(40));
            EditorGUILayout.LabelField("Ô chờ tối đa (max):", GUILayout.Width(120));
            maxCapacity = EditorGUILayout.IntField(maxCapacity, GUILayout.Width(40));
            EditorGUILayout.LabelField("Tuyến đường (Road):", GUILayout.Width(120));
            currentRoadId = EditorGUILayout.IntField(currentRoadId, GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Hàng đợi rơi thêm (Queue): {queuePassengers.Count} hạt", GUILayout.Width(220));
            if (GUILayout.Button("🧹 Xóa Queue", GUILayout.Width(90)))
            {
                queuePassengers.Clear();
                RecalculateDemand();
            }
            if (GUILayout.Button("⚡ Cân Bằng Queue Tự Động", GUILayout.Width(160)))
            {
                AutoBalanceQueueFromFleet();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCarSlotCell(int r, int c)
        {
            string cell = carGrid[r, c];
            bool isEmpty = string.IsNullOrEmpty(cell) || cell.Equals("None", StringComparison.OrdinalIgnoreCase) || cell.StartsWith("-1");

            int colorIdx = -1;
            int cap = 100;
            int mech = 0;

            if (!isEmpty)
            {
                string[] parts = cell.Split('_');
                if (parts.Length >= 2)
                {
                    int.TryParse(parts[0], out colorIdx);
                    int.TryParse(parts[1], out cap);
                    if (parts.Length >= 3) int.TryParse(parts[2], out mech);
                }
            }

            Color cellBg = (colorIdx >= 0 && colorIdx < PaletteColors.Length) ? PaletteColors[colorIdx] : new Color(0.25f, 0.25f, 0.25f);
            GUI.backgroundColor = cellBg;

            string btnText = isEmpty ? "Trống" : $"{cap}c";
            if (mech == 1) btnText += "\n[3-in-1]";
            else if (mech == 2) btnText += "\n[Khóa]";
            else if (mech == 3) btnText += "\n[Băng]";
            else if (mech == 4) btnText += "\n[VIP]";

            if (GUILayout.Button(btnText, GUILayout.Width(72), GUILayout.Height(38)))
            {
                ShowCarCellMenu(r, c, colorIdx, cap, mech);
            }

            GUI.backgroundColor = Color.white;
        }

        private void ShowCarCellMenu(int r, int c, int curCol, int curCap, int curMech)
        {
            GenericMenu menu = new GenericMenu();

            menu.AddItem(new GUIContent("Trống (None)"), curCol < 0, () =>
            {
                carGrid[r, c] = "None";
                RecalculateDemand();
            });

            menu.AddSeparator("");

            // Đổi sức chứa
            int[] caps = new int[] { 50, 100, 150, 200 };
            foreach (var cp in caps)
            {
                int capVal = cp;
                menu.AddItem(new GUIContent($"Sức chứa/{capVal} chỗ"), curCap == capVal, () =>
                {
                    int col = curCol >= 0 ? curCol : selectedBrushColor;
                    carGrid[r, c] = $"{col}_{capVal}_{curMech}";
                    RecalculateDemand();
                });
            }

            menu.AddSeparator("");

            // Đổi màu
            for (int i = 0; i < 18; i++)
            {
                int colIdx = i;
                string cName = (colIdx < ColorNames.Length) ? ColorNames[colIdx] : $"#{colIdx}";
                menu.AddItem(new GUIContent($"Màu/#{colIdx} - {cName}"), curCol == colIdx, () =>
                {
                    int capV = curCap > 0 ? curCap : 100;
                    carGrid[r, c] = $"{colIdx}_{capV}_{curMech}";
                    RecalculateDemand();
                });
            }

            menu.AddSeparator("");

            // Đổi cơ chế
            menu.AddItem(new GUIContent("Cơ chế/Thường (None)"), curMech == 0, () =>
            {
                int col = curCol >= 0 ? curCol : selectedBrushColor;
                carGrid[r, c] = $"{col}_{curCap}_0";
                RecalculateDemand();
            });
            menu.AddItem(new GUIContent("Cơ chế/3-in-One Gộp 3"), curMech == 1, () =>
            {
                int col = curCol >= 0 ? curCol : selectedBrushColor;
                carGrid[r, c] = $"{col}_{curCap}_1";
                RecalculateDemand();
            });
            menu.AddItem(new GUIContent("Cơ chế/Khóa & Chìa khóa"), curMech == 2, () =>
            {
                int col = curCol >= 0 ? curCol : selectedBrushColor;
                carGrid[r, c] = $"{col}_{curCap}_2";
                RecalculateDemand();
            });
            menu.AddItem(new GUIContent("Cơ chế/Đóng băng (Ice)"), curMech == 3, () =>
            {
                int col = curCol >= 0 ? curCol : selectedBrushColor;
                carGrid[r, c] = $"{col}_{curCap}_3";
                RecalculateDemand();
            });

            menu.ShowAsContext();
        }

        private void ResizeParkingGrid(int newRows, int newCols)
        {
            string[,] next = new string[newRows, newCols];
            for (int r = 0; r < newRows; r++)
            {
                for (int c = 0; c < newCols; c++)
                {
                    if (r < parkingRows && c < parkingCols) next[r, c] = carGrid[r, c];
                    else next[r, c] = "None";
                }
            }
            parkingRows = newRows;
            parkingCols = newCols;
            carGrid = next;
            RecalculateDemand();
        }
        #endregion

        #region THUẬT TOÁN GEN XE THÔNG MINH & BỘ GIẢI (SMART SOLVER)
        /// <summary>
        /// Thuật toán gen xe thông minh cốt lõi:
        /// 1. Tính toán chính xác số hạt cát theo từng màu (trừ vùng khoét đường).
        /// 2. Phân rã số lượng thành các xe chuẩn: 200, 150, 100, 50 chỗ.
        /// 3. Phân tích thứ tự cào cát từ dưới đáy lên (Bottom-up drain order).
        /// 4. Đặt các màu ở đáy cát vào Hàng 0 (vạch xuất phát) để xe đón khách ngay lập tức, không gây kẹt ô chờ!
        /// 5. Phân bổ các màu tiếp theo vào các hàng sau.
        /// 6. Tự động kiểm tra tính giải được (Winnability Solver) và đảo vị trí nếu phát hiện nguy cơ kẹt.
        /// </summary>
        public void AutoGenerateWinnableFleet()
        {
            RecalculateDemand();

            // 1. Phân rã nhu cầu từng màu thành danh sách các xe (Color, Capacity)
            List<KeyValuePair<int, int>> fleetList = new List<KeyValuePair<int, int>>();
            int[] standardCaps = new int[] { 200, 150, 100, 50 };

            foreach (var kvp in totalDemand)
            {
                int col = kvp.Key;
                int remaining = kvp.Value;
                if (remaining <= 0) continue;

                while (remaining > 0)
                {
                    int chosenCap = 50;
                    foreach (int cap in standardCaps)
                    {
                        if (remaining >= cap)
                        {
                            chosenCap = cap;
                            break;
                        }
                    }
                    fleetList.Add(new KeyValuePair<int, int>(col, chosenCap));
                    remaining -= chosenCap;
                }
            }

            if (fleetList.Count == 0)
            {
                solverStatusMsg = "Tranh cát chưa có hạt màu nào!";
                solverStatusType = MessageType.Warning;
                return;
            }

            // 2. Tính kích thước ma trận xe phù hợp
            int totalCars = fleetList.Count;
            int cols = 4;
            int rows = Mathf.Clamp(Mathf.CeilToInt((float)totalCars / cols), 3, 10);
            ResizeParkingGrid(rows, cols);

            // 3. Phân tích thứ tự màu lộ diện từ đáy bàn cát (y = 0..39)
            List<int> drainPriority = GetBottomToTopColorOrder();

            // Sắp xếp các xe: ưu tiên các xe mang màu lộ diện sớm lên đầu
            fleetList.Sort((a, b) =>
            {
                int prioA = drainPriority.IndexOf(a.Key);
                if (prioA < 0) prioA = 999;
                int prioB = drainPriority.IndexOf(b.Key);
                if (prioB < 0) prioB = 999;

                if (prioA != prioB) return prioA.CompareTo(prioB);
                return b.Value.CompareTo(a.Value); // Xe to hơn ưu tiên trước
            });

            // 4. Xếp xe vào lưới: Hàng 0 bắt buộc phải có đủ các màu lộ diện ở đáy bàn cát
            for (int r = 0; r < parkingRows; r++)
                for (int c = 0; c < parkingCols; c++)
                    carGrid[r, c] = "None";

            // Xếp hàng 0 trước
            HashSet<int> row0Colors = new HashSet<int>();
            int fleetIdx = 0;
            for (int c = 0; c < parkingCols; c++)
            {
                if (fleetIdx >= fleetList.Count) break;

                // Tìm xe có màu chưa có ở hàng 0
                int pick = -1;
                for (int i = fleetIdx; i < fleetList.Count; i++)
                {
                    if (!row0Colors.Contains(fleetList[i].Key))
                    {
                        pick = i;
                        break;
                    }
                }
                if (pick < 0) pick = fleetIdx;

                var chosenCar = fleetList[pick];
                fleetList.RemoveAt(pick);

                carGrid[0, c] = $"{chosenCar.Key}_{chosenCar.Value}_0";
                row0Colors.Add(chosenCar.Key);
            }

            // Xếp các hàng tiếp theo (Hàng 1 đến N)
            for (int r = 1; r < parkingRows; r++)
            {
                for (int c = 0; c < parkingCols; c++)
                {
                    if (fleetList.Count == 0) break;
                    var car = fleetList[0];
                    fleetList.RemoveAt(0);
                    carGrid[r, c] = $"{car.Key}_{car.Value}_0";
                }
            }

            // 5. Chạy Solver kiểm tra và tự động đảo xe nếu bị kẹt
            RunSolverAndAutoFix();
            RecalculateDemand();
            Repaint();
        }

        private List<int> GetBottomToTopColorOrder()
        {
            List<int> order = new List<int>();
            var roadCfg = LevelConfigLoader.GetRoadConfig(currentRoadId);

            // Duyệt từ y = 0 (đáy) lên y = 39 (đỉnh)
            for (int y = 0; y < 40; y++)
            {
                for (int x = 0; x < 40; x++)
                {
                    if (roadCfg != null && roadCfg.IsCutout(x, 39 - y)) continue;

                    int col = sandboardGrid[y, x];
                    if (col >= 0 && !order.Contains(col))
                    {
                        order.Add(col);
                    }
                }
            }
            return order;
        }

        /// <summary>
        /// Mô phỏng gameplay gốc để kiểm tra tính giải được của màn chơi (Solvability Check)
        /// </summary>
        private void RunSolverAndAutoFix()
        {
            // Mô phỏng tối đa 5 lần đảo để tìm cấu hình thắng 100%
            for (int attempt = 0; attempt < 5; attempt++)
            {
                bool winnable = SimulateWinnability(out int moves, out string failReason);
                if (winnable)
                {
                    solverStatusMsg = $"✅ 100% Thắng Được! (Hoàn thành trong {moves} lượt đi, không bị kẹt ô chờ).";
                    solverStatusType = MessageType.Info;
                    return;
                }

                // Nếu kẹt, thực hiện hoán đổi hàng 1 hoặc hàng 0
                if (parkingRows > 1)
                {
                    int swapC1 = UnityEngine.Random.Range(0, parkingCols);
                    int swapC2 = UnityEngine.Random.Range(0, parkingCols);
                    string tmp = carGrid[0, swapC1];
                    carGrid[0, swapC1] = carGrid[1, swapC2];
                    carGrid[1, swapC2] = tmp;
                }
            }

            solverStatusMsg = "⚠️ Có nguy cơ kẹt nếu đi sai nước. Khuyến nghị kiểm tra vị trí xe hàng 0.";
            solverStatusType = MessageType.Warning;
        }

        private bool SimulateWinnability(out int moves, out string failReason)
        {
            moves = 0;
            failReason = "";

            // Sao chép bàn cát ảo
            int[,] simSand = new int[40, 40];
            Array.Copy(sandboardGrid, simSand, sandboardGrid.Length);
            var roadCfg = LevelConfigLoader.GetRoadConfig(currentRoadId);

            // Tạo cột xe ảo
            List<List<KeyValuePair<int, int>>> simColumns = new List<List<KeyValuePair<int, int>>>();
            for (int c = 0; c < parkingCols; c++)
            {
                var col = new List<KeyValuePair<int, int>>();
                for (int r = 0; r < parkingRows; r++)
                {
                    string cell = carGrid[r, c];
                    if (string.IsNullOrEmpty(cell) || cell.Equals("None", StringComparison.OrdinalIgnoreCase)) continue;
                    string[] parts = cell.Split('_');
                    if (parts.Length >= 2 && int.TryParse(parts[0], out int color) && int.TryParse(parts[1], out int cap))
                    {
                        col.Add(new KeyValuePair<int, int>(color, cap));
                    }
                }
                simColumns.Add(col);
            }

            // Vòng ray đón khách: danh sách xe đang trong loop (tối đa maxCapacity slots)
            List<KeyValuePair<int, int>> trackBays = new List<KeyValuePair<int, int>>();
            int maxTrackSlots = Mathf.Max(4, maxCapacity);

            // Vòng lặp giải từng lượt
            for (int step = 0; step < 120; step++)
            {
                // 1. Cho các xe trên ray đón cát từ đáy
                bool absorbedAny = false;
                for (int b = trackBays.Count - 1; b >= 0; b--)
                {
                    var car = trackBays[b];
                    int carColor = car.Key;
                    int carCap = car.Value;

                    // Hút các hạt ở đáy bàn cát có màu tương ứng
                    int drained = 0;
                    for (int y = 0; y < 40; y++)
                    {
                        for (int x = 0; x < 40; x++)
                        {
                            if (roadCfg != null && roadCfg.IsCutout(x, 39 - y)) continue;
                            if (simSand[y, x] == carColor)
                            {
                                simSand[y, x] = -1;
                                drained++;
                                if (drained >= carCap) break;
                            }
                        }
                        if (drained >= carCap) break;
                    }

                    if (drained > 0)
                    {
                        absorbedAny = true;
                        carCap -= drained;
                        if (carCap <= 0)
                        {
                            // Xe đầy khách -> Rời khỏi bến đỗ!
                            trackBays.RemoveAt(b);
                        }
                        else
                        {
                            trackBays[b] = new KeyValuePair<int, int>(carColor, carCap);
                        }
                    }
                }

                // 2. Kiểm tra nếu tất cả các xe đã xong
                bool allEmpty = true;
                foreach (var col in simColumns) if (col.Count > 0) { allEmpty = false; break; }
                if (allEmpty && trackBays.Count == 0)
                {
                    moves = step;
                    return true;
                }

                // 3. Nếu còn chỗ trên ray -> Đưa xe có màu khớp đáy cát vào ray
                if (trackBays.Count < maxTrackSlots)
                {
                    int bestCol = -1;
                    for (int c = 0; c < simColumns.Count; c++)
                    {
                        if (simColumns[c].Count > 0)
                        {
                            var frontCar = simColumns[c][0];
                            // Kiểm tra xem xe này có thể ăn cát ở đáy ngay không
                            if (CanDrainAnySand(simSand, roadCfg, frontCar.Key))
                            {
                                bestCol = c;
                                break;
                            }
                        }
                    }

                    if (bestCol < 0)
                    {
                        // Nếu không có xe nào ăn được ngay, lấy xe đầu tiên còn trống
                        for (int c = 0; c < simColumns.Count; c++)
                        {
                            if (simColumns[c].Count > 0) { bestCol = c; break; }
                        }
                    }

                    if (bestCol >= 0)
                    {
                        var carToTrack = simColumns[bestCol][0];
                        simColumns[bestCol].RemoveAt(0);
                        trackBays.Add(carToTrack);
                        moves++;
                        continue;
                    }
                }

                // 4. Nếu ô chờ đầy và không xe nào ăn được hạt cát nào -> KẸT (Deadlock)!
                if (!absorbedAny && trackBays.Count >= maxTrackSlots)
                {
                    failReason = $"Kẹt toàn bộ {maxTrackSlots} ô chờ tại lượt {step}.";
                    return false;
                }
            }

            failReason = "Vượt quá 120 lượt mô phỏng.";
            return false;
        }

        private bool CanDrainAnySand(int[,] sand, RoadConfigData roadCfg, int targetColor)
        {
            for (int y = 0; y < 15; y++) // Xem 15 hàng đáy
            {
                for (int x = 0; x < 40; x++)
                {
                    if (roadCfg != null && roadCfg.IsCutout(x, 39 - y)) continue;
                    if (sand[y, x] == targetColor) return true;
                }
            }
            return false;
        }

        private void AutoBalanceQueueFromFleet()
        {
            queuePassengers.Clear();
            foreach (var kvp in carSeatCounts)
            {
                int c = kvp.Key;
                int seatC = kvp.Value;
                int boardC = boardCounts.ContainsKey(c) ? boardCounts[c] : 0;
                int needMore = seatC - boardC;
                if (needMore > 0)
                {
                    int qCount = Mathf.CeilToInt((float)needMore / exchangeRatio);
                    for (int i = 0; i < qCount; i++) queuePassengers.Add(c);
                }
            }
            RecalculateDemand();
            Repaint();
        }
        #endregion

        #region SINH PASSENGER & BÃI XE CHUẨN GỐC VÀO SCENE
        /// <summary>
        /// Chức năng trọng tâm: Sinh toàn bộ Passenger & Bãi Đỗ Xe trực tiếp vào Scene 3D
        /// Chuẩn 1:1 Cocos Creator (kích thước, khoảng cách zigzag, cutout mask, vị trí).
        /// </summary>
        public void SpawnIntoActiveScene()
        {
            // 1. Tìm hoặc tạo SandBoard_Manager
            SandBoardManager boardMgr = FindFirstObjectByType<SandBoardManager>();
            if (boardMgr == null)
            {
                GameObject bObj = new GameObject("SandBoard_Manager");
                boardMgr = bObj.AddComponent<SandBoardManager>();
            }

            // Gán dữ liệu chuẩn gốc Cocos 1:1
            boardMgr.boardPosZ = 6.0f;
            boardMgr.beadSpacing = 0.2586f;
            boardMgr.beadRadius = 0.1293f;
            boardMgr.beadScale = 0.55f; // Scale chuẩn để hạt cát nằm khít không đè lên nhau
            boardMgr.enableZigzag = true;
            boardMgr.zigzagOffset = 0.0828f;
            boardMgr.columns = 40;
            boardMgr.rows = 40;
            boardMgr.currentRoadId = currentRoadId;
            boardMgr.levelId = currentLevelId;

            // Nạp Prefab & Asset
            AssignPrefabsToManagers(boardMgr, null);

            // Sinh bảng cát vào Scene
            boardMgr.BuildBoard(sandboardGrid, boardMgr.passengerColors, boardMgr.beadMaterial);

            // 2. Tìm hoặc tạo ParkingLot_Manager
            ParkingLotManager parkMgr = FindFirstObjectByType<ParkingLotManager>();
            if (parkMgr == null)
            {
                GameObject pObj = new GameObject("ParkingLot_Manager");
                pObj.transform.position = new Vector3(0f, 0f, -4.388f); // Tọa độ entityRoot chuẩn Cocos!
                parkMgr = pObj.AddComponent<ParkingLotManager>();
            }

            AssignPrefabsToManagers(null, parkMgr);

            // Sinh bãi xe vào Scene
            parkMgr.SpawnParkingLot(carGrid, parkingRows, parkingCols, parkMgr.carPrefab, parkMgr.busColors);

            // Mark Scene Dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log($"<color=green>[Level Designer Pro]</color> Đã sinh thành công {boardMgr.TotalRemainingBeads} Passenger và {parkingCols} cột Xe vào Scene chuẩn Cocos 1:1!");
            EditorUtility.DisplayDialog("Thành Công", $"Đã sinh toàn bộ {boardMgr.TotalRemainingBeads} hạt cát Passenger và bãi xe {parkingRows}x{parkingCols} vào Scene!", "OK");
        }

        public void ClearActiveScene()
        {
            SandBoardManager boardMgr = FindFirstObjectByType<SandBoardManager>();
            if (boardMgr != null) boardMgr.ClearBoard();

            ParkingLotManager parkMgr = FindFirstObjectByType<ParkingLotManager>();
            if (parkMgr != null) parkMgr.ClearParkingLot();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("<color=yellow>[Level Designer Pro]</color> Đã dọn sạch Passenger và Xe trên Scene!");
        }

        private void AssignPrefabsToManagers(SandBoardManager boardMgr, ParkingLotManager parkMgr)
        {
            var pData = AssetDatabase.LoadAssetAtPath<GamePrefabData>("Assets/Data/GamePrefabData.asset");
            var pColors = AssetDatabase.LoadAssetAtPath<PassengerColorData>("Assets/Data/PassengerColorData.asset");
            var bColors = AssetDatabase.LoadAssetAtPath<BusColorData>("Assets/Data/BusColorData.asset");
            var passPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Passenger.prefab");
            var carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Car.prefab");
            var extraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Extra Car.prefab");
            var beadMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Mat_PassengerBead.mat");

            if (boardMgr != null)
            {
                boardMgr.beadPrefab = passPrefab;
                boardMgr.beadMaterial = beadMat;
                boardMgr.passengerColors = pColors;
            }

            if (parkMgr != null)
            {
                parkMgr.carPrefab = carPrefab;
                parkMgr.extraCarPrefab = extraPrefab;
                parkMgr.busColors = bColors;
            }
        }
        #endregion

        #region XỬ LÝ CANVAS TRANH CÁT (TEXTURE & TRANSFORMS)
        private void EnsureCanvasTexture()
        {
            if (sandCanvasTex == null)
            {
                sandCanvasTex = new Texture2D(40, 40, TextureFormat.RGBA32, false);
                sandCanvasTex.filterMode = FilterMode.Point;
                sandCanvasTex.wrapMode = TextureWrapMode.Clamp;
            }
            UpdateCanvasTexture();
        }

        private void UpdateCanvasTexture()
        {
            if (sandCanvasTex == null) return;
            var roadCfg = LevelConfigLoader.GetRoadConfig(currentRoadId);

            for (int y = 0; y < 40; y++)
            {
                for (int x = 0; x < 40; x++)
                {
                    // Texture pixel: y = 0 ở đáy, y = 39 ở đỉnh
                    bool isCutout = (roadCfg != null && roadCfg.IsCutout(x, 39 - y));

                    if (isCutout)
                    {
                        sandCanvasTex.SetPixel(x, y, new Color(0.12f, 0.12f, 0.15f, 0.95f)); // Vùng khoét đường
                    }
                    else
                    {
                        int c = sandboardGrid[y, x];
                        Color col = (c >= 0 && c < PaletteColors.Length) ? PaletteColors[c] : new Color(0.2f, 0.2f, 0.2f, 1f);
                        sandCanvasTex.SetPixel(x, y, col);
                    }
                }
            }
            sandCanvasTex.Apply();
        }

        private void RotateCanvas90()
        {
            int[,] temp = new int[40, 40];
            for (int y = 0; y < 40; y++)
                for (int x = 0; x < 40; x++)
                    temp[x, 39 - y] = sandboardGrid[y, x];
            sandboardGrid = temp;
            UpdateCanvasTexture();
            RecalculateDemand();
        }

        private void FlipCanvasHorizontal()
        {
            int[,] temp = new int[40, 40];
            for (int y = 0; y < 40; y++)
                for (int x = 0; x < 40; x++)
                    temp[y, 39 - x] = sandboardGrid[y, x];
            sandboardGrid = temp;
            UpdateCanvasTexture();
            RecalculateDemand();
        }

        private void FlipCanvasVertical()
        {
            int[,] temp = new int[40, 40];
            for (int y = 0; y < 40; y++)
                for (int x = 0; x < 40; x++)
                    temp[39 - y, x] = sandboardGrid[y, x];
            sandboardGrid = temp;
            UpdateCanvasTexture();
            RecalculateDemand();
        }

        private void ClearCanvas()
        {
            for (int y = 0; y < 40; y++)
                for (int x = 0; x < 40; x++)
                    sandboardGrid[y, x] = -1;
            UpdateCanvasTexture();
            RecalculateDemand();
        }
        #endregion

        #region TÍNH TOÁN CÂN BẰNG NHU CẦU (DEMAND CALCULATOR)
        private void RecalculateDemand()
        {
            boardCounts.Clear();
            queueCounts.Clear();
            totalDemand.Clear();
            carSeatCounts.Clear();

            var roadCfg = LevelConfigLoader.GetRoadConfig(currentRoadId);

            // 1. Đếm hạt tranh cát (chỉ tính ngoài vùng khoét đường)
            for (int y = 0; y < 40; y++)
            {
                for (int x = 0; x < 40; x++)
                {
                    if (roadCfg != null && roadCfg.IsCutout(x, 39 - y)) continue;

                    int c = sandboardGrid[y, x];
                    if (c >= 0)
                    {
                        if (!boardCounts.ContainsKey(c)) boardCounts[c] = 0;
                        boardCounts[c]++;
                    }
                }
            }

            // 2. Đếm khách hàng đợi (Queue)
            if (queuePassengers != null)
            {
                foreach (var q in queuePassengers)
                {
                    if (q >= 0)
                    {
                        if (!queueCounts.ContainsKey(q)) queueCounts[q] = 0;
                        queueCounts[q] += exchangeRatio;
                    }
                }
            }

            // 3. Tổng nhu cầu = Hạt cát + Queue
            foreach (var kvp in boardCounts) totalDemand[kvp.Key] = kvp.Value;
            foreach (var kvp in queueCounts)
            {
                if (!totalDemand.ContainsKey(kvp.Key)) totalDemand[kvp.Key] = 0;
                totalDemand[kvp.Key] += kvp.Value;
            }

            // 4. Tổng ghế xe trong bãi
            for (int r = 0; r < parkingRows; r++)
            {
                for (int c = 0; c < parkingCols; c++)
                {
                    string cell = carGrid[r, c];
                    if (string.IsNullOrEmpty(cell) || cell.Equals("None", StringComparison.OrdinalIgnoreCase)) continue;
                    string[] parts = cell.Split('_');
                    if (parts.Length >= 2 && int.TryParse(parts[0], out int color) && int.TryParse(parts[1], out int cap))
                    {
                        if (!carSeatCounts.ContainsKey(color)) carSeatCounts[color] = 0;
                        carSeatCounts[color] += cap;
                    }
                }
            }
        }
        #endregion

        #region ĐỌC & LƯU CẤU HÌNH (JSON I/O)
        public void LoadLevel(int levelId)
        {
            var lvl = LevelConfigLoader.LoadLevel(levelId);
            if (lvl == null)
            {
                EditorUtility.DisplayDialog("Lỗi", $"Không tìm thấy Level {levelId} trong levelNCXHCfg.json!", "OK");
                return;
            }

            currentLevelId = lvl.id;
            currentRoadId = lvl.road;
            iniCapacity = lvl.iniCapacity;
            maxCapacity = lvl.maxCapacity;
            exchangeRatio = lvl.exchangeRatio > 0 ? lvl.exchangeRatio : 10;
            queuePassengers = lvl.queue != null ? new List<int>(lvl.queue) : new List<int>();

            // Nạp thông tin Collect & PixelMap
            var col = LevelConfigLoader.GetCollect(lvl.collect);
            if (col != null)
            {
                currentPictureId = col.picture;
                currentCollectName = col.name;
            }
            else
            {
                currentPictureId = lvl.collect;
                currentCollectName = "Mặc định";
            }

            // Nạp tranh cát PixelMap chuẩn 100% đứng
            var pData = LevelConfigLoader.LoadPixelMap(currentPictureId);
            if (pData != null && pData.points != null)
            {
                for (int x = 0; x < 40; x++)
                {
                    for (int y = 0; y < 40; y++)
                    {
                        // JSON: points[col, row] -> Unity: sandboardGrid[39 - row, col]
                        sandboardGrid[y, x] = pData.points[x, 39 - y];
                    }
                }
            }

            // Nạp ma trận xe
            int rCount = Mathf.Max(1, lvl.RowCount);
            int cCount = Mathf.Max(1, lvl.ColCount);
            ResizeParkingGrid(rCount, cCount);

            for (int r = 0; r < parkingRows; r++)
            {
                for (int c = 0; c < parkingCols; c++)
                {
                    if (r < lvl.bus.Count && c < lvl.bus[r].Count) carGrid[r, c] = lvl.bus[r][c];
                    else carGrid[r, c] = "None";
                }
            }

            EnsureCanvasTexture();
            RecalculateDemand();
            SimulateWinnability(out int moves, out string _);
            solverStatusMsg = $"Đã nạp Level {levelId}. Kiểm tra: {(moves > 0 ? $"Thắng trong {moves} bước." : "Cần kiểm tra")}";
            solverStatusType = MessageType.Info;
            Repaint();
        }

        public void LoadPixelMapDirect(int pictureId, string collectName)
        {
            var pData = LevelConfigLoader.LoadPixelMap(pictureId);
            if (pData != null && pData.points != null)
            {
                currentPictureId = pictureId;
                currentCollectName = collectName;

                for (int x = 0; x < 40; x++)
                {
                    for (int y = 0; y < 40; y++)
                    {
                        sandboardGrid[y, x] = pData.points[x, 39 - y];
                    }
                }

                UpdateCanvasTexture();
                RecalculateDemand();
                Repaint();
                Debug.Log($"<color=green>[Level Designer Pro]</color> Đã nạp tranh cát #{pictureId} '{collectName}' chuẩn đứng 100%!");
            }
        }

        public void SaveLevel()
        {
            var lvl = LevelConfigLoader.LoadLevel(currentLevelId) ?? new LevelData { id = currentLevelId };
            lvl.id = currentLevelId;
            lvl.road = currentRoadId;
            lvl.iniCapacity = iniCapacity;
            lvl.maxCapacity = maxCapacity;
            lvl.exchangeRatio = exchangeRatio;
            lvl.queue = new List<int>(queuePassengers);

            // Ghi ma trận xe
            lvl.bus = new List<List<string>>();
            for (int r = 0; r < parkingRows; r++)
            {
                var rowList = new List<string>();
                for (int c = 0; c < parkingCols; c++)
                {
                    rowList.Add(string.IsNullOrEmpty(carGrid[r, c]) ? "None" : carGrid[r, c]);
                }
                lvl.bus.Add(rowList);
            }

            // Lưu Level JSON
            LevelConfigLoader.SaveLevel(lvl);

            // Lưu PixelMap JSON
            int[,] toSave = new int[40, 40];
            for (int x = 0; x < 40; x++)
                for (int y = 0; y < 40; y++)
                    toSave[x, 39 - y] = sandboardGrid[y, x];

            LevelConfigLoader.SavePixelMap(currentPictureId, toSave);

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Thành Công", $"Đã lưu Level {currentLevelId} và PixelMap #{currentPictureId} thành công!", "OK");
        }
        #endregion
    }
}
