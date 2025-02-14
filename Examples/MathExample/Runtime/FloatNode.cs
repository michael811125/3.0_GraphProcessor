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

[NodeTooltip("浮点数节点")]
[NodeMenu("Float")]
public class FloatNode : BaseNode
{
    public float num;
}

[ViewModel(typeof(FloatNode))]
public class FloatNodeVM : BaseNodeVM, IGetValueFromPort, IGetValueFromPort<float>
{
    public FloatNode T_Model
    {
        get;
    }

    public float Value
    {
        get { return GetPropertyValue<float>(nameof(FloatNode.num)); }
        set { SetPropertyValue(nameof(FloatNode.num), value); }
    }

    public FloatNodeVM(FloatNode model) : base(model)
    {
        T_Model = model;
        this[nameof(FloatNode.num)] = new BindableProperty<float>(() => T_Model.num, v => T_Model.num = v);
        AddPort(new BasePortVM("Output", BasePort.Orientation.Horizontal, BasePort.Direction.Output, BasePort.Capacity.Multi, typeof(float))
        {
            HideLabel = true
        });
    }

    public object GetValue(string port)
    {
        return T_Model.num;
    }

    float IGetValueFromPort<float>.GetValue(string port)
    {
        return T_Model.num;
    }
}
