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
        string folder = "Assets/Sprites/3D_Passengers";

        Assign(GameColorType.White, $"{folder}/3D_Passenger_16_White.png");
        Assign(GameColorType.Green, $"{folder}/3D_Passenger_08_GreenTeal.png");
        Assign(GameColorType.Cyan, $"{folder}/3D_Passenger_04_Cyan.png");
        Assign(GameColorType.Blue, $"{folder}/3D_Passenger_01_Blue.png");
        Assign(GameColorType.Brown, $"{folder}/3D_Passenger_02_Brown.png");
        Assign(GameColorType.Purple, $"{folder}/3D_Passenger_13_Purple.png");
        Assign(GameColorType.Powder, $"{folder}/3D_Passenger_12_Powder.png");
        Assign(GameColorType.Yellow, $"{folder}/3D_Passenger_17_Yellow.png");
        Assign(GameColorType.Red, $"{folder}/3D_Passenger_14_Red.png");
        Assign(GameColorType.EmeraldGreen, $"{folder}/3D_Passenger_06_LimeGreen.png");
        Assign(GameColorType.Orange, $"{folder}/3D_Passenger_10_Orange.png");
        Assign(GameColorType.Black, $"{folder}/3D_Passenger_00_Black.png");
        Assign(GameColorType.DarkGreen, $"{folder}/3D_Passenger_05_DarkGreen.png");
        Assign(GameColorType.Burgundy, $"{folder}/3D_Passenger_03_Burgundy.png");
        Assign(GameColorType.GrayishBlue, $"{folder}/3D_Passenger_07_GrayishBlue.png");
        Assign(GameColorType.LightPurple, $"{folder}/3D_Passenger_09_LightPurple.png");
        Assign(GameColorType.Pink, $"{folder}/3D_Passenger_11_Pink.png");
        Assign(GameColorType.Teal, $"{folder}/3D_Passenger_15_Teal.png");

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

