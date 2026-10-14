using System.Collections.Generic;
using UnityEngine;

namespace WhoGetInBus.GamePlay
{
    /// <summary>
    /// HỆ THỐNG ĐƯỜNG RAY CHỮ U KHÉP KÍN (Loop Track Spline System)
    /// Chuẩn game gốc Cocos Creator 1:1 (CartTrackController & BPathCursor):
    /// - Quản lý spline vòng lặp kín bằng tham số hoá độ dài cung (Arc-Length Parameterized Spline)
    /// - Giúp xe buýt luôn chạy chính xác 100% trên tim đường, không cắt góc, không vẹo đầu
    /// - Tính toán tiếp tuyến hướng nhìn trước (Sample Look-Ahead Tangent) và nghiêng thân xe (Banking Lean)
    /// - Quản lý ngã rẽ lối thoát (Exit Junction) và điểm xuất bến nhập làn từ bãi đỗ (Parking Entry)
    /// </summary>
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
        public int parkingEntryWaypointIdx = 0;

        [Header("Thông Số Chuyển Động Chuẩn (Cocos 1:1)")]
        public float baseSpeed = 5.0f;
        public float rotateSpeed = 8.0f;
        public float lookAheadDistance = 0.45f;
        public float followGapDistance = 1.65f;
        public float maxBankingLean = 6.0f;

        [Header("Luật Hợp Thể 3 Xe Cùng Màu (Extra Car Merge)")]
        public GameObject extraCarPrefab;
        public BusColorData busColors;
        public GamePrefabData prefabData;

        [Header("Chuẩn Toạ Độ Cocos -> Unity 1:1")]
        public bool autoCoordinateConvert = true;

        [Header("Dữ Liệu Spline Tuyến Đường (Arc-Length Track Cache)")]
        public List<Vector3> loopPathPoints = new List<Vector3>();
        public float[] loopSegLens;
        public float[] loopPrefixLens;
        public float loopTotalLen;

        public List<Vector3> exitPathPoints = new List<Vector3>();
        public float[] exitSegLens;
        public float[] exitPrefixLens;
        public float exitTotalLen;

        public float exitJunctionDistance = 0f;
        public float parkingEntryDistance = 0f;

        // Danh sách xe đang hoạt động trên đường
        public List<CarLoopFollower> activeCarsOnTrack = new List<CarLoopFollower>();

        private void Awake()
        {
            Instance = this;
            if (loopPathPoints.Count > 1 && (loopSegLens == null || loopSegLens.Length == 0))
            {
                RebuildPathCache();
            }
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

        #region PATH SPLINE EVALUATION (Chuẩn BPathCursor.ts Cocos Creator)

        /// <summary>
        /// Khởi tạo và tính toán độ dài từng phân đoạn, mảng tích lũy độ dài (Prefix Sums)
        /// </summary>
        public void RebuildPathCache()
        {
            if (loopPathPoints == null || loopPathPoints.Count < 2) return;

            // 1. Khép kín vòng loop: đảm bảo điểm cuối nối về điểm đầu
            Vector3 first = loopPathPoints[0];
            Vector3 last = loopPathPoints[loopPathPoints.Count - 1];
            if ((last - first).sqrMagnitude > 0.0001f)
            {
                loopPathPoints.Add(first);
            }

            // 2. Tính toán độ dài phân đoạn và prefix sums cho đường Loop
            int loopSegCount = loopPathPoints.Count - 1;
            loopSegLens = new float[loopSegCount];
            loopPrefixLens = new float[loopSegCount];
            loopTotalLen = 0f;

            for (int i = 0; i < loopSegCount; i++)
            {
                loopPrefixLens[i] = loopTotalLen;
                float len = Vector3.Distance(loopPathPoints[i], loopPathPoints[i + 1]);
                loopSegLens[i] = len;
                loopTotalLen += len;
            }

            // 3. Tính toán độ dài phân đoạn cho đường Exit (hở, không khép kín)
            if (exitPathPoints != null && exitPathPoints.Count >= 2)
            {
                int exitSegCount = exitPathPoints.Count - 1;
                exitSegLens = new float[exitSegCount];
                exitPrefixLens = new float[exitSegCount];
                exitTotalLen = 0f;

                for (int i = 0; i < exitSegCount; i++)
                {
                    exitPrefixLens[i] = exitTotalLen;
                    float len = Vector3.Distance(exitPathPoints[i], exitPathPoints[i + 1]);
                    exitSegLens[i] = len;
                    exitTotalLen += len;
                }

                // Điểm rẽ ra Exit là vị trí trên Loop gần exitPathPoints[0] nhất
                exitJunctionDistance = FindClosestLoopDistance(exitPathPoints[0]);
            }

            // 4. Điểm nhập làn từ bãi đỗ
            if (parkingEntryWaypoint != null)
            {
                parkingEntryDistance = FindClosestLoopDistance(parkingEntryWaypoint.position);
            }
            else
            {
                parkingEntryDistance = 0f;
            }
        }

        /// <summary>
        /// Đánh giá toạ độ 3D chính xác trên tim đường tại khoảng cách distance (Arc-Length)
        /// </summary>
        public Vector3 EvaluatePosition(float distance, bool isExit)
        {
            var points = isExit ? exitPathPoints : loopPathPoints;
            var segLens = isExit ? exitSegLens : loopSegLens;
            var prefixLens = isExit ? exitPrefixLens : loopPrefixLens;
            float totalLen = isExit ? exitTotalLen : loopTotalLen;

            if (points == null || points.Count == 0) return Vector3.zero;
            if (points.Count == 1) return points[0];
            if (segLens == null || segLens.Length == 0) return points[0];

            if (!isExit)
            {
                if (totalLen <= 0.0001f) return points[0];
                distance = distance % totalLen;
                if (distance < 0f) distance += totalLen;
            }
            else
            {
                if (distance <= 0f) return points[0];
                if (distance >= totalLen) return points[points.Count - 1];
            }

            for (int i = 0; i < segLens.Length; i++)
            {
                float segStart = prefixLens[i];
                float segLen = segLens[i];
                if (distance <= segStart + segLen || i == segLens.Length - 1)
                {
                    float segProgress = segLen > 0.0001f ? Mathf.Clamp01((distance - segStart) / segLen) : 0f;
                    return Vector3.Lerp(points[i], points[i + 1], segProgress);
                }
            }

            return points[points.Count - 1];
        }

        /// <summary>
        /// Lấy vector tiếp tuyến nhìn trước (Look-Ahead Tangent) mượt mà chuẩn Cocos 1:1
        /// </summary>
        public Vector3 SampleLookAheadTangent(float distance, float lookAheadDist, bool isExit)
        {
            Vector3 pA = EvaluatePosition(distance, isExit);
            Vector3 pB = EvaluatePosition(distance + Mathf.Max(0.05f, lookAheadDist), isExit);
            Vector3 dir = pB - pA;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                return dir.normalized;
            }

            // Fallback: tiếp tuyến theo phân đoạn hiện tại
            var points = isExit ? exitPathPoints : loopPathPoints;
            if (points != null && points.Count >= 2)
            {
                Vector3 fallbackDir = points[1] - points[0];
                fallbackDir.y = 0f;
                return fallbackDir.sqrMagnitude > 0.0001f ? fallbackDir.normalized : Vector3.forward;
            }
            return Vector3.forward;
        }

        /// <summary>
        /// Tính toán góc nghiêng thân xe (Banking Lean Deg) bằng công thức chuẩn Cocos Creator 1:1:
        /// targetLean = -sign(cross) * 6 * min(1, angle/20) * 1.5
        /// </summary>
        public float GetTurnLeanTargetDeg(Vector3 currentForward, Vector3 lookAheadTangent)
        {
            float cross = currentForward.x * lookAheadTangent.z - currentForward.z * lookAheadTangent.x;
            float dot = Mathf.Clamp(Vector3.Dot(currentForward, lookAheadTangent), -1f, 1f);
            float angleDeg = Mathf.Abs(Mathf.Atan2(cross, dot) * Mathf.Rad2Deg);
            if (angleDeg < 0.75f) return 0f;

            float lean = 6.0f * Mathf.Min(1f, angleDeg / 20f) * Mathf.Min(1f, 1.5f);
            return Mathf.Clamp(-Mathf.Sign(cross) * lean, -maxBankingLean, maxBankingLean);
        }

        /// <summary>
        /// Tìm khoảng cách loopDistance gần nhất tương ứng với toạ độ worldPos
        /// </summary>
        public float FindClosestLoopDistance(Vector3 worldPos)
        {
            if (loopPathPoints == null || loopPathPoints.Count < 2 || loopSegLens == null || loopSegLens.Length == 0) return 0f;

            float bestDistSqr = float.MaxValue;
            int bestSeg = 0;
            float bestT = 0f;

            for (int i = 0; i < loopSegLens.Length; i++)
            {
                Vector3 a = loopPathPoints[i];
                Vector3 b = loopPathPoints[i + 1];
                Vector3 ab = b - a;
                float sqrLen = ab.sqrMagnitude;
                float t = (sqrLen > 0.0001f) ? Mathf.Clamp01(Vector3.Dot(worldPos - a, ab) / sqrLen) : 0f;
                Vector3 proj = Vector3.Lerp(a, b, t);
                float d2 = (worldPos - proj).sqrMagnitude;
                if (d2 < bestDistSqr)
                {
                    bestDistSqr = d2;
                    bestSeg = i;
                    bestT = t;
                }
            }

            return loopPrefixLens[bestSeg] + loopSegLens[bestSeg] * bestT;
        }

        /// <summary>
        /// Tính khoảng cách đi tới (Forward Loop Distance) giữa 2 xe trên vòng lặp
        /// </summary>
        public float GetForwardLoopDistance(float fromDist, float toDist)
        {
            if (loopTotalLen <= 0f) return float.MaxValue;
            float diff = toDist - fromDist;
            while (diff <= 0f) diff += loopTotalLen;
            return diff;
        }

        /// <summary>
        /// Chuẩn hoá khoảng cách trên vòng lặp về phạm vi [0, loopTotalLen)
        /// </summary>
        public float NormalizeLoopDistance(float dist)
        {
            if (loopTotalLen <= 0f) return 0f;
            float r = dist % loopTotalLen;
            if (r < 0f) r += loopTotalLen;
            return r;
        }

        /// <summary>
        /// Kiểm tra xem vị trí hiện tại có nằm trong vùng đón khách (Boarding Zone) không
        /// Chuẩn Cocos 1:1: Toàn bộ vòng lặp (trừ khi xe rẽ vào đường thoát exit hoặc còn ở trạm xuất bến đáy Z < -2.0m)
        /// đều có thể đón khách nếu tiếp cận các ô Boarding Gate.
        /// </summary>
        public bool IsBoardingZone(float dist)
        {
            Vector3 pos = EvaluatePosition(dist, false);
            return pos.z >= -2.0f;
        }

        [System.Serializable]
        public struct CartDeployPlan
        {
            public Vector3 mergeWorldPos;
            public float mergeDistance;
            public Vector3 entryTangent;
            public Quaternion targetRotation;
        }

        /// <summary>
        /// Tạo kế hoạch nhập làn cho xe xuất phát từ bãi đỗ (chuẩn 100% CreateCartDeployPlan trong Cocos)
        /// Xe tiến thẳng từ ô đỗ ra đoạn tim đường trước mặt bãi đỗ và hòa vào làn đường theo hướng tiếp tuyến!
        /// </summary>
        public CartDeployPlan CreateDeployPlan(Vector3 carWorldPos)
        {
            CartDeployPlan plan = new CartDeployPlan();
            if (loopPathPoints == null || loopPathPoints.Count < 2)
            {
                plan.mergeWorldPos = carWorldPos + Vector3.forward * 2f;
                plan.mergeDistance = 0f;
                plan.entryTangent = Vector3.right;
                plan.targetRotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
                return plan;
            }

            // Đoạn đường đáy chạy ngang trước mặt bãi đỗ xe có Z của loopPathPoints[0] (khoảng -4.17m)
            float roadBottomZ = loopPathPoints[0].z;
            Vector3 targetStaging = new Vector3(carWorldPos.x, 0f, roadBottomZ);

            // Tìm vị trí chiếu gần nhất trên tim đường ray tương ứng với cột đỗ của xe
            plan.mergeDistance = FindClosestLoopDistance(targetStaging);
            plan.mergeWorldPos = EvaluatePosition(plan.mergeDistance, false);
            plan.entryTangent = SampleLookAheadTangent(plan.mergeDistance, lookAheadDistance, false);
            if (plan.entryTangent.sqrMagnitude < 0.001f) plan.entryTangent = Vector3.right;
            plan.targetRotation = Quaternion.LookRotation(plan.entryTangent.normalized, Vector3.up);

            return plan;
        }

        /// <summary>
        /// Kiểm tra xem làn đường thoát (Exit Lane) có đủ thông thoáng để xe rẽ vào không
        /// </summary>
        public bool IsExitLaneClear(float minGap)
        {
            if (minGap <= 0f) return true;
            foreach (var car in activeCarsOnTrack)
            {
                if (car != null && car.isInLoop && car.isExiting)
                {
                    if (car.currentTrackDistance < minGap) return false;
                }
            }
            return true;
        }

        public int FindClosestWaypointIndex(Vector3 pos)
        {
            if (loopWaypoints == null || loopWaypoints.Count == 0) return 0;
            int closest = 0;
            float minDist = float.MaxValue;
            for (int i = 0; i < loopWaypoints.Count; i++)
            {
                if (loopWaypoints[i] == null) continue;
                float d = (loopWaypoints[i].position - pos).sqrMagnitude;
                if (d < minDist)
                {
                    minDist = d;
                    closest = i;
                }
            }
            return closest;
        }

        #endregion

        #region HỢP THỂ 3 XE CÙNG MÀU (Extra Car Merge)

        public void CheckAndMergeSameColorCars(GameColorType color)
        {
            List<CarLoopFollower> sameColorCars = new List<CarLoopFollower>();
            foreach (var car in activeCarsOnTrack)
            {
                if (car != null && car.isInLoop && !car.isPendingExit && !car.isExiting && !car.isMerging && !car.isExtraCar && car.carColor == color)
                {
                    sameColorCars.Add(car);
                }
            }

            // ĐỦ 3 XE CÙNG MÀU TRÊN ĐƯỜNG -> GỘP THÀNH EXTRA CAR 2 TẦNG
            if (sameColorCars.Count >= 3)
            {
                // Sắp xếp xe chạy trước nhất (theo track distance) là leadCar
                sameColorCars.Sort((a, b) => b.currentTrackDistance.CompareTo(a.currentTrackDistance));
                StartCoroutine(MergeThreeCarsRoutine(sameColorCars[0], sameColorCars[1], sameColorCars[2], color));
            }
        }

        private System.Collections.IEnumerator MergeThreeCarsRoutine(CarLoopFollower leadCar, CarLoopFollower tailCar1, CarLoopFollower tailCar2, GameColorType color)
        {
            if (leadCar == null || tailCar1 == null || tailCar2 == null) yield break;

            leadCar.isMerging = true;
            tailCar1.isMerging = true;
            tailCar2.isMerging = true;

            Debug.Log($"<color=cyan>⚡ [GỘP XE]</color> Phát hiện 3 xe thường màu {color} trên đường! Bắt đầu gộp thành EXTRA CAR 2 tầng!");

            Vector3 startP1 = tailCar1.transform.position;
            Vector3 startP2 = tailCar2.transform.position;
            Vector3 scale1 = tailCar1.transform.localScale;
            Vector3 scale2 = tailCar2.transform.localScale;

            float elapsed = 0f;
            float duration = 0.45f;

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

            int combinedSeats = leadCar.currentSeats;
            int combinedTotal = leadCar.totalSeats;
            if (tailCar1 != null) { combinedSeats += tailCar1.currentSeats; combinedTotal += tailCar1.totalSeats; }
            if (tailCar2 != null) { combinedSeats += tailCar2.currentSeats; combinedTotal += tailCar2.totalSeats; }

            float trackDist = leadCar.currentTrackDistance;
            Vector3 spawnPos = leadCar.transform.position;
            Quaternion spawnRot = leadCar.transform.rotation;
            float speed = leadCar.currentSpeed;
            CarMechanicType mech = leadCar.mechanicType;

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

            UnregisterCarFromTrack(leadCar);
            Destroy(leadCar.gameObject);

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
                follower.EnterTrack(trackDist);
                follower.currentSpeed = speed;

                RegisterCarOnTrack(follower);
                follower.PlayMergeSuccessEffect();
            }
        }

        #endregion

        #region LOAD ROAD CONFIG (roadNCXHCfg.json)

        public void LoadRoadFromConfig(int roadId, GamePrefabData pData = null)
        {
            if (pData != null) prefabData = pData;
            if (prefabData != null)
            {
                if (extraCarPrefab == null) extraCarPrefab = prefabData.extraCarPrefab;
                if (busColors == null) busColors = prefabData.busColors;
            }

            var roadData = WhoGetInBus.Data.LevelConfigLoader.GetRoadConfig(roadId);
            if (roadData == null)
            {
                Debug.LogWarning($"[LoopTrackSystem] Không tìm thấy Road #{roadId}!");
                return;
            }

            if (roadData.speed > 0f) baseSpeed = roadData.speed;

            // 1. Dọn dẹp waypoints cũ
            var toDestroy = new List<GameObject>();
            for (int i = 0; i < transform.childCount; i++)
            {
                toDestroy.Add(transform.GetChild(i).gameObject);
            }
            foreach (var g in toDestroy)
            {
                if (Application.isPlaying) Destroy(g);
                else DestroyImmediate(g);
            }

            loopWaypoints.Clear();
            exitWaypoints.Clear();
            loopPathPoints.Clear();
            exitPathPoints.Clear();

            // 2. Nạp Loop Waypoints từ roadData.loop_path_root
            loopPathPoints = ParseCoordinatesRaw(roadData.loop_path_root);
            for (int i = 0; i < loopPathPoints.Count; i++)
            {
                GameObject wp = new GameObject($"Loop_WP_{i}");
                wp.transform.SetParent(transform, false);
                wp.transform.position = loopPathPoints[i];
                loopWaypoints.Add(wp.transform);
            }

            // 3. Nạp Exit Waypoints từ roadData.exit_path_root
            exitPathPoints = ParseCoordinatesRaw(roadData.exit_path_root);
            for (int i = 0; i < exitPathPoints.Count; i++)
            {
                GameObject wp = new GameObject($"Exit_WP_{i}");
                wp.transform.SetParent(transform, false);
                wp.transform.position = exitPathPoints[i];
                exitWaypoints.Add(wp.transform);
            }

            // 4. Khởi tạo Arc-Length Path Spline Cache
            RebuildPathCache();

            // 5. Ngã rẽ Exit Junction
            if (exitWaypoints.Count > 0 && loopWaypoints.Count > 0)
            {
                exitJunctionPoint = FindClosestWaypoint(loopWaypoints, exitWaypoints[0].position);
            }

            // 6. Điểm nhập làn từ bãi xe: mặc định đoạn đáy trước mặt bãi đỗ
            if (loopWaypoints.Count > 0)
            {
                parkingEntryWaypointIdx = 0;
                parkingEntryWaypoint = loopWaypoints[0];
                parkingEntryDistance = FindClosestLoopDistance(parkingEntryWaypoint.position);
            }

            // 7. Điểm đón khách sâu nhất (Boarding Gate Point)
            float maxZ = float.MinValue;
            for (int i = 0; i < loopWaypoints.Count; i++)
            {
                if (loopWaypoints[i].position.z > maxZ)
                {
                    maxZ = loopWaypoints[i].position.z;
                    boardingGatePoint = loopWaypoints[i];
                }
            }

            Debug.Log($"<color=green>[LoopTrackSystem]</color> Đã nạp Road #{roadId}: {loopPathPoints.Count} loop points, {exitPathPoints.Count} exit points. Tổng chiều dài loop: {loopTotalLen:F2}m. Exit tại: {exitJunctionDistance:F2}m, Entry tại: {parkingEntryDistance:F2}m (Chuẩn Cocos 1:1!)");
        }

#if UNITY_EDITOR
        [ContextMenu("Nạp Lại Road 5 (Chuẩn Cocos 1:1)")]
        public void ReloadRoad5InEditor()
        {
            LoadRoadFromConfig(5);
        }
#endif

        private List<Vector3> ParseCoordinatesRaw(string raw)
        {
            var list = new List<Vector3>();
            if (string.IsNullOrEmpty(raw)) return list;

            string[] points = raw.Split(';');
            foreach (var p in points)
            {
                string clean = p.Trim().Trim('[', ']');
                if (string.IsNullOrEmpty(clean)) continue;

                string[] parts = clean.Split(',');
                if (parts.Length >= 2)
                {
                    if (float.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x) &&
                        float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z))
                    {
                        // Chuẩn chuyển đổi hệ toạ độ từ Cocos sang Unity: X giữ nguyên, Z đổi dấu -z
                        Vector3 pt = new Vector3(x, 0f, -z);
                        list.Add(pt);
                    }
                }
            }
            return list;
        }

        private Transform FindClosestWaypoint(List<Transform> list, Vector3 target)
        {
            Transform closest = null;
            float minDist = float.MaxValue;
            foreach (var t in list)
            {
                if (t == null) continue;
                float d = Vector3.Distance(t.position, target);
                if (d < minDist)
                {
                    minDist = d;
                    closest = t;
                }
            }
            return closest;
        }

        // Phương thức tương thích ngược
        public Vector3 GetLookAheadTangent(Vector3 currentPos, int currentWpIdx, bool isExit)
        {
            float dist = FindClosestLoopDistance(currentPos);
            return SampleLookAheadTangent(dist, lookAheadDistance, isExit);
        }

        public int GetNextWaypointIndex(int currentIdx, bool isExit)
        {
            var list = isExit ? exitWaypoints : loopWaypoints;
            if (list == null || list.Count == 0) return 0;
            if (isExit) return Mathf.Min(currentIdx + 1, list.Count - 1);
            return (currentIdx + 1) % list.Count;
        }

        #endregion

        #region GIZMOS

        private void OnDrawGizmos()
        {
            // Vẽ đường Loop chữ U: Xanh lam cho đường chạy thường, Vàng cam rực rỡ cho vùng đón khách (Boarding Zone Z >= 1.5m)
            if (loopPathPoints != null && loopPathPoints.Count > 1)
            {
                for (int i = 0; i < loopPathPoints.Count - 1; i++)
                {
                    bool isBoarding = loopPathPoints[i].z >= -2.0f || loopPathPoints[i + 1].z >= -2.0f;
                    Gizmos.color = isBoarding 
                        ? new Color(1f, 0.75f, 0.15f, 0.95f) 
                        : new Color(0.1f, 0.85f, 1f, 0.9f);

                    Gizmos.DrawLine(loopPathPoints[i], loopPathPoints[i + 1]);
                    Gizmos.DrawSphere(loopPathPoints[i], isBoarding ? 0.22f : 0.14f);
                }
            }

            // Vẽ đường thoát Exit màu xanh lá (Green)
            if (exitPathPoints != null && exitPathPoints.Count > 1)
            {
                Gizmos.color = Color.green;
                for (int i = 0; i < exitPathPoints.Count - 1; i++)
                {
                    Gizmos.DrawLine(exitPathPoints[i], exitPathPoints[i + 1]);
                    Gizmos.DrawCube(exitPathPoints[i], Vector3.one * 0.2f);
                }
            }
        }

        #endregion
    }
}
