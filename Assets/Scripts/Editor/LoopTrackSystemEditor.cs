using UnityEngine;
using UnityEditor;
using WhoGetInBus.GamePlay;
using WhoGetInBus.Data;

namespace WhoGetInBus.Editor
{
    [CustomEditor(typeof(LoopTrackSystem))]
    public class LoopTrackSystemEditor : UnityEditor.Editor
    {
        private bool showDefaultInspector = false;

        public override void OnInspectorGUI()
        {
            LoopTrackSystem mgr = (LoopTrackSystem)target;

            serializedObject.Update();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("🛣️ LOOP TRACK SYSTEM (ARC-LENGTH SPLINE)", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            // 0. SCRIPTABLEOBJECT CONFIG DATA (LƯU VĨNH VIỄN)
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📁 File Cấu Hình Dữ Liệu (ScriptableObject Config)", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            RoadTrackConfigData newConfig = (RoadTrackConfigData)EditorGUILayout.ObjectField("Config Asset", mgr.trackConfig, typeof(RoadTrackConfigData), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(mgr, "Change RoadTrack Config");
                mgr.trackConfig = newConfig;
                if (mgr.trackConfig != null)
                {
                    mgr.SyncFromConfig();
                }
                EditorUtility.SetDirty(mgr);
            }

            if (mgr.trackConfig == null)
            {
                EditorGUILayout.HelpBox("⚠️ Chưa liên kết file RoadTrackConfigData.asset! Nhấn nút dưới để tự động tạo/kết nối.", MessageType.Warning);
                if (GUILayout.Button("⚡ Tự Động Kết Nối / Tạo RoadTrackConfigData.asset", GUILayout.Height(26)))
                {
                    mgr.EnsureConfigLoaded();
                    EditorUtility.SetDirty(mgr);
                    SceneView.RepaintAll();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("✅ Đang liên kết RoadTrackConfigData.asset. Mọi thông số co giãn & căn chỉnh được lưu vĩnh viễn vào file data, KHÔNG bao giờ bị mất hoặc reset khi sửa code C#!", MessageType.Info);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // 1. THANH CHUYỂN ROAD NHANH (VỚI MŨI TÊN ◀ VÀ ▶)
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🎮 Chọn Tuyến Đường (Road Config)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("◀", GUILayout.Width(30), GUILayout.Height(22)))
            {
                mgr.currentRoadId = Mathf.Max(1, mgr.currentRoadId - 1);
                mgr.LoadRoadFromConfig(mgr.currentRoadId);
                EditorUtility.SetDirty(mgr);
                SceneView.RepaintAll();
            }

            int newRoad = EditorGUILayout.IntField("Road ID", mgr.currentRoadId);
            if (newRoad != mgr.currentRoadId)
            {
                mgr.currentRoadId = Mathf.Clamp(newRoad, 1, 16);
                mgr.LoadRoadFromConfig(mgr.currentRoadId);
                EditorUtility.SetDirty(mgr);
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("▶", GUILayout.Width(30), GUILayout.Height(22)))
            {
                mgr.currentRoadId = Mathf.Min(16, mgr.currentRoadId + 1);
                mgr.LoadRoadFromConfig(mgr.currentRoadId);
                EditorUtility.SetDirty(mgr);
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("🔄 Tải Lại Toạ Độ Road Này (Reload Road)", GUILayout.Height(24)))
            {
                mgr.LoadRoadFromConfig(mgr.currentRoadId);
                EditorUtility.SetDirty(mgr);
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // 2. HIỆU CHỈNH TỶ LỆ CO GIÃN & CĂN CHỈNH ĐƯỜNG (SLIDERS REALTIME)
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("⚙️ Căn Chỉnh Tỷ Lệ Đường Đua (Real-Time Sliders)", EditorStyles.boldLabel);

            // Bật/tắt co giãn
            EditorGUI.BeginChangeCheck();
            bool newEnableScale = EditorGUILayout.Toggle(new GUIContent("Bật Co Giãn (Enable Track Scale)", "Bật/tắt tự động co giãn toạ độ khớp mặt đường nhựa Unity"), mgr.enableTrackScale);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(mgr, "Toggle Enable Track Scale");
                mgr.enableTrackScale = newEnableScale;
                mgr.SyncToConfig();
                mgr.RebuildWaypointsFromCurrentConfig();
                EditorUtility.SetDirty(mgr);
                if (mgr.trackConfig != null)
                {
                    EditorUtility.SetDirty(mgr.trackConfig);
                    AssetDatabase.SaveAssets();
                }
                SceneView.RepaintAll();
            }

            if (mgr.enableTrackScale)
            {
                // SLIDER SCALE X (CHIỀU RỘNG LÒNG ĐƯỜNG)
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                float newScaleX = EditorGUILayout.Slider(new GUIContent("Rộng Lòng Đường (Scale X)", "Co hẹp / mở rộng chiều ngang 2 làn đường xe chạy"), mgr.trackScale.x, 0.5f, 1.3f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(mgr, "Change Track Scale X");
                    mgr.trackScale = new Vector2(newScaleX, mgr.trackScale.y);
                    mgr.SyncToConfig();
                    mgr.RebuildWaypointsFromCurrentConfig();
                    EditorUtility.SetDirty(mgr);
                    if (mgr.trackConfig != null)
                    {
                        EditorUtility.SetDirty(mgr.trackConfig);
                        AssetDatabase.SaveAssets();
                    }
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("0.80", GUILayout.Width(38), GUILayout.Height(18)))
                {
                    ApplyScaleX(mgr, 0.80f);
                }
                if (GUILayout.Button("0.852", GUILayout.Width(38), GUILayout.Height(18)))
                {
                    ApplyScaleX(mgr, 0.852f);
                }
                if (GUILayout.Button("0.90", GUILayout.Width(38), GUILayout.Height(18)))
                {
                    ApplyScaleX(mgr, 0.90f);
                }
                if (GUILayout.Button("1.0", GUILayout.Width(32), GUILayout.Height(18)))
                {
                    ApplyScaleX(mgr, 1.0f);
                }
                EditorGUILayout.EndHorizontal();

                // SLIDER SCALE Z (CHIỀU DÀI VÒNG ĐUA)
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                float newScaleZ = EditorGUILayout.Slider(new GUIContent("Dài Vòng Đua (Scale Z)", "Thu ngắn vòm chữ U trên đỉnh & đáy"), mgr.trackScale.y, 0.5f, 1.3f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(mgr, "Change Track Scale Z");
                    mgr.trackScale = new Vector2(mgr.trackScale.x, newScaleZ);
                    mgr.SyncToConfig();
                    mgr.RebuildWaypointsFromCurrentConfig();
                    EditorUtility.SetDirty(mgr);
                    if (mgr.trackConfig != null)
                    {
                        EditorUtility.SetDirty(mgr.trackConfig);
                        AssetDatabase.SaveAssets();
                    }
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("0.767", GUILayout.Width(38), GUILayout.Height(18)))
                {
                    ApplyScaleZ(mgr, 0.767f);
                }
                if (GUILayout.Button("0.80", GUILayout.Width(38), GUILayout.Height(18)))
                {
                    ApplyScaleZ(mgr, 0.80f);
                }
                if (GUILayout.Button("1.0", GUILayout.Width(32), GUILayout.Height(18)))
                {
                    ApplyScaleZ(mgr, 1.0f);
                }
                EditorGUILayout.EndHorizontal();

                // SLIDER OFFSET Z (DỊCH LÊN/XUỐNG TIM ĐƯỜNG)
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                float newOffsetZ = EditorGUILayout.Slider(new GUIContent("Dịch Tim Dọc (Offset Z)", "Dịch toàn bộ đường lên trên (+) hoặc xuống dưới (-)"), mgr.trackOffsetZ, -2.0f, 2.0f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(mgr, "Change Track Offset Z");
                    mgr.trackOffsetZ = newOffsetZ;
                    mgr.SyncToConfig();
                    mgr.RebuildWaypointsFromCurrentConfig();
                    EditorUtility.SetDirty(mgr);
                    if (mgr.trackConfig != null)
                    {
                        EditorUtility.SetDirty(mgr.trackConfig);
                        AssetDatabase.SaveAssets();
                    }
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("-0.018", GUILayout.Width(45), GUILayout.Height(18)))
                {
                    ApplyOffsetZ(mgr, -0.018f);
                }
                if (GUILayout.Button("0", GUILayout.Width(25), GUILayout.Height(18)))
                {
                    ApplyOffsetZ(mgr, 0f);
                }
                EditorGUILayout.EndHorizontal();

                // SLIDER OFFSET X (DỊCH TRÁI/PHẢI)
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                float newOffsetX = EditorGUILayout.Slider(new GUIContent("Dịch Tim Ngang (Offset X)", "Dịch toàn bộ đường sang trái (-) hoặc sang phải (+)"), mgr.trackOffsetX, -1.5f, 1.5f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(mgr, "Change Track Offset X");
                    mgr.trackOffsetX = newOffsetX;
                    mgr.SyncToConfig();
                    mgr.RebuildWaypointsFromCurrentConfig();
                    EditorUtility.SetDirty(mgr);
                    if (mgr.trackConfig != null)
                    {
                        EditorUtility.SetDirty(mgr.trackConfig);
                        AssetDatabase.SaveAssets();
                    }
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("0", GUILayout.Width(25), GUILayout.Height(18)))
                {
                    ApplyOffsetX(mgr, 0f);
                }
                EditorGUILayout.EndHorizontal();

                // SLIDER CAO ĐỘ HEIGHT OFFSET
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                float newHeight = EditorGUILayout.Slider(new GUIContent("Cao Độ (Height Offset)", "Nâng cao độ waypoint trục Y tránh z-fighting"), mgr.trackHeightOffset, -0.5f, 0.5f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(mgr, "Change Track Height Offset");
                    mgr.trackHeightOffset = newHeight;
                    mgr.SyncToConfig();
                    mgr.RebuildWaypointsFromCurrentConfig();
                    EditorUtility.SetDirty(mgr);
                    if (mgr.trackConfig != null)
                    {
                        EditorUtility.SetDirty(mgr.trackConfig);
                        AssetDatabase.SaveAssets();
                    }
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("0", GUILayout.Width(25), GUILayout.Height(18)))
                {
                    ApplyHeightOffset(mgr, 0f);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // 3. CÁC NÚT ĐẶT LẠI MẪU NHANH (PRESETS)
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("⚡ Thiết Lập Nhanh (Presets)", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🎯 Chuẩn Vừa Khít (0.852, 0.767, -0.018)", GUILayout.Height(26)))
            {
                if (mgr.trackConfig != null) mgr.trackConfig.ResetToFitRoadDefault();
                mgr.SyncFromConfig();
                EditorUtility.SetDirty(mgr);
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("📐 Rộng Hơn (0.90)", GUILayout.Height(26)))
            {
                if (mgr.trackConfig != null) mgr.trackConfig.SetWiderPreset();
                mgr.SyncFromConfig();
                EditorUtility.SetDirty(mgr);
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📏 Hẹp Hơn (0.80)", GUILayout.Height(26)))
            {
                if (mgr.trackConfig != null) mgr.trackConfig.SetNarrowPreset();
                mgr.SyncFromConfig();
                EditorUtility.SetDirty(mgr);
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("🌐 Toạ Độ Gốc Cocos (1.0, 1.0, 0)", GUILayout.Height(26)))
            {
                if (mgr.trackConfig != null) mgr.trackConfig.ResetToRawCocos();
                mgr.SyncFromConfig();
                EditorUtility.SetDirty(mgr);
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // 4. THÔNG TIN TUYẾN ĐƯỜNG HIỆN TẠI (DIAGNOSTICS)
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"📊 Thống Kê Tuyến Đường: Road #{mgr.currentRoadId}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"• Điểm Loop: {mgr.loopWaypoints.Count} điểm (Tổng dài: {mgr.loopTotalLen:F2}m)");
            EditorGUILayout.LabelField($"• Điểm Exit: {mgr.exitWaypoints.Count} điểm (Tổng dài: {mgr.exitTotalLen:F2}m)");
            EditorGUILayout.LabelField($"• Ngã rẽ Exit tại: {mgr.exitJunctionDistance:F2}m");
            EditorGUILayout.LabelField($"• Nhập làn Bãi xe tại: {mgr.parkingEntryDistance:F2}m");
            EditorGUILayout.LabelField($"• Số xe đang hoạt động: {mgr.activeCarsOnTrack.Count} xe");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // DEFAULT INSPECTOR COLLAPSIBLE
            showDefaultInspector = EditorGUILayout.Foldout(showDefaultInspector, "Xem Toàn Bộ Thuộc Tính Gốc (Default Fields)");
            if (showDefaultInspector)
            {
                DrawDefaultInspector();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void ApplyScaleX(LoopTrackSystem mgr, float val)
        {
            Undo.RecordObject(mgr, "Change Track Scale X");
            mgr.trackScale = new Vector2(val, mgr.trackScale.y);
            mgr.SyncToConfig();
            mgr.RebuildWaypointsFromCurrentConfig();
            EditorUtility.SetDirty(mgr);
            if (mgr.trackConfig != null)
            {
                EditorUtility.SetDirty(mgr.trackConfig);
                AssetDatabase.SaveAssets();
            }
            SceneView.RepaintAll();
        }

        private void ApplyScaleZ(LoopTrackSystem mgr, float val)
        {
            Undo.RecordObject(mgr, "Change Track Scale Z");
            mgr.trackScale = new Vector2(mgr.trackScale.x, val);
            mgr.SyncToConfig();
            mgr.RebuildWaypointsFromCurrentConfig();
            EditorUtility.SetDirty(mgr);
            if (mgr.trackConfig != null)
            {
                EditorUtility.SetDirty(mgr.trackConfig);
                AssetDatabase.SaveAssets();
            }
            SceneView.RepaintAll();
        }

        private void ApplyOffsetZ(LoopTrackSystem mgr, float val)
        {
            Undo.RecordObject(mgr, "Change Track Offset Z");
            mgr.trackOffsetZ = val;
            mgr.SyncToConfig();
            mgr.RebuildWaypointsFromCurrentConfig();
            EditorUtility.SetDirty(mgr);
            if (mgr.trackConfig != null)
            {
                EditorUtility.SetDirty(mgr.trackConfig);
                AssetDatabase.SaveAssets();
            }
            SceneView.RepaintAll();
        }

        private void ApplyOffsetX(LoopTrackSystem mgr, float val)
        {
            Undo.RecordObject(mgr, "Change Track Offset X");
            mgr.trackOffsetX = val;
            mgr.SyncToConfig();
            mgr.RebuildWaypointsFromCurrentConfig();
            EditorUtility.SetDirty(mgr);
            if (mgr.trackConfig != null)
            {
                EditorUtility.SetDirty(mgr.trackConfig);
                AssetDatabase.SaveAssets();
            }
            SceneView.RepaintAll();
        }

        private void ApplyHeightOffset(LoopTrackSystem mgr, float val)
        {
            Undo.RecordObject(mgr, "Change Track Height Offset");
            mgr.trackHeightOffset = val;
            mgr.SyncToConfig();
            mgr.RebuildWaypointsFromCurrentConfig();
            EditorUtility.SetDirty(mgr);
            if (mgr.trackConfig != null)
            {
                EditorUtility.SetDirty(mgr.trackConfig);
                AssetDatabase.SaveAssets();
            }
            SceneView.RepaintAll();
        }
    }
}
