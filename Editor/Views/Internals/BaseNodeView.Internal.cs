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
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

using NodeView = UnityEditor.Experimental.GraphView.Node;

namespace CZToolKit.GraphProcessor.Editors
{
    public partial class BaseNodeView : NodeView, IBindableView<BaseNodeVM>
    {
        #region 字段
        
        public readonly Label titleLabel;
        public readonly VisualElement contents;
        public readonly VisualElement nodeBorder;
        public readonly VisualElement topPortContainer;
        public readonly VisualElement bottomPortContainer;
        public readonly VisualElement controlsContainer;
        public readonly VisualElement contentsHorizontalDivider;
        public readonly VisualElement portsVerticalDivider;
        public readonly VisualElement controlsHorizontalDivider;

        List<IconBadge> badges = new List<IconBadge>();
        Dictionary<string, BasePortView> portViews = new Dictionary<string, BasePortView>();
        #endregion

        #region 属性
        
        public Label TitleLabel
        {
            get
            {
                return titleLabel;
            }
        }
        public BaseGraphView Owner
        {
            get;
            private set;
        }
        public BaseNodeVM ViewModel
        {
            get;
            protected set;
        }
        public IReadOnlyDictionary<string, BasePortView> PortViews
        {
            get { return portViews; }
        }
        #endregion

        public BaseNodeView() : base()
        {
            contents = mainContainer.Q("contents");
            
            nodeBorder = this.Q(name: "node-border");
            titleLabel = titleContainer.Q<Label>("title-label");
            contentsHorizontalDivider = contentContainer.Q(name: "divider", className: "horizontal");
            portsVerticalDivider = topContainer.Q(name: "divider", className: "vertical");

            controlsContainer = new VisualElement { name = "controls" };
            contents.Add(controlsContainer);

            topPortContainer = new VisualElement { name = "top-input" };
            nodeBorder.Insert(0, topPortContainer);

            bottomPortContainer = new VisualElement { name = "bottom-input" };
            nodeBorder.Add(bottomPortContainer);
            
            styleSheets.Add(GraphProcessorStyles.BaseNodeViewStyle);
        }

        #region Initialize
        public void SetUp(BaseNodeVM node, BaseGraphView graphView)
        {
            ViewModel = node;
            Owner = graphView;

            // 初始化
            base.SetPosition(new Rect(ViewModel.Position.ToVector2(), GetPosition().size));
            title = ViewModel.Title;
            tooltip = ViewModel.Tooltip;
            if (ViewModel.ContainsKey(BaseNodeVM.TITLE_COLOR_NAME))
            {
                titleContainer.style.backgroundColor = ViewModel.TitleColor.ToColor();
                TitleLabel.style.color = ViewModel.TitleColor.ToColor().GetLuminance() > 0.5f && ViewModel.TitleColor.a > 0.5f ? Color.black : Color.white * 0.9f;
            }
            
            foreach (var port in ViewModel.Ports.Values)
            {
                BasePortView portView = NewPortView(port);
                portView.SetUp(port, Owner);
                portViews[port.Name] = portView;

                if (portView.orientation == Orientation.Horizontal)
                {
                    if (portView.direction == Direction.Input)
                        inputContainer.Add(portView);
                    else
                        outputContainer.Add(portView);
                }
                else
                {
                    if (portView.direction == Direction.Input)
                        topPortContainer.Add(portView);
                    else
                        bottomPortContainer.Add(portView);
                }
            }
            RefreshPorts();
            RefreshContentsHorizontalDivider();
            RefreshPortContainer();
            OnInitialized();
        }

        public void BindingProperties()
        {
            ViewModel.BindingProperty<InternalVector2>(nameof(BaseNode.position), OnPositionChanged);
            ViewModel.BindingProperty<string>(BaseNodeVM.TITLE_NAME, OnTitleChanged);
            if (ViewModel.ContainsKey(BaseNodeVM.TITLE_COLOR_NAME))
                ViewModel.BindingProperty<InternalColor>(BaseNodeVM.TITLE_COLOR_NAME, OnTitleColorChanged);
            ViewModel.BindingProperty<string>(BaseNodeVM.TOOLTIP_NAME, OnTooltipChanged);

            ViewModel.onPortAdded += OnPortAdded;
            ViewModel.onPortRemoved += OnPortRemoved;

            foreach (var portView in portViews.Values)
            {
                portView.BindingProperties();
            }

            OnBindingProperties();
        }

        public void UnBindingProperties()
        {
            ViewModel.UnBindingProperty<string>(BaseNodeVM.TITLE_NAME, OnTitleChanged);
            if (ViewModel.ContainsKey(BaseNodeVM.TITLE_COLOR_NAME))
                ViewModel.UnBindingProperty<InternalColor>(BaseNodeVM.TITLE_COLOR_NAME, OnTitleColorChanged);
            ViewModel.UnBindingProperty<string>(BaseNodeVM.TOOLTIP_NAME, OnTooltipChanged);
            ViewModel.UnBindingProperty<InternalVector2>(nameof(BaseNode.position), OnPositionChanged);

            ViewModel.onPortAdded -= OnPortAdded;
            ViewModel.onPortRemoved -= OnPortRemoved;

            foreach (var portView in portViews.Values)
            {
                portView.UnBindingProperties();
            }

            OnUnBindingProperties();
        }
        #endregion

        #region Callbacks
        void OnPortAdded(BasePortVM port)
        {
            AddPortView(port);
            RefreshPorts();
            RefreshContentsHorizontalDivider();
            RefreshPortContainer();
        }

        void OnPortRemoved(BasePortVM port)
        {
            RemovePortView(port);
            RefreshPorts();
            RefreshContentsHorizontalDivider();
            RefreshPortContainer();
        }

        void OnTitleChanged(string title)
        {
            base.title = title;
        }

        void OnTooltipChanged(string tooltip)
        {
            base.tooltip = tooltip;
        }

        void OnPositionChanged(InternalVector2 position)
        {
            base.SetPosition(new Rect(position.ToVector2(), GetPosition().size));
            Owner.SetDirty();
        }

        void OnTitleColorChanged(InternalColor color)
        {
            titleContainer.style.backgroundColor = color.ToColor();
            TitleLabel.style.color = color.ToColor().GetLuminance() > 0.5f && color.a > 0.5f ? Color.black : Color.white * 0.9f;
        }
        #endregion

        public void SetDeletable(bool deletable)
        {
            if (deletable)
                capabilities |= Capabilities.Deletable;
            else
                capabilities &= ~Capabilities.Deletable;
        }

        public void SetMovable(bool movable)
        {
            if (movable)
                capabilities = capabilities | Capabilities.Movable;
            else
                capabilities = capabilities & (~Capabilities.Movable);
        }

        public void SetSelectable(bool selectable)
        {
            if (selectable)
                capabilities |= Capabilities.Selectable;
            else
                capabilities &= ~Capabilities.Selectable;
        }

        void AddPortView(BasePortVM port)
        {
            BasePortView portView = NewPortView(port);
            portView.SetUp(port, Owner);
            portView.BindingProperties();
            portViews[port.Name] = portView;

            if (portView.orientation == Orientation.Horizontal)
            {
                if (portView.direction == Direction.Input)
                    inputContainer.Add(portView);
                else
                    outputContainer.Add(portView);
            }
            else
            {
                if (portView.direction == Direction.Input)
                    topPortContainer.Add(portView);
                else
                    bottomPortContainer.Add(portView);
            }
        }

        void RemovePortView(BasePortVM port)
        {
            portViews[port.Name].RemoveFromHierarchy();
            portViews[port.Name].UnBindingProperties();
            portViews.Remove(port.Name);
        }

        void RefreshContentsHorizontalDivider()
        {
            if (portViews.Values.FirstOrDefault(port => port.orientation == Orientation.Horizontal) != null)
                contentsHorizontalDivider.RemoveFromClassList("hidden");
            else
                contentsHorizontalDivider.AddToClassList("hidden");
        }

        void RefreshPortContainer()
        {
            if (topPortContainer.childCount > 0)
                topPortContainer.RemoveFromClassList("hidden");
            else
                topPortContainer.AddToClassList("hidden");

            if (bottomPortContainer.childCount > 0)
                bottomPortContainer.RemoveFromClassList("hidden");
            else
                bottomPortContainer.AddToClassList("hidden");
        }
    }
}
#endif