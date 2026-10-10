using UnityEngine;
using UnityEditor;
using WhoGetInBus.GamePlay;

namespace WhoGetInBus.EditorTools
{
    [CustomEditor(typeof(RoadConfigGizmoViewer))]
    public class RoadConfigGizmoViewerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var viewer = (RoadConfigGizmoViewer)target;

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical("box");
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.2f, 0.85f, 1f) }
            };
            GUILayout.Label("🛣️ ROAD CONFIG GIZMO VIEWER", headerStyle);
            GUILayout.Label($"Xem toạ độ đường đi từ Assets/GameConfig/roadNCXHCfg.json", EditorStyles.centeredGreyMiniLabel);

            if (GUILayout.Button("🚀 MỞ CỬA SỔ XEM ĐƯỜNG ĐI (ROAD VIEWER WINDOW)", GUILayout.Height(36)))
            {
                RoadConfigGizmoViewerWindow.OpenWindow();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            EditorGUI.BeginChangeCheck();

            DrawDefaultInspector();

            if (EditorGUI.EndChangeCheck())
            {
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔄 Nạp Lại Config", GUILayout.Height(28)))
            {
                viewer.ReloadRoadConfigs();
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("🎯 Focus Camera Vào Đường", GUILayout.Height(28)))
            {
                var road = viewer.GetSelectedRoad();
                if (road != null && road.loopWaypoints != null && road.loopWaypoints.Count > 0)
                {
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
                }
            }
            EditorGUILayout.EndHorizontal();

            // Hiển thị thông tin nhanh
            var curRoad = viewer.GetSelectedRoad();
            if (curRoad != null)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox(
                    $"Road #{curRoad.id}: {curRoad.loopWaypoints.Count} điểm Loop, {curRoad.exitWaypoints.Count} điểm Exit.\n" +
                    $"Tốc độ: {curRoad.speed} (max: {curRoad.maxSpeed}).\n" +
                    $"Được sử dụng trong {curRoad.levelsUsing.Count} levels.", MessageType.Info);
            }
        }
    }
}
