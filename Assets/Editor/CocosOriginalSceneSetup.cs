#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

namespace WhoGetInBus.EditorTools
{
    public class CocosOriginalSceneSetup : EditorWindow
    {
        [MenuItem("Tools/1. Bố Trí Lại Scene Hiện Tại Chuẩn Cocos (2 Camera + Canvas)", false, 0)]
        public static void SetupCurrentActiveScene()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            Undo.RegisterCompleteObjectUndo(Selection.objects, "Setup Cocos Scene Layout");

            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer < 0) uiLayer = 5;

            // =====================================================================
            // 1. CAMERA 3D (MAIN CAMERA - OVERLAY CAMERA TRONG URP)
            // =====================================================================
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                var camObj = GameObject.Find("Main Camera");
                if (camObj != null) mainCam = camObj.GetComponent<Camera>();
            }

            if (mainCam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                mainCam = camObj.AddComponent<Camera>();
            }

            // GỠ BỎ QUAN HỆ CHA-CON: Main Camera phải đứng độc lập, không lồng vào Camera khác!
            mainCam.transform.SetParent(null, true);
            mainCam.transform.position = new Vector3(0f, 10f, -7f);
            mainCam.transform.rotation = Quaternion.Euler(58f, 0f, 0f);
            mainCam.transform.localScale = Vector3.one;
            mainCam.orthographic = true;
            mainCam.orthographicSize = 9.2f;
            mainCam.nearClipPlane = 0.01f;
            mainCam.farClipPlane = 50f;
            mainCam.cullingMask = ~(1 << uiLayer); // Không vẽ lớp UI

            // Cấu hình URP Overlay cho Main Camera
            var mainCamData = mainCam.GetUniversalAdditionalCameraData();
            if (mainCamData != null)
            {
                mainCamData.renderType = CameraRenderType.Overlay;
            }

            // Gắn / Cập nhật adapter đồng bộ màn hình chuẩn Cocos (750x1334, 9.2f)
            var adapter = mainCam.GetComponent<GamePlay.CameraResolutionAdapter>();
            if (adapter == null) adapter = mainCam.gameObject.AddComponent<GamePlay.CameraResolutionAdapter>();
            adapter.baseOrthoSize = 9.2f;
            adapter.referenceResolution = new Vector2(750f, 1334f);
            adapter.fitMode = GamePlay.CameraResolutionAdapter.AspectFitMode.FitAll;
            adapter.lockTransform = false;

            // =====================================================================
            // 2. CAMERA 2D (CAMERA UI 3D - BASE CAMERA TRONG URP)
            // =====================================================================
            GameObject bgCamObj = GameObject.Find("Camera UI 3D (Background)");
            if (bgCamObj == null) bgCamObj = GameObject.Find("Camera UI 3D");
            if (bgCamObj == null)
            {
                bgCamObj = new GameObject("Camera UI 3D (Background)");
            }
            bgCamObj.name = "Camera UI 3D (Background)";
            bgCamObj.transform.SetParent(null, true);
            bgCamObj.transform.position = new Vector3(0, 0, -100);
            bgCamObj.transform.rotation = Quaternion.identity;
            bgCamObj.transform.localScale = Vector3.one;

            // Xóa adapter khỏi Camera UI (Camera UI chiếu Canvas không được gắn adapter đổi orthoSize!)
            var oldBgAdapter = bgCamObj.GetComponent<GamePlay.CameraResolutionAdapter>();
            if (oldBgAdapter != null)
            {
                Object.DestroyImmediate(oldBgAdapter);
            }

            Camera bgCam = bgCamObj.GetComponent<Camera>();
            if (bgCam == null) bgCam = bgCamObj.AddComponent<Camera>();

            bgCam.clearFlags = CameraClearFlags.SolidColor;
            bgCam.backgroundColor = new Color(0.18f, 0.17f, 0.22f, 1f); // Nền tím xám ấm áp
            bgCam.orthographic = true;
            bgCam.orthographicSize = 6.67f;
            bgCam.depth = -1;
            bgCam.nearClipPlane = 0.3f;
            bgCam.farClipPlane = 1000f; // Để 1000f tránh bị clip UI khi Canvas đặt ở xa
            bgCam.cullingMask = 1 << uiLayer;

            // Cấu hình URP Base Camera và Camera Stack
            var bgCamData = bgCam.GetUniversalAdditionalCameraData();
            if (bgCamData != null)
            {
                bgCamData.renderType = CameraRenderType.Base;
                if (!bgCamData.cameraStack.Contains(mainCam))
                {
                    bgCamData.cameraStack.Clear();
                    bgCamData.cameraStack.Add(mainCam);
                }
            }

            // =====================================================================
            // 3. CANVAS CHỨA MẶT ĐƯỜNG (SCREEN SPACE - CAMERA)
            // =====================================================================
            GameObject roadCanvasObj = GameObject.Find("Background Canvas (Road)");
            if (roadCanvasObj == null) roadCanvasObj = GameObject.Find("Map Canvass");
            if (roadCanvasObj == null)
            {
                roadCanvasObj = new GameObject("Background Canvas (Road)");
            }
            roadCanvasObj.name = "Background Canvas (Road)";
            roadCanvasObj.layer = uiLayer;

            Canvas roadCanvas = roadCanvasObj.GetComponent<Canvas>();
            if (roadCanvas == null) roadCanvas = roadCanvasObj.AddComponent<Canvas>();
            roadCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            roadCanvas.worldCamera = bgCam;
            roadCanvas.planeDistance = 20; // Đặt 20 an toàn trong tầm nhìn của Camera UI

            CanvasScaler roadScaler = roadCanvasObj.GetComponent<CanvasScaler>();
            if (roadScaler == null) roadScaler = roadCanvasObj.AddComponent<CanvasScaler>();
            roadScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            roadScaler.referenceResolution = new Vector2(750, 1334);
            roadScaler.matchWidthOrHeight = 0.5f;

            if (roadCanvasObj.GetComponent<GraphicRaycaster>() == null)
            {
                roadCanvasObj.AddComponent<GraphicRaycaster>();
            }

            // targetController (Gốc điều khiển đường trong Cocos ở Y = 336)
            Transform targetCtrlTr = roadCanvasObj.transform.Find("targetController");
            GameObject targetCtrl;
            if (targetCtrlTr == null)
            {
                targetCtrl = CreateUIElement("targetController", roadCanvasObj.transform);
            }
            else
            {
                targetCtrl = targetCtrlTr.gameObject;
            }
            targetCtrl.layer = uiLayer;
            RectTransform targetCtrlRt = targetCtrl.GetComponent<RectTransform>();
            targetCtrlRt.anchoredPosition = new Vector2(0, 336);

            // Tìm hoặc chuyển đổi Image cũ vào targetController
            Transform bottomTr = targetCtrl.transform.Find("roadBottom");
            if (bottomTr == null) bottomTr = roadCanvasObj.transform.Find("Image");
            if (bottomTr == null) bottomTr = targetCtrl.transform.Find("Image");
            GameObject roadBottomObj = bottomTr != null ? bottomTr.gameObject : CreateUIElement("roadBottom", targetCtrl.transform);
            roadBottomObj.name = "roadBottom";
            roadBottomObj.transform.SetParent(targetCtrl.transform, false);
            roadBottomObj.layer = uiLayer;
            RectTransform roadBottomRt = roadBottomObj.GetComponent<RectTransform>();
            roadBottomRt.anchorMin = new Vector2(0.5f, 0.5f);
            roadBottomRt.anchorMax = new Vector2(0.5f, 0.5f);
            roadBottomRt.pivot = new Vector2(0.5f, 1f);
            roadBottomRt.anchoredPosition = new Vector2(0, -453);
            roadBottomRt.sizeDelta = new Vector2(667, 805);

            Image roadBottomImg = roadBottomObj.GetComponent<Image>();
            if (roadBottomImg == null) roadBottomImg = roadBottomObj.AddComponent<Image>();
            if (roadBottomImg.sprite == null)
            {
                Sprite bottomSprite = LoadSpriteByPath("Assets/Sprite/UI/level/CITY/roadBottom_02.png")
                                   ?? LoadSpriteByPath("Assets/Sprite/UI/level/FARM/roadBottom_03.png")
                                   ?? LoadSpriteByPath("Assets/Sprite/UI/level/Sea/roadBottom.png");
                if (bottomSprite != null) roadBottomImg.sprite = bottomSprite;
            }
            roadBottomImg.type = Image.Type.Sliced; // 9-Slice chuẩn Cocos!

            // roadTop (Mặt đường vòng cua xe bus bên trên)
            Transform topTr = targetCtrl.transform.Find("roadTop");
            if (topTr == null) topTr = roadCanvasObj.transform.Find("Image (1)");
            if (topTr == null) topTr = targetCtrl.transform.Find("Image (1)");
            GameObject roadTopObj = topTr != null ? topTr.gameObject : CreateUIElement("roadTop", targetCtrl.transform);
            roadTopObj.name = "roadTop";
            roadTopObj.transform.SetParent(targetCtrl.transform, false);
            roadTopObj.layer = uiLayer;
            RectTransform roadTopRt = roadTopObj.GetComponent<RectTransform>();
            roadTopRt.anchorMin = new Vector2(0.5f, 0.5f);
            roadTopRt.anchorMax = new Vector2(0.5f, 0.5f);
            roadTopRt.pivot = new Vector2(0.5f, 0f);
            roadTopRt.anchoredPosition = new Vector2(0, -454);
            roadTopRt.sizeDelta = new Vector2(667, 484);

            Image roadTopImg = roadTopObj.GetComponent<Image>();
            if (roadTopImg == null) roadTopImg = roadTopObj.AddComponent<Image>();
            if (roadTopImg.sprite == null)
            {
                Sprite topSprite = LoadSpriteByPath("Assets/Sprite/UI/level/CITY/roadTop32_05.png")
                                ?? LoadSpriteByPath("Assets/Sprite/UI/level/CITY/roadTop5_05.png")
                                ?? LoadSpriteByPath("Assets/Sprite/UI/level/CITY/roadTop12_1.png")
                                ?? LoadSpriteByPath("Assets/Sprite/UI/level/FARM/roadTop4_03.png")
                                ?? LoadSpriteByName("roadTop");
                if (topSprite != null) roadTopImg.sprite = topSprite;
            }
            roadTopImg.type = Image.Type.Simple;

            // Đồng bộ CanvasScaler của các Canvas khác (như Canvas Booster) về Portrait 750x1334
            var allScalers = Object.FindObjectsByType<CanvasScaler>(FindObjectsSortMode.None);
            foreach (var scaler in allScalers)
            {
                if (scaler.gameObject != roadCanvasObj)
                {
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(750, 1334);
                    scaler.matchWidthOrHeight = 0.5f;
                }
            }

            // =====================================================================
            // 4. CĂN CHỈNH BIỂN BÁO 0/6 + (COUNT) VÀ ẨN CÁC SPRITERENDERER 3D CŨ
            // =====================================================================
            GameObject countObj = GameObject.Find("Count");
            if (countObj != null)
            {
                countObj.transform.position = new Vector3(0f, 0.52f, -2.897f);
            }

            // Ẩn các tấm SpriteRenderer cũ dưới sàn trong ---Map--- để không bị đè nhau
            GameObject mapRoot = GameObject.Find("---Map---");
            if (mapRoot != null)
            {
                for (int i = 0; i < mapRoot.transform.childCount; i++)
                {
                    var child = mapRoot.transform.GetChild(i);
                    if (child.name.Contains("Top") || child.name.Contains("Bottom") || child.name.Contains("Plane"))
                    {
                        child.gameObject.SetActive(false);
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log("<color=green>[CocosOriginalSceneSetup] Đã thiết lập xong 2 Camera + Canvas 2D chuẩn Cocos vào scene hiện tại!</color>");
            EditorUtility.DisplayDialog("Hoàn tất!", 
                "Đã tự động cài đặt xong kiến trúc 2 Camera chuẩn Cocos vào Scene hiện tại:\n\n" +
                "1. Camera UI 3D (Background): Depth -1, chiếu mặt đường 2D (Không bao giờ bị méo).\n" +
                "2. Main Camera 3D: Chiếu đè các xe 3D và biển 0/6+ lên mặt đường (Góc 58°, Ortho 9.2).\n" +
                "3. Các tấm SpriteRenderer cũ bị méo trên sàn đã được tạm ẩn đi.\n\n" +
                "Bây giờ bạn chuyển sang tab Game View để xem kết quả mượt mà liền mạch nhé!", "OK");
        }

        [MenuItem("Tools/2. Tạo Scene Mới Chuẩn Cocos 1-1 (Kèm Đầy Đủ Xe Mẫu)", false, 1)]
        public static void BuildOriginalCocosScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Cocos_Original_GamePlay";

            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer < 0) uiLayer = 5;

            // 1. Camera UI 3D
            GameObject bgCamObj = new GameObject("Camera UI 3D (Background)");
            Camera bgCam = bgCamObj.AddComponent<Camera>();
            bgCam.clearFlags = CameraClearFlags.SolidColor;
            bgCam.backgroundColor = new Color(0.18f, 0.17f, 0.22f, 1f);
            bgCam.orthographic = true;
            bgCam.orthographicSize = 6.67f;
            bgCam.depth = -1;
            bgCam.cullingMask = 1 << uiLayer;
            bgCamObj.transform.position = new Vector3(0, 0, -100);

            // 2. Main Camera 3D
            GameObject mainCamObj = new GameObject("Main Camera (3D Gameplay)");
            mainCamObj.tag = "MainCamera";
            Camera mainCam = mainCamObj.AddComponent<Camera>();
            mainCam.orthographic = true;
            mainCam.orthographicSize = 9.2f;
            mainCam.nearClipPlane = 0.01f;
            mainCam.farClipPlane = 50f;
            mainCam.cullingMask = ~(1 << uiLayer);
            mainCamObj.transform.position = new Vector3(0f, 10f, -7f);
            mainCamObj.transform.rotation = Quaternion.Euler(58f, 0f, 0f);

            var bgCamData = bgCam.GetUniversalAdditionalCameraData();
            var mainCamData = mainCam.GetUniversalAdditionalCameraData();
            if (bgCamData != null && mainCamData != null)
            {
                bgCamData.renderType = CameraRenderType.Base;
                mainCamData.renderType = CameraRenderType.Overlay;
                bgCamData.cameraStack.Add(mainCam);
            }

            // Gắn adapter
            var adapter = mainCamObj.AddComponent<GamePlay.CameraResolutionAdapter>();
            adapter.baseOrthoSize = 9.2f;
            adapter.referenceResolution = new Vector2(750f, 1334f);
            adapter.fitMode = GamePlay.CameraResolutionAdapter.AspectFitMode.FitAll;

            // 3. Directional Light
            GameObject lightObj = new GameObject("Directional Light");
            Light dirLight = lightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(1f, 0.98f, 0.92f);
            dirLight.intensity = 1.25f;
            lightObj.transform.position = new Vector3(0f, 10f, 0f);
            lightObj.transform.rotation = Quaternion.Euler(70f, -53f, 0f);

            // 4. Background Canvas (Road)
            GameObject roadCanvasObj = new GameObject("Background Canvas (Road)");
            roadCanvasObj.layer = uiLayer;
            Canvas roadCanvas = roadCanvasObj.AddComponent<Canvas>();
            roadCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            roadCanvas.worldCamera = bgCam;
            roadCanvas.planeDistance = 100;

            CanvasScaler roadScaler = roadCanvasObj.AddComponent<CanvasScaler>();
            roadScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            roadScaler.referenceResolution = new Vector2(750, 1334);
            roadScaler.matchWidthOrHeight = 0.5f;
            roadCanvasObj.AddComponent<GraphicRaycaster>();

            GameObject targetCtrl = CreateUIElement("targetController", roadCanvasObj.transform);
            targetCtrl.layer = uiLayer;
            RectTransform targetCtrlRt = targetCtrl.GetComponent<RectTransform>();
            targetCtrlRt.anchoredPosition = new Vector2(0, 336);

            // roadBottom
            GameObject roadBottomObj = CreateUIElement("roadBottom", targetCtrl.transform);
            roadBottomObj.layer = uiLayer;
            RectTransform roadBottomRt = roadBottomObj.GetComponent<RectTransform>();
            roadBottomRt.anchorMin = new Vector2(0.5f, 0.5f);
            roadBottomRt.anchorMax = new Vector2(0.5f, 0.5f);
            roadBottomRt.pivot = new Vector2(0.5f, 1f);
            roadBottomRt.anchoredPosition = new Vector2(0, -453);
            roadBottomRt.sizeDelta = new Vector2(667, 805);

            Image roadBottomImg = roadBottomObj.AddComponent<Image>();
            Sprite bottomSprite = LoadSpriteByPath("Assets/Sprite/UI/level/CITY/roadBottom_02.png")
                               ?? LoadSpriteByPath("Assets/Sprite/UI/level/FARM/roadBottom_03.png")
                               ?? LoadSpriteByPath("Assets/Sprite/UI/level/Sea/roadBottom.png");
            if (bottomSprite != null)
            {
                roadBottomImg.sprite = bottomSprite;
                roadBottomImg.type = Image.Type.Sliced;
            }

            // roadTop
            GameObject roadTopObj = CreateUIElement("roadTop", targetCtrl.transform);
            roadTopObj.layer = uiLayer;
            RectTransform roadTopRt = roadTopObj.GetComponent<RectTransform>();
            roadTopRt.anchorMin = new Vector2(0.5f, 0.5f);
            roadTopRt.anchorMax = new Vector2(0.5f, 0.5f);
            roadTopRt.pivot = new Vector2(0.5f, 0f);
            roadTopRt.anchoredPosition = new Vector2(0, -454);
            roadTopRt.sizeDelta = new Vector2(667, 484);

            Image roadTopImg = roadTopObj.AddComponent<Image>();
            Sprite topSprite = LoadSpriteByPath("Assets/Sprite/UI/level/CITY/roadTop5_05.png")
                            ?? LoadSpriteByPath("Assets/Sprite/UI/level/CITY/roadTop12_1.png")
                            ?? LoadSpriteByPath("Assets/Sprite/UI/level/FARM/roadTop4_03.png");
            if (topSprite != null)
            {
                roadTopImg.sprite = topSprite;
                roadTopImg.type = Image.Type.Simple;
            }

            // 5. 3D Elements Container
            GameObject level3DRoot = new GameObject("---Level 3D Container---");
            GameObject entityRoot = new GameObject("entityRoot (Lưới xe 3D)");
            entityRoot.transform.SetParent(level3DRoot.transform, false);
            entityRoot.transform.localPosition = new Vector3(0f, 0f, -4.388f);

            GameObject carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Car.prefab");
            int cols = 5;
            int rows = 4;
            float spaceX = Mathf.Min(1.6f, 5.6f / (cols - 1));
            float spaceZ = 1.8f;
            float startX = -(cols - 1) * spaceX / 2f;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    GameObject carObj;
                    if (carPrefab != null)
                    {
                        carObj = (GameObject)PrefabUtility.InstantiatePrefab(carPrefab, entityRoot.transform);
                    }
                    else
                    {
                        carObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        carObj.transform.SetParent(entityRoot.transform, false);
                        carObj.transform.localScale = new Vector3(1.1f, 0.7f, 1.5f);
                    }
                    carObj.name = $"Car_R{r}_C{c}";
                    float posX = startX + c * spaceX;
                    float posZ = -r * spaceZ;
                    carObj.transform.localPosition = new Vector3(posX, 0f, posZ);
                }
            }

            // EventSystem
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventObj = new GameObject("EventSystem");
                eventObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            string savePath = "Assets/Scenes/Cocos_Original_GamePlay.unity";
            EditorSceneManager.SaveScene(scene, savePath);
            EditorUtility.DisplayDialog("Thành công!", $"Đã tạo xong scene mới tại {savePath}", "OK");
        }

        private static GameObject CreateUIElement(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static Sprite LoadSpriteByPath(string path)
        {
            if (File.Exists(path))
            {
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return null;
        }

        private static Sprite LoadSpriteByName(string spriteName)
        {
            string[] guids = AssetDatabase.FindAssets($"{spriteName} t:Sprite");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return null;
        }
    }
}
#endif
