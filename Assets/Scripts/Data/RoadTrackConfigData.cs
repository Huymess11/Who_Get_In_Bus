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
        /// <summary>
        /// Tỷ lệ chuẩn khớp game gốc (cả 2 trục như nhau). Suy ra từ:
        ///  - Cocos: toạ độ đường là world 1:1, camera ortho 9.2 ⇔ 667px UI (72.5px UI / đơn vị), ảnh roadTop neo đáy tại y = -118px.
        ///  - Unity: UI dựng ×1.4587 so với Cocos (khung Top rộng 972.95 ứng với khung 667px của Cocos), đáy Top tại y = -150,
        ///    camera ortho 9.2 ⇔ 1080 đơn vị UI (117.39 / đơn vị world) với Canvas 1080x2160 khớp chiều rộng.
        ///  → scale = 1.4587 × 72.5 / 117.39 = 0.9009; offsetZ = +0.1478 (đưa đáy ảnh Top về đúng chỗ).
        /// Chỉ đúng khi ảnh Top giữ nguyên tỉ lệ ảnh (EnvironmentController) — đổi bố cục UI thì phải tính lại.
        /// </summary>
        public const float OriginalScale = 0.9009f;
        public const float OriginalOffsetZ = 0.1478f;

        [Header("Bật/Tắt Co Giãn Đường (Track Scale)")]
        [Tooltip("Bật/tắt tự động co giãn toạ độ waypoint từ Cocos sang vừa khít với lòng đường Unity")]
        public bool enableTrackScale = true;

        [Header("Hiệu Chỉnh Tỷ Lệ Co Giãn (Scale)")]
        [Tooltip("Tỷ lệ trục X (Chuẩn game gốc: 0.9009 — xem OriginalScale; 1.0 là toạ độ gốc Cocos)")]
        [Range(0.5f, 1.3f)]
        public float scaleX = OriginalScale;

        [Tooltip("Tỷ lệ trục Z (Chuẩn game gốc: 0.9009, bằng trục X vì ảnh đường giữ đúng tỉ lệ)")]
        [Range(0.5f, 1.3f)]
        public float scaleZ = OriginalScale;

        [Header("Độ Lệch Dịch Tâm (Offset)")]
        [Tooltip("Dịch ngang trục X sang trái (-) hoặc sang phải (+)")]
        [Range(-2f, 2f)]
        public float offsetX = 0f;

        [Tooltip("Dịch dọc trục Z lên trên (+) hoặc xuống dưới (-). Chuẩn game gốc: +0.1478 (xem OriginalOffsetZ)")]
        [Range(-2f, 2f)]
        public float offsetZ = OriginalOffsetZ;

        [Header("Cao Độ Nâng Lên (Elevation / Height Offset)")]
        [Tooltip("Cao độ nâng các điểm waypoint lên trục Y để không bị chìm/z-fighting")]
        [Range(-0.5f, 0.5f)]
        public float heightOffset = 0f;

        /// <summary>Bắn ra khi thông số được chỉnh trong Inspector/Editor (các công cụ Editor đăng ký để đồng bộ lại).</summary>
        public static event System.Action<RoadTrackConfigData> ConfigChanged;

        public Vector2 TrackScale => new Vector2(scaleX, scaleZ);
        public Vector3 TrackOffset => new Vector3(offsetX, heightOffset, offsetZ);

        [ContextMenu("⚡ Đặt Về Chuẩn Game Gốc (Scale 0.9009, OffsetZ +0.1478)")]
        public void ResetToFitRoadDefault()
        {
            enableTrackScale = true;
            scaleX = OriginalScale;
            scaleZ = OriginalScale;
            offsetX = 0f;
            offsetZ = OriginalOffsetZ;
            heightOffset = 0f;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            NotifyChanges();
#endif
            Debug.Log($"<color=green>[RoadTrackConfigData]</color> Đã đặt về Chuẩn Game Gốc (Scale: {OriginalScale}, OffsetZ: {OriginalOffsetZ})!");
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
