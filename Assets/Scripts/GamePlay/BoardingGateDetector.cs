using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DouyinGame.GamePlay
{
    public class BoardingGateDetector : MonoBehaviour
    {
        public static BoardingGateDetector Instance { get; private set; }

        [Header("Thông Số Cổng Đón Khách")]
        public float checkRadius = 1.8f;
        public float absorbInterval = 0.1f; // Nhịp độ hút khách
        public float flightDuration = 0.28f;
        public float arcPeakHeight = 1.6f;

        [Header("Prefab Hiệu Ứng Khách Bay")]
        public GameObject flightActorPrefab;

        private float nextAbsorbTime = 0f;

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            if (Time.time < nextAbsorbTime) return;

            // Quét các xe đang chạy ngang qua vùng cổng đón
            Collider[] hits = Physics.OverlapSphere(transform.position, checkRadius);
            foreach (var hit in hits)
            {
                CarLoopFollower car = hit.GetComponentInParent<CarLoopFollower>();
                if (car != null && car.isInLoop && car.currentSeats > 0 && !car.isPendingExit)
                {
                    if (CheckAndAbsorbPassenger(car))
                    {
                        nextAbsorbTime = Time.time + absorbInterval;
                        break;
                    }
                }
            }
        }

        private bool CheckAndAbsorbPassenger(CarLoopFollower car)
        {
            if (SandBoardManager.Instance == null) return false;

            GameObject beadObj;
            Vector3 worldPos;
            int foundCol, foundRow;

            // Tìm hạt cùng màu ở hàng đáy của tranh cát
            if (SandBoardManager.Instance.TryGetPassengerAtBottom(car.carColor, out beadObj, out worldPos, out foundCol, out foundRow))
            {
                // Bắt đầu chuyến bay Parabol vào thùng xe
                StartCoroutine(FlyPassengerParabola(worldPos, car, foundCol, foundRow));
                return true;
            }

            return false;
        }

        private IEnumerator FlyPassengerParabola(Vector3 startPos, CarLoopFollower car, int col, int row)
        {
            // Spawn temporary actor bay
            GameObject actor = null;
            if (flightActorPrefab != null)
            {
                actor = Instantiate(flightActorPrefab, startPos, Quaternion.identity);
            }
            else
            {
                actor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                var c = actor.GetComponent<Collider>();
                if (c != null) Destroy(c);
            }

            // Nếu dùng SpriteRenderer (như Passenger.prefab): CHỈ ĐỔI SPRITE, ĐỂ NGUYÊN MATERIAL!
            SpriteRenderer sr = actor.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                if (SandBoardManager.Instance != null && SandBoardManager.Instance.passengerColors != null && SandBoardManager.Instance.passengerColors.data.ContainsKey(car.carColor))
                {
                    sr.sprite = SandBoardManager.Instance.passengerColors.data[car.carColor];
                }
            }
            else
            {
                var rend = actor.GetComponentInChildren<MeshRenderer>();
                if (rend != null && SandBoardManager.Instance != null)
                {
                    rend.material.color = SandBoardManager.Instance.GetPaletteColor((int)car.carColor);
                }
            }

            float elapsed = 0f;

            while (elapsed < flightDuration && car != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flightDuration);

                Vector3 carPos = car.transform.position + Vector3.up * 0.8f;
                Vector3 currentPos = Vector3.Lerp(startPos, carPos, t);
                // Quỹ đạo Parabol hình vòng cung
                currentPos.y += Mathf.Sin(Mathf.PI * t) * arcPeakHeight;

                if (actor != null) actor.transform.position = currentPos;
                yield return null;
            }

            if (actor != null) Destroy(actor);

            // Khi khách chạm vào xe
            if (car != null)
            {
                car.AbsorbPassenger();
            }

            // Tiêu thụ hạt cát trên bảng và kích hoạt sạt lở
            if (SandBoardManager.Instance != null)
            {
                SandBoardManager.Instance.ConsumeBead(col, row);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.4f);
            Gizmos.DrawSphere(transform.position, checkRadius);
        }
    }
}
