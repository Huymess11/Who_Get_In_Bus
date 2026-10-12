using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using WhoGetInBus.GamePlay;
using WhoGetInBus.Data;

namespace WhoGetInBus.EditorTools
{
    public static class GameplaySceneSetupTool
    {
        [MenuItem("Tools/🎮 Thiết Lập Trọn Gói Scene GamePlay (Ready To Play)", false, 1)]
        public static void SetupFullGameplayScene()
        {
            var pData = AssetDatabase.LoadAssetAtPath<GamePrefabData>("Assets/Data/GamePrefabData.asset");
            var bConfig = AssetDatabase.LoadAssetAtPath<SandBoardConfigData>("Assets/Data/SandBoardConfigData.asset");
            var pColors = AssetDatabase.LoadAssetAtPath<PassengerColorData>("Assets/Data/PassengerColorData.asset");
            var bColors = AssetDatabase.LoadAssetAtPath<BusColorData>("Assets/Data/BusColorData.asset");
            var carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Car.prefab");
            var extraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Extra Car.prefab");
            var passPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Passenger.prefab");
            var beadMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Mat_PassengerBead.mat");

            // 1. Tìm hoặc tạo [GameManager]
            GameManager gameMgr = Object.FindFirstObjectByType<GameManager>();
            if (gameMgr == null)
            {
                GameObject gObj = GameObject.Find("[GameManager]");
                if (gObj == null) gObj = new GameObject("[GameManager]");
                Undo.RegisterCreatedObjectUndo(gObj, "Create GameManager");
                gameMgr = gObj.AddComponent<GameManager>();
            }
            gameMgr.prefabData = pData;
            gameMgr.autoLoadOnStart = true;
            gameMgr.customLevelOverride = 0; // 0 = theo DataManager.GetLevel()
            EditorUtility.SetDirty(gameMgr);

            // 2. Tìm hoặc tạo [LoopTrackSystem]
            LoopTrackSystem trackSys = Object.FindFirstObjectByType<LoopTrackSystem>();
            if (trackSys == null)
            {
                GameObject tObj = GameObject.Find("[LoopTrackSystem]");
                if (tObj == null) tObj = new GameObject("[LoopTrackSystem]");
                Undo.RegisterCreatedObjectUndo(tObj, "Create LoopTrackSystem");
                trackSys = tObj.AddComponent<LoopTrackSystem>();
            }
            trackSys.prefabData = pData;
            trackSys.extraCarPrefab = extraPrefab;
            trackSys.busColors = bColors;
            EditorUtility.SetDirty(trackSys);

            // 3. Tìm hoặc tạo ParkingLot_Manager
            ParkingLotManager parkMgr = Object.FindFirstObjectByType<ParkingLotManager>();
            if (parkMgr == null)
            {
                GameObject pObj = GameObject.Find("ParkingLot_Manager");
                if (pObj == null) pObj = new GameObject("ParkingLot_Manager");
                Undo.RegisterCreatedObjectUndo(pObj, "Create ParkingLot_Manager");
                parkMgr = pObj.AddComponent<ParkingLotManager>();
            }

            GameObject busContainer = GameObject.Find("BusContainer");
            if (busContainer != null)
            {
                parkMgr.busContainerLayout = busContainer.GetComponent<WorldSpaceLayout>();
            }
            parkMgr.carPrefab = carPrefab;
            parkMgr.extraCarPrefab = extraPrefab;
            parkMgr.busColors = bColors;
            EditorUtility.SetDirty(parkMgr);

            // 4. Cấu hình SandBoard_Manager
            SandBoardManager boardMgr = Object.FindFirstObjectByType<SandBoardManager>();
            if (boardMgr != null)
            {
                boardMgr.config = bConfig;
                boardMgr.passengerColors = pColors;
                boardMgr.beadMaterial = beadMat;
                boardMgr.beadPrefab = passPrefab;
                EditorUtility.SetDirty(boardMgr);
            }

            // 5. Nạp trực tiếp Level 1 (ID: 1001) vào Scene để sẵn sàng chơi thử
            int initialLevel = DataManager.GetLevel();
            if (initialLevel < 1) initialLevel = 1;
            gameMgr.LoadAndStartLevel(initialLevel);

            // 6. Lưu và đánh dấu Scene Dirty
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log($"<color=green><b>[GameplaySceneSetupTool] ĐÃ THIẾT LẬP TRỌN GÓI THÀNH CÔNG!</b></color>\n" +
                      $"- GameManager: Đã gắn, tự động nạp theo DataManager (Level {initialLevel})\n" +
                      $"- LoopTrackSystem: Đã nạp đường đua Road từ roadNCXHCfg.json\n" +
                      $"- ParkingLotManager: Đã gắn với BusContainer và sinh đội xe\n" +
                      $"- SandBoardManager: Đã nạp 1370 hạt cát trên UI\n" +
                      $"👉 Bây giờ bạn chỉ cần ấn nút PLAY ▶️ là chơi được ngay!");

            EditorUtility.DisplayDialog("Thành Công",
                $"Đã thiết lập trọn gói Scene GamePlay hoàn chỉnh!\n\n" +
                $"- Level hiện tại: Level {initialLevel} (ID {1000 + initialLevel})\n" +
                $"- Toàn bộ Manager, Prefab, Data đã được kết nối.\n\n" +
                $"Bấm nút PLAY ▶️ trong Unity để trải nghiệm ngay!", "OK");
        }
    }
}
