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
        [LabelText("Màu nền sàn")]
        public Color floorColor = Color.white;

        [VerticalGroup("Info/Right")]
        [LabelText("Số lượng Top Road"), ReadOnly]
        [ShowInInspector]
        public int TopRoadCount => topRoads != null ? topRoads.Count : 0;

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
            if (topRoads != null && topRoads.TryGetValue(roadId, out Sprite sp))
            {
                return sp;
            }
            return null;
        }

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

        #region CÁC HÀM TIỆN ÍCH TRUY VẤN (GETTERS)

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
        [Button("⚡ TỰ ĐỘNG QUÉT & GẮN TOÀN BỘ ẢNH ĐƯỜNG (TOP & DOWN) THEO MÔI TRƯỜNG (1-CLICK)", ButtonSizes.Large), GUIColor(0.2f, 0.85f, 0.35f)]
        public void AutoAssignRoadSprites()
        {
            if (environments == null) environments = new Dictionary<LevelEnvironmentType, EnvironmentRoadSet>();
            environments.Clear();

            // Ánh xạ 5 thư mục chuẩn trong Assets/Sprite/UI/level
            var folderMapping = new Dictionary<LevelEnvironmentType, (string folder, string name, Color color)>()
            {
                { LevelEnvironmentType.Farm,   ("Assets/Sprite/UI/level/FARM",   "FARM (Đồng cỏ / Nông trại)", new Color(0.48f, 0.76f, 0.35f)) },
                { LevelEnvironmentType.City,   ("Assets/Sprite/UI/level/CITY",   "CITY (Đô thị / Nhà ga)",     new Color(0.55f, 0.58f, 0.65f)) },
                { LevelEnvironmentType.Snow,   ("Assets/Sprite/UI/level/SNOW",   "SNOW (Băng tuyết)",          new Color(0.75f, 0.88f, 0.98f)) },
                { LevelEnvironmentType.Sea,    ("Assets/Sprite/UI/level/Sea",    "Sea (Biển / Đại dương)",     new Color(0.28f, 0.68f, 0.88f)) },
                { LevelEnvironmentType.Desert, ("Assets/Sprite/UI/level/DESERT", "DESERT (Sa mạc)",           new Color(0.92f, 0.74f, 0.45f)) }
            };

            int totalDownLoaded = 0;
            int totalTopLoaded = 0;
            int totalOverlaysLoaded = 0;

            foreach (var kvp in folderMapping)
            {
                LevelEnvironmentType env = kvp.Key;
                string folder = kvp.Value.folder;
                string envName = kvp.Value.name;
                Color floorColor = kvp.Value.color;

                var roadSet = new EnvironmentRoadSet
                {
                    environmentName = envName,
                    floorColor = floorColor
                };

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
                    // 3. Quét Top Roads chính: roadTop1, roadTop5_05, roadTop13_04...
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
