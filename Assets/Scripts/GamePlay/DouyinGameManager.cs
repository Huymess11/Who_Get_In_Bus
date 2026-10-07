using System.Collections.Generic;
using UnityEngine;

namespace DouyinGame.GamePlay
{
    public class DouyinGameManager : MonoBehaviour
    {
        public static DouyinGameManager Instance { get; private set; }

        public enum PlayState
        {
            Idle,
            Playing,
            Victory,
            Failed
        }

        [Header("Trạng Thái Trò Chơi")]
        public PlayState currentState = PlayState.Idle;
        public int completedCarsCount = 0;
        public float playTime = 0f;

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            if (currentState == PlayState.Playing)
            {
                playTime += Time.deltaTime;
            }
        }

        public void StartGame()
        {
            currentState = PlayState.Playing;
            completedCarsCount = 0;
            playTime = 0f;
            Debug.Log("<color=green>[DouyinGameManager]</color> Trò chơi BẮT ĐẦU! Click vào xe ở bãi đỗ để xuất bến đón khách!");
        }

        public void OnCarCompleted(CarLoopFollower car)
        {
            completedCarsCount++;
            Debug.Log($"<color=green>[DouyinGameManager]</color> Xe {car.name} đã gom đủ 100% khách và rời màn thành công! (Tổng xe thoát: {completedCarsCount})");
        }

        public void OnLevelVictory()
        {
            if (currentState != PlayState.Victory)
            {
                currentState = PlayState.Victory;
                Debug.Log($"<color=yellow>★★★ CHÚC MỪNG CHIẾN THẮNG (VICTORY)! ★★★</color> Đã hoàn thành gom sạch tranh cát trong {playTime:F1}s!");
            }
        }

        private void OnGUI()
        {
            if (currentState == PlayState.Idle) return;

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            GUIStyle boldLabel = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            GUIStyle miniLabel = new GUIStyle(GUI.skin.label) { fontSize = 11 };

            GUI.backgroundColor = new Color(0.1f, 0.15f, 0.25f, 0.85f);
            GUILayout.BeginArea(new Rect(20, 20, 320, 140), boxStyle);

            GUILayout.Label("🎮 DOUYIN MINI GAME - CHƠI THỬ TRỰC TIẾP", boldLabel);
            GUILayout.Space(4);

            int remainingBeads = SandBoardManager.Instance != null ? SandBoardManager.Instance.TotalRemainingBeads : 0;
            GUILayout.Label($"⏳ Hạt cát còn lại: {remainingBeads}");
            GUILayout.Label($"🚌 Xe hoàn thành thoát màn: {completedCarsCount}");
            GUILayout.Label($"⏱️ Thời gian chơi: {playTime:F1}s");

            GUILayout.Space(6);
            if (currentState == PlayState.Victory)
            {
                GUI.color = Color.green;
                GUILayout.Label("🎉 CHIẾN THẮNG! LEVEL CÂN BẰNG 100%!", boldLabel);
                GUI.color = Color.white;
            }
            else
            {
                GUILayout.Label("👉 Click vào xe ở bãi đỗ để xuất bến đón khách!", miniLabel);
            }

            GUILayout.EndArea();
            GUI.backgroundColor = Color.white;
        }
    }
}
