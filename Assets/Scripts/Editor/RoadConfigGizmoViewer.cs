using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WhoGetInBus.GamePlay
{
    /// <summary>
    /// Component hỗ trợ vẽ Gizmos và xem trực quan các toạ độ đường đi (Waypoints)
    /// được định nghĩa trong Assets/GameConfig/roadNCXHCfg.json.
    /// Hỗ trợ cả 2D Canvas UI (Map Canvass / roadTop & roadBottom), 2D World và 3D Ground.
    /// </summary>
    [ExecuteInEditMode]
    public class RoadConfigGizmoViewer : MonoBehaviour
    {
        [System.Serializable]
        public class RoadInfo
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
        }

        public enum DisplayPlane
        {
            Canvas_2D_UI, // 2D Canvas UI (Khớp chuẩn Map Canvass / Top & Bottom)
            XY_2D_World,  // Mặt phẳng X-Y 2D Thế Giới (Scale 1:1)
            XZ_3D_Ground  // Mặt phẳng X-Z 3D Sàn Gameplay
        }

        [Header("--- LỰA CHỌN ĐƯỜNG ĐI ---")]
        [Tooltip("ID loại đường cần xem (1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 12, 13, 21, 22, 31, 32)")]
        public int selectedRoadId = 1;

        [Header("--- THIẾT LẬP MẶT PHẲNG & CHẾ ĐỘ 2D/3D ---")]
        [Tooltip("Chọn chế độ hiển thị: Canvas_2D_UI (chuẩn Map Canvass), XY_2D_World, hoặc XZ_3D_Ground")]
        public DisplayPlane planeMode = DisplayPlane.Canvas_2D_UI;
        [Tooltip("Đảo ngược trục Z/Y: Mặc định BẬT vì file 2D gốc có Y dương hướng xuống (top-down), trong Unity trục +Y/+Z hướng lên trên màn hình.")]
        public bool invertZ = true;
        [Tooltip("Đảo ngược trục X: Lật gương trái/phải nếu cần")]
        public bool invertX = false;
        [Tooltip("Cao độ nâng lên để không bị chìm/z-fighting")]
        public float heightOffset = 0.05f;
        [Tooltip("Dịch toạ độ gốc toàn bộ đường đi")]
        public Vector3 worldOffset = Vector3.zero;

        [Header("--- CÀI ĐẶT 2D CANVAS UI (MAP CANVASS) ---")]
        [Tooltip("Canvas UI chứa đường 2D (Tự động tìm Map Canvass nếu để trống)")]
        public Canvas targetCanvas;
        [Tooltip("Tỷ lệ scale từ toạ độ Config sang Canvas pixels. Chuẩn game Cocos là 125")]
        public Vector2 canvasScale = new Vector2(125f, 125f);
        [Tooltip("Độ lệch tâm trên Canvas (pixels). Đường nối Top & Bottom là (0, -180)")]
        public Vector2 canvasOffset = new Vector2(0f, -180f);
        [Tooltip("Toạ độ ngã rẽ Top-Bottom trong config (mặc định 1.91)")]
        public float configJunctionY = 1.91f;

        [Header("--- HIỂN THỊ TRÊN GAME VIEW (LINE RENDERER) ---")]
        [Tooltip("Tự động vẽ vạch đường hiển thị trực tiếp trên cả màn hình Game View")]
        public bool showInGameView = true;
        public float gameViewLineWidth = 0.06f;

        [Header("--- HIỂN THỊ ĐƯỜNG & ĐIỂM (GIZMOS TRÊN SCENE) ---")]
        public bool showLoopPath = true;
        public bool showExitPath = true;
        public bool showCavePath = true;
        public bool showWaypoints = true;
        public float waypointRadius = 0.18f;
        public bool showDirectionArrows = true;
        public bool showLabels = true;
        public bool showCoordinates = false;

        [Header("--- SO SÁNH NHIỀU ĐƯỜNG ---")]
        [Tooltip("Vẽ mờ tất cả 16 đường để đối chiếu hình dạng")]
        public bool showAllRoads = false;

        [Header("--- MÔ PHỎNG XE CHẠY THỬ (SIMULATION) ---")]
        public bool enableSimulation = false;
        [Range(0f, 1f)]
        public float simProgress = 0f;
        public bool simExitBranch = false;
        public float simSpeedMultiplier = 1f;

        [Header("--- MÀU SẮC GIZMOS ---")]
        public Color loopColor = new Color(0.15f, 0.85f, 1.0f, 0.95f);      // Cyan
        public Color exitColor = new Color(0.2f, 1.0f, 0.35f, 0.95f);       // Lime Green
        public Color caveColor = new Color(1.0f, 0.45f, 0.05f, 0.95f);      // Bright Orange
        public Color startColor = new Color(1.0f, 0.9f, 0.1f, 1.0f);        // Yellow
        public Color junctionColor = new Color(1.0f, 0.2f, 0.6f, 1.0f);     // Magenta / Pink

        // Dữ liệu bộ nhớ cache
        [HideInInspector]
        public Dictionary<int, RoadInfo> roadCache = new Dictionary<int, RoadInfo>();
        [HideInInspector]
        public List<int> availableRoadIds = new List<int>();

        private float lastSimTime = 0f;
        private LineRenderer loopLineRenderer;
        private LineRenderer exitLineRenderer;

        private void OnEnable()
        {
            ReloadRoadConfigs();
        }

        private void Update()
        {
            if (enableSimulation && Application.isEditor)
            {
                float dt = Time.realtimeSinceStartup - lastSimTime;
                if (dt > 0.1f) dt = 0.016f;
                lastSimTime = Time.realtimeSinceStartup;

                var road = GetSelectedRoad();
                if (road != null)
                {
                    float pathLen = GetPathLength(simExitBranch ? road.exitWaypoints : road.loopWaypoints);
                    if (pathLen > 0.001f)
                    {
                        float spd = (road.speed > 0 ? road.speed : 5f) * simSpeedMultiplier;
                        simProgress = (simProgress + (spd * dt / pathLen)) % 1f;
                    }
                }
            }
            else
            {
                lastSimTime = Time.realtimeSinceStartup;
            }
        }

        public RoadInfo GetSelectedRoad()
        {
            if (roadCache == null || roadCache.Count == 0)
            {
                ReloadRoadConfigs();
            }

            if (roadCache != null && roadCache.TryGetValue(selectedRoadId, out var road))
            {
                return road;
            }
            return null;
        }

        public void ReloadRoadConfigs()
        {
            roadCache.Clear();
            availableRoadIds.Clear();

            string roadPath = Path.Combine(Application.dataPath, "GameConfig", "roadNCXHCfg.json");
            string levelPath = Path.Combine(Application.dataPath, "GameConfig", "levelNCXHCfg.json");

            if (!File.Exists(roadPath))
            {
                Debug.LogWarning($"[RoadConfigGizmoViewer] Không tìm thấy file tại: {roadPath}");
                return;
            }

            try
            {
                string roadJson = File.ReadAllText(roadPath);
                ParseRoadConfigs(roadJson);

                if (File.Exists(levelPath))
                {
                    string levelJson = File.ReadAllText(levelPath);
                    ParseLevelRoadMapping(levelJson);
                }

                availableRoadIds.Sort();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RoadConfigGizmoViewer] Lỗi đọc roadNCXHCfg.json: {ex.Message}");
            }
        }

        private void ParseRoadConfigs(string json)
        {
            var matches = Regex.Matches(json, @"""(\d+)""\s*:\s*\{([\s\S]*?)(?=\n\s*""\d+""\s*:|\n\})");
            foreach (Match m in matches)
            {
                int rId = int.Parse(m.Groups[1].Value);
                string block = m.Groups[2].Value;

                var info = new RoadInfo();
                info.id = rId;

                var loopMatch = Regex.Match(block, @"""loop_path_root""\s*:\s*""([^""]*)""");
                if (loopMatch.Success) info.loopPathRaw = loopMatch.Groups[1].Value;

                var exitMatch = Regex.Match(block, @"""exit_path_root""\s*:\s*""([^""]*)""");
                if (exitMatch.Success) info.exitPathRaw = exitMatch.Groups[1].Value;

                var caveMatch = Regex.Match(block, @"""cave_path""\s*:\s*""([^""]*)""");
                if (caveMatch.Success) info.cavePathRaw = caveMatch.Groups[1].Value;

                var spdMatch = Regex.Match(block, @"""speed""\s*:\s*([\d.]+)");
                if (spdMatch.Success) float.TryParse(spdMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out info.speed);

                var maxSpdMatch = Regex.Match(block, @"""max_speed""\s*:\s*([\d.]+)");
                if (maxSpdMatch.Success) float.TryParse(maxSpdMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out info.maxSpeed);

                var accSpdMatch = Regex.Match(block, @"""acc_speed""\s*:\s*([\d.]+)");
                if (accSpdMatch.Success) float.TryParse(accSpdMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out info.accSpeed);

                if (!string.IsNullOrEmpty(info.cavePathRaw))
                {
                    string[] cparts = info.cavePathRaw.Split(',');
                    if (cparts.Length >= 2)
                    {
                        int.TryParse(cparts[0].Trim(), out info.caveStartIdx);
                        int.TryParse(cparts[1].Trim(), out info.caveEndIdx);
                    }
                }

                info.loopWaypoints = ParseCoordinates(info.loopPathRaw);
                info.exitWaypoints = ParseCoordinates(info.exitPathRaw);

                roadCache[rId] = info;
                availableRoadIds.Add(rId);
            }
        }

        private void ParseLevelRoadMapping(string json)
        {
            var matches = Regex.Matches(json, @"""(\d+)""\s*:\s*\{[\s\S]*?""road""\s*:\s*(\d+)");
            foreach (Match m in matches)
            {
                int lvlId = int.Parse(m.Groups[1].Value);
                int rId = int.Parse(m.Groups[2].Value);

                if (roadCache.TryGetValue(rId, out var rInfo))
                {
                    if (!rInfo.levelsUsing.Contains(lvlId))
                    {
                        rInfo.levelsUsing.Add(lvlId);
                    }
                }
            }
        }

        public List<Vector3> ParseCoordinates(string raw)
        {
            var list = new List<Vector3>();
            if (string.IsNullOrEmpty(raw)) return list;

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

                        list.Add(TransformPoint(x, yOrZ, elev));
                    }
                }
            }
            return list;
        }

        public Vector3 TransformPoint(float x, float yOrZ, float elevation)
        {
            float calcX = invertX ? -x : x;
            float calcZ = invertZ ? -yOrZ : yOrZ;

            if (planeMode == DisplayPlane.Canvas_2D_UI)
            {
                // Toạ độ điểm trong không gian Pixel của Canvas:
                float pixelX = calcX * canvasScale.x + canvasOffset.x;
                float pixelY = (calcZ + (invertZ ? configJunctionY : -configJunctionY)) * canvasScale.y + canvasOffset.y;

                Vector3 localCanvasPos = new Vector3(pixelX, pixelY, elevation);

                var canvas = targetCanvas;
                if (canvas == null)
                {
                    var cObj = GameObject.Find("Map Canvass") ?? GameObject.Find("Background Canvas (Road)");
                    if (cObj != null) canvas = cObj.GetComponent<Canvas>();
                    if (canvas == null) canvas = GetComponentInParent<Canvas>() ?? FindAnyObjectByType<Canvas>();
                }

                if (canvas != null)
                {
                    return canvas.transform.TransformPoint(localCanvasPos) + worldOffset;
                }
                return transform.position + worldOffset + localCanvasPos;
            }
            else if (planeMode == DisplayPlane.XY_2D_World)
            {
                Vector3 worldPos = new Vector3(calcX, calcZ, elevation + heightOffset);
                return transform.position + worldOffset + worldPos;
            }
            else // XZ_3D_Ground
            {
                Vector3 worldPos = new Vector3(calcX, elevation + heightOffset, calcZ);
                return transform.position + worldOffset + worldPos;
            }
        }

        private void OnValidate()
        {
            ReloadRoadConfigs();
        }

        private float GetPathLength(List<Vector3> pts)
        {
            if (pts == null || pts.Count < 2) return 0f;
            float total = 0f;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                total += Vector3.Distance(pts[i], pts[i + 1]);
            }
            return total;
        }

        public Vector3 EvaluatePathPosition(List<Vector3> pts, float t, out Vector3 forwardDir)
        {
            forwardDir = Vector3.forward;
            if (pts == null || pts.Count == 0) return transform.position;
            if (pts.Count == 1) return pts[0];

            float totalLen = GetPathLength(pts);
            float targetDist = Mathf.Clamp01(t) * totalLen;

            float curDist = 0f;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                float segmentLen = Vector3.Distance(pts[i], pts[i + 1]);
                if (curDist + segmentLen >= targetDist || i == pts.Count - 2)
                {
                    float segT = segmentLen > 0.0001f ? (targetDist - curDist) / segmentLen : 0f;
                    Vector3 pos = Vector3.Lerp(pts[i], pts[i + 1], segT);
                    forwardDir = (pts[i + 1] - pts[i]).normalized;
                    return pos;
                }
                curDist += segmentLen;
            }
            return pts[pts.Count - 1];
        }

        private void OnDrawGizmos()
        {
            if (roadCache == null || roadCache.Count == 0)
            {
                ReloadRoadConfigs();
            }

            if (showAllRoads)
            {
                foreach (var kvp in roadCache)
                {
                    if (kvp.Key == selectedRoadId) continue;
                    DrawRoadGizmos(kvp.Value, false);
                }
            }

            var road = GetSelectedRoad();
            if (road != null)
            {
                DrawRoadGizmos(road, true);

                if (enableSimulation)
                {
                    DrawSimulatedCar(road);
                }

                // Cập nhật LineRenderer để nhìn thấy trên Game View
                UpdateGameViewLines(road);
            }
        }

        private void DrawRoadGizmos(RoadInfo road, bool isSelected)
        {
            if (showLoopPath && road.loopWaypoints != null && road.loopWaypoints.Count > 1)
            {
                Color mainColor = isSelected ? loopColor : new Color(loopColor.r, loopColor.g, loopColor.b, 0.18f);

                for (int i = 0; i < road.loopWaypoints.Count; i++)
                {
                    Vector3 pCurr = road.loopWaypoints[i];
                    Vector3 pNext = road.loopWaypoints[(i + 1) % road.loopWaypoints.Count];

                    bool isCave = isSelected && showCavePath && road.caveStartIdx >= 0 && road.caveEndIdx >= 0 &&
                                  i >= road.caveStartIdx && i < road.caveEndIdx;

                    Gizmos.color = isCave ? caveColor : mainColor;
                    Gizmos.DrawLine(pCurr, pNext);

                    if (isSelected && showWaypoints)
                    {
                        if (i == 0)
                        {
                            Gizmos.color = startColor;
                            Gizmos.DrawSphere(pCurr, waypointRadius * 1.35f);
                        }
                        else if (isCave)
                        {
                            Gizmos.color = caveColor;
                            Gizmos.DrawCube(pCurr, Vector3.one * (waypointRadius * 1.5f));
                        }
                        else
                        {
                            Gizmos.color = mainColor;
                            Gizmos.DrawSphere(pCurr, waypointRadius);
                        }

                        if (showDirectionArrows && Vector3.Distance(pCurr, pNext) > 0.2f)
                        {
                            Vector3 mid = (pCurr + pNext) * 0.5f;
                            Vector3 dir = (pNext - pCurr).normalized;
                            DrawArrow(mid, dir, 0.35f, isCave ? caveColor : mainColor);
                        }
                    }
                }
            }

            if (showExitPath && road.exitWaypoints != null && road.exitWaypoints.Count > 0)
            {
                Color exColor = isSelected ? exitColor : new Color(exitColor.r, exitColor.g, exitColor.b, 0.18f);

                for (int i = 0; i < road.exitWaypoints.Count; i++)
                {
                    Vector3 pCurr = road.exitWaypoints[i];

                    if (i < road.exitWaypoints.Count - 1)
                    {
                        Vector3 pNext = road.exitWaypoints[i + 1];
                        Gizmos.color = exColor;
                        Gizmos.DrawLine(pCurr, pNext);

                        if (isSelected && showDirectionArrows)
                        {
                            Vector3 mid = (pCurr + pNext) * 0.5f;
                            Vector3 dir = (pNext - pCurr).normalized;
                            DrawArrow(mid, dir, 0.45f, exColor);
                        }
                    }

                    if (isSelected && showWaypoints)
                    {
                        if (i == 0)
                        {
                            Gizmos.color = junctionColor;
                            Gizmos.DrawWireSphere(pCurr, waypointRadius * 1.8f);
                            Gizmos.DrawSphere(pCurr, waypointRadius * 0.9f);
                        }
                        else
                        {
                            Gizmos.color = exColor;
                            Gizmos.DrawCube(pCurr, Vector3.one * (waypointRadius * 1.4f));
                        }
                    }
                }
            }
        }

        private void DrawSimulatedCar(RoadInfo road)
        {
            List<Vector3> pts = simExitBranch ? road.exitWaypoints : road.loopWaypoints;
            if (pts == null || pts.Count < 2) return;

            Vector3 carPos = EvaluatePathPosition(pts, simProgress, out Vector3 fwd);

            Gizmos.color = simExitBranch ? Color.green : new Color(1f, 0.8f, 0.2f, 0.95f);
            Vector3 carSize = (planeMode == DisplayPlane.XZ_3D_Ground) 
                ? new Vector3(1.2f, 0.7f, 1.8f) 
                : new Vector3(0.6f, 0.9f, 0.2f);

            Matrix4x4 oldMat = Gizmos.matrix;
            Vector3 upDir = (planeMode == DisplayPlane.XZ_3D_Ground) ? Vector3.up : Vector3.back;
            Quaternion rot = fwd.sqrMagnitude > 0.001f ? Quaternion.LookRotation(fwd, upDir) : Quaternion.identity;
            Gizmos.matrix = Matrix4x4.TRS(carPos, rot, Vector3.one);

            Gizmos.DrawCube(Vector3.zero, carSize);
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(Vector3.zero, carSize);

            Gizmos.matrix = oldMat;
        }

        private void DrawArrow(Vector3 pos, Vector3 direction, float arrowLength, Color col)
        {
            if (direction.sqrMagnitude < 0.001f) return;
            Gizmos.color = col;
            Vector3 right = (planeMode == DisplayPlane.XZ_3D_Ground) 
                ? Vector3.Cross(Vector3.up, direction).normalized * (arrowLength * 0.35f)
                : Vector3.Cross(Vector3.forward, direction).normalized * (arrowLength * 0.35f);

            Vector3 arrowTip = pos + direction * (arrowLength * 0.5f);
            Vector3 arrowBase = pos - direction * (arrowLength * 0.5f);
            Vector3 wing1 = arrowTip - direction * (arrowLength * 0.6f) + right;
            Vector3 wing2 = arrowTip - direction * (arrowLength * 0.6f) - right;

            Gizmos.DrawLine(arrowBase, arrowTip);
            Gizmos.DrawLine(arrowTip, wing1);
            Gizmos.DrawLine(arrowTip, wing2);
        }

        public void UpdateGameViewLines(RoadInfo road)
        {
            if (!showInGameView || road == null)
            {
                if (loopLineRenderer != null) loopLineRenderer.enabled = false;
                if (exitLineRenderer != null) exitLineRenderer.enabled = false;
                return;
            }

            if (loopLineRenderer == null)
            {
                var loopObj = transform.Find("[GameView_LoopLine]");
                if (loopObj == null)
                {
                    loopObj = new GameObject("[GameView_LoopLine]").transform;
                    loopObj.SetParent(transform, false);
                }
                loopLineRenderer = loopObj.GetComponent<LineRenderer>();
                if (loopLineRenderer == null) loopLineRenderer = loopObj.gameObject.AddComponent<LineRenderer>();
                SetupLineRenderer(loopLineRenderer, loopColor, true);
            }

            if (exitLineRenderer == null)
            {
                var exitObj = transform.Find("[GameView_ExitLine]");
                if (exitObj == null)
                {
                    exitObj = new GameObject("[GameView_ExitLine]").transform;
                    exitObj.SetParent(transform, false);
                }
                exitLineRenderer = exitObj.GetComponent<LineRenderer>();
                if (exitLineRenderer == null) exitLineRenderer = exitObj.gameObject.AddComponent<LineRenderer>();
                SetupLineRenderer(exitLineRenderer, exitColor, false);
            }

            if (showLoopPath && road.loopWaypoints != null && road.loopWaypoints.Count > 1)
            {
                loopLineRenderer.enabled = true;
                loopLineRenderer.positionCount = road.loopWaypoints.Count + 1;
                for (int i = 0; i < road.loopWaypoints.Count; i++)
                {
                    loopLineRenderer.SetPosition(i, road.loopWaypoints[i]);
                }
                loopLineRenderer.SetPosition(road.loopWaypoints.Count, road.loopWaypoints[0]);
            }
            else
            {
                if (loopLineRenderer != null) loopLineRenderer.enabled = false;
            }

            if (showExitPath && road.exitWaypoints != null && road.exitWaypoints.Count > 0)
            {
                exitLineRenderer.enabled = true;
                exitLineRenderer.positionCount = road.exitWaypoints.Count;
                for (int i = 0; i < road.exitWaypoints.Count; i++)
                {
                    exitLineRenderer.SetPosition(i, road.exitWaypoints[i]);
                }
            }
            else
            {
                if (exitLineRenderer != null) exitLineRenderer.enabled = false;
            }
        }

        private void SetupLineRenderer(LineRenderer lr, Color col, bool loop)
        {
            lr.useWorldSpace = true;
            lr.startWidth = gameViewLineWidth;
            lr.endWidth = gameViewLineWidth;
            lr.startColor = col;
            lr.endColor = col;
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Unlit/Color");
            if (shader != null) lr.material = new Material(shader);
            lr.loop = loop;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!showLabels) return;
            var road = GetSelectedRoad();
            if (road == null) return;

            GUIStyle labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.white;
            labelStyle.fontSize = 11;
            labelStyle.fontStyle = FontStyle.Bold;
            labelStyle.alignment = TextAnchor.MiddleCenter;

            Texture2D bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0.08f, 0.12f, 0.18f, 0.85f));
            bgTex.Apply();
            labelStyle.normal.background = bgTex;
            labelStyle.padding = new RectOffset(4, 4, 2, 2);

            Vector3 textOffset = (planeMode == DisplayPlane.XZ_3D_Ground) 
                ? new Vector3(0f, 0.35f, 0f) 
                : new Vector3(0f, 0.35f, -0.1f);

            if (showLoopPath && road.loopWaypoints != null)
            {
                for (int i = 0; i < road.loopWaypoints.Count; i++)
                {
                    Vector3 pos = road.loopWaypoints[i] + textOffset;
                    string txt = $"WP #{i}";
                    if (showCoordinates)
                    {
                        txt += $"\n({road.loopWaypoints[i].x:F1}, {road.loopWaypoints[i].y:F1})";
                    }

                    if (i == 0) txt = $"★ START (WP 0)";
                    if (road.caveStartIdx >= 0 && i == road.caveStartIdx) txt += " [HẦM VÀO]";
                    if (road.caveEndIdx >= 0 && i == road.caveEndIdx) txt += " [HẦM RA]";

                    Handles.Label(pos, txt, labelStyle);
                }
            }

            if (showExitPath && road.exitWaypoints != null)
            {
                for (int i = 0; i < road.exitWaypoints.Count; i++)
                {
                    Vector3 pos = road.exitWaypoints[i] + textOffset;
                    string txt = (i == 0) ? $"★ EXIT JUNCTION" : $"EXIT #{i}";
                    if (showCoordinates)
                    {
                        txt += $"\n({road.exitWaypoints[i].x:F1}, {road.exitWaypoints[i].y:F1})";
                    }
                    Handles.Label(pos, txt, labelStyle);
                }
            }
        }
#endif
    }
}
