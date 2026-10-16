using UnityEngine;

namespace WhoGetInBus.GamePlay
{
    /// <summary>
    /// ĐÈN GIAO THÔNG + THANH CHẮN Ở LÀN THOÁT (Cocos: CartTrackController.applyExitPathOpenVisual / applyExitPathCloseVisual).
    ///
    /// - Đóng (mặc định): bật On Red + Off Green, tắt Off Red + On Green, hạ thanh chắn.
    /// - Mở (có xe đầy khách chờ ra hoặc đang chạy trên làn thoát): bật On Green + Off Red, tắt On Red + Off Green, nâng thanh chắn.
    ///
    /// LoopTrackSystem gọi SetExitOpen mỗi khi trạng thái xe ra thay đổi; script chỉ chạy Update trong lúc thanh chắn đang chuyển động.
    /// </summary>
    public class TrafficAndBarrierManager : MonoBehaviour
    {
        [Header("Đèn Giao Thông (kéo thả các con của Traffic light)")]
        [SerializeField] private GameObject onRed;
        [SerializeField] private GameObject offRed;
        [SerializeField] private GameObject onGreen;
        [SerializeField] private GameObject offGreen;

        [Header("Thanh Chắn (kéo thả object Arm của Barrier)")]
        [Tooltip("Phần thanh ngang, pivot nằm ở bản lề")]
        [SerializeField] private Transform barrierArm;
        [Tooltip("Góc xoay cộng thêm (local) khi mở so với lúc đóng. Nếu thanh chắn chúi xuống thì đổi dấu")]
        [SerializeField] private Vector3 openRotationOffset = new Vector3(0f, 0f, 80f);
        [Tooltip("Thời gian nâng thanh chắn (giây)")]
        [SerializeField] private float openDuration = 0.5f;
        [Tooltip("Thời gian hạ thanh chắn (giây)")]
        [SerializeField] private float closeDuration = 0.35f;

        public bool IsOpen { get; private set; }

        private Quaternion closedRotation;
        private Quaternion openRotation;
        private float progress;      // 0 = đóng, 1 = mở
        private bool initialized;

        private void Awake()
        {
            Init();
            SetExitOpen(false, true);
        }

        private void Init()
        {
            if (initialized) return;
            initialized = true;
            if (barrierArm != null)
            {
                closedRotation = barrierArm.localRotation;
                openRotation = closedRotation * Quaternion.Euler(openRotationOffset);
            }
        }

        /// <summary>Mở/đóng làn thoát. instant = true: đặt ngay không chạy hiệu ứng (khi nạp màn).</summary>
        public void SetExitOpen(bool open, bool instant = false)
        {
            Init();
            if (open == IsOpen && !instant) return;
            IsOpen = open;

            SetActive(onRed, !open);
            SetActive(offGreen, !open);
            SetActive(offRed, open);
            SetActive(onGreen, open);

            if (instant)
            {
                progress = open ? 1f : 0f;
                ApplyArm();
                enabled = false;
            }
            else
            {
                enabled = barrierArm != null;
            }
        }

        private void Update()
        {
            float target = IsOpen ? 1f : 0f;
            float duration = Mathf.Max(0.01f, IsOpen ? openDuration : closeDuration);
            progress = Mathf.MoveTowards(progress, target, Time.deltaTime / duration);
            ApplyArm();
            if (Mathf.Approximately(progress, target)) enabled = false;
        }

        private void ApplyArm()
        {
            if (barrierArm == null) return;
            barrierArm.localRotation = Quaternion.Slerp(closedRotation, openRotation, Mathf.SmoothStep(0f, 1f, progress));
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
        }

#if UNITY_EDITOR
        /// <summary>Thêm component vào scene sẽ tự tìm theo tên con (On Red, Off Red, On Green, Off Green, Arm) nếu chưa kéo thả.</summary>
        private void Reset()
        {
            onRed = FindChild("On Red")?.gameObject;
            offRed = FindChild("Off Red")?.gameObject;
            onGreen = FindChild("On Green")?.gameObject;
            offGreen = FindChild("Off Green")?.gameObject;
            barrierArm = FindChild("Arm");
        }

        private static Transform FindChild(string name)
        {
            // ưu tiên object đang bật (scene có thể còn bản cũ đã tắt trùng tên)
            foreach (var mode in new[] { FindObjectsInactive.Exclude, FindObjectsInactive.Include })
                foreach (var t in FindObjectsByType<Transform>(mode, FindObjectsSortMode.None))
                    if (t.name == name && t.gameObject.scene.IsValid()) return t;
            return null;
        }
#endif
    }
}
