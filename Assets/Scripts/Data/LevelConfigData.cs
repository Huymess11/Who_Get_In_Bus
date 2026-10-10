using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace WhoGetInBus.Data
{
    /// <summary>
    /// Dữ liệu cấu hình của 1 Level trong levelNCXHCfg.json (Chuẩn 1:1 Cocos Creator gốc)
    /// </summary>
    [System.Serializable]
    public class LevelData
    {
        public int id = 1001;
        public int next = 1002;
        public int collect = 1;
        public int exchangeRatio = 10;
        public int iniCapacity = 4;
        public int maxCapacity = 4;
        public int road = 5;
        public int bgStyle = 2;
        public int uiStyle = 1;
        public int weather = 0;
        public int bus_prompt = 1;
        public int diff_prompt = 0;
        public int bus_random = 0;

        /// <summary>
        /// Ma trận 2D xe buýt: bus[hàng][cột]
        /// Mỗi phần tử có định dạng: "{colorId}_{capacity}" hoặc "{colorId}_{capacity}_{mechanicType}"
        /// </summary>
        public List<List<string>> bus = new List<List<string>>();

        /// <summary>
        /// Danh sách màu hành khách xếp hàng rơi xuống bổ sung từ đỉnh bàn cát
        /// </summary>
        public List<int> queue = new List<int>();

        /// <summary>
        /// Danh sách xe trong nhà để xe (gara) nếu có
        /// </summary>
        public List<List<string>> carport = new List<List<string>>();

        public int RowCount => bus != null ? bus.Count : 0;
        public int ColCount => (bus != null && bus.Count > 0 && bus[0] != null) ? bus[0].Count : 0;
    }

    /// <summary>
    /// Cấu hình liên kết tranh cát trong collectNCXHCfg.json
    /// </summary>
    [System.Serializable]
    public class CollectData
    {
        public int id = 1;
        public int picture = 10006;
        public int unlockType = 1;
        public string name = "甜趣香蕉";
        public int unlockLevel = 1001;
    }

    /// <summary>
    /// Cấu hình đường chạy và vùng khoét rãnh trong roadNCXHCfg.json
    /// </summary>
    [System.Serializable]
    public class RoadConfigData
    {
        public int id = 5;
        public string loop_path_root = "";
        public string exit_path_root = "";
        public string cave_path = "";
        public float speed = 5f;
        public float max_speed = 7.2f;
        public float acc_speed = 0.1f;

        /// <summary>
        /// Ma trận 40x40 điểm: points[col, row]
        /// col = 0..39 (trục X: trái sang phải)
        /// row = 0..39 (trục Y: đỉnh trên cùng 0 xuống đáy 39)
        /// Giá trị -1 là vùng khoét rãnh đường đón xe (không sinh hạt cát)
        /// </summary>
        public int[,] points = new int[40, 40];

        public bool IsCutout(int col, int row)
        {
            if (col < 0 || col >= 40 || row < 0 || row >= 40) return false;
            return points != null && points[col, row] == -1;
        }
    }

    /// <summary>
    /// Dữ liệu ma trận tranh cát 40x40 trong Assets/Level/PixelMap/{picture}.json (Chuẩn Cocos gốc 1:1)
    /// </summary>
    [System.Serializable]
    public class PixelMapData
    {
        public int gridPoint = 5;
        public int gridSize = 40;
        public int showGrid = 1;
        public int hasGap = 1;
        public string bgPath = "";
        public string bgOpacity = "100";
        public int v = 2;

        /// <summary>
        /// Ma trận 40x40 điểm màu: points[col, row]
        /// col = 0..39 (trục X: từ trái sang phải)
        /// row = 0..39 (trục Y: từ đỉnh trên cùng 0 xuống đáy 39)
        /// Giá trị 0..17 là mã màu chuẩn, -1 là ô trống
        /// </summary>
        public int[,] points = new int[40, 40];
    }

    /// <summary>
    /// Bộ nạp và ghi cấu hình Level chuẩn gốc (LevelConfigLoader)
    /// Đọc trực tiếp từ:
    /// - Assets/GameConfig/levelNCXHCfg.json
    /// - Assets/GameConfig/roadNCXHCfg.json
    /// - Assets/GameConfig/collectNCXHCfg.json
    /// - Assets/Level/PixelMap/{picture}.json
    /// </summary>
    public static class LevelConfigLoader
    {
        public static string LevelCfgPath => Path.Combine(Application.dataPath, "GameConfig", "levelNCXHCfg.json");
        public static string RoadCfgPath => Path.Combine(Application.dataPath, "GameConfig", "roadNCXHCfg.json");
        public static string CollectCfgPath => Path.Combine(Application.dataPath, "GameConfig", "collectNCXHCfg.json");
        public static string PixelMapDir => Path.Combine(Application.dataPath, "Level", "PixelMap");

        private static List<int> cachedLevelIds = null;
        private static Dictionary<int, CollectData> cachedCollects = null;
        private static Dictionary<int, RoadConfigData> cachedRoads = null;

        /// <summary>
        /// Lấy toàn bộ danh sách Level ID có trong file levelNCXHCfg.json (1001, 1002, ...)
        /// </summary>
        public static List<int> GetAllLevelIds(bool forceReload = false)
        {
            if (cachedLevelIds != null && !forceReload) return cachedLevelIds;

            cachedLevelIds = new List<int>();
            if (!File.Exists(LevelCfgPath))
            {
                Debug.LogError($"[LevelConfigLoader] Không tìm thấy file: {LevelCfgPath}");
                return cachedLevelIds;
            }

            try
            {
                string json = File.ReadAllText(LevelCfgPath);
                var matches = Regex.Matches(json, @"""(\d{4,5})""\s*:\s*\{");
                foreach (Match m in matches)
                {
                    if (int.TryParse(m.Groups[1].Value, out int id))
                    {
                        if (!cachedLevelIds.Contains(id)) cachedLevelIds.Add(id);
                    }
                }
                cachedLevelIds.Sort();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LevelConfigLoader] Lỗi đọc danh sách Level ID: {ex.Message}");
            }

            return cachedLevelIds;
        }

        /// <summary>
        /// Nạp toàn bộ bảng collectNCXHCfg.json vào bộ nhớ cache
        /// </summary>
        public static Dictionary<int, CollectData> GetAllCollects(bool forceReload = false)
        {
            if (cachedCollects != null && !forceReload) return cachedCollects;

            cachedCollects = new Dictionary<int, CollectData>();
            if (!File.Exists(CollectCfgPath))
            {
                Debug.LogWarning($"[LevelConfigLoader] Không tìm thấy file: {CollectCfgPath}");
                return cachedCollects;
            }

            try
            {
                string json = File.ReadAllText(CollectCfgPath);
                var matches = Regex.Matches(json, @"""(\d+)""\s*:\s*\{([^}]*)\}");
                foreach (Match m in matches)
                {
                    if (int.TryParse(m.Groups[1].Value, out int cId))
                    {
                        string body = m.Groups[2].Value;
                        var data = new CollectData { id = cId };

                        var pMatch = Regex.Match(body, @"""picture""\s*:\s*(\d+)");
                        if (pMatch.Success) int.TryParse(pMatch.Groups[1].Value, out data.picture);

                        var nMatch = Regex.Match(body, @"""name""\s*:\s*""([^""]*)""");
                        if (nMatch.Success) data.name = nMatch.Groups[1].Value;

                        var uMatch = Regex.Match(body, @"""unlockLevel""\s*:\s*(\d+)");
                        if (uMatch.Success) int.TryParse(uMatch.Groups[1].Value, out data.unlockLevel);

                        var tMatch = Regex.Match(body, @"""unlockType""\s*:\s*(\d+)");
                        if (tMatch.Success) int.TryParse(tMatch.Groups[1].Value, out data.unlockType);

                        cachedCollects[cId] = data;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LevelConfigLoader] Lỗi đọc collectNCXHCfg.json: {ex.Message}");
            }

            return cachedCollects;
        }

        /// <summary>
        /// Lấy thông tin Collect theo ID (1, 2, ...)
        /// </summary>
        public static CollectData GetCollect(int collectId)
        {
            var dict = GetAllCollects();
            if (dict.TryGetValue(collectId, out var collect)) return collect;
            return null;
        }

        /// <summary>
        /// Tìm thông tin Collect theo Picture ID (ví dụ 10006, 10009, ...)
        /// </summary>
        public static CollectData GetCollectByPicture(int pictureId)
        {
            var dict = GetAllCollects();
            foreach (var kvp in dict.Values)
            {
                if (kvp.picture == pictureId) return kvp;
            }
            return null;
        }

        /// <summary>
        /// Nạp cấu hình tuyến đường RoadConfigData từ roadNCXHCfg.json
        /// </summary>
        public static RoadConfigData GetRoadConfig(int roadId, bool forceReload = false)
        {
            if (cachedRoads == null) cachedRoads = new Dictionary<int, RoadConfigData>();
            if (!forceReload && cachedRoads.TryGetValue(roadId, out var rData)) return rData;

            if (!File.Exists(RoadCfgPath))
            {
                Debug.LogWarning($"[LevelConfigLoader] Không tìm thấy file: {RoadCfgPath}");
                return null;
            }

            try
            {
                string json = File.ReadAllText(RoadCfgPath);
                string key = $"\"{roadId}\"";
                int keyIdx = json.IndexOf(key);
                if (keyIdx < 0)
                {
                    Debug.LogWarning($"[LevelConfigLoader] Không tìm thấy Tuyến Road #{roadId} trong roadNCXHCfg.json");
                    return null;
                }

                int openBrace = json.IndexOf('{', keyIdx);
                if (openBrace < 0) return null;
                int closeBrace = FindClosingBracket(json, openBrace, '{', '}');
                if (closeBrace < 0) return null;

                string block = json.Substring(openBrace, closeBrace - openBrace + 1);
                var road = new RoadConfigData { id = roadId };

                var spMatch = Regex.Match(block, @"""speed""\s*:\s*([0-9.]+)");
                if (spMatch.Success && float.TryParse(spMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float sp))
                {
                    road.speed = sp;
                }

                var lpMatch = Regex.Match(block, @"""loop_path_root""\s*:\s*""([^""]*)""");
                if (lpMatch.Success) road.loop_path_root = lpMatch.Groups[1].Value;

                var epMatch = Regex.Match(block, @"""exit_path_root""\s*:\s*""([^""]*)""");
                if (epMatch.Success) road.exit_path_root = epMatch.Groups[1].Value;

                int pointsIdx = block.IndexOf("\"points\"");
                if (pointsIdx >= 0)
                {
                    int arrStart = block.IndexOf('[', pointsIdx);
                    if (arrStart >= 0)
                    {
                        int arrEnd = FindClosingBracket(block, arrStart, '[', ']');
                        if (arrEnd >= 0)
                        {
                            string pointsBlock = block.Substring(arrStart + 1, arrEnd - arrStart - 1);
                            int cur = 0;
                            int col = 0;
                            while (cur < pointsBlock.Length && col < 40)
                            {
                                int rowStart = pointsBlock.IndexOf('[', cur);
                                if (rowStart < 0) break;
                                int rowEnd = FindClosingBracket(pointsBlock, rowStart, '[', ']');
                                if (rowEnd < 0) break;

                                string colBlock = pointsBlock.Substring(rowStart + 1, rowEnd - rowStart - 1);
                                string[] tokens = colBlock.Split(new[] { ',', '\r', '\n', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                int row = 0;
                                foreach (var t in tokens)
                                {
                                    if (row >= 40) break;
                                    if (int.TryParse(t.Trim(), out int val))
                                    {
                                        road.points[col, row] = val;
                                    }
                                    row++;
                                }
                                col++;
                                cur = rowEnd + 1;
                            }
                        }
                    }
                }

                cachedRoads[roadId] = road;
                return road;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LevelConfigLoader] Lỗi đọc Road #{roadId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Nạp thông tin Level cụ thể từ levelNCXHCfg.json
        /// </summary>
        public static LevelData LoadLevel(int levelId)
        {
            if (!File.Exists(LevelCfgPath))
            {
                Debug.LogError($"[LevelConfigLoader] Không tìm thấy file: {LevelCfgPath}");
                return null;
            }

            try
            {
                string json = File.ReadAllText(LevelCfgPath);
                string key = $"\"{levelId}\"";
                int keyIdx = json.IndexOf(key);
                if (keyIdx < 0)
                {
                    Debug.LogWarning($"[LevelConfigLoader] Không tìm thấy Level {levelId} trong levelNCXHCfg.json");
                    return null;
                }

                int openBrace = json.IndexOf('{', keyIdx);
                if (openBrace < 0) return null;
                int closeBrace = FindClosingBracket(json, openBrace, '{', '}');
                if (closeBrace < 0) return null;

                string levelBlock = json.Substring(openBrace, closeBrace - openBrace + 1);
                var lvl = new LevelData { id = levelId };

                // Parse các trường số nguyên
                lvl.next = ParseIntField(levelBlock, "next", levelId + 1);
                lvl.collect = ParseIntField(levelBlock, "collect", 1);
                lvl.exchangeRatio = ParseIntField(levelBlock, "exchangeRatio", 10);
                lvl.iniCapacity = ParseIntField(levelBlock, "iniCapacity", 4);
                lvl.maxCapacity = ParseIntField(levelBlock, "maxCapacity", 4);
                lvl.road = ParseIntField(levelBlock, "road", 5);
                lvl.bgStyle = ParseIntField(levelBlock, "bgStyle", 2);
                lvl.uiStyle = ParseIntField(levelBlock, "uiStyle", 1);
                lvl.weather = ParseIntField(levelBlock, "weather", 0);
                lvl.bus_prompt = ParseIntField(levelBlock, "bus_prompt", 1);
                lvl.diff_prompt = ParseIntField(levelBlock, "diff_prompt", 0);
                lvl.bus_random = ParseIntField(levelBlock, "bus_random", 0);

                // Parse mảng 2D "bus"
                lvl.bus = Parse2DStringArray(levelBlock, "bus");

                // Parse mảng "queue"
                lvl.queue = Parse1DIntArray(levelBlock, "queue");

                // Parse mảng 2D "carport"
                lvl.carport = Parse2DStringArray(levelBlock, "carport");

                return lvl;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LevelConfigLoader] Lỗi nạp Level {levelId}: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Nạp tranh cát PixelMap theo picture ID (ví dụ 10006)
        /// </summary>
        public static PixelMapData LoadPixelMap(int pictureId)
        {
            string fileName = $"{pictureId}.json";
            string path = Path.Combine(PixelMapDir, fileName);

            if (!File.Exists(path))
            {
                Debug.LogWarning($"[LevelConfigLoader] Không tìm thấy file PixelMap tại: {path}");
                return null;
            }

            try
            {
                string json = File.ReadAllText(path);
                var mapData = new PixelMapData();

                mapData.gridSize = ParseIntField(json, "gridSize", 40);
                mapData.gridPoint = ParseIntField(json, "gridPoint", 5);
                mapData.showGrid = ParseIntField(json, "showGrid", 1);
                mapData.hasGap = ParseIntField(json, "hasGap", 1);

                int pointsIdx = json.IndexOf("\"points\"");
                if (pointsIdx >= 0)
                {
                    int arrStart = json.IndexOf('[', pointsIdx);
                    if (arrStart >= 0)
                    {
                        int arrEnd = FindClosingBracket(json, arrStart, '[', ']');
                        if (arrEnd >= 0)
                        {
                            string pointsBlock = json.Substring(arrStart + 1, arrEnd - arrStart - 1);
                            int cur = 0;
                            int col = 0;
                            while (cur < pointsBlock.Length && col < 40)
                            {
                                int rowStart = pointsBlock.IndexOf('[', cur);
                                if (rowStart < 0) break;
                                int rowEnd = FindClosingBracket(pointsBlock, rowStart, '[', ']');
                                if (rowEnd < 0) break;

                                string colBlock = pointsBlock.Substring(rowStart + 1, rowEnd - rowStart - 1);
                                string[] tokens = colBlock.Split(new[] { ',', '\r', '\n', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                int row = 0;
                                foreach (var t in tokens)
                                {
                                    if (row >= 40) break;
                                    if (int.TryParse(t.Trim(), out int val))
                                    {
                                        mapData.points[col, row] = val;
                                    }
                                    else
                                    {
                                        mapData.points[col, row] = -1;
                                    }
                                    row++;
                                }
                                col++;
                                cur = rowEnd + 1;
                            }
                        }
                    }
                }

                return mapData;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LevelConfigLoader] Lỗi nạp PixelMap {pictureId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Ghi hoặc Cập nhật Level vào levelNCXHCfg.json
        /// </summary>
        public static bool SaveLevel(LevelData level)
        {
            if (level == null) return false;

            if (!File.Exists(LevelCfgPath))
            {
                Debug.LogError($"[LevelConfigLoader] Không tìm thấy file: {LevelCfgPath}");
                return false;
            }

            try
            {
                string json = File.ReadAllText(LevelCfgPath);
                string key = $"\"{level.id}\"";
                int keyIdx = json.IndexOf(key);

                string levelJsonChunk = SerializeLevelBlock(level);

                if (keyIdx >= 0)
                {
                    // Cập nhật level đã tồn tại
                    int openBrace = json.IndexOf('{', keyIdx);
                    int closeBrace = FindClosingBracket(json, openBrace, '{', '}');

                    string before = json.Substring(0, keyIdx);
                    string after = json.Substring(closeBrace + 1);

                    string newJson = before + $"{key}:  " + levelJsonChunk + after;
                    File.WriteAllText(LevelCfgPath, newJson, Encoding.UTF8);
                }
                else
                {
                    // Thêm level mới vào cuối file trước dấu '}' đóng
                    int lastClose = json.LastIndexOf('}');
                    if (lastClose >= 0)
                    {
                        string before = json.Substring(0, lastClose).TrimEnd();
                        if (before.EndsWith(",")) before = before.TrimEnd(',');
                        string newJson = before + ",\n    " + $"{key}:  " + levelJsonChunk + "\n}\n";
                        File.WriteAllText(LevelCfgPath, newJson, Encoding.UTF8);
                    }
                }

                GetAllLevelIds(forceReload: true);
                Debug.Log($"<color=green>[LevelConfigLoader]</color> Đã lưu Level {level.id} thành công vào {LevelCfgPath}!");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LevelConfigLoader] Lỗi lưu Level {level.id}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Ghi lại file PixelMap {pictureId}.json (Chuẩn 1:1 Cocos: points[col][row])
        /// </summary>
        public static bool SavePixelMap(int pictureId, int[,] points, int gridSize = 40)
        {
            if (points == null) return false;
            if (!Directory.Exists(PixelMapDir)) Directory.CreateDirectory(PixelMapDir);

            string path = Path.Combine(PixelMapDir, $"{pictureId}.json");
            try
            {
                StringBuilder sb = new StringBuilder(40 * 40 * 10);
                sb.AppendLine("{");
                sb.AppendLine("    \"gridPoint\":  5,");
                sb.AppendLine($"    \"gridSize\":  {gridSize},");
                sb.AppendLine("    \"showGrid\":  1,");
                sb.AppendLine("    \"hasGap\":  1,");
                sb.AppendLine("    \"bgPath\":  \"\",");
                sb.AppendLine("    \"bgOpacity\":  \"100\",");
                sb.AppendLine("    \"v\":  2,");
                sb.AppendLine("    \"points\":  [");

                for (int col = 0; col < gridSize; col++)
                {
                    sb.Append("                   [\n");
                    for (int row = 0; row < gridSize; row++)
                    {
                        sb.Append("                       ").Append(points[col, row]);
                        if (row < gridSize - 1) sb.Append(",");
                        sb.Append("\n");
                    }
                    sb.Append("                   ]");
                    if (col < gridSize - 1) sb.Append(",");
                    sb.Append("\n");
                }

                sb.AppendLine("               ]");
                sb.AppendLine("}");

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                Debug.Log($"<color=green>[LevelConfigLoader]</color> Đã lưu PixelMap {pictureId}.json thành công!");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LevelConfigLoader] Lỗi lưu PixelMap {pictureId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Thống kê chuẩn nhu cầu hành khách (Seats / Beads) của 1 Level:
        /// 1. Hạt trên bàn cát: chỉ đếm các ô ngoài vùng khoét đường (road.points[col, row] != -1)
        /// 2. Hạt trong hàng đợi rơi thêm: queue[i] * exchangeRatio
        /// </summary>
        public static void CountLevelDemand(LevelData level, PixelMapData pixelMap, RoadConfigData roadConfig,
            out Dictionary<int, int> boardCounts, out Dictionary<int, int> queueCounts, out Dictionary<int, int> totalDemand)
        {
            boardCounts = new Dictionary<int, int>();
            queueCounts = new Dictionary<int, int>();
            totalDemand = new Dictionary<int, int>();

            if (pixelMap != null && pixelMap.points != null)
            {
                for (int col = 0; col < 40; col++)
                {
                    for (int row = 0; row < 40; row++)
                    {
                        if (roadConfig != null && roadConfig.IsCutout(col, row)) continue;

                        int c = pixelMap.points[col, row];
                        if (c >= 0)
                        {
                            if (!boardCounts.ContainsKey(c)) boardCounts[c] = 0;
                            boardCounts[c]++;
                        }
                    }
                }
            }

            if (level != null && level.queue != null)
            {
                int ratio = level.exchangeRatio > 0 ? level.exchangeRatio : 10;
                foreach (int qColor in level.queue)
                {
                    if (qColor >= 0)
                    {
                        if (!queueCounts.ContainsKey(qColor)) queueCounts[qColor] = 0;
                        queueCounts[qColor] += ratio;
                    }
                }
            }

            foreach (var kvp in boardCounts)
            {
                if (!totalDemand.ContainsKey(kvp.Key)) totalDemand[kvp.Key] = 0;
                totalDemand[kvp.Key] += kvp.Value;
            }

            foreach (var kvp in queueCounts)
            {
                if (!totalDemand.ContainsKey(kvp.Key)) totalDemand[kvp.Key] = 0;
                totalDemand[kvp.Key] += kvp.Value;
            }
        }

        /// <summary>
        /// Thống kê số lượng hạt cát của từng màu trong ma trận 40x40
        /// </summary>
        public static Dictionary<int, int> CountPixelColors(int[,] points)
        {
            var counts = new Dictionary<int, int>();
            if (points == null) return counts;

            int dim0 = points.GetLength(0);
            int dim1 = points.GetLength(1);

            for (int col = 0; col < dim0; col++)
            {
                for (int row = 0; row < dim1; row++)
                {
                    int color = points[col, row];
                    if (color >= 0)
                    {
                        if (!counts.ContainsKey(color)) counts[color] = 0;
                        counts[color]++;
                    }
                }
            }
            return counts;
        }

        #region HÀM TIỆN ÍCH PARSE & SERIALIZE JSON
        private static int FindClosingBracket(string text, int openIdx, char openCh, char closeCh)
        {
            int depth = 0;
            bool inQuote = false;
            for (int i = openIdx; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"' && (i == 0 || text[i - 1] != '\\')) inQuote = !inQuote;
                else if (!inQuote)
                {
                    if (c == openCh) depth++;
                    else if (c == closeCh)
                    {
                        depth--;
                        if (depth == 0) return i;
                    }
                }
            }
            return -1;
        }

        private static int ParseIntField(string json, string fieldName, int defaultVal)
        {
            var match = Regex.Match(json, $@"""{fieldName}""\s*:\s*(-?\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int val))
            {
                return val;
            }
            return defaultVal;
        }

        private static List<List<string>> Parse2DStringArray(string json, string fieldName)
        {
            var result = new List<List<string>>();
            int fieldIdx = json.IndexOf($"\"{fieldName}\"");
            if (fieldIdx < 0) return result;

            int arrStart = json.IndexOf('[', fieldIdx);
            if (arrStart < 0) return result;
            int arrEnd = FindClosingBracket(json, arrStart, '[', ']');
            if (arrEnd < 0) return result;

            string block = json.Substring(arrStart + 1, arrEnd - arrStart - 1);
            int cur = 0;
            while (cur < block.Length)
            {
                int rowStart = block.IndexOf('[', cur);
                if (rowStart < 0) break;
                int rowEnd = FindClosingBracket(block, rowStart, '[', ']');
                if (rowEnd < 0) break;

                string rowBlock = block.Substring(rowStart + 1, rowEnd - rowStart - 1);
                var rowList = new List<string>();
                var matches = Regex.Matches(rowBlock, @"""([^""]+)""");
                foreach (Match m in matches)
                {
                    rowList.Add(m.Groups[1].Value);
                }
                result.Add(rowList);
                cur = rowEnd + 1;
            }

            return result;
        }

        private static List<int> Parse1DIntArray(string json, string fieldName)
        {
            var result = new List<int>();
            int fieldIdx = json.IndexOf($"\"{fieldName}\"");
            if (fieldIdx < 0) return result;

            int arrStart = json.IndexOf('[', fieldIdx);
            if (arrStart < 0) return result;
            int arrEnd = FindClosingBracket(json, arrStart, '[', ']');
            if (arrEnd < 0) return result;

            string block = json.Substring(arrStart + 1, arrEnd - arrStart - 1);
            var matches = Regex.Matches(block, @"(-?\d+)");
            foreach (Match m in matches)
            {
                if (int.TryParse(m.Groups[1].Value, out int val))
                {
                    result.Add(val);
                }
            }
            return result;
        }

        private static string SerializeLevelBlock(LevelData lvl)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"                 \"id\":  {lvl.id},");
            sb.AppendLine($"                 \"next\":  {lvl.next},");
            sb.AppendLine($"                 \"collect\":  {lvl.collect},");
            sb.AppendLine($"                 \"exchangeRatio\":  {lvl.exchangeRatio},");
            sb.AppendLine($"                 \"iniCapacity\":  {lvl.iniCapacity},");
            sb.AppendLine($"                 \"maxCapacity\":  {lvl.maxCapacity},");
            sb.AppendLine($"                 \"road\":  {lvl.road},");
            sb.AppendLine($"                 \"bgStyle\":  {lvl.bgStyle},");
            sb.AppendLine($"                 \"uiStyle\":  {lvl.uiStyle},");
            sb.AppendLine($"                 \"weather\":  {lvl.weather},");
            sb.AppendLine($"                 \"bus_prompt\":  {lvl.bus_prompt},");
            sb.AppendLine($"                 \"diff_prompt\":  {lvl.diff_prompt},");
            sb.AppendLine($"                 \"bus_random\":  {lvl.bus_random},");

            // bus
            sb.AppendLine("                 \"bus\":  [");
            for (int r = 0; r < lvl.bus.Count; r++)
            {
                sb.AppendLine("                             [");
                for (int c = 0; c < lvl.bus[r].Count; c++)
                {
                    sb.Append($"                                 \"{lvl.bus[r][c]}\"");
                    if (c < lvl.bus[r].Count - 1) sb.Append(",");
                    sb.Append("\n");
                }
                sb.Append("                             ]");
                if (r < lvl.bus.Count - 1) sb.Append(",");
                sb.Append("\n");
            }
            sb.AppendLine("                         ],");

            // queue
            sb.AppendLine("                 \"queue\":  [");
            for (int q = 0; q < lvl.queue.Count; q++)
            {
                sb.Append($"                               {lvl.queue[q]}");
                if (q < lvl.queue.Count - 1) sb.Append(",");
                sb.Append("\n");
            }
            sb.AppendLine("                           ],");

            // line_random
            sb.AppendLine("                 \"line_random\":  [");
            sb.AppendLine("                                     0");
            sb.AppendLine("                                 ],");

            // minibus_tran
            sb.AppendLine("                 \"minibus_tran\":  [");
            sb.AppendLine("                                      8,");
            sb.AppendLine("                                      12");
            sb.AppendLine("                                  ],");

            // bus_tran
            sb.AppendLine("                 \"bus_tran\":  [");
            sb.AppendLine("                                  10,");
            sb.AppendLine("                                  15");
            sb.AppendLine("                              ],");

            // carport
            sb.AppendLine("                 \"carport\":  [");
            if (lvl.carport != null && lvl.carport.Count > 0)
            {
                for (int cr = 0; cr < lvl.carport.Count; cr++)
                {
                    sb.AppendLine("                                 [");
                    for (int cc = 0; cc < lvl.carport[cr].Count; cc++)
                    {
                        sb.Append($"                                     \"{lvl.carport[cr][cc]}\"");
                        if (cc < lvl.carport[cr].Count - 1) sb.Append(",");
                        sb.Append("\n");
                    }
                    sb.Append("                                 ]");
                    if (cr < lvl.carport.Count - 1) sb.Append(",");
                    sb.Append("\n");
                }
            }
            else
            {
                sb.AppendLine("                                 [");
                sb.AppendLine("                                     \"None\"");
                sb.AppendLine("                                 ]");
            }
            sb.AppendLine("                             ]");

            sb.Append("             }");
            return sb.ToString();
        }
        #endregion
    }
}
