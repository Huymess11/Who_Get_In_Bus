using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WhoGetInBus.GamePlay
{
    public class ParkingLotManager : MonoBehaviour
    {
        public static ParkingLotManager Instance { get; private set; }

        [Header("Cấu Hình Bãi Đỗ Xe (Chuẩn Cocos Creator 1:1)")]
        [Tooltip("Khoảng cách giữa các cột xe (tự động tính theo công thức Cocos: min(1.6, 5.6 / (cols - 1)))")]
        public float slotSpacingX = 1.4f;
        [Tooltip("Khoảng cách giữa các hàng xe (chuẩn Cocos: 1.8m)")]
        public float slotSpacingZ = 1.8f;
        public GameObject carPrefab;
        public GameObject extraCarPrefab; // Xe buýt 2 tầng (Extra Car)
        public BusColorData busColors;

        [Header("Tham Chiếu BusContainer Layout & Cars Holder")]
        public WorldSpaceLayout busContainerLayout;
        public Transform carsHolder;

        public void EnsureCarsHolder()
        {
            if (carsHolder == null)
            {
                var holderChild = transform.Find("Cars_Holder");
                if (holderChild != null)
                {
                    carsHolder = holderChild;
                }
                else if (GetComponent<WorldSpaceLayout>() != null || gameObject.name.Contains("BusContainer"))
                {
                    GameObject holderObj = new GameObject("Cars_Holder");
                    holderObj.transform.SetParent(transform, false);
                    carsHolder = holderObj.transform;
                }
                else
                {
                    carsHolder = transform;
                }
            }
        }

        public void EnsureBusContainerLayout(int cols)
        {
            if (busContainerLayout == null)
            {
                busContainerLayout = GetComponent<WorldSpaceLayout>();
                if (busContainerLayout == null)
                {
                    var busContainer = GameObject.Find("BusContainer");
                    if (busContainer != null)
                    {
                        busContainerLayout = busContainer.GetComponent<WorldSpaceLayout>();
                    }
                }
            }

            if (busContainerLayout != null)
            {
                // Đếm số lượng bus point con thực sự (loại trừ Cars_Holder)
                int busPointCount = 0;
                foreach (Transform child in busContainerLayout.transform)
                {
                    if (child.name.StartsWith("bus point")) busPointCount++;
                }

                while (busPointCount < cols)
                {
                    GameObject newPoint = new GameObject($"bus point ({busPointCount})");
                    newPoint.transform.SetParent(busContainerLayout.transform, false);
                    busPointCount++;
                }

                busContainerLayout.SetupForColumnCount(cols);
                Debug.Log($"<color=cyan>[ParkingLotManager]</color> Đã kích hoạt {cols} bus points trong BusContainer cho Level này!");
            }
        }

        public Vector3 GetSlotPosition(int colIndex, int rowIndex, int totalCols)
        {
            if (busContainerLayout != null)
            {
                int activeIdx = 0;
                for (int i = 0; i < busContainerLayout.transform.childCount; i++)
                {
                    Transform child = busContainerLayout.transform.GetChild(i);
                    if (child.gameObject.activeSelf && child.name.StartsWith("bus point", System.StringComparison.OrdinalIgnoreCase))
                    {
                        if (activeIdx == colIndex)
                        {
                            return new Vector3(child.position.x, child.position.y, child.position.z - rowIndex * slotSpacingZ);
                        }
                        activeIdx++;
                    }
                }
            }

            float startX = -((totalCols - 1) * slotSpacingX) * 0.5f;
            return transform.position + new Vector3(startX + colIndex * slotSpacingX, 0f, -rowIndex * slotSpacingZ);
        }

        // Lưu trữ danh sách xe theo từng cột: column[c] chứa hàng đợi các xe từ trước ra sau
        private List<List<CarLoopFollower>> columnsOfCars = new List<List<CarLoopFollower>>();

        /// <summary>Bãi xe đã sinh xe cho màn hiện tại chưa.</summary>
        public bool HasLoaded { get; private set; }

        /// <summary>Số xe còn nằm trong bãi chưa xuất bến (Cocos: _boardBucketCount). 0 = hết xe → đường đua chuyển sang tốc độ Final & hút cát chế độ relaxed.</summary>
        public int RemainingCarCount
        {
            get
            {
                int n = 0;
                for (int c = 0; c < columnsOfCars.Count; c++) n += columnsOfCars[c].Count;
                return n;
            }
        }

        private void Awake()
        {
            Instance = this;
            if (busContainerLayout == null)
            {
                busContainerLayout = GetComponent<WorldSpaceLayout>();
            }
            EnsureCarsHolder();
        }

        /// <summary>
        /// Nạp và sinh trực tiếp bãi đỗ xe từ Level ID (1, 2, ...)
        /// </summary>
        public void LoadLevelDirect(int targetLevelId, GamePrefabData prefabData = null)
        {
            var levelData = WhoGetInBus.Data.LevelConfigLoader.LoadLevel(targetLevelId);
            if (levelData != null)
            {
                int rows = Mathf.Max(1, levelData.RowCount);
                int cols = Mathf.Max(1, levelData.ColCount);
                string[,] grid = new string[rows, cols];
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        if (r < levelData.bus.Count && c < levelData.bus[r].Count)
                            grid[r, c] = levelData.bus[r][c];
                        else
                            grid[r, c] = "-1_0_0";
                    }
                }
                SpawnParkingLot(grid, rows, cols, prefabData);
            }
            else
            {
                Debug.LogWarning($"[ParkingLotManager] Không tìm thấy dữ liệu level {targetLevelId} chuẩn gốc!");
            }
        }

        public void SpawnParkingLot(string[,] carGrid, int rows, int cols, GamePrefabData prefabData)
        {
            if (prefabData != null)
            {
                carPrefab = prefabData.carPrefab;
                extraCarPrefab = prefabData.extraCarPrefab;
                busColors = prefabData.busColors;
            }
            SpawnParkingLot(carGrid, rows, cols, carPrefab, busColors);
        }

        public void SpawnParkingLot(string[,] carGrid, int rows, int cols, GameObject prefab, BusColorData colors)
        {
            carPrefab = prefab;
            busColors = colors;
            ClearParkingLot();

            columnsOfCars.Clear();

            EnsureBusContainerLayout(cols);

            // Tính khoảng cách cột chuẩn Cocos Creator 3.x
            slotSpacingX = Mathf.Min(1.6f, 5.6f / Mathf.Max(1, cols - 1));
            slotSpacingZ = 1.8f;
            float startX = -((cols - 1) * slotSpacingX) * 0.5f;

            for (int c = 0; c < cols; c++)
            {
                var colList = new List<CarLoopFollower>();

                for (int r = 0; r < rows; r++)
                {
                    string cellData = carGrid[r, c];
                    if (string.IsNullOrEmpty(cellData) || cellData.StartsWith("-1") || cellData.Equals("None", System.StringComparison.OrdinalIgnoreCase)) continue;

                    string[] parts = cellData.Split('_');
                    int colorIdx = 0, cap = 100, mech = 0;
                    if (parts.Length >= 2)
                    {
                        int.TryParse(parts[0], out colorIdx);
                        int.TryParse(parts[1], out cap);
                        if (parts.Length >= 3) int.TryParse(parts[2], out mech);
                    }

                    // Bãi đỗ xe luôn sinh ra xe thường (Car.prefab) theo đúng luật
                    GameObject chosenPrefab = (carPrefab != null) ? carPrefab : extraCarPrefab;

                    EnsureCarsHolder();
                    Transform parentHolder = carsHolder != null ? carsHolder : transform;

                    Vector3 slotPos = GetSlotPosition(c, r, cols);
                    GameObject carObj = null;
#if UNITY_EDITOR
                    if (!Application.isPlaying && chosenPrefab != null)
                    {
                        carObj = (GameObject)PrefabUtility.InstantiatePrefab(chosenPrefab, parentHolder);
                        carObj.transform.position = slotPos;
                        carObj.transform.rotation = Quaternion.identity;
                    }
                    else
                    {
                        carObj = Instantiate(chosenPrefab, slotPos, Quaternion.identity, parentHolder);
                    }
#else
                    carObj = Instantiate(chosenPrefab, slotPos, Quaternion.identity, parentHolder);
#endif
                    carObj.name = $"Car_C{c}_R{r}_{colorIdx}_{cap}c";

                    // Đảm bảo xe có Collider vừa vặn kích thước xe để click chuẩn xác
                    if (carObj.GetComponent<Collider>() == null)
                    {
                        var box = carObj.AddComponent<BoxCollider>();
                        box.size = new Vector3(1.3f, 1.4f, 1.6f);
                        box.center = new Vector3(0, 0.7f, 0);
                    }

                    var follower = carObj.GetComponent<CarLoopFollower>();
                    if (follower == null) follower = carObj.AddComponent<CarLoopFollower>();

                    follower.InitializeCar((GameColorType)colorIdx, cap, (CarMechanicType)mech, busColors);
                    colList.Add(follower);
                }

                columnsOfCars.Add(colList);
            }

            HasLoaded = true;
            Debug.Log($"<color=green>[ParkingLotManager]</color> Đã sinh bãi đỗ {cols} cột xe thành công!");
        }

        public void ClearParkingLot()
        {
            EnsureCarsHolder();
            if (carsHolder != null && carsHolder != transform)
            {
                List<GameObject> toDestroy = new List<GameObject>();
                for (int i = 0; i < carsHolder.childCount; i++)
                {
                    toDestroy.Add(carsHolder.GetChild(i).gameObject);
                }
                foreach (var g in toDestroy)
                {
                    if (Application.isPlaying) Destroy(g);
                    else DestroyImmediate(g);
                }
            }
            else
            {
                // Chỉ xóa các đối tượng xe buýt (Car), bảo vệ tuyệt đối không xóa bus point!
                var existingCars = GetComponentsInChildren<Car>(true);
                foreach (var c in existingCars)
                {
                    if (c != null && c.gameObject != gameObject)
                    {
                        if (Application.isPlaying) Destroy(c.gameObject);
                        else DestroyImmediate(c.gameObject);
                    }
                }
            }
            columnsOfCars.Clear();
            HasLoaded = false;
        }

        /// <summary>
        /// Xử lý click vào xe: xe hàng đầu (r == 0) xuất bến, xe hàng sau (r > 0) rung lắc báo kẹt (Bus Out style)
        /// </summary>
        public void OnCarClicked(Car car)
        {
            if (car == null || car.isInLoop || car.isPendingExit || car.isMerging) return;

            // Tìm vị trí của xe trong các cột bãi đỗ
            for (int c = 0; c < columnsOfCars.Count; c++)
            {
                var colList = columnsOfCars[c];
                for (int r = 0; r < colList.Count; r++)
                {
                    if (colList[r] == car)
                    {
                        if (r == 0)
                        {
                            // 1. Xe ở hàng đầu tiên -> Xuất bến ra đường đua đón khách!
                            var track = LoopTrackSystem.Instance;
                            if (track == null || !track.PathReady)
                            {
                                car.PlayBlockedBumpAnimation();
                                return;
                            }
                            colList.RemoveAt(0);
                            CarLoopFollower follower = car as CarLoopFollower ?? car.GetComponent<CarLoopFollower>();
                            StartCoroutine(DispatchCarRoutine(follower, c));
                        }
                        else
                        {
                            // 2. Xe ở hàng sau -> Bị kẹt phía sau -> Diễn hoạt cảnh đâm chồm và rung lắc!
                            car.PlayBlockedBumpAnimation();
                        }
                        return;
                    }
                }
            }
        }

        public bool TryDispatchCar(CarLoopFollower car)
        {
            if (car == null) return false;
            OnCarClicked(car);
            return true;
        }

        /// <summary>
        /// XUẤT BẾN CHUẨN COCOS (LevelController.deployEntityToCartTrack + Bucket.deployToCartTrack):
        /// 1. Giữ chỗ nhập làn (nếu quanh điểm nhập đã có xe thì xe lao ra với tốc độ giảm).
        /// 2. Xe lao THẲNG tới điểm nhập làn trên tim đường với tốc độ tối đa (road.max_speed × 1.5), nhún nhẹ 0.2 và xoay dần về hướng đường.
        /// 3. Tới nơi: đăng ký vào đường đua đúng vị trí đó rồi chạy tiếp theo cơ chế CartTrackController.
        /// </summary>
        private IEnumerator DispatchCarRoutine(CarLoopFollower car, int colIndex)
        {
            if (car == null) yield break;

            car.StopBlockedBumpAnimation();

            // Cocos: bucket rời bãi ngay khi được chọn → các xe phía sau dồn lên luôn, không chờ xe nhập làn xong
            AdvanceColumn(colIndex);

            var track = LoopTrackSystem.Instance;
            CartDeployPlan plan = track != null ? track.CreateCartDeployPlan(car.transform.position) : null;
            if (plan == null || !plan.valid) yield break;

            var reservation = track.PrepareCartDeploy(car, plan);
            if (reservation == null) yield break;

            float speed = reservation.reducedSpeed ? track.BlockedDeployMoveSpeed : track.MaxMoveSpeed;
            Vector3 startPos = car.transform.position;
            Quaternion startRot = car.transform.rotation;
            float distance = Vector3.Distance(startPos, plan.mergeWorld);
            float duration = Mathf.Max(0.1f, distance / Mathf.Max(0.01f, speed));
            float arcHeight = track.deployArcHeight;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (car == null) yield break;
                if (!track.IsCartDeployActive(car)) yield break; // bị huỷ (nạp màn mới...)

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Chuyển động tuyến tính + cung nhún trục Y (Cocos: tween linear, arcHeight 0.2)
                Vector3 pos = Vector3.Lerp(startPos, plan.mergeWorld, t);
                pos.y = Mathf.Lerp(startPos.y, plan.mergeWorld.y, t) + Mathf.Sin(Mathf.PI * t) * arcHeight;
                car.transform.position = pos;
                car.transform.rotation = Quaternion.Slerp(startRot, plan.targetRotation, t);

                yield return null;
            }

            if (car == null) yield break;

            if (track.IsCartDeployActive(car))
            {
                car.transform.position = plan.mergeWorld;
                track.RegisterCartAtDeployPlan(car, plan);
            }
        }

        private void AdvanceColumn(int colIndex)
        {
            if (colIndex < 0 || colIndex >= columnsOfCars.Count) return;

            var colList = columnsOfCars[colIndex];

            for (int r = 0; r < colList.Count; r++)
            {
                CarLoopFollower car = colList[r];
                if (car != null)
                {
                    car.StopBlockedBumpAnimation();
                    Vector3 targetPos = GetSlotPosition(colIndex, r, columnsOfCars.Count);
                    StartCoroutine(SlideCarForward(car, targetPos));
                }
            }
        }

        private IEnumerator SlideCarForward(CarLoopFollower car, Vector3 targetPos)
        {
            Vector3 startPos = car.transform.position;
            float elapsed = 0f;
            float duration = 0.25f;

            while (elapsed < duration && car != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                car.transform.position = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }

            if (car != null) car.transform.position = targetPos;
        }
    }
}
