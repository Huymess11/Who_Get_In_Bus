using TMPro;
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

        [Header("UI Số Xe Trên Đường (Cocos GameUI.quantityLabel = curSelectCount/iniCapacity)")]
        [Tooltip("Hiển thị \"số xe đang chạy chưa đầy / số chỗ tối đa\". Xe xuất bến → +1, xe đầy khách (done) → -1, gộp 3 xe → -2")]
        [SerializeField] private TextMeshProUGUI CarCount;
        [Tooltip("Màu nhấp nháy khi đường đã kín chỗ (Cocos: #a06838 ↔ màu gốc, 1.2s)")]
        [SerializeField] private Color carCountFullColor = new Color32(0xa0, 0x68, 0x38, 0xff);

        private int shownSelectedCount = -1;
        private int shownCapacity = -1;
        private Color carCountBaseColor = Color.white;
        private Coroutine carCountPulse;

        private void Awake()
        {
            Instance = this;
            if (CarCount != null) carCountBaseColor = CarCount.color;
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

            UpdateCarCountLabel(true);
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
            shownSelectedCount = -1;
            shownCapacity = -1;
            UpdateCarCountLabel(false);
            StartGame();
        }

        #region UI SỐ XE TRÊN ĐƯỜNG (Cocos GameUI.refreshUnlockSlotLabel)

        /// <summary>
        /// Số xe chiếm chỗ lấy từ LoopTrackSystem.SelectedCartCount (xe đang lao ra + đang chạy chưa đầy + xe đang bay vào gộp).
        /// Chỉ cập nhật khi giá trị đổi; animate = true thì nhãn nảy 1 → 1.2 → 1 (0.2s mỗi chiều), đổi chữ ở đỉnh nảy như Cocos.
        /// </summary>
        private void UpdateCarCountLabel(bool animate)
        {
            if (CarCount == null || loopTrack == null) return;

            int selected = loopTrack.SelectedCartCount;
            int capacity = loopTrack.DeployableCartCount;

            // Kín chỗ: màu chữ nhấp nháy giữa màu gốc và carCountFullColor (Cocos: 1.2s mỗi chiều, lặp vô hạn)
            CarCount.color = selected >= capacity
                ? Color.Lerp(carCountBaseColor, carCountFullColor, Mathf.PingPong(Time.time / 1.2f, 1f))
                : carCountBaseColor;

            if (selected == shownSelectedCount && capacity == shownCapacity) return;
            shownSelectedCount = selected;
            shownCapacity = capacity;

            string text = $"{selected}/{capacity}";
            if (carCountPulse != null) StopCoroutine(carCountPulse);
            CarCount.rectTransform.localScale = Vector3.one;

            if (animate && isActiveAndEnabled) carCountPulse = StartCoroutine(CarCountPulseRoutine(text));
            else CarCount.text = text;
        }

        private System.Collections.IEnumerator CarCountPulseRoutine(string text)
        {
            var rt = CarCount.rectTransform;
            const float half = 0.2f;
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 1.2f, t / half);
                yield return null;
            }

            CarCount.text = text;

            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                rt.localScale = Vector3.one * Mathf.Lerp(1.2f, 1f, t / half);
                yield return null;
            }
            rt.localScale = Vector3.one;
            carCountPulse = null;
        }

        #endregion

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

            CheckVictoryByCars();
        }

        /// <summary>
        /// Điều kiện thắng của game gốc (GameUI.checkVictory): bãi xe đã hết xe và không còn xe nào trên đường
        /// (hoặc cát đã hết sạch — xem SandBoardManager). Cát còn thừa vài hạt do dữ liệu level lệch ghế thì vẫn thắng.
        /// </summary>
        private void CheckVictoryByCars()
        {
            if (currentState != PlayState.Playing) return;
            if (parkingLot.RemainingCarCount == 0 && loopTrack.IsTrackEmpty)
            {
                OnLevelVictory();
            }
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
