using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WhoGetInBus.Data
{
    /// <summary>
    /// ScriptableObject lưu trữ toàn bộ thông số co giãn và căn chỉnh toạ độ đường đua (LoopTrack & Road Waypoints).
    /// Giúp lưu dữ liệu vĩnh viễn trong Project, KHÔNG bao giờ bị reset hoặc mất khi sửa code C#.
    /// Đồng bộ trực tiếp giữa LoopTrackSystem và RoadConfigGizmoViewer.
    /// Cho phép kéo thả slider căn chỉnh theo thời gian thực (Real-time) trên Scene View y hệt SandBoardConfigData.
    /// </summary>
    [CreateAssetMenu(fileName = "RoadTrackConfigData", menuName = "STO/RoadTrackConfigData")]
    public class RoadTrackConfigData : ScriptableObject
    {
        [Header("Bật/Tắt Co Giãn Đường (Track Scale)")]
        [Tooltip("Bật/tắt tự động co giãn toạ độ waypoint từ Cocos sang vừa khít với lòng đường Unity")]
        public bool enableTrackScale = true;

        [Header("Hiệu Chỉnh Tỷ Lệ Co Giãn (Scale)")]
        [Tooltip("Tỷ lệ chiều rộng trục X (Mặc định: 100/117.39 = 0.852 — đổi 1 đơn vị Cocos (100px UI) sang đơn vị Unity với camera ortho 9.2; 1.0 là toạ độ gốc Cocos)")]
        [Range(0.5f, 1.3f)]
        public float scaleX = 0.852f;

        [Tooltip("Tỷ lệ chiều dọc trục Z (Mặc định: 0.767 — khớp ảnh Top đang bị ép dọc (700px thay vì 777px). Nếu bật matchOriginalRoadArtProportion ở GameManager thì dùng 0.852)")]
        [Range(0.5f, 1.3f)]
        public float scaleZ = 0.767f;

        [Header("Độ Lệch Dịch Tâm (Offset)")]
        [Tooltip("Dịch ngang trục X sang trái (-) hoặc sang phải (+)")]
        [Range(-2f, 2f)]
        public float offsetX = 0f;

        [Tooltip("Dịch dọc trục Z lên trên (+) hoặc xuống dưới (-). Mặc định: -0.018 (đo từ ảnh đường Top hiện tại)")]
        [Range(-2f, 2f)]
        public float offsetZ = -0.018f;

        [Header("Cao Độ Nâng Lên (Elevation / Height Offset)")]
        [Tooltip("Cao độ nâng các điểm waypoint lên trục Y để không bị chìm/z-fighting")]
        [Range(-0.5f, 0.5f)]
        public float heightOffset = 0f;

        /// <summary>Bắn ra khi thông số được chỉnh trong Inspector/Editor (các công cụ Editor đăng ký để đồng bộ lại).</summary>
        public static event System.Action<RoadTrackConfigData> ConfigChanged;

        public Vector2 TrackScale => new Vector2(scaleX, scaleZ);
        public Vector3 TrackOffset => new Vector3(offsetX, heightOffset, offsetZ);

        [ContextMenu("⚡ Đặt Về Chuẩn Vừa Khít Lòng Đường (X: 0.852, Z: 0.767 — khớp ảnh đường hiện tại)")]
        public void ResetToFitRoadDefault()
        {
            enableTrackScale = true;
            scaleX = 0.852f;
            scaleZ = 0.767f;
            offsetX = 0f;
            offsetZ = -0.018f;
            heightOffset = 0f;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            NotifyChanges();
#endif
            Debug.Log("<color=green>[RoadTrackConfigData]</color> Đã đặt về Chuẩn Vừa Khít Lòng Đường (X: 0.852, Z: 0.767, OffsetZ: -0.018)!");
        }

        [ContextMenu("⚡ Đặt Về Chuẩn Rộng Hơn (X: 0.90, Z: 0.767)")]
        public void SetWiderPreset()
        {
            enableTrackScale = true;
            scaleX = 0.90f;
            scaleZ = 0.767f;
            offsetX = 0f;
            offsetZ = -0.018f;
            heightOffset = 0f;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            NotifyChanges();
#endif
            Debug.Log("<color=green>[RoadTrackConfigData]</color> Đã đặt về Chuẩn Rộng Hơn (X: 0.90)!");
        }

        [ContextMenu("⚡ Đặt Về Chuẩn Hẹp Hơn (X: 0.80, Z: 0.767)")]
        public void SetNarrowPreset()
        {
            enableTrackScale = true;
            scaleX = 0.80f;
            scaleZ = 0.767f;
            offsetX = 0f;
            offsetZ = -0.018f;
            heightOffset = 0f;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            NotifyChanges();
#endif
            Debug.Log("<color=green>[RoadTrackConfigData]</color> Đã đặt về Chuẩn Hẹp Hơn (X: 0.80)!");
        }

        [ContextMenu("⚡ Đặt Về Gốc Cocos 1:1 (Không Scale)")]
        public void ResetToRawCocos()
        {
            enableTrackScale = false;
            scaleX = 1.0f;
            scaleZ = 1.0f;
            offsetX = 0f;
            offsetZ = 0f;
            heightOffset = 0f;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            NotifyChanges();
#endif
            Debug.Log("<color=green>[RoadTrackConfigData]</color> Đã đặt về Toạ Độ Gốc Cocos 1:1!");
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            NotifyChanges();
        }

        public void NotifyChanges()
        {
            var loopTrack = Object.FindFirstObjectByType<WhoGetInBus.GamePlay.LoopTrackSystem>();
            if (loopTrack != null)
            {
                if (loopTrack.trackConfig == null) loopTrack.trackConfig = this;
                loopTrack.SyncFromConfig();
                EditorUtility.SetDirty(loopTrack.gameObject);
            }

            // RoadConfigGizmoViewer nằm trong assembly Editor nên không tham chiếu trực tiếp được: báo qua event
            ConfigChanged?.Invoke(this);

            SceneView.RepaintAll();
        }
#endif
    }
}
