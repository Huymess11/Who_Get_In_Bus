using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DouyinGame.GamePlay
{
    public class SandBoardManager : MonoBehaviour
    {
        public static SandBoardManager Instance { get; private set; }

        [Header("Kích Thước Tranh Cát")]
        public int columns = 40;
        public int rows = 40;
        public float beadSpacing = 0.28f;
        public float beadRadius = 0.13f;

        [Header("Khoét Rãnh Đường Cong Cho Xe")]
        public bool enableRoadCutout = true;
        public int cutoutColMin = 14;
        public int cutoutColMax = 25;
        public int cutoutRowMax = 18;

        [Header("Prefab & Material")]
        public GameObject beadPrefab;
        public Material beadMaterial;
        public PassengerColorData passengerColors;

        // Lưu trữ các hạt trên bảng: [col, row]
        private GameObject[,] beadObjects;
        private int[,] beadColorIndices; // -1 nếu rỗng, hoặc 0..17
        private int totalRemainingBeads = 0;

        public int TotalRemainingBeads => totalRemainingBeads;

        private void Awake()
        {
            Instance = this;
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

            beadObjects = new GameObject[columns, rows];
            beadColorIndices = new int[columns, rows];
            totalRemainingBeads = 0;

            float startX = -((columns - 1) * beadSpacing) * 0.5f;
            float startZ = 0f;

            for (int r = 0; r < rows; r++)
            {
                float z = startZ + r * beadSpacing;

                for (int c = 0; c < columns; c++)
                {
                    // Kiểm tra vùng khoét rãnh đường đón xe
                    if (enableRoadCutout && IsInRoadCutout(c, r))
                    {
                        beadColorIndices[c, r] = -1;
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
                        continue;
                    }

                    beadColorIndices[c, r] = colorIdx;
                    totalRemainingBeads++;

                    float x = startX + c * beadSpacing;
                    Vector3 localPos = new Vector3(x, 0.12f, z);

                    GameObject bead = SpawnBeadObject(localPos, colorIdx);
                    beadObjects[c, r] = bead;
                }
            }

            Debug.Log($"<color=cyan>[SandBoardManager]</color> Đã sinh thành công {totalRemainingBeads} hạt cát nằm ngang 90 độ trên mặt đất!");
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

        private GameObject SpawnBeadObject(Vector3 localPos, int colorIdx)
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
                bead.transform.localScale = beadPrefab.transform.localScale;
            }
            else
            {
                bead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bead.transform.SetParent(transform);
                var col = bead.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Destroy(col);
                    else DestroyImmediate(col);
                }
                bead.transform.localScale = Vector3.one * (beadRadius * 2f);
            }

            bead.name = $"Passenger_{colorIdx}";
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
                // Giữ nguyên material gốc của Sprite Renderer, tuyệt đối không gán material màu!
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

        // Tìm hạt ở đáy thấp nhất của các cột có màu trùng khớp với xe
        public bool TryGetPassengerAtBottom(GameColorType targetColor, out GameObject beadObj, out Vector3 worldPos, out int foundCol, out int foundRow)
        {
            beadObj = null;
            worldPos = Vector3.zero;
            foundCol = -1;
            foundRow = -1;

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
                    if (beadColorIndices[c, r] >= 0)
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
                        // Nếu chạm phải hạt đầu tiên của cột mà khác màu thì cột này tạm thời bị chặn
                        break;
                    }
                }
            }

            return false;
        }

        public void ConsumeBead(int c, int r)
        {
            if (c < 0 || c >= columns || r < 0 || r >= rows) return;

            if (beadObjects[c, r] != null)
            {
                Destroy(beadObjects[c, r]);
                beadObjects[c, r] = null;
            }

            beadColorIndices[c, r] = -1;
            totalRemainingBeads = Mathf.Max(0, totalRemainingBeads - 1);

            // Kích hoạt hiệu ứng cát sạt lở rơi xuống bù vào vị trí trống (ASMR gravity collapse)
            CollapseColumn(c, r);

            if (totalRemainingBeads <= 0)
            {
                if (DouyinGameManager.Instance != null)
                {
                    DouyinGameManager.Instance.OnLevelVictory();
                }
            }
        }

        private void CollapseColumn(int c, int startR)
        {
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

                    // Cho hạt trượt dồn về phía trước (trục Z) mượt mà
                    if (beadObjects[c, targetRow] != null)
                    {
                        float newZ = targetRow * beadSpacing;
                        StartCoroutine(AnimateFall(beadObjects[c, targetRow], newZ));
                    }
                }
            }
        }

        private IEnumerator AnimateFall(GameObject bead, float targetLocalZ)
        {
            if (bead == null) yield break;
            Vector3 startPos = bead.transform.localPosition;
            Vector3 endPos = new Vector3(startPos.x, startPos.y, targetLocalZ);

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
