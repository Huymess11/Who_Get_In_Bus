using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WhoGetInBus.Data
{
    /// <summary>
    /// Bộ ảnh đường đua cho một Môi trường cụ thể (Down Road & các biến thể Top Road)
    /// </summary>
    [Serializable]
    public class EnvironmentRoadSet
    {
        [HorizontalGroup("Info", 85)]
        [PreviewField(80, ObjectFieldAlignment.Left), HideLabel]
        [Tooltip("Ảnh mặt đường dưới (Down Road / Road Bottom - gắn tại chân bãi xe)")]
        public Sprite downRoad;

        [VerticalGroup("Info/Right")]
        [LabelText("Tên môi trường"), ReadOnly]
        public string environmentName;

        [VerticalGroup("Info/Right")]
        [LabelText("Màu nền (bg)")]
        [Tooltip("Màu Image nền 'bg' của môi trường (Cocos: ROAD_BOTTOM_COLOR_DEFINE). Alpha = 0 → dùng màu gốc mặc định")]
        [UnityEngine.Serialization.FormerlySerializedAs("floorColor")]
        public Color bgColor = new Color(0f, 0f, 0f, 0f);

        [VerticalGroup("Info/Right")]
        [LabelText("Số lượng Top Road"), ReadOnly]
        [ShowInInspector]
        public int TopRoadCount => topRoads != null ? topRoads.Count : 0;

        [Header("🏞️ BANNER / VIEW / THANH ĐÁY THEO MÔI TRƯỜNG")]
        [PreviewField(60, ObjectFieldAlignment.Left)]
        [LabelText("Top Banner")]
        [Tooltip("Ảnh banner phía trên (Cocos: game/ding{bgStyle})")]
        public Sprite topBanner;

        [LabelText("Các ảnh View")]
        [Tooltip("Ảnh cảnh trang trí (cây, nhà...). Phần tử i gán vào View thứ i trong EnvironmentController.Views. View nào không có ảnh tương ứng sẽ bị ẩn")]
        public List<Sprite> views = new List<Sprite>();

        [PreviewField(40, ObjectFieldAlignment.Left)]
        [LabelText("Thanh đáy (tuỳ chọn)")]
        [Tooltip("Ảnh nền thanh dưới đáy (Cocos: game/dik{bgStyle}). Để trống = giữ ảnh hiện tại trong scene")]
        public Sprite bottomBar;

        [Header("🛣️ ẢNH ĐƯỜNG TRÊN THEO LOẠI ĐƯỜNG (TOP ROADS)")]
        [Tooltip("Bảng tra cứu: Key = Road ID (1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 12, 13, 21, 22, 31, 32...) -> Value = Sprite mặt đường trên")]
        [DictionaryDrawerSettings(KeyLabel = "Road ID", ValueLabel = "Sprite Top Road")]
        public Dictionary<int, Sprite> topRoads = new Dictionary<int, Sprite>();

        [Header("🌉 ẢNH LỚP PHỤ (CẦU VƯỢT / HẦM XUYÊN NÚI OVERLAYS)")]
        [Tooltip("Các layer đè như lan can cầu (roadTop12_1), nóc hầm (roadTop21_1...): Key = Tên Sprite -> Value = Sprite")]
        [DictionaryDrawerSettings(KeyLabel = "Tên Lớp Phụ", ValueLabel = "Sprite Overlay")]
        public Dictionary<string, Sprite> extraOverlays = new Dictionary<string, Sprite>();

        /// <summary>
        /// Lấy Sprite đường trên theo Road ID
        /// </summary>
        public Sprite GetTopRoad(int roadId)
        {
            if (topRoads != null && topRoads.TryGetValue(roadId, out Sprite sp) && sp != null)
            {
                return sp;
            }

            // Một số đường trong game gốc dùng ảnh mang tên khác (road 22 = roadTop21_5_xx, nằm trong extraOverlays do khớp mẫu tên lớp phụ)
            if (TopRoadAlias.TryGetValue(roadId, out string alias) && extraOverlays != null)
            {
                foreach (var kv in extraOverlays)
                {
                    string key = kv.Key;
                    if (kv.Value == null || string.IsNullOrEmpty(key)) continue;
                    if (key == alias || (key.Length == alias.Length + 3 && key.StartsWith(alias + "_0"))) return kv.Value;
                }
            }
            return null;
        }

        /// <summary>Road ID → tên gốc ảnh Top khi không theo mẫu roadTop{id} (Cocos: prefab road/{bgStyle}/roadTop22 dùng frame roadTop21_5_xx).</summary>
        private static readonly Dictionary<int, string> TopRoadAlias = new Dictionary<int, string>
        {
            { 22, "roadTop21_5" },
        };

        /// <summary>
        /// Lấy Sprite đường trên theo Enum RoadTopType
        /// </summary>
        public Sprite GetTopRoad(RoadTopType roadType)
        {
            return GetTopRoad((int)roadType);
        }

        /// <summary>
        /// Lấy Sprite lớp phủ overlay nếu có (cầu/hầm)
        /// </summary>
        public Sprite GetOverlay(string overlayName)
        {
            if (extraOverlays != null && extraOverlays.TryGetValue(overlayName, out Sprite sp))
            {
                return sp;
            }
            return null;
        }
    }

    /// <summary>
    /// DỮ LIỆU TÀI NGUYÊN MẶT ĐƯỜNG THEO MÔI TRƯỜNG (Road Environment Data)
    /// Quản lý tập trung toàn bộ ảnh Down Road và Top Road theo từng Theme/Environment của Level:
    /// 1. FARM (Mục trường / Nông trại) - bgStyle = 1
    /// 2. CITY (Đô thị / Nhà ga)        - bgStyle = 2
    /// 3. SNOW (Băng tuyết)             - bgStyle = 3
    /// 4. Sea (Biển / Đại dương)        - bgStyle = 4
    /// 5. DESERT (Sa mạc)               - bgStyle = 5
    /// </summary>
    [CreateAssetMenu(fileName = "RoadEnvironmentData", menuName = "STO/RoadEnvironmentData")]
    public class RoadEnvironmentData : SerializedScriptableObject
    {
        [Header("🌍 BẢNG DỮ LIỆU ĐƯỜNG THEO 5 MÔI TRƯỜNG")]
        [Tooltip("Ánh xạ từ LevelEnvironmentType sang bộ ảnh đường tương ứng (Down Road & Top Roads)")]
        [DictionaryDrawerSettings(KeyLabel = "Môi Trường Level (bgStyle)", ValueLabel = "Bộ Ảnh Đường")]
        public Dictionary<LevelEnvironmentType, EnvironmentRoadSet> environments = new Dictionary<LevelEnvironmentType, EnvironmentRoadSet>();

        /// <summary>Màu nền gốc theo bgStyle 1..5 (Cocos: ROAD_BOTTOM_COLOR_DEFINE = #8bc542, #d1c0ba, #eff3fa, #51bbf2, #ffdf9d).</summary>
        public static readonly Color32[] OriginalBgColors =
        {
            new Color32(0x8b, 0xc5, 0x42, 0xff),
            new Color32(0xd1, 0xc0, 0xba, 0xff),
            new Color32(0xef, 0xf3, 0xfa, 0xff),
            new Color32(0x51, 0xbb, 0xf2, 0xff),
            new Color32(0xff, 0xdf, 0x9d, 0xff),
        };

        #region CÁC HÀM TIỆN ÍCH TRUY VẤN (GETTERS)

        /// <summary>Màu nền 'bg' theo bgStyle: lấy từ data nếu đã đặt (alpha &gt; 0), ngược lại dùng màu gốc.</summary>
        public Color GetBgColor(int bgStyle)
        {
            var set = GetEnvironment(bgStyle);
            if (set != null && set.bgColor.a > 0f) return set.bgColor;
            int i = Mathf.Clamp(bgStyle - 1, 0, OriginalBgColors.Length - 1);
            return OriginalBgColors[i];
        }

        /// <summary>
        /// Lấy bộ dữ liệu đường theo Environment Type
        /// </summary>
        public EnvironmentRoadSet GetEnvironment(LevelEnvironmentType env)
        {
            if (environments != null && environments.TryGetValue(env, out var set))
            {
                return set;
            }
            return null;
        }

        /// <summary>
        /// Lấy bộ dữ liệu đường theo giá trị bgStyle (1..5) trong data level
        /// </summary>
        public EnvironmentRoadSet GetEnvironment(int bgStyle)
        {
            return GetEnvironment((LevelEnvironmentType)bgStyle);
        }

        /// <summary>
        /// Lấy ảnh mặt đường dưới (Down Road) theo loại môi trường
        /// </summary>
        public Sprite GetDownRoad(LevelEnvironmentType env)
        {
            var set = GetEnvironment(env);
            return set != null ? set.downRoad : null;
        }

        /// <summary>
        /// Lấy ảnh mặt đường dưới (Down Road) theo bgStyle
        /// </summary>
        public Sprite GetDownRoad(int bgStyle)
        {
            return GetDownRoad((LevelEnvironmentType)bgStyle);
        }

        /// <summary>
        /// Lấy ảnh mặt đường dưới (Down Road) theo RoadBottomStyle
        /// </summary>
        public Sprite GetDownRoad(RoadBottomStyle style)
        {
            return GetDownRoad((int)style);
        }

        /// <summary>
        /// Lấy ảnh đường trên (Top Road) theo môi trường và Road ID
        /// </summary>
        public Sprite GetTopRoad(LevelEnvironmentType env, int roadId)
        {
            var set = GetEnvironment(env);
            return set != null ? set.GetTopRoad(roadId) : null;
        }

        /// <summary>
        /// Lấy ảnh đường trên (Top Road) theo bgStyle và Road ID
        /// </summary>
        public Sprite GetTopRoad(int bgStyle, int roadId)
        {
            return GetTopRoad((LevelEnvironmentType)bgStyle, roadId);
        }

        /// <summary>
        /// Lấy ảnh đường trên (Top Road) theo môi trường và RoadTopType
        /// </summary>
        public Sprite GetTopRoad(LevelEnvironmentType env, RoadTopType roadType)
        {
            return GetTopRoad(env, (int)roadType);
        }

        /// <summary>
        /// Lấy ảnh đường trên (Top Road) theo bgStyle và RoadTopType
        /// </summary>
        public Sprite GetTopRoad(int bgStyle, RoadTopType roadType)
        {
            return GetTopRoad((LevelEnvironmentType)bgStyle, (int)roadType);
        }

        /// <summary>
        /// Lấy ảnh đường trên (Top Road) theo RoadBottomStyle và RoadTopType
        /// </summary>
        public Sprite GetTopRoad(RoadBottomStyle style, RoadTopType roadType)
        {
            return GetTopRoad((int)style, (int)roadType);
        }

        #endregion

        #region TỰ ĐỘNG QUÉT & GẮN RESOURCE TRONG EDITOR (1-CLICK)

#if UNITY_EDITOR
        private const string LevelSpriteFolder = "Assets/Sprite/UI/level";

        // 5 thư mục môi trường chuẩn trong Assets/Sprite/UI/level
        private static readonly Dictionary<LevelEnvironmentType, (string folder, string name)> EnvironmentFolders =
            new Dictionary<LevelEnvironmentType, (string folder, string name)>
            {
                { LevelEnvironmentType.Farm,   (LevelSpriteFolder + "/FARM",   "FARM (Đồng cỏ / Nông trại)") },
                { LevelEnvironmentType.City,   (LevelSpriteFolder + "/CITY",   "CITY (Đô thị / Nhà ga)") },
                { LevelEnvironmentType.Snow,   (LevelSpriteFolder + "/SNOW",   "SNOW (Băng tuyết)") },
                { LevelEnvironmentType.Sea,    (LevelSpriteFolder + "/Sea",    "Sea (Biển / Đại dương)") },
                { LevelEnvironmentType.Desert, (LevelSpriteFolder + "/DESERT", "DESERT (Sa mạc)") },
            };

        /// <summary>
        /// Gắn Top Banner (ding{n}), Thanh đáy (dik{n}), Màu nền gốc và các ảnh View (mọi png trong thư mục môi trường
        /// không phải roadTop*/roadBottom*, sắp theo tên) cho 1 môi trường.
        /// </summary>
        private static void FillStyleSprites(LevelEnvironmentType env, string folder, EnvironmentRoadSet set)
        {
            int style = (int)env;
            set.bgColor = OriginalBgColors[Mathf.Clamp(style - 1, 0, OriginalBgColors.Length - 1)];
            set.topBanner = AssetDatabase.LoadAssetAtPath<Sprite>($"{LevelSpriteFolder}/ding{style}.png");
            set.bottomBar = AssetDatabase.LoadAssetAtPath<Sprite>($"{LevelSpriteFolder}/dik{style}.png");

            if (set.views == null) set.views = new List<Sprite>();
            set.views.Clear();
            if (!Directory.Exists(folder)) return;

            var files = new List<string>(Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly));
            files.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string file in files)
            {
                string filename = Path.GetFileNameWithoutExtension(file);
                if (filename.StartsWith("roadTop", StringComparison.OrdinalIgnoreCase) ||
                    filename.StartsWith("roadBottom", StringComparison.OrdinalIgnoreCase)) continue;

                Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(file.Replace('\\', '/'));
                if (sp != null) set.views.Add(sp);
            }
        }

        [Button("🏞️ GẮN BANNER / VIEW / THANH ĐÁY / MÀU NỀN CHO 5 MÔI TRƯỜNG (giữ nguyên ảnh đường)", ButtonSizes.Large), GUIColor(0.35f, 0.7f, 1f)]
        public void AutoAssignStyleSprites()
        {
            if (environments == null) environments = new Dictionary<LevelEnvironmentType, EnvironmentRoadSet>();

            int banners = 0, views = 0;
            foreach (var kvp in EnvironmentFolders)
            {
                if (!environments.TryGetValue(kvp.Key, out var set) || set == null)
                {
                    set = new EnvironmentRoadSet { environmentName = kvp.Value.name };
                    environments[kvp.Key] = set;
                }
                FillStyleSprites(kvp.Key, kvp.Value.folder, set);
                if (set.topBanner != null) banners++;
                views += set.views.Count;
            }

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green><b>[RoadEnvironmentData]</b></color> Đã gắn {banners}/5 Top Banner, {views} ảnh View, thanh đáy và màu nền gốc.");
        }

        [Button("⚡ TỰ ĐỘNG QUÉT & GẮN TOÀN BỘ ẢNH ĐƯỜNG (TOP & DOWN) THEO MÔI TRƯỜNG (1-CLICK)", ButtonSizes.Large), GUIColor(0.2f, 0.85f, 0.35f)]
        public void AutoAssignRoadSprites()
        {
            if (environments == null) environments = new Dictionary<LevelEnvironmentType, EnvironmentRoadSet>();
            environments.Clear();

            // Ánh xạ 5 thư mục chuẩn trong Assets/Sprite/UI/level
            var folderMapping = EnvironmentFolders;

            int totalDownLoaded = 0;
            int totalTopLoaded = 0;
            int totalOverlaysLoaded = 0;

            foreach (var kvp in folderMapping)
            {
                LevelEnvironmentType env = kvp.Key;
                string folder = kvp.Value.folder;
                string envName = kvp.Value.name;
                var roadSet = new EnvironmentRoadSet { environmentName = envName };
                FillStyleSprites(env, folder, roadSet);

                if (!Directory.Exists(folder))
                {
                    Debug.LogWarning($"[RoadEnvironmentData] Không tìm thấy thư mục: {folder}");
                    environments[env] = roadSet;
                    continue;
                }

                string[] pngFiles = Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly);
                foreach (string file in pngFiles)
                {
                    string filename = Path.GetFileNameWithoutExtension(file);
                    string assetPath = file.Replace('\\', '/');

                    // 1. Quét Down Road (roadBottom)
                    if (filename.StartsWith("roadBottom", StringComparison.OrdinalIgnoreCase))
                    {
                        Sprite bottomSp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                        if (bottomSp != null)
                        {
                            roadSet.downRoad = bottomSp;
                            totalDownLoaded++;
                        }
                    }
                    // 2. Quét các lớp phụ Overlay (cầu/hầm): roadTop12_1, roadTop21_1_03...
                    else if (Regex.IsMatch(filename, @"^roadTop(\d+)_([1-9])(?:_(\d+))?$", RegexOptions.IgnoreCase))
                    {
                        Sprite overlaySp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                        if (overlaySp != null)
                        {
                            roadSet.extraOverlays[filename] = overlaySp;
                            totalOverlaysLoaded++;
                        }
                    }
                    // 3. Ảnh View (cảnh trang trí: a3_03, d1, za...) đã được FillStyleSprites gom riêng
                    else if (!filename.StartsWith("roadTop", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    // 4. Quét Top Roads chính: roadTop1, roadTop5_05, roadTop13_04...
                    else
                    {
                        var match = Regex.Match(filename, @"^roadTop(\d+)(?:_0\d)?$", RegexOptions.IgnoreCase);
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int roadId))
                        {
                            Sprite topSp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                            if (topSp != null)
                            {
                                roadSet.topRoads[roadId] = topSp;
                                totalTopLoaded++;
                            }
                        }
                    }
                }

                environments[env] = roadSet;
            }

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();

            Debug.Log($"<color=green><b>[RoadEnvironmentData]</b></color> Đã quét và nạp thành công <b>{environments.Count}/5</b> môi trường: " +
                      $"<b>{totalDownLoaded}</b> Down Roads, <b>{totalTopLoaded}</b> Top Roads, <b>{totalOverlaysLoaded}</b> Overlays!");
        }
#endif

        #endregion
    }
}
