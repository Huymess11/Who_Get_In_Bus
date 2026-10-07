#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DouyinGame.Editor
{
    public static class CreateBusMaterials
    {
        private struct ColorConfig
        {
            public string name;
            public Color mainColor;
            public Color shadeColor;
            public Color outlineColor;

            public ColorConfig(string name, Color mainColor, Color shadeColor, Color outlineColor)
            {
                this.name = name;
                this.mainColor = mainColor;
                this.shadeColor = shadeColor;
                this.outlineColor = outlineColor;
            }
        }

        private static readonly ColorConfig[] Colors = new ColorConfig[]
        {
            // 0: white (Trắng)
            new ColorConfig("white",
                new Color(255f / 255f, 255f / 255f, 255f / 255f, 1f),
                new Color(202f / 255f, 219f / 255f, 235f / 255f, 1f),
                new Color(255f / 255f, 255f / 255f, 255f / 255f, 1f)),

            // 1: green (Xanh lá)
            new ColorConfig("green",
                new Color(48f / 255f, 209f / 255f, 131f / 255f, 1f),
                new Color(48f / 255f, 209f / 255f, 131f / 255f, 1f),
                new Color(0f / 255f, 122f / 255f, 63f / 255f, 1f)),

            // 2: cyan (Xanh lơ)
            new ColorConfig("cyan",
                new Color(0f / 255f, 224f / 255f, 255f / 255f, 1f),
                new Color(0f / 255f, 224f / 255f, 255f / 255f, 1f),
                new Color(0f / 255f, 121f / 255f, 151f / 255f, 1f)),

            // 3: blue (Xanh dương)
            new ColorConfig("blue",
                new Color(8f / 255f, 147f / 255f, 255f / 255f, 1f),
                new Color(8f / 255f, 147f / 255f, 255f / 255f, 1f),
                new Color(0f / 255f, 52f / 255f, 119f / 255f, 1f)),

            // 4: brown (Nâu)
            new ColorConfig("brown",
                new Color(134f / 255f, 70f / 255f, 28f / 255f, 1f),
                new Color(131f / 255f, 47f / 255f, 27f / 255f, 1f),
                new Color(70f / 255f, 15f / 255f, 0f / 255f, 1f)),

            // 5: purple (Tím)
            new ColorConfig("purple",
                new Color(191f / 255f, 78f / 255f, 238f / 255f, 1f),
                new Color(151f / 255f, 57f / 255f, 238f / 255f, 1f),
                new Color(57f / 255f, 0f / 255f, 117f / 255f, 1f)),

            // 6: powder (Be hồng)
            new ColorConfig("powder",
                new Color(247f / 255f, 211f / 255f, 189f / 255f, 1f),
                new Color(247f / 255f, 211f / 255f, 189f / 255f, 1f),
                new Color(119f / 255f, 46f / 255f, 0f / 255f, 1f)),

            // 7: yellow (Vàng)
            new ColorConfig("yellow",
                new Color(250f / 255f, 225f / 255f, 42f / 255f, 1f),
                new Color(255f / 255f, 214f / 255f, 49f / 255f, 1f),
                new Color(114f / 255f, 65f / 255f, 0f / 255f, 1f)),

            // 8: red (Đỏ)
            new ColorConfig("red",
                new Color(250f / 255f, 60f / 255f, 60f / 255f, 1f),
                new Color(241f / 255f, 26f / 255f, 26f / 255f, 1f),
                new Color(145f / 255f, 1f / 255f, 8f / 255f, 1f)),

            // 9: emerald_green (Nõn chuối)
            new ColorConfig("emerald_green",
                new Color(153f / 255f, 233f / 255f, 33f / 255f, 1f),
                new Color(153f / 255f, 233f / 255f, 33f / 255f, 1f),
                new Color(54f / 255f, 126f / 255f, 0f / 255f, 1f)),

            // 10: orange (Cam)
            new ColorConfig("orange",
                new Color(255f / 255f, 143f / 255f, 0f / 255f, 1f),
                new Color(255f / 255f, 112f / 255f, 0f / 255f, 1f),
                new Color(138f / 255f, 44f / 255f, 0f / 255f, 1f)),

            // 11: black (Đen xám)
            new ColorConfig("black",
                new Color(53f / 255f, 53f / 255f, 53f / 255f, 1f),
                new Color(45f / 255f, 45f / 255f, 45f / 255f, 1f),
                new Color(45f / 255f, 45f / 255f, 45f / 255f, 1f)),

            // 12: dark_green (Xanh lục đậm)
            new ColorConfig("dark_green",
                new Color(13f / 255f, 168f / 255f, 0f / 255f, 1f),
                new Color(13f / 255f, 168f / 255f, 0f / 255f, 1f),
                new Color(0f / 255f, 65f / 255f, 9f / 255f, 1f)),

            // 13: burgundy (Đỏ mận)
            new ColorConfig("burgundy",
                new Color(185f / 255f, 4f / 255f, 94f / 255f, 1f),
                new Color(185f / 255f, 4f / 255f, 94f / 255f, 1f),
                new Color(99f / 255f, 0f / 255f, 49f / 255f, 1f)),

            // 14: grayish_blue (Xanh xám)
            new ColorConfig("grayish_blue",
                new Color(134f / 255f, 137f / 255f, 209f / 255f, 1f),
                new Color(134f / 255f, 137f / 255f, 209f / 255f, 1f),
                new Color(5f / 255f, 11f / 255f, 117f / 255f, 1f)),

            // 15: light_purple (Tím nhạt)
            new ColorConfig("light_purple",
                new Color(195f / 255f, 192f / 255f, 247f / 255f, 1f),
                new Color(195f / 255f, 192f / 255f, 247f / 255f, 1f),
                new Color(13f / 255f, 6f / 255f, 143f / 255f, 1f)),

            // 16: pink (Hồng phấn)
            new ColorConfig("pink",
                new Color(252f / 255f, 106f / 255f, 234f / 255f, 1f),
                new Color(243f / 255f, 102f / 255f, 226f / 255f, 1f),
                new Color(156f / 255f, 1f / 255f, 73f / 255f, 1f)),

            // 17: teal (Xanh ngọc)
            new ColorConfig("teal",
                new Color(186f / 255f, 245f / 255f, 217f / 255f, 1f),
                new Color(153f / 255f, 231f / 195f, 195f / 255f, 1f),
                new Color(0f / 255f, 95f / 255f, 51f / 255f, 1f))
        };

        // Đã gỡ bỏ tự động sinh trên load. Chỉ chạy khi người dùng bấm Menu: Tools/Generate Bus Materials

        [MenuItem("Tools/Generate Bus Materials")]
        public static void GenerateAll()
        {
            GenerateInFolder("Assets/Material/Bus");
            GenerateInFolder("Assets/Material/nbuss");
        }

        private static void GenerateInFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // Tìm shader: ưu tiên Toony Colors Pro 2/Hybrid Shader 2 (Outline)
            Shader shader = Shader.Find("Toony Colors Pro 2/Hybrid Shader 2 (Outline)");
            if (shader == null)
            {
                shader = Shader.Find("Toony Colors Pro 2/Hybrid Shader 2");
            }

            if (shader == null)
            {
                Debug.LogError("[CreateBusMaterials] Không tìm thấy Shader 'Toony Colors Pro 2/Hybrid Shader 2'!");
                return;
            }

            foreach (var cfg in Colors)
            {
                string matPath = $"{folderPath}/{cfg.name}.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                bool isNew = (mat == null);
                if (isNew)
                {
                    mat = new Material(shader);
                }
                else
                {
                    mat.shader = shader;
                }

                mat.name = cfg.name;
                mat.SetColor("_BaseColor", cfg.mainColor);
                mat.SetColor("_Color", cfg.mainColor);
                mat.SetColor("_HColor", Color.white);
                mat.SetColor("_SColor", cfg.shadeColor);
                mat.SetFloat("_UseOutline", 1f);
                mat.SetColor("_OutlineColor", cfg.outlineColor);
                mat.SetFloat("_OutlineWidth", 0.5f);
                mat.SetFloat("_RampThreshold", 0.75f);
                mat.SetFloat("_RampSmoothing", 0.1f);
                mat.SetFloat("_ShadowColorLightAtten", 1f);

                if (isNew)
                {
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                else
                {
                    EditorUtility.SetDirty(mat);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CreateBusMaterials] Đã tạo thành công 18 materials trong {folderPath}!");
        }
    }
}
#endif
