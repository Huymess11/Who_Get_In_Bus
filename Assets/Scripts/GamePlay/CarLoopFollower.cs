using UnityEngine;
using TMPro;

namespace WhoGetInBus.GamePlay
{
    public class CarLoopFollower : MonoBehaviour
    {
        [Header("Thông Tin Xe")]
        public GameColorType carColor = GameColorType.Yellow;
        public int totalSeats = 100;
        public int currentSeats = 100;
        public int mechanicType = 0; // 0: Normal, 1: Lock, 2: Ice, 5: Fly, 501: Key
        public bool isPendingExit = false;
        public bool isInLoop = false;
        public bool isExtraCar = false; // Đã là xe 2 tầng Extra Car chưa
        public bool isMerging = false; // Đang trong quá trình hiệu ứng gộp 3 xe

        [Header("Hiển Thị & Giao Diện")]
        public TMP_Text seatText;
        public MeshRenderer bodyRenderer;

        [Header("Trạng Thái Di Chuyển")]
        public int currentWaypointIdx = 0;
        public float currentSpeed = 6.0f;
        public float bankingLean = 0f;

        private Vector3 originalScale;
        private float bounceTimer = 0f;

        private void Awake()
        {
            originalScale = transform.localScale;
            if (seatText == null) seatText = GetComponentInChildren<TMP_Text>();
            if (bodyRenderer == null)
            {
                var bodyChild = transform.Find("Body");
                if (bodyChild != null) bodyRenderer = bodyChild.GetComponent<MeshRenderer>();
            }
        }

        public void InitializeCar(GameColorType color, int seats, int mechanic, BusColorData busColors)
        {
            carColor = color;
            totalSeats = seats;
            currentSeats = seats;
            mechanicType = mechanic;
            isPendingExit = false;
            isInLoop = false;

            UpdateVisuals(busColors);
        }

        public void UpdateVisuals(BusColorData busColors)
        {
            if (seatText == null) seatText = GetComponentInChildren<TMP_Text>();
            if (bodyRenderer == null)
            {
                var bodyChild = transform.Find("Body");
                if (bodyChild != null) bodyRenderer = bodyChild.GetComponent<MeshRenderer>();
                if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<MeshRenderer>();
            }

            // Cập nhật chữ số chỗ trên nóc xe
            if (seatText != null)
            {
                seatText.text = currentSeats.ToString();
            }

            // Gắn Material đúng màu từ BusColorData
            if (busColors != null && busColors.data.ContainsKey(carColor) && bodyRenderer != null)
            {
                bodyRenderer.material = busColors.data[carColor];
            }
        }

        public void EnterTrack(int startWaypointIdx = 0)
        {
            isInLoop = true;
            currentWaypointIdx = startWaypointIdx;
            currentSpeed = LoopTrackSystem.Instance != null ? LoopTrackSystem.Instance.baseSpeed : 6f;
        }

        private void Update()
        {
            if (!isInLoop || LoopTrackSystem.Instance == null) return;

            // Xử lý hiệu ứng nảy xe khi nhận khách
            if (bounceTimer > 0f)
            {
                bounceTimer -= Time.deltaTime;
                float punch = Mathf.Sin(bounceTimer * Mathf.PI * 8f) * 0.12f;
                transform.localScale = originalScale + new Vector3(punch, punch * 1.5f, punch);
            }
            else
            {
                transform.localScale = originalScale;
            }

            MoveAlongPath();
        }

        private void MoveAlongPath()
        {
            if (isMerging) return; // Nếu đang trong quá trình hiệu ứng gộp xe thì không di chuyển theo waypoint

            var track = LoopTrackSystem.Instance;
            var waypoints = isPendingExit ? track.exitWaypoints : track.loopWaypoints;
            if (waypoints == null || waypoints.Count == 0) return;

            Transform targetWp = waypoints[Mathf.Clamp(currentWaypointIdx, 0, waypoints.Count - 1)];
            if (targetWp == null) return;

            Vector3 toTarget = (targetWp.position - transform.position);
            toTarget.y = 0;
            float dist = toTarget.magnitude;

            // 1. Chống đâm xe: Kiểm tra khoảng cách với xe phía trước
            float effectiveSpeed = currentSpeed;
            if (IsCarAheadBlocked(track.followGapDistance))
            {
                effectiveSpeed = Mathf.Max(0.5f, currentSpeed * 0.2f); // Hãm phanh an toàn
            }

            // 2. Di chuyển xe về điểm target
            transform.position = Vector3.MoveTowards(transform.position, targetWp.position, effectiveSpeed * Time.deltaTime);

            // 3. Xoay thân xe theo tiếp tuyến Look-Ahead và nghiêng thân xe (Banking Lean)
            Vector3 tangent = track.GetLookAheadTangent(transform.position, currentWaypointIdx, isPendingExit);
            if (tangent != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(tangent, Vector3.up);

                // Tính góc cua để tạo hiệu ứng nghiêng thân xe mượt mà
                float angle = Vector3.SignedAngle(transform.forward, tangent, Vector3.up);
                float targetRoll = Mathf.Clamp(-angle * 0.35f, -track.maxBankingLean, track.maxBankingLean);
                bankingLean = Mathf.Lerp(bankingLean, targetRoll, Time.deltaTime * 6f);

                Quaternion rollRot = Quaternion.Euler(0, 0, bankingLean);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot * rollRot, Time.deltaTime * track.rotateSpeed);
            }

            // 4. Khi chạm mốc Waypoint -> chuyển điểm tiếp theo
            if (dist < 0.35f)
            {
                // Kiểm tra ngã rẽ: nếu xe đã đầy và đang ở điểm Junction -> rẽ ra Exit!
                if (!isPendingExit && currentSeats <= 0 && track.exitJunctionPoint != null)
                {
                    if (Vector3.Distance(transform.position, track.exitJunctionPoint.position) < 1.5f)
                    {
                        isPendingExit = true;
                        currentWaypointIdx = 0;
                        return;
                    }
                }

                if (isPendingExit)
                {
                    if (currentWaypointIdx >= waypoints.Count - 1)
                    {
                        // Đã chạy hết đường thoát -> biến mất và báo thành công
                        OnExitFinished();
                        return;
                    }
                    currentWaypointIdx++;
                }
                else
                {
                    currentWaypointIdx = (currentWaypointIdx + 1) % waypoints.Count;
                }
            }
        }

        private bool IsCarAheadBlocked(float gapDist)
        {
            if (isMerging) return false;

            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward, out hit, gapDist))
            {
                CarLoopFollower otherCar = hit.collider.GetComponentInParent<CarLoopFollower>();
                if (otherCar != null && otherCar != this && !otherCar.isMerging)
                {
                    return true;
                }
            }
            return false;
        }

        public void AbsorbPassenger()
        {
            if (currentSeats > 0)
            {
                currentSeats--;
                if (seatText != null) seatText.text = currentSeats.ToString();
                bounceTimer = 0.25f; // Kích hoạt nhún thân xe
            }
        }

        public void PlayMergeSuccessEffect()
        {
            bounceTimer = 0.5f;
            transform.localScale = originalScale * 1.4f;
            Debug.Log($"<color=yellow>⭐ [HỢP THỂ THÀNH CÔNG]</color> Đã gộp 3 xe màu {carColor} thành EXTRA CAR 2 tầng với {currentSeats} chỗ!");
        }

        private void OnExitFinished()
        {
            isInLoop = false;
            if (LoopTrackSystem.Instance != null)
            {
                LoopTrackSystem.Instance.UnregisterCarFromTrack(this);
            }
            gameObject.SetActive(false);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnCarCompleted(this);
            }
        }
    }
}
