#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Editor.FlowIoC.CustomFolderDrawer.Scripts
{
    [CustomEditor(typeof(ED_CustomFolderConfig))]
    public class TestScriptableEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (GUILayout.Button("Refresh", GUILayout.Height(40)))
            {
                CustomFolderDrawer.Apply();
            }
        }
    }
}
#endif