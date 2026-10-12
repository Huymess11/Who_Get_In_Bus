using System.Collections.Generic;
using UnityEngine;

namespace WhoGetInBus.GamePlay
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public enum PlayState
        {
            Idle,
            Playing,
            Victory,
            Failed
        }

        [Header("Trạng Thái Trò Chơi")]
        public PlayState currentState = PlayState.Idle;
        public int completedCarsCount = 0;
        public float playTime = 0f;
        public int currentLevelNumber = 1;

        [Header("Cấu Hình Nạp Level")]
        public GamePrefabData prefabData;
        public bool autoLoadOnStart = true;
        [Tooltip("0 = Nạp tự động theo DataManager.GetLevel(), >0 = Test level cụ thể")]
        public int customLevelOverride = 0;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (autoLoadOnStart)
            {
                int lvl = customLevelOverride > 0 ? customLevelOverride : DataManager.GetLevel();
                LoadAndStartLevel(lvl);
            }
        }

        private void Update()
        {
            if (currentState == PlayState.Playing)
            {
                playTime += Time.deltaTime;
            }
        }

        /// <summary>
        /// Nạp và kích hoạt Level tương ứng chuẩn gốc (1 -> 1001, 2 -> 1002...)
        /// </summary>
        public void LoadAndStartLevel(int levelNumber)
        {
            currentLevelNumber = levelNumber;
            int targetLevelId = 1000 + levelNumber;
            Debug.Log($"<color=cyan>[GameManager]</color> Đang nạp Level {levelNumber} (ID: {targetLevelId})...");

            var levelData = WhoGetInBus.Data.LevelConfigLoader.LoadLevel(targetLevelId);
            if (levelData == null)
            {
                Debug.LogWarning($"[GameManager] Không tìm thấy dữ liệu cho Level {targetLevelId}, chuyển về mặc định 1001!");
                targetLevelId = 1001;
                levelData = WhoGetInBus.Data.LevelConfigLoader.LoadLevel(targetLevelId);
            }

            if (prefabData == null)
            {
#if UNITY_EDITOR
                prefabData = UnityEditor.AssetDatabase.LoadAssetAtPath<GamePrefabData>("Assets/Data/GamePrefabData.asset");
#endif
            }

            // 1. Nạp Bàn Cát UI
            if (SandBoardManager.Instance != null)
            {
                SandBoardManager.Instance.LoadLevelDirect(targetLevelId, prefabData);
            }

            // 2. Nạp Tuyến Đường Đua Loop & Cổng đón khách
            int roadId = levelData != null ? levelData.road : 5;
            if (LoopTrackSystem.Instance != null)
            {
                LoopTrackSystem.Instance.LoadRoadFromConfig(roadId, prefabData);
            }

            // 3. Nạp Bãi Xe
            if (ParkingLotManager.Instance != null)
            {
                ParkingLotManager.Instance.LoadLevelDirect(targetLevelId, prefabData);
            }

            // 4. Bắt đầu tính giờ và kích hoạt chơi
            StartGame();
        }

        public void StartGame()
        {
            currentState = PlayState.Playing;
            completedCarsCount = 0;
            playTime = 0f;
            Debug.Log("<color=green>[GameManager]</color> Trò chơi BẮT ĐẦU! Click vào xe ở bãi đỗ để xuất bến đón khách!");
        }

        public void OnCarCompleted(CarLoopFollower car)
        {
            completedCarsCount++;
            Debug.Log($"<color=green>[GameManager]</color> Xe {car.name} đã gom đủ 100% khách và rời màn thành công! (Tổng xe thoát: {completedCarsCount})");
        }

        public void OnLevelVictory()
        {
            if (currentState != PlayState.Victory)
            {
                currentState = PlayState.Victory;
                Debug.Log($"<color=yellow>★★★ CHÚC MỪNG CHIẾN THẮNG (VICTORY)! ★★★</color> Đã hoàn thành Level {currentLevelNumber} trong {playTime:F1}s!");

                // Tự động mở khóa Level kế tiếp trong DataManager
                DataManager.NextLevel();
                Debug.Log($"<color=green>[GameManager]</color> DataManager đã cập nhật sang Level tiếp theo: Level {DataManager.GetLevel()}!");
            }
        }

        private void OnGUI()
        {
            if (currentState == PlayState.Idle) return;

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            GUIStyle boldLabel = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            GUIStyle miniLabel = new GUIStyle(GUI.skin.label) { fontSize = 11 };

            GUI.backgroundColor = new Color(0.1f, 0.15f, 0.25f, 0.85f);
            GUILayout.BeginArea(new Rect(20, 20, 320, 200), boxStyle);

            GUILayout.Label($"🎮 BUS JAM - LEVEL {currentLevelNumber}", boldLabel);
            GUILayout.Space(2);

            int remainingBeads = SandBoardManager.Instance != null ? SandBoardManager.Instance.TotalRemainingBeads : 0;
            GUILayout.Label($"⏳ Hạt cát còn lại: {remainingBeads}");
            GUILayout.Label($"🚌 Xe hoàn thành thoát màn: {completedCarsCount}");
            GUILayout.Label($"⏱️ Thời gian chơi: {playTime:F1}s");

            GUILayout.Space(4);
            if (currentState == PlayState.Victory)
            {
                GUI.color = Color.green;
                GUILayout.Label("🎉 CHIẾN THẮNG (VICTORY)! 🎉", boldLabel);
                GUI.color = Color.white;

                if (GUILayout.Button("▶️ Chơi Level Tiếp Theo", GUILayout.Height(28)))
                {
                    LoadAndStartLevel(DataManager.GetLevel());
                }
            }
            else
            {
                GUILayout.Label("👉 Click vào xe đầu cột để xuất bến đón khách!", miniLabel);
                if (GUILayout.Button("🔄 Chơi Lại Màn Này", GUILayout.Height(22)))
                {
                    LoadAndStartLevel(currentLevelNumber);
                }
            }

            GUILayout.EndArea();
            GUI.backgroundColor = Color.white;
        }
    }
}
