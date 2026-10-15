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

    public bool keepTopRoadProportion = false;

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
            if (keepTopRoadProportion && topSp.rect.width > 0f)
            {
                var rt = TopRoad.rectTransform;
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, rt.sizeDelta.x * topSp.rect.height / topSp.rect.width);
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
