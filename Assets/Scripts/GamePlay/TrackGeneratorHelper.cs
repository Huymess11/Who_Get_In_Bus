using System.Collections.Generic;
using UnityEngine;

namespace WhoGetInBus.GamePlay
{
    public static class TrackGeneratorHelper
    {
        public static LoopTrackSystem SetupDefaultLoopTrack(Vector3 boardCenterPos)
        {
            GameObject root = GameObject.Find("[LoopTrackSystem]");
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }

            root = new GameObject("[LoopTrackSystem]");
            root.transform.position = Vector3.zero;

            var trackSys = root.AddComponent<LoopTrackSystem>();

            // 1. Tạo Cổng đón khách (Boarding Gate) tại chân tranh cát
            Vector3 gatePos = new Vector3(boardCenterPos.x, 0f, boardCenterPos.z - 2.5f);
            GameObject gateObj = new GameObject("BoardingGate_Station");
            gateObj.transform.SetParent(root.transform);
            gateObj.transform.position = gatePos;
            gateObj.AddComponent<BoardingGateDetector>();
            trackSys.boardingGatePoint = gateObj.transform;

            // 2. Tạo danh sách các điểm Waypoints vòng lặp chữ U (U-Loop)
            // Lượn từ bãi xe vào -> vòng lên bên trái -> qua cổng đón -> vòng sang phải -> uốn vòng dưới
            Vector3[] loopCoords = new Vector3[]
            {
                new Vector3(-4.0f, 0f, gatePos.z - 5.0f), // WP0: Điểm nhập làn bãi đỗ xe
                new Vector3(-5.2f, 0f, gatePos.z - 2.5f), // WP1: Cua trái lên
                new Vector3(-3.2f, 0f, gatePos.z + 0.8f), // WP2: Áp sát rãnh khoét
                new Vector3( 0.0f, 0f, gatePos.z),        // WP3: Ngay tại Cổng đón khách
                new Vector3( 3.2f, 0f, gatePos.z + 0.8f), // WP4: Rời cổng đón, cua phải
                new Vector3( 5.2f, 0f, gatePos.z - 2.5f), // WP5: Vòng cua phải xuống
                new Vector3( 3.8f, 0f, gatePos.z - 5.0f), // WP6: Ngã rẽ Exit Junction
                new Vector3( 0.0f, 0f, gatePos.z - 6.5f)  // WP7: Đáy vòng U lặp lại
            };

            trackSys.loopWaypoints.Clear();
            for (int i = 0; i < loopCoords.Length; i++)
            {
                GameObject wp = new GameObject($"Loop_WP_{i}");
                wp.transform.SetParent(root.transform);
                wp.transform.position = loopCoords[i];
                trackSys.loopWaypoints.Add(wp.transform);
            }

            // Gán điểm nhập làn
            trackSys.parkingEntryWaypoint = trackSys.loopWaypoints[0];

            // Gán điểm ngã rẽ Exit Junction
            trackSys.exitJunctionPoint = trackSys.loopWaypoints[6];

            // 3. Tạo các điểm Làn Thoát Hiểm (Exit Path)
            Vector3[] exitCoords = new Vector3[]
            {
                new Vector3( 7.0f, 0f, gatePos.z - 5.0f), // Exit 0: Rẽ ngang sang phải
                new Vector3(13.0f, 0f, gatePos.z - 5.0f), // Exit 1: Tăng tốc chạy ra ngoài
                new Vector3(22.0f, 0f, gatePos.z - 5.0f)  // Exit 2: Biến mất khỏi màn hình
            };

            trackSys.exitWaypoints.Clear();
            for (int i = 0; i < exitCoords.Length; i++)
            {
                GameObject wp = new GameObject($"Exit_WP_{i}");
                wp.transform.SetParent(root.transform);
                wp.transform.position = exitCoords[i];
                trackSys.exitWaypoints.Add(wp.transform);
            }

            Debug.Log("<color=green>[TrackGeneratorHelper]</color> Đã tạo thành công Hệ Thống Đường Loop Chữ U Chuẩn!");
            return trackSys;
        }
    }
}
