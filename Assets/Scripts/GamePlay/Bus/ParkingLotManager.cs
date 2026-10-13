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

        private IEnumerator DispatchCarRoutine(CarLoopFollower car, int colIndex)
        {
            if (car == null) yield break;

            car.StopBlockedBumpAnimation();

            Vector3 startPos = car.transform.position;
            int entryWpIdx = LoopTrackSystem.Instance != null ? LoopTrackSystem.Instance.parkingEntryWaypointIdx : 0;
            Transform entryWp = LoopTrackSystem.Instance != null && LoopTrackSystem.Instance.parkingEntryWaypoint != null
                ? LoopTrackSystem.Instance.parkingEntryWaypoint
                : null;

            Vector3 entryPos = entryWp != null
                ? entryWp.position
                : (transform.position + Vector3.forward * 4f);

            int trackCount = LoopTrackSystem.Instance != null && LoopTrackSystem.Instance.loopWaypoints != null
                ? LoopTrackSystem.Instance.loopWaypoints.Count
                : 0;
            int nextWpIdx = trackCount > 0 ? (entryWpIdx + 1) % trackCount : 0;
            Vector3 nextWpPos = trackCount > 0 && LoopTrackSystem.Instance.loopWaypoints[nextWpIdx] != null
                ? LoopTrackSystem.Instance.loopWaypoints[nextWpIdx].position
                : (entryPos + Vector3.forward);

            Vector3 trackTangent = nextWpPos - entryPos;
            trackTangent.y = 0;
            if (trackTangent.sqrMagnitude < 0.001f) trackTangent = Vector3.forward;
            Quaternion targetRot = Quaternion.LookRotation(trackTangent.normalized, Vector3.up);

            // QUỸ ĐẠO XUẤT BẾN CHUẨN GỐC COCOS (Cubic Bezier Deploy Path):
            // 1. P0: Vị trí xuất phát trong bãi đỗ xe
            Vector3 p0 = startPos;

            // 2. P1: Xe chạy THẲNG TIẾN LÊN PHÍA TRƯỚC (hướng +Z) một đoạn để ra khỏi làn bãi đỗ!
            float straightForwardDist = Mathf.Clamp((entryPos.z - startPos.z) * 0.45f, 1.2f, 2.2f);
            Vector3 p1 = startPos + Vector3.forward * straightForwardDist;

            // 3. P2: Điểm uốn cong tiếp cận dần vào hướng tiếp tuyến của đường đua
            Vector3 p2 = new Vector3(
                Mathf.Lerp(startPos.x, entryPos.x, 0.75f),
                startPos.y,
                Mathf.Lerp(p1.z, entryPos.z, 0.65f)
            );

            // 4. P3: Điểm nhập làn trên đường đua
            Vector3 p3 = entryPos;

            float elapsed = 0f;
            float totalDist = Vector3.Distance(startPos, entryPos);
            float duration = Mathf.Clamp(totalDist / 6.5f, 0.65f, 0.85f);
            const float arcPeakHeight = 0.15f; // Độ nảy ga nhẹ khi xuất bến chuẩn Cocos

            Quaternion initialRot = car.transform.rotation;

            while (elapsed < duration && car != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easeT = Mathf.SmoothStep(0f, 1f, t);

                // Tính toạ độ theo đường cong Cubic Bezier
                float oneMinusT = 1f - easeT;
                Vector3 currentPos =
                    oneMinusT * oneMinusT * oneMinusT * p0 +
                    3f * oneMinusT * oneMinusT * easeT * p1 +
                    3f * oneMinusT * easeT * easeT * p2 +
                    easeT * easeT * easeT * p3;

                // Độ nhún nảy cung vòng trục Y hình sin
                currentPos.y += Mathf.Sin(Mathf.PI * easeT) * arcPeakHeight;
                car.transform.position = currentPos;

                // Tính tiếp tuyến đạo hàm của đường cong Bezier để xoay đầu xe mượt mà từ từ
                Vector3 tangent =
                    3f * oneMinusT * oneMinusT * (p1 - p0) +
                    6f * oneMinusT * easeT * (p2 - p1) +
                    3f * easeT * easeT * (p3 - p2);
                tangent.y = 0;

                if (tangent.sqrMagnitude > 0.001f)
                {
                    Quaternion curveRot = Quaternion.LookRotation(tangent.normalized, Vector3.up);
                    // Hòa trộn góc quay: ở đầu t=0 giữ thẳng, càng về cuối càng bám sát góc targetRot
                    car.transform.rotation = Quaternion.Slerp(initialRot, curveRot, Mathf.Clamp01(t * 1.35f));
                }

                yield return null;
            }

            if (car != null)
            {
                car.transform.position = entryPos;
                car.transform.rotation = targetRot;
                car.EnterTrack(nextWpIdx);

                if (LoopTrackSystem.Instance != null)
                {
                    LoopTrackSystem.Instance.RegisterCarOnTrack(car);
                }
            }

            // Cho các xe phía sau trong cột này dồn lên phía trước
            AdvanceColumn(colIndex);
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
