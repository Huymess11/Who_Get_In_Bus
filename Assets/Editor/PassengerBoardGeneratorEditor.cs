// File editor nay da duoc vo hieu hoa de tranh loi CS0246 khi khong co PassengerBoardGenerator
#if FALSE
using UnityEngine;
using UnityEditor;

namespace DouyinGame.Editor
{
    [CustomEditor(typeof(PassengerBoardGenerator))]
    public class PassengerBoardGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
        }
    }
}
#endif
