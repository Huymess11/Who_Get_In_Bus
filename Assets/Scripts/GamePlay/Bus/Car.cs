using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

namespace WhoGetInBus.GamePlay
{
    /// <summary>
    /// LỚP BASE CỦA XE BUÝT (Car Base)
    /// Quản lý dữ liệu, màu sắc, số ghế, hiển thị và hành vi đón khách của chiếc xe.
    /// </summary>
    public class Car : MonoBehaviour, IPointerDownHandler
    {
        [Header("Thông Tin Cơ Bản")]
        public GameColorType carColor = GameColorType.Yellow;
        public int totalSeats = 100;
        public int currentSeats = 100;
        public CarMechanicType mechanicType = CarMechanicType.Normal;

        [Header("Trạng Thái Xe")]
        public bool isInLoop = false;
        public bool isPendingExit = false;
        public bool isExtraCar = false; // Xe buýt 2 tầng siêu cấp
        public bool isMerging = false;  // Xe đuôi đang bay vào xe dẫn đầu khi gộp 3 xe (đã rời làn)
        [Tooltip("Xe đang tham gia gộp 3 (Cocos: threeInOneLockSet) — vẫn chạy trên làn nhưng không hút cát")]
        public bool isThreeInOneLocked = false;

        [Header("Đón Khách (Cocos Bucket: inFlightSandCount / absorbChainCap)")]
        [Tooltip("Số khách đã bị hút khỏi bàn cát nhưng chưa bay tới xe (Cocos: inFlightSandCount)")]
        public int inFlightSandCount = 0;
        [Tooltip("Số hạt tối đa hút trong 1 lượt quét (random lại mỗi lần xe cắt qua điểm vào vòng loop)")]
        public int absorbChainCap = 8;
        [Tooltip("Điểm neo để chiếu lên bàn cát & làm đích bay của khách. Bỏ trống: dùng vị trí nhãn số ghế, rồi tới vị trí xe (Cocos: boardAnchor)")]
        public Transform boardAnchor;

        [Header("Giao Diện & Hiển Thị")]
        public TMP_Text seatText;
        public MeshRenderer bodyRenderer;
        public BusColorData busColorData;

        [Header("Hiệu Ứng Bị Chặn (Blocked Bump & Wobble)")]
        public bool isBumping { get; private set; }
        private Coroutine bumpCoroutine;
        private Quaternion savedBaseRot;
        private Vector3 savedBaseScale = Vector3.one;

        // Bộ đệm hiệu ứng rung nảy xe (Bounce)
        protected Vector3 originalScale;
        protected float bounceTimer = 0f;

        [Header("Xe Đã Đủ Khách (Cocos: Bucket.setCompleteState)")]
        [Tooltip("Dấu tick hiện thay số ghế khi xe đủ khách. Đặt cùng chỗ với số ghế (vd. con của Canvas trên nóc xe), KHÔNG đặt làm con của Text số ghế vì Text sẽ bị tắt")]
        public GameObject TickObject;
        [Tooltip("Thời gian số ghế thu nhỏ biến mất (giây)")]
        public float completeCountHideDuration = 0.12f;
        [Tooltip("Thời gian dấu tick bật ra (giây)")]
        public float completeTickPopDuration = 0.25f;

        /// <summary>Đã chuyển sang trạng thái đủ khách (đã ẩn số ghế, hiện dấu tick).</summary>
        public bool IsCompleteShown { get; private set; }
        private Coroutine completeCoroutine;
        private Vector3 seatTextBaseScale = Vector3.one;
        private Vector3 tickBaseScale = Vector3.one;
        private bool visualVisible = true;

        protected virtual void Awake()
        {
            originalScale = transform.localScale;
            CacheComponents();
            if (seatText != null) seatTextBaseScale = seatText.transform.localScale;
            if (TickObject != null)
            {
                tickBaseScale = TickObject.transform.localScale;
                if (tickBaseScale.sqrMagnitude < 1e-6f) tickBaseScale = Vector3.one;
                TickObject.SetActive(false);
            }
        }

        /// <summary>
        /// Tự động tìm kiếm các component hiển thị nếu chưa gán
        /// </summary>
        protected Transform canvasTransform;
        protected static readonly Quaternion CameraBillboardRot = Quaternion.Euler(58f, 0f, 0f);

        public void CacheComponents()
        {
            if (seatText == null) seatText = GetComponentInChildren<TMP_Text>();
            if (canvasTransform == null)
            {
                var cv = GetComponentInChildren<Canvas>();
                if (cv != null) canvasTransform = cv.transform;
                else if (seatText != null) canvasTransform = seatText.transform;
            }

            if (bodyRenderer == null)
            {
                var bodyChild = transform.Find("Body");
                bodyRenderer = bodyChild != null ? bodyChild.GetComponent<MeshRenderer>() : GetComponentInChildren<MeshRenderer>();
            }

            // Đảm bảo xe luôn có Collider để nhận sự kiện OnMouseDown/Click
            if (GetComponent<Collider>() == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(1.3f, 1.4f, 1.6f);
                box.center = new Vector3(0, 0.7f, 0);
            }

            // Đảm bảo Main Camera có PhysicsRaycaster để nhận diện click qua EventSystem
            var cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (cam != null && cam.GetComponent<PhysicsRaycaster>() == null)
            {
                cam.gameObject.AddComponent<PhysicsRaycaster>();
            }
        }

        protected virtual void LateUpdate()
        {
            // Giữ text số ghế trên nóc luôn quay đúng hướng chuẩn về Camera, thẳng thắn, không bị xoay ngược hay lộn đầu
            Quaternion targetBillboardRot = CameraBillboardRot;
            var cam = Camera.main;
            if (cam != null)
            {
                targetBillboardRot = cam.transform.rotation;
            }

            if (seatText != null)
            {
                seatText.transform.rotation = targetBillboardRot;
            }
            else if (canvasTransform != null)
            {
                canvasTransform.rotation = targetBillboardRot;
            }

            // Dấu tick cũng luôn quay về camera như số ghế
            if (TickObject != null && TickObject.activeSelf)
            {
                TickObject.transform.rotation = targetBillboardRot;
            }
        }

        /// <summary>
        /// Khởi tạo thông số chuẩn cho xe (hỗ trợ Enum CarMechanicType)
        /// </summary>
        public virtual void InitializeCar(GameColorType color, int seats, CarMechanicType mechanic, BusColorData colors)
        {
            carColor = color;
            totalSeats = seats;
            currentSeats = seats;
            mechanicType = mechanic;
            busColorData = colors;
            isInLoop = false;
            isPendingExit = false;
            isMerging = false;
            isThreeInOneLocked = false;
            inFlightSandCount = 0;

            ResetCompleteState();
            UpdateVisuals();
        }

        #region XE ĐỦ KHÁCH: ẨN SỐ GHẾ → HIỆN DẤU TICK

        /// <summary>
        /// Xe đủ khách: số ghế thu nhỏ biến mất rồi dấu tick bật ra (Cocos: setCompleteState = ẩn quantityLabel, hiện completeNode).
        /// </summary>
        public void PlayCompleteState()
        {
            if (IsCompleteShown) return;
            IsCompleteShown = true;

            if (completeCoroutine != null) StopCoroutine(completeCoroutine);

            // Bật tick lên trước (đang tắt sẵn trong prefab) ở kích thước 0, rồi mới diễn bật ra
            if (TickObject != null)
            {
                TickObject.transform.localScale = Vector3.zero;
                TickObject.SetActive(visualVisible);
            }

            if (isActiveAndEnabled)
            {
                completeCoroutine = StartCoroutine(CompleteStateRoutine());
            }
            else
            {
                HideSeatCount();
                if (TickObject != null) TickObject.transform.localScale = tickBaseScale;
            }
        }

        private IEnumerator CompleteStateRoutine()
        {
            // 1. Số ghế thu nhỏ về 0 (ease-in) rồi tắt chữ
            if (seatText != null && seatText.enabled)
            {
                var tr = seatText.transform;
                float t = 0f, dur = Mathf.Max(0.01f, completeCountHideDuration);
                while (t < dur)
                {
                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / dur);
                    tr.localScale = seatTextBaseScale * (1f - k * k);
                    yield return null;
                }
            }
            HideSeatCount();

            // 2. Dấu tick bật ra 0 → vượt 1 → 1 (backOut)
            if (TickObject != null)
            {
                var tr = TickObject.transform;
                float t = 0f, dur = Mathf.Max(0.01f, completeTickPopDuration);
                while (t < dur)
                {
                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / dur) - 1f;
                    const float s = 1.70158f;
                    tr.localScale = tickBaseScale * (k * k * ((s + 1f) * k + s) + 1f);
                    yield return null;
                }
                tr.localScale = tickBaseScale;
            }
            completeCoroutine = null;
        }

        /// <summary>Tắt chữ số ghế (chỉ tắt component, không tắt GameObject để tick đặt làm con của Text vẫn hiện được).</summary>
        private void HideSeatCount()
        {
            if (seatText == null) return;
            seatText.transform.localScale = seatTextBaseScale;
            seatText.enabled = false;
        }

        /// <summary>Trả về trạng thái chưa đủ khách: hiện số ghế, ẩn dấu tick.</summary>
        public void ResetCompleteState()
        {
            IsCompleteShown = false;
            if (completeCoroutine != null)
            {
                StopCoroutine(completeCoroutine);
                completeCoroutine = null;
            }
            if (seatText != null)
            {
                seatText.transform.localScale = seatTextBaseScale;
                seatText.gameObject.SetActive(true);
                seatText.enabled = visualVisible;
            }
            if (TickObject != null)
            {
                TickObject.transform.localScale = tickBaseScale;
                TickObject.SetActive(false);
            }
        }

        #endregion

        public virtual void InitializeCar(GameColorType color, int seats, int mechanic, BusColorData colors)
        {
            InitializeCar(color, seats, (CarMechanicType)mechanic, colors);
        }

        /// <summary>
        /// Cập nhật hiển thị số ghế trên nóc và màu sơn thân xe
        /// </summary>
        public virtual void UpdateVisuals(BusColorData colors = null)
        {
            if (colors != null) busColorData = colors;
            CacheComponents();

            // 1. Cập nhật số ghế còn lại
            if (seatText != null)
            {
                seatText.text = currentSeats.ToString();
            }

            // 2. Gán Material đúng màu xe
            if (busColorData != null && busColorData.data.TryGetValue(carColor, out Material targetMaterial))
            {
                if (bodyRenderer != null) bodyRenderer.material = targetMaterial;
            }
        }

        /// <summary>
        /// Nhu cầu thực: số ghế còn trống sau khi trừ đi khách đang bay tới (Cocos: Bucket.realDemand).
        /// </summary>
        public int RealDemand => Mathf.Max(0, currentSeats - inFlightSandCount);

        /// <summary>Đặt khoảng [min, max] cho số hạt hút mỗi lượt rồi random luôn 1 giá trị (Cocos: setAbsorbChainTran).</summary>
        public void SetAbsorbChainTran(IList<int> tran, int defaultMin, int defaultMax)
        {
            absorbChainMin = defaultMin;
            absorbChainMax = defaultMax;
            if (tran != null && tran.Count >= 2)
            {
                absorbChainMin = Mathf.Min(tran[0], tran[1]);
                absorbChainMax = Mathf.Max(tran[0], tran[1]);
            }
            RerollAbsorbChainCap();
        }

        /// <summary>Random lại số hạt tối đa hút trong lượt kế tiếp (gọi mỗi lần xe cắt qua điểm vào vòng loop).</summary>
        public void RerollAbsorbChainCap()
        {
            absorbChainCap = absorbChainMax > absorbChainMin ? Random.Range(absorbChainMin, absorbChainMax + 1) : absorbChainMin;
        }

        private int absorbChainMin = 8;
        private int absorbChainMax = 12;

        /// <summary>Số hạt được phép hút trong lượt quét này = min(absorbChainCap, nhu cầu thực).</summary>
        public int GetAbsorbChainCap()
        {
            return Mathf.Min(absorbChainCap, RealDemand);
        }

        /// <summary>
        /// Điểm neo trong thế giới 3D để (1) chiếu lên bàn cát tìm ô cần hút, (2) làm đích cho khách bay tới.
        /// Cocos: Bucket.getBoardAnchorUIWorldPos (node "boardAnchor" hoặc renderRoot2D — chính là nhãn số ghế trên nóc xe).
        /// </summary>
        public Vector3 GetBoardAnchorWorldPos()
        {
            if (boardAnchor != null) return boardAnchor.position;
            if (seatText != null) return seatText.transform.position;
            return transform.position + Vector3.up * 0.85f;
        }

        /// <summary>
        /// Khách bay tới nơi: trừ ghế, nảy thân xe (Cocos: onAbsorbSandArrive).
        /// </summary>
        public virtual void OnBoardingArrive(int count)
        {
            inFlightSandCount = Mathf.Max(0, inFlightSandCount - count);
            AbsorbPassengers(count);
        }

        /// <summary>
        /// Đón 1 hành khách vào xe: giảm ghế và kích hoạt hiệu ứng rung nảy
        /// </summary>
        public virtual void AbsorbPassenger()
        {
            AbsorbPassengers(1);
        }

        public virtual void AbsorbPassengers(int count)
        {
            if (count <= 0 || currentSeats <= 0) return;
            currentSeats = Mathf.Max(0, currentSeats - count);
            if (seatText != null) seatText.text = currentSeats.ToString();
            bounceTimer = 0.25f; // Rung nảy thân xe
        }

        /// <summary>Ẩn/hiện toàn bộ phần nhìn thấy của xe (dùng khi xe chui vào hầm - Cocos: enterCave/exitCave).</summary>
        public void SetVisualVisible(bool visible)
        {
            // Chỉ truy vấn component 1 lần rồi dùng lại (xe ra/vào hầm mỗi vòng)
            if (visualRenderers == null) visualRenderers = GetComponentsInChildren<Renderer>(true);
            if (bodyCollider == null) bodyCollider = GetComponent<Collider>();

            for (int i = 0; i < visualRenderers.Length; i++) visualRenderers[i].enabled = visible;
            if (seatText != null) seatText.enabled = visible && !IsCompleteShown; // xe đã đủ khách thì số ghế giữ tắt
            if (bodyCollider != null) bodyCollider.enabled = visible;

            // Dấu tick (thường là UI Image, không phải Renderer) cũng ẩn theo khi xe chui hầm
            visualVisible = visible;
            if (TickObject != null && IsCompleteShown) TickObject.SetActive(visible);
        }

        private Renderer[] visualRenderers;
        private Collider bodyCollider;

        /// <summary>
        /// Hiệu ứng rung nảy thân xe khi đón khách hoặc hợp thể
        /// </summary>
        protected virtual void UpdateBounce()
        {
            if (bounceTimer > 0f)
            {
                bounceTimer -= Time.deltaTime;
                if (bounceTimer <= 0f)
                {
                    bounceTimer = 0f;
                    transform.localScale = originalScale;
                }
                else
                {
                    float punch = Mathf.Sin(bounceTimer * Mathf.PI * 8f) * 0.12f;
                    transform.localScale = originalScale + new Vector3(punch, punch * 1.5f, punch);
                }
            }
        }

        /// <summary>
        /// Hiệu ứng hợp thể thành công thành xe 2 tầng Extra Car (phóng to từ 0 do LoopTrackSystem chạy — Cocos: playThreeInOneVfx)
        /// </summary>
        public virtual void PlayMergeSuccessEffect()
        {
            Debug.Log($"<color=yellow>⭐ [HỢP THỂ THÀNH CÔNG]</color> Đã gộp 3 xe màu {carColor} thành EXTRA CAR 2 tầng với {currentSeats} chỗ!");
        }

        // Tương thích ngược với Car.cs cũ
        public Material GetColor()
        {
            if (busColorData != null && busColorData.data.TryGetValue(carColor, out Material targetMaterial))
            {
                return targetMaterial;
            }
            return null;
        }

        #region XỬ LÝ CLICK & HOẠT CẢNH BỊ CHẶN (BUS OUT STYLE)

        /// <summary>
        /// Bắt sự kiện click chuột hoặc chạm màn hình (hỗ trợ cả New Input System & Legacy Input System)
        /// </summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            OnCarClicked();
        }

        private void OnMouseDown()
        {
            OnCarClicked();
        }

        /// <summary>
        /// Xử lý khi xe được click: gửi thông báo đến ParkingLotManager
        /// </summary>
        public virtual void OnCarClicked()
        {
            // OnPointerDown (EventSystem) và OnMouseDown cùng bắn cho 1 lần chạm → chỉ xử lý 1 lần mỗi frame
            if (Time.frameCount == lastClickFrame) return;
            lastClickFrame = Time.frameCount;

            if (ParkingLotManager.Instance != null)
            {
                ParkingLotManager.Instance.OnCarClicked(this);
            }
        }

        private int lastClickFrame = -1;

        /// <summary>
        /// Hoạt cảnh xe không xuất bến được: nhún và lắc lư tại chỗ
        /// </summary>
        public virtual void PlayBlockedBumpAnimation()
        {
            if (isInLoop || isPendingExit || isMerging) return;

            // Đang nhún dở: trả về dáng gốc đã lưu rồi diễn lại từ đầu (không lưu đè dáng đang méo làm gốc)
            if (bumpCoroutine != null)
            {
                StopCoroutine(bumpCoroutine);
                bumpCoroutine = null;
                transform.rotation = savedBaseRot;
                transform.localScale = savedBaseScale;
            }
            else
            {
                savedBaseRot = transform.rotation;
                savedBaseScale = transform.localScale;
            }

            if (!isActiveAndEnabled) return;
            bumpCoroutine = StartCoroutine(BlockedBumpRoutine());
        }

        /// <summary>
        /// Dừng ngay hoạt cảnh nhún lắc và trả lại góc xoay + kích thước gốc.
        /// Hoạt cảnh này không bao giờ đụng tới vị trí nên xe đang trượt / lao ra không bị kéo về chỗ cũ.
        /// </summary>
        public virtual void StopBlockedBumpAnimation()
        {
            if (bumpCoroutine != null)
            {
                StopCoroutine(bumpCoroutine);
                bumpCoroutine = null;
            }

            if (isBumping)
            {
                transform.rotation = savedBaseRot;
                transform.localScale = savedBaseScale;
                isBumping = false;
            }
        }

        [Header("Hoạt Cảnh Không Xuất Bến Được (nhún + lắc lư tại chỗ)")]
        [Tooltip("Tổng thời gian nhún lắc (giây)")]
        public float blockedWobbleDuration = 0.4f;
        [Tooltip("Độ nén thân xe khi nhún (0.08 = nén 8% chiều cao)")]
        public float blockedSquash = 0.08f;
        [Tooltip("Góc lắc lư sang hai bên tối đa (độ)")]
        public float blockedSwayAngle = 6f;

        /// <summary>
        /// Xe không xuất bến được: nhún (nén/giãn thân xe) và lắc lư sang hai bên tại chỗ, tắt dần — không tiến về phía trước.
        /// Chỉ thay đổi góc xoay + kích thước, KHÔNG ghi vị trí (để chạy song song an toàn với hoạt cảnh dồn hàng của bãi xe).
        /// </summary>
        protected virtual IEnumerator BlockedBumpRoutine()
        {
            isBumping = true;
            Quaternion originRot = savedBaseRot;
            Vector3 originScale = savedBaseScale;

            float dur = Mathf.Max(0.05f, blockedWobbleDuration);
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / dur);
                float decay = (1f - p) * (1f - p);

                // Nhún: thân xe dẹt xuống rồi bật lên (giữ thể tích: cao giảm thì ngang nở ra)
                float s = Mathf.Sin(p * Mathf.PI * 3f) * blockedSquash * decay;
                transform.localScale = Vector3.Scale(originScale, new Vector3(1f + s * 0.5f, 1f - s, 1f + s * 0.5f));

                // Lắc lư sang trái/phải quanh trục dọc thân xe
                float roll = Mathf.Sin(p * Mathf.PI * 4f) * blockedSwayAngle * decay;
                transform.rotation = originRot * Quaternion.Euler(0f, 0f, roll);
                yield return null;
            }

            transform.rotation = originRot;
            transform.localScale = originScale;
            isBumping = false;
            bumpCoroutine = null;
        }

        #endregion
    }
}
