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

#if UNITY_EDITOR && ODIN_INSPECTOR
using CZToolKit.Core.Editors;
using Sirenix.OdinInspector.Editor;
using UnityEditor;

namespace CZToolKit.GraphProcessor.Editors
{
    [CustomObjectEditor(typeof(BaseNodeView))]
    public class BaseNodeInspector : ObjectEditor
    {
        PropertyTree propertyTree;

        public override void OnEnable()
        {
            if (Target == null)
            {
                base.OnEnable();
                return;
            }

            var view = Target as BaseNodeView;
            if (view == null || view.ViewModel == null)
                return;

            propertyTree = PropertyTree.Create(view.ViewModel.Model);
            propertyTree.DrawMonoScriptObjectField = true;
            foreach (var property in propertyTree.EnumerateTree(false, true))
            {
                if (property.ValueEntry == null)
                    continue;
                property.ValueEntry.OnValueChanged += (i) =>
                {
                    if (view.ViewModel.TryGetValue(property.Name, out var bindableProperty))
                        bindableProperty.SetValueWithNotify(property.ValueEntry.WeakSmartValue);
                };
            }
        }

        public override void OnInspectorGUI()
        {
            var view = Target as BaseNodeView;
            if (view == null || view.ViewModel == null)
                return;
            if (propertyTree == null)
                return;
            propertyTree.Draw(false);
            Editor.Repaint();
        }

        public override void OnDisable()
        {
            base.OnDisable();
            propertyTree?.Dispose();
        }
    }
}
#endif