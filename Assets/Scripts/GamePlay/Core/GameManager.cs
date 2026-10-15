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

        [Tooltip("Tắt (mặc định): giữ nguyên kích thước ảnh Top trong scene. Bật: tính lại chiều cao ảnh Top theo đúng tỉ lệ ảnh như game gốc.")]
        public bool matchOriginalRoadArtProportion = false;

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

            if (prefabData == null)
            {
#if UNITY_EDITOR
                prefabData = UnityEditor.AssetDatabase.LoadAssetAtPath<GamePrefabData>("Assets/Data/GamePrefabData.asset");
#endif
            }

            // 1. Nạp Bàn Cát UI
            var board = SandBoardManager.Instance != null ? SandBoardManager.Instance : FindFirstObjectByType<SandBoardManager>();
            if (board != null)
            {
                board.LoadLevelDirect(targetLevelId, prefabData);
            }

            // 2. Nạp Tuyến Đường Đua Loop & Cổng đón khách
            int roadId = levelData != null ? levelData.road : 5;
            var track = LoopTrackSystem.Instance != null ? LoopTrackSystem.Instance : FindFirstObjectByType<LoopTrackSystem>();
            if (track != null)
            {
                track.LoadRoadFromConfig(roadId, prefabData);
                track.currentLevel = levelData; // minibus_tran / bus_tran: số hạt xe hút mỗi lượt
            }

            // 3. Nạp Bãi Xe
            var park = ParkingLotManager.Instance != null ? ParkingLotManager.Instance : FindFirstObjectByType<ParkingLotManager>();
            if (park != null)
            {
                park.LoadLevelDirect(targetLevelId, prefabData);
            }

            // 4. Cập nhật ảnh mặt đường theo Môi trường Level (Top & Down Road)
            UpdateRoadEnvironmentVisuals(levelData);

            // 5. Bắt đầu tính giờ và kích hoạt chơi
            StartGame();
        }

        /// <summary>
        /// Tự động cập nhật ảnh Down Road và Top Road theo loại môi trường (bgStyle) và loại đường (road)
        /// </summary>
        private void UpdateRoadEnvironmentVisuals(WhoGetInBus.Data.LevelData levelData)
        {
            if (levelData == null) return;
            var roadEnv = prefabData != null ? prefabData.roadEnvironmentData : null;
            if (roadEnv == null)
            {
#if UNITY_EDITOR
                roadEnv = UnityEditor.AssetDatabase.LoadAssetAtPath<WhoGetInBus.Data.RoadEnvironmentData>("Assets/Data/RoadEnvironmentData.asset");
#endif
            }
            if (roadEnv == null) return;

            Sprite downSp = roadEnv.GetDownRoad(levelData.bgStyle);
            Sprite topSp = roadEnv.GetTopRoad(levelData.bgStyle, levelData.road);

            // 1. Gán vào Map 3D SpriteRenderer (dưới GameObject ---Map---)
            var mapRoot = GameObject.Find("---Map---");
            if (mapRoot != null)
            {
                var bottomTrans = mapRoot.transform.Find("Bottom");
                if (bottomTrans != null)
                {
                    var sr = bottomTrans.GetComponent<SpriteRenderer>();
                    if (sr != null && downSp != null) sr.sprite = downSp;
                }

                var topTrans = mapRoot.transform.Find("Top");
                if (topTrans != null)
                {
                    var sr = topTrans.GetComponent<SpriteRenderer>();
                    if (sr != null && topSp != null) sr.sprite = topSp;
                }
            }

            // 2. Gán vào Map Canvass UI Image nếu đang hiển thị qua Canvas
            var canvasRoot = GameObject.Find("Map Canvass");
            if (canvasRoot != null)
            {
                var bottomImg = canvasRoot.transform.Find("Bottom")?.GetComponent<UnityEngine.UI.Image>();
                if (bottomImg != null && downSp != null) bottomImg.sprite = downSp;

                var topImg = canvasRoot.transform.Find("Top")?.GetComponent<UnityEngine.UI.Image>();
                if (topImg != null && topSp != null)
                {
                    topImg.sprite = topSp;

                    // Game gốc hiển thị ảnh đường ĐỀU 2 trục; ảnh Top trong scene đang bị ép dọc (700px thay vì ~777px).
                    // Mặc định GIỮ NGUYÊN kích thước scene. Bật cờ để về đúng tỉ lệ gốc (khi đó dùng scaleZ 0.852, offsetZ +0.229).
                    var topRect = topImg.rectTransform;
                    if (matchOriginalRoadArtProportion && topSp.rect.width > 0f)
                    {
                        float width = topRect.sizeDelta.x;
                        topRect.sizeDelta = new Vector2(width, width * topSp.rect.height / topSp.rect.width);
                    }
                }
            }

            Debug.Log($"<color=cyan>[GameManager]</color> Đã áp dụng ảnh mặt đường: Environment #{levelData.bgStyle} (Down Road), Road #{levelData.road} (Top Road)!");
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
