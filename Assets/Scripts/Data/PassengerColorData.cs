using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "PassengerColorData", menuName = "STO/PassengerColorData")]
public class PassengerColorData : SerializedScriptableObject
{
    public Dictionary<GameColorType, Sprite> data = new();

#if UNITY_EDITOR
    [Button("⚡ TỰ ĐỘNG GẮN 18 ẢNH ĐÚNG MÀU (1-CLICK)", ButtonSizes.Large), GUIColor(0.2f, 0.85f, 0.35f)]
    public void AutoAssignSprites()
    {
        data.Clear();
        string folder = "Assets/Sprite/UI/prefabs";

        Assign(GameColorType.White, $"{folder}/white.png");
        Assign(GameColorType.Green, $"{folder}/green.png");
        Assign(GameColorType.Cyan, $"{folder}/cyan.png");
        Assign(GameColorType.Blue, $"{folder}/blue.png");
        Assign(GameColorType.Brown, $"{folder}/brown.png");
        Assign(GameColorType.Purple, $"{folder}/purple.png");
        Assign(GameColorType.Powder, $"{folder}/powder.png");
        Assign(GameColorType.Yellow, $"{folder}/yellow.png");
        Assign(GameColorType.Red, $"{folder}/red.png");
        Assign(GameColorType.EmeraldGreen, $"{folder}/emerald_green.png");
        Assign(GameColorType.Orange, $"{folder}/orange.png");
        Assign(GameColorType.Black, $"{folder}/black.png");
        Assign(GameColorType.DarkGreen, $"{folder}/dark_green.png");
        Assign(GameColorType.Burgundy, $"{folder}/burgundy.png");
        Assign(GameColorType.GrayishBlue, $"{folder}/grayish_blue.png");
        Assign(GameColorType.LightPurple, $"{folder}/light_purple.png");
        Assign(GameColorType.Pink, $"{folder}/pink.png");
        Assign(GameColorType.Teal, $"{folder}/teal.png");

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        Debug.Log($"<color=green>[PassengerColorData]</color> Đã tự động gắn thành công {data.Count}/18 màu vào Sprite!");
    }

    private void Assign(GameColorType colorType, string path)
    {
        Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sp != null)
        {
            data[colorType] = sp;
        }
        else
        {
            Debug.LogWarning($"[PassengerColorData] Không tìm thấy sprite: {path}");
        }
    }
#endif
}

