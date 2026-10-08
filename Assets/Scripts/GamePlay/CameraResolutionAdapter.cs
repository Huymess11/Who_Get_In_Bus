using System.Collections.Generic;
using UnityEngine;

public class CameraResolutionAdapter : MonoBehaviour
{
    public enum AspectFitMode
    {
        [Tooltip("Tự động thích ứng: iPhone dài thì mở rộng để không mất 2 bên, iPad bè thì giữ chiều cao để không mất trên/dưới")]
        FitAll,

        [Tooltip("Khóa bề ngang thế giới: Công thức chuẩn LevelController.ts game gốc")]
        FitWidth,

        [Tooltip("Khóa bề dọc thế giới: Giữ nguyên orthoSize")]
        FitHeight
    }

    [Header("--- Cấu Hình Màn Hình Chuẩn (Design Resolution) ---")]
    [Tooltip("Độ phân giải chuẩn thiết kế (Game gốc: 750 x 1334, chiều cao ortho thiết kế = 667)")]
    public Vector2 referenceResolution = new Vector2(750f, 1334f);

    [Tooltip("Kích thước Orthographic chuẩn trong Scene ở độ phân giải thiết kế")]
    public float baseOrthoSize = 15f;

    [Header("--- Chế Độ Thích Ứng Tỉ Lệ (Fit Mode) ---")]
    [Tooltip("FitAll là tối ưu nhất: đảm bảo cả iPhone dài và iPad ngắn đều hiển thị trọn vẹn bàn cát & bãi xe")]
    public AspectFitMode fitMode = AspectFitMode.FitAll;

    [Header("--- Đồng Bộ Thêm Camera Phụ (Highlight / UI) ---")]
    [Tooltip("Các camera phụ cần đồng bộ orthoSize (ví dụ cameraHighlight trong code gốc)")]
    public List<Camera> additionalCameras = new List<Camera>();

    [Header("--- Cố Định Vị Trí & Góc Xoay (Lock Transform) ---")]
    [Tooltip("Nếu bật, cố định Position và Rotation không cho phép script khác vô tình thay đổi")]
    public bool lockTransform = false;

    [SerializeField, HideInInspector]
    private Vector3 fixedPosition;

    [SerializeField, HideInInspector]
    private Quaternion fixedRotation;

    [SerializeField, HideInInspector]
    private bool hasRecordedTransform = false;

    // Cache component
    private Camera targetCamera;
    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;
    private float lastOrthoSize = -1f;

    public Camera TargetCamera
    {
        get
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }
            return targetCamera;
        }
    }

    public float CurrentOrthoSize => TargetCamera != null ? TargetCamera.orthographicSize : baseOrthoSize;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        if (targetCamera != null)
        {
            targetCamera.orthographic = true;
            if (baseOrthoSize <= 0f)
            {
                baseOrthoSize = targetCamera.orthographicSize > 0f ? targetCamera.orthographicSize : 15f;
            }
        }

        if (!hasRecordedTransform)
        {
            RecordFixedTransform();
        }

        ApplyCameraFit();
    }

    private void OnEnable()
    {
        ApplyCameraFit();
    }

    private void Start()
    {
        ApplyCameraFit();
    }

    private void Update()
    {
        // Kiểm tra khi kích thước cửa sổ / độ phân giải thay đổi (window-resize hoặc xoay màn hình)
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            ApplyCameraFit();
        }

        // Nếu bật khóa Transform
        if (lockTransform && hasRecordedTransform)
        {
            if (transform.localPosition != fixedPosition)
            {
                transform.localPosition = fixedPosition;
            }
            if (transform.localRotation != fixedRotation)
            {
                transform.localRotation = fixedRotation;
            }
        }
    }

    /// <summary>
    /// Ghi nhận vị trí và góc nhìn hiện tại để khóa cố định 100%
    /// </summary>
    [ContextMenu("Record Current Transform as Fixed")]
    public void RecordFixedTransform()
    {
        fixedPosition = transform.localPosition;
        fixedRotation = transform.localRotation;
        hasRecordedTransform = true;
    }

    /// <summary>
    /// Tính toán và áp dụng kích thước Orthographic Size phù hợp với tỉ lệ màn hình hiện tại
    /// </summary>
    [ContextMenu("Apply Camera Fit Now")]
    public void ApplyCameraFit()
    {
        if (TargetCamera == null) return;
        if (Screen.width <= 0 || Screen.height <= 0) return;

        TargetCamera.orthographic = true;

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        float targetAspect = referenceResolution.x / referenceResolution.y;
        float currentAspect = (float)Screen.width / (float)Screen.height;

        float calculatedOrthoSize = baseOrthoSize;

        switch (fitMode)
        {
            case AspectFitMode.FitAll:
                // Màn hình hẹp/dài hơn chuẩn (iPhone 16, Galaxy Ultra):
                // Cần tăng orthoSize để bề ngang không bị cắt.
                // Màn hình bè/ngắn hơn chuẩn (iPad, Tablet 4:3):
                // Giữ baseOrthoSize để mép trên (bàn cát) và mép dưới (bãi xe) không bị cắt.
                if (currentAspect < targetAspect)
                {
                    calculatedOrthoSize = baseOrthoSize * (targetAspect / currentAspect);
                }
                else
                {
                    calculatedOrthoSize = baseOrthoSize;
                }
                break;

            case AspectFitMode.FitWidth:
                // Công thức gốc Cocos Creator LevelController.ts:
                // this.cameraDefault.orthoHeight = this.cameraDefault.orthoHeight / 667 * canvas.orthoHeight;
                // Tương đương hoàn toàn với: baseOrthoSize * (targetAspect / currentAspect)
                calculatedOrthoSize = baseOrthoSize * (targetAspect / currentAspect);
                break;

            case AspectFitMode.FitHeight:
                calculatedOrthoSize = baseOrthoSize;
                break;
        }

        TargetCamera.orthographicSize = calculatedOrthoSize;
        lastOrthoSize = calculatedOrthoSize;

        // Đồng bộ sang các camera phụ (Highlight Camera, SubCamera, UI Camera)
        SyncAdditionalCameras(calculatedOrthoSize);
    }

    private void SyncAdditionalCameras(float orthoSize)
    {
        if (additionalCameras == null || additionalCameras.Count == 0) return;

        for (int i = 0; i < additionalCameras.Count; i++)
        {
            Camera cam = additionalCameras[i];
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = orthoSize;
            }
        }
    }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Camera cam = TargetCamera;
            if (cam == null || !cam.orthographic) return;

            Gizmos.color = Color.cyan;
            Matrix4x4 originalMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

            float height = cam.orthographicSize * 2f;
            float width = height * cam.aspect;
            float distance = (cam.nearClipPlane + cam.farClipPlane) * 0.5f;

            Gizmos.DrawWireCube(new Vector3(0, 0, distance), new Vector3(width, height, cam.farClipPlane - cam.nearClipPlane));

            Gizmos.matrix = originalMatrix;
        }
#endif
}
