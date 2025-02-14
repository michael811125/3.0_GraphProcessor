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
#if UNITY_EDITOR
using CZToolKit.GraphProcessor.Editors;
using UnityEngine.UIElements;

[CustomView(typeof(LogNode))]
public class LogNodeView : BaseNodeView
{
    Button btnDebug;

    public LogNodeView()
    {
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        btnDebug = new Button();
        btnDebug.text = "Log";
        btnDebug.clicked += OnClick;
        PortViews["Input"].Add(btnDebug);
        PortViews["Input"].PortLabel.AddToClassList("hidden");
    }

    private void OnClick()
    {
        (ViewModel as LogNodeVM).DebugInput();
    }
}
#endif