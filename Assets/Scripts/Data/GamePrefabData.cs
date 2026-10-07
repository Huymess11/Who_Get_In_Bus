using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "GamePrefabData", menuName = "STO/GamePrefabData")]
public class GamePrefabData : SerializedScriptableObject
{
    [Header("🚗 PREFAB XE CỘ (VEHICLES)")]
    [Tooltip("Xe buýt tiêu chuẩn 1 tầng (Sức chứa <= 50 chỗ)")]
    public GameObject carPrefab;

    [Tooltip("Xe buýt 2 tầng Extra Car (Sức chứa > 50 chỗ, 100c, 150c, 200c)")]
    public GameObject extraCarPrefab;

    [Header("👥 PREFAB HÀNH KHÁCH & TRANH CÁT (PASSENGERS)")]
    [Tooltip("Prefab mô hình hành khách / hạt cát 3D")]
    public GameObject passengerPrefab;

    [Tooltip("Material màu hạt cát hành khách")]
    public Material beadMaterial;

    [Header("🎨 DỮ LIỆU BẢNG MÀU CHUẨN (COLOR DATA)")]
    public BusColorData busColors;
    public PassengerColorData passengerColors;

    [Header("⚙️ CẤU HÌNH PHÂN LOẠI XE")]
    [Tooltip("Ngưỡng số ghế để chuyển sang dùng Extra Car (Xe buýt 2 tầng)")]
    public int extraCarThreshold = 51;

    /// <summary>
    /// Lấy đúng loại Prefab xe (Car 1 tầng hoặc Extra Car 2 tầng) dựa trên sức chứa
    /// </summary>
    public GameObject GetCarPrefab(int capacity)
    {
        if (capacity >= extraCarThreshold && extraCarPrefab != null)
        {
            return extraCarPrefab;
        }
        return (carPrefab != null) ? carPrefab : extraCarPrefab;
    }

#if UNITY_EDITOR
    [Button("⚡ TỰ ĐỘNG GẮN TẤT CẢ PREFABS & RESOURCES (1-CLICK)", ButtonSizes.Large), GUIColor(0.2f, 0.85f, 0.35f)]
    public void AutoAssignResources()
    {
        carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Car.prefab");
        extraCarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Extra Car.prefab");
        passengerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Passenger.prefab");
        beadMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Mat_PassengerBead.mat");
        busColors = AssetDatabase.LoadAssetAtPath<BusColorData>("Assets/Data/BusColorData.asset");
        passengerColors = AssetDatabase.LoadAssetAtPath<PassengerColorData>("Assets/Data/PassengerColorData.asset");

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        Debug.Log("<color=green>[GamePrefabData]</color> Đã tự động gắn đầy đủ Car, Extra Car, Passenger và Color Data!");
    }

    [MenuItem("Tools/Tạo & Gắn GamePrefabData Asset", false, 3)]
    public static void CreateOrUpdateAsset()
    {
        string assetPath = "Assets/Data/GamePrefabData.asset";
        var asset = AssetDatabase.LoadAssetAtPath<GamePrefabData>(assetPath);
        if (asset == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data"))
            {
                AssetDatabase.CreateFolder("Assets", "Data");
            }
            asset = CreateInstance<GamePrefabData>();
            AssetDatabase.CreateAsset(asset, assetPath);
            Debug.Log($"<color=green>[GamePrefabData]</color> Đã tạo mới file asset tại: {assetPath}");
        }

        asset.AutoAssignResources();
        Selection.activeObject = asset;
    }
#endif
}
