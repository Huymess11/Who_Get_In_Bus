using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace WhoGetInBus.EditorTools
{
    [InitializeOnLoad]
    public static class BusContainerSetupTool
    {
        static BusContainerSetupTool()
        {
            EditorApplication.delayCall += CheckAndSetupBusContainerOnLoad;
        }

        private static void CheckAndSetupBusContainerOnLoad()
        {
            GameObject busContainer = GameObject.Find("BusContainer");
            if (busContainer == null) return;

            // Kiểm tra xem trong BusContainer có còn chứa đối tượng con bắt đầu bằng "Car" không
            bool hasCarChild = false;
            for (int i = 0; i < busContainer.transform.childCount; i++)
            {
                if (busContainer.transform.GetChild(i).name.StartsWith("Car"))
                {
                    hasCarChild = true;
                    break;
                }
            }

            if (hasCarChild)
            {
                Debug.Log("<color=yellow>[BusContainerSetupTool]</color> Phát hiện các Car cũ trong BusContainer. Tự động chuyển đổi sang các 'bus point' tĩnh chuẩn...");
                SetupBusContainer(false);
            }
        }

        [MenuItem("Tools/🚌 Thiết Lập BusContainer (Thay Xe Bằng Bus Point Tĩnh)", false, 11)]
        public static void MenuSetupBusContainer()
        {
            SetupBusContainer(true);
        }

        public static void SetupBusContainer(bool showDialog)
        {
            GameObject busContainer = GameObject.Find("BusContainer");
            if (busContainer == null)
            {
                if (showDialog) EditorUtility.DisplayDialog("Lỗi", "Không tìm thấy GameObject 'BusContainer' trong Scene hiện tại!", "OK");
                return;
            }

            // 1. Thu thập và xóa các đối tượng Car con cũ
            var toDestroy = new List<GameObject>();
            for (int i = 0; i < busContainer.transform.childCount; i++)
            {
                toDestroy.Add(busContainer.transform.GetChild(i).gameObject);
            }

            foreach (var obj in toDestroy)
            {
                Undo.DestroyObjectImmediate(obj);
            }

            // 2. Tạo 6 object tĩnh 'bus point' (bus point, bus point (1), ... bus point (5))
            for (int i = 0; i < 6; i++)
            {
                string pName = i == 0 ? "bus point" : $"bus point ({i})";
                GameObject point = new GameObject(pName);
                Undo.RegisterCreatedObjectUndo(point, "Create bus point");
                point.transform.SetParent(busContainer.transform, false);
                point.transform.localPosition = Vector3.zero;
                point.transform.localRotation = Quaternion.identity;
                point.transform.localScale = Vector3.one;

                // Mặc định 4 điểm đầu active, 2 điểm sau inactive
                point.SetActive(i < 4);
            }

            // 3. Cấu hình WorldSpaceLayout trên BusContainer
            var layout = busContainer.GetComponent<WorldSpaceLayout>();
            if (layout == null)
            {
                layout = Undo.AddComponent<WorldSpaceLayout>(busContainer);
            }

            layout.direction = WorldSpaceLayout.LayoutDirection.X;
            layout.spacing4OrLess = 0.5f;
            layout.spacing5 = 0.35f;
            layout.spacing6 = 0.15f;
            layout.defaultItemSize = 0.87129f;
            layout.useBounds = true;
            layout.centerAlign = true;
            layout.includeChildrenBounds = true;

            // Thiết lập mặc định cho 4 hàng/cột xe (spacing = 0.5f)
            layout.SetupForColumnCount(4);

            EditorUtility.SetDirty(layout);
            EditorUtility.SetDirty(busContainer);
            EditorSceneManager.MarkSceneDirty(busContainer.scene);

            Debug.Log("<color=green>[BusContainerSetupTool]</color> <b>ĐÃ THAY THẾ CAR THÀNH 6 BUS POINT TĨNH THÀNH CÔNG!</b>\n" +
                      "- 1, 2, 3, 4 hàng xe: Spacing = 0.5\n" +
                      "- 5 hàng xe: Spacing = 0.35\n" +
                      "- 6 hàng xe: Spacing = 0.15\n" +
                      "- DefaultItemSize: 0.87129 (khớp chuẩn độ rộng xe và căn giữa)");

            if (showDialog)
            {
                EditorUtility.DisplayDialog("Thành Công", "Đã thay thế toàn bộ Car bằng 6 'bus point' tĩnh và cấu hình Spacing động cho WorldSpaceLayout!", "OK");
            }
        }
    }
}
