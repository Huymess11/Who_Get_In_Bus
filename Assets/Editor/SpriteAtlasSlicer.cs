#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace WhoGetInBus.Editor
{
    public static class SpriteAtlasSlicer
    {
        private const string TexturesDir = "Assets/REF/Textures";
        private const string OutputDir = "Assets/REF/Sliced_Sprites";

        [MenuItem("Tools/Slice All Textures and Export PNGs")]
        public static void SliceAll()
        {
            try
            {
                AssetDatabase.StartAssetEditing();
                ProcessAllTextures();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SpriteAtlasSlicer] Error slicing textures: {ex}");
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }
        }

        private static void ProcessAllTextures()
        {
            if (!Directory.Exists(OutputDir))
            {
                Directory.CreateDirectory(OutputDir);
            }

            var pngFiles = Directory.GetFiles(TexturesDir, "*.png");
            int slicedCount = 0;

            foreach (var filePath in pngFiles)
            {
                string assetPath = filePath.Replace('\\', '/');
                string fileName = Path.GetFileName(assetPath);

                // Specific known atlases with custom layouts
                if (fileName == "179083717197239.png")
                {
                    SliceEmojiAndPassengerAtlas(assetPath);
                    slicedCount++;
                }
                else if (fileName == "179083717146338.png")
                {
                    SliceDigitAtlas(assetPath);
                    slicedCount++;
                }
                else if (fileName == "1790837211909117.png")
                {
                    SliceComboAtlas(assetPath);
                    slicedCount++;
                }
                else if (fileName == "179083719488184.png")
                {
                    SliceSlotCardAtlas(assetPath);
                    slicedCount++;
                }
                else
                {
                    // Auto-slice other packed atlases
                    if (AutoSliceTexture(assetPath))
                    {
                        slicedCount++;
                    }
                }
            }

            Debug.Log($"[SpriteAtlasSlicer] Finished processing! Sliced {slicedCount} atlases into sprites & exported PNGs to {OutputDir}.");
        }

        #region Custom Layout Slicers

        /// <summary>
        /// 179083717197239.png (510x329):
        /// - 10 Emojis in 2 rows of 5 (W=102, H=100)
        /// - 18 Passengers matching the 18 bus colors (Row 3 has 10, Row 4 has 8, W=48, H=64)
        /// </summary>
        private static void SliceEmojiAndPassengerAtlas(string assetPath)
        {
            var texture = LoadReadableTexture(assetPath);
            if (texture == null) return;

            int w = texture.width; // 510
            int h = texture.height; // 329

            var rects = new List<SpriteRect>();

            // Row 0 of Emojis (Top row in image -> highest Y in Unity space)
            // Top Y: 0..100 -> Unity Y: 329 - 100 = 229
            for (int i = 0; i < 5; i++)
            {
                rects.Add(CreateSpriteRect(
                    $"Emoji_{i}",
                    new Rect(i * 102, 229, 102, 100)
                ));
            }

            // Row 1 of Emojis (Second row in image)
            // Top Y: 101..200 -> Unity Y: 329 - 201 = 128
            for (int i = 0; i < 5; i++)
            {
                rects.Add(CreateSpriteRect(
                    $"Emoji_{i + 5}",
                    new Rect(i * 102, 128, 102, 100)
                ));
            }

            // Passenger color names in order matching the 18 bus materials
            string[] passengerColors = new string[]
            {
                "white",          // 0
                "green",          // 1
                "cyan",           // 2
                "blue",           // 3
                "brown",          // 4
                "purple",         // 5
                "powder",         // 6
                "yellow",         // 7
                "red",            // 8
                "emerald_green",  // 9
                "orange",         // 10
                "black",          // 11
                "dark_green",     // 12
                "burgundy",       // 13
                "grayish_blue",   // 14
                "light_purple",   // 15
                "pink",           // 16
                "teal"            // 17
            };

            // Row 2: 10 Passengers (indices 0..9)
            // Top Y: 201..264 -> Unity Y: 329 - 265 = 64
            for (int i = 0; i < 10; i++)
            {
                rects.Add(CreateSpriteRect(
                    $"Passenger_{i:D2}_{passengerColors[i]}",
                    new Rect(i * 48, 64, 48, 64)
                ));
            }

            // Row 3: 8 Passengers (indices 10..17)
            // Top Y: 265..328 -> Unity Y: 329 - 329 = 0
            for (int i = 0; i < 8; i++)
            {
                int idx = i + 10;
                rects.Add(CreateSpriteRect(
                    $"Passenger_{idx:D2}_{passengerColors[idx]}",
                    new Rect(i * 48, 0, 48, 64)
                ));
            }

            ApplySpriteRects(assetPath, rects);
            ExportSpritePNGs(assetPath, texture, rects);
            UnityEngine.Object.DestroyImmediate(texture);
        }

        /// <summary>
        /// 179083717146338.png (370x37): 10 Digits (0 to 9) in a 37x37 grid
        /// </summary>
        private static void SliceDigitAtlas(string assetPath)
        {
            var texture = LoadReadableTexture(assetPath);
            if (texture == null) return;

            var rects = new List<SpriteRect>();
            for (int i = 0; i < 10; i++)
            {
                rects.Add(CreateSpriteRect(
                    $"Digit_{i}",
                    new Rect(i * 37, 0, 37, 37)
                ));
            }

            ApplySpriteRects(assetPath, rects);
            ExportSpritePNGs(assetPath, texture, rects);
            UnityEngine.Object.DestroyImmediate(texture);
        }

        /// <summary>
        /// 1790837211909117.png (540x57): 10 Stylized Combo numbers (0 to 9) in a 54x57 grid
        /// </summary>
        private static void SliceComboAtlas(string assetPath)
        {
            var texture = LoadReadableTexture(assetPath);
            if (texture == null) return;

            var rects = new List<SpriteRect>();
            for (int i = 0; i < 10; i++)
            {
                rects.Add(CreateSpriteRect(
                    $"Combo_{i}",
                    new Rect(i * 54, 0, 54, 57)
                ));
            }

            ApplySpriteRects(assetPath, rects);
            ExportSpritePNGs(assetPath, texture, rects);
            UnityEngine.Object.DestroyImmediate(texture);
        }

        /// <summary>
        /// 179083719488184.png (256x384):
        /// - 6 color card/slot backgrounds in 2 cols x 3 rows (W=128, H=128)
        /// - Row 0 (Top): Yellow (left), Blue (right)
        /// - Row 1 (Mid): Cyan (left), Orange (right)
        /// - Row 2 (Bot): Powder/Beige (left), Powder/Beige (right)
        /// </summary>
        private static void SliceSlotCardAtlas(string assetPath)
        {
            var texture = LoadReadableTexture(assetPath);
            if (texture == null) return;

            var rects = new List<SpriteRect>();
            int idx = 0;
            // Unity Y coords: Top row = 256, Mid row = 128, Bot row = 0
            for (int r = 2; r >= 0; r--)
            {
                for (int c = 0; c < 2; c++)
                {
                    rects.Add(CreateSpriteRect(
                        $"{Path.GetFileNameWithoutExtension(assetPath)}_{idx}",
                        new Rect(c * 128, r * 128, 128, 128)
                    ));
                    idx++;
                }
            }

            ApplySpriteRects(assetPath, rects);
            ExportSpritePNGs(assetPath, texture, rects);
            UnityEngine.Object.DestroyImmediate(texture);
        }

        #endregion

        #region Auto Slicing Algorithm

        private static bool AutoSliceTexture(string assetPath)
        {
            var texture = LoadReadableTexture(assetPath);
            if (texture == null) return false;

            int w = texture.width;
            int h = texture.height;
            if (w < 20 || h < 20)
            {
                UnityEngine.Object.DestroyImmediate(texture);
                return false;
            }

            // Get pixels and find opaque regions (alpha > 15)
            Color32[] pixels = texture.GetPixels32();
            bool[,] opaque = new bool[w, h];
            int opaqueCount = 0;

            for (int y = 0; y < h; y++)
            {
                int rowOffset = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (pixels[rowOffset + x].a > 15)
                    {
                        opaque[x, y] = true;
                        opaqueCount++;
                    }
                }
            }

            // Skip fully opaque or fully transparent images
            if (opaqueCount == 0 || opaqueCount >= (w * h * 0.98f))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                return false;
            }

            // BFS connected components
            bool[,] visited = new bool[w, h];
            var rawBoxes = new List<RectInt>();
            int[] dx = { 0, 0, 1, -1, 1, -1, 1, -1 };
            int[] dy = { 1, -1, 0, 0, 1, 1, -1, -1 };

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (opaque[x, y] && !visited[x, y])
                    {
                        int minX = x, maxX = x, minY = y, maxY = y;
                        int pixelCount = 0;
                        var queue = new Queue<Vector2Int>();
                        queue.Enqueue(new Vector2Int(x, y));
                        visited[x, y] = true;

                        while (queue.Count > 0)
                        {
                            var pt = queue.Dequeue();
                            pixelCount++;
                            if (pt.x < minX) minX = pt.x;
                            if (pt.x > maxX) maxX = pt.x;
                            if (pt.y < minY) minY = pt.y;
                            if (pt.y > maxY) maxY = pt.y;

                            for (int d = 0; d < 8; d++)
                            {
                                int nx = pt.x + dx[d];
                                int ny = pt.y + dy[d];
                                if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                                {
                                    if (opaque[nx, ny] && !visited[nx, ny])
                                    {
                                        visited[nx, ny] = true;
                                        queue.Enqueue(new Vector2Int(nx, ny));
                                    }
                                }
                            }
                        }

                        int bw = maxX - minX + 1;
                        int bh = maxY - minY + 1;
                        if (bw >= 8 && bh >= 8 && pixelCount >= 30)
                        {
                            rawBoxes.Add(new RectInt(minX, minY, bw, bh));
                        }
                    }
                }
            }

            // Merge nearby boxes (within 4 pixels gap)
            bool merged = true;
            while (merged)
            {
                merged = false;
                for (int i = 0; i < rawBoxes.Count; i++)
                {
                    for (int j = i + 1; j < rawBoxes.Count; j++)
                    {
                        var r1 = rawBoxes[i];
                        var r2 = rawBoxes[j];
                        var expanded = new RectInt(r1.x - 4, r1.y - 4, r1.width + 8, r1.height + 8);
                        if (expanded.Overlaps(r2))
                        {
                            int nx = Mathf.Min(r1.x, r2.x);
                            int ny = Mathf.Min(r1.y, r2.y);
                            int nr = Mathf.Max(r1.xMax, r2.xMax);
                            int nb = Mathf.Max(r1.yMax, r2.yMax);
                            rawBoxes[i] = new RectInt(nx, ny, nr - nx, nb - ny);
                            rawBoxes.RemoveAt(j);
                            merged = true;
                            break;
                        }
                    }
                    if (merged) break;
                }
            }

            if (rawBoxes.Count <= 1)
            {
                UnityEngine.Object.DestroyImmediate(texture);
                return false;
            }

            // Sort top-to-bottom (highest Unity Y first), then left-to-right
            rawBoxes.Sort((a, b) =>
            {
                if (Mathf.Abs(a.y - b.y) > 16) return b.y.CompareTo(a.y); // top first
                return a.x.CompareTo(b.x);
            });

            string baseName = Path.GetFileNameWithoutExtension(assetPath);
            var rects = new List<SpriteRect>();
            for (int i = 0; i < rawBoxes.Count; i++)
            {
                var b = rawBoxes[i];
                rects.Add(CreateSpriteRect(
                    $"{baseName}_{i}",
                    new Rect(b.x, b.y, b.width, b.height)
                ));
            }

            ApplySpriteRects(assetPath, rects);
            ExportSpritePNGs(assetPath, texture, rects);
            UnityEngine.Object.DestroyImmediate(texture);
            return true;
        }

        #endregion

        #region Helpers

        private static SpriteRect CreateSpriteRect(string name, Rect rect)
        {
            return new SpriteRect
            {
                name = name,
                rect = rect,
                pivot = new Vector2(0.5f, 0.5f),
                alignment = SpriteAlignment.Center,
                spriteID = GUID.Generate()
            };
        }

        private static Texture2D LoadReadableTexture(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return null;

            bool originalReadable = importer.isReadable;
            var originalCompression = importer.textureCompression;

            if (!originalReadable || originalCompression != TextureImporterCompression.Uncompressed)
            {
                importer.isReadable = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static void ApplySpriteRects(string assetPath, List<SpriteRect> spriteRects)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.isReadable = true;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);

            if (dataProvider != null)
            {
                dataProvider.InitSpriteEditorDataProvider();
                dataProvider.SetSpriteRects(spriteRects.ToArray());
                dataProvider.Apply();
            }
            else
            {
                var metaList = new List<SpriteMetaData>();
                foreach (var sr in spriteRects)
                {
                    metaList.Add(new SpriteMetaData
                    {
                        name = sr.name,
                        rect = sr.rect,
                        pivot = sr.pivot,
                        alignment = (int)sr.alignment
                    });
                }
#pragma warning disable CS0618
                importer.spritesheet = metaList.ToArray();
#pragma warning restore CS0618
            }

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        private static void ExportSpritePNGs(string assetPath, Texture2D texture, List<SpriteRect> rects)
        {
            string baseName = Path.GetFileNameWithoutExtension(assetPath);
            string targetFolder = Path.Combine(OutputDir, baseName).Replace('\\', '/');

            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            foreach (var sr in rects)
            {
                int rx = Mathf.RoundToInt(sr.rect.x);
                int ry = Mathf.RoundToInt(sr.rect.y);
                int rw = Mathf.RoundToInt(sr.rect.width);
                int rh = Mathf.RoundToInt(sr.rect.height);

                if (rw <= 0 || rh <= 0) continue;

                Texture2D cropped = new Texture2D(rw, rh, TextureFormat.RGBA32, false);
                Color[] pixels = texture.GetPixels(rx, ry, rw, rh);
                cropped.SetPixels(pixels);
                cropped.Apply();

                byte[] pngBytes = cropped.EncodeToPNG();
                string filePath = Path.Combine(targetFolder, $"{sr.name}.png");
                File.WriteAllBytes(filePath, pngBytes);

                UnityEngine.Object.DestroyImmediate(cropped);
            }
        }

        #endregion
    }
}
#endif
