using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WhoGetInBus.GamePlay
{
    /// <summary>
    /// ĐIỀU KHIỂN XE CHẠY ĐƯỜNG RAY (Kế thừa từ Car Base)
    /// Chuẩn game gốc Cocos Creator 1:1 (CartTrackController & BPathCursor):
    /// - Di chuyển bám chính xác 100% trên tim đường ray bằng Arc-Length Track Spline (không cắt góc, không vẹo đầu)
    /// - Đầu xe quay mượt mà theo tiếp tuyến nhìn trước (Exponential Damping Yaw)
    /// - Thân xe nghiêng ôm cua bằng hệ lò xo giảm xóc vật lý (Spring-Damper Banking Lean)
    /// - Tự động giữ khoảng cách chống va chạm với xe phía trước theo chu vi đường ray (Follow Gap Distance)
    /// - Cơ chế hút đàn hành khách (Batch Absorb) bay vòng cung Parabol vào xe
    /// - Rẽ vào làn đường thoát (Exit Track) khi xe đã đón đủ 100% khách
    /// </summary>
    public class CarLoopFollower : Car
    {
        [Header("Trạng Thái Di Chuyển Đường Ray")]
        public float currentTrackDistance = 0f;
        public int currentWaypointIdx = 0;
        public float currentSpeed = 5.0f;
        public float bankingLean = 0f;
        public bool isExiting = false;

        [Header("Thông Số Giảm Xóc Ôm Cua (Spring-Damper Lean)")]
        private float bodyLeanDeg = 0f;
        private float bodyLeanVelocity = 0f;
        private Quaternion facingCurrentRot = Quaternion.identity;

        [Header("Cơ Chế Đón Khách Dọc Đường (Cocos Proximity Batch Boarding)")]
        [Tooltip("Bán kính quét hút khách viền Gate khi xe chạy ngang qua (chuẩn 3.5m)")]
        public float absorbCheckRadius = 3.5f;
        [Tooltip("Nhịp độ quét đón khách liên tục (chuẩn Cocos 0.04s)")]
        public float absorbInterval = 0.04f;
        public float flightDuration = 0.32f;
        public float arcPeakHeight = 1.4f;

        private float nextAbsorbCheckTime = 0f;
        private int inFlightPassengersCount = 0;

        /// <summary>
        /// Đưa xe nhập làn vào đường đua tại khoảng cách distance trên spline
        /// </summary>
        public void EnterTrack(float startDistance)
        {
            isInLoop = true;
            isPendingExit = false;
            isExiting = false;
            isMerging = false;
            currentTrackDistance = startDistance;
            currentSpeed = LoopTrackSystem.Instance != null ? LoopTrackSystem.Instance.baseSpeed : 5.0f;
            inFlightPassengersCount = 0;

            var track = LoopTrackSystem.Instance;
            if (track != null)
            {
                transform.position = track.EvaluatePosition(currentTrackDistance, false);
                Vector3 initialTangent = track.SampleLookAheadTangent(currentTrackDistance, track.lookAheadDistance, false);
                if (initialTangent.sqrMagnitude > 0.001f)
                {
                    facingCurrentRot = Quaternion.LookRotation(initialTangent, Vector3.up);
                    transform.rotation = facingCurrentRot;
                }
                bodyLeanDeg = 0f;
                bodyLeanVelocity = 0f;
                bankingLean = 0f;
            }
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

        private void Update()
        {
            UpdateBounce();

            if (!isInLoop || isMerging || LoopTrackSystem.Instance == null) return;

            MoveAlongTrack();

            CheckAndAbsorbPassengersAlongTrack();
        }

        #region DI CHUYỂN BÁM TIM ĐƯỜNG RAY (ARC-LENGTH TRACK FOLLOWING)

        private void MoveAlongTrack()
        {
            var track = LoopTrackSystem.Instance;
            if (track == null) return;

            // 1. Tính toán vận tốc hiệu dụng (tự động hãm phanh nếu xe phía trước quá gần)
            float effectiveSpeed = ComputeEffectiveSpeed(track);
            float moveDist = effectiveSpeed * Time.deltaTime;

            // 2. Nếu xe đã đầy khách: kiểm tra ngã rẽ lối thoát (Exit Junction)
            if (isPendingExit && !isExiting)
            {
                float distToJunction = track.NormalizeLoopDistance(currentTrackDistance - track.exitJunctionDistance);
                distToJunction = Mathf.Min(distToJunction, track.loopTotalLen - distToJunction);

                bool crossingThisFrame = distToJunction < 0.35f;

                if (crossingThisFrame && track.IsExitLaneClear(track.followGapDistance))
                {
                    isExiting = true;
                    currentTrackDistance = 0f;
                    bodyLeanDeg = 0f;
                    bodyLeanVelocity = 0f;
                    bankingLean = 0f;
                    Vector3 exitTan = track.SampleLookAheadTangent(0f, track.lookAheadDistance, true);
                    facingCurrentRot = Quaternion.LookRotation(exitTan, Vector3.up);
                    transform.rotation = facingCurrentRot;
                    return;
                }
            }

            // 3. Tiến lên theo cung đường
            if (isExiting)
            {
                currentTrackDistance += moveDist;
                if (currentTrackDistance >= track.exitTotalLen)
                {
                    OnExitFinished();
                    return;
                }

                Vector3 pos = track.EvaluatePosition(currentTrackDistance, true);
                Vector3 heading = track.SampleLookAheadTangent(currentTrackDistance, track.lookAheadDistance, true);
                ApplyTransform(track, pos, heading);
            }
            else
            {
                currentTrackDistance = track.NormalizeLoopDistance(currentTrackDistance + moveDist);
                Vector3 pos = track.EvaluatePosition(currentTrackDistance, false);
                Vector3 heading = track.SampleLookAheadTangent(currentTrackDistance, track.lookAheadDistance, false);
                ApplyTransform(track, pos, heading);

                // Giữ currentWaypointIdx đồng bộ cho Inspector và tiện ích
                currentWaypointIdx = track.FindClosestWaypointIndex(pos);
            }
        }

        /// <summary>
        /// Áp dụng vị trí chuẩn 100% tim đường và xoay đầu xe mượt mà + nghiêng thân xe ôm cua
        /// </summary>
        private void ApplyTransform(LoopTrackSystem track, Vector3 pos, Vector3 heading)
        {
            transform.position = pos;

            if (heading.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(heading, Vector3.up);
                float factor = 1f - Mathf.Exp(-track.rotateSpeed * Time.deltaTime);
                facingCurrentRot = Quaternion.Slerp(facingCurrentRot, targetRot, factor);

                // Vật lý nghiêng thân xe (Spring-Damper Banking Lean chuẩn Cocos 1:1)
                float targetLean = track.GetTurnLeanTargetDeg(facingCurrentRot * Vector3.forward, heading);
                float dtClamped = Mathf.Min(Time.deltaTime, 0.05f);
                float accel = 85f * (targetLean - bodyLeanDeg) - 10f * bodyLeanVelocity;
                bodyLeanVelocity += accel * dtClamped;
                bodyLeanDeg += bodyLeanVelocity * dtClamped;
                bodyLeanDeg = Mathf.Clamp(bodyLeanDeg, -track.maxBankingLean, track.maxBankingLean);
                bankingLean = bodyLeanDeg;

                transform.rotation = facingCurrentRot * Quaternion.Euler(0f, 0f, bodyLeanDeg);
            }
        }

        /// <summary>
        /// Tính toán tốc độ an toàn giữ khoảng cách chống đâm xe phía trước
        /// </summary>
        private float ComputeEffectiveSpeed(LoopTrackSystem track)
        {
            if (isMerging) return currentSpeed;

            float gapDist = track.followGapDistance;
            if (gapDist <= 0f) return currentSpeed;

            float speed = currentSpeed;

            // Tăng tốc khi chạy ở cung đường vòng ngoài (xa vùng tranh cát) để giảm thời gian chờ
            if (!isExiting && !track.IsBoardingZone(currentTrackDistance))
            {
                speed *= 1.35f;
            }

            if (!isExiting)
            {
                foreach (var other in track.activeCarsOnTrack)
                {
                    if (other == null || other == this || !other.isInLoop || other.isMerging || other.isExiting) continue;

                    float forwardDist = track.GetForwardLoopDistance(this.currentTrackDistance, other.currentTrackDistance);
                    if (forwardDist < gapDist)
                    {
                        if (forwardDist < 0.6f) return 0f; // Dừng hẳn nếu quá gần
                        float ratio = Mathf.Clamp01((forwardDist - 0.6f) / (gapDist - 0.6f));
                        speed = Mathf.Min(speed, currentSpeed * ratio);
                    }
                }
            }
            else
            {
                foreach (var other in track.activeCarsOnTrack)
                {
                    if (other == null || other == this || !other.isInLoop || other.isMerging || !other.isExiting) continue;

                    if (other.currentTrackDistance > this.currentTrackDistance)
                    {
                        float dist = other.currentTrackDistance - this.currentTrackDistance;
                        if (dist < gapDist)
                        {
                            if (dist < 0.6f) return 0f;
                            float ratio = Mathf.Clamp01((dist - 0.6f) / (gapDist - 0.6f));
                            speed = Mathf.Min(speed, currentSpeed * ratio);
                        }
                    }
                }
            }

            return speed;
        }

        #endregion

        #region ĐÓN KHÁCH THEO ĐÀN (BATCH ABSORB & PARABOLA FLIGHT)

        private void CheckAndAbsorbPassengersAlongTrack()
        {
            if (Time.time < nextAbsorbCheckTime) return;
            if (currentSeats <= 0 || isPendingExit || isExiting || isMerging) return;
            if (LoopTrackSystem.Instance == null || SandBoardManager.Instance == null) return;

            // Chỉ quét khi xe chạy trong vùng đón khách tiếp giáp chân tranh cát
            if (!LoopTrackSystem.Instance.IsBoardingZone(currentTrackDistance)) return;

            int seatsAvailable = currentSeats - inFlightPassengersCount;
            if (seatsAvailable <= 0) return;

            // Giới hạn số lượng hạt đang bay tối đa (24 hạt) để đảm bảo FPS ổn định và luồng cát mượt mà
            int maxFlightCapacity = 24 - inFlightPassengersCount;
            if (maxFlightCapacity <= 0) return;

            // Hút theo đàn (Batch Absorb): trích xuất 8 - 12 hạt một lần quét (chuẩn minibus_tran / bus_tran trong Cocos)
            int batchTargetCap = isExtraCar ? 12 : 8;
            int maxCanTake = Mathf.Clamp(seatsAvailable, 1, Mathf.Min(batchTargetCap, maxFlightCapacity));
            var batch = SandBoardManager.Instance.TryExtractPassengerBatchNearCar(this, transform.position, absorbCheckRadius, maxCanTake);

            if (batch != null && batch.Count > 0)
            {
                nextAbsorbCheckTime = Time.time + absorbInterval;
                inFlightPassengersCount += batch.Count;

                for (int i = 0; i < batch.Count; i++)
                {
                    var item = batch[i];
                    // Độ trễ so le rất nhanh (25ms) tạo thành chuỗi hạt bay liên tiếp như dòng nước/thác cát
                    float staggerDelay = i * 0.025f;

                    // Tính độ lệch cánh cung theo hướng ngang (Perpendicular Spread) tạo hiệu ứng luồng phun chuẩn Cocos h=[0, -0.55, 0.55, -1, 1, -0.3, 0.3]
                    Vector3 travelDir = (transform.position - item.worldPos);
                    travelDir.y = 0f;
                    Vector3 perp = Vector3.Cross(travelDir.normalized, Vector3.up);
                    float[] laneOffsets = new float[] { 0f, -0.55f, 0.55f, -1.0f, 1.0f, -0.3f, 0.3f };
                    float spreadFactor = laneOffsets[i % laneOffsets.Length] * 0.15f;
                    Vector3 laneOffset = perp * spreadFactor;

                    StartCoroutine(FlyPassengerParabolaRoutine(item.worldPos, staggerDelay, laneOffset));
                }
            }
        }

        private IEnumerator FlyPassengerParabolaRoutine(Vector3 startPos, float startDelay, Vector3 laneOffset)
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
                var col = actor.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }

            if (actor != null && SandBoardManager.Instance != null)
            {
                var sr = actor.GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                {
                    // Giữ nguyên material gốc của SpriteRenderer từ prefab, chỉ cập nhật sprite theo màu xe nếu có
                    if (SandBoardManager.Instance.passengerColors != null &&
                        SandBoardManager.Instance.passengerColors.data.TryGetValue(carColor, out Sprite sprite))
                    {
                        sr.sprite = sprite;
                    }
                }
                else
                {
                    // Fallback chỉ dành cho Primitive Sphere (nếu không có SpriteRenderer)
                    var meshRend = actor.GetComponentInChildren<MeshRenderer>();
                    if (meshRend != null)
                    {
                        meshRend.material.color = SandBoardManager.Instance.GetPaletteColor((int)carColor);
                    }
                }
            }

            float elapsed = 0f;
            while (elapsed < flightDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flightDuration);

                // Điểm đích linh hoạt bám sát nóc xe buýt khi xe đang di chuyển
                Vector3 targetRoofPos = transform.position + Vector3.up * 0.85f;
                Vector3 pos = Vector3.Lerp(startPos, targetRoofPos, t);
                pos.y += 4f * arcPeakHeight * t * (1f - t);
                pos += laneOffset * Mathf.Sin(t * Mathf.PI);

                if (actor != null) actor.transform.position = pos;
                yield return null;
            }

            if (actor != null) Destroy(actor);

            // Khi hành khách tiếp đất vào nóc xe
            inFlightPassengersCount = Mathf.Max(0, inFlightPassengersCount - 1);
            AbsorbPassenger();

            // Nếu xe đã hết sạch chỗ -> Đánh dấu chuẩn bị rẽ sang làn thoát
            if (currentSeats <= 0 && !isPendingExit)
            {
                isPendingExit = true;
            }
        }

        #endregion

        /// <summary>
        /// Xe đã hoàn thành chặng đường và chạy ra khỏi màn hình
        /// </summary>
        private void OnExitFinished()
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
