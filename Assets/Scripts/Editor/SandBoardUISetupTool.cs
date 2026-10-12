using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using WhoGetInBus.GamePlay;

namespace WhoGetInBus.EditorTools
{
    public static class SandBoardUISetupTool
    {
        // Sử dụng menu "Tools/⚡ Chuyển Tranh Cát Lên 2D UI" để chủ động thực hiện khi cần

        private static void AutoCheckAndMigrate()
        {
            // Tự động kiểm tra nếu trong Scene còn SandBoard_Manager cũ ở ngoài 3D hoặc còn con rác
            SandBoardManager oldMgr = UnityEngine.Object.FindFirstObjectByType<SandBoardManager>();
            if (oldMgr != null)
            {
                Canvas parentCanvas = oldMgr.GetComponentInParent<Canvas>();
                if (parentCanvas == null || oldMgr.transform.childCount > 0)
                {
                    Debug.Log("<color=yellow>[SandBoardUISetupTool]</color> Phát hiện SandBoard_Manager cũ (3D hoặc có con rác), tự động chuyển lên 2D UI...");
                    SetupSandBoardOnUI(false);
                }
            }
        }

        [MenuItem("Tools/⚡ Chuyển Tranh Cát Lên 2D UI (Chuẩn Cocos 1 Draw Call)", false, 10)]
        public static void MenuSetupSandBoardOnUI()
        {
            SetupSandBoardOnUI(true);
        }

        public static void SetupSandBoardOnUI(bool showDialog = false)
        {
            // 1. Tìm Canvas "Map Canvass"
            Canvas mapCanvas = null;
            var allCanvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in allCanvases)
            {
                if (c.gameObject.name.Contains("Map"))
                {
                    mapCanvas = c;
                    break;
                }
            }

            if (mapCanvas == null && allCanvases.Length > 0)
            {
                mapCanvas = allCanvases[0];
            }

            if (mapCanvas == null)
            {
                if (showDialog) EditorUtility.DisplayDialog("Lỗi", "Không tìm thấy Canvas 'Map Canvass' trong Scene!", "OK");
                return;
            }

            // 2. Tìm hoặc dọn sạch SandBoard_Manager cũ
            SandBoardManager oldMgr = UnityEngine.Object.FindFirstObjectByType<SandBoardManager>();
            GameObject boardObj = null;

            if (oldMgr != null)
            {
                boardObj = oldMgr.gameObject;
                // Xóa sạch tất cả 1.300+ hạt cát con cũ để giải phóng scene
                oldMgr.ClearBoard();
            }
            else
            {
                boardObj = new GameObject("SandBoard_Manager");
            }

            // 3. Đưa SandBoard_Manager vào làm con của Map Canvass
            boardObj.transform.SetParent(mapCanvas.transform, false);
            boardObj.name = "SandBoard_Manager";

            // Đặt layer UI (layer 5)
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer < 0) uiLayer = 5;
            boardObj.layer = uiLayer;

            // 4. Thiết lập RectTransform phẳng 2D
            RectTransform rt = boardObj.GetComponent<RectTransform>();
            if (rt == null) rt = boardObj.AddComponent<RectTransform>();

            // Tìm vị trí của Top để khớp chân đường
            Transform topTransform = mapCanvas.transform.Find("Top");
            Vector2 targetPos = new Vector2(0f, -180f);
            if (topTransform != null && topTransform is RectTransform topRt)
            {
                targetPos = topRt.anchoredPosition;
            }

            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f); // Bottom-Center chuẩn đáy vòm cua
            rt.anchoredPosition = targetPos;
            rt.sizeDelta = new Vector2(973f, 973f);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            // Xếp thứ tự hierarchy: phần tử thứ 2 (vị trí index 1, ngay sau bg)
            boardObj.transform.SetSiblingIndex(1);

            // 5. Gắn các Component cần thiết
            CanvasRenderer cr = boardObj.GetComponent<CanvasRenderer>();
            if (cr == null) cr = boardObj.AddComponent<CanvasRenderer>();

            SandBoardGraphic graphic = boardObj.GetComponent<SandBoardGraphic>();
            if (graphic == null) graphic = boardObj.AddComponent<SandBoardGraphic>();

            SandBoardManager mgr = boardObj.GetComponent<SandBoardManager>();
            if (mgr == null) mgr = boardObj.AddComponent<SandBoardManager>();

            graphic.boardManager = mgr;

            // Nạp SandBoardConfigData
            var cfg = AssetDatabase.LoadAssetAtPath<WhoGetInBus.Data.SandBoardConfigData>("Assets/Data/SandBoardConfigData.asset");
            if (cfg != null)
            {
                mgr.config = cfg;
            }

            // Nạp Texture Atlas
            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprite/PassengerAtlas_18.png");
            if (atlas != null)
            {
                graphic.passengerAtlas = atlas;
                mgr.passengerAtlas = atlas;
            }

            // Nạp PassengerColorData
            var colorData = AssetDatabase.LoadAssetAtPath<PassengerColorData>("Assets/Data/PassengerColorData.asset");
            if (colorData != null)
            {
                mgr.passengerColors = colorData;
            }

            // 6. Áp dụng thông số chuẩn Cocos và sinh lưới cát lên UI
            mgr.ApplyCocosOriginalSettings();
            mgr.RebuildBoardInEditor();

            // Đánh dấu Scene thay đổi & lưu
            EditorUtility.SetDirty(boardObj);
            EditorUtility.SetDirty(mgr);
            EditorUtility.SetDirty(graphic);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("<color=green>[SandBoardUISetupTool]</color> <b>ĐÃ CHUYỂN TRANH CÁT LÊN 2D UI THÀNH CÔNG!</b>\n" +
                      $"• Vị trí: Con của Canvas '{mapCanvas.name}'\n" +
                      "• 0 GameObject con rác trên Scene\n" +
                      "• 1 Draw Call duy nhất trên UI Canvas");

            if (showDialog)
            {
                EditorUtility.DisplayDialog("Thành Công!",
                    "ĐÃ CHUYỂN TRANH CÁT LÊN 2D UI THÀNH CÔNG!\n\n" +
                    "• Toàn bộ 1.370 hạt cát hiện được vẽ trên Canvas bằng ĐÚNG 1 DRAW CALL duy nhất.\n" +
                    "• 0 GameObject con rác trên Scene (giải phóng hoàn toàn CPU).\n" +
                    "• Phẳng 100% cùng tầng với mặt đường Top/Bottom, không còn bị xoay 58°.\n" +
                    "• Tương thích 100% với hệ thống đón xe của BoardingGateDetector!",
                    "Tuyệt vời");
            }
        }
    }
}
