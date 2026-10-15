using System.Collections.Generic;
using UnityEngine;

namespace WhoGetInBus.GamePlay
{
    /// <summary>
    /// HỆ THỐNG KHÁCH BAY LÊN XE — port từ Cocos "PassengerBoardingPresenter" + "PassengerBoardActor.tickParabolicFlight".
    ///
    ///  - Mỗi xe có hàng đợi phóng riêng, nhả 1 khách mỗi 0.02s (launch cooldown) → tạo thành dòng khách liên tục.
    ///  - Giới hạn số khách đang bay hiển thị: tối đa 32 / xe và 200 tổng. Phần dư không thể hiển thị sẽ tới nơi NGAY
    ///    (hoặc gom thành 1 diễn viên đại diện nhiều khách) — số ghế luôn khớp số hạt đã bị hút.
    ///  - Quỹ đạo: easing s = e·(2-e)·e, parabol cao độ min(0.55·100px, 0.16·dist), lệch ngang theo làn (lane) kiểu sin(πe).
    ///  - Thời gian bay = clamp(dist / 360 · 0.5, 0.26, 0.62)s, đích bám theo điểm neo của xe (xe vẫn đang chạy).
    /// Hệ toạ độ Cocos tính bằng pixel UI; Unity dùng world unit nên quy đổi qua pixelsPerWorldUnit.
    /// </summary>
    public class PassengerFlightSystem : MonoBehaviour
    {
        [Header("Tham Chiếu Scene (kéo thả)")]
        [SerializeField] private SandBoardManager sandBoard;

        [Header("Prefab khách bay (SpriteRenderer)")]
        public GameObject passengerPrefab;

        [Header("Thông số bay (chuẩn Cocos GameCfgModuleEx)")]
        [Tooltip("Quy đổi 1 world unit Unity = bao nhiêu pixel UI Cocos")]
        public float pixelsPerWorldUnit = 120f;
        [Tooltip("passengerBoardParabolicSpeed (px/s)")]
        public float flightSpeedPx = 360f;
        [Tooltip("passengerBoardFlightDurationScale")]
        public float flightDurationScale = 0.5f;
        [Tooltip("passengerBoardJumpHeightPx")]
        public float jumpHeightPx = 100f;
        [Tooltip("Khoảng cách giữa 2 lần phóng khách liên tiếp của cùng 1 xe (giây)")]
        public float launchInterval = 0.02f;
        public float laneWidthPx = 28f;
        [Tooltip("maxBoardingPassengerVisualPerBucket")]
        public int maxVisualPerCar = 32;
        [Tooltip("maxBoardingPassengerVisualTotal")]
        public int maxVisualTotal = 200;

        private static readonly float[] Lanes = { 0f, -0.55f, 0.55f, -1f, 1f, -0.3f, 0.3f };

        private class Flight
        {
            public GameObject go;
            public Transform tr;
            public SpriteRenderer sr;
            public Car car;
            public int count;
            public Vector3 start;
            public Vector3 baseScale = Vector3.one;
            public float elapsed;
            public float duration;
            public float arcHeight;
            public float lane;
            public bool launched;
        }

        private readonly List<Flight> flights = new List<Flight>(256);
        private readonly Dictionary<Car, Queue<Flight>> waiting = new Dictionary<Car, Queue<Flight>>();
        private readonly Dictionary<Car, float> cooldown = new Dictionary<Car, float>();
        private readonly Dictionary<Car, int> visualCount = new Dictionary<Car, int>();
        private readonly Dictionary<Car, int> nextLane = new Dictionary<Car, int>();
        private readonly HashSet<Car> launchedThisTick = new HashSet<Car>();
        private readonly List<Car> carScratch = new List<Car>();
        private readonly Stack<Flight> pool = new Stack<Flight>();

        // Gốc chứa các diễn viên khách bay. Tách khỏi transform của LoopTrackSystem vì LoopTrackSystem.LoadRoadFromConfig xoá toàn bộ con của nó.
        private Transform flightRoot;
        private Transform FlightRoot
        {
            get
            {
                if (flightRoot == null)
                {
                    var go = new GameObject("PassengerFlights");
                    flightRoot = go.transform;
                }
                return flightRoot;
            }
        }

        private void OnDestroy()
        {
            if (flightRoot != null) Destroy(flightRoot.gameObject);
        }

        /// <summary>Xe còn khách đang bay/chờ phóng không (Cocos: hasPendingBoardingForBucket).</summary>
        public bool HasPending(Car car)
        {
            return car != null && visualCount.TryGetValue(car, out int n) && n > 0;
        }

        /// <summary>Có bất kỳ khách nào đang bay không.</summary>
        public bool HasActiveBoarding => flights.Count > 0;

        /// <summary>
        /// Phát lệnh cho 1 lô khách bay lên xe (Cocos: PassengerBoardingPresenter.dispatch).
        /// startPoints: toạ độ 3D của từng hạt vừa bị hút; removedCount: tổng số khách của lô.
        /// </summary>
        public void Dispatch(Car car, List<Vector3> startPoints, int removedCount)
        {
            if (car == null || removedCount <= 0) return;

            int slots = SlotsLeft(car);
            int n = startPoints != null ? startPoints.Count : 0;
            int o = Mathf.Min(n, slots);

            car.inFlightSandCount += removedCount;

            if (o <= 0)
            {
                car.OnBoardingArrive(removedCount);
                return;
            }

            int started = 0;
            for (int i = 0; i < o; i++)
            {
                if (SpawnFlight(startPoints[i], car, 1)) started++;
                else break;
            }

            int rest = removedCount - started;
            if (rest <= 0) return;

            if (SlotsLeft(car) <= 0 || started >= n)
            {
                car.OnBoardingArrive(rest);
                return;
            }

            Vector3 centroid = Vector3.zero;
            int cnt = 0;
            for (int i = started; i < n; i++)
            {
                centroid += startPoints[i];
                cnt++;
            }
            centroid = cnt > 0 ? centroid / cnt : startPoints[n - 1];

            if (!SpawnFlight(centroid, car, rest)) car.OnBoardingArrive(rest);
        }

        private int SlotsLeft(Car car)
        {
            visualCount.TryGetValue(car, out int perCar);
            return Mathf.Min(maxVisualPerCar - perCar, maxVisualTotal - flights.Count);
        }

        private bool SpawnFlight(Vector3 startPos, Car car, int count)
        {
            Flight f = AcquireFlight(car);
            if (f == null) return false;

            f.car = car;
            f.count = count;
            f.start = startPos;
            f.elapsed = 0f;
            f.duration = 0f;
            f.launched = false;
            f.tr.position = startPos;
            f.go.SetActive(false);

            flights.Add(f);
            visualCount.TryGetValue(car, out int vc);
            visualCount[car] = vc + 1;

            nextLane.TryGetValue(car, out int laneIndex);
            nextLane[car] = laneIndex + 1;
            f.lane = Lanes[laneIndex % Lanes.Length] * (1f + 0.18f * Mathf.Min(laneIndex / Lanes.Length, 2));

            Enqueue(f);
            return true;
        }

        private void Enqueue(Flight f)
        {
            var car = f.car;
            waiting.TryGetValue(car, out var q);
            cooldown.TryGetValue(car, out float cd);

            if ((q == null || q.Count == 0) && cd <= 0f)
            {
                Release(f);
                RestartCooldown(car);
                launchedThisTick.Add(car);
            }
            else
            {
                if (q == null)
                {
                    q = new Queue<Flight>();
                    waiting[car] = q;
                }
                q.Enqueue(f);
            }
        }

        private void RestartCooldown(Car car)
        {
            cooldown.TryGetValue(car, out float cd);
            cooldown[car] = launchInterval + Mathf.Max(-launchInterval, Mathf.Min(0f, cd));
        }

        private void Release(Flight f)
        {
            if (f.car == null)
            {
                Finish(f);
                return;
            }

            f.launched = true;
            f.go.SetActive(true);
            f.elapsed = 0f;

            Vector3 target = f.car.GetBoardAnchorWorldPos();
            float distPx = Vector3.Distance(f.start, target) * pixelsPerWorldUnit;
            f.duration = Mathf.Min(0.62f, Mathf.Max(0.26f, distPx / Mathf.Max(1f, flightSpeedPx) * Mathf.Max(0.1f, flightDurationScale)));
            f.arcHeight = Mathf.Min(0.55f * jumpHeightPx, 0.16f * distPx) / pixelsPerWorldUnit;
        }

        private void Update()
        {
            if (flights.Count == 0 && waiting.Count == 0 && cooldown.Count == 0) return;

            float dt = Time.deltaTime;
            TickLaunchQueues(dt);
            TickFlights(dt);
        }

        private void TickLaunchQueues(float dt)
        {
            if (dt > 0f && cooldown.Count > 0)
            {
                carScratch.Clear();
                foreach (var kv in cooldown) carScratch.Add(kv.Key);
                for (int i = 0; i < carScratch.Count; i++)
                {
                    var car = carScratch[i];
                    if (car == null)
                    {
                        cooldown.Remove(car);
                        continue;
                    }
                    if (launchedThisTick.Contains(car)) continue;
                    float remaining = cooldown[car] - dt;
                    if (remaining <= 0f && !waiting.ContainsKey(car)) cooldown.Remove(car);
                    else cooldown[car] = remaining;
                }
            }

            if (waiting.Count > 0)
            {
                carScratch.Clear();
                foreach (var kv in waiting) carScratch.Add(kv.Key);
                for (int i = 0; i < carScratch.Count; i++)
                {
                    var car = carScratch[i];
                    var q = waiting[car];
                    if (car == null)
                    {
                        while (q.Count > 0) Finish(q.Dequeue());
                        waiting.Remove(car);
                        continue;
                    }

                    if (q.Count > 0 && !launchedThisTick.Contains(car) && (!cooldown.TryGetValue(car, out float cd) || cd <= 0f))
                    {
                        Release(q.Dequeue());
                        RestartCooldown(car);
                    }

                    if (q.Count == 0)
                    {
                        waiting.Remove(car);
                        if (cooldown.TryGetValue(car, out float cd2) && cd2 <= 0f) cooldown.Remove(car);
                    }
                }
            }

            launchedThisTick.Clear();
        }

        private void TickFlights(float dt)
        {
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                var f = flights[i];
                if (!f.launched) continue;

                if (f.car == null)
                {
                    Finish(f);
                    continue;
                }

                Vector3 target = f.car.GetBoardAnchorWorldPos();
                f.elapsed += dt;
                if (f.elapsed >= f.duration)
                {
                    Finish(f);
                    continue;
                }

                float e = f.elapsed / f.duration;
                float s = e * e * (2f - e);
                float n = Mathf.Sin(Mathf.PI * e);
                float arc = 4f * f.arcHeight * e * (1f - e);

                Vector3 d = target - f.start;
                Vector3 flat = new Vector3(d.x, 0f, d.z);
                float flatLen = flat.magnitude;
                Vector3 perp = flatLen > 1e-4f ? new Vector3(-flat.z, 0f, flat.x) / flatLen : Vector3.right;
                float lateral = n * f.lane * laneWidthPx / pixelsPerWorldUnit;

                f.tr.position = f.start + d * s + perp * lateral + Vector3.up * arc;
                f.tr.localScale = f.baseScale * (1f - 0.2f * s * s);
            }
        }

        /// <summary>Khách đáp xuống xe: trả diễn viên về pool và báo cho xe (Cocos: handleBoardComplete).</summary>
        private void Finish(Flight f)
        {
            flights.Remove(f);
            var car = f.car;
            int count = f.count;

            if (car != null)
            {
                if (visualCount.TryGetValue(car, out int vc))
                {
                    vc--;
                    if (vc <= 0)
                    {
                        visualCount.Remove(car);
                        nextLane.Remove(car);
                    }
                    else visualCount[car] = vc;
                }
            }

            ReleaseFlight(f);

            if (car != null) car.OnBoardingArrive(count);
        }

        #region POOL

        private Flight AcquireFlight(Car car)
        {
            Flight f = null;
            while (pool.Count > 0 && (f == null || f.go == null)) f = pool.Pop();
            if (f == null || f.go == null) f = CreateFlight();
            if (f == null || f.go == null) return null;

            if (f.sr != null && sandBoard != null)
            {
                var colors = sandBoard.passengerColors;
                if (colors != null && colors.data != null && colors.data.TryGetValue(car.carColor, out Sprite sprite) && sprite != null)
                {
                    f.sr.sprite = sprite;
                }
            }
            else if (f.sr == null && sandBoard != null)
            {
                var mr = f.go.GetComponentInChildren<MeshRenderer>();
                if (mr != null) mr.material.color = sandBoard.GetPaletteColor((int)car.carColor);
            }

            f.tr.localScale = f.baseScale;
            return f;
        }

        private Flight CreateFlight()
        {
            GameObject go;
            if (passengerPrefab != null)
            {
                go = Instantiate(passengerPrefab, FlightRoot);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.SetParent(FlightRoot, false);
                go.transform.localScale = Vector3.one * 0.35f;
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }

            go.SetActive(false);
            return new Flight
            {
                go = go,
                tr = go.transform,
                sr = go.GetComponentInChildren<SpriteRenderer>(),
                baseScale = go.transform.localScale
            };
        }

        private void ReleaseFlight(Flight f)
        {
            f.car = null;
            f.launched = false;
            if (f.go != null)
            {
                f.go.SetActive(false);
                pool.Push(f);
            }
        }

        #endregion

        /// <summary>Huỷ toàn bộ khách đang bay (khi nạp màn mới).</summary>
        public void Clear()
        {
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                var f = flights[i];
                flights.RemoveAt(i);
                ReleaseFlight(f);
            }
            waiting.Clear();
            cooldown.Clear();
            visualCount.Clear();
            nextLane.Clear();
            launchedThisTick.Clear();
        }
    }
}
