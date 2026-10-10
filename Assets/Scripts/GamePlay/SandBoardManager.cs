using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WhoGetInBus.GamePlay
{
    public class SandBoardManager : MonoBehaviour
    {
        public static SandBoardManager Instance { get; private set; }

        [Header("Kích Thước Tranh Cát (Chuẩn Cocos Creator 1:1)")]
        public int columns = 40;
        public int rows = 40;
        [Tooltip("Khoảng cách giữa các hạt cát (Chuẩn Cocos: 750px / 40 = 18.75px -> 0.2586m ~ 0.26m)")]
        public float beadSpacing = 0.2586f;
        [Tooltip("Bán kính hạt cát (Chuẩn: beadSpacing / 2 = 0.1293m)")]
        public float beadRadius = 0.1293f;

        [Header("Thông Số Scale & Vị Trí (Chuẩn Cocos Creator 1:1)")]
        [Tooltip("Scale của các hạt cát lúc tạo ra (chuẩn 0.55 để vừa khít lưới 0.26m)")]
        [Range(0.1f, 3f)]
        public float beadScale = 0.55f;

        [Tooltip("Vị trí Z của bảng cát (Chuẩn Cocos: 6.0 tương ứng Canvas Y = +371px)")]
        public float boardPosZ = 6.0f;

        [Header("Lệch Zigzag Cát (Chuẩn Cocos Creator 1:1)")]
        [Tooltip("Bật/tắt hiệu ứng xếp hạt so le hình zigzag")]
        public bool enableZigzag = true;

        [Tooltip("Độ lệch trục X giữa các hàng xen kẽ nhau (chuẩn Cocos: oddRowOffsetX = 6px -> 0.0828m ~ 0.083m)")]
        [Range(-0.5f, 0.5f)]
        public float zigzagOffset = 0.0828f;

        [Header("Khoét Rãnh Đường Cong Cho Xe")]
        public bool enableRoadCutout = true;
        public int cutoutColMin = 13;
        public int cutoutColMax = 26;
        public int cutoutRowMax = 19;

        [Header("Prefab & Material")]
        public GameObject beadPrefab;
        public Material beadMaterial;
        public PassengerColorData passengerColors;

        [Header("Dữ Liệu Màn Chơi (Level Data)")]
        [Tooltip("Level ID chuẩn gốc Cocos (1001, 1002, 1003...)")]
        public int levelId = 1001;
        [Tooltip("ID Tuyến đường (road trong levelNCXHCfg.json, mặc định = 5)")]
        public int currentRoadId = 5;
        public TextAsset levelConfigFile;
        [TextArea(2, 6)]
        public string sandDataString;

        // Lưu trữ các hạt trên bảng: [col, row]
        private GameObject[,] beadObjects;
        private int[,] beadColorIndices; // -1 nếu rỗng, hoặc 0..17
        private int totalRemainingBeads = 0;

        public int TotalRemainingBeads => totalRemainingBeads;

        private void Awake()
        {
            Instance = this;
            transform.position = new Vector3(transform.position.x, transform.position.y, boardPosZ);

            if (totalRemainingBeads == 0 && transform.childCount > 0)
            {
                InitFromExistingChildren();
            }
        }

        /// <summary>
        /// Nạp và sinh trực tiếp tranh cát từ Level ID chuẩn gốc (1001, 1002, ...)
        /// </summary>
        public void LoadLevelDirect(int targetLevelId, GamePrefabData prefabData = null)
        {
            var levelData = WhoGetInBus.Data.LevelConfigLoader.LoadLevel(targetLevelId);
            if (levelData != null)
            {
                var collectData = WhoGetInBus.Data.LevelConfigLoader.GetCollect(levelData.collect);
                int picId = collectData != null ? collectData.picture : levelData.collect;
                var pixelMap = WhoGetInBus.Data.LevelConfigLoader.LoadPixelMap(picId);
                if (pixelMap != null)
                {
                    int[,] pData = new int[40, 40];
                    for (int r = 0; r < 40; r++)
                    {
                        for (int c = 0; c < 40; c++)
                        {
                            // In 3D: r = 0 là đáy (gần cổng đường), r = 39 là đỉnh trên cùng.
                            // Trong file PixelMap: col = 0..39 (trái sang phải), row = 0..39 (đỉnh trên xuống đáy).
                            // Vì vậy: 3D col c = json col c; 3D row r = json row (39 - r).
                            pData[r, c] = pixelMap.points[c, 39 - r];
                        }
                    }
                    levelId = targetLevelId;
                    currentRoadId = levelData.road;
                    BuildBoard(pData, prefabData);
                    return;
                }
            }
            Debug.LogWarning($"[SandBoardManager] Không tìm thấy dữ liệu level {targetLevelId} chuẩn gốc!");
        }



        public Vector3 GetBeadLocalPosition(int col, int row)
        {
            float startX = -((columns - 1) * beadSpacing) * 0.5f;
            float startZ = 0f;
            float x = startX + col * beadSpacing;
            if (enableZigzag && (row % 2 != 0))
            {
                x += zigzagOffset;
            }
            float z = startZ + row * beadSpacing;
            return new Vector3(x, 0.12f, z);
        }

        public void BuildBoard(int[,] pixelData, GamePrefabData prefabData)
        {
            if (prefabData != null)
            {
                beadPrefab = prefabData.passengerPrefab;
                beadMaterial = prefabData.beadMaterial;
                passengerColors = prefabData.passengerColors;
            }
            BuildBoard(pixelData, passengerColors, beadMaterial);
        }

        public void BuildBoard(int[,] pixelData, PassengerColorData pColors, Material bMat)
        {
            passengerColors = pColors;
            beadMaterial = bMat;
            ClearBoard();

            // Cập nhật vị trí Z của board
            transform.position = new Vector3(transform.position.x, transform.position.y, boardPosZ);

            beadObjects = new GameObject[columns, rows];
            beadColorIndices = new int[columns, rows];
            totalRemainingBeads = 0;

            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    // Kiểm tra vùng khoét rãnh đường đón xe
                    if (enableRoadCutout && IsInRoadCutout(c, r))
                    {
                        beadColorIndices[c, r] = -1;
                        sb.Append("-1,");
                        continue;
                    }

                    int colorIdx = -1;
                    if (pixelData != null && r < pixelData.GetLength(0) && c < pixelData.GetLength(1))
                    {
                        colorIdx = pixelData[r, c];
                    }

                    if (colorIdx < 0)
                    {
                        beadColorIndices[c, r] = -1;
                        sb.Append("-1,");
                        continue;
                    }

                    beadColorIndices[c, r] = colorIdx;
                    totalRemainingBeads++;
                    sb.Append(colorIdx).Append(",");

                    Vector3 localPos = GetBeadLocalPosition(c, r);
                    GameObject bead = SpawnBeadObject(localPos, colorIdx, c, r);
                    beadObjects[c, r] = bead;
                }
            }

            if (sb.Length > 0) sb.Length--;
            sandDataString = sb.ToString();

            Debug.Log($"<color=cyan>[SandBoardManager]</color> Đã sinh thành công {totalRemainingBeads} hạt cát (Scale: {beadScale}, PosZ: {boardPosZ}, Zigzag: {(enableZigzag ? zigzagOffset.ToString("F2") : "Tắt")})!");
        }

        public void ClearBoard()
        {
            List<GameObject> toDestroy = new List<GameObject>();
            for (int i = 0; i < transform.childCount; i++)
            {
                toDestroy.Add(transform.GetChild(i).gameObject);
            }
            foreach (var g in toDestroy)
            {
                if (Application.isPlaying) Destroy(g);
                else DestroyImmediate(g);
            }
            totalRemainingBeads = 0;
        }

        public bool IsInRoadCutout(int col, int row)
        {
            // Kiểm tra theo cấu hình RoadConfigData chuẩn gốc từ roadNCXHCfg.json
            var roadCfg = WhoGetInBus.Data.LevelConfigLoader.GetRoadConfig(currentRoadId);
            if (roadCfg != null)
            {
                // In 3D: row 0 là đáy (gần cổng đường), row 39 là đỉnh trên
                // Trong roadCfg.points: col là 0..39, row là 0 (đỉnh) .. 39 (đáy)
                int jsonRow = 39 - row;
                if (col >= 0 && col < 40 && jsonRow >= 0 && jsonRow < 40)
                {
                    return roadCfg.IsCutout(col, jsonRow);
                }
            }

            // Fallback nếu không có file cấu hình road
            if (row > cutoutRowMax) return false;
            if (col >= cutoutColMin && col <= cutoutColMax)
            {
                int centerCol = (cutoutColMin + cutoutColMax) / 2;
                float distFromCenter = Mathf.Abs(col - centerCol);
                float maxAllowedRow = cutoutRowMax - (distFromCenter * 0.7f);
                return row <= maxAllowedRow;
            }
            return false;
        }

        private GameObject SpawnBeadObject(Vector3 localPos, int colorIdx, int col = -1, int row = -1)
        {
            GameObject bead = null;
            if (beadPrefab != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    bead = (GameObject)PrefabUtility.InstantiatePrefab(beadPrefab, transform);
                }
                else
                {
                    bead = Instantiate(beadPrefab, transform);
                }
#else
                bead = Instantiate(beadPrefab, transform);
#endif
                bead.transform.localScale = Vector3.one * beadScale;
            }
            else
            {
                bead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bead.transform.SetParent(transform);
                var colComp = bead.GetComponent<Collider>();
                if (colComp != null)
                {
                    if (Application.isPlaying) Destroy(colComp);
                    else DestroyImmediate(colComp);
                }
                bead.transform.localScale = Vector3.one * beadScale;
            }

            if (col >= 0 && row >= 0)
            {
                bead.name = $"Passenger_{col}_{row}_{colorIdx}";
            }
            else
            {
                bead.name = $"Passenger_{colorIdx}";
            }

            bead.transform.localPosition = localPos;
            bead.transform.localRotation = (beadPrefab != null) ? beadPrefab.transform.localRotation : Quaternion.identity;

            // 1. NẾU DÙNG SPRITE RENDERER (NHƯ PASSENGER.PREFAB): CHỈ ĐỔI ẢNH (SPRITE), ĐỂ NGUYÊN MATERIAL CỦA SPRITE RENDERER!
            SpriteRenderer sr = bead.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                if (passengerColors != null && passengerColors.data.ContainsKey((GameColorType)colorIdx))
                {
                    sr.sprite = passengerColors.data[(GameColorType)colorIdx];
                }
            }
            else
            {
                // 2. NẾU LÀ 3D MESH / SPHERE PRIMITIVE: MỚI GÁN MATERIAL MÀU
                MeshRenderer mr = bead.GetComponentInChildren<MeshRenderer>();
                if (mr != null)
                {
                    if (beadMaterial != null) mr.material = beadMaterial;
                    mr.material.color = GetPaletteColor(colorIdx);
                }
            }

            return bead;
        }

        private void Reset()
        {
            ApplyCocosOriginalSettings();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (transform.position.z != boardPosZ)
            {
                transform.position = new Vector3(transform.position.x, transform.position.y, boardPosZ);
            }
        }
#endif

        /// <summary>
        /// Áp dụng 100% đúng vị trí và thông số chuẩn gốc Cocos Creator (1:1).
        /// </summary>
        [ContextMenu("⚡ Áp Dụng Thông Số Chuẩn Gốc Cocos (1:1)")]
        public void ApplyCocosOriginalSettings()
        {
            columns = 40;
            rows = 40;
            // Cocos: canvas 750px / 40 cols = 18.75px -> chiếu sang Unity 3D (ortho size 9.2): 0.2586m (~0.26m)
            beadSpacing = 0.2586f;
            beadRadius = 0.1293f;
            beadScale = 0.55f;     // Khớp với sprite width 0.48m * 0.55 = 0.264m vừa khít lưới
            boardPosZ = 6.0f;      // Tọa độ Z chuẩn chiếu lên screen Y = +371px
            enableZigzag = true;
            zigzagOffset = 0.0828f;// oddRowOffsetX = 6px -> 0.0828m (~0.083m)
            enableRoadCutout = true;
            cutoutColMin = 13;
            cutoutColMax = 26;
            cutoutRowMax = 19;

            // Đặt vị trí, góc quay và scale chuẩn của SandBoard_Manager GameObject
            transform.position = new Vector3(0f, 0f, 6.0f);
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            UpdateExistingBeads();

#if UNITY_EDITOR
            Undo.RecordObject(transform, "Apply Cocos Original Settings");
            Undo.RecordObject(this, "Apply Cocos Original Settings");
            EditorUtility.SetDirty(gameObject);
            EditorUtility.SetDirty(this);
            SceneView.RepaintAll();
#endif
            Debug.Log("<color=green>[SandBoardManager]</color> <b>ĐÃ ÁP DỤNG THÀNH CÔNG THÔNG SỐ CHUẨN GỐC COCOS 1:1!</b>\n" +
                      $"• Bead Spacing: {beadSpacing} (~0.26m)\n" +
                      $"• Zigzag Offset: {zigzagOffset} (~0.083m)\n" +
                      $"• Bead Scale: {beadScale}\n" +
                      $"• Transform Position: {transform.position}");
        }

        [ContextMenu("⚡ Cập Nhật Scale & Lệch Zigzag")]
        public void UpdateExistingBeads()
        {
            // Cập nhật vị trí Z của board
            transform.position = new Vector3(transform.position.x, transform.position.y, boardPosZ);

            int count = transform.childCount;
            if (count == 0)
            {
                Debug.LogWarning("[SandBoardManager] Hiện không có hạt cát nào là con của SandBoard_Manager để cập nhật. Hãy nhấn 'Tái Tạo Tranh Cát' để tạo mới.");
                return;
            }

            float startX = -((columns - 1) * beadSpacing) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == null) continue;

#if UNITY_EDITOR
                Undo.RecordObject(child, "Update Bead Scale & Position");
#endif
                child.localScale = Vector3.one * beadScale;

                int col = -1;
                int row = -1;
                int colorIdx = 0;

                string[] tokens = child.name.Split('_');
                if (tokens.Length >= 4 && int.TryParse(tokens[1], out int pCol) && int.TryParse(tokens[2], out int pRow))
                {
                    col = pCol;
                    row = pRow;
                    if (int.TryParse(tokens[3], out int pColor)) colorIdx = pColor;
                }
                else if (tokens.Length >= 2 && int.TryParse(tokens[1], out int cOnly))
                {
                    colorIdx = cOnly;
                    row = Mathf.Clamp(Mathf.RoundToInt(child.localPosition.z / beadSpacing), 0, rows - 1);
                    col = Mathf.Clamp(Mathf.RoundToInt((child.localPosition.x - startX) / beadSpacing), 0, columns - 1);
                    child.name = $"Passenger_{col}_{row}_{colorIdx}";
                }
                else
                {
                    row = Mathf.Clamp(Mathf.RoundToInt(child.localPosition.z / beadSpacing), 0, rows - 1);
                    col = Mathf.Clamp(Mathf.RoundToInt((child.localPosition.x - startX) / beadSpacing), 0, columns - 1);
                }

                if (col >= 0 && row >= 0)
                {
                    child.localPosition = GetBeadLocalPosition(col, row);
                }
            }

#if UNITY_EDITOR
            Undo.RecordObject(transform, "Update Board Position");
            EditorUtility.SetDirty(gameObject);
#endif
            Debug.Log($"<color=green>[SandBoardManager]</color> Đã cập nhật xong {count} hạt cát! (Scale: {beadScale}, Zigzag: {(enableZigzag ? zigzagOffset.ToString("F2") : "Tắt")}, PosZ: {boardPosZ})");
        }

        [ContextMenu("🔄 Tái Tạo Toàn Bộ Tranh Cát")]
        public void RebuildBoardInEditor()
        {
#if UNITY_EDITOR
            if (beadPrefab == null || passengerColors == null || beadMaterial == null)
            {
                var prefabData = AssetDatabase.LoadAssetAtPath<GamePrefabData>("Assets/Data/GamePrefabData.asset");
                if (prefabData != null)
                {
                    if (beadPrefab == null) beadPrefab = prefabData.passengerPrefab;
                    if (beadMaterial == null) beadMaterial = prefabData.beadMaterial;
                    if (passengerColors == null) passengerColors = prefabData.passengerColors;
                }
            }
#endif

            int[,] pixelData = GetOrLoadPixelData();
            BuildBoard(pixelData, passengerColors, beadMaterial);

#if UNITY_EDITOR
            EditorUtility.SetDirty(gameObject);
#endif
        }

        public int[,] GetOrLoadPixelData()
        {
            int[,] pixelData = new int[rows, columns];

            // 1. Thử lấy từ LevelConfigLoader chuẩn gốc trước tiên
            if (levelId > 0)
            {
                var levelData = WhoGetInBus.Data.LevelConfigLoader.LoadLevel(levelId);
                if (levelData != null)
                {
                    var collectData = WhoGetInBus.Data.LevelConfigLoader.GetCollect(levelData.collect);
                    int picId = collectData != null ? collectData.picture : levelData.collect;
                    var pixelMap = WhoGetInBus.Data.LevelConfigLoader.LoadPixelMap(picId);
                    if (pixelMap != null)
                    {
                        currentRoadId = levelData.road;
                        for (int r = 0; r < rows; r++)
                        {
                            for (int c = 0; c < columns; c++)
                            {
                                int jsonRow = (rows - 1) - r;
                                pixelData[r, c] = pixelMap.points[c, jsonRow];
                            }
                        }
                        return pixelData;
                    }
                }
            }

            // 2. Thử lấy từ sandDataString nếu có
            if (!string.IsNullOrEmpty(sandDataString))
            {
                string[] tokens = sandDataString.Split(',');
                if (tokens.Length >= rows * columns)
                {
                    for (int r = 0; r < rows; r++)
                    {
                        for (int c = 0; c < columns; c++)
                        {
                            int idx = r * columns + c;
                            if (idx < tokens.Length && int.TryParse(tokens[idx], out int val))
                            {
                                pixelData[r, c] = val;
                            }
                            else
                            {
                                pixelData[r, c] = -1;
                            }
                        }
                    }
                    return pixelData;
                }
            }

            // 2. Thử lấy từ file level JSON
            string jsonContent = null;
            if (levelConfigFile != null)
            {
                jsonContent = levelConfigFile.text;
            }
#if UNITY_EDITOR
            else
            {
                var defaultJson = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Scene_Data/Levels/Level_1_Config.json");
                if (defaultJson != null)
                {
                    levelConfigFile = defaultJson;
                    jsonContent = defaultJson.text;
                }
            }
#endif

            if (!string.IsNullOrEmpty(jsonContent))
            {
                var sandMatch = System.Text.RegularExpressions.Regex.Match(jsonContent, "\"sand_data\"\\s*:\\s*\"([^\"]+)\"");
                if (sandMatch.Success)
                {
                    string[] tokens = sandMatch.Groups[1].Value.Split(',');
                    for (int r = 0; r < rows; r++)
                    {
                        for (int c = 0; c < columns; c++)
                        {
                            int idx = r * columns + c;
                            if (idx < tokens.Length && int.TryParse(tokens[idx], out int val))
                            {
                                pixelData[r, c] = val;
                            }
                            else
                            {
                                pixelData[r, c] = -1;
                            }
                        }
                    }
                    return pixelData;
                }
            }

            // 3. Fallback: Nếu không có dữ liệu, tạo mẫu màu thử nghiệm (Test Pattern) để người dùng xem luôn
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    pixelData[r, c] = (r / 4 + c / 4) % 18;
                }
            }
            return pixelData;
        }

        public void InitFromExistingChildren()
        {
            beadObjects = new GameObject[columns, rows];
            beadColorIndices = new int[columns, rows];
            totalRemainingBeads = 0;

            float startX = -((columns - 1) * beadSpacing) * 0.5f;

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == null) continue;

                int col = -1;
                int row = -1;
                int colorIdx = 0;

                string[] tokens = child.name.Split('_');
                if (tokens.Length >= 4 && int.TryParse(tokens[1], out int pCol) && int.TryParse(tokens[2], out int pRow))
                {
                    col = pCol;
                    row = pRow;
                    if (int.TryParse(tokens[3], out int pColor)) colorIdx = pColor;
                }
                else if (tokens.Length >= 2 && int.TryParse(tokens[1], out int cOnly))
                {
                    colorIdx = cOnly;
                    row = Mathf.Clamp(Mathf.RoundToInt(child.localPosition.z / beadSpacing), 0, rows - 1);
                    col = Mathf.Clamp(Mathf.RoundToInt((child.localPosition.x - startX) / beadSpacing), 0, columns - 1);
                }

                if (col >= 0 && col < columns && row >= 0 && row < rows)
                {
                    beadObjects[col, row] = child.gameObject;
                    beadColorIndices[col, row] = colorIdx;
                    totalRemainingBeads++;
                }
            }
        }

        public Color GetPaletteColor(int colorIdx)
        {
            Color[] palette = new Color[]
            {
                new Color(1.000f, 1.000f, 1.000f), // 0: White
                new Color(0.188f, 0.820f, 0.514f), // 1: Green
                new Color(0.000f, 0.878f, 1.000f), // 2: Cyan
                new Color(0.031f, 0.576f, 1.000f), // 3: Blue
                new Color(0.525f, 0.275f, 0.110f), // 4: Brown
                new Color(0.749f, 0.306f, 0.933f), // 5: Purple
                new Color(0.969f, 0.827f, 0.741f), // 6: Powder
                new Color(0.980f, 0.882f, 0.165f), // 7: Yellow
                new Color(0.980f, 0.235f, 0.235f), // 8: Red
                new Color(0.600f, 0.914f, 0.129f), // 9: EmeraldGreen
                new Color(1.000f, 0.561f, 0.000f), // 10: Orange
                new Color(0.208f, 0.208f, 0.208f), // 11: Black
                new Color(0.051f, 0.659f, 0.000f), // 12: DarkGreen
                new Color(0.725f, 0.016f, 0.369f), // 13: Burgundy
                new Color(0.525f, 0.537f, 0.820f), // 14: GrayishBlue
                new Color(0.765f, 0.753f, 0.969f), // 15: LightPurple
                new Color(0.988f, 0.416f, 0.918f), // 16: Pink
                new Color(0.729f, 0.961f, 0.851f)  // 17: Teal
            };

            if (colorIdx >= 0 && colorIdx < palette.Length) return palette[colorIdx];
            return Color.white;
        }

        public bool TryGetPassengerAtBottom(GameColorType targetColor, out GameObject beadObj, out Vector3 worldPos, out int foundCol, out int foundRow)
        {
            beadObj = null;
            worldPos = Vector3.zero;
            foundCol = -1;
            foundRow = -1;

            if (beadObjects == null || beadColorIndices == null)
            {
                InitFromExistingChildren();
            }

            int targetIdx = (int)targetColor;

            // Quét từ các cột gần trung tâm trước
            List<int> colOrder = new List<int>();
            int center = columns / 2;
            for (int offset = 0; offset <= center; offset++)
            {
                if (center - offset >= 0) colOrder.Add(center - offset);
                if (offset > 0 && center + offset < columns) colOrder.Add(center + offset);
            }

            foreach (int c in colOrder)
            {
                for (int r = 0; r < rows; r++)
                {
                    if (beadColorIndices != null && beadColorIndices[c, r] >= 0)
                    {
                        if (beadColorIndices[c, r] == targetIdx)
                        {
                            beadObj = beadObjects[c, r];
                            if (beadObj != null)
                            {
                                worldPos = beadObj.transform.position;
                                foundCol = c;
                                foundRow = r;
                                return true;
                            }
                        }
                        break;
                    }
                }
            }

            return false;
        }

        public void ConsumeBead(int c, int r)
        {
            if (c < 0 || c >= columns || r < 0 || r >= rows) return;

            if (beadObjects != null && beadObjects[c, r] != null)
            {
                Destroy(beadObjects[c, r]);
                beadObjects[c, r] = null;
            }

            if (beadColorIndices != null)
            {
                beadColorIndices[c, r] = -1;
            }
            totalRemainingBeads = Mathf.Max(0, totalRemainingBeads - 1);

            // Kích hoạt hiệu ứng cát sạt lở rơi xuống bù vào vị trí trống (ASMR gravity collapse)
            CollapseColumn(c, r);

            if (totalRemainingBeads <= 0)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.OnLevelVictory();
                }
            }
        }

        private void CollapseColumn(int c, int startR)
        {
            if (beadColorIndices == null || beadObjects == null) return;

            // Dồn tất cả hạt phía trên ô startR tụt xuống 1 nấc
            for (int r = startR + 1; r < rows; r++)
            {
                if (beadColorIndices[c, r] >= 0)
                {
                    int targetRow = r - 1;
                    beadColorIndices[c, targetRow] = beadColorIndices[c, r];
                    beadObjects[c, targetRow] = beadObjects[c, r];

                    beadColorIndices[c, r] = -1;
                    beadObjects[c, r] = null;

                    // Cho hạt trượt dồn về phía trước (trục Z và X theo zigzag) mượt mà
                    if (beadObjects[c, targetRow] != null)
                    {
                        Vector3 targetLocalPos = GetBeadLocalPosition(c, targetRow);
                        StartCoroutine(AnimateFall(beadObjects[c, targetRow], targetLocalPos));
                    }
                }
            }
        }

        private IEnumerator AnimateFall(GameObject bead, Vector3 endPos)
        {
            if (bead == null) yield break;
            Vector3 startPos = bead.transform.localPosition;
            float elapsed = 0f;
            float duration = 0.12f;

            while (elapsed < duration && bead != null)
            {
                elapsed += Time.deltaTime;
                bead.transform.localPosition = Vector3.Lerp(startPos, endPos, elapsed / duration);
                yield return null;
            }

            if (bead != null) bead.transform.localPosition = endPos;
        }
    }
}
