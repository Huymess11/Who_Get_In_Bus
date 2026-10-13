using UnityEditor;
using UnityEngine;
using WhoGetInBus.Data;

namespace WhoGetInBus.EditorTools
{
    [InitializeOnLoad]
    public static class RoadEnvironmentAssetCreator
    {
        static RoadEnvironmentAssetCreator()
        {
            EditorApplication.delayCall += CheckAndCreateAsset;
        }

        private static void CheckAndCreateAsset()
        {
            string assetPath = "Assets/Data/RoadEnvironmentData.asset";
            RoadEnvironmentData data = AssetDatabase.LoadAssetAtPath<RoadEnvironmentData>(assetPath);
            if (data == null)
            {
                CreateAndPopulateAsset();
            }
        }

        [MenuItem("Tools/Who Get In Bus/⚡ Tạo & Gắn RoadEnvironmentData")]
        public static void CreateAndPopulateAsset()
        {
            string assetPath = "Assets/Data/RoadEnvironmentData.asset";
            RoadEnvironmentData data = AssetDatabase.LoadAssetAtPath<RoadEnvironmentData>(assetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<RoadEnvironmentData>();
                AssetDatabase.CreateAsset(data, assetPath);
            }

            data.AutoAssignRoadSprites();

            // Tự động gán vào GamePrefabData
            var prefabData = AssetDatabase.LoadAssetAtPath<GamePrefabData>("Assets/Data/GamePrefabData.asset");
            if (prefabData != null)
            {
                prefabData.roadEnvironmentData = data;
                EditorUtility.SetDirty(prefabData);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green><b>[RoadEnvironmentAssetCreator]</b></color> Đã tạo và cấu hình thành công RoadEnvironmentData.asset!");
        }
    }
}
