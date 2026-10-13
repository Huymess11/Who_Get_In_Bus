using UnityEngine;

namespace WhoGetInBus.GamePlay
{
    /// <summary>
    /// ĐIỀU KHIỂN XE CHẠY ĐƯỜNG RAY (Kế thừa từ Car Base)
    /// Chuyên trách:
    /// - Di chuyển bám theo các điểm Waypoint trên đường ray chữ U
    /// - Ôm cua nghiêng thân xe (Banking Lean)
    /// - Tự động giữ khoảng cách chống đâm xe phía trước
    /// - Rẽ vào làn thoát khi đã đón đủ 100% khách
    /// </summary>
    public class CarLoopFollower : Car
    {
        [Header("Trạng Thái Di Chuyển")]
        public int currentWaypointIdx = 0;
        public float currentSpeed = 6.0f;
        public float bankingLean = 0f;

        /// <summary>
        /// Đưa xe nhập làn vào đường đua tại điểm xuất phát
        /// </summary>
        public void EnterTrack(int startWaypointIdx = 0)
        {
            isInLoop = true;
            currentWaypointIdx = startWaypointIdx;
            currentSpeed = LoopTrackSystem.Instance != null ? LoopTrackSystem.Instance.baseSpeed : 6f;
        }

        [Header("Cơ Chế Đón Khách Dọc Đường (Cocos Proximity Boarding)")]
        [Tooltip("Bán kính hút khách viền Gate khi xe chạy ngang qua (chuẩn 2.4m)")]
        public float absorbCheckRadius = 2.4f;
        [Tooltip("Nhịp độ đón khách (chuẩn 0.12 giây/lần)")]
        public float absorbInterval = 0.12f;
        public float flightDuration = 0.28f;
        public float arcPeakHeight = 1.6f;

        private float nextAbsorbCheckTime = 0f;

        private void Update()
        {
            // Cập nhật hiệu ứng rung nhún từ lớp cha Car
            UpdateBounce();

            // Nếu chưa vào làn hoặc đang trong hiệu ứng gộp xe thì không di chuyển
            if (!isInLoop || isMerging || LoopTrackSystem.Instance == null) return;

            MoveAlongTrack();

            // Cơ chế đón khách chuẩn game gốc Cocos:
            // Bất cứ khi nào xe chạy dọc theo đường ray tiếp giáp ô Gate cùng màu -> Hút khách vào xe!
            CheckAndAbsorbPassengersAlongTrack();
        }

        private void CheckAndAbsorbPassengersAlongTrack()
        {
            if (Time.time < nextAbsorbCheckTime) return;
            if (currentSeats <= 0 || isPendingExit || isMerging) return;
            if (SandBoardManager.Instance == null) return;

            // HÚT THEO ĐÀN (BATCH ABSORB): Hút 1 lúc 3 - 6 hạt cùng màu gần xe (chuẩn Cocos)
            int maxCanTake = Mathf.Clamp(currentSeats, 1, 6);
            var batch = SandBoardManager.Instance.TryAbsorbPassengerBatchNearCar(this, transform.position, absorbCheckRadius, maxCanTake);

            if (batch != null && batch.Count > 0)
            {
                // Nhịp độ giãn cách giữa các đợt hút (chuẩn constant 35: 0.08s ~ 0.12s)
                nextAbsorbCheckTime = Time.time + Mathf.Max(0.08f, absorbInterval);

                for (int i = 0; i < batch.Count; i++)
                {
                    var item = batch[i];
                    // Độ trễ so le nối đuôi nhau giữa các hạt trong đàn (0.025s / hạt)
                    float staggerDelay = i * 0.025f;
                    StartCoroutine(FlyPassengerParabolaRoutine(item.worldPos, item.col, item.row, staggerDelay));
                }
            }
        }

        private System.Collections.IEnumerator FlyPassengerParabolaRoutine(Vector3 startPos, int col, int row, float startDelay)
        {
            if (startDelay > 0f)
            {
                yield return new WaitForSeconds(startDelay);
            }

            GameObject actor = null;
            var prefabData = LoopTrackSystem.Instance != null ? LoopTrackSystem.Instance.prefabData : null;
            if (prefabData != null && prefabData.passengerPrefab != null)
            {
                actor = Instantiate(prefabData.passengerPrefab, startPos, Quaternion.identity);
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
                if (sr != null && SandBoardManager.Instance.passengerColors != null &&
                    SandBoardManager.Instance.passengerColors.data.TryGetValue(carColor, out Sprite sprite))
                {
                    sr.sprite = sprite;
                }
                var rend = actor.GetComponentInChildren<Renderer>();
                if (rend != null)
                    rend.material.color = SandBoardManager.Instance.GetPaletteColor((int)carColor);
            }

            float elapsed = 0f;
            while (elapsed < flightDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flightDuration);
                Vector3 carPos = transform.position + Vector3.up * 0.8f;
                Vector3 pos = Vector3.Lerp(startPos, carPos, t);
                pos.y += 4f * arcPeakHeight * t * (1f - t);
                if (actor != null) actor.transform.position = pos;
                yield return null;
            }

            if (actor != null) Destroy(actor);

            AbsorbPassenger();
            SandBoardManager.Instance?.ConsumeBead(col, row);
        }

        /// <summary>
        /// Di chuyển bám theo danh sách Waypoint của LoopTrackSystem
        /// </summary>
        private void MoveAlongTrack()
        {
            var track = LoopTrackSystem.Instance;
            var waypoints = isPendingExit ? track.exitWaypoints : track.loopWaypoints;
            if (waypoints == null || waypoints.Count == 0) return;

            Transform targetWp = waypoints[Mathf.Clamp(currentWaypointIdx, 0, waypoints.Count - 1)];
            if (targetWp == null) return;

            // 1. Chống đâm xe: Hãm tốc độ nếu xe trước quá gần
            float effectiveSpeed = IsCarAheadBlocked(track.followGapDistance) ? Mathf.Max(0.5f, currentSpeed * 0.2f) : currentSpeed;

            // 2. Di chuyển tịnh tiến về mốc Waypoint
            transform.position = Vector3.MoveTowards(transform.position, targetWp.position, effectiveSpeed * Time.deltaTime);

            // 3. Xoay đầu xe theo tiếp tuyến Look-Ahead và nghiêng thân xe khi ôm cua
            Vector3 tangent = track.GetLookAheadTangent(transform.position, currentWaypointIdx, isPendingExit);
            if (tangent != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(tangent, Vector3.up);
                float angle = Vector3.SignedAngle(transform.forward, tangent, Vector3.up);
                float targetRoll = Mathf.Clamp(-angle * 0.35f, -track.maxBankingLean, track.maxBankingLean);
                bankingLean = Mathf.Lerp(bankingLean, targetRoll, Time.deltaTime * 6f);

                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot * Quaternion.Euler(0, 0, bankingLean), Time.deltaTime * track.rotateSpeed);
            }

            // 4. Khi đến gần Waypoint hiện tại, chuyển sang Waypoint tiếp theo
            Vector3 diff = targetWp.position - transform.position;
            diff.y = 0;
            if (diff.sqrMagnitude < 0.12f) // Tối ưu: dùng sqrMagnitude thay vì Distance (tránh căn bậc 2)
            {
                AdvanceToNextWaypoint(track, waypoints);
            }
        }

        /// <summary>
        /// Chuyển mốc Waypoint hoặc rẽ vào làn thoát nếu xe đã đầy khách
        /// </summary>
        private void AdvanceToNextWaypoint(LoopTrackSystem track, System.Collections.Generic.List<Transform> waypoints)
        {
            // Nếu xe đã hết chỗ và đến gần ngã rẽ Exit -> Rẽ vào làn thoát!
            if (!isPendingExit && currentSeats <= 0 && track.exitJunctionPoint != null)
            {
                if ((transform.position - track.exitJunctionPoint.position).sqrMagnitude < 2.25f) // < 1.5m
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

        /// <summary>
        /// Raycast kiểm tra khoảng cách an toàn với xe phía trước
        /// </summary>
        private bool IsCarAheadBlocked(float gapDist)
        {
            if (isMerging) return false;

            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward, out RaycastHit hit, gapDist))
            {
                Car otherCar = hit.collider.GetComponentInParent<Car>();
                // Chỉ kiểm tra các xe khác cũng đang chạy trên đường đua (isInLoop)
                if (otherCar != null && otherCar != this && !otherCar.isMerging && otherCar.isInLoop)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Xe đã hoàn thành chặng đường và chạy ra khỏi màn hình
        /// </summary>
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
