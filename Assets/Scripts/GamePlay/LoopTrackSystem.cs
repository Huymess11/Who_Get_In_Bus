using System.Collections.Generic;
using UnityEngine;

namespace DouyinGame.GamePlay
{
    public class LoopTrackSystem : MonoBehaviour
    {
        public static LoopTrackSystem Instance { get; private set; }

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

        [Header("Thông Số Chuyển Động Chuẩn")]
        public float baseSpeed = 6.0f;
        public float rotateSpeed = 8.0f;
        public float lookAheadDistance = 0.45f;
        public float followGapDistance = 1.65f;
        public float maxBankingLean = 6.0f;

        [Header("Luật Hợp Thể 3 Xe Cùng Màu (Extra Car Merge)")]
        [Tooltip("Prefab xe buýt 2 tầng Extra Car tạo ra khi gộp 3 xe cùng màu")]
        public GameObject extraCarPrefab;
        public BusColorData busColors;
        public GamePrefabData prefabData;

        // Danh sách xe đang hoạt động trên đường
        public List<CarLoopFollower> activeCarsOnTrack = new List<CarLoopFollower>();

        private void Awake()
        {
            Instance = this;
        }

        public void RegisterCarOnTrack(CarLoopFollower car)
        {
            if (car == null) return;
            if (!activeCarsOnTrack.Contains(car))
            {
                activeCarsOnTrack.Add(car);
            }
            CheckAndMergeSameColorCars(car.carColor);
        }

        public void UnregisterCarFromTrack(CarLoopFollower car)
        {
            if (car == null) return;
            activeCarsOnTrack.Remove(car);
        }

        public void CheckAndMergeSameColorCars(GameColorType color)
        {
            // Lọc các xe thường cùng màu đang chạy trên đường (chưa thoát, chưa merge, chưa phải Extra Car)
            List<CarLoopFollower> sameColorCars = new List<CarLoopFollower>();
            foreach (var car in activeCarsOnTrack)
            {
                if (car != null && car.isInLoop && !car.isPendingExit && !car.isMerging && !car.isExtraCar && car.carColor == color)
                {
                    sameColorCars.Add(car);
                }
            }

            // ĐỦ 3 XE CÙNG MÀU TRÊN ĐƯỜNG -> KÍCH HOẠT LUẬT GỘP THÀNH EXTRA CAR 2 TẦNG!
            if (sameColorCars.Count >= 3)
            {
                // Sắp xếp xe đi trước nhất sẽ là leadCar
                sameColorCars.Sort((a, b) => b.currentWaypointIdx.CompareTo(a.currentWaypointIdx));
                StartCoroutine(MergeThreeCarsRoutine(sameColorCars[0], sameColorCars[1], sameColorCars[2], color));
            }
        }

        private System.Collections.IEnumerator MergeThreeCarsRoutine(CarLoopFollower leadCar, CarLoopFollower tailCar1, CarLoopFollower tailCar2, GameColorType color)
        {
            if (leadCar == null || tailCar1 == null || tailCar2 == null) yield break;

            leadCar.isMerging = true;
            tailCar1.isMerging = true;
            tailCar2.isMerging = true;

            Debug.Log($"<color=cyan>⚡ [LUẬT DOUYIN]</color> Phát hiện 3 xe thường màu {color} trên đường! Bắt đầu gộp thành EXTRA CAR 2 tầng!");

            Vector3 startP1 = tailCar1.transform.position;
            Vector3 startP2 = tailCar2.transform.position;
            Vector3 scale1 = tailCar1.transform.localScale;
            Vector3 scale2 = tailCar2.transform.localScale;

            float elapsed = 0f;
            float duration = 0.45f;

            // 2 xe sau lao tới nhập vào xe dẫn đầu
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

                if (tailCar1 != null && leadCar != null)
                {
                    tailCar1.transform.position = Vector3.Lerp(startP1, leadCar.transform.position, t);
                    tailCar1.transform.localScale = Vector3.Lerp(scale1, scale1 * 0.3f, t);
                }
                if (tailCar2 != null && leadCar != null)
                {
                    tailCar2.transform.position = Vector3.Lerp(startP2, leadCar.transform.position, t);
                    tailCar2.transform.localScale = Vector3.Lerp(scale2, scale2 * 0.3f, t);
                }
                yield return null;
            }

            if (leadCar == null) yield break;

            // Tính toán thông số sau khi gộp 3 xe
            int combinedSeats = leadCar.currentSeats;
            int combinedTotal = leadCar.totalSeats;
            if (tailCar1 != null) { combinedSeats += tailCar1.currentSeats; combinedTotal += tailCar1.totalSeats; }
            if (tailCar2 != null) { combinedSeats += tailCar2.currentSeats; combinedTotal += tailCar2.totalSeats; }

            int wpIdx = leadCar.currentWaypointIdx;
            Vector3 spawnPos = leadCar.transform.position;
            Quaternion spawnRot = leadCar.transform.rotation;
            float speed = leadCar.currentSpeed;
            int mech = leadCar.mechanicType;

            // Hủy 2 xe phụ
            if (tailCar1 != null)
            {
                UnregisterCarFromTrack(tailCar1);
                Destroy(tailCar1.gameObject);
            }
            if (tailCar2 != null)
            {
                UnregisterCarFromTrack(tailCar2);
                Destroy(tailCar2.gameObject);
            }

            // Hủy xe dẫn đầu cũ
            UnregisterCarFromTrack(leadCar);
            Destroy(leadCar.gameObject);

            // Sinh ra EXTRA CAR (Xe buýt 2 tầng)
            GameObject prefabToUse = extraCarPrefab;
            if (prefabToUse == null && prefabData != null) prefabToUse = prefabData.extraCarPrefab;

            if (prefabToUse != null)
            {
                GameObject extraObj = Instantiate(prefabToUse, spawnPos, spawnRot, transform);
                extraObj.name = $"ExtraCar_Merged_{color}_{combinedSeats}c";

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

                follower.InitializeCar(color, combinedSeats, mech, colors);
                follower.totalSeats = combinedTotal;
                follower.currentSeats = combinedSeats;
                follower.isExtraCar = true;
                follower.isMerging = false;
                follower.EnterTrack(wpIdx);
                follower.currentSpeed = speed;

                RegisterCarOnTrack(follower);
                follower.PlayMergeSuccessEffect();
            }
        }

        public Vector3 GetLookAheadTangent(Vector3 currentPos, int currentWpIdx, bool isExit)
        {
            var list = isExit ? exitWaypoints : loopWaypoints;
            if (list == null || list.Count == 0) return Vector3.forward;

            int nextIdx = (currentWpIdx + 1) % list.Count;
            if (isExit && nextIdx >= list.Count) nextIdx = list.Count - 1;

            if (nextIdx < list.Count && list[nextIdx] != null)
            {
                Vector3 target = list[nextIdx].position;
                Vector3 dir = (target - currentPos);
                dir.y = 0;
                return dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;
            }
            return Vector3.forward;
        }

        public int GetNextWaypointIndex(int currentIdx, bool isExit)
        {
            var list = isExit ? exitWaypoints : loopWaypoints;
            if (list == null || list.Count == 0) return 0;

            if (isExit)
            {
                return Mathf.Min(currentIdx + 1, list.Count - 1);
            }
            return (currentIdx + 1) % list.Count;
        }

        private void OnDrawGizmos()
        {
            // Vẽ đường Loop chữ U màu xanh lam (Cyan)
            if (loopWaypoints != null && loopWaypoints.Count > 1)
            {
                Gizmos.color = new Color(0.1f, 0.85f, 1f, 0.9f);
                for (int i = 0; i < loopWaypoints.Count; i++)
                {
                    Transform curr = loopWaypoints[i];
                    Transform next = loopWaypoints[(i + 1) % loopWaypoints.Count];
                    if (curr != null && next != null)
                    {
                        Gizmos.DrawLine(curr.position, next.position);
                        Gizmos.DrawSphere(curr.position, 0.25f);
                    }
                }
            }

            // Vẽ Cổng đón khách (Boarding Gate) màu vàng (Yellow)
            if (boardingGatePoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(boardingGatePoint.position, new Vector3(2.5f, 1.5f, 1.5f));
            }

            // Vẽ Ngã rẽ & Đường thoát (Exit) màu xanh lá (Green)
            if (exitWaypoints != null && exitWaypoints.Count > 0)
            {
                Gizmos.color = Color.green;
                if (exitJunctionPoint != null && exitWaypoints[0] != null)
                {
                    Gizmos.DrawLine(exitJunctionPoint.position, exitWaypoints[0].position);
                }
                for (int i = 0; i < exitWaypoints.Count - 1; i++)
                {
                    if (exitWaypoints[i] != null && exitWaypoints[i + 1] != null)
                    {
                        Gizmos.DrawLine(exitWaypoints[i].position, exitWaypoints[i + 1].position);
                        Gizmos.DrawCube(exitWaypoints[i].position, Vector3.one * 0.3f);
                    }
                }
            }
        }
    }
}
