#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace DouyinGame.Editor
{
    public class DouyinSceneSpawner : EditorWindow
    {
        // Đã gỡ bỏ tự động sinh trên load scene. Chỉ chạy khi người dùng bấm Menu hoặc nút trong Tool.

        [MenuItem("Douyin Tools/Spawn Gameplay Simulation Scene (Mô phỏng đầy đủ)", false, 10)]
        public static void SpawnSimulationMenu()
        {
            SpawnFullScene();
        }

        [MenuItem("Douyin Tools/Clear Simulation Scene (Xóa mô phỏng)", false, 11)]
        public static void ClearSimulationMenu()
        {
            ClearScene();
        }

        [MenuItem("Douyin Tools/Open Spawner Window", false, 12)]
        public static void OpenWindow()
        {
            GetWindow<DouyinSceneSpawner>("Douyin Spawner");
        }

        private void OnGUI()
        {
            GUILayout.Label("Mô phỏng Bố trí Scene Cocos sang Unity", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (GUILayout.Button("1. Sinh toàn bộ Object & Bố trí chuẩn (Cocos 1:1)", GUILayout.Height(40)))
            {
                SpawnFullScene();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("2. Căn chỉnh Camera & Lighting chuẩn Cocos", GUILayout.Height(30)))
            {
                SetupCameraAndLight();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("3. Xóa các Object mô phỏng", GUILayout.Height(30)))
            {
                ClearScene();
            }
        }

        public static void ClearScene()
        {
            string[] rootNames = new string[] { "[Environment]", "[Parking_Area]", "[Passenger_Queue]", "[Track_Waypoints]", "[Passenger_Board]" };
            foreach (string name in rootNames)
            {
                GameObject obj = GameObject.Find(name);
                if (obj != null)
                {
                    Undo.DestroyObjectImmediate(obj);
                }
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("<color=yellow>[DouyinSceneSpawner] Đã dọn dẹp các object mô phỏng!</color>");
        }

        public static void SpawnFullScene()
        {
            ClearScene();

            Debug.Log("<color=cyan>=== BẮT ĐẦU SINH VÀ BỐ TRÍ OBJECT MÔ PHỎNG THEO COCOS CREATOR ===</color>");

            // 1. Setup Camera & Lighting
            SetupCameraAndLight();

            // 2. Setup Environment (Mặt đường, vòng cung rãnh xám, vòm trạm, cây cảnh, đèn & barie)
            SetupEnvironment();

            // 3. Setup Parking Grid (Bãi đỗ xe chứa các Car theo công thức Cocos 5 cột)
            SetupParkingGrid();

            // 4. Setup Passenger Queue (Hàng khách chờ đón xe)
            SetupPassengerQueue();

            // 5. Setup Track Waypoints (Đường cong khép kín quanh map)
            SetupTrackWaypoints();

            // 6. Setup Passenger Board (Bảng tranh hạt cườm hành khách - Level 4 Dolphin)
            SetupPassengerBoard();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("<color=green>=== HOÀN TẤT SINH OBJECT VÀ ĐÃ LƯU SCENE THÀNH CÔNG! ===</color>");
        }

        private static void SetupCameraAndLight()
        {
            // --- MAIN CAMERA ---
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                cam = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }

            cam.transform.position = new Vector3(0f, 48f, -28.5f);
            cam.transform.rotation = Quaternion.Euler(58.5f, 0f, 0f);
            cam.orthographic = true;
            cam.orthographicSize = 30.84f; // Chính xác từ scene: 30.839155f
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 150f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(49f / 255f, 77f / 255f, 121f / 255f, 1f); // #314D79

            // --- DIRECTIONAL LIGHT ---
            Light dirLight = Object.FindFirstObjectByType<Light>();
            if (dirLight == null || dirLight.type != LightType.Directional)
            {
                GameObject lightObj = new GameObject("Directional Light");
                dirLight = lightObj.AddComponent<Light>();
                dirLight.type = LightType.Directional;
            }

            dirLight.transform.position = new Vector3(0f, 20f, 0f);
            dirLight.transform.rotation = Quaternion.Euler(106.36f, 0f, 0f);
            dirLight.color = Color.white;
            dirLight.intensity = 1.2f;
            dirLight.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(1f, 1f, 1f, 0.8f);
            RenderSettings.ambientEquatorColor = new Color(209f / 255f, 192f / 255f, 186f / 255f, 1f);
            RenderSettings.ambientGroundColor = new Color(157f / 255f, 147f / 255f, 138f / 255f, 1f);
        }

        private static void SetupEnvironment()
        {
            GameObject envRoot = new GameObject("[Environment]");
            Undo.RegisterCreatedObjectUndo(envRoot, "Create [Environment]");

            // 1. Purple Road Base (Bãi đỗ xe phía dưới)
            Texture2D purpleRoadTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Douyin_Extracted/textures/Map_Road_Purple_Base.png");
            Material purpleRoadMat = CreateMaterialWithTexture("Mat_Road_Purple_Base", purpleRoadTex);

            GameObject purpleBase = GameObject.CreatePrimitive(PrimitiveType.Quad);
            purpleBase.name = "Ground_Purple_Base";
            purpleBase.transform.SetParent(envRoot.transform, false);
            purpleBase.transform.localPosition = new Vector3(0f, 0f, 8.5f);
            purpleBase.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            purpleBase.transform.localScale = new Vector3(18f, 22f, 1f);
            Object.DestroyImmediate(purpleBase.GetComponent<Collider>());
            if (purpleRoadMat != null) purpleBase.GetComponent<MeshRenderer>().sharedMaterial = purpleRoadMat;

            // 2. Track Loop Grey Slot (Vòng cua rãnh xám phía trên)
            Texture2D trackTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Douyin_Extracted/textures/Track_Loop_Grey_Slot.png");
            Material trackMat = CreateMaterialWithTexture("Mat_Track_Grey_Slot", trackTex);

            GameObject trackLoop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            trackLoop.name = "Track_Loop_Grey";
            trackLoop.transform.SetParent(envRoot.transform, false);
            trackLoop.transform.localPosition = new Vector3(0f, 0.02f, -2.5f);
            trackLoop.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            trackLoop.transform.localScale = new Vector3(16f, 12.5f, 1f);
            Object.DestroyImmediate(trackLoop.GetComponent<Collider>());
            if (trackMat != null) trackLoop.GetComponent<MeshRenderer>().sharedMaterial = trackMat;

            // 3. Canopy Arch (Mái vòm trạm đón xe)
            Texture2D canopyTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Douyin_Extracted/textures/Map_Canopy_Arch.png");
            Material canopyMat = CreateMaterialWithTexture("Mat_Canopy_Arch", canopyTex);

            GameObject canopy = GameObject.CreatePrimitive(PrimitiveType.Quad);
            canopy.name = "Canopy_Station";
            canopy.transform.SetParent(envRoot.transform, false);
            canopy.transform.localPosition = new Vector3(5.8f, 2.2f, -0.5f);
            canopy.transform.localRotation = Quaternion.Euler(45f, -90f, 0f);
            canopy.transform.localScale = new Vector3(5f, 3.5f, 1f);
            Object.DestroyImmediate(canopy.GetComponent<Collider>());
            if (canopyMat != null) canopy.GetComponent<MeshRenderer>().sharedMaterial = canopyMat;

            // 4. Bonsai Trees (Cây cảnh bên đường)
            Texture2D treeTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Douyin_Extracted/textures/Map_Tree_Planter.png");
            Material treeMat = CreateMaterialWithTexture("Mat_Tree_Planter", treeTex);

            float[] treeZPositions = new float[] { -5f, 0f, 5f, 10f, 15f };
            foreach (float z in treeZPositions)
            {
                CreateTreePlanter(envRoot.transform, new Vector3(-8.2f, 0.7f, z), treeMat);
                CreateTreePlanter(envRoot.transform, new Vector3(8.2f, 0.7f, z), treeMat);
            }

            // 5. Traffic Light (model_01)
            Mesh lightMesh = LoadMesh("model_01_skinned_541v_988t");
            if (lightMesh != null)
            {
                GameObject trafficLight = new GameObject("Traffic_Light");
                trafficLight.transform.SetParent(envRoot.transform, false);
                trafficLight.transform.localPosition = new Vector3(4.6f, 0f, -6f);
                trafficLight.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                MeshFilter mf = trafficLight.AddComponent<MeshFilter>();
                MeshRenderer mr = trafficLight.AddComponent<MeshRenderer>();
                mf.sharedMesh = lightMesh;
                mr.sharedMaterial = GetOrCreateSimpleMaterial("Mat_TrafficLight", new Color(0.2f, 0.2f, 0.2f, 1f));
            }

            // 6. Barrier Gate (model_02)
            Mesh gateMesh = LoadMesh("model_02_skinned_584v_1032t");
            if (gateMesh != null)
            {
                GameObject barrier = new GameObject("Barrier_Gate");
                barrier.transform.SetParent(envRoot.transform, false);
                barrier.transform.localPosition = new Vector3(3.2f, 0f, -6.2f);
                barrier.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                MeshFilter mf = barrier.AddComponent<MeshFilter>();
                MeshRenderer mr = barrier.AddComponent<MeshRenderer>();
                mf.sharedMesh = gateMesh;
                mr.sharedMaterial = GetOrCreateSimpleMaterial("Mat_BarrierGate", new Color(0.9f, 0.7f, 0.1f, 1f));
            }
        }

        private static void CreateTreePlanter(Transform parent, Vector3 localPos, Material mat)
        {
            GameObject tree = GameObject.CreatePrimitive(PrimitiveType.Quad);
            tree.name = "Tree_Planter";
            tree.transform.SetParent(parent, false);
            tree.transform.localPosition = localPos;
            tree.transform.localRotation = Quaternion.Euler(58.5f, 0f, 0f); // Nghiêng nhìn thẳng vào Camera
            tree.transform.localScale = new Vector3(1.5f, 1.8f, 1f);
            Object.DestroyImmediate(tree.GetComponent<Collider>());
            if (mat != null) tree.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static void SetupParkingGrid()
        {
            GameObject parkRoot = new GameObject("[Parking_Area]");
            Undo.RegisterCreatedObjectUndo(parkRoot, "Create [Parking_Area]");

            GameObject carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Car.prefab");
            GameObject extraCarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Extra Car.prefab");
            if (carPrefab == null)
            {
                Debug.LogError("[DouyinSceneSpawner] Không tìm thấy Car.prefab tại Assets/Prefabs/Car.prefab!");
                return;
            }

            // Tải danh sách các Material màu sắc xe trong Assets/Material/Bus
            string[] colorNames = new string[]
            {
                "blue", "red", "yellow", "green", "purple", "cyan", "orange", "pink"
            };
            Dictionary<string, Material> colorMats = new Dictionary<string, Material>();
            foreach (string c in colorNames)
            {
                Material m = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Material/Bus/{c}.mat");
                if (m != null) colorMats[c] = m;
            }

            // Ma trận bố trí xe mô phỏng Level thực tế (5 cột x 4 hàng)
            // Tương đương cấu hình Cocos levelCfg.bus
            string[][] levelLayout = new string[][]
            {
                new string[] { "blue", "yellow", "red", "green" },    // Cột 0
                new string[] { "red", "green", "blue", "cyan" },      // Cột 1
                new string[] { "purple", "yellow", "orange", "blue" }, // Cột 2
                new string[] { "green", "red", "purple", "yellow" },  // Cột 3
                new string[] { "yellow", "cyan", "red", "purple" }    // Cột 4
            };

            int colCount = 5;
            int rowCount = 4;
            // Công thức tính toán tọa độ chính xác từ Cocos LevelController.initEntitySpace:
            // spaceX = Math.Min(1.6, 5.6 / (colCount - 1)) = 1.4f
            // spaceZ = 1.8f
            // startX = -(colCount - 1) * spaceX / 2 + 0.75f = -2.05f
            // baseZ = 6.0f
            float spaceX = Mathf.Min(1.6f, 5.6f / (colCount - 1));
            float spaceZ = 2.4f; // Khoảng cách hàng xe để xe không đè lên nhau
            float startX = -(colCount - 1) * spaceX / 2f;
            float baseZ = 4.5f;

            for (int col = 0; col < colCount; col++)
            {
                GameObject colGroup = new GameObject($"Column_{col}");
                colGroup.transform.SetParent(parkRoot.transform, false);

                for (int row = 0; row < rowCount; row++)
                {
                    Vector3 carPos = new Vector3(startX + col * spaceX, 0f, baseZ + row * spaceZ);

                    // Sử dụng Car.prefab (hoặc Extra Car ở hàng cuối cho phong phú)
                    GameObject targetPrefab = (row == 3 && extraCarPrefab != null) ? extraCarPrefab : carPrefab;
                    GameObject carObj = (GameObject)PrefabUtility.InstantiatePrefab(targetPrefab, colGroup.transform);
                    carObj.name = $"Car_C{col}_R{row}";
                    carObj.transform.localPosition = carPos;
                    carObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // Quay đầu hướng về phía trước

                    // Đổi màu phần Body của xe
                    string colorKey = levelLayout[col][row];
                    if (colorMats.TryGetValue(colorKey, out Material carMat))
                    {
                        Transform bodyTrans = carObj.transform.Find("Body");
                        if (bodyTrans != null)
                        {
                            MeshRenderer mr = bodyTrans.GetComponent<MeshRenderer>();
                            if (mr != null)
                            {
                                mr.sharedMaterial = carMat;
                            }
                        }
                    }

                    // Tạo khung ô đỗ xe dưới sàn (Parking Slot Frame)
                    CreateParkingSlotFrame(colGroup.transform, carPos + new Vector3(0f, 0.01f, 0f));
                }
            }
        }

        private static void CreateParkingSlotFrame(Transform parent, Vector3 localPos)
        {
            GameObject slot = GameObject.CreatePrimitive(PrimitiveType.Quad);
            slot.name = "Slot_Frame";
            slot.transform.SetParent(parent, false);
            slot.transform.localPosition = localPos;
            slot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            slot.transform.localScale = new Vector3(1.3f, 2.2f, 1f);
            Object.DestroyImmediate(slot.GetComponent<Collider>());

            Material slotMat = GetOrCreateSimpleMaterial("Mat_Slot_Border", new Color(0.85f, 0.88f, 0.95f, 0.35f));
            slot.GetComponent<MeshRenderer>().sharedMaterial = slotMat;
        }

        private static void SetupPassengerQueue()
        {
            GameObject queueRoot = new GameObject("[Passenger_Queue]");
            Undo.RegisterCreatedObjectUndo(queueRoot, "Create [Passenger_Queue]");

            // Danh sách màu khách tương ứng với các xe đi đầu
            string[] queueColors = new string[]
            {
                "blue", "blue", "blue", "blue",
                "red", "red", "red", "red",
                "purple", "purple", "purple", "purple"
            };

            Dictionary<string, Material> colorMats = new Dictionary<string, Material>();
            string[] keys = new string[] { "blue", "red", "purple" };
            foreach (string k in keys)
            {
                Material m = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Material/Bus/{k}.mat");
                if (m != null) colorMats[k] = m;
            }

            Vector3 startPos = new Vector3(4.8f, 0.45f, -0.5f);
            float stepZ = 0.75f;

            for (int i = 0; i < queueColors.Length; i++)
            {
                GameObject passenger = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                passenger.name = $"Passenger_{i}_{queueColors[i]}";
                passenger.transform.SetParent(queueRoot.transform, false);
                passenger.transform.localPosition = startPos + new Vector3(0f, 0f, i * stepZ);
                passenger.transform.localScale = new Vector3(0.4f, 0.45f, 0.4f);
                Object.DestroyImmediate(passenger.GetComponent<Collider>());

                string colorKey = queueColors[i];
                if (colorMats.TryGetValue(colorKey, out Material pMat))
                {
                    passenger.GetComponent<MeshRenderer>().sharedMaterial = pMat;
                }
            }
        }

        private static void SetupTrackWaypoints()
        {
            GameObject trackRoot = new GameObject("[Track_Waypoints]");
            Undo.RegisterCreatedObjectUndo(trackRoot, "Create [Track_Waypoints]");

            Vector3[] loopWaypoints = new Vector3[]
            {
                new Vector3(0f, 0.1f, -4.5f),    // Xuất phát từ bãi
                new Vector3(-4.5f, 0.1f, -4.5f), // Góc trái dưới
                new Vector3(-4.5f, 0.1f, 1.5f),  // Làn trái lên
                new Vector3(-2.0f, 0.1f, 3.8f),  // Cung cua trên đỉnh
                new Vector3(2.0f, 0.1f, 3.8f),   // Cung cua trên đỉnh
                new Vector3(4.5f, 0.1f, 1.5f),   // Làn phải (trạm đón khách)
                new Vector3(4.5f, 0.1f, -4.5f),  // Cổng ra (Traffic Light & Barrier)
                new Vector3(8.0f, 0.1f, -4.5f),  // Đường thoát 1
                new Vector3(12.0f, 0.1f, -4.5f)  // Đường thoát ra ngoài
            };

            for (int i = 0; i < loopWaypoints.Length; i++)
            {
                GameObject wp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                wp.name = $"WP_{i}";
                wp.transform.SetParent(trackRoot.transform, false);
                wp.transform.localPosition = loopWaypoints[i];
                wp.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
                Object.DestroyImmediate(wp.GetComponent<Collider>());

                Material wpMat = GetOrCreateSimpleMaterial("Mat_Waypoint", new Color(0.2f, 1f, 0.3f, 0.8f));
                wp.GetComponent<MeshRenderer>().sharedMaterial = wpMat;
            }
        }

        private static Material CreateMaterialWithTexture(string matName, Texture2D tex)
        {
            if (tex == null) return null;

            string matFolder = "Assets/Material/Environment";
            if (!Directory.Exists(matFolder))
            {
                Directory.CreateDirectory(matFolder);
            }

            string matPath = $"{matFolder}/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") 
                         ?? Shader.Find("Unlit/Transparent") 
                         ?? Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }

            mat.mainTexture = tex;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material GetOrCreateSimpleMaterial(string matName, Color color)
        {
            string matFolder = "Assets/Material/Environment";
            if (!Directory.Exists(matFolder)) Directory.CreateDirectory(matFolder);

            string matPath = $"{matFolder}/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") 
                         ?? Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(shader);
                mat.color = color;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            return mat;
        }

        private static void SetupPassengerBoard()
        {
            GameObject boardRoot = new GameObject("[Passenger_Board]");
            Undo.RegisterCreatedObjectUndo(boardRoot, "Create [Passenger_Board]");

            // Đặt bảng nằm khớp vị trí vòng cong đường đón xe phía trên
            boardRoot.transform.localPosition = new Vector3(0f, 0.05f, -2.5f);
            boardRoot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            boardRoot.transform.localScale = Vector3.one * 1.0f;

            PassengerBoardGenerator gen = boardRoot.AddComponent<PassengerBoardGenerator>();
            gen.columns = 40;
            gen.rows = 36;
            gen.beadRadius = 0.14f;
            gen.useHexStagger = true;
            gen.cutRoadLoop = true;
            gen.pattern = PassengerBoardGenerator.PresetPattern.Level_04_Dolphin;

            // Load Bead Material
            string matPath = "Assets/Material/Mat_PassengerBead.mat";
            Material beadMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (beadMat == null)
            {
                Shader shader = Shader.Find("Douyin/PassengerBoardBead") 
                             ?? Shader.Find("Sprites/Default") 
                             ?? Shader.Find("Universal Render Pipeline/Unlit");
                beadMat = new Material(shader);
                Texture2D beadTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Bead_Circle.png");
                if (beadTex != null) beadMat.mainTexture = beadTex;

                string matDir = "Assets/Material";
                if (!Directory.Exists(matDir)) Directory.CreateDirectory(matDir);
                AssetDatabase.CreateAsset(beadMat, matPath);
            }
            gen.beadMaterial = beadMat;

            gen.GenerateBoard();
        }

        private static Mesh LoadMesh(string meshName)
        {
            string[] guids = AssetDatabase.FindAssets($"{meshName} t:Mesh");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<Mesh>(path);
            }
            return null;
        }
    }
}
#endif
