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
using CZToolKit.Core.ViewModel;
using CZToolKit.GraphProcessor;
using UnityEngine;

[NodeMenu("Log")]
public class LogNode : BaseNode { }

[ViewModel(typeof(LogNode))]
public class LogNodeVM : BaseNodeVM
{
    public LogNodeVM(BaseNode model) : base(model)
    {
        AddPort(new BasePortVM("Input", BasePort.Orientation.Horizontal, BasePort.Direction.Input, BasePort.Capacity.Single));
    }

    public void DebugInput()
    {
        Debug.Log(Ports["Input"].GetConnectionValue());
    }
}
