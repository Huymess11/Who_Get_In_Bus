using UnityEngine;
using UnityEditor;

namespace DouyinGame.Editor
{
    [CustomEditor(typeof(PassengerBoardGenerator))]
    public class PassengerBoardGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PassengerBoardGenerator gen = (PassengerBoardGenerator)target;

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Thao Tác Sinh Bảng Hạt", EditorStyles.boldLabel);

            GUI.backgroundColor = new Color(0.2f, 0.8f, 1f);
            if (GUILayout.Button("► 1. SINH BẢNG HẠT (GENERATE BOARD)", GUILayout.Height(40)))
            {
                gen.GenerateBoard();
                EditorUtility.SetDirty(gen);
            }

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            EditorGUILayout.Space(5);
            if (GUILayout.Button("✕ 2. XÓA BẢNG HẠT (CLEAR)", GUILayout.Height(30)))
            {
                gen.ClearBoard();
                EditorUtility.SetDirty(gen);
            }
            GUI.backgroundColor = Color.white;
        }
    }

    internal static class EditorStylesExt
    {
        public static void LabelMeshHeader(string text)
        {
            GUILayout.Label(text, EditorStyles.boldLabel);
        }
    }
}
