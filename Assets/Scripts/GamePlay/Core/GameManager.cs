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

        [Header("Tham Chiếu Scene (kéo thả)")]
        [SerializeField] private SandBoardManager sandBoard;
        [SerializeField] private LoopTrackSystem loopTrack;
        [SerializeField] private ParkingLotManager parkingLot;
        [SerializeField] private EnvironmentController envController;

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
        /// Nạp và kích hoạt Level tương ứng (1, 2, 3...)
        /// </summary>
        public void LoadAndStartLevel(int levelNumber)
        {
            if (sandBoard == null || loopTrack == null || parkingLot == null || envController == null)
            {
                Debug.LogError("[GameManager] Thiếu tham chiếu scene (sandBoard / loopTrack / parkingLot / envController). Hãy kéo vào Inspector.", this);
                return;
            }

            currentLevelNumber = levelNumber;
            int targetLevelId = levelNumber;
            Debug.Log($"<color=cyan>[GameManager]</color> Đang nạp Level {levelNumber}...");

            var levelData = WhoGetInBus.Data.LevelConfigLoader.LoadLevel(targetLevelId);
            if (levelData == null)
            {
                Debug.LogWarning($"[GameManager] Không tìm thấy dữ liệu cho Level {targetLevelId}, chuyển về mặc định Level 1!");
                targetLevelId = 1;
                levelData = WhoGetInBus.Data.LevelConfigLoader.LoadLevel(targetLevelId);
            }

            sandBoard.LoadLevelDirect(targetLevelId, prefabData);

            int roadId = levelData != null ? levelData.road : 5;
            loopTrack.LoadRoadFromConfig(roadId, prefabData);
            loopTrack.currentLevel = levelData; 

            parkingLot.LoadLevelDirect(targetLevelId, prefabData);

            envController.Apply(levelData);
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
    }
}
