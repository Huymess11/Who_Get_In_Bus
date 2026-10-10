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

        // Lưu trữ danh sách xe theo từng cột: column[c] chứa hàng đợi các xe từ trước ra sau
        private List<List<CarLoopFollower>> columnsOfCars = new List<List<CarLoopFollower>>();

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// Nạp và sinh trực tiếp bãi đỗ xe từ Level ID chuẩn gốc (1001, 1002, ...)
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

                    Vector3 slotPos = transform.position + new Vector3(startX + c * slotSpacingX, 0f, -r * slotSpacingZ);
                    GameObject carObj = null;
#if UNITY_EDITOR
                    if (!Application.isPlaying && chosenPrefab != null)
                    {
                        carObj = (GameObject)PrefabUtility.InstantiatePrefab(chosenPrefab, transform);
                        carObj.transform.position = slotPos;
                        carObj.transform.rotation = Quaternion.identity;
                    }
                    else
                    {
                        carObj = Instantiate(chosenPrefab, slotPos, Quaternion.identity, transform);
                    }
#else
                    carObj = Instantiate(chosenPrefab, slotPos, Quaternion.identity, transform);
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

                    follower.InitializeCar((GameColorType)colorIdx, cap, mech, busColors);
                    colList.Add(follower);
                }

                columnsOfCars.Add(colList);
            }

            Debug.Log($"<color=green>[ParkingLotManager]</color> Đã sinh bãi đỗ {cols} cột xe thành công!");
        }

        public void ClearParkingLot()
        {
            List<GameObject> toDestroy = new List<GameObject>();
            for (int i = 0; i < transform.childCount; i++)
            {
                toDestroy.Add(transform.GetChild(i).gameObject);
            }
            foreach (var g in toDestroy)
            {
                if (Application.isPlaying) Destroy(g);
                else DestroyImmediate(g);
            }
            columnsOfCars.Clear();
        }

        private void Update()
        {
            // Xử lý click/tap chọn xe trên bãi đỗ
            if (Input.GetMouseButtonDown(0))
            {
                HandlePlayerClick();
            }
        }

        private void HandlePlayerClick()
        {
            Camera cam = Camera.main;
            if (cam == null) cam = FindFirstObjectByType<Camera>();
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f))
            {
                CarLoopFollower car = hit.collider.GetComponentInParent<CarLoopFollower>();
                if (car != null && !car.isInLoop)
                {
                    TryDispatchCar(car);
                }
            }
        }

        public bool TryDispatchCar(CarLoopFollower car)
        {
            // Tìm xem xe nằm ở cột nào và có phải là xe đầu hàng không
            for (int c = 0; c < columnsOfCars.Count; c++)
            {
                var colList = columnsOfCars[c];
                if (colList.Count > 0 && colList[0] == car)
                {
                    // Xe ở đầu hàng -> Xuất bến vào đường Loop!
                    colList.RemoveAt(0);
                    StartCoroutine(DispatchCarRoutine(car, c));
                    return true;
                }
            }
            return false;
        }

        private IEnumerator DispatchCarRoutine(CarLoopFollower car, int colIndex)
        {
            Vector3 startPos = car.transform.position;
            Vector3 entryPos = LoopTrackSystem.Instance != null && LoopTrackSystem.Instance.parkingEntryWaypoint != null
                ? LoopTrackSystem.Instance.parkingEntryWaypoint.position
                : (transform.position + Vector3.forward * 8f);

            float elapsed = 0f;
            float duration = 0.6f;

            while (elapsed < duration && car != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                car.transform.position = Vector3.Lerp(startPos, entryPos, t);
                yield return null;
            }

            if (car != null)
            {
                car.transform.position = entryPos;
                car.EnterTrack(0);

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
            float startX = -((columnsOfCars.Count - 1) * slotSpacingX) * 0.5f;

            for (int r = 0; r < colList.Count; r++)
            {
                CarLoopFollower car = colList[r];
                if (car != null)
                {
                    Vector3 targetPos = transform.position + new Vector3(startX + colIndex * slotSpacingX, 0f, -r * slotSpacingZ);
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
