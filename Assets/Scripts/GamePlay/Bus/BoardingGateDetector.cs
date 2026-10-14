using System.Collections;
using UnityEngine;

namespace WhoGetInBus.GamePlay
{
    /// <summary>
    /// CỔNG ĐÓN KHÁCH - VÙNG ĐÓN (Boarding Zone)
    /// 
    /// CÁCH HOẠT ĐỘNG CHUẨN GAME GỐC COCOS:
    /// - Không phải 1 điểm nhỏ xíu với bán kính nhỏ.
    /// - Đây là 1 "trạm" gắn vào MỖI waypoint trong đoạn cua U-turn tiếp giáp tranh cát.
    /// - Khi LoadRoadFromConfig(), code tự tìm TẤT CẢ waypoints có Z > boardingZoneMinZ
    ///   (tức là toàn bộ cua U-turn phía trên, sát chân tranh cát) và gắn BoardingGateDetector
    ///   vào từng điểm một.
    /// - Xe chạy qua bất kỳ điểm nào trong vùng đó đều được hút khách → mượt mà liên tục!
    /// 
    /// Ví dụ Road 5 có 4 waypoints trong vùng boarding (Z > 2.0 trong Unity):
    ///   WP[8]  → (0.85, 0, +3.51)   ← vào cua
    ///   WP[9]  → (0.38, 0, +4.33)   ← đỉnh cua phải
    ///   WP[10] → (-0.38, 0, +4.33)  ← đỉnh cua trái
    ///   WP[11] → (-0.88, 0, +3.51)  ← ra cua
    /// → Xe mất ~2 giây đi qua, hút được nhiều khách liên tục!
    /// </summary>
    public class BoardingGateDetector : MonoBehaviour
    {
        // KHÔNG dùng Singleton vì có nhiều điểm cùng là boarding gate
        public static BoardingGateDetector Instance { get; private set; }

        [Header("Thông Số Đón Khách")]
        [Tooltip("Bán kính phát hiện xe - nhỏ thôi (1.5f) vì mỗi waypoint chỉ cần ăn xe đi qua đó")]
        public float checkRadius = 1.5f;

        [Tooltip("Nhịp độ hút khách (giây/lần) - chia sẻ chung toàn bộ boarding zone")]
        public float absorbInterval = 0.12f;

        public float flightDuration = 0.28f;
        public float arcPeakHeight = 1.6f;

        [Header("Prefab Hiệu Ứng Khách Bay")]
        public GameObject flightActorPrefab;

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            // Tránh xung đột trùng lặp: việc đón khách theo vị trí thực tế trên đường đua
            // đã được thực hiện trực tiếp bởi CarLoopFollower.CheckAndAbsorbPassengersAlongTrack()!
        }

        private bool CheckAndAbsorbPassenger(Car car)
        {
            if (SandBoardManager.Instance == null) return false;

            if (SandBoardManager.Instance.TryGetPassengerAtBottom(car.carColor, out GameObject beadObj, out Vector3 worldPos, out int foundCol, out int foundRow))
            {
                StartCoroutine(FlyPassengerParabola(worldPos, car, foundCol, foundRow));
                return true;
            }
            return false;
        }

        private IEnumerator FlyPassengerParabola(Vector3 startPos, Car car, int col, int row)
        {
            GameObject actor = null;
            if (flightActorPrefab != null)
            {
                actor = Instantiate(flightActorPrefab, startPos, Quaternion.identity);
            }
            else
            {
                actor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                actor.transform.position = startPos;
                actor.transform.localScale = Vector3.one * 0.35f;
                var colComponent = actor.GetComponent<Collider>();
                if (colComponent != null) Destroy(colComponent);
            }

            if (actor != null && SandBoardManager.Instance != null)
            {
                var sr = actor.GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                {
                    if (SandBoardManager.Instance.passengerColors != null &&
                        SandBoardManager.Instance.passengerColors.data.TryGetValue(car.carColor, out Sprite sprite))
                    {
                        sr.sprite = sprite;
                    }
                }
                else
                {
                    var meshRend = actor.GetComponentInChildren<MeshRenderer>();
                    if (meshRend != null)
                        meshRend.material.color = SandBoardManager.Instance.GetPaletteColor((int)car.carColor);
                }
            }

            float elapsed = 0f;
            while (elapsed < flightDuration && car != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flightDuration);
                Vector3 carPos = car.transform.position + Vector3.up * 0.8f;
                Vector3 pos = Vector3.Lerp(startPos, carPos, t);
                pos.y += 4f * arcPeakHeight * t * (1f - t);
                if (actor != null) actor.transform.position = pos;
                yield return null;
            }

            if (actor != null) Destroy(actor);
            if (car != null) car.AbsorbPassenger();
            SandBoardManager.Instance?.ConsumeBead(col, row);
        }
    }
}
