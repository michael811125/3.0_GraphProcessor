#region 注 释
/***
 *
 *  Title:
 *  
 *  Description:
 *  
 *  Date:
 *  Version:
 *  Writer: 半只龙虾人
 *  Github: https://github.com/HalfLobsterMan
 *  Blog: https://www.crosshair.top/
 *
 */
#endregion
using CZToolKit.Core.Editors;
using CZToolKit.GraphProcessor.Internal;
using UnityEditor;
using UnityEngine;

namespace CZToolKit.GraphProcessor.Editors
{
    [CustomEditor(typeof(InternalGraphOwner), true)]
    public class GraphOwnerEditor : BasicEditor
    {
        protected override void RegisterDrawers()
        {
            base.RegisterDrawers();
            RegisterDrawer("serializedVariables", DrawSerialziedVaraibles);
            RegisterDrawer("variablesUnityReference", property =>
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.PropertyField(property);
                EditorGUI.EndDisabledGroup();
            });
            RegisterDrawer("serializedGraph", DrawSerialziedVaraibles);
            RegisterDrawer("graphUnityReferences", property =>
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.PropertyField(property);
                EditorGUI.EndDisabledGroup();
            });
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (GUILayout.Button("Edit", GUILayout.Height(30)))
                BaseGraphWindow.Open(target as IGraphAsset);
        }

        private void DrawSerialziedVaraibles(SerializedProperty property)
        {
            var @bool = GUIHelper.GetContextData(property.displayName, false);
            @bool.value = EditorGUILayout.BeginFoldoutHeaderGroup(@bool.value, property.displayName);

            if (@bool.value)
                GUILayout.TextArea(property.stringValue, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }
}
