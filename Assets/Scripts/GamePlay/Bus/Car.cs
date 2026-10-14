using System.Collections;
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
        public bool isMerging = false;  // Đang trong hiệu ứng gộp 3 xe

        [Header("Giao Diện & Hiển Thị")]
        public TMP_Text seatText;
        public MeshRenderer bodyRenderer;
        public BusColorData busColorData;

        [Header("Hiệu Ứng Bị Chặn (Blocked Bump & Wobble)")]
        public bool isBumping { get; private set; }
        private Coroutine bumpCoroutine;
        private Vector3 savedBasePos;
        private Quaternion savedBaseRot;

        // Bộ đệm hiệu ứng rung nảy xe (Bounce)
        protected Vector3 originalScale;
        protected float bounceTimer = 0f;

        protected virtual void Awake()
        {
            originalScale = transform.localScale;
            CacheComponents();
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

            UpdateVisuals();
        }

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
        /// Đón 1 hành khách vào xe: giảm ghế và kích hoạt hiệu ứng rung nảy
        /// </summary>
        public virtual void AbsorbPassenger()
        {
            if (currentSeats > 0)
            {
                currentSeats--;
                if (seatText != null) seatText.text = currentSeats.ToString();
                bounceTimer = 0.25f; // Rung nảy thân xe
            }
        }

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
        /// Hiệu ứng hợp thể thành công thành xe 2 tầng Extra Car
        /// </summary>
        public virtual void PlayMergeSuccessEffect()
        {
            bounceTimer = 0.5f;
            transform.localScale = originalScale * 1.4f;
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
            if (ParkingLotManager.Instance != null)
            {
                ParkingLotManager.Instance.OnCarClicked(this);
            }
        }

        /// <summary>
        /// Hoạt cảnh xe bị kẹt phía sau: chồm lên phía trước rồi nảy dội lùi lại và rung lắc dữ dội (Bus Out style)
        /// </summary>
        public virtual void PlayBlockedBumpAnimation()
        {
            if (isInLoop || isPendingExit || isMerging) return;

            if (bumpCoroutine != null)
            {
                StopCoroutine(bumpCoroutine);
                transform.position = savedBasePos;
                transform.rotation = savedBaseRot;
            }
            else
            {
                savedBasePos = transform.position;
                savedBaseRot = transform.rotation;
            }

            bumpCoroutine = StartCoroutine(BlockedBumpRoutine());
        }

        /// <summary>
        /// Dừng ngay hoạt cảnh rung lắc nếu xe cần di chuyển tiến lên
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
                transform.position = savedBasePos;
                transform.rotation = savedBaseRot;
                isBumping = false;
            }
        }

        /// <summary>
        /// Coroutine mô phỏng hiệu ứng va chạm đàn hồi cực đã tay:
        /// 1. Nhào tới trước 0.35m trong 0.07s
        /// 2. Bật nảy lùi về vị trí ban đầu trong 0.08s
        /// 3. Thân xe rung lắc chao đảo (Wobble Shake) 0.2s tắt dần
        /// </summary>
        protected virtual IEnumerator BlockedBumpRoutine()
        {
            isBumping = true;
            Vector3 originPos = savedBasePos;
            Quaternion originRot = savedBaseRot;

            float bumpDist = 0.35f;
            Vector3 bumpTarget = originPos + transform.forward * bumpDist;

            // Phase 1: Xe chồm mạnh lên phía trước (Ease-Out, 0.07s)
            float t = 0f;
            float bumpDur = 0.07f;
            while (t < bumpDur)
            {
                t += Time.deltaTime;
                float p = Mathf.Sin((t / bumpDur) * (Mathf.PI * 0.5f));
                transform.position = Vector3.Lerp(originPos, bumpTarget, p);
                yield return null;
            }

            // Phase 2: Nảy dội ngược về vị trí ban đầu (Ease-In, 0.08s)
            t = 0f;
            float returnDur = 0.08f;
            while (t < returnDur)
            {
                t += Time.deltaTime;
                float p = 1f - Mathf.Cos((t / returnDur) * (Mathf.PI * 0.5f));
                transform.position = Vector3.Lerp(bumpTarget, originPos, p);
                yield return null;
            }
            transform.position = originPos;

            // Phase 3: Thân xe rung lắc chao đảo quanh các trục (Wobble Shake, 0.2s)
            t = 0f;
            float shakeDur = 0.2f;
            float maxAngle = 4.5f;
            while (t < shakeDur)
            {
                t += Time.deltaTime;
                float decay = 1f - (t / shakeDur);
                float roll = Mathf.Sin(t * 36f) * maxAngle * decay;
                float yaw = Mathf.Cos(t * 28f) * (maxAngle * 0.4f) * decay;
                float pitch = Mathf.Sin(t * 42f) * (maxAngle * 0.25f) * decay;

                transform.rotation = originRot * Quaternion.Euler(pitch, yaw, roll);
                yield return null;
            }

            transform.position = originPos;
            transform.rotation = originRot;
            isBumping = false;
            bumpCoroutine = null;
        }

        #endregion
    }
}
