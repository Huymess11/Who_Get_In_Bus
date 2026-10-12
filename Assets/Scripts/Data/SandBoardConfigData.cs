using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WhoGetInBus.Data
{
    /// <summary>
    /// ScriptableObject lưu trữ toàn bộ thông số cấu hình tranh cát (kích thước hạt, khoảng cách, vị trí bảng, so le...).
    /// Giúp lưu dữ liệu vĩnh viễn trong Project, không bao giờ bị reset hoặc mất khi sửa code C#.
    /// </summary>
    [CreateAssetMenu(fileName = "SandBoardConfigData", menuName = "STO/SandBoardConfigData")]
    public class SandBoardConfigData : ScriptableObject
    {
        [Header("Vị Trí Bảng Cát Trên Canvas UI (Board Position)")]
        [Tooltip("Toạ độ AnchoredPosition (X, Y) của GameObject SandBoard_Manager trên Canvas")]
        public Vector2 boardPosition = new Vector2(0f, -3.5f);

        [Header("Thông Số Kích Thước & Tỉ Lệ Hạt Cát")]
        [Tooltip("Kích thước quad hạt cát (chuẩn Cocos: 34 x 44 pixel theo tỷ lệ ảnh 48x62, tạo độ lấp lên 20px)")]
        public Vector2 beadSize = new Vector2(34f, 44f);

        [Tooltip("Tỉ lệ scale bổ sung của hạt cát (chuẩn 1.0)")]
        [Range(0.5f, 2.0f)]
        public float beadScale = 1.0f;

        [Header("Khoảng Cách Lưới (Grid Spacing)")]
        [Tooltip("Khoảng cách giữa các cột hạt trên UI theo chiều ngang trục X (chuẩn Cocos: 24.32 pixel)")]
        [Range(10f, 50f)]
        public float beadSpacingX = 24.32f;

        [Tooltip("Khoảng cách giữa các hàng hạt trên UI theo chiều dọc trục Y (chuẩn Cocos: 24.32 pixel)")]
        [Range(10f, 50f)]
        public float beadSpacingY = 24.32f;

        [Header("Xếp So Le (Zigzag Hex-Packing)")]
        [Tooltip("Bật/tắt hiệu ứng xếp hạt so le")]
        public bool enableZigzag = true;

        [Tooltip("Độ lệch trục X của các hàng so le (chuẩn tổ ong: 12.16 pixel = nửa spacingX)")]
        [Range(0f, 30f)]
        public float zigzagOffset = 12.16f;

        [Header("Căn Chỉnh Độ Lệch Hạt Cát (Bead Offset)")]
        [Tooltip("Độ lệch pixel ngang (trái/phải) của toàn bộ các hạt cát")]
        [Range(-300f, 300f)]
        public float boardOffsetX = 0f;

        [Tooltip("Độ lệch pixel dọc (lên/xuống) của toàn bộ các hạt cát")]
        [Range(-300f, 300f)]
        public float boardOffsetY = 0f;

        [Header("Hiệu Ứng Sạt Lở (Collapse)")]
        [Tooltip("Thời gian hiệu ứng hạt trên trượt rơi xuống lấp chỗ trống (giây)")]
        [Range(0.05f, 0.5f)]
        public float collapseDuration = 0.12f;

        [Header("Kích Thước Ma Trận")]
        public int columns = 40;
        public int rows = 40;

        [Header("Asset Tham Chiếu")]
        public Texture2D passengerAtlas;
        public PassengerColorData passengerColors;

        [ContextMenu("⚡ Đặt Về Chuẩn Cocos 1:1")]
        public void ResetToCocosDefault()
        {
            boardPosition = new Vector2(0f, -3.5f);
            beadSize = new Vector2(34f, 44f);
            beadScale = 1.0f;
            beadSpacingX = 24.32f;
            beadSpacingY = 24.32f;
            enableZigzag = true;
            zigzagOffset = 12.16f;
            boardOffsetX = 0f;
            boardOffsetY = 0f;
            collapseDuration = 0.12f;
            columns = 40;
            rows = 40;

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
#endif
            Debug.Log("<color=green>[SandBoardConfigData]</color> Đã đặt toàn bộ thông số về Chuẩn Cocos 1:1!");
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            WhoGetInBus.GamePlay.SandBoardManager mgr = Object.FindFirstObjectByType<WhoGetInBus.GamePlay.SandBoardManager>();
            if (mgr != null && (mgr.config == this || mgr.config == null))
            {
                if (mgr.config == null) mgr.config = this;
                mgr.SyncFromConfig();
                mgr.UpdateBoardPosition();
                mgr.RefreshGraphic();
                EditorUtility.SetDirty(mgr.gameObject);
                SceneView.RepaintAll();
            }
        }
#endif
    }
}
