using UnityEngine;
using UnityEditor;
using WhoGetInBus.GamePlay;
using WhoGetInBus.Data;

namespace WhoGetInBus.Editor
{
    [CustomEditor(typeof(SandBoardManager))]
    public class SandBoardManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            SandBoardManager mgr = (SandBoardManager)target;

            serializedObject.Update();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("🏖️ SAND BOARD MANAGER (UGUI 1-DRAW CALL)", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            // 0. SCRIPTABLEOBJECT CONFIG DATA (LƯU VĨNH VIỄN)
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📁 File Cấu Hình Dữ Liệu (ScriptableObject Config)", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            SandBoardConfigData newConfig = (SandBoardConfigData)EditorGUILayout.ObjectField("Config Asset", mgr.config, typeof(SandBoardConfigData), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(mgr, "Change SandBoard Config");
                mgr.config = newConfig;
                if (mgr.config != null)
                {
                    mgr.SyncFromConfig();
                    mgr.RefreshGraphic();
                }
                EditorUtility.SetDirty(mgr);
            }

            if (mgr.config == null)
            {
                EditorGUILayout.HelpBox("⚠️ Chưa liên kết file SandBoardConfigData.asset! Nhấn nút dưới để tự động kết nối.", MessageType.Warning);
                if (GUILayout.Button("⚡ Tự Động Kết Nối / Nạp SandBoardConfigData.asset", GUILayout.Height(26)))
                {
                    mgr.EnsureConfigLoaded();
                    EditorUtility.SetDirty(mgr);
                    SceneView.RepaintAll();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("✅ Đang liên kết SandBoardConfigData.asset. Mọi thông số dưới đây được lưu vĩnh viễn vào file data, KHÔNG bao giờ bị mất hoặc reset khi sửa code C#!", MessageType.Info);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // 1. THANH CHUYỂN LEVEL NHANH (VỚI MŨI TÊN ◀ VÀ ▶)
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🎮 Chọn Màn Chơi (Level Data)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("◀", GUILayout.Width(30), GUILayout.Height(22)))
            {
                mgr.levelId = Mathf.Max(1, mgr.levelId - 1);
                mgr.RebuildBoardInEditor();
                EditorUtility.SetDirty(mgr);
                SceneView.RepaintAll();
            }

            int newLevel = EditorGUILayout.IntField("Level ID", mgr.levelId);
            if (newLevel != mgr.levelId)
            {
                mgr.levelId = newLevel;
                mgr.RebuildBoardInEditor();
                EditorUtility.SetDirty(mgr);
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("▶", GUILayout.Width(30), GUILayout.Height(22)))
            {
                mgr.levelId = Mathf.Min(501, mgr.levelId + 1);
                mgr.RebuildBoardInEditor();
                EditorUtility.SetDirty(mgr);
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"Tuyến đường (Road ID): {mgr.currentRoadId}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // 2. THÔNG SỐ HẠT CÁT & VỊ TRÍ UI
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("⚙️ Thông Số Vị Trí Bảng & Kích Thước Hạt Cát", EditorStyles.boldLabel);

            // Vị Trí Bảng Cát Trên Canvas (RectTransform Anchored Position)
            RectTransform rt = mgr.GetComponent<RectTransform>();
            if (rt != null)
            {
                EditorGUI.BeginChangeCheck();
                Vector2 newBoardPos = EditorGUILayout.Vector2Field(new GUIContent("Board Position UI (X, Y)", "Vị trí toạ độ AnchoredPosition của bảng cát trên màn hình Canvas"), rt.anchoredPosition);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(rt, "Change Board Position");
                    rt.anchoredPosition = newBoardPos;
                    if (mgr.config != null)
                    {
                        Undo.RecordObject(mgr.config, "Change Config Board Position");
                        mgr.config.boardPosition = newBoardPos;
                        EditorUtility.SetDirty(mgr.config);
                        AssetDatabase.SaveAssets();
                    }
                    EditorUtility.SetDirty(rt);
                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.Space(2);

            // Bead Size & Scale
            EditorGUI.BeginChangeCheck();
            Vector2 newSize = EditorGUILayout.Vector2Field(new GUIContent("Bead Size px (Rộng x Cao)", "Kích thước quad hạt cát (chuẩn Cocos: 34 x 44 px để tạo độ lấp lên 20px)"), mgr.beadSize);
            float newScale = EditorGUILayout.Slider(new GUIContent("Bead Scale (Tỉ Lệ)", "Tỉ lệ scale bổ sung của hạt cát (chuẩn 1.0)"), mgr.beadScale, 0.5f, 1.6f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(mgr, "Change Bead Size / Scale");
                mgr.beadSize = newSize;
                mgr.beadScale = newScale;
                mgr.SyncToConfig();
                mgr.UpdateBeadScalesOnly();
                EditorUtility.SetDirty(mgr);
                if (mgr.config != null)
                {
                    EditorUtility.SetDirty(mgr.config);
                    AssetDatabase.SaveAssets();
                }
                SceneView.RepaintAll();
            }

            // Offset X (Trái / Phải) với cập nhật tức thì
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            float newOffsetX = EditorGUILayout.Slider(new GUIContent("Offset X (Trái / Phải)", "Độ lệch pixel ngang sang trái (-) hoặc sang phải (+)"), mgr.boardOffsetX, -300f, 300f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(mgr, "Change Board Offset X");
                mgr.boardOffsetX = newOffsetX;
                mgr.SyncToConfig();
                mgr.UpdateExistingBeads();
                EditorUtility.SetDirty(mgr);
                if (mgr.config != null)
                {
                    EditorUtility.SetDirty(mgr.config);
                    AssetDatabase.SaveAssets();
                }
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("0", GUILayout.Width(25), GUILayout.Height(18)))
            {
                Undo.RecordObject(mgr, "Reset Board Offset X");
                mgr.boardOffsetX = 0f;
                mgr.SyncToConfig();
                mgr.UpdateExistingBeads();
                EditorUtility.SetDirty(mgr);
                if (mgr.config != null)
                {
                    EditorUtility.SetDirty(mgr.config);
                    AssetDatabase.SaveAssets();
                }
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            // Offset Y (Lên / Xuống) với cập nhật tức thì
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            float newOffsetY = EditorGUILayout.Slider(new GUIContent("Offset Y (Lên / Xuống)", "Độ lệch pixel dọc lên (+) hoặc xuống (-)"), mgr.boardOffsetY, -300f, 300f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(mgr, "Change Board Offset Y");
                mgr.boardOffsetY = newOffsetY;
                mgr.SyncToConfig();
                mgr.UpdateExistingBeads();
                EditorUtility.SetDirty(mgr);
                if (mgr.config != null)
                {
                    EditorUtility.SetDirty(mgr.config);
                    AssetDatabase.SaveAssets();
                }
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("0", GUILayout.Width(25), GUILayout.Height(18)))
            {
                Undo.RecordObject(mgr, "Reset Board Offset Y");
                mgr.boardOffsetY = 0f;
                mgr.SyncToConfig();
                mgr.UpdateExistingBeads();
                EditorUtility.SetDirty(mgr);
                if (mgr.config != null)
                {
                    EditorUtility.SetDirty(mgr.config);
                    AssetDatabase.SaveAssets();
                }
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            // Spacing X (Khoảng cách ngang px)
            EditorGUI.BeginChangeCheck();
            float newSpacingX = EditorGUILayout.Slider(new GUIContent("Spacing X (Khoảng Cách Ngang px)", "Khoảng cách giữa các cột hạt trên UI (chuẩn 24.32 px)"), mgr.beadSpacingX, 12f, 40f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(mgr, "Change Bead Spacing X");
                mgr.beadSpacingX = newSpacingX;
                mgr.beadSpacing = newSpacingX;
                mgr.SyncToConfig();
                mgr.UpdateExistingBeads();
                EditorUtility.SetDirty(mgr);
                if (mgr.config != null)
                {
                    EditorUtility.SetDirty(mgr.config);
                    AssetDatabase.SaveAssets();
                }
                SceneView.RepaintAll();
            }

            // Spacing Y (Khoảng cách dọc px)
            EditorGUI.BeginChangeCheck();
            float newSpacingY = EditorGUILayout.Slider(new GUIContent("Spacing Y (Khoảng Cách Dọc px)", "Khoảng cách giữa các hàng hạt trên UI (chuẩn 24.32 px)"), mgr.beadSpacingY, 12f, 40f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(mgr, "Change Bead Spacing Y");
                mgr.beadSpacingY = newSpacingY;
                mgr.SyncToConfig();
                mgr.UpdateExistingBeads();
                EditorUtility.SetDirty(mgr);
                if (mgr.config != null)
                {
                    EditorUtility.SetDirty(mgr.config);
                    AssetDatabase.SaveAssets();
                }
                SceneView.RepaintAll();
            }

            // Zigzag
            EditorGUI.BeginChangeCheck();
            bool newZigzag = EditorGUILayout.Toggle(new GUIContent("Enable Zigzag (Xếp So Le)", "Xếp so le các hạt giữa các hàng"), mgr.enableZigzag);
            float newZigzagOffset = mgr.zigzagOffset;
            if (newZigzag)
            {
                newZigzagOffset = EditorGUILayout.Slider(new GUIContent("Zigzag Offset px", "Chuẩn Cocos: 12.16 pixel"), mgr.zigzagOffset, -25f, 25f);
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(mgr, "Change Zigzag Settings");
                mgr.enableZigzag = newZigzag;
                mgr.zigzagOffset = newZigzagOffset;
                mgr.SyncToConfig();
                mgr.UpdateExistingBeads();
                EditorUtility.SetDirty(mgr);
                if (mgr.config != null)
                {
                    EditorUtility.SetDirty(mgr.config);
                    AssetDatabase.SaveAssets();
                }
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // 3. PREFABS & ATLAS
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📦 Asset & Texture Atlas", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            Texture2D newAtlas = (Texture2D)EditorGUILayout.ObjectField("Passenger Atlas (18 Màu)", mgr.passengerAtlas, typeof(Texture2D), false);
            PassengerColorData newColors = (PassengerColorData)EditorGUILayout.ObjectField("Passenger Colors", mgr.passengerColors, typeof(PassengerColorData), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(mgr, "Change Atlas / Colors");
                mgr.passengerAtlas = newAtlas;
                mgr.passengerColors = newColors;
                mgr.SyncToConfig();
                mgr.RefreshGraphic();
                EditorUtility.SetDirty(mgr);
                if (mgr.config != null)
                {
                    EditorUtility.SetDirty(mgr.config);
                    AssetDatabase.SaveAssets();
                }
                SceneView.RepaintAll();
            }

            if (mgr.passengerAtlas == null)
            {
                if (GUILayout.Button("⚡ Tự Động Nạp PassengerAtlas_18.png"))
                {
                    mgr.passengerAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprite/PassengerAtlas_18.png");
                    mgr.EnsureAtlasLoaded();
                    mgr.SyncToConfig();
                    mgr.RefreshGraphic();
                    EditorUtility.SetDirty(mgr);
                    if (mgr.config != null)
                    {
                        EditorUtility.SetDirty(mgr.config);
                        AssetDatabase.SaveAssets();
                    }
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // 4. THAO TÁC CHÍNH (GỌN GÀNG, ĐƠN GIẢN)
            GUI.backgroundColor = new Color(0.2f, 0.75f, 1.0f);
            if (GUILayout.Button("⚡ Áp Dụng Chuẩn Gốc Cocos (34x44, Lớp Đè & So Le 1:1)", GUILayout.Height(30)))
            {
                mgr.ApplyCocosOriginalSettings();
                mgr.RebuildBoardInEditor();
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.25f, 0.85f, 0.45f);
            if (GUILayout.Button("🎨 Sinh Tranh Cát (Rebuild UI)", GUILayout.Height(36)))
            {
                mgr.RebuildBoardInEditor();
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = new Color(1.0f, 0.4f, 0.4f);
            if (GUILayout.Button("🗑️ Xóa Bảng Cát", GUILayout.Height(36)))
            {
                if (EditorUtility.DisplayDialog("Xóa bảng cát", "Bạn có chắc chắn muốn xóa tất cả hạt cát trên bảng không?", "Xóa", "Hủy"))
                {
                    mgr.ClearBoard();
                    EditorUtility.SetDirty(mgr);
                    SceneView.RepaintAll();
                }
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);
            if (GUILayout.Button("🛠️ Mở Level Designer Pro", GUILayout.Height(24)))
            {
                WhoGetInBus.EditorTools.GameLevelDesignerWindow.OpenWindow();
            }

            EditorGUILayout.Space(4);

            // 5. THỐNG KÊ GỌN
            EditorGUILayout.HelpBox($"Số hạt hiện có: {mgr.TotalRemainingBeads}  |  Draw Call: 1  |  Scale: {mgr.beadScale:F2}", MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
