using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using WhoGetInBus.GamePlay;

namespace WhoGetInBus.EditorTools
{
    /// <summary>
    /// Cửa sổ Editor chuyên dụng xem trực quan và kiểm tra toàn bộ các loại đường đi (Road Config)
    /// trong Assets/GameConfig/roadNCXHCfg.json.
    /// Hỗ trợ cả 2D Canvas UI (Map Canvass / roadTop & roadBottom), 2D World và 3D Ground.
    /// </summary>
    public class RoadConfigGizmoViewerWindow : EditorWindow
    {
        [MenuItem("Tools/Road Config Gizmo Viewer (Xem Đường Đi GameConfig)", false, 5)]
        public static void OpenWindow()
        {
            var window = GetWindow<RoadConfigGizmoViewerWindow>("Road Gizmo Viewer");
            window.minSize = new Vector2(540, 750);
            window.Show();
        }

        #region DATA MODEL
        public class RoadData
        {
            public int id;
            public float speed = 5f;
            public float maxSpeed = 7.2f;
            public float accSpeed = 0.1f;
            public string loopPathRaw = "";
            public string exitPathRaw = "";
            public string cavePathRaw = "";
            public int caveStartIdx = -1;
            public int caveEndIdx = -1;

            public List<Vector3> loopWaypoints = new List<Vector3>();
            public List<Vector3> exitWaypoints = new List<Vector3>();
            public List<int> levelsUsing = new List<int>();

            public float loopLength = 0f;
            public float exitLength = 0f;
        }

        public enum WindowDisplayMode
        {
            Canvas_2D_UI, // 2D Canvas UI (Khớp chuẩn Map Canvass / Top & Bottom)
            XY_2D_World,  // 2D Thế Giới X-Y (Scale 1:1)
            XZ_3D_Ground  // 3D Mặt đất X-Z
        }
        #endregion

        #region STATE VARIABLES
        private Dictionary<int, RoadData> roads = new Dictionary<int, RoadData>();
        private List<int> roadIds = new List<int>();
        private int selectedRoadIndex = 0;
        private int selectedRoadId = 1;

        // Tìm kiếm theo Level
        private int searchLevelInput = 1001;
        private string searchLevelResult = "";

        // Chế độ hiển thị mặt phẳng
        private WindowDisplayMode displayMode = WindowDisplayMode.Canvas_2D_UI;
        private bool invertZ = true; // Mặc định BẬT: Đảo ngược trục Z/Y (-Y) để khớp chuẩn với Map
        private bool invertX = false;
        private float heightOffset = 0.05f;
        private Vector3 worldOffset = Vector3.zero;

        // Cài đặt 2D Canvas UI
        private Canvas targetCanvas;
        private Vector2 canvasScale = new Vector2(125f, 125f);
        private Vector2 canvasOffset = new Vector2(0f, -180f);
        private float configJunctionY = 1.91f;

        private bool showLoopPath = true;
        private bool showExitPath = true;
        private bool showCavePath = true;
        private bool showWaypoints = true;
        private float waypointSize = 0.22f;
        private bool showDirectionArrows = true;
        private bool showLabels = true;
        private bool showCoordinates = false;
        private bool showAllRoads = false;

        // Màu sắc
        private Color colorLoop = new Color(0.15f, 0.85f, 1.0f, 0.95f);
        private Color colorExit = new Color(0.2f, 1.0f, 0.35f, 0.95f);
        private Color colorCave = new Color(1.0f, 0.45f, 0.05f, 0.95f);
        private Color colorStart = new Color(1.0f, 0.9f, 0.1f, 1.0f);
        private Color colorJunction = new Color(1.0f, 0.2f, 0.6f, 1.0f);

        // Mô phỏng chuyển động xe
        private bool isSimulating = false;
        private float simProgress = 0f;
        private bool simExitBranch = false;
        private float simSpeedMultiplier = 1.0f;
        private double lastUpdateTime = 0;

        // UI Tabs & Scroll
        private Vector2 scrollPos;
        private int currentTab = 0;
        private readonly string[] tabNames = new string[] { "🔍 Xem & Thao Tác Đường", "📊 Danh Sách 16 Đường & Level", "⚙ Cài Đặt Mặt Phẳng & Map" };

        private GUIStyle labelStyle;
        private Texture2D labelBgTex;
        #endregion

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += OnEditorUpdate;
            FindTargetCanvas();
            LoadAllConfigs();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.update -= OnEditorUpdate;
            if (labelBgTex != null)
            {
                DestroyImmediate(labelBgTex);
                labelBgTex = null;
            }
        }

        private void FindTargetCanvas()
        {
            if (targetCanvas == null)
            {
                var cObj = GameObject.Find("Map Canvass") ?? GameObject.Find("Background Canvas (Road)");
                if (cObj != null) targetCanvas = cObj.GetComponent<Canvas>();
                if (targetCanvas == null) targetCanvas = FindAnyObjectByType<Canvas>();
            }
        }

        private void OnEditorUpdate()
        {
            if (isSimulating)
            {
                double now = EditorApplication.timeSinceStartup;
                float dt = (float)(now - lastUpdateTime);
                if (dt > 0.1f) dt = 0.016f;
                lastUpdateTime = now;

                var road = GetCurrentRoad();
                if (road != null)
                {
                    float pathLen = simExitBranch ? road.exitLength : road.loopLength;
                    if (pathLen > 0.001f)
                    {
                        float spd = (road.speed > 0 ? road.speed : 5f) * simSpeedMultiplier;
                        simProgress = (simProgress + (spd * dt / pathLen)) % 1f;
                        SceneView.RepaintAll();
                        Repaint();
                    }
                }
            }
            else
            {
                lastUpdateTime = EditorApplication.timeSinceStartup;
            }
        }

        #region LOAD & PARSE CONFIGS
        public void LoadAllConfigs()
        {
            roads.Clear();
            roadIds.Clear();

            string roadPath = Path.Combine(Application.dataPath, "GameConfig", "roadNCXHCfg.json");
            string levelPath = Path.Combine(Application.dataPath, "GameConfig", "levelNCXHCfg.json");

            if (!File.Exists(roadPath))
            {
                Debug.LogError($"[RoadConfigGizmoViewer] Không tìm thấy file: {roadPath}");
                return;
            }

            try
            {
                string roadJson = File.ReadAllText(roadPath);
                var matches = Regex.Matches(roadJson, @"""(\d+)""\s*:\s*\{([\s\S]*?)(?=\n\s*""\d+""\s*:|\n\})");

                foreach (Match m in matches)
                {
                    int rId = int.Parse(m.Groups[1].Value);
                    string block = m.Groups[2].Value;

                    var data = new RoadData { id = rId };

                    var loopMatch = Regex.Match(block, @"""loop_path_root""\s*:\s*""([^""]*)""");
                    if (loopMatch.Success) data.loopPathRaw = loopMatch.Groups[1].Value;

                    var exitMatch = Regex.Match(block, @"""exit_path_root""\s*:\s*""([^""]*)""");
                    if (exitMatch.Success) data.exitPathRaw = exitMatch.Groups[1].Value;

                    var caveMatch = Regex.Match(block, @"""cave_path""\s*:\s*""([^""]*)""");
                    if (caveMatch.Success) data.cavePathRaw = caveMatch.Groups[1].Value;

                    var spdMatch = Regex.Match(block, @"""speed""\s*:\s*([\d.]+)");
                    if (spdMatch.Success) float.TryParse(spdMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out data.speed);

                    var maxSpdMatch = Regex.Match(block, @"""max_speed""\s*:\s*([\d.]+)");
                    if (maxSpdMatch.Success) float.TryParse(maxSpdMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out data.maxSpeed);

                    var accSpdMatch = Regex.Match(block, @"""acc_speed""\s*:\s*([\d.]+)");
                    if (accSpdMatch.Success) float.TryParse(accSpdMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out data.accSpeed);

                    if (!string.IsNullOrEmpty(data.cavePathRaw))
                    {
                        string[] cparts = data.cavePathRaw.Split(',');
                        if (cparts.Length >= 2)
                        {
                            int.TryParse(cparts[0].Trim(), out data.caveStartIdx);
                            int.TryParse(cparts[1].Trim(), out data.caveEndIdx);
                        }
                    }

                    data.loopWaypoints = ParsePoints(data.loopPathRaw);
                    data.exitWaypoints = ParsePoints(data.exitPathRaw);
                    data.loopLength = ComputeLength(data.loopWaypoints);
                    data.exitLength = ComputeLength(data.exitWaypoints);

                    roads[rId] = data;
                    roadIds.Add(rId);
                }

                roadIds.Sort();

                // Nạp Level mapping
                if (File.Exists(levelPath))
                {
                    string levelJson = File.ReadAllText(levelPath);
                    var lvlMatches = Regex.Matches(levelJson, @"""(\d+)""\s*:\s*\{[\s\S]*?""road""\s*:\s*(\d+)");
                    foreach (Match lm in lvlMatches)
                    {
                        int lvlId = int.Parse(lm.Groups[1].Value);
                        int rId = int.Parse(lm.Groups[2].Value);
                        if (roads.TryGetValue(rId, out var rData))
                        {
                            if (!rData.levelsUsing.Contains(lvlId)) rData.levelsUsing.Add(lvlId);
                        }
                    }
                }

                if (selectedRoadIndex >= roadIds.Count) selectedRoadIndex = 0;
                if (roadIds.Count > 0) selectedRoadId = roadIds[selectedRoadIndex];
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RoadConfigGizmoViewer] Lỗi phân tích: {ex.Message}");
            }
        }

        private List<Vector3> ParsePoints(string raw)
        {
            var list = new List<Vector3>();
            if (string.IsNullOrEmpty(raw)) return list;

            FindTargetCanvas();

            string[] points = raw.Split(';');
            foreach (var p in points)
            {
                string clean = p.Trim().Trim('[', ']');
                if (string.IsNullOrEmpty(clean)) continue;

                string[] parts = clean.Split(',');
                if (parts.Length >= 2)
                {
                    if (float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                        float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float yOrZ))
                    {
                        float elev = 0f;
                        if (parts.Length >= 3)
                        {
                            float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out elev);
                        }

                        float calcX = invertX ? -x : x;
                        float calcZ = invertZ ? -yOrZ : yOrZ;

                        if (displayMode == WindowDisplayMode.Canvas_2D_UI)
                        {
                            float pixelX = calcX * canvasScale.x + canvasOffset.x;
                            float pixelY = (calcZ + (invertZ ? configJunctionY : -configJunctionY)) * canvasScale.y + canvasOffset.y;

                            Vector3 localCanvasPos = new Vector3(pixelX, pixelY, elev);

                            if (targetCanvas != null)
                            {
                                list.Add(targetCanvas.transform.TransformPoint(localCanvasPos) + worldOffset);
                            }
                            else
                            {
                                list.Add(localCanvasPos + worldOffset);
                            }
                        }
                        else if (displayMode == WindowDisplayMode.XY_2D_World)
                        {
                            Vector3 pt = new Vector3(calcX, calcZ, elev + heightOffset);
                            list.Add(pt + worldOffset);
                        }
                        else // XZ_3D_Ground
                        {
                            Vector3 pt = new Vector3(calcX, elev + heightOffset, calcZ);
                            list.Add(pt + worldOffset);
                        }
                    }
                }
            }
            return list;
        }

        private float ComputeLength(List<Vector3> pts)
        {
            if (pts == null || pts.Count < 2) return 0f;
            float len = 0f;
            for (int i = 0; i < pts.Count - 1; i++) len += Vector3.Distance(pts[i], pts[i + 1]);
            return len;
        }

        private void RecalculateAllPoints()
        {
            foreach (var kvp in roads)
            {
                kvp.Value.loopWaypoints = ParsePoints(kvp.Value.loopPathRaw);
                kvp.Value.exitWaypoints = ParsePoints(kvp.Value.exitPathRaw);
                kvp.Value.loopLength = ComputeLength(kvp.Value.loopWaypoints);
                kvp.Value.exitLength = ComputeLength(kvp.Value.exitWaypoints);
            }
            SceneView.RepaintAll();
        }

        public RoadData GetCurrentRoad()
        {
            if (roads.TryGetValue(selectedRoadId, out var r)) return r;
            return null;
        }
        #endregion

        #region GUI RENDERING
        private void OnGUI()
        {
            DrawHeaderBanner();

            currentTab = GUILayout.Toolbar(currentTab, tabNames, GUILayout.Height(30));
            EditorGUILayout.Space(6);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            switch (currentTab)
            {
                case 0:
                    DrawTabMainViewer();
                    break;
                case 1:
                    DrawTabRoadsSummary();
                    break;
                case 2:
                    DrawTabSettings();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeaderBanner()
        {
            EditorGUILayout.BeginVertical("box");
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.2f, 0.85f, 1f) }
            };
            GUILayout.Label("🛣️ ROAD CONFIG GIZMO VIEWER", titleStyle);
            GUILayout.Label($"Xem toạ độ đường đi ({roadIds.Count} loại đường) khớp với 2D/3D GamePlay", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private void DrawTabMainViewer()
        {
            var curRoad = GetCurrentRoad();

            // 1. THANH CHỌN MẶT PHẲNG 2D / 3D
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("1. CHẾ ĐỘ HIỂN THỊ MẶT ĐƯỜNG (2D / 3D):", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = (displayMode == WindowDisplayMode.Canvas_2D_UI) ? new Color(0.2f, 0.9f, 0.4f) : Color.white;
            if (GUILayout.Button("🖼️ 2D Canvas UI (Map Canvass)", GUILayout.Height(32)))
            {
                displayMode = WindowDisplayMode.Canvas_2D_UI;
                invertZ = true;
                FindTargetCanvas();
            }
            GUI.backgroundColor = (displayMode == WindowDisplayMode.XY_2D_World) ? new Color(0.2f, 0.9f, 0.4f) : Color.white;
            if (GUILayout.Button("📐 2D World (X-Y)", GUILayout.Height(32)))
            {
                displayMode = WindowDisplayMode.XY_2D_World;
                invertZ = true;
            }
            GUI.backgroundColor = (displayMode == WindowDisplayMode.XZ_3D_Ground) ? new Color(0.2f, 0.9f, 0.4f) : Color.white;
            if (GUILayout.Button("🌐 3D Sàn (X-Z)", GUILayout.Height(32)))
            {
                displayMode = WindowDisplayMode.XZ_3D_Ground;
                invertZ = true;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                RecalculateAllPoints();
                FocusSceneOnSelectedRoad();
            }

            if (displayMode == WindowDisplayMode.Canvas_2D_UI)
            {
                EditorGUILayout.HelpBox($"Đang ở chế độ 2D Canvas UI: Khớp chuẩn theo 'Map Canvass' (Scale = {canvasScale.x}, Junction Y = {configJunctionY}).", MessageType.Info);
            }

            EditorGUILayout.EndVertical();

            // 2. BỘ CHỌN ĐƯỜNG ĐI
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("2. LỰA CHỌN LOẠI ĐƯỜNG ĐI:", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = selectedRoadIndex > 0;
            if (GUILayout.Button("◀ Trước", GUILayout.Width(75), GUILayout.Height(28)))
            {
                selectedRoadIndex--;
                selectedRoadId = roadIds[selectedRoadIndex];
                FocusSceneOnSelectedRoad();
            }
            GUI.enabled = true;

            string[] roadOptions = new string[roadIds.Count];
            for (int i = 0; i < roadIds.Count; i++)
            {
                int id = roadIds[i];
                var r = roads[id];
                roadOptions[i] = $"Road #{id} ({r.loopWaypoints.Count} loop, {r.exitWaypoints.Count} exit - {r.levelsUsing.Count} levels)";
            }

            int newIdx = EditorGUILayout.Popup(selectedRoadIndex, roadOptions, GUILayout.Height(28));
            if (newIdx != selectedRoadIndex)
            {
                selectedRoadIndex = newIdx;
                selectedRoadId = roadIds[selectedRoadIndex];
                FocusSceneOnSelectedRoad();
            }

            GUI.enabled = selectedRoadIndex < roadIds.Count - 1;
            if (GUILayout.Button("Sau ▶", GUILayout.Width(75), GUILayout.Height(28)))
            {
                selectedRoadIndex++;
                selectedRoadId = roadIds[selectedRoadIndex];
                FocusSceneOnSelectedRoad();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            // Ma trận 16 nút chọn nhanh
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Chọn nhanh mã đường:", EditorStyles.miniBoldLabel);
            int cols = 4;
            for (int i = 0; i < roadIds.Count; i += cols)
            {
                EditorGUILayout.BeginHorizontal();
                for (int c = 0; c < cols; c++)
                {
                    int idx = i + c;
                    if (idx < roadIds.Count)
                    {
                        int id = roadIds[idx];
                        bool isSel = (id == selectedRoadId);
                        GUI.backgroundColor = isSel ? new Color(0.2f, 0.9f, 0.4f) : Color.white;
                        if (GUILayout.Button($"Road {id}", GUILayout.Height(26)))
                        {
                            selectedRoadIndex = idx;
                            selectedRoadId = id;
                            FocusSceneOnSelectedRoad();
                        }
                    }
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(6);
            // Tra cứu theo số Level
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("🔎 Tra cứu theo Level:", GUILayout.Width(130));
            searchLevelInput = EditorGUILayout.IntField(searchLevelInput, GUILayout.Width(80));
            if (GUILayout.Button("Tìm Road Của Level", GUILayout.Height(22)))
            {
                FindRoadByLevel(searchLevelInput);
            }
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(searchLevelResult))
            {
                EditorGUILayout.HelpBox(searchLevelResult, MessageType.Info);
            }

            EditorGUILayout.EndVertical();

            // 3. THÔNG TIN CHI TIẾT ĐƯỜNG ĐANG CHỌN
            if (curRoad != null)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"3. THÔNG TIN CHI TIẾT: ROAD #{curRoad.id}", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"• Tốc độ chạy (speed): {curRoad.speed}");
                EditorGUILayout.LabelField($"• Max speed: {curRoad.maxSpeed}");
                EditorGUILayout.LabelField($"• Gia tốc (acc): {curRoad.accSpeed}");
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"• Số điểm Loop: {curRoad.loopWaypoints.Count} điểm (Dài: {curRoad.loopLength:F1}m)");
                EditorGUILayout.LabelField($"• Số điểm Exit: {curRoad.exitWaypoints.Count} điểm (Dài: {curRoad.exitLength:F1}m)");
                EditorGUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(curRoad.cavePathRaw))
                {
                    EditorGUILayout.LabelField($"• Lối hầm (Cave Path): Điểm WP #{curRoad.caveStartIdx} ➔ WP #{curRoad.caveEndIdx}", EditorStyles.boldLabel);
                }
                else
                {
                    EditorGUILayout.LabelField("• Lối hầm (Cave Path): Không có (Đường tiêu chuẩn)");
                }

                EditorGUILayout.LabelField($"• Số Level sử dụng loại đường này: {curRoad.levelsUsing.Count} levels", EditorStyles.miniBoldLabel);
                if (curRoad.levelsUsing.Count > 0)
                {
                    string sampleLvls = string.Join(", ", curRoad.levelsUsing.GetRange(0, Mathf.Min(8, curRoad.levelsUsing.Count)));
                    if (curRoad.levelsUsing.Count > 8) sampleLvls += "...";
                    EditorGUILayout.LabelField($"  (Ví dụ: {sampleLvls})", EditorStyles.miniLabel);
                }

                EditorGUILayout.EndVertical();
            }

            // 4. THAO TÁC NHANH TRÊN SCENE VIEW
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("4. THAO TÁC SCENE & MÔ PHỎNG:", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🎯 Focus Camera Scene (F)", GUILayout.Height(32)))
            {
                FocusSceneOnSelectedRoad();
            }
            if (GUILayout.Button("🔄 Nạp Lại File JSON", GUILayout.Height(32)))
            {
                LoadAllConfigs();
                ShowNotification(new GUIContent("Đã nạp lại file JSON thành công!"));
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📦 Tạo GameObject Viewer (Kèm Game View Lines)", GUILayout.Height(32)))
            {
                SpawnOrUpdateViewerInScene();
            }
            if (GUILayout.Button("📋 Copy Toạ Độ Vector3[]", GUILayout.Height(32)))
            {
                CopyCoordinatesToClipboard(curRoad);
            }
            EditorGUILayout.EndHorizontal();

            // MÔ PHỎNG XE CHẠY THỬ NGHIỆM
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("🚗 Mô phỏng xe chạy thử trên Scene (Simulation):", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = isSimulating ? new Color(1f, 0.4f, 0.4f) : new Color(0.3f, 0.85f, 0.4f);
            if (GUILayout.Button(isSimulating ? "⏹ DỪNG MÔ PHỎNG" : "▶ CHẠY MÔ PHỎNG XE", GUILayout.Height(32)))
            {
                isSimulating = !isSimulating;
                lastUpdateTime = EditorApplication.timeSinceStartup;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;

            simExitBranch = GUILayout.Toggle(simExitBranch, "Chạy trên làn Exit", "Button", GUILayout.Height(32), GUILayout.Width(130));
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            simProgress = EditorGUILayout.Slider("Vị trí xe (% tiến độ):", simProgress, 0f, 1f);
            simSpeedMultiplier = EditorGUILayout.Slider("Tốc độ mô phỏng:", simSpeedMultiplier, 0.2f, 3.0f);
            if (EditorGUI.EndChangeCheck())
            {
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTabRoadsSummary()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("BẢNG TỔNG HỢP 16 LOẠI ĐƯỜNG ĐI TRONG GAME:", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal("box");
            EditorGUILayout.LabelField("Road ID", EditorStyles.boldLabel, GUILayout.Width(65));
            EditorGUILayout.LabelField("Điểm Loop", EditorStyles.boldLabel, GUILayout.Width(75));
            EditorGUILayout.LabelField("Điểm Exit", EditorStyles.boldLabel, GUILayout.Width(75));
            EditorGUILayout.LabelField("Cave Path", EditorStyles.boldLabel, GUILayout.Width(75));
            EditorGUILayout.LabelField("Số Level", EditorStyles.boldLabel, GUILayout.Width(65));
            EditorGUILayout.LabelField("Thao tác", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < roadIds.Count; i++)
            {
                int id = roadIds[i];
                var r = roads[id];
                bool isCur = (id == selectedRoadId);

                GUI.backgroundColor = isCur ? new Color(0.85f, 0.95f, 1f) : Color.white;
                EditorGUILayout.BeginHorizontal("box");
                EditorGUILayout.LabelField($"Road {id}", isCur ? EditorStyles.boldLabel : EditorStyles.label, GUILayout.Width(65));
                EditorGUILayout.LabelField($"{r.loopWaypoints.Count} pts", GUILayout.Width(75));
                EditorGUILayout.LabelField($"{r.exitWaypoints.Count} pts", GUILayout.Width(75));
                EditorGUILayout.LabelField(string.IsNullOrEmpty(r.cavePathRaw) ? "-" : r.cavePathRaw, GUILayout.Width(75));
                EditorGUILayout.LabelField($"{r.levelsUsing.Count} lvls", GUILayout.Width(65));

                if (GUILayout.Button(isCur ? "Đang chọn" : "Xem Gizmo", GUILayout.Height(20)))
                {
                    selectedRoadIndex = i;
                    selectedRoadId = id;
                    currentTab = 0;
                    FocusSceneOnSelectedRoad();
                }
                EditorGUILayout.EndHorizontal();
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTabSettings()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("CÀI ĐẶT 2D CANVAS UI (MAP CANVASS):", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            targetCanvas = (Canvas)EditorGUILayout.ObjectField("Target Canvas (Map Canvass):", targetCanvas, typeof(Canvas), true);
            canvasScale = EditorGUILayout.Vector2Field("Tỷ lệ Canvas Scale (Chuẩn: 125):", canvasScale);
            canvasOffset = EditorGUILayout.Vector2Field("Dịch tâm Canvas Offset (Chuẩn: 0, -180):", canvasOffset);
            configJunctionY = EditorGUILayout.FloatField("Toạ độ Ngã Rẽ Junction Y (Chuẩn: 1.91):", configJunctionY);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🎯 Đặt Lại Chuẩn Map Canvass (125, -180)", GUILayout.Height(26)))
            {
                canvasScale = new Vector2(125f, 125f);
                canvasOffset = new Vector2(0f, -180f);
                configJunctionY = 1.91f;
                invertZ = true;
                invertX = false;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("HƯỚNG TRỤC TOẠ ĐỘ:", EditorStyles.boldLabel);
            invertZ = EditorGUILayout.Toggle("Đảo ngược trục Z/Y (-Y):", invertZ);
            invertX = EditorGUILayout.Toggle("Đảo ngược trục X (-X):", invertX);
            worldOffset = EditorGUILayout.Vector3Field("Dịch tâm toạ độ (World Offset):", worldOffset);
            heightOffset = EditorGUILayout.FloatField("Độ cao nâng lên (Height Offset):", heightOffset);

            if (EditorGUI.EndChangeCheck())
            {
                RecalculateAllPoints();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("CÀI ĐẶT GIZMOS & NHÃN HIỂN THỊ SCENE:", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            showLoopPath = EditorGUILayout.Toggle("Vẽ đường Loop Path:", showLoopPath);
            showExitPath = EditorGUILayout.Toggle("Vẽ đường Exit Path:", showExitPath);
            showCavePath = EditorGUILayout.Toggle("Đánh dấu đoạn Hầm (Cave Path):", showCavePath);
            showWaypoints = EditorGUILayout.Toggle("Hiện khối điểm Waypoints:", showWaypoints);
            waypointSize = EditorGUILayout.Slider("Kích thước Waypoint Sphere:", waypointSize, 0.05f, 0.6f);
            showDirectionArrows = EditorGUILayout.Toggle("Hiện mũi tên chỉ hướng:", showDirectionArrows);
            showLabels = EditorGUILayout.Toggle("Hiện nhãn số thứ tự WP # trên Scene:", showLabels);
            showCoordinates = EditorGUILayout.Toggle("Hiện toạ độ (X, Z/Y) trên từng điểm:", showCoordinates);
            showAllRoads = EditorGUILayout.Toggle("Xem mờ tất cả 16 đường cùng lúc:", showAllRoads);
            if (EditorGUI.EndChangeCheck())
            {
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Màu sắc đường:", EditorStyles.miniBoldLabel);
            colorLoop = EditorGUILayout.ColorField("Màu Loop Path:", colorLoop);
            colorExit = EditorGUILayout.ColorField("Màu Exit Path:", colorExit);
            colorCave = EditorGUILayout.ColorField("Màu Cave Path:", colorCave);
            colorStart = EditorGUILayout.ColorField("Màu Điểm Bắt Đầu:", colorStart);
            colorJunction = EditorGUILayout.ColorField("Màu Ngã Rẽ Thoát:", colorJunction);

            EditorGUILayout.EndVertical();
        }
        #endregion

        #region SCENE GUI DRAWING
        private void OnSceneGUI(SceneView sceneView)
        {
            if (roads == null || roads.Count == 0) return;

            InitLabelStyle();

            if (showAllRoads)
            {
                foreach (var kvp in roads)
                {
                    if (kvp.Key == selectedRoadId) continue;
                    DrawRoadInScene(kvp.Value, false);
                }
            }

            var curRoad = GetCurrentRoad();
            if (curRoad != null)
            {
                DrawRoadInScene(curRoad, true);

                if (isSimulating || simProgress > 0f)
                {
                    DrawSimulatedCarInScene(curRoad);
                }
            }
        }

        private void DrawRoadInScene(RoadData road, bool isSelected)
        {
            Color lCol = isSelected ? colorLoop : new Color(colorLoop.r, colorLoop.g, colorLoop.b, 0.15f);
            Color exCol = isSelected ? colorExit : new Color(colorExit.r, colorExit.g, colorExit.b, 0.15f);

            // A. Vẽ Loop Path
            if (showLoopPath && road.loopWaypoints != null && road.loopWaypoints.Count > 1)
            {
                for (int i = 0; i < road.loopWaypoints.Count; i++)
                {
                    Vector3 p1 = road.loopWaypoints[i];
                    Vector3 p2 = road.loopWaypoints[(i + 1) % road.loopWaypoints.Count];

                    bool isCave = isSelected && showCavePath && road.caveStartIdx >= 0 && road.caveEndIdx >= 0 &&
                                  i >= road.caveStartIdx && i < road.caveEndIdx;

                    Color segCol = isCave ? colorCave : lCol;
                    Handles.color = segCol;
                    Handles.DrawAAPolyLine(isSelected ? 4f : 1.5f, p1, p2);

                    if (isSelected && showWaypoints)
                    {
                        if (i == 0)
                        {
                            Handles.color = colorStart;
                            Handles.SphereHandleCap(0, p1, Quaternion.identity, waypointSize * 2.2f, EventType.Repaint);
                        }
                        else if (isCave)
                        {
                            Handles.color = colorCave;
                            Handles.CubeHandleCap(0, p1, Quaternion.identity, waypointSize * 2.4f, EventType.Repaint);
                        }
                        else
                        {
                            Handles.color = segCol;
                            Handles.SphereHandleCap(0, p1, Quaternion.identity, waypointSize * 1.8f, EventType.Repaint);
                        }

                        if (showDirectionArrows && Vector3.Distance(p1, p2) > 0.2f)
                        {
                            Vector3 mid = (p1 + p2) * 0.5f;
                            Vector3 dir = (p2 - p1).normalized;
                            DrawSceneArrow(mid, dir, 0.45f, segCol);
                        }
                    }

                    if (isSelected && showLabels)
                    {
                        string txt = $"WP #{i}";
                        if (showCoordinates) txt += $"\n({p1.x:F1}, {p1.y:F1})";
                        if (i == 0) txt = $"★ START (WP 0)";
                        if (road.caveStartIdx >= 0 && i == road.caveStartIdx) txt += " [HẦM VÀO]";
                        if (road.caveEndIdx >= 0 && i == road.caveEndIdx) txt += " [HẦM RA]";

                        Vector3 lblPos = p1 + (displayMode == WindowDisplayMode.XZ_3D_Ground ? new Vector3(0, 0.35f, 0) : new Vector3(0, 0.35f, -0.1f));
                        Handles.Label(lblPos, txt, labelStyle);
                    }
                }
            }

            // B. Vẽ Exit Path
            if (showExitPath && road.exitWaypoints != null && road.exitWaypoints.Count > 0)
            {
                for (int i = 0; i < road.exitWaypoints.Count; i++)
                {
                    Vector3 p1 = road.exitWaypoints[i];
                    if (i < road.exitWaypoints.Count - 1)
                    {
                        Vector3 p2 = road.exitWaypoints[i + 1];
                        Handles.color = exCol;
                        Handles.DrawAAPolyLine(isSelected ? 4f : 1.5f, p1, p2);

                        if (isSelected && showDirectionArrows)
                        {
                            Vector3 mid = (p1 + p2) * 0.5f;
                            Vector3 dir = (p2 - p1).normalized;
                            DrawSceneArrow(mid, dir, 0.55f, exCol);
                        }
                    }

                    if (isSelected && showWaypoints)
                    {
                        if (i == 0)
                        {
                            Handles.color = colorJunction;
                            Handles.SphereHandleCap(0, p1, Quaternion.identity, waypointSize * 2.8f, EventType.Repaint);
                        }
                        else
                        {
                            Handles.color = exCol;
                            Handles.CubeHandleCap(0, p1, Quaternion.identity, waypointSize * 2.2f, EventType.Repaint);
                        }
                    }

                    if (isSelected && showLabels)
                    {
                        string txt = (i == 0) ? $"★ EXIT JUNCTION" : $"EXIT #{i}";
                        if (showCoordinates) txt += $"\n({p1.x:F1}, {p1.y:F1})";
                        Vector3 lblPos = p1 + (displayMode == WindowDisplayMode.XZ_3D_Ground ? new Vector3(0, 0.35f, 0) : new Vector3(0, 0.35f, -0.1f));
                        Handles.Label(lblPos, txt, labelStyle);
                    }
                }
            }
        }

        private void DrawSimulatedCarInScene(RoadData road)
        {
            List<Vector3> pts = simExitBranch ? road.exitWaypoints : road.loopWaypoints;
            if (pts == null || pts.Count < 2) return;

            Vector3 pos = EvaluatePathPos(pts, simProgress, out Vector3 fwd);
            Vector3 upDir = (displayMode == WindowDisplayMode.XZ_3D_Ground) ? Vector3.up : Vector3.back;
            Quaternion rot = fwd.sqrMagnitude > 0.001f ? Quaternion.LookRotation(fwd, upDir) : Quaternion.identity;

            Handles.color = simExitBranch ? Color.green : new Color(1f, 0.85f, 0.2f, 1f);
            Vector3 carSize = (displayMode == WindowDisplayMode.XZ_3D_Ground) 
                ? new Vector3(1.2f, 0.7f, 1.8f) 
                : new Vector3(0.6f, 0.9f, 0.2f);

            Matrix4x4 m = Matrix4x4.TRS(pos, rot, Vector3.one);
            using (new Handles.DrawingScope(m))
            {
                Handles.DrawWireCube(Vector3.zero, carSize);
                Handles.color = new Color(1f, 0.9f, 0.2f, 0.4f);
                Handles.CubeHandleCap(0, Vector3.zero, Quaternion.identity, 0.9f, EventType.Repaint);
            }

            Handles.color = Color.white;
            Handles.Label(pos + (displayMode == WindowDisplayMode.XZ_3D_Ground ? new Vector3(0, 0.9f, 0) : new Vector3(0, 0.9f, -0.2f)), $"BUS ({(simProgress * 100):F0}%)", labelStyle);
        }

        private Vector3 EvaluatePathPos(List<Vector3> pts, float t, out Vector3 forwardDir)
        {
            forwardDir = Vector3.forward;
            if (pts == null || pts.Count == 0) return Vector3.zero;
            if (pts.Count == 1) return pts[0];

            float total = ComputeLength(pts);
            float target = Mathf.Clamp01(t) * total;
            float cur = 0f;

            for (int i = 0; i < pts.Count - 1; i++)
            {
                float seg = Vector3.Distance(pts[i], pts[i + 1]);
                if (cur + seg >= target || i == pts.Count - 2)
                {
                    float factor = seg > 0.0001f ? (target - cur) / seg : 0f;
                    forwardDir = (pts[i + 1] - pts[i]).normalized;
                    return Vector3.Lerp(pts[i], pts[i + 1], factor);
                }
                cur += seg;
            }
            return pts[pts.Count - 1];
        }

        private void DrawSceneArrow(Vector3 pos, Vector3 dir, float size, Color col)
        {
            if (dir.sqrMagnitude < 0.001f) return;
            Handles.color = col;
            Vector3 r = (displayMode == WindowDisplayMode.XZ_3D_Ground)
                ? Vector3.Cross(Vector3.up, dir).normalized * (size * 0.35f)
                : Vector3.Cross(Vector3.forward, dir).normalized * (size * 0.35f);

            Vector3 tip = pos + dir * (size * 0.5f);
            Vector3 w1 = tip - dir * (size * 0.6f) + r;
            Vector3 w2 = tip - dir * (size * 0.6f) - r;

            Handles.DrawLine(pos - dir * (size * 0.5f), tip);
            Handles.DrawLine(tip, w1);
            Handles.DrawLine(tip, w2);
        }

        private void InitLabelStyle()
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle();
                labelStyle.normal.textColor = Color.white;
                labelStyle.fontSize = 11;
                labelStyle.fontStyle = FontStyle.Bold;
                labelStyle.alignment = TextAnchor.MiddleCenter;
                labelStyle.padding = new RectOffset(4, 4, 2, 2);

                labelBgTex = new Texture2D(1, 1);
                labelBgTex.SetPixel(0, 0, new Color(0.08f, 0.12f, 0.18f, 0.88f));
                labelBgTex.Apply();
                labelStyle.normal.background = labelBgTex;
            }
        }
        #endregion

        #region HELPER ACTIONS
        private void FocusSceneOnSelectedRoad()
        {
            var road = GetCurrentRoad();
            if (road == null || road.loopWaypoints == null || road.loopWaypoints.Count == 0) return;

            Bounds b = new Bounds(road.loopWaypoints[0], Vector3.zero);
            foreach (var pt in road.loopWaypoints) b.Encapsulate(pt);
            if (road.exitWaypoints != null)
            {
                foreach (var pt in road.exitWaypoints) b.Encapsulate(pt);
            }

            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.Frame(b, false);
            }
            SceneView.RepaintAll();
        }

        private void FindRoadByLevel(int levelId)
        {
            foreach (var kvp in roads)
            {
                if (kvp.Value.levelsUsing.Contains(levelId))
                {
                    selectedRoadId = kvp.Key;
                    selectedRoadIndex = roadIds.IndexOf(selectedRoadId);
                    searchLevelResult = $"Level {levelId} đang sử dụng Road #{selectedRoadId} (Tự động chọn & hiển thị!)";
                    FocusSceneOnSelectedRoad();
                    return;
                }
            }
            searchLevelResult = $"Không tìm thấy Level {levelId} trong levelNCXHCfg.json!";
        }

        private void SpawnOrUpdateViewerInScene()
        {
            GameObject obj = GameObject.Find("[RoadConfigGizmoViewer]");
            if (obj == null)
            {
                obj = new GameObject("[RoadConfigGizmoViewer]");
                Undo.RegisterCreatedObjectUndo(obj, "Create RoadConfigGizmoViewer");
            }

            var viewer = obj.GetComponent<RoadConfigGizmoViewer>();
            if (viewer == null) viewer = obj.AddComponent<RoadConfigGizmoViewer>();

            viewer.selectedRoadId = selectedRoadId;
            viewer.planeMode = (RoadConfigGizmoViewer.DisplayPlane)(int)displayMode;
            viewer.invertZ = invertZ;
            viewer.invertX = invertX;
            viewer.heightOffset = heightOffset;
            viewer.worldOffset = worldOffset;

            viewer.targetCanvas = targetCanvas;
            viewer.canvasScale = canvasScale;
            viewer.canvasOffset = canvasOffset;
            viewer.configJunctionY = configJunctionY;

            viewer.showInGameView = true;
            viewer.showLoopPath = showLoopPath;
            viewer.showExitPath = showExitPath;
            viewer.showCavePath = showCavePath;
            viewer.showWaypoints = showWaypoints;
            viewer.waypointRadius = waypointSize;
            viewer.showDirectionArrows = showDirectionArrows;
            viewer.showLabels = showLabels;
            viewer.showCoordinates = showCoordinates;
            viewer.showAllRoads = showAllRoads;
            viewer.enableSimulation = isSimulating;
            viewer.simProgress = simProgress;
            viewer.simExitBranch = simExitBranch;
            viewer.simSpeedMultiplier = simSpeedMultiplier;

            viewer.ReloadRoadConfigs();
            Selection.activeGameObject = obj;
            ShowNotification(new GUIContent($"Đã tạo [RoadConfigGizmoViewer] (Khớp 2D Canvas & Bật Game View)!"));
        }

        private void CopyCoordinatesToClipboard(RoadData road)
        {
            if (road == null) return;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"// Road #{road.id} Waypoints (Loop: {road.loopWaypoints.Count}, Exit: {road.exitWaypoints.Count})");
            sb.AppendLine("public static readonly Vector3[] LoopPoints = new Vector3[] {");
            for (int i = 0; i < road.loopWaypoints.Count; i++)
            {
                var p = road.loopWaypoints[i];
                sb.AppendLine($"    new Vector3({p.x:F3}f, {p.y:F3}f, {p.z:F3}f), // WP {i}");
            }
            sb.AppendLine("};");

            if (road.exitWaypoints.Count > 0)
            {
                sb.AppendLine("public static readonly Vector3[] ExitPoints = new Vector3[] {");
                for (int i = 0; i < road.exitWaypoints.Count; i++)
                {
                    var p = road.exitWaypoints[i];
                    sb.AppendLine($"    new Vector3({p.x:F3}f, {p.y:F3}f, {p.z:F3}f), // Exit {i}");
                }
                sb.AppendLine("};");
            }

            EditorGUIUtility.systemCopyBuffer = sb.ToString();
            ShowNotification(new GUIContent("Đã sao chép danh sách toạ độ C# vào Clipboard!"));
        }
        #endregion
    }
}
