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

        [Header("Kích Thước Tranh Cát")]
        public int columns = 40;
        public int rows = 40;
        public float beadSpacing = 0.28f;
        public float beadRadius = 0.13f;

        [Header("Thông Số Scale & Vị Trí (Mới)")]
        [Tooltip("Scale của các hạt cát lúc tạo ra (mặc định = 1)")]
        [Range(0.1f, 3f)]
        public float beadScale = 1.0f;

        [Tooltip("Vị trí Z của bảng cát (mặc định = 6)")]
        public float boardPosZ = 6.0f;

        [Header("Lệch Zigzag Cát")]
        [Tooltip("Bật/tắt hiệu ứng xếp hạt so le hình zigzag")]
        public bool enableZigzag = true;

        [Tooltip("Độ lệch trục X giữa các hàng xen kẽ nhau (mặc định 0.14 = một nửa beadSpacing)")]
        [Range(-0.5f, 0.5f)]
        public float zigzagOffset = 0.14f;

        [Header("Khoét Rãnh Đường Cong Cho Xe")]
        public bool enableRoadCutout = true;
        public int cutoutColMin = 14;
        public int cutoutColMax = 25;
        public int cutoutRowMax = 18;

        [Header("Prefab & Material")]
        public GameObject beadPrefab;
        public Material beadMaterial;
        public PassengerColorData passengerColors;

        [Header("Dữ Liệu Màn Chơi (Level Data)")]
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
            if (row > cutoutRowMax) return false;
            // Tạo hình vòm chữ U khoét rãnh
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

            // 1. Thử lấy từ sandDataString nếu có
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
                new Color(1f, 1f, 1f),          // White
                new Color(0.19f, 0.82f, 0.51f), // Green
                new Color(0.27f, 0.85f, 1f),    // Cyan
                new Color(0.16f, 0.51f, 0.96f), // Blue
                new Color(0.4f, 0.22f, 0.18f),  // Brown
                new Color(0.71f, 0.45f, 0.99f), // Purple
                new Color(1f, 0.79f, 0.66f),    // Powder
                new Color(1f, 0.88f, 0.28f),    // Yellow
                new Color(1f, 0.32f, 0.36f),    // Red
                new Color(0.6f, 0.9f, 0.2f),    // EmeraldGreen
                new Color(1f, 0.5f, 0.1f),      // Orange
                new Color(0.15f, 0.15f, 0.15f), // Black
                new Color(0.1f, 0.5f, 0.2f),    // DarkGreen
                new Color(0.6f, 0.1f, 0.1f),    // Burgundy
                new Color(0.4f, 0.5f, 0.6f),    // GrayishBlue
                new Color(0.8f, 0.6f, 0.9f),    // LightPurple
                new Color(0.95f, 0.4f, 0.7f),   // Pink
                new Color(0.1f, 0.85f, 0.85f)   // Teal
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
