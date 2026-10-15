using System.Collections.Generic;
using UnityEngine;
using WhoGetInBus.Data;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WhoGetInBus.GamePlay
{
    /// <summary>Trạng thái tốc độ đường đua (Cocos: CartTrackController.updateSpeedState).</summary>
    public enum TrackSpeedState
    {
        Normal,
        Final,
        Full
    }

    /// <summary>Kế hoạch triển khai xe từ bãi đỗ ra đường đua (Cocos: createCartDeployPlan).</summary>
    public class CartDeployPlan
    {
        public bool valid;
        public Vector3 sourceWorld;
        public Vector3 mergeWorld;
        public float mergeDistance;
        public Vector3 mergeTangent;
        public Quaternion targetRotation = Quaternion.identity;

        // Tương thích tên cũ
        public Vector3 mergeWorldPos => mergeWorld;
        public Vector3 entryTangent => mergeTangent;
    }

    /// <summary>Phiếu giữ chỗ nhập làn (Cocos: deployReservations).</summary>
    public class CartDeployReservation
    {
        public CartDeployPlan plan;
        /// <summary>true: không đủ khoảng trống để giữ chỗ → xe vào làn với tốc độ giảm (Cocos: reducedSpeed / passThrough)</summary>
        public bool reducedSpeed;
    }

    /// <summary>
    /// HỆ THỐNG ĐƯỜNG RAY CHỮ U KHÉP KÍN (Loop Track Spline System)
    /// Chuẩn game gốc Cocos Creator 1:1 (CartTrackController & BPathCursor):
    /// - Quản lý spline vòng lặp kín bằng tham số hoá độ dài cung (Arc-Length Parameterized Spline)
    /// - Giúp xe buýt luôn chạy chính xác 100% trên tim đường, không cắt góc, không vẹo đầu
    /// - Tính toán tiếp tuyến hướng nhìn trước (Sample Look-Ahead Tangent) và nghiêng thân xe (Banking Lean)
    /// - Quản lý ngã rẽ lối thoát (Exit Junction), đoạn tăng tốc cuối vòng, hầm (cave) và điểm nhập làn từ bãi đỗ
    /// - Điều phối chuyển động tập trung: hạn mức quãng đường mỗi khung hình (move allowance) có giới hạn theo khoảng cách
    ///   an toàn với xe phía trước (followGapDistance), giống hệt buildMoveAllowances của Cocos
    /// - Vòng quét "ăn cát" của các xe (tickAbsorb / collectAbsorbFlyTasks) với ngân sách quét mỗi nhịp
    /// </summary>
    public class LoopTrackSystem : MonoBehaviour
    {
        public static LoopTrackSystem Instance { get; private set; }

        [Header("📁 File Cấu Hình Dữ Liệu (ScriptableObject Config)")]
        [Tooltip("File ScriptableObject lưu trữ toàn bộ thông số co giãn và căn chỉnh đường đua")]
        public RoadTrackConfigData trackConfig;
        [Tooltip("ID Tuyến đường đang nạp/hiển thị trong editor (1..16)")]
        public int currentRoadId = 5;

        [Header("Waypoints Vòng Lặp Chữ U (Loop Waypoints)")]
        [Tooltip("Danh sách các điểm tạo thành đường cong chữ U khép kín đón khách")]
        public List<Transform> loopWaypoints = new List<Transform>();

        [Header("Cổng Đón Khách (Boarding Gate)")]
        [Tooltip("Vị trí trạm đón khách tiếp giáp chân tranh cát")]
        public Transform boardingGatePoint;

        [Header("Ngã Rẽ & Làn Đường Thoát (Exit Path)")]
        [Tooltip("Điểm ngã rẽ tách khỏi vòng loop khi xe đã đầy khách")]
        public Transform exitJunctionPoint;
        [Tooltip("Các điểm dẫn xe chạy ra khỏi màn hình")]
        public List<Transform> exitWaypoints = new List<Transform>();

        [Header("Điểm Xe Từ Bãi Đỗ Nhập Làn (Spawn Entry)")]
        public Transform parkingEntryWaypoint;
        public int parkingEntryWaypointIdx = 0;

        [Header("Thông Số Chuyển Động Chuẩn (Cocos 1:1)")]
        [Tooltip("Tốc độ đường (roadNCXHCfg.speed) — nạp tự động theo road")]
        public float baseSpeed = 5.0f;
        [Tooltip("Tốc độ xe quay đầu (mũ giảm dần, Cocos: rotateSpeed = 8)")]
        public float rotateSpeed = 8.0f;
        [Tooltip("Khoảng nhìn trước dọc đường để tính hướng đầu xe (Cocos: facingLookAheadDistance = 0.45)")]
        public float lookAheadDistance = 0.45f;
        [Tooltip("Khoảng cách tim-tim tối thiểu giữa 2 xe cùng làn (Cocos: followGapDistance = 1.65)")]
        public float followGapDistance = 1.65f;
        public float maxBankingLean = 6.0f;

        [Header("Hệ Số Tốc Độ (Cocos GameCfgModuleEx)")]
        [Tooltip("emptyCarSpeed: tốc độ khi bãi xe đã hết xe (trạng thái Final)")]
        public float emptyCarSpeed = 8f;
        [Tooltip("accelerateSpeedRatio: hệ số nhân tốc độ ở đoạn cuối vòng (từ điểm áp chót tới điểm đầu)")]
        public float accelerateSpeedRatio = 1.5f;
        [Tooltip("blockedWaitingSpeedRatio: hệ số tốc độ của xe nhập làn bị chặn")]
        public float blockedWaitingSpeedRatio = 0.5f;
        [Tooltip("Tự chuyển Normal ↔ Final theo việc bãi xe còn xe hay không")]
        public bool autoSpeedState = true;
        public TrackSpeedState speedState = TrackSpeedState.Normal;

        [Header("Nhập Làn Từ Bãi Đỗ (Cocos deployToCartTrack)")]
        [Tooltip("Độ nhún (đơn vị) khi xe lao ra đường (Cocos: 0.2)")]
        public float deployArcHeight = 0.17f;
        [Tooltip("Xe có x nhỏ hơn ngưỡng này sẽ dịch điểm nhập làn sang phải (Cocos: deployTurnThresholdX = 3.5, nhân hệ số co X)")]
        public float deployTurnThresholdX = 3.0f;
        [Tooltip("Độ dịch điểm nhập làn sang phải (Cocos: deployTurnStagingOffsetX = 0.35)")]
        public float deployTurnStagingOffsetX = 0.3f;
        [Tooltip("Dung sai khi xác định xe đang ở đúng ngã rẽ / điểm cắt (Cocos: 0.15)")]
        public float junctionEpsilon = 0.15f;

        [Header("Vòng Quét Ăn Cát (Cocos LevelController.tickAbsorb)")]
        [Tooltip("sandArtDetectionInterval: chu kỳ quét (giây)")]
        public float absorbDetectionInterval = 0.025f;
        [Tooltip("absorbScanBudgetPerTick: nếu số xe > ngưỡng này thì mỗi nhịp quét 2 xe, ngược lại 1 xe")]
        public int absorbScanBudgetPerTick = 3;
        [Tooltip("Dữ liệu màn đang chơi (lấy minibus_tran / bus_tran). GameManager gán khi nạp level")]
        [System.NonSerialized] public LevelData currentLevel;

        [Header("Tham Chiếu Scene (kéo thả)")]
        [SerializeField] private SandBoardManager sandBoard;
        [SerializeField] private ParkingLotManager parkingLot;
        [Tooltip("Hệ thống khách bay lên xe (gán passengerPrefab trên chính component đó)")]
        [SerializeField] private PassengerFlightSystem flightSystem;

        [Header("Luật Hợp Thể 3 Xe Cùng Màu (Extra Car Merge)")]
        public GameObject extraCarPrefab;
        public BusColorData busColors;
        public GamePrefabData prefabData;

        [Header("Chuẩn Toạ Độ Cocos -> Unity 1:1")]
        public bool autoCoordinateConvert = true;

        [Header("Hiệu Chỉnh Tỷ Lệ Đường Đua (Fit Track To Road Visual)")]
        [Tooltip("Bật/tắt tự động co giãn toạ độ waypoint khớp với mặt đường vẽ 2D")]
        public bool enableTrackScale = true;
        [Tooltip("Tỷ lệ scale trục X và Z để thu hẹp vừa khít lòng đường nhựa (Mặc định: X = 0.852, Z = 0.767)")]
        public Vector2 trackScale = new Vector2(0.852f, 0.767f);
        [Tooltip("Độ dịch tâm trục X (trái/phải)")]
        public float trackOffsetX = 0f;
        [Tooltip("Độ dịch tâm trục Z (tiến/lùi) để vòm chữ U và đáy vừa khít tim đường")]
        public float trackOffsetZ = -0.018f;
        [Tooltip("Cao độ nâng lên để tránh z-fighting")]
        public float trackHeightOffset = 0f;

        [Header("Dữ Liệu Spline Tuyến Đường (Arc-Length Track Cache)")]
        public List<Vector3> loopPathPoints = new List<Vector3>();
        public float[] loopSegLens;
        public float[] loopPrefixLens;
        public float loopTotalLen;

        public List<Vector3> exitPathPoints = new List<Vector3>();
        public float[] exitSegLens;
        public float[] exitPrefixLens;
        public float exitTotalLen;

        public float exitJunctionDistance = 0f;
        public float parkingEntryDistance = 0f;
        [Tooltip("Khoảng cách trên loop tại điểm áp chót — từ đây xe được nhân tốc độ accelerateSpeedRatio")]
        public float loopBoostStartDistance = 0f;
        public bool hasCavePath = false;
        public float loopCaveEntryDistance = 0f;
        public float loopCaveExitDistance = 0f;

        // Danh sách xe đang hoạt động trên đường
        public List<CarLoopFollower> activeCarsOnTrack = new List<CarLoopFollower>();

        // ---- Runtime ----
        private float moveSpeed = 5f;
        private RoadConfigData currentRoadCfg;
        private float fullSpeedTimer;
        private float absorbTimer;
        private int absorbScanCursor;

        private readonly List<CarLoopFollower> loopingOrder = new List<CarLoopFollower>();
        private readonly List<CarLoopFollower> exitingOrder = new List<CarLoopFollower>();
        private readonly List<CarLoopFollower> removeList = new List<CarLoopFollower>();
        private readonly List<CarLoopFollower> selectScratch = new List<CarLoopFollower>();
        private readonly List<CarLoopFollower> overlapScratch = new List<CarLoopFollower>();
        private readonly List<CarLoopFollower> seekScratch = new List<CarLoopFollower>();
        private readonly List<SandBoardManager.PassengerAbsorbItem> absorbItems = new List<SandBoardManager.PassengerAbsorbItem>(32);
        private readonly List<Vector3> absorbPoints = new List<Vector3>(32);
        private readonly Dictionary<CarLoopFollower, CartDeployReservation> deployReservations = new Dictionary<CarLoopFollower, CartDeployReservation>();
        private readonly HashSet<CarLoopFollower> mergingTails = new HashSet<CarLoopFollower>();
        private readonly List<CarLoopFollower> mergeScratch = new List<CarLoopFollower>();
        private bool mergingThreeInOne;
        private Coroutine mergeCoroutine;

        public float MoveSpeed => moveSpeed;

        /// <summary>Hệ số đổi đơn vị Cocos → Unity (= trackScale.x, mặc định 100px/117.39px = 0.852). Mọi khoảng cách/tốc độ lấy từ Cocos phải nhân hệ số này để giữ đúng tỉ lệ với đường.</summary>
        public float WorldScale => (enableTrackScale && trackScale.x > 0.0001f) ? trackScale.x : 1f;

        /// <summary>Khoảng cách tối thiểu giữa 2 xe (đã đổi sang đơn vị Unity).</summary>
        public float FollowGap => Mathf.Max(0f, followGapDistance) * WorldScale;

        /// <summary>Khoảng nhìn trước (đã đổi sang đơn vị Unity).</summary>
        public float LookAhead => lookAheadDistance * WorldScale;

        /// <summary>Dung sai ngã rẽ/điểm cắt (đã đổi sang đơn vị Unity).</summary>
        public float JunctionEps => junctionEpsilon * WorldScale;

        /// <summary>Tốc độ tối đa của xe trên đường (Cocos: maxMoveSpeed) — cũng là tốc độ xe lao từ bãi ra đường.</summary>
        public float MaxMoveSpeed
        {
            get
            {
                float roadMax = currentRoadCfg != null ? currentRoadCfg.max_speed : 0f;
                return roadMax > 0f ? roadMax * WorldScale * accelerateSpeedRatio : moveSpeed * accelerateSpeedRatio;
            }
        }

        /// <summary>Tốc độ lao ra đường khi bị chặn (Cocos: blockedDeployMoveSpeed).</summary>
        public float BlockedDeployMoveSpeed => MaxMoveSpeed * Mathf.Max(0f, blockedWaitingSpeedRatio);

        public bool PathReady => loopPathPoints != null && loopPathPoints.Count >= 2 && loopSegLens != null && loopSegLens.Length > 0 && loopTotalLen > 0f;

        /// <summary>Không còn xe nào đang chạy hoặc đang lao ra đường (dùng để xét thắng màn).</summary>
        public bool IsTrackEmpty => activeCarsOnTrack.Count == 0 && deployReservations.Count == 0 && mergingTails.Count == 0 && !mergingThreeInOne;

        /// <summary>Số xe tối đa được chạy trên đường cùng lúc (Cocos: deployableCartCount = level.iniCapacity).</summary>
        public int DeployableCartCount => currentLevel != null && currentLevel.iniCapacity > 0 ? currentLevel.iniCapacity : 4;

        /// <summary>
        /// Số xe đang chiếm chỗ trên đường (Cocos: _curSelectCount): xe đang lao ra, đang chạy chưa đầy, xe đang bay vào gộp.
        /// Xe đã đầy (chờ rẽ ra / đang thoát) KHÔNG chiếm chỗ — Cocos removeSelected ngay khi xe đầy.
        /// </summary>
        public int SelectedCartCount
        {
            get
            {
                int n = deployReservations.Count + mergingTails.Count;
                for (int i = 0; i < activeCarsOnTrack.Count; i++)
                {
                    var car = activeCarsOnTrack[i];
                    if (car != null && !car.isPendingExit && !car.isExiting && !deployReservations.ContainsKey(car)) n++;
                }
                return n;
            }
        }

        /// <summary>Còn chỗ cho 1 xe nữa xuất bến không (Cocos: select → deployableCartCount <= _curSelectCount thì từ chối).</summary>
        public bool HasFreeDeploySlot => SelectedCartCount < DeployableCartCount;

        /// <summary>Số hạt còn lại (bàn + hàng đợi) của 1 màu (Cocos: targetController.getRemainingCount).</summary>
        public int GetRemainingSandOfColor(GameColorType color) => sandBoard != null ? sandBoard.GetRemainingCountOfColor(color) : 0;

        /// <summary>Tổng nhu cầu thực (ghế trống trừ khách đang bay) của các xe 1 màu đang ở trên đường / đang lao ra / đang gộp.</summary>
        public int SumRealDemandOfColor(GameColorType color)
        {
            int n = 0;
            for (int i = 0; i < activeCarsOnTrack.Count; i++)
            {
                var car = activeCarsOnTrack[i];
                if (car != null && car.carColor == color) n += car.RealDemand;
            }
            foreach (var car in deployReservations.Keys)
            {
                if (car != null && car.carColor == color && !activeCarsOnTrack.Contains(car)) n += car.RealDemand;
            }
            foreach (var car in mergingTails)
            {
                if (car != null && car.carColor == color) n += car.RealDemand;
            }
            return n;
        }

        private void Awake()
        {
            Instance = this;
            EnsureConfigLoaded();
            if (loopPathPoints.Count > 1 && (loopSegLens == null || loopSegLens.Length == 0))
            {
                RebuildPathCache();
            }
            moveSpeed = baseSpeed * WorldScale;
        }

        public void EnsureConfigLoaded()
        {
            if (trackConfig == null)
            {
#if UNITY_EDITOR
                trackConfig = AssetDatabase.LoadAssetAtPath<RoadTrackConfigData>("Assets/Data/RoadTrackConfigData.asset");
                if (trackConfig == null)
                {
                    if (!System.IO.Directory.Exists("Assets/Data"))
                    {
                        System.IO.Directory.CreateDirectory("Assets/Data");
                    }
                    trackConfig = ScriptableObject.CreateInstance<RoadTrackConfigData>();
                    AssetDatabase.CreateAsset(trackConfig, "Assets/Data/RoadTrackConfigData.asset");
                    AssetDatabase.SaveAssets();
                }
#else
                trackConfig = Resources.Load<RoadTrackConfigData>("RoadTrackConfigData");
#endif
            }
            if (trackConfig != null)
            {
                SyncFromConfig();
            }
        }

        public void SyncFromConfig()
        {
            if (trackConfig == null) return;
            enableTrackScale = trackConfig.enableTrackScale;
            trackScale = trackConfig.TrackScale;
            trackOffsetX = trackConfig.offsetX;
            trackOffsetZ = trackConfig.offsetZ;
            trackHeightOffset = trackConfig.heightOffset;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                RebuildWaypointsFromCurrentConfig();
            }
#endif
        }

        public void SyncToConfig()
        {
            if (trackConfig == null) return;
            trackConfig.enableTrackScale = enableTrackScale;
            trackConfig.scaleX = trackScale.x;
            trackConfig.scaleZ = trackScale.y;
            trackConfig.offsetX = trackOffsetX;
            trackConfig.offsetZ = trackOffsetZ;
            trackConfig.heightOffset = trackHeightOffset;
        }

        public void RebuildWaypointsFromCurrentConfig()
        {
            int rId = currentRoadId > 0 ? currentRoadId : 5;
            var roadData = WhoGetInBus.Data.LevelConfigLoader.GetRoadConfig(rId);
            if (roadData == null) return;
            currentRoadCfg = roadData;

            loopPathPoints = ParseCoordinatesRaw(roadData.loop_path_root);
            exitPathPoints = ParseCoordinatesRaw(roadData.exit_path_root);

            if (loopWaypoints != null && loopWaypoints.Count == loopPathPoints.Count && loopWaypoints.TrueForAll(w => w != null))
            {
                for (int i = 0; i < loopPathPoints.Count; i++)
                {
                    loopWaypoints[i].position = loopPathPoints[i];
                }
            }
            else
            {
                LoadRoadFromConfig(rId);
                return;
            }

            if (exitWaypoints != null && exitWaypoints.Count == exitPathPoints.Count && exitWaypoints.TrueForAll(w => w != null))
            {
                for (int i = 0; i < exitPathPoints.Count; i++)
                {
                    exitWaypoints[i].position = exitPathPoints[i];
                }
            }
            else
            {
                LoadRoadFromConfig(rId);
                return;
            }

            RebuildPathCache();

            if (exitWaypoints.Count > 0 && loopWaypoints.Count > 0)
            {
                exitJunctionPoint = FindClosestWaypoint(loopWaypoints, exitWaypoints[0].position);
            }

            if (loopWaypoints.Count > 0)
            {
                parkingEntryWaypointIdx = 0;
                parkingEntryWaypoint = loopWaypoints[0];
                parkingEntryDistance = FindClosestLoopDistance(parkingEntryWaypoint.position);
            }

            float maxZ = float.MinValue;
            for (int i = 0; i < loopWaypoints.Count; i++)
            {
                if (loopWaypoints[i].position.z > maxZ)
                {
                    maxZ = loopWaypoints[i].position.z;
                    boardingGatePoint = loopWaypoints[i];
                }
            }
        }

        #region ĐĂNG KÝ XE TRÊN ĐƯỜNG

        /// <summary>Thêm xe vào danh sách chạy trên đường + gán khoảng hút hạt theo level (Cocos: registerCartInternal).</summary>
        public void RegisterCarOnTrack(CarLoopFollower car)
        {
            if (car == null) return;
            AddCarToTrack(car);
        }

        private void AddCarToTrack(CarLoopFollower car)
        {
            if (!activeCarsOnTrack.Contains(car))
            {
                activeCarsOnTrack.Add(car);
            }

            // minibus_tran cho xe thường, bus_tran cho xe Extra (3 trong 1)
            var tran = currentLevel != null ? (car.isExtraCar ? currentLevel.bus_tran : currentLevel.minibus_tran) : null;
            car.SetAbsorbChainTran(tran, car.isExtraCar ? 10 : 8, car.isExtraCar ? 15 : 12);
        }

        public void UnregisterCarFromTrack(CarLoopFollower car)
        {
            if (car == null) return;
            activeCarsOnTrack.Remove(car);
            deployReservations.Remove(car);
        }

        /// <summary>Xoá toàn bộ xe/chỗ giữ khi nạp màn mới.</summary>
        public void ResetTrackRuntime()
        {
            if (mergeCoroutine != null) StopCoroutine(mergeCoroutine);
            mergeCoroutine = null;
            mergingThreeInOne = false;
            mergingTails.Clear();
            activeCarsOnTrack.Clear();
            deployReservations.Clear();
            absorbTimer = 0f;
            absorbScanCursor = 0;
            fullSpeedTimer = 0f;
            SetSpeedState(TrackSpeedState.Normal);
            if (flightSystem != null) flightSystem.Clear();
        }

        #endregion

        #region PATH SPLINE EVALUATION (Chuẩn BPathCursor.ts Cocos Creator)

        /// <summary>
        /// Khởi tạo và tính toán độ dài từng phân đoạn, mảng tích lũy độ dài (Prefix Sums)
        /// </summary>
        public void RebuildPathCache()
        {
            if (loopPathPoints == null || loopPathPoints.Count < 2) return;

            // 1. Khép kín vòng loop: đảm bảo điểm cuối nối về điểm đầu
            Vector3 first = loopPathPoints[0];
            Vector3 last = loopPathPoints[loopPathPoints.Count - 1];
            if ((last - first).sqrMagnitude > 0.0001f)
            {
                loopPathPoints.Add(first);
            }

            // 2. Tính toán độ dài phân đoạn và prefix sums cho đường Loop
            int loopSegCount = loopPathPoints.Count - 1;
            loopSegLens = new float[loopSegCount];
            loopPrefixLens = new float[loopSegCount];
            loopTotalLen = 0f;

            for (int i = 0; i < loopSegCount; i++)
            {
                loopPrefixLens[i] = loopTotalLen;
                float len = Vector3.Distance(loopPathPoints[i], loopPathPoints[i + 1]);
                loopSegLens[i] = len;
                loopTotalLen += len;
            }

            // 3. Tính toán độ dài phân đoạn cho đường Exit (hở, không khép kín)
            if (exitPathPoints != null && exitPathPoints.Count >= 2)
            {
                int exitSegCount = exitPathPoints.Count - 1;
                exitSegLens = new float[exitSegCount];
                exitPrefixLens = new float[exitSegCount];
                exitTotalLen = 0f;

                for (int i = 0; i < exitSegCount; i++)
                {
                    exitPrefixLens[i] = exitTotalLen;
                    float len = Vector3.Distance(exitPathPoints[i], exitPathPoints[i + 1]);
                    exitSegLens[i] = len;
                    exitTotalLen += len;
                }

                // Điểm rẽ ra Exit là vị trí trên Loop gần exitPathPoints[0] nhất
                exitJunctionDistance = FindClosestLoopDistance(exitPathPoints[0]);
            }

            // 4. Điểm nhập làn từ bãi đỗ
            if (parkingEntryWaypoint != null)
            {
                parkingEntryDistance = FindClosestLoopDistance(parkingEntryWaypoint.position);
            }
            else
            {
                parkingEntryDistance = 0f;
            }

            // 5. Đoạn tăng tốc cuối vòng (Cocos: cacheLoopBoostStart)
            if (loopPathPoints.Count < 3 || loopPrefixLens.Length < 2)
            {
                loopBoostStartDistance = exitJunctionDistance;
            }
            else
            {
                float t = GetLoopDistanceAtPointIndex(loopPathPoints.Count - 2);
                if (t <= exitJunctionDistance + JunctionEps) t = exitJunctionDistance;
                loopBoostStartDistance = t;
            }

            // 6. Hầm (Cocos: cacheCavePath) — cave_path = "chỉ số điểm vào,chỉ số điểm ra"
            CacheCavePath();
        }

        private void CacheCavePath()
        {
            hasCavePath = false;
            loopCaveEntryDistance = 0f;
            loopCaveExitDistance = 0f;

            var road = currentRoadCfg ?? WhoGetInBus.Data.LevelConfigLoader.GetRoadConfig(currentRoadId);
            if (road == null || string.IsNullOrWhiteSpace(road.cave_path)) return;

            string[] parts = road.cave_path.Split(',');
            if (parts.Length != 2) return;
            if (!int.TryParse(parts[0].Trim(), out int entryIdx) || !int.TryParse(parts[1].Trim(), out int exitIdx)) return;
            int pointCount = loopPathPoints.Count;
            if (entryIdx < 0 || exitIdx < 0 || entryIdx >= pointCount || exitIdx >= pointCount || entryIdx == exitIdx) return;

            loopCaveEntryDistance = GetLoopDistanceAtPointIndex(entryIdx);
            loopCaveExitDistance = GetLoopDistanceAtPointIndex(exitIdx);
            hasCavePath = true;
        }

        /// <summary>Khoảng cách tại điểm thứ index của loop (Cocos: getLoopDistanceAtPointIndex).</summary>
        public float GetLoopDistanceAtPointIndex(int index)
        {
            if (index <= 0 || loopPrefixLens == null) return 0f;
            if (index >= loopPrefixLens.Length) return loopTotalLen;
            return loopPrefixLens[index];
        }

        /// <summary>
        /// Đánh giá toạ độ 3D chính xác trên tim đường tại khoảng cách distance (Arc-Length)
        /// </summary>
        public Vector3 EvaluatePosition(float distance, bool isExit)
        {
            var points = isExit ? exitPathPoints : loopPathPoints;
            var segLens = isExit ? exitSegLens : loopSegLens;
            var prefixLens = isExit ? exitPrefixLens : loopPrefixLens;
            float totalLen = isExit ? exitTotalLen : loopTotalLen;

            if (points == null || points.Count == 0) return Vector3.zero;
            if (points.Count == 1) return points[0];
            if (segLens == null || segLens.Length == 0) return points[0];

            if (!isExit)
            {
                if (totalLen <= 0.0001f) return points[0];
                distance = distance % totalLen;
                if (distance < 0f) distance += totalLen;
            }
            else
            {
                if (distance <= 0f) return points[0];
                if (distance >= totalLen) return points[points.Count - 1];
            }

            for (int i = 0; i < segLens.Length; i++)
            {
                float segStart = prefixLens[i];
                float segLen = segLens[i];
                if (distance <= segStart + segLen || i == segLens.Length - 1)
                {
                    float segProgress = segLen > 0.0001f ? Mathf.Clamp01((distance - segStart) / segLen) : 0f;
                    return Vector3.Lerp(points[i], points[i + 1], segProgress);
                }
            }

            return points[points.Count - 1];
        }

        /// <summary>
        /// Lấy vector tiếp tuyến nhìn trước (Look-Ahead Tangent) mượt mà chuẩn Cocos 1:1
        /// </summary>
        public Vector3 SampleLookAheadTangent(float distance, float lookAheadDist, bool isExit)
        {
            Vector3 pA = EvaluatePosition(distance, isExit);
            Vector3 pB = EvaluatePosition(distance + Mathf.Max(0.05f, lookAheadDist), isExit);
            Vector3 dir = pB - pA;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                return dir.normalized;
            }

            // Fallback: tiếp tuyến theo phân đoạn hiện tại
            var points = isExit ? exitPathPoints : loopPathPoints;
            if (points != null && points.Count >= 2)
            {
                Vector3 fallbackDir = points[points.Count - 1] - points[points.Count - 2];
                fallbackDir.y = 0f;
                return fallbackDir.sqrMagnitude > 0.0001f ? fallbackDir.normalized : Vector3.forward;
            }
            return Vector3.forward;
        }

        /// <summary>
        /// Góc nghiêng thân xe mục tiêu (Cocos: getTurnLeanTargetDeg): so hướng nhìn trước hiện tại với hướng nhìn trước tiếp theo.
        /// lean = 6 · min(1, góc/20°), bỏ qua nếu góc &lt; 0.75°.
        /// </summary>
        public float GetTurnLeanTargetDeg(float distance, bool isExit)
        {
            Vector3 now = SampleLookAheadTangent(distance, LookAhead, isExit);
            Vector3 ahead = SampleLookAheadTangent(distance + Mathf.Max(0.05f, LookAhead), LookAhead, isExit);
            return GetTurnLeanTargetDeg(now, ahead);
        }

        /// <summary>
        /// Tính toán góc nghiêng thân xe (Banking Lean Deg) bằng công thức chuẩn Cocos Creator 1:1:
        /// targetLean = -sign(cross) * 6 * min(1, angle/20)
        /// </summary>
        public float GetTurnLeanTargetDeg(Vector3 currentForward, Vector3 lookAheadTangent)
        {
            float cross = currentForward.x * lookAheadTangent.z - currentForward.z * lookAheadTangent.x;
            float dot = Mathf.Clamp(Vector3.Dot(currentForward, lookAheadTangent), -1f, 1f);
            float angleDeg = Mathf.Abs(Mathf.Atan2(cross, dot) * Mathf.Rad2Deg);
            if (angleDeg < 0.75f) return 0f;

            float lean = 6.0f * Mathf.Min(1f, angleDeg / 20f);
            return Mathf.Clamp(-Mathf.Sign(cross) * lean, -maxBankingLean, maxBankingLean);
        }

        /// <summary>
        /// Tìm khoảng cách loopDistance gần nhất tương ứng với toạ độ worldPos
        /// </summary>
        public float FindClosestLoopDistance(Vector3 worldPos)
        {
            if (loopPathPoints == null || loopPathPoints.Count < 2 || loopSegLens == null || loopSegLens.Length == 0) return 0f;

            float bestDistSqr = float.MaxValue;
            int bestSeg = 0;
            float bestT = 0f;

            for (int i = 0; i < loopSegLens.Length; i++)
            {
                Vector3 a = loopPathPoints[i];
                Vector3 b = loopPathPoints[i + 1];
                Vector3 ab = b - a;
                float sqrLen = ab.sqrMagnitude;
                float t = (sqrLen > 0.0001f) ? Mathf.Clamp01(Vector3.Dot(worldPos - a, ab) / sqrLen) : 0f;
                Vector3 proj = Vector3.Lerp(a, b, t);
                float d2 = (worldPos - proj).sqrMagnitude;
                if (d2 < bestDistSqr)
                {
                    bestDistSqr = d2;
                    bestSeg = i;
                    bestT = t;
                }
            }

            return loopPrefixLens[bestSeg] + loopSegLens[bestSeg] * bestT;
        }

        /// <summary>Khoảng cách trên đường Exit gần worldPos nhất.</summary>
        public float FindClosestExitDistance(Vector3 worldPos)
        {
            if (exitPathPoints == null || exitPathPoints.Count < 2 || exitSegLens == null || exitSegLens.Length == 0) return 0f;

            float bestDistSqr = float.MaxValue;
            int bestSeg = 0;
            float bestT = 0f;

            for (int i = 0; i < exitSegLens.Length; i++)
            {
                Vector3 a = exitPathPoints[i];
                Vector3 b = exitPathPoints[i + 1];
                Vector3 ab = b - a;
                float sqrLen = ab.sqrMagnitude;
                float t = (sqrLen > 0.0001f) ? Mathf.Clamp01(Vector3.Dot(worldPos - a, ab) / sqrLen) : 0f;
                Vector3 proj = Vector3.Lerp(a, b, t);
                float d2 = (worldPos - proj).sqrMagnitude;
                if (d2 < bestDistSqr)
                {
                    bestDistSqr = d2;
                    bestSeg = i;
                    bestT = t;
                }
            }

            return exitPrefixLens[bestSeg] + exitSegLens[bestSeg] * bestT;
        }

        /// <summary>
        /// Tính khoảng cách đi tới (Forward Loop Distance) giữa 2 xe trên vòng lặp
        /// </summary>
        public float GetForwardLoopDistance(float fromDist, float toDist)
        {
            if (loopTotalLen <= 0f) return float.MaxValue;
            float diff = toDist - fromDist;
            while (diff <= 0f) diff += loopTotalLen;
            return diff;
        }

        /// <summary>Khoảng cách ngắn nhất giữa 2 vị trí trên vòng (Cocos: getLoopDistanceSeparation).</summary>
        public float GetLoopDistanceSeparation(float a, float b)
        {
            if (loopTotalLen <= 0f) return float.PositiveInfinity;
            float d = Mathf.Abs(a - b) % loopTotalLen;
            return Mathf.Min(d, loopTotalLen - d);
        }

        /// <summary>
        /// Chuẩn hoá khoảng cách trên vòng lặp về phạm vi [0, loopTotalLen)
        /// </summary>
        public float NormalizeLoopDistance(float dist)
        {
            if (loopTotalLen <= 0f) return 0f;
            float r = dist % loopTotalLen;
            if (r < 0f) r += loopTotalLen;
            return r;
        }

        /// <summary>Cocos: BPathCursor.isAtLoopDistance.</summary>
        public bool IsAtLoopDistance(float dist, float target, float eps)
        {
            if (loopTotalLen <= 0f) return false;
            float g = Mathf.Abs(NormalizeLoopDistance(dist - target));
            return Mathf.Min(g, loopTotalLen - g) <= eps;
        }

        /// <summary>Cocos: BPathCursor.didCrossLoopDistance — vị trí từ prev tới cur có đi qua target không.</summary>
        public bool DidCrossLoopDistance(float prev, float cur, float target, float eps)
        {
            if (loopTotalLen <= 0f) return false;
            float g = NormalizeLoopDistance(prev);
            float u = NormalizeLoopDistance(cur);
            if (Mathf.Approximately(g, u)) return IsAtLoopDistance(cur, target, eps);

            float c = NormalizeLoopDistance(target);
            return g < u ? (c > g && c <= u) : (c > g || c <= u);
        }

        /// <summary>
        /// Xe đang nằm trong đoạn được phép ăn cát: từ điểm vào vòng loop tới ngã rẽ thoát.
        /// (Cocos: isLoopSkipAbsorb = distance &gt; exitDistance + 0.15)
        /// </summary>
        public bool IsBoardingZone(float dist)
        {
            return NormalizeLoopDistance(dist) <= exitJunctionDistance + JunctionEps;
        }

        public int FindClosestWaypointIndex(Vector3 pos)
        {
            if (loopWaypoints == null || loopWaypoints.Count == 0) return 0;
            int closest = 0;
            float minDist = float.MaxValue;
            for (int i = 0; i < loopWaypoints.Count; i++)
            {
                if (loopWaypoints[i] == null) continue;
                float d = (loopWaypoints[i].position - pos).sqrMagnitude;
                if (d < minDist)
                {
                    minDist = d;
                    closest = i;
                }
            }
            return closest;
        }

        #endregion

        #region ĐIỀU PHỐI CHUYỂN ĐỘNG TẬP TRUNG (Cocos CartTrackController.tick)

        private void Update()
        {
            if (!Application.isPlaying) return;

            float dt = Time.deltaTime;
            TickSpeedState(dt);

            if (!PathReady) return;

            TickCarts(dt);
            TickAbsorb(dt);
        }

        /// <summary>Cocos: updateSpeedState.</summary>
        public void SetSpeedState(TrackSpeedState state)
        {
            speedState = state;
            fullSpeedTimer = 0f;
            switch (state)
            {
                case TrackSpeedState.Final:
                    moveSpeed = emptyCarSpeed * WorldScale;
                    break;
                case TrackSpeedState.Full:
                    // Cocos: addSpeed — cộng acc_speed mỗi giây cho tới max_speed
                    moveSpeed = (currentRoadCfg != null ? currentRoadCfg.speed : baseSpeed) * WorldScale;
                    break;
                default:
                    moveSpeed = (currentRoadCfg != null && currentRoadCfg.speed > 0f ? currentRoadCfg.speed : baseSpeed) * WorldScale;
                    break;
            }
        }

        private void TickSpeedState(float dt)
        {
            if (autoSpeedState)
            {
                var lot = parkingLot;
                bool lotEmpty = lot != null && lot.HasLoaded && lot.RemainingCarCount <= 0;
                if (lotEmpty && speedState == TrackSpeedState.Normal) SetSpeedState(TrackSpeedState.Final);
                else if (!lotEmpty && speedState == TrackSpeedState.Final) SetSpeedState(TrackSpeedState.Normal);
            }

            if (speedState == TrackSpeedState.Full && currentRoadCfg != null)
            {
                fullSpeedTimer += dt;
                while (fullSpeedTimer >= 1f)
                {
                    fullSpeedTimer -= 1f;
                    moveSpeed = Mathf.Min(currentRoadCfg.max_speed * WorldScale, moveSpeed + currentRoadCfg.acc_speed * WorldScale);
                }
            }
        }

        private void TickCarts(float dt)
        {
            if (activeCarsOnTrack.Count == 0) return;

            for (int i = activeCarsOnTrack.Count - 1; i >= 0; i--)
            {
                if (activeCarsOnTrack[i] == null) activeCarsOnTrack.RemoveAt(i);
            }
            if (activeCarsOnTrack.Count == 0) return;

            BuildMoveAllowances(dt);

            removeList.Clear();
            for (int i = 0; i < activeCarsOnTrack.Count; i++)
            {
                var car = activeCarsOnTrack[i];
                if (car == null || car.isMerging || !car.isInLoop) continue;

                float allowance = car.moveAllowance;
                if (allowance <= 0f) continue;

                if (car.isExiting)
                {
                    car.currentTrackDistance += allowance;
                    if (car.currentTrackDistance >= exitTotalLen)
                    {
                        removeList.Add(car);
                        continue;
                    }
                    car.ApplyTrackTransform(this, dt, false);
                }
                else
                {
                    bool switchedToExit = TickLoopingCart(car, allowance);
                    car.ApplyTrackTransform(this, dt, switchedToExit);
                }
            }

            for (int i = 0; i < removeList.Count; i++) removeList[i].FinishExit();
            removeList.Clear();
        }

        /// <summary>Cocos: tickLoopingCart. Trả về true nếu xe vừa chuyển sang làn thoát.</summary>
        private bool TickLoopingCart(CarLoopFollower car, float allowance)
        {
            float eps = JunctionEps;

            if (car.isPendingExit && IsAtLoopDistance(car.currentTrackDistance, exitJunctionDistance, eps))
            {
                if (IsExitEntryClear())
                {
                    SwitchToExiting(car);
                    return true;
                }
                car.currentTrackDistance = exitJunctionDistance; // đứng chờ ở ngã rẽ tới khi làn thoát thông
                return false;
            }

            float prev = NormalizeLoopDistance(car.currentTrackDistance);
            float cur = NormalizeLoopDistance(prev + allowance);
            car.currentTrackDistance = cur;

            bool crossedEntry = DidCrossLoopDistance(prev, cur, 0f, eps);
            bool crossedExit = DidCrossLoopDistance(prev, cur, exitJunctionDistance, eps);
            bool crossedBoost = DidCrossLoopDistance(prev, cur, loopBoostStartDistance, eps);

            if (crossedEntry) OnCartCrossedLoopEntry(car);

            if (crossedExit)
            {
                if (car.isPendingExit)
                {
                    if (IsExitEntryClear())
                    {
                        SwitchToExiting(car);
                        return true;
                    }
                    car.currentTrackDistance = exitJunctionDistance;
                    return false;
                }
                if (!crossedEntry) car.isLoopSkipAbsorb = true;
            }

            if (crossedBoost && !crossedEntry) car.isLoopSegmentBoost = true;

            TickCaveCrossing(car, prev, cur);
            return false;
        }

        private void OnCartCrossedLoopEntry(CarLoopFollower car)
        {
            car.isLoopSegmentBoost = false;
            car.isLoopSkipAbsorb = false;
            if (car.lapPassengerCount > 0) car.emptyRoundCount = 0;
            else car.emptyRoundCount += 1;
            car.lapPassengerCount = 0;

            // Mỗi vòng random lại số hạt tối đa được hút (Cocos: onCartCrossedLoopEntry → rerollAbsorbChainCap)
            car.RerollAbsorbChainCap();
        }

        private void SwitchToExiting(CarLoopFollower car)
        {
            if (car.isExiting) return;

            car.isExiting = true;
            car.isPendingExit = false;
            car.isLoopSegmentBoost = false;
            car.isLoopSkipAbsorb = false;
            car.ResetLean();
            if (car.isInCave) ExitCave(car);

            Vector3 junction = EvaluatePosition(exitJunctionDistance, false);
            car.currentTrackDistance = FindClosestExitDistance(junction);
        }

        /// <summary>Làn thoát thông thoáng cho 1 xe nữa vào chưa (Cocos: isExitEntryClear).</summary>
        public bool IsExitEntryClear()
        {
            return IsExitLaneClear(FollowGap);
        }

        /// <summary>
        /// Kiểm tra xem làn đường thoát (Exit Lane) có đủ thông thoáng để xe rẽ vào không
        /// </summary>
        public bool IsExitLaneClear(float minGap)
        {
            if (minGap <= 0f) return true;
            for (int i = 0; i < activeCarsOnTrack.Count; i++)
            {
                var car = activeCarsOnTrack[i];
                if (car != null && car.isInLoop && car.isExiting && car.currentTrackDistance < minGap) return false;
            }
            return true;
        }

        /// <summary>Xe đã đầy khách: đánh dấu chờ rẽ ra làn thoát (Cocos: markCartPendingExit).</summary>
        public bool MarkCartPendingExit(CarLoopFollower car)
        {
            if (car == null || !activeCarsOnTrack.Contains(car)) return false;
            if (car.isExiting || car.isPendingExit) return true;

            car.isPendingExit = true;
            if (PathReady && IsAtLoopDistance(car.currentTrackDistance, exitJunctionDistance, JunctionEps) && IsExitEntryClear())
            {
                SwitchToExiting(car);
            }
            return true;
        }

        /// <summary>Cocos: addLapPassengerCount.</summary>
        public void AddLapPassengerCount(CarLoopFollower car, int count)
        {
            if (car == null || count <= 0 || car.isExiting) return;
            car.lapPassengerCount += count;
            car.emptyRoundCount = 0;
        }

        /// <summary>Cocos: syncLoopSegmentFlagsFromCursor.</summary>
        public void SyncLoopSegmentFlags(CarLoopFollower car)
        {
            if (loopTotalLen <= 0f)
            {
                car.isLoopSegmentBoost = false;
                car.isLoopSkipAbsorb = false;
                return;
            }
            float t = NormalizeLoopDistance(car.currentTrackDistance);
            car.isLoopSkipAbsorb = t > exitJunctionDistance + JunctionEps;
            car.isLoopSegmentBoost = t > loopBoostStartDistance + JunctionEps;
        }

        #endregion

        #region HẠN MỨC DI CHUYỂN & KHOẢNG CÁCH AN TOÀN (Cocos buildMoveAllowances)

        private float GetCartMoveSpeed(CarLoopFollower car)
        {
            float speed = (!car.isExiting && car.isLoopSegmentBoost) ? moveSpeed * accelerateSpeedRatio : moveSpeed;
            if (car.deploySpeedLimited)
            {
                UpdateDeploySpeedRecovery(car);
                return car.deploySpeedLimited ? speed * GetDeploySpeedRatio(car) : speed;
            }
            return speed;
        }

        private void BuildMoveAllowances(float dt)
        {
            UpdateDeployOverlapEscapeRatios();

            loopingOrder.Clear();
            exitingOrder.Clear();
            for (int i = 0; i < activeCarsOnTrack.Count; i++)
            {
                var car = activeCarsOnTrack[i];
                if (car == null) continue;

                float speed = GetCartMoveSpeed(car);
                car.currentSpeed = speed;
                car.moveAllowance = (speed > 0f && dt > 0f) ? speed * dt : 0f;

                if (!car.isInLoop || car.isMerging) continue;
                if (car.isExiting) exitingOrder.Add(car);
                else loopingOrder.Add(car);
            }

            float gap = FollowGap;
            if (gap <= 0f) return;

            loopingOrder.Sort(CompareLoopDistanceDelegate);
            exitingOrder.Sort(CompareExitDistanceDelegate);

            LimitLoopingCartMovesForDeployReservations(gap);
            LimitLoopingCartMoves(gap);
            LimitExitingCartMoves(gap);
        }

        public float GetLoopDistance(CarLoopFollower car)
        {
            return NormalizeLoopDistance(car.currentTrackDistance);
        }

        // Delegate so sánh được cache để không sinh rác mỗi khung hình
        private System.Comparison<CarLoopFollower> compareLoopDistance;
        private System.Comparison<CarLoopFollower> compareExitDistance;

        private System.Comparison<CarLoopFollower> CompareLoopDistanceDelegate =>
            compareLoopDistance ??= (a, b) => GetLoopDistance(a).CompareTo(GetLoopDistance(b));

        private System.Comparison<CarLoopFollower> CompareExitDistanceDelegate =>
            compareExitDistance ??= (a, b) => a.currentTrackDistance.CompareTo(b.currentTrackDistance);

        private static float ComputeFollowAllowedMove(float own, float front, float gapToFront, float minGap, bool limited)
        {
            if (minGap <= 0f) return own;
            float r = Mathf.Max(0f, front + gapToFront - minGap);
            if (!limited || r > 0f) return Mathf.Min(own, r);
            return front <= 0f ? 0f : Mathf.Min(own, 0.85f * front);
        }

        private bool IsSlowDeploySeek(CarLoopFollower car)
        {
            return car.deploySpeedLimited && GetDeploySpeedRatio(car) < 1f;
        }

        private int GetEffectiveFollowFrontIndex(List<CarLoopFollower> order, int index, int count)
        {
            var self = order[index];
            for (int n = 1; n < count; n++)
            {
                int r = (index + n) % count;
                var other = order[r];
                if (!IsSlowDeploySeek(other) || self.deploySpeedLimited) return r;
            }
            return -1;
        }

        /// <summary>Không cho xe đang chạy phía sau lao qua ô đã giữ chỗ nhập làn (Cocos: limitLoopingCartMovesForDeployReservations).</summary>
        private void LimitLoopingCartMovesForDeployReservations(float gap)
        {
            if (loopTotalLen <= 0f || loopingOrder.Count == 0 || deployReservations.Count == 0) return;

            foreach (var res in deployReservations.Values)
            {
                if (res.reducedSpeed) continue;

                CarLoopFollower best = null;
                float bestS = float.PositiveInfinity;
                for (int i = 0; i < loopingOrder.Count; i++)
                {
                    var car = loopingOrder[i];
                    float u = res.plan.mergeDistance - GetLoopDistance(car);
                    if (u < 0f) u += loopTotalLen;
                    if (u >= bestS) continue;
                    best = car;
                    bestS = u;
                }

                if (best != null)
                {
                    best.moveAllowance = Mathf.Min(best.moveAllowance, Mathf.Max(0f, bestS - gap));
                }
            }
        }

        private void LimitLoopingCartMoves(float gap)
        {
            int count = loopingOrder.Count;
            if (count <= 1 || loopTotalLen <= 0f) return;

            for (int pass = 0; pass < count; pass++)
            {
                bool changed = false;
                for (int a = count - 1; a >= 0; a--)
                {
                    var car = loopingOrder[a];
                    int frontIdx = GetEffectiveFollowFrontIndex(loopingOrder, a, count);
                    if (frontIdx < 0) continue;

                    var front = loopingOrder[frontIdx];
                    float h = GetLoopDistance(car);
                    float c = GetLoopDistance(front);
                    if (frontIdx <= a) c += loopTotalLen;
                    float gapToFront = c - h;

                    float allowed = ComputeFollowAllowedMove(car.moveAllowance, front.moveAllowance, gapToFront, gap, car.deploySpeedLimited);
                    if (car.moveAllowance > allowed)
                    {
                        car.moveAllowance = allowed;
                        changed = true;
                    }
                }
                if (!changed) break;
            }
        }

        private void LimitExitingCartMoves(float gap)
        {
            for (int i = exitingOrder.Count - 2; i >= 0; i--)
            {
                var rear = exitingOrder[i];
                var front = exitingOrder[i + 1];
                float d = front.currentTrackDistance - rear.currentTrackDistance;
                rear.moveAllowance = Mathf.Min(rear.moveAllowance, Mathf.Max(0f, front.moveAllowance + d - gap));
            }
        }

        #endregion

        #region NHẬP LÀN TỪ BÃI ĐỖ (Cocos createCartDeployPlan / prepareCartDeploy / registerCartAtDeployPlan)

        /// <summary>
        /// Tạo kế hoạch nhập làn cho xe xuất phát từ bãi đỗ (Cocos: createCartDeployPlan).
        /// Điểm nhập làn = hình chiếu lên tim đường của điểm có x của xe (dịch sang phải nếu x &lt; ngưỡng) và z của điểm đầu loop.
        /// </summary>
        public CartDeployPlan CreateCartDeployPlan(Vector3 carWorldPos)
        {
            if (!PathReady) return null;

            float stagingX = carWorldPos.x;
            if (stagingX < deployTurnThresholdX) stagingX += deployTurnStagingOffsetX;
            Vector3 staging = new Vector3(stagingX, loopPathPoints[0].y, loopPathPoints[0].z);

            var plan = new CartDeployPlan();
            plan.sourceWorld = carWorldPos;
            plan.mergeDistance = FindClosestLoopDistance(staging);
            plan.mergeWorld = EvaluatePosition(plan.mergeDistance, false);
            plan.mergeTangent = SampleLookAheadTangent(plan.mergeDistance, LookAhead, false);
            if (plan.mergeTangent.sqrMagnitude < 0.001f) plan.mergeTangent = Vector3.right;
            plan.targetRotation = Quaternion.LookRotation(plan.mergeTangent.normalized, Vector3.up);
            plan.valid = true;
            return plan;
        }

        /// <summary>Tương thích tên cũ.</summary>
        public CartDeployPlan CreateDeployPlan(Vector3 carWorldPos)
        {
            return CreateCartDeployPlan(carWorldPos) ?? new CartDeployPlan { sourceWorld = carWorldPos, valid = false };
        }

        /// <summary>
        /// Giữ chỗ nhập làn (Cocos: prepareCartDeploy). Nếu quanh điểm nhập đã có xe/chỗ giữ cách &lt; followGap thì
        /// xe lao ra với tốc độ giảm và vào làn theo chế độ "tìm khe" (reducedSpeed).
        /// </summary>
        public CartDeployReservation PrepareCartDeploy(CarLoopFollower car, CartDeployPlan plan)
        {
            if (car == null || plan == null || !plan.valid) return null;

            CancelCartDeploy(car);
            var res = new CartDeployReservation
            {
                plan = plan,
                reducedSpeed = !CanReserveDeploy(car, plan)
            };
            deployReservations[car] = res;
            return res;
        }

        private bool CanReserveDeploy(CarLoopFollower self, CartDeployPlan plan)
        {
            float minGap = FollowGap;

            for (int i = 0; i < activeCarsOnTrack.Count; i++)
            {
                var car = activeCarsOnTrack[i];
                if (car == null || car == self || car.isExiting || !car.isInLoop) continue;
                if (GetLoopDistanceSeparation(GetLoopDistance(car), plan.mergeDistance) < minGap) return false;
            }

            foreach (var kv in deployReservations)
            {
                if (kv.Key == self || kv.Key == null) continue;
                if (GetLoopDistanceSeparation(kv.Value.plan.mergeDistance, plan.mergeDistance) < minGap) return false;
            }
            return true;
        }

        public bool IsCartDeployActive(CarLoopFollower car)
        {
            return car != null && deployReservations.ContainsKey(car);
        }

        public void CancelCartDeploy(CarLoopFollower car)
        {
            if (car != null) deployReservations.Remove(car);
        }

        public void CompleteCartDeploy(CarLoopFollower car)
        {
            if (car != null) deployReservations.Remove(car);
        }

        /// <summary>
        /// Xe lao tới điểm nhập làn xong → đặt vào làn tại đúng vị trí đó (Cocos: registerCartAtDeployPlan).
        /// </summary>
        public bool RegisterCartAtDeployPlan(CarLoopFollower car, CartDeployPlan plan)
        {
            if (car == null || plan == null || !plan.valid || !PathReady) return false;

            deployReservations.TryGetValue(car, out var reservation);
            bool reduced = reservation != null && reservation.reducedSpeed;

            car.EnterTrack(plan.mergeDistance, false);
            AddCarToTrack(car);

            if (reduced)
            {
                car.deploySpeedLimited = true;
                car.deploySpeedRatio = Mathf.Max(0f, blockedWaitingSpeedRatio);
                ResolveDeploySeekSpeedAtEnter(car);
            }

            KickstartCartAfterRegister(car);
            CompleteCartDeploy(car);
            CheckAndMergeSameColorCars(car.carColor);
            return true;
        }

        /// <summary>Cocos: kickstartCartAfterRegister — cho xe tiến ngay 1 bước để không đứng khựng sau khi nhập làn.</summary>
        private void KickstartCartAfterRegister(CarLoopFollower car)
        {
            float dt = Mathf.Min(Mathf.Max(Time.deltaTime, 1f / 60f), 1f / 30f);
            UpdateDeployOverlapEscapeRatios();

            float step = GetCartMoveSpeed(car) * dt;
            float baseStep = step;
            float gap = FollowGap;

            if (gap > 0f && loopTotalLen > 0f)
            {
                float mine = GetLoopDistance(car);
                for (int i = 0; i < activeCarsOnTrack.Count; i++)
                {
                    var other = activeCarsOnTrack[i];
                    if (other == null || other == car || other.isExiting || !other.isInLoop) continue;
                    if (IsSlowDeploySeek(other) && !car.deploySpeedLimited) continue;

                    float h = GetForwardLoopDistance(mine, GetLoopDistance(other));
                    if (h >= gap) step = Mathf.Min(step, h - gap);
                    else if (car.deploySpeedLimited) step = h < 0.2f ? 0f : Mathf.Min(step, baseStep * (h / gap));
                    else step = Mathf.Min(step, Mathf.Max(0f, h - gap));
                }
            }

            if (step <= 0f)
            {
                car.ApplyTrackTransform(this, 0f, true);
                return;
            }

            TickLoopingCart(car, step);
            car.ApplyTrackTransform(this, dt, false);
        }

        // ---- Tốc độ giảm khi nhập làn bị chặn (Cocos: deploySpeedLimited) ----

        private float GetDeploySpeedRatio(CarLoopFollower car)
        {
            return Mathf.Max(car.deploySpeedRatio, car.deployOverlapEscapeRatio);
        }

        private void UpdateDeployOverlapEscapeRatios()
        {
            for (int i = 0; i < activeCarsOnTrack.Count; i++)
            {
                if (activeCarsOnTrack[i] != null) activeCarsOnTrack[i].deployOverlapEscapeRatio = 0f;
            }
            if (loopTotalLen <= 0f) return;

            float gap = FollowGap;
            overlapScratch.Clear();
            for (int i = 0; i < activeCarsOnTrack.Count; i++)
            {
                var car = activeCarsOnTrack[i];
                if (car != null && car.isInLoop && !car.isExiting && car.deploySpeedLimited) overlapScratch.Add(car);
            }

            int n = overlapScratch.Count;
            if (n < 2) return;
            overlapScratch.Sort(CompareLoopDistanceDelegate);

            float escape = Mathf.Min(0.95f, Mathf.Max(0.8f, blockedWaitingSpeedRatio + 0.12f));
            for (int p = 0; p < n; p++)
            {
                var d = overlapScratch[p];
                var h = overlapScratch[(p + 1) % n];
                if (GetForwardLoopDistance(GetLoopDistance(d), GetLoopDistance(h)) >= gap) continue;

                var c = d.transform.position.x >= h.transform.position.x ? d : h;
                c.deployOverlapEscapeRatio = Mathf.Max(c.deployOverlapEscapeRatio, escape);
            }
        }

        private void UpdateDeploySpeedRecovery(CarLoopFollower car)
        {
            if (!car.deploySpeedLimited || car.isExiting || loopTotalLen <= 0f) return;

            float gap = FollowGap;
            if (gap <= 0f)
            {
                ReleaseDeploySpeedLimit(car);
                return;
            }

            GetDeploySpeedRecoveryGapInfo(car, out float front, out float rear);
            bool enoughRoom = front >= gap && rear >= gap && front + rear >= 2f * gap;
            if (!enoughRoom) return;

            float dt = Mathf.Max(0f, Time.deltaTime);
            float ratio = car.deploySpeedRatio;
            ratio = ratio > 1f ? Mathf.Max(1f, ratio - 2.5f * dt) : Mathf.Min(1f, ratio + 2.5f * dt);
            car.deploySpeedRatio = ratio;
            if (ratio >= 1f) ReleaseDeploySpeedLimit(car);
        }

        private static void ReleaseDeploySpeedLimit(CarLoopFollower car)
        {
            car.deploySpeedLimited = false;
            car.deploySpeedRatio = 1f;
        }

        private void GetDeploySpeedRecoveryGapInfo(CarLoopFollower car, out float nearestFront, out float nearestRear)
        {
            float mine = GetLoopDistance(car);
            nearestFront = float.PositiveInfinity;
            nearestRear = float.PositiveInfinity;

            for (int i = 0; i < activeCarsOnTrack.Count; i++)
            {
                var other = activeCarsOnTrack[i];
                if (other == null || other == car || other.isExiting || !other.isInLoop || IsSlowDeploySeek(other)) continue;

                float forward = GetForwardLoopDistance(mine, GetLoopDistance(other));
                float rear = loopTotalLen - forward;
                if (forward < nearestFront) nearestFront = forward;
                if (rear < nearestRear) nearestRear = rear;
            }
        }

        /// <summary>Cocos: resolveDeploySeekSpeedAtEnter — khe trống phía trước gần hơn thì tăng tốc, ngược lại giảm tốc chờ.</summary>
        private void ResolveDeploySeekSpeedAtEnter(CarLoopFollower car)
        {
            float gap = FollowGap;
            if (gap <= 0f) return;

            FindNearestDeploySeekSlotDistances(car, gap, out float front, out float rear);
            car.deploySpeedRatio = front < rear ? accelerateSpeedRatio : Mathf.Max(0f, blockedWaitingSpeedRatio);
        }

        private void FindNearestDeploySeekSlotDistances(CarLoopFollower self, float gap, out float front, out float rear)
        {
            front = float.PositiveInfinity;
            rear = float.PositiveInfinity;

            seekScratch.Clear();
            for (int i = 0; i < activeCarsOnTrack.Count; i++)
            {
                var car = activeCarsOnTrack[i];
                if (car == null || car == self || car.isExiting || !car.isInLoop || car.deploySpeedLimited) continue;
                seekScratch.Add(car);
            }
            if (seekScratch.Count == 0) return;

            seekScratch.Sort(CompareLoopDistanceDelegate);

            float l = GetLoopDistance(self);
            float need = 2f * gap;
            int u = seekScratch.Count;
            for (int c = 0; c < u; c++)
            {
                float g = GetLoopDistance(seekScratch[c]);
                float v = GetLoopDistance(seekScratch[(c + 1) % u]);
                float f = v - g;
                if (f <= 0f) f += loopTotalLen;
                if (f < need) continue;

                float slotStart = g + gap;
                if (slotStart >= loopTotalLen) slotStart -= loopTotalLen;
                float slotEnd = v - gap;
                if (slotEnd < 0f) slotEnd += loopTotalLen;

                float s = slotStart - l;
                if (s <= 0f) s += loopTotalLen;
                float m = l - slotEnd;
                if (m <= 0f) m += loopTotalLen;

                if (s < front) front = s;
                if (m < rear) rear = m;
            }
        }

        #endregion

        #region HẦM (Cocos enterCave / exitCave)

        private void TickCaveCrossing(CarLoopFollower car, float prev, float cur)
        {
            if (!hasCavePath) return;
            if (DidCrossLoopDistance(prev, cur, loopCaveEntryDistance, JunctionEps)) EnterCave(car);
            if (DidCrossLoopDistance(prev, cur, loopCaveExitDistance, JunctionEps)) ExitCave(car);
        }

        private bool IsInCaveSegment(float dist)
        {
            if (!hasCavePath || loopTotalLen <= 0f) return false;
            float t = NormalizeLoopDistance(dist);
            float o = loopCaveEntryDistance;
            float i = loopCaveExitDistance;
            float z = JunctionEps;
            return o < i ? (t >= o + z && t < i - z) : (t >= o + z || t < i - z);
        }

        /// <summary>Đồng bộ trạng thái hầm khi xe vừa nhập làn giữa đoạn hầm (Cocos: syncCaveStateFromCursor).</summary>
        public void SyncCaveState(CarLoopFollower car)
        {
            if (!hasCavePath)
            {
                if (car.isInCave) ExitCave(car);
                return;
            }
            bool inside = IsInCaveSegment(car.currentTrackDistance);
            if (inside && !car.isInCave) EnterCave(car);
            else if (!inside && car.isInCave) ExitCave(car);
        }

        private void EnterCave(CarLoopFollower car)
        {
            if (car.isInCave) return;
            car.isInCave = true;
            car.SetVisualVisible(false);
        }

        private void ExitCave(CarLoopFollower car)
        {
            if (!car.isInCave) return;
            car.isInCave = false;
            car.SetVisualVisible(true);
        }

        #endregion

        #region VÒNG QUÉT ĂN CÁT (Cocos LevelController.tickAbsorb)

        private void TickAbsorb(float dt)
        {
            if (sandBoard == null || activeCarsOnTrack.Count == 0) return;

            absorbTimer += dt;
            if (absorbTimer < absorbDetectionInterval) return;
            absorbTimer = 0f;

            CollectAbsorbFlyTasks();
        }

        private void CollectAbsorbFlyTasks()
        {
            selectScratch.Clear();
            for (int i = 0; i < activeCarsOnTrack.Count; i++)
            {
                var car = activeCarsOnTrack[i];
                if (car != null && car.isInLoop && !car.isExiting && !car.isMerging) selectScratch.Add(car);
            }

            int e = selectScratch.Count;
            if (e == 0)
            {
                absorbScanCursor = 0;
                return;
            }

            var lot = parkingLot;
            bool boardEmpty = lot != null && lot.HasLoaded && lot.RemainingCarCount <= 0;

            int budget = e > absorbScanBudgetPerTick ? 2 : 1;
            int scanned = 0;
            int visited = 0;
            int idx = absorbScanCursor % e;
            while (visited < e && scanned < budget)
            {
                if (TryCollectAbsorbForCar(selectScratch[idx], boardEmpty)) scanned++;
                visited++;
                idx = (idx + 1) % e;
            }
            absorbScanCursor = idx;
        }

        /// <summary>Cocos: tryCollectAbsorbForBucket. Trả về true nếu lượt quét này được tính vào ngân sách.</summary>
        private bool TryCollectAbsorbForCar(CarLoopFollower car, bool boardEmpty)
        {
            if (car.isMerging || car.isThreeInOneLocked) return false;
            if (car.RealDemand <= 0) return false;
            if (car.isLoopSkipAbsorb || car.isInCave) return false;

            // Cuối màn: sau 2 vòng trống thì mở khoá chế độ hút rộng (Cocos: tryUnlockRelaxedAbsorb)
            if (boardEmpty && !car.relaxedAbsorbUnlocked && car.emptyRoundCount >= 2) car.relaxedAbsorbUnlocked = true;

            int cap = car.GetAbsorbChainCap();
            if (cap <= 0) return true;

            absorbItems.Clear();
            int removed = sandBoard.ExtractPassengersForCar(car, cap, boardEmpty, car.relaxedAbsorbUnlocked, absorbItems);
            if (removed <= 0) return true;

            absorbPoints.Clear();
            for (int i = 0; i < absorbItems.Count; i++) absorbPoints.Add(absorbItems[i].worldPos);

            flightSystem.Dispatch(car, absorbPoints, removed);
            return true;
        }

        /// <summary>Xe còn khách đang bay tới không (Cocos: hasPendingBoardingForBucket).</summary>
        public bool HasPendingBoarding(Car car)
        {
            return flightSystem != null && flightSystem.HasPending(car);
        }

        #endregion

        #region HỢP THỂ 3 XE CÙNG MÀU (Cocos LevelController.checkAndMergeThreeInOne / mergeThreeInOne)

        [Header("Gộp 3 Xe Cùng Màu (Cocos mergeThreeInOne)")]
        [Tooltip("Thời gian 2 xe đuôi bay vào xe dẫn đầu (Cocos: moveToMovingWorldPos 0.3s backIn)")]
        public float mergeFlyDuration = 0.3f;
        [Tooltip("Thời gian xe gộp phóng to từ 0 (Cocos: playThreeInOneVfx 0.15s backOut)")]
        public float mergeScaleInDuration = 0.15f;

        /// <summary>
        /// Cocos: isThreeInOneMergeReady — xe đã nhập làn xong (không còn lao ra), chưa đầy (realDemand > 0),
        /// không phải xe nối (BucketConnection) và chưa bị khoá bởi lượt gộp khác. Xe gộp (Extra) vẫn được gộp tiếp.
        /// </summary>
        private bool IsThreeInOneMergeReady(CarLoopFollower car)
        {
            return car != null
                && car.isInLoop && !car.isExiting && !car.isPendingExit
                && !car.isMerging && !car.isThreeInOneLocked
                && car.RealDemand > 0
                && !IsConnectionMechanic(car.mechanicType)
                && !deployReservations.ContainsKey(car)
                && activeCarsOnTrack.Contains(car);
        }

        private static bool IsConnectionMechanic(CarMechanicType type)
        {
            return type == CarMechanicType.Linked || type == CarMechanicType.LinkedVariant;
        }

        /// <summary>
        /// Cocos: checkAndMergeThreeInOne — gọi sau khi 1 xe nhập làn xong. Nếu đang gộp thì bỏ qua
        /// (vòng lặp gộp đang chạy sẽ tự quét lại sau mỗi lần gộp).
        /// </summary>
        public void CheckAndMergeSameColorCars(GameColorType color)
        {
            if (mergingThreeInOne || !Application.isPlaying) return;
            if (!FindThreeInOneGroup(mergeScratch)) return;
            mergeCoroutine = StartCoroutine(MergeThreeInOneLoop());
        }

        /// <summary>Gom các xe sẵn sàng theo màu (giữ thứ tự xuất bến), lấy 3 xe đầu của màu đầu tiên đủ 3.</summary>
        private bool FindThreeInOneGroup(List<CarLoopFollower> result)
        {
            result.Clear();
            for (int i = 0; i < activeCarsOnTrack.Count; i++)
            {
                var first = activeCarsOnTrack[i];
                if (!IsThreeInOneMergeReady(first)) continue;

                bool colorSeenBefore = false;
                for (int k = 0; k < i; k++)
                {
                    var prev = activeCarsOnTrack[k];
                    if (prev != null && prev.carColor == first.carColor && IsThreeInOneMergeReady(prev))
                    {
                        colorSeenBefore = true;
                        break;
                    }
                }
                if (colorSeenBefore) continue;

                result.Add(first);
                for (int j = i + 1; j < activeCarsOnTrack.Count && result.Count < 3; j++)
                {
                    var other = activeCarsOnTrack[j];
                    if (other != null && other.carColor == first.carColor && IsThreeInOneMergeReady(other)) result.Add(other);
                }
                if (result.Count >= 3) return true;
                result.Clear();
            }
            return false;
        }

        private System.Collections.IEnumerator MergeThreeInOneLoop()
        {
            mergingThreeInOne = true;
            var group = new CarLoopFollower[3];
            var tails = new List<CarLoopFollower>(2);
            var tailStarts = new List<Vector3>(2);

            try
            {
                while (FindThreeInOneGroup(mergeScratch))
                {
                    for (int i = 0; i < 3; i++) group[i] = mergeScratch[i];
                    GameColorType color = group[0].carColor;

                    // 1. Khách đang bay tới 3 xe đáp xuống ngay để số ghế còn lại chính xác (Cocos: forceArriveForBuckets)
                    if (flightSystem != null)
                    {
                        for (int i = 0; i < 3; i++) flightSystem.ForceArrive(group[i]);
                    }
                    bool intact = true;
                    for (int i = 0; i < 3; i++) intact &= IsThreeInOneMergeReady(group[i]);
                    if (!intact) break;

                    // 2. Khoá cả 3 xe: không hút cát nữa (Cocos: threeInOneLockSet)
                    for (int i = 0; i < 3; i++) group[i].isThreeInOneLocked = true;

                    // 3. Xe dẫn đầu = xe ở xa nhất phía trên màn hình (Cocos: worldPosition.z nhỏ nhất; trục z Unity ngược chiều Cocos)
                    CarLoopFollower lead = group[0];
                    for (int i = 1; i < 3; i++)
                    {
                        if (group[i].transform.position.z > lead.transform.position.z) lead = group[i];
                    }

                    // 4. 2 xe đuôi rời làn ngay (nhường chỗ cho xe phía sau) rồi bay vào xe dẫn đang chạy
                    tails.Clear();
                    tailStarts.Clear();
                    for (int i = 0; i < 3; i++)
                    {
                        var car = group[i];
                        if (car == lead) continue;
                        activeCarsOnTrack.Remove(car);
                        car.isMerging = true;
                        mergingTails.Add(car);
                        tails.Add(car);
                        tailStarts.Add(car.transform.position);
                    }

                    Debug.Log($"<color=cyan>⚡ [GỘP XE]</color> 3 xe màu {color} trên đường → gộp thành xe 3-trong-1.");

                    bool aborted = false;
                    float elapsed = 0f;
                    float duration = Mathf.Max(0.01f, mergeFlyDuration);
                    while (elapsed < duration)
                    {
                        if (lead == null || !lead.isInLoop || lead.isExiting || !activeCarsOnTrack.Contains(lead))
                        {
                            aborted = true;
                            break;
                        }
                        elapsed += Time.deltaTime;
                        float k = BackIn(Mathf.Clamp01(elapsed / duration));
                        Vector3 target = lead.transform.position;
                        for (int i = 0; i < tails.Count; i++)
                        {
                            if (tails[i] != null) tails[i].transform.position = Vector3.LerpUnclamped(tailStarts[i], target, k);
                        }
                        yield return null;
                    }

                    if (!aborted)
                    {
                        for (int i = 0; i < tails.Count; i++) aborted |= tails[i] == null;
                    }

                    if (aborted)
                    {
                        RestoreThreeInOneAfterAbort(lead, tails);
                        break;
                    }

                    // 5. Xe gộp: số ghế = tổng ghế còn lại của 3 xe, đặt đúng chỗ & hướng của xe dẫn đầu
                    int seats = lead.currentSeats;
                    for (int i = 0; i < tails.Count; i++) seats += tails[i].currentSeats;

                    CarLoopFollower merged = SpawnThreeInOneCar(color, seats, lead);
                    if (merged == null)
                    {
                        RestoreThreeInOneAfterAbort(lead, tails);
                        break;
                    }

                    for (int i = 0; i < tails.Count; i++)
                    {
                        mergingTails.Remove(tails[i]);
                        Destroy(tails[i].gameObject);
                    }
                    UnregisterCarFromTrack(lead);
                    Destroy(lead.gameObject);

                    AddCarToTrack(merged);
                    merged.PlayMergeSuccessEffect();

                    // 6. Phóng to từ 0 (backOut) — đợi xong rồi mới xét lượt gộp tiếp theo như Cocos
                    Vector3 fullScale = merged.transform.localScale;
                    float s = 0f;
                    float scaleDuration = Mathf.Max(0.01f, mergeScaleInDuration);
                    merged.transform.localScale = Vector3.zero;
                    while (s < scaleDuration && merged != null)
                    {
                        s += Time.deltaTime;
                        merged.transform.localScale = fullScale * BackOut(Mathf.Clamp01(s / scaleDuration));
                        yield return null;
                    }
                    if (merged != null) merged.transform.localScale = fullScale;
                }
            }
            finally
            {
                mergingThreeInOne = false;
                mergeCoroutine = null;
            }
        }

        /// <summary>Huỷ lượt gộp: trả 2 xe đuôi về làn tại vị trí hiện tại và mở khoá (Cocos: restoreThreeInOneMoversAfterAbort).</summary>
        private void RestoreThreeInOneAfterAbort(CarLoopFollower lead, List<CarLoopFollower> tails)
        {
            if (lead != null) lead.isThreeInOneLocked = false;
            for (int i = 0; i < tails.Count; i++)
            {
                var car = tails[i];
                mergingTails.Remove(car);
                if (car == null) continue;
                car.isThreeInOneLocked = false;
                if (PathReady)
                {
                    car.EnterTrack(FindClosestLoopDistance(car.transform.position), false);
                    AddCarToTrack(car);
                }
                else
                {
                    car.isMerging = false;
                }
            }
        }

        /// <summary>Cocos: getTrackByBucket — tạo xe 3-trong-1 (type DEFAULT, quantity = tổng ghế còn lại).</summary>
        private CarLoopFollower SpawnThreeInOneCar(GameColorType color, int seats, CarLoopFollower lead)
        {
            GameObject prefabToUse = extraCarPrefab;
            if (prefabToUse == null && prefabData != null) prefabToUse = prefabData.extraCarPrefab;
            if (prefabToUse == null)
            {
                Debug.LogWarning("[LoopTrackSystem] Thiếu extraCarPrefab — không thể gộp 3 xe.");
                return null;
            }

            GameObject extraObj = Instantiate(prefabToUse, lead.transform.position, lead.transform.rotation, transform);
            extraObj.name = $"ExtraCar_Merged_{color}_{seats}c";

            if (extraObj.GetComponent<Collider>() == null)
            {
                var box = extraObj.AddComponent<BoxCollider>();
                box.size = new Vector3(1.8f, 2.4f, 3.2f);
                box.center = new Vector3(0, 1.2f, 0);
            }

            var follower = extraObj.GetComponent<CarLoopFollower>();
            if (follower == null) follower = extraObj.AddComponent<CarLoopFollower>();

            var colors = busColors;
            if (colors == null && prefabData != null) colors = prefabData.busColors;

            follower.InitializeCar(color, seats, CarMechanicType.Normal, colors);
            follower.isExtraCar = true;
            follower.EnterTrack(lead.currentTrackDistance, false);
            return follower;
        }

        // Cocos/tween easing.backIn & backOut (s = 1.70158)
        private static float BackIn(float k)
        {
            const float s = 1.70158f;
            return k * k * ((s + 1f) * k - s);
        }

        private static float BackOut(float k)
        {
            const float s = 1.70158f;
            k -= 1f;
            return k * k * ((s + 1f) * k + s) + 1f;
        }

        #endregion

        #region LOAD ROAD CONFIG (roadNCXHCfg.json)

        public void LoadRoadFromConfig(int roadId, GamePrefabData pData = null)
        {
            currentRoadId = roadId;
            EnsureConfigLoaded();
            if (pData != null) prefabData = pData;
            if (prefabData != null)
            {
                if (extraCarPrefab == null) extraCarPrefab = prefabData.extraCarPrefab;
                if (busColors == null) busColors = prefabData.busColors;
            }

            var roadData = WhoGetInBus.Data.LevelConfigLoader.GetRoadConfig(roadId);
            if (roadData == null)
            {
                Debug.LogWarning($"[LoopTrackSystem] Không tìm thấy Road #{roadId}!");
                return;
            }
            currentRoadCfg = roadData;

            if (roadData.speed > 0f) baseSpeed = roadData.speed;
            if (Application.isPlaying) ResetTrackRuntime();
            moveSpeed = baseSpeed * WorldScale;

            // 1. Dọn dẹp waypoints cũ
            var toDestroy = new List<GameObject>();
            for (int i = 0; i < transform.childCount; i++)
            {
                toDestroy.Add(transform.GetChild(i).gameObject);
            }
            foreach (var g in toDestroy)
            {
                if (Application.isPlaying) Destroy(g);
                else DestroyImmediate(g);
            }

            loopWaypoints.Clear();
            exitWaypoints.Clear();
            loopPathPoints.Clear();
            exitPathPoints.Clear();

            // 2. Nạp Loop Waypoints từ roadData.loop_path_root
            loopPathPoints = ParseCoordinatesRaw(roadData.loop_path_root);
            for (int i = 0; i < loopPathPoints.Count; i++)
            {
                GameObject wp = new GameObject($"Loop_WP_{i}");
                wp.transform.SetParent(transform, false);
                wp.transform.position = loopPathPoints[i];
                loopWaypoints.Add(wp.transform);
            }

            // 3. Nạp Exit Waypoints từ roadData.exit_path_root
            exitPathPoints = ParseCoordinatesRaw(roadData.exit_path_root);
            for (int i = 0; i < exitPathPoints.Count; i++)
            {
                GameObject wp = new GameObject($"Exit_WP_{i}");
                wp.transform.SetParent(transform, false);
                wp.transform.position = exitPathPoints[i];
                exitWaypoints.Add(wp.transform);
            }

            // 4. Khởi tạo Arc-Length Path Spline Cache
            RebuildPathCache();

            // 5. Ngã rẽ Exit Junction
            if (exitWaypoints.Count > 0 && loopWaypoints.Count > 0)
            {
                exitJunctionPoint = FindClosestWaypoint(loopWaypoints, exitWaypoints[0].position);
            }

            // 6. Điểm nhập làn từ bãi xe: mặc định đoạn đáy trước mặt bãi đỗ
            if (loopWaypoints.Count > 0)
            {
                parkingEntryWaypointIdx = 0;
                parkingEntryWaypoint = loopWaypoints[0];
                parkingEntryDistance = FindClosestLoopDistance(parkingEntryWaypoint.position);
            }

            // 7. Điểm đón khách sâu nhất (Boarding Gate Point)
            float maxZ = float.MinValue;
            for (int i = 0; i < loopWaypoints.Count; i++)
            {
                if (loopWaypoints[i].position.z > maxZ)
                {
                    maxZ = loopWaypoints[i].position.z;
                    boardingGatePoint = loopWaypoints[i];
                }
            }

            Debug.Log($"<color=green>[LoopTrackSystem]</color> Đã nạp Road #{roadId}: {loopPathPoints.Count} loop points, {exitPathPoints.Count} exit points. Tổng chiều dài loop: {loopTotalLen:F2}m. Exit tại: {exitJunctionDistance:F2}m, Tăng tốc từ: {loopBoostStartDistance:F2}m, Hầm: {(hasCavePath ? $"{loopCaveEntryDistance:F2}→{loopCaveExitDistance:F2}m" : "không")} (Chuẩn Cocos 1:1!)");
        }

#if UNITY_EDITOR
        [ContextMenu("Nạp Lại Road 5 (Chuẩn Cocos 1:1)")]
        public void ReloadRoad5InEditor()
        {
            LoadRoadFromConfig(5);
        }
#endif

        private List<Vector3> ParseCoordinatesRaw(string raw)
        {
            var list = new List<Vector3>();
            if (string.IsNullOrEmpty(raw)) return list;

            string[] points = raw.Split(';');
            foreach (var p in points)
            {
                string clean = p.Trim().Trim('[', ']');
                if (string.IsNullOrEmpty(clean)) continue;

                string[] parts = clean.Split(',');
                if (parts.Length >= 2)
                {
                    if (float.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x) &&
                        float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z))
                    {
                        // Chuẩn chuyển đổi hệ toạ độ từ Cocos sang Unity: X giữ nguyên, Z đổi dấu -z (kèm hệ số co giãn khớp lòng đường)
                        float finalX = enableTrackScale ? (x * trackScale.x + trackOffsetX) : x;
                        float finalZ = enableTrackScale ? (-z * trackScale.y + trackOffsetZ) : -z;
                        float finalY = enableTrackScale ? trackHeightOffset : 0f;
                        Vector3 pt = new Vector3(finalX, finalY, finalZ);
                        list.Add(pt);
                    }
                }
            }
            return list;
        }

        private Transform FindClosestWaypoint(List<Transform> list, Vector3 target)
        {
            Transform closest = null;
            float minDist = float.MaxValue;
            foreach (var t in list)
            {
                if (t == null) continue;
                float d = Vector3.Distance(t.position, target);
                if (d < minDist)
                {
                    minDist = d;
                    closest = t;
                }
            }
            return closest;
        }

        // Phương thức tương thích ngược
        public Vector3 GetLookAheadTangent(Vector3 currentPos, int currentWpIdx, bool isExit)
        {
            float dist = FindClosestLoopDistance(currentPos);
            return SampleLookAheadTangent(dist, LookAhead, isExit);
        }

        public int GetNextWaypointIndex(int currentIdx, bool isExit)
        {
            var list = isExit ? exitWaypoints : loopWaypoints;
            if (list == null || list.Count == 0) return 0;
            if (isExit) return Mathf.Min(currentIdx + 1, list.Count - 1);
            return (currentIdx + 1) % list.Count;
        }

        #endregion

        #region GIZMOS

        private void OnDrawGizmos()
        {
            // Vẽ đường Loop chữ U: Xanh lam cho đường chạy thường, Vàng cam cho đoạn được phép ăn cát (từ điểm vào tới ngã rẽ thoát),
            // Đỏ cam cho đoạn tăng tốc cuối vòng.
            if (loopPathPoints != null && loopPathPoints.Count > 1)
            {
                bool hasCache = loopPrefixLens != null && loopPrefixLens.Length == loopPathPoints.Count - 1;
                for (int i = 0; i < loopPathPoints.Count - 1; i++)
                {
                    float segStart = hasCache ? loopPrefixLens[i] : 0f;
                    bool absorbZone = hasCache && segStart < exitJunctionDistance - 0.001f;
                    bool boostZone = hasCache && segStart >= loopBoostStartDistance - 0.001f && loopBoostStartDistance > exitJunctionDistance;

                    Gizmos.color = boostZone
                        ? new Color(1f, 0.35f, 0.2f, 0.95f)
                        : absorbZone
                            ? new Color(1f, 0.75f, 0.15f, 0.95f)
                            : new Color(0.1f, 0.85f, 1f, 0.9f);

                    Gizmos.DrawLine(loopPathPoints[i], loopPathPoints[i + 1]);
                    Gizmos.DrawSphere(loopPathPoints[i], absorbZone ? 0.22f : 0.14f);
                }
            }

            // Vẽ đường thoát Exit màu xanh lá (Green)
            if (exitPathPoints != null && exitPathPoints.Count > 1)
            {
                Gizmos.color = Color.green;
                for (int i = 0; i < exitPathPoints.Count - 1; i++)
                {
                    Gizmos.DrawLine(exitPathPoints[i], exitPathPoints[i + 1]);
                    Gizmos.DrawCube(exitPathPoints[i], Vector3.one * 0.2f);
                }
            }
        }

        #endregion
    }
}
