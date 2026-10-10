using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "BusColorData", menuName = "STO/BusColorData")]
public class BusColorData : SerializedScriptableObject
{
    public Dictionary<GameColorType, Material> data = new();

#if UNITY_EDITOR
    [Button("⚡ TỰ ĐỘNG GẮN 18 MATERIAL ĐÚNG MÀU (1-CLICK)", ButtonSizes.Large), GUIColor(0.2f, 0.85f, 0.35f)]
    public void AutoAssignMaterials()
    {
        data.Clear();
        string folder = "Assets/Material/Bus";

        Assign(GameColorType.White, $"{folder}/white.mat");
        Assign(GameColorType.Green, $"{folder}/green.mat");
        Assign(GameColorType.Cyan, $"{folder}/cyan.mat");
        Assign(GameColorType.Blue, $"{folder}/blue.mat");
        Assign(GameColorType.Brown, $"{folder}/brown.mat");
        Assign(GameColorType.Purple, $"{folder}/purple.mat");
        Assign(GameColorType.Powder, $"{folder}/powder.mat");
        Assign(GameColorType.Yellow, $"{folder}/yellow.mat");
        Assign(GameColorType.Red, $"{folder}/red.mat");
        Assign(GameColorType.EmeraldGreen, $"{folder}/emerald_green.mat");
        Assign(GameColorType.Orange, $"{folder}/orange.mat");
        Assign(GameColorType.Black, $"{folder}/black.mat");
        Assign(GameColorType.DarkGreen, $"{folder}/dark_green.mat");
        Assign(GameColorType.Burgundy, $"{folder}/burgundy.mat");
        Assign(GameColorType.GrayishBlue, $"{folder}/grayish_blue.mat");
        Assign(GameColorType.LightPurple, $"{folder}/light_purple.mat");
        Assign(GameColorType.Pink, $"{folder}/pink.mat");
        Assign(GameColorType.Teal, $"{folder}/teal.mat");

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        Debug.Log($"<color=green>[BusColorData]</color> Đã tự động gắn thành công {data.Count}/18 Material vào BusColorData!");
    }

    private void Assign(GameColorType colorType, string path)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null)
        {
            data[colorType] = mat;
        }
        else
        {
            Debug.LogWarning($"[BusColorData] Không tìm thấy Material tại: {path}");
        }
    }
#endif
}

