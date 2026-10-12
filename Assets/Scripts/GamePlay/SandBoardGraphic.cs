using UnityEngine;
using UnityEngine.UI;

namespace WhoGetInBus.GamePlay
{
    /// <summary>
    /// Component UGUI chuyên trách vẽ toàn bộ Tranh Cát (Sandboard Passenger) bằng 1 Draw Call duy nhất.
    /// Kế thừa MaskableGraphic theo đúng kiến trúc Cocos Creator gốc (PassengerBoardRenderer).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    [ExecuteAlways]
    public class SandBoardGraphic : MaskableGraphic
    {
        [Header("Atlas Texture (18 Màu Khách)")]
        [Tooltip("Texture atlas ghép 18 màu của hạt khách (tự động nạp PassengerAtlas_18)")]
        public Texture2D passengerAtlas;

        [Header("Tham Chiếu Manager")]
        public SandBoardManager boardManager;

        public override Texture mainTexture
        {
            get
            {
                if (passengerAtlas != null) return passengerAtlas;
                if (boardManager != null)
                {
                    if (boardManager.config != null && boardManager.config.passengerAtlas != null) return boardManager.config.passengerAtlas;
                    if (boardManager.passengerAtlas != null) return boardManager.passengerAtlas;
                }
                return s_WhiteTexture;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (boardManager == null)
            {
                boardManager = GetComponent<SandBoardManager>();
            }
            EnsureAtlas();
        }

        public void EnsureAtlas()
        {
            if (passengerAtlas == null)
            {
#if UNITY_EDITOR
                passengerAtlas = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprite/PassengerAtlas_18.png");
#else
                passengerAtlas = Resources.Load<Texture2D>("PassengerAtlas_18");
#endif
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if (boardManager == null)
            {
                boardManager = GetComponent<SandBoardManager>();
            }
            if (boardManager == null) return;

            int cols = boardManager.config != null ? boardManager.config.columns : boardManager.columns;
            int rows = boardManager.config != null ? boardManager.config.rows : boardManager.rows;
            var colorIndices = boardManager.BeadColorIndices;
            if (colorIndices == null) return;

            Vector2 bSize = boardManager.config != null ? boardManager.config.beadSize : boardManager.beadSize;
            float bScale = boardManager.config != null ? boardManager.config.beadScale : boardManager.beadScale;
            float hw = bSize.x * 0.5f * bScale;
            float hh = bSize.y * 0.5f * bScale;

            const float invTotalColors = 1f / 18f;
            Color32 vColor = color; // Dùng vertex color của Graphic (mặc định white)

            // Sinh từ trên ảnh xuống (r = rows - 1 -> 0) và từ trái sang phải (c = 0 -> cols - 1)
            // để các hạt phía dưới/trước lấp lên trên các hạt phía trên/sau chuẩn Cocos Creator 1:1
            for (int r = rows - 1; r >= 0; r--)
            {
                for (int c = 0; c < cols; c++)
                {
                    int colorIdx = colorIndices[c, r];
                    if (colorIdx < 0 || colorIdx >= 18) continue;

                    Vector2 basePos = boardManager.GetBeadUIPosition(c, r);
                    Vector2 animOffset = boardManager.GetCollapseOffset(c, r);
                    Vector2 pos = basePos + animOffset;

                    // Tính tọa độ UV trong dải atlas 18 màu (864 x 62 px)
                    float u0 = colorIdx * invTotalColors;
                    float u1 = (colorIdx + 1) * invTotalColors;
                    float v0 = 0f;
                    float v1 = 1f;

                    int startIndex = vh.currentVertCount;

                    // 4 đỉnh Quad (hình chữ nhật hạt cát)
                    vh.AddVert(new Vector3(pos.x - hw, pos.y - hh, 0f), vColor, new Vector2(u0, v0)); // Bottom-Left
                    vh.AddVert(new Vector3(pos.x - hw, pos.y + hh, 0f), vColor, new Vector2(u0, v1)); // Top-Left
                    vh.AddVert(new Vector3(pos.x + hw, pos.y + hh, 0f), vColor, new Vector2(u1, v1)); // Top-Right
                    vh.AddVert(new Vector3(pos.x + hw, pos.y - hh, 0f), vColor, new Vector2(u1, v0)); // Bottom-Right

                    // 2 Tam giác ghép thành Quad
                    vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
                    vh.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
                }
            }
        }
    }
}
