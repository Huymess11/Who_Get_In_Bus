using UnityEngine;
using UnityEditor;
using DouyinGame.GamePlay;

namespace DouyinGame.Editor
{
    [CustomEditor(typeof(SandBoardManager))]
    public class SandBoardManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            SandBoardManager mgr = (SandBoardManager)target;

            // Header Style
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("🏖️ SAND BOARD MANAGER 🏖️", titleStyle);
            EditorGUILayout.Space(6);

            // BẢNG NÚT BẤM XEM TRỰC TIẾP TRÊN SCENE
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🎮 XEM TRỰC TIẾP (KHÔNG CẦN CHẠY GAME):", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Nút 1: Cập nhật nhanh các hạt hiện có (Scale, Zigzag, Z=6)
            GUI.backgroundColor = new Color(0.2f, 0.85f, 1.0f);
            if (GUILayout.Button("⚡ CẬP NHẬT SCALE & LỆCH ZIGZAG (XEM NGAY)", GUILayout.Height(38)))
            {
                mgr.UpdateExistingBeads();
                SceneView.RepaintAll();
            }

            // Nút 2: Tái tạo mới toàn bộ tranh cát từ dữ liệu level
            GUI.backgroundColor = new Color(0.35f, 0.95f, 0.45f);
            if (GUILayout.Button("🔄 TÁI TẠO TOÀN BỘ TRANH CÁT (REBUILD)", GUILayout.Height(38)))
            {
                mgr.RebuildBoardInEditor();
                SceneView.RepaintAll();
            }

            // Nút 3 & Nút 4
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();

            // Nút 3: Đặt nhanh vị trí Z = 6
            GUI.backgroundColor = new Color(1.0f, 0.9f, 0.3f);
            if (GUILayout.Button("📍 ĐẶT VỊ TRÍ Z = 6.0", GUILayout.Height(28)))
            {
                mgr.boardPosZ = 6f;
                mgr.transform.position = new Vector3(mgr.transform.position.x, mgr.transform.position.y, 6f);
                EditorUtility.SetDirty(mgr.gameObject);
                SceneView.RepaintAll();
                Debug.Log("<color=yellow>[SandBoardManager]</color> Đã đặt vị trí SandBoard_Manager tại Z = 6.0!");
            }

            // Nút 4: Xóa tranh cát
            GUI.backgroundColor = new Color(1.0f, 0.45f, 0.45f);
            if (GUILayout.Button("🗑️ XÓA TRANH CÁT", GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog("Xác nhận xóa", "Bạn có chắc chắn muốn xóa toàn bộ hạt cát trên bảng không?", "Xóa", "Hủy"))
                {
                    mgr.ClearBoard();
                    EditorUtility.SetDirty(mgr.gameObject);
                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.EndHorizontal();

            // Nút 5: Mở Level Designer Pro (Tab 5)
            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(0.2f, 0.75f, 1.0f);
            if (GUILayout.Button("🛠️ MỞ LEVEL DESIGNER PRO (TAB 5: CAMERA & BÀN CÁT 3D)", GUILayout.Height(32)))
            {
                GameLevelDesign.Editor.GameLevelDesignerWindow.OpenWindowToTab(4);
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // VẼ CÁC TRƯỜNG THÔNG SỐ (INSPECTOR)
            DrawDefaultInspector();

            EditorGUILayout.Space(8);

            // THỐNG KÊ CHI TIẾT
            int childCount = mgr.transform.childCount;
            string zigzagStatus = mgr.enableZigzag ? $"BẬT (Offset: {mgr.zigzagOffset:F2})" : "TẮT";
            EditorGUILayout.HelpBox(
                $"📊 THÔNG TIN HIỆN TẠI:\n" +
                $"• Số lượng hạt con trong Hierarchy: {childCount}\n" +
                $"• Tọa độ Transform Position Z: {mgr.transform.position.z:F2} (Mục tiêu: {mgr.boardPosZ:F2})\n" +
                $"• Scale hạt cát (beadScale): {mgr.beadScale:F2}\n" +
                $"• Lệch zigzag: {zigzagStatus}\n" +
                $"👉 Bạn có thể kéo slider thông số ở trên và ấn nút '⚡ CẬP NHẬT SCALE & LỆCH ZIGZAG' để xem ngay trên Scene!",
                MessageType.Info);
        }
    }
}
