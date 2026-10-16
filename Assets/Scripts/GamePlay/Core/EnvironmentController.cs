using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using WhoGetInBus.Data;

public class EnvironmentController : MonoBehaviour
{
    public RoadEnvironmentData Data;
    public Image TopRoad;
    public Image BottomRoad;

    public List<Image> TopRoadDetail;

    [Tooltip("Các ảnh cảnh trang trí; View[i] nhận RoadEnvironmentData.views[i] của môi trường")]
    public List<Image> View;
    [Tooltip("Banner phía trên (ding1..5)")]
    public Image TopBanner;
    [Tooltip("Image nền 'bg' — đổi màu theo môi trường")]
    public Image BG;
    [Tooltip("(Tuỳ chọn) Nền thanh dưới đáy — đổi sprite dik1..5. Để trống nếu dùng UI thanh đáy riêng")]
    public Image BottomBar;

    [Header("Chuẩn ảnh Top theo từng đường (Cocos: prefab road/{bgStyle}/roadTop{id})")]
    [Tooltip("Game gốc: mọi ảnh Top neo đáy cùng 1 điểm, cùng chiều rộng, chiều CAO = số pixel ảnh (ảnh mỗi đường cao khác nhau: road5 536, road21 467, road8 267...). " +
             "Bật để chiều cao khung Top = chiều cao ảnh × Pixel Scale Y; giữ nguyên chiều rộng và vị trí đáy đã căn trong scene")]
    public bool matchOriginalTopRoadHeight = true;
    [Tooltip("Số đơn vị UI cho 1 pixel ảnh theo chiều dọc. Mặc định 700/536: khung Top 700 đã căn khớp game gốc với ảnh road 5 cao 536px")]
    public float topRoadPixelScaleY = 700f / 536f;

    private static readonly Regex OverlayName = new Regex(@"^Overlay Road(\d+)\s+(\S+)", RegexOptions.Compiled);

    public void Apply(LevelData level)
    {
        if (level == null) return;
        Apply(level.bgStyle, level.road);
    }

    public void Apply(int bgStyle, int roadId)
    {
        if (Data == null)
        {
            return;
        }

        Sprite downSp = Data.GetDownRoad(bgStyle);
        Sprite topSp = Data.GetTopRoad(bgStyle, roadId);

        if (BottomRoad != null && downSp != null) BottomRoad.sprite = downSp;

        if (TopRoad != null && topSp != null)
        {
            TopRoad.sprite = topSp;
            if (matchOriginalTopRoadHeight)
            {
                // Ảnh Top nào cũng có phần nội dung sát đáy, rộng như nhau → chỉ cần cao theo số pixel ảnh, đáy giữ nguyên.
                // Riêng prefab có Sprite CUSTOM size thì Cocos ép ảnh theo khung (road 11: ảnh 583px hiển thị cao 544).
                var rt = TopRoad.rectTransform;
                float squash = OriginalTopSquashY.TryGetValue(roadId, out float s) ? s : 1f;
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, topSp.rect.height * topRoadPixelScaleY * squash);
            }
        }

        // Ảnh phụ: chỉ đổi sprite và bật/tắt, vị trí + kích thước do scene quyết định
        if (TopRoadDetail != null)
        {
            for (int i = 0; i < TopRoadDetail.Count; i++)
            {
                var img = TopRoadDetail[i];
                if (img == null) continue;

                var m = OverlayName.Match(img.gameObject.name);
                if (!m.Success)
                {
                    Debug.LogWarning($"[EnvironmentController] Tên '{img.gameObject.name}' sai định dạng 'Overlay Road{{roadId}} {{tênSprite}}', bỏ qua.");
                    continue;
                }

                bool match = int.Parse(m.Groups[1].Value) == roadId;
                img.gameObject.SetActive(match);
                if (!match) continue;

                Sprite sp = FindOverlaySprite(bgStyle, m.Groups[2].Value);
                if (sp == null)
                {
                    Debug.LogWarning($"[EnvironmentController] Không tìm thấy sprite '{m.Groups[2].Value}' (bgStyle {bgStyle}) trong extraOverlays của RoadEnvironmentData.");
                    continue;
                }

                img.sprite = sp;
                if (matchOriginalOverlayLayout) PlaceOverlayLikeOriginal(img, roadId, bgStyle, m.Groups[2].Value, sp);
            }
        }

        ApplyStyle(bgStyle);
    }

    #region ẢNH HẦM / CẦU THEO PREFAB GỐC (Cocos: con của road/{bgStyle}/roadTop{id})

    [Tooltip("Tự đặt vị trí + kích thước ảnh hầm/cầu đúng như prefab gốc của từng môi trường (tính từ đáy giữa ảnh Top). Tắt để giữ nguyên vị trí đặt tay trong scene")]
    public bool matchOriginalOverlayLayout = true;

    // Toạ độ tâm node con (pixel Cocos, tính từ đáy giữa ảnh Top) theo bgStyle 1..5 = FARM, CITY, SNOW, Sea, DESERT
    private static readonly Dictionary<string, Vector2[]> OriginalOverlayPos = new Dictionary<string, Vector2[]>
    {
        { "21:roadTop21_1", Same(new Vector2(281f, 320f)) },
        { "21:roadTop21_2", Same(new Vector2(-27f, 449f)) },
        { "22:roadTop21_3", new[] { new Vector2(-282.782f, 320f), new Vector2(-285.466f, 320f), new Vector2(-282.597f, 320f), new Vector2(-281.093f, 320f), new Vector2(-280.765f, 320f) } },
        { "22:roadTop21_4", new[] { new Vector2(0.996f, 449f), new Vector2(1.14f, 443.908f), new Vector2(-2.08f, 449f), new Vector2(0.688f, 449f), new Vector2(0.77f, 449f) } },
        { "12:roadTop12_1", new[] { new Vector2(-0.311f, 371.433f), new Vector2(0.988f, 364.735f), new Vector2(0.064f, 365.611f), new Vector2(-0.956f, 364.95f), new Vector2(-0.956f, 363.872f) } },
        { "11:roadTop11_1", Same(new Vector2(-0.956f, 448.34f)) },
    };

    // Ảnh bị Cocos cắt viền trong suốt lệch tâm (spriteFrame.offset, pixel, y hướng lên) — Unity vẽ cả ảnh nên phải bù lại
    private static readonly Dictionary<string, Vector2> OriginalFrameOffset = new Dictionary<string, Vector2>
    {
        { "roadTop21_1_03", new Vector2(2f, 0f) },
        { "roadTop21_2_02", new Vector2(0.5f, 0.5f) },
        { "roadTop21_3_02", new Vector2(0.5f, 0f) },
        { "roadTop21_3_04", new Vector2(2f, 0f) },
        { "roadTop21_4", new Vector2(0.5f, 0.5f) },
        { "roadTop21_4_05", new Vector2(-1f, 0.5f) },
    };

    // Prefab gốc dùng Sprite CUSTOM size khác kích thước ảnh: tỉ lệ ép chiều dọc (khung / nội dung ảnh). Các đường khác = 1
    private static readonly Dictionary<int, float> OriginalTopSquashY = new Dictionary<int, float>
    {
        { 11, 544f / 583f },
    };

    private static Vector2[] Same(Vector2 v) => new[] { v, v, v, v, v };

    /// <summary>
    /// Cocos: node con đặt tại (x, y) so với gốc prefab (neo đáy giữa), hiển thị ảnh đã cắt viền.
    /// Unity: tâm = đáy giữa ảnh Top + (x − offset) × tỷ lệ, kích thước = số pixel ảnh × tỷ lệ (tỷ lệ lấy từ khung Top hiện tại).
    /// </summary>
    private void PlaceOverlayLikeOriginal(Image img, int roadId, int bgStyle, string baseName, Sprite sp)
    {
        if (TopRoad == null || TopRoad.sprite == null) return;
        if (!OriginalOverlayPos.TryGetValue(roadId + ":" + baseName, out var table)) return;

        var topRt = TopRoad.rectTransform;
        Rect topPx = TopRoad.sprite.rect;
        if (topPx.width <= 0f || topPx.height <= 0f) return;

        // Toạ độ node con tính theo pixel prefab (không bị ép theo ảnh Top) → dùng tỉ lệ pixel chuẩn
        float sX = topRt.sizeDelta.x / topPx.width;
        float squash = matchOriginalTopRoadHeight && OriginalTopSquashY.TryGetValue(roadId, out float sq) ? sq : 1f;
        float sY = topRt.sizeDelta.y / (topPx.height * squash);
        Vector2 bottomCenter = topRt.anchoredPosition + new Vector2((0.5f - topRt.pivot.x) * topRt.sizeDelta.x, -topRt.pivot.y * topRt.sizeDelta.y);

        Vector2 p = table[Mathf.Clamp(bgStyle - 1, 0, table.Length - 1)];
        OriginalFrameOffset.TryGetValue(sp.name, out Vector2 off);

        var rt = img.rectTransform;
        rt.anchoredPosition = bottomCenter + new Vector2((p.x - off.x) * sX, (p.y - off.y) * sY);
        rt.sizeDelta = new Vector2(sp.rect.width * sX, sp.rect.height * sY);
    }

    #endregion

    /// <summary>
    /// Đổi Top Banner, các View, thanh đáy và màu nền theo môi trường (Cocos: GameUI bgSprites + bgStyleSprite.color).
    /// Chỉ đổi sprite / màu và bật tắt — vị trí, kích thước do scene quyết định.
    /// </summary>
    private void ApplyStyle(int bgStyle)
    {
        EnvironmentRoadSet set = Data.GetEnvironment(bgStyle);

        if (BG != null) BG.color = Data.GetBgColor(bgStyle);
        if (set == null) return;

        if (TopBanner != null && set.topBanner != null) TopBanner.sprite = set.topBanner;
        if (BottomBar != null && set.bottomBar != null) BottomBar.sprite = set.bottomBar;

        // View thứ i nhận ảnh thứ i; môi trường có ít ảnh hơn số View thì ẩn các View thừa.
        // Data chưa gắn ảnh View nào thì giữ nguyên scene.
        if (View != null && set.views != null && set.views.Count > 0)
        {
            for (int i = 0; i < View.Count; i++)
            {
                var img = View[i];
                if (img == null) continue;

                Sprite sp = i < set.views.Count ? set.views[i] : null;
                img.gameObject.SetActive(sp != null);
                if (sp != null) img.sprite = sp;
            }
        }
    }

    private Sprite FindOverlaySprite(int bgStyle, string baseName)
    {
        Sprite sp = FindInSet(Data.GetEnvironment(bgStyle), baseName);
        if (sp != null) return sp;

        if (Data.environments != null)
        {
            foreach (var kv in Data.environments)
            {
                sp = FindInSet(kv.Value, baseName);
                if (sp != null) return sp;
            }
        }
        return null;
    }

    private static Sprite FindInSet(EnvironmentRoadSet set, string baseName)
    {
        if (set == null || set.extraOverlays == null) return null;
        foreach (var kv in set.extraOverlays)
        {
            string key = kv.Key;
            if (kv.Value == null || string.IsNullOrEmpty(key)) continue;
            bool exact = key == baseName;
            bool withSuffix = key.Length == baseName.Length + 3 && key.StartsWith(baseName + "_0");
            if (exact || withSuffix) return kv.Value;
        }
        return null;
    }
}
