using UnityEngine;
using UnityEditor;
using WhoGetInBus.GamePlay;

namespace WhoGetInBus.Editor
{
    [CustomEditor(typeof(SandBoardManager))]
    public class SandBoardManagerEditor : UnityEditor.Editor
    {
        [MenuItem("Tools/Who Get In Bus/⚡ Đặt SandBoard Chuẩn Gốc Cocos (1:1)")]
        public static void ApplyCocosOriginalSettingsFromMenu()
        {
            var mgr = Object.FindFirstObjectByType<SandBoardManager>();
            if (mgr != null)
            {
                mgr.ApplyCocosOriginalSettings();
                EditorUtility.DisplayDialog("Thành công", 
                    "Đã đặt SandBoard_Manager đúng vị trí và thông số chuẩn gốc Cocos 1:1!\n\n" +
                    "• Position: (0, 0, 6.0)\n" +
                    "• Bead Spacing: 0.2586m (~0.26m)\n" +
                    "• Zigzag Offset: 0.0828m (~0.083m)\n" +
                    "• Bead Scale: 0.55\n" +
                    "• Grid: 40 x 40", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Thông báo", "Không tìm thấy GameObject chứa component SandBoardManager trong Scene hiện tại!", "OK");
            }
        }

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
            EditorGUILayout.LabelField("🏖️ SAND BOARD MANAGER (CHUẨN GỐC COCOS) 🏖️", titleStyle);
            EditorGUILayout.Space(6);

            // BẢNG NÚT BẤM THAO TÁC NHANH
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🎮 THAO TÁC 1-CLICK (CHUẨN GỐC 1:1):", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // NÚT CHÍNH: ĐẶT THÔNG SỐ & VỊ TRÍ CHUẨN GỐC COCOS 1:1
            GUI.backgroundColor = new Color(1.0f, 0.85f, 0.2f);
            if (GUILayout.Button("⚡ ĐẶT THÔNG SỐ & VỊ TRÍ CHUẨN GỐC COCOS (1:1)", GUILayout.Height(42)))
            {
                mgr.ApplyCocosOriginalSettings();
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(4);

            // Nút 1: Cập nhật nhanh các hạt hiện có (Scale, Zigzag, Z=6)
            GUI.backgroundColor = new Color(0.2f, 0.85f, 1.0f);
            if (GUILayout.Button("🔄 CẬP NHẬT LẠI VỊ TRÍ & SCALE TẤT CẢ HẠT HIỆN TẠI", GUILayout.Height(32)))
            {
                mgr.UpdateExistingBeads();
                SceneView.RepaintAll();
            }

            // Nút 2: Tái tạo mới toàn bộ tranh cát từ dữ liệu level
            GUI.backgroundColor = new Color(0.35f, 0.95f, 0.45f);
            if (GUILayout.Button("🎨 TÁI TẠO TOÀN BỘ TRANH CÁT (REBUILD LEVEL DATA)", GUILayout.Height(36)))
            {
                mgr.RebuildBoardInEditor();
                SceneView.RepaintAll();
            }

            // Nút 3 & Nút 4
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();

            // Nút 3: Đặt nhanh vị trí Z = 6
            GUI.backgroundColor = new Color(1.0f, 0.95f, 0.5f);
            if (GUILayout.Button("📍 ĐẶT VỊ TRÍ Z = 6.0", GUILayout.Height(28)))
            {
                mgr.boardPosZ = 6f;
                mgr.transform.position = new Vector3(0f, 0f, 6f);
                EditorUtility.SetDirty(mgr.gameObject);
                SceneView.RepaintAll();
                Debug.Log("<color=yellow>[SandBoardManager]</color> Đã đặt vị trí SandBoard_Manager tại Z = 6.0!");
            }

            // Nút 4: Xóa tranh cát
            GUI.backgroundColor = new Color(1.0f, 0.45f, 0.45f);
            if (GUILayout.Button("🗑️ XÓA BẢNG CÁT", GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog("Xác nhận xóa", "Bạn có chắc chắn muốn xóa toàn bộ hạt cát trên bảng không?", "Xóa", "Hủy"))
                {
                    mgr.ClearBoard();
                    EditorUtility.SetDirty(mgr.gameObject);
                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.EndHorizontal();

            // Nút 5: Mở Level Designer Pro
            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(0.2f, 0.75f, 1.0f);
            if (GUILayout.Button("🛠️ MỞ LEVEL DESIGNER PRO", GUILayout.Height(32)))
            {
                WhoGetInBus.EditorTools.GameLevelDesignerWindow.OpenWindow();
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // BẢNG SO SÁNH THÔNG SỐ VỚI GAME GỐC
            bool isExactSpacing = Mathf.Approximately(mgr.beadSpacing, 0.2586f) || Mathf.Approximately(mgr.beadSpacing, 0.26f);
            bool isExactZigzag = Mathf.Approximately(mgr.zigzagOffset, 0.0828f) || Mathf.Approximately(mgr.zigzagOffset, 0.083f);
            bool isExactScale = Mathf.Approximately(mgr.beadScale, 0.55f);
            bool isExactPosZ = Mathf.Approximately(mgr.transform.position.z, 6.0f);

            bool isAllExact = isExactSpacing && isExactZigzag && isExactScale && isExactPosZ;

            if (isAllExact)
            {
                EditorGUILayout.HelpBox(
                    "✅ TOÀN BỘ THÔNG SỐ ĐÃ TRÙNG KHỚP 100% VỚI GAME GỐC COCOS CREATOR:\n" +
                    $"• Bead Spacing: {mgr.beadSpacing:F4} (Chuẩn gốc Cocos 18.75px = 0.2586m)\n" +
                    $"• Zigzag Offset: {mgr.zigzagOffset:F4} (Chuẩn gốc Cocos 6px = 0.0828m)\n" +
                    $"• Bead Scale: {mgr.beadScale:F2} (Vừa khít lưới 0.26m)\n" +
                    $"• Vị trí Position Z: {mgr.transform.position.z:F2} (Chiếu đúng Canvas Y = +371px)",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "⚠️ CẢNH BÁO: CÓ THÔNG SỐ CHƯA KHỚP VỚI GAME GỐC COCOS:\n" +
                    $"• Bead Spacing: {mgr.beadSpacing:F4} (Chuẩn gốc: 0.2586 / ~0.26)\n" +
                    $"• Zigzag Offset: {mgr.zigzagOffset:F4} (Chuẩn gốc: 0.0828 / ~0.083)\n" +
                    $"• Bead Scale: {mgr.beadScale:F2} (Chuẩn gốc: 0.55)\n" +
                    $"• Transform Pos Z: {mgr.transform.position.z:F2} (Chuẩn gốc: 6.0)\n\n" +
                    "👉 Hãy bấm nút vàng [⚡ ĐẶT THÔNG SỐ & VỊ TRÍ CHUẨN GỐC COCOS (1:1)] ở trên để sửa ngay!",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(8);

            // VẼ CÁC TRƯỜNG THÔNG SỐ (INSPECTOR)
            DrawDefaultInspector();

            EditorGUILayout.Space(8);

            // THỐNG KÊ CHI TIẾT
            int childCount = mgr.transform.childCount;
            string zigzagStatus = mgr.enableZigzag ? $"BẬT (Offset: {mgr.zigzagOffset:F4})" : "TẮT";
            EditorGUILayout.HelpBox(
                $"📊 THỐNG KÊ SCENE:\n" +
                $"• Số lượng hạt con trong Hierarchy: {childCount}\n" +
                $"• Tọa độ Transform: {mgr.transform.position}\n" +
                $"• Scale hạt cát (beadScale): {mgr.beadScale:F2}\n" +
                $"• Lệch zigzag: {zigzagStatus}",
                MessageType.None);
        }
    }
}
