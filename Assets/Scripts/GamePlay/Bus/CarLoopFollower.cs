using UnityEngine;

namespace WhoGetInBus.GamePlay
{
    /// <summary>
    /// XE CHẠY ĐƯỜNG RAY (Kế thừa từ Car Base) — trạng thái runtime của 1 cart trong Cocos CartTrackController.
    ///
    /// Việc DI CHUYỂN và ĂN CÁT do LoopTrackSystem điều phối tập trung (giống Cocos):
    ///  - LoopTrackSystem tính "hạn mức quãng đường" (moveAllowance) mỗi khung hình cho từng xe, có giới hạn theo khoảng cách
    ///    an toàn với xe phía trước, rồi gọi ApplyTrackTransform để đặt vị trí/hướng xe.
    ///  - Đầu xe quay mượt theo tiếp tuyến nhìn trước (Exponential Damping Yaw),
    ///    thân xe nghiêng ôm cua bằng lò xo giảm xóc (Spring-Damper Banking Lean).
    ///  - Xe chỉ hút khách khi nằm ở đoạn từ điểm vào vòng loop tới ngã rẽ thoát (isLoopSkipAbsorb = false) và không ở trong hầm.
    ///  - Khi nhận đủ khách (ghế = 0 và không còn khách đang bay) xe đánh dấu chờ rẽ ra làn thoát tại ngã rẽ.
    /// </summary>
    public class CarLoopFollower : Car
    {
        [Header("Trạng Thái Di Chuyển Đường Ray")]
        [Tooltip("Khoảng cách trên vòng loop (hoặc trên đường thoát khi isExiting)")]
        public float currentTrackDistance = 0f;
        public int currentWaypointIdx = 0;
        [Tooltip("Tốc độ hiệu dụng hiện tại (đơn vị/giây) do LoopTrackSystem tính")]
        public float currentSpeed = 5.0f;
        public float bankingLean = 0f;
        public bool isExiting = false;

        [Header("Cờ Đoạn Đường (Cocos: cart runtime)")]
        [Tooltip("Đang ở đoạn cuối vòng (từ điểm áp chót tới điểm đầu) → được nhân tốc độ accelerateSpeedRatio")]
        public bool isLoopSegmentBoost = false;
        [Tooltip("Đang ngoài đoạn được phép ăn cát (sau ngã rẽ thoát, trước điểm vào vòng)")]
        public bool isLoopSkipAbsorb = true;
        [Tooltip("Đang chui trong hầm: xe ẩn đi và không ăn cát")]
        public bool isInCave = false;
        [Tooltip("Số khách đón được trong lượt vòng hiện tại (Cocos: cart.count)")]
        public int lapPassengerCount = 1;
        [Tooltip("Số vòng liên tiếp không đón được ai (Cocos: emptyRoundCount)")]
        public int emptyRoundCount = 0;
        [Tooltip("Cuối màn: sau 2 vòng trống thì được hút rộng hơn (Cocos: relaxedAbsorbUnlocked)")]
        public bool relaxedAbsorbUnlocked = false;

        [Header("Nhập Làn Bị Chặn (Cocos: deploySpeedLimited)")]
        public bool deploySpeedLimited = false;
        public float deploySpeedRatio = 1f;

        [HideInInspector] public float deployOverlapEscapeRatio = 0f;
        [HideInInspector] public float moveAllowance = 0f;

        // Trạng thái hướng đầu xe & nghiêng thân xe
        private float bodyLeanDeg = 0f;
        private float bodyLeanVelocity = 0f;
        private Quaternion facingCurrentRot = Quaternion.identity;

        /// <summary>
        /// Đưa xe nhập làn vào đường đua tại khoảng cách distance trên spline (đặt lại hướng đầu xe theo tiếp tuyến).
        /// </summary>
        public void EnterTrack(float startDistance)
        {
            EnterTrack(startDistance, true);
        }

        /// <summary>
        /// Đưa xe nhập làn tại khoảng cách distance. snapFacing = false: giữ hướng đầu xe hiện tại
        /// (xe vừa lao từ bãi đỗ ra đã xoay dần về hướng đường) rồi tiếp tục xoay mượt.
        /// </summary>
        public void EnterTrack(float startDistance, bool snapFacing)
        {
            isInLoop = true;
            isPendingExit = false;
            isExiting = false;
            isMerging = false;
            currentTrackDistance = startDistance;

            lapPassengerCount = 1;
            emptyRoundCount = 0;
            relaxedAbsorbUnlocked = false;
            deploySpeedLimited = false;
            deploySpeedRatio = 1f;
            deployOverlapEscapeRatio = 0f;
            moveAllowance = 0f;

            var track = LoopTrackSystem.Instance;
            currentSpeed = track != null ? track.MoveSpeed : 5.0f;
            if (track == null) return;

            track.SyncLoopSegmentFlags(this);
            track.SyncCaveState(this);

            transform.position = track.EvaluatePosition(currentTrackDistance, false);
            if (snapFacing)
            {
                Vector3 initialTangent = track.SampleLookAheadTangent(currentTrackDistance, track.LookAhead, false);
                if (initialTangent.sqrMagnitude > 0.001f)
                {
                    facingCurrentRot = Quaternion.LookRotation(initialTangent, Vector3.up);
                    transform.rotation = facingCurrentRot;
                }
            }
            else
            {
                facingCurrentRot = transform.rotation;
            }
            ResetLean();
        }

        /// <summary>
        /// Nạp xe theo mốc waypoint (Hỗ trợ tương thích ngược)
        /// </summary>
        public void EnterTrack(int startWaypointIdx = 0)
        {
            var track = LoopTrackSystem.Instance;
            float dist = 0f;
            if (track != null && track.loopWaypoints != null && startWaypointIdx < track.loopWaypoints.Count && track.loopWaypoints[startWaypointIdx] != null)
            {
                dist = track.FindClosestLoopDistance(track.loopWaypoints[startWaypointIdx].position);
            }
            EnterTrack(dist);
        }

        public void ResetLean()
        {
            bodyLeanDeg = 0f;
            bodyLeanVelocity = 0f;
            bankingLean = 0f;
        }

        private void Update()
        {
            // Di chuyển & ăn cát do LoopTrackSystem điều phối tập trung
            UpdateBounce();
        }

        #region ÁP DỤNG VỊ TRÍ & HƯỚNG (Cocos applyCursorWorldTransform)

        /// <summary>
        /// Áp dụng vị trí chuẩn 100% tim đường, xoay đầu xe mượt mà + nghiêng thân xe ôm cua.
        /// instant = true: xoay ngay không nội suy (vừa nhập làn / vừa rẽ sang làn thoát).
        /// </summary>
        public void ApplyTrackTransform(LoopTrackSystem track, float dt, bool instant)
        {
            Vector3 pos = track.EvaluatePosition(currentTrackDistance, isExiting);
            transform.position = pos;

            if (!isExiting) currentWaypointIdx = track.FindClosestWaypointIndex(pos);

            Vector3 heading = track.SampleLookAheadTangent(currentTrackDistance, track.LookAhead, isExiting);
            if (heading.sqrMagnitude <= 0.001f) return;

            Quaternion targetRot = Quaternion.LookRotation(heading, Vector3.up);
            if (instant || dt <= 0f || track.rotateSpeed <= 0f)
            {
                facingCurrentRot = targetRot;
            }
            else
            {
                float factor = 1f - Mathf.Exp(-track.rotateSpeed * dt);
                facingCurrentRot = Quaternion.Slerp(facingCurrentRot, targetRot, factor);
            }

            // Vật lý nghiêng thân xe (Spring-Damper Banking Lean chuẩn Cocos 1:1)
            float targetLean = track.GetTurnLeanTargetDeg(currentTrackDistance, isExiting);
            if (instant || dt <= 0f)
            {
                bodyLeanDeg = targetLean;
                bodyLeanVelocity = 0f;
            }
            else
            {
                float dtClamped = Mathf.Min(dt, 0.05f);
                float accel = 85f * (targetLean - bodyLeanDeg) - 10f * bodyLeanVelocity;
                bodyLeanVelocity += accel * dtClamped;
                bodyLeanDeg += bodyLeanVelocity * dtClamped;

                float maxLean = track.maxBankingLean;
                if (bodyLeanDeg > maxLean)
                {
                    bodyLeanDeg = maxLean;
                    bodyLeanVelocity = Mathf.Min(0f, bodyLeanVelocity);
                }
                else if (bodyLeanDeg < -maxLean)
                {
                    bodyLeanDeg = -maxLean;
                    bodyLeanVelocity = Mathf.Max(0f, bodyLeanVelocity);
                }
            }
            bankingLean = bodyLeanDeg;

            transform.rotation = facingCurrentRot * Quaternion.Euler(0f, 0f, bodyLeanDeg);
        }

        #endregion

        #region NHẬN KHÁCH (Cocos onAbsorbSandArrive / onBucketFilled)

        /// <summary>
        /// Khách bay tới nơi: trừ ghế, cộng vào số khách của vòng, và nếu xe đã đầy thì chuẩn bị rẽ ra làn thoát.
        /// </summary>
        public override void OnBoardingArrive(int count)
        {
            base.OnBoardingArrive(count);

            var track = LoopTrackSystem.Instance;
            if (track != null) track.AddLapPassengerCount(this, count);

            CheckFilled(track);
        }

        private void CheckFilled(LoopTrackSystem track)
        {
            if (isPendingExit || isExiting) return;
            if (currentSeats > 0 || inFlightSandCount > 0) return;
            if (track != null && track.HasPendingBoarding(this)) return;

            if (track != null) track.MarkCartPendingExit(this);
            else isPendingExit = true;
        }

        #endregion

        /// <summary>
        /// Xe đã hoàn thành chặng đường và chạy ra khỏi màn hình
        /// </summary>
        public void FinishExit()
        {
            isInLoop = false;
            isExiting = false;
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
