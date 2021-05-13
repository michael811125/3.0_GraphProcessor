﻿using CZToolKit.Core;
using CZToolKit.Core.Editors;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Linq;
using System.Collections.Generic;

using Blackboard = UnityEditor.Experimental.GraphView.Blackboard;

namespace CZToolKit.GraphProcessor.Editors
{
    public class BaseGraphView : GraphView
    {
        const string GraphViewStylePath = "GraphProcessor/Styles/BaseGraphView";
        static StyleSheet graphViewStyle;
        public static StyleSheet GraphViewStyle
        {
            get
            {
                if (graphViewStyle == null)
                    graphViewStyle = Resources.Load<StyleSheet>(GraphViewStylePath);
                return graphViewStyle;
            }
        }

        ExposedParameterView blackboard;

        public BaseEdgeConnectorListener connectorListener;

        List<IOnGUIObserver> onGUIObservers = new List<IOnGUIObserver>(16);

        protected virtual Type GetDefaultNodeViewType(Type _nodeDataType) { return typeof(BaseNodeView); }

        public bool Initialized { get; private set; }
        public bool IsDirty { get; private set; }
        public CreateNodeMenuWindow CreateNodeMenu { get; private set; }
        public BaseGraphWindow GraphWindow { get; private set; }
        public BaseGraph GraphData { get; private set; }
        public SerializedObject SerializedObject { get; private set; }
        public Dictionary<string, BaseNodeView> NodeViews { get; private set; } = new Dictionary<string, BaseNodeView>();
        public List<EdgeView> EdgeViews { get; private set; } = new List<EdgeView>();
        public List<GroupView> GroupViews { get; private set; } = new List<GroupView>();
        public Dictionary<string, BaseStackNodeView> StackNodeViews { get; private set; } = new Dictionary<string, BaseStackNodeView>();
        public List<IOnGUIObserver> OnGUIObservers { get { return onGUIObservers; } }

        protected override bool canCopySelection
        {
            get { return selection.Any(e => e is BaseNodeView || e is GroupView || e is BaseStackNodeView); }
        }

        protected override bool canCutSelection
        {
            get { return selection.Any(e => e is BaseNodeView || e is GroupView || e is BaseStackNodeView); }
        }

        public BaseGraphView()
        {
            styleSheets.Add(GraphViewStyle);
            GridBackground gridBackground = new GridBackground();
            gridBackground.style.backgroundColor = new Color(1f, 1f, 1f);
            Insert(0, gridBackground);
            SetupZoom(0.05f, 2f);
            this.StretchToParentSize();
        }

        #region Initialize
        protected virtual BaseEdgeConnectorListener CreateEdgeConnectorListener()
        {
            return new BaseEdgeConnectorListener(this);
        }

        public void Initialize(BaseGraphWindow _window, BaseGraph _graphData)
        {
            if (Initialized) return;
            GraphWindow = _window;
            GraphData = _graphData;
            SerializedObject = new SerializedObject(GraphData);
            GraphWindow.Toolbar.AddButton("Center", ResetPositionAndZoom);
            GraphWindow.Toolbar.AddToggle("Show Parameters", GraphData.blackboardoVisible, (v) =>
            {
                GetBlackboard().style.display = v ? DisplayStyle.Flex : DisplayStyle.None;
                GraphData.blackboardoVisible = v;
            });

            connectorListener = CreateEdgeConnectorListener();

            double time = EditorApplication.timeSinceStartup;
            Add(new IMGUIContainer(() =>
            {
                if (IsDirty && EditorApplication.timeSinceStartup > time && GraphData != null)
                {
                    IsDirty = false;
                    EditorUtility.SetDirty(GraphData);
                    time += 1;
                }
            }));
            InitViewAndCallbacks();
            InitializeGraphView();
            InitializeNodeViews();
            InitializeEdgeViews();
            InitializeStackNodes();
            InitializeGroups();
            InitializeBlackboard();

            OnInitialized();
            Initialized = true;
        }

        protected virtual void OnInitialized() { }

        void InitViewAndCallbacks()
        {
            serializeGraphElements = SerializeGraphElementsCallback;
            canPasteSerializedData = CanPasteSerializedDataCallback;
            unserializeAndPaste = DeserializeAndPasteCallback;
            graphViewChanged = GraphViewChangedCallback;
            viewTransformChanged = ViewTransformChangedCallback;

            EditorSceneManager.sceneSaved += _ => SaveGraphToDisk();

            RegisterCallback<KeyDownEvent>(KeyDownCallback);
            RegisterCallback<DragPerformEvent>(DragPerformedCallback);
            RegisterCallback<DragUpdatedEvent>(DragUpdatedCallback);
            RegisterCallback<MouseDownEvent>(MouseDownCallback);
            RegisterCallback<MouseUpEvent>(MouseUpCallback);

            InitializeManipulators();

            CreateNodeMenu = ScriptableObject.CreateInstance<CreateNodeMenuWindow>();
            CreateNodeMenu.Initialize(this, GetNodeTypes());
        }

        protected virtual void InitializeManipulators()
        {
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
        }

        void InitializeGraphView()
        {
            viewTransform.position = GraphData.position;
            viewTransform.scale = GraphData.scale;
            nodeCreationRequest = (c) => SearchWindow.Open(new SearchWindowContext(c.screenMousePosition), CreateNodeMenu);
        }

        /// <summary> 初始化所有节点视图 </summary>
        void InitializeNodeViews()
        {
            foreach (var node in GraphData.NodesGUIDMapping)
            {
                if (node.Value == null) continue;
                AddNodeView(node.Value);
            }
        }

        /// <summary> 初始化所有连接的视图 </summary>
        void InitializeEdgeViews()
        {
            foreach (var serializedEdge in GraphData.EdgesGUIDMapping)
            {
                if (serializedEdge.Value == null) continue;
                BaseNodeView inputNodeView = null, outputNodeView = null;
                if (serializedEdge.Value.InputNode != null)
                    NodeViews.TryGetValue(serializedEdge.Value.InputNodeGUID, out inputNodeView);
                if (serializedEdge.Value.OutputNode != null)
                    NodeViews.TryGetValue(serializedEdge.Value.OutputNodeGUID, out outputNodeView);
                if (inputNodeView == null || outputNodeView == null)
                    continue;
                ConnectView(inputNodeView.PortViews[serializedEdge.Value.InputFieldName], outputNodeView.PortViews[serializedEdge.Value.OutputFieldName], serializedEdge.Value);
            }
        }

        /// <summary> 初始化所有Group的视图 </summary>
        void InitializeGroups()
        {
            foreach (var group in GraphData.Groups)
                AddGroupView(group);
        }

        void InitializeStackNodes()
        {
            foreach (var stackNode in GraphData.StackNodesGUIDMapping.Values)
                AddStackNodeView(stackNode);
        }

        void InitializeBlackboard()
        {
            blackboard = new ExposedParameterView(this);
            blackboard.SetPosition(GraphData.blackboardPosition);
            blackboard.style.display = GraphData.blackboardoVisible ? DisplayStyle.Flex : DisplayStyle.None;
            Add(blackboard);
        }

        #endregion

        public virtual void OnGUI()
        {
            foreach (var observer in OnGUIObservers)
                observer.OnGUI();
        }

        public override Blackboard GetBlackboard() { return blackboard; }

        protected virtual IEnumerable<Type> GetNodeTypes()
        {
            return Utility.GetChildrenTypes<BaseNode>();
        }

        #region Callbacks

        #region 系统回调
        /// <summary> 构建右键菜单 </summary>
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            Vector2 position = (evt.currentTarget as VisualElement).ChangeCoordinatesTo(contentViewContainer, evt.localMousePosition);
            evt.menu.AppendAction("New Stack", (e) =>
            {
                BaseStackNode stackNode = new BaseStackNode(position);
                stackNode.OnCreated();
                AddStackNode(stackNode);
            }, DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("New Group", (e) => AddSelectionsToGroup(AddGroup(new BaseGroup("New Group", position))), DropdownMenuAction.AlwaysEnabled);

            evt.menu.AppendAction("Select Asset", (e) => EditorGUIUtility.PingObject(GraphData), DropdownMenuAction.AlwaysEnabled);

            base.BuildContextualMenu(evt);

            evt.menu.AppendAction("Save Asset", (e) =>
            {
                SetDirty();
                AssetDatabase.SaveAssets();
            }, DropdownMenuAction.AlwaysEnabled);

            evt.menu.AppendAction("Help/Reset Blackboard Windows", e =>
            {
                blackboard.SetPosition(new Rect(Vector2.zero, BaseGraph.DefaultBlackboardSize));
            });
        }

        /// <summary> 获取兼容接口 </summary>
        public override List<Port> GetCompatiblePorts(Port _startPortView, NodeAdapter _nodeAdapter)
        {
            List<Port> compatiblePorts = new List<Port>();

            PortView startPortView = _startPortView as PortView;

            ports.ForEach((Action<Port>)(_portView =>
            {
                PortView portView = _portView as PortView;

                if (portView.Owner == startPortView.Owner)
                    return;

                if (portView.direction == startPortView.direction)
                    return;

                if (portView.Edges.Any(e => e.input == startPortView || e.output == startPortView))
                    return;

                if (startPortView.PortData.TypeConstraint == PortTypeConstraint.None || portView.PortData.TypeConstraint == PortTypeConstraint.None)
                {
                    compatiblePorts.Add(_portView);
                    return;
                }
                if (startPortView.PortData.TypeConstraint == PortTypeConstraint.Inherited && startPortView.PortData.DisplayType.IsAssignableFrom((Type)portView.PortData.DisplayType))
                {
                    compatiblePorts.Add(_portView);
                    return;
                }
                if (startPortView.PortData.TypeConstraint == PortTypeConstraint.Strict && startPortView.PortData.DisplayType == portView.PortData.DisplayType)
                {
                    compatiblePorts.Add(_portView);
                    return;
                }
            }));
            return compatiblePorts;
        }
        #endregion

        string SerializeGraphElementsCallback(IEnumerable<GraphElement> elements)
        {
            var data = new CopyPasteHelper();

            foreach (var element in elements)
            {
                if (element is BaseNodeView nodeView)
                {
                    data.copiedNodes.Add(JsonSerializer.Serialize(nodeView.NodeData));
                    continue;
                }

                if (element is EdgeView edgeView)
                {
                    data.copiedEdges.Add(JsonSerializer.Serialize(edgeView.serializedEdge));
                    continue;
                }

                if (element is BaseStackNodeView stackNodeView)
                {
                    data.copiedStacks.Add(JsonSerializer.Serialize(stackNodeView.stackNode));
                    continue;
                }

                if (element is GroupView groupView)
                {
                    data.copiedGroups.Add(JsonSerializer.Serialize(groupView.GroupData));
                    continue;
                }
            }
            ClearSelection();
            return JsonUtility.ToJson(data, true);
        }

        bool CanPasteSerializedDataCallback(string serializedData)
        {
            try { return JsonUtility.FromJson(serializedData, typeof(CopyPasteHelper)) != null; }
            catch { return false; }
        }

        void DeserializeAndPasteCallback(string operationName, string serializedData)
        {
            RegisterCompleteObjectUndo(operationName);
            var data = JsonUtility.FromJson<CopyPasteHelper>(serializedData);
            Dictionary<string, BaseNode> copiedNodesMap = new Dictionary<string, BaseNode>();
            foreach (var serializedNode in data.copiedNodes)
            {
                var node = JsonSerializer.Deserialize(serializedNode) as BaseNode;
                if (node == null)
                    continue;
                node.position.position += new Vector2(20, 20);
                string sourceGUID = node.GUID;
                // 新节点重置id
                node.OnCreated();
                // 新节点与旧id存入字典
                copiedNodesMap[sourceGUID] = node;
                AddNode(node);
                AddToSelection(NodeViews[node.GUID]);
            }

            foreach (var serializedGroup in data.copiedGroups)
            {
                var group = JsonSerializer.Deserialize<BaseGroup>(serializedGroup);
                group.position.position += new Vector2(20, 20);

                var oldGUIDList = group.innerNodeGUIDs.ToList();
                group.innerNodeGUIDs.Clear();

                foreach (var guid in oldGUIDList)
                {
                    if (copiedNodesMap.TryGetValue(guid, out var node))
                        group.innerNodeGUIDs.Add(node.GUID);
                }
                AddGroup(group);
            }

            foreach (var serializedEdge in data.copiedEdges)
            {
                var edge = JsonSerializer.Deserialize<SerializableEdge>(serializedEdge);

                copiedNodesMap.TryGetValue(edge.InputNodeGUID, out var inputNode);
                copiedNodesMap.TryGetValue(edge.OutputNodeGUID, out var outputNode);

                inputNode = inputNode ?? edge.InputNode;
                outputNode = outputNode ?? edge.OutputNode;
                if (inputNode == null || outputNode == null) continue;

                inputNode.TryGetPort(edge.InputFieldName, out NodePort inputPort);
                outputNode.TryGetPort(edge.OutputFieldName, out NodePort outputPort);
                if (!inputPort.IsMulti && inputPort.IsConnected) continue;
                if (!outputPort.IsMulti && outputPort.IsConnected) continue;

                if (NodeViews.TryGetValue(inputNode.GUID, out BaseNodeView inputNodeView)
                    && NodeViews.TryGetValue(outputNode.GUID, out BaseNodeView outputNodeView))
                {
                    Connect(inputNodeView.PortViews[edge.InputFieldName], outputNodeView.PortViews[edge.OutputFieldName]);
                }
            }

            this.SetDirty(true);
        }

        /// <summary> GraphView发生改变时调用 </summary>
        GraphViewChange GraphViewChangedCallback(GraphViewChange changes)
        {
            if (changes.elementsToRemove != null)
            {
                RegisterCompleteObjectUndo("Remove Graph Elements");

                changes.elementsToRemove.Sort((e1, e2) =>
                {
                    int GetPriority(GraphElement e)
                    {
                        if (e is BaseNodeView)
                            return 0;
                        else
                            return 1;
                    }
                    return GetPriority(e1).CompareTo(GetPriority(e2));
                });

                //Handle ourselves the edge and node remove
                changes.elementsToRemove.RemoveAll(e =>
                {
                    switch (e)
                    {
                        case EdgeView edgeView:
                            Disconnect(edgeView);
                            return true;
                        case BaseNodeView nodeView:
                            RemoveNode(nodeView);
                            return true;
                        case BlackboardField blackboardField:
                            if (GraphData.RemoveExposedParameter(blackboardField.userData as ExposedParameter))
                                blackboard.RemoveField(blackboardField);
                            return true;
                        case GroupView groupView:
                            RemoveGroup(groupView);
                            return true;
                        case BaseStackNodeView stackNodeView:
                            RemoveStackNode(stackNodeView);
                            return true;
                    }

                    return false;
                });

                this.SetDirty();
            }

            return changes;
        }

        /// <summary> 转换发生改变时调用 </summary>
        void ViewTransformChangedCallback(GraphView view)
        {
            if (GraphData != null)
            {
                GraphData.position = viewTransform.position;
                GraphData.scale = viewTransform.scale;
            }
        }

        protected virtual void KeyDownCallback(KeyDownEvent e)
        {
            if (e.keyCode == KeyCode.S && e.commandKey)
            {
                SaveGraphToDisk();
                e.StopPropagation();
            }
        }

        void MouseUpCallback(MouseUpEvent e)
        {
            schedule.Execute(() =>
            {
                if (DoesSelectionContainsInspectorNodes())
                    UpdateNodeInspectorSelection();
            }).ExecuteLater(1);
        }

        void MouseDownCallback(MouseDownEvent e)
        {
            // When left clicking on the graph (not a node or something else)
            if (e.button == 0)
            {
                foreach (var nodeView in NodeViews.Values)
                {
                    if (nodeView is IHasSettingNodeView settingNodeView)
                        settingNodeView.CloseSettings();
                }
            }

            if (DoesSelectionContainsInspectorNodes())
                UpdateNodeInspectorSelection();
        }

        bool DoesSelectionContainsInspectorNodes()
        {
            return true;
            //return selection.Any(s => s is BaseNodeView v && v.nodeData.needsInspector);
        }

        void DragPerformedCallback(DragPerformEvent e)
        {
            var mousePos = (e.currentTarget as VisualElement).ChangeCoordinatesTo(contentViewContainer, e.localMousePosition);
            var dragData = DragAndDrop.GetGenericData("DragSelection") as List<ISelectable>;

            if (dragData == null)
                return;

            var exposedParameterFieldViews = dragData.OfType<BlackboardField>();
            if (exposedParameterFieldViews.Any())
            {
                foreach (var paramFieldView in exposedParameterFieldViews)
                {
                    RegisterCompleteObjectUndo("Create Parameter Node");
                    var paramNode = BaseNode.CreateNew<ParameterNode>(mousePos);
                    paramNode.paramGUID = (paramFieldView.userData as ExposedParameter).GUID;
                    AddNode(paramNode);
                    this.SetDirty();
                }
            }
        }

        void DragUpdatedCallback(DragUpdatedEvent e)
        {
            var dragData = DragAndDrop.GetGenericData("DragSelection") as List<ISelectable>;
            bool dragging = false;

            if (dragData != null)
            {
                // Handle drag from exposed parameter view
                if (dragData.OfType<BlackboardField>().Any())
                    dragging = true;
            }

            if (dragging)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
            }

            UpdateNodeInspectorSelection();
        }

        #endregion

        #region API
        public void SetDirty(bool _immediately = false)
        {
            if (_immediately)
                EditorUtility.SetDirty(GraphData);
            else
                IsDirty = true;
        }

        #endregion

        #region Graph content modification

        public void UpdateNodeInspectorSelection()
        {
            HashSet<BaseNodeView> selectedNodeViews = new HashSet<BaseNodeView>();
            bool drawnNode = false;
            foreach (var element in selection)
            {
                if (element is BaseNodeView nodeView && Contains(nodeView))
                {
                    EditorGUILayoutExtension.DrawFieldsInInspector("Node Inspector", nodeView.NodeData);
                    drawnNode = true;
                }
            }
            if (!drawnNode)
                Selection.activeObject = GraphData;
        }

        public RelayNodeView AddRelayNode(PortView _inputPortView, PortView _outputPortView, Vector2 _position)
        {
            var relayNode = BaseNode.CreateNew<RelayNode>(_position);
            var nodeView = AddNode(relayNode) as RelayNodeView;

            if (_outputPortView != null)
                Connect(nodeView.PortViews["input"], _outputPortView);

            if (_inputPortView != null)
                Connect(_inputPortView, nodeView.PortViews["output"]);
            return nodeView;
        }

        public void RemoveRelayNode(RelayNodeView _relayNodeView)
        {
            // 获取relayNodeViewinput侧接口
            // 获取relayNodeViewoutput侧接口

            // 如果两个接口都不为空，连接这两个接口
        }

        public BaseNodeView AddNode(BaseNode _nodeData)
        {
            RegisterCompleteObjectUndo("AddNode " + _nodeData.GetType().Name);
            GraphData.AddNode(_nodeData);
            BaseNodeView nodeView = AddNodeView(_nodeData);
            this.SetDirty();
            return nodeView;
        }

        public BaseNodeView AddNodeView(BaseNode _nodeData)
        {
            Type nodeViewType = NodeEditorUtility.GetNodeViewType(_nodeData.GetType());
            if (nodeViewType == null)
                nodeViewType = GetDefaultNodeViewType(_nodeData.GetType());

            BaseNodeView nodeView = Activator.CreateInstance(nodeViewType) as BaseNodeView;
            nodeView.Initialize(this, _nodeData);
            AddElement(nodeView);
            NodeViews[_nodeData.GUID] = nodeView;

            if (nodeView is IOnGUIObserver observer)
                onGUIObservers.Add(observer);
            return nodeView;
        }

        public void RemoveNode(BaseNodeView _nodeView)
        {
            // 先断开所有连线
            foreach (var portView in _nodeView.PortViews.Values)
            {
                Disconnect(portView);
            }

            GraphData.RemoveNode(_nodeView.NodeData);

            // 然后移除节点View
            RemoveNodeView(_nodeView);
            UpdateNodeInspectorSelection();
        }

        public void RemoveNodeView(BaseNodeView _nodeView)
        {
            RemoveElement(_nodeView);
            NodeViews.Remove(_nodeView.NodeData.GUID);
            if (_nodeView is IOnGUIObserver observer)
                onGUIObservers.Remove(observer);
            UpdateNodeInspectorSelection();
        }

        void RemoveNodeViews()
        {
            foreach (var nodeView in NodeViews.Values)
                RemoveElement(nodeView);
            NodeViews.Clear();
            onGUIObservers.Clear();
        }

        public GroupView AddGroup(BaseGroup _groupData)
        {
            GraphData.AddGroup(_groupData);
            return AddGroupView(_groupData);
        }

        public GroupView AddGroupView(BaseGroup _groupData)
        {
            var groupView = new GroupView();
            groupView.Initialize(this, _groupData);
            GroupViews.Add(groupView);
            AddElement(groupView);
            return groupView;
        }

        public void AddSelectionsToGroup(GroupView _groupView)
        {
            foreach (var selectedNode in selection)
            {
                if (selectedNode is BaseNodeView)
                {
                    if (GroupViews.Exists(x => x.ContainsElement(selectedNode as BaseNodeView)))
                        continue;

                    _groupView.AddElement(selectedNode as BaseNodeView);
                }
            }
        }

        public void RemoveGroup(GroupView _groupView)
        {
            GraphData.RemoveGroup(_groupView.GroupData);
            RemoveGroupView(_groupView);
        }

        public void RemoveGroupView(GroupView _groupView)
        {
            GroupViews.Remove(_groupView);
            RemoveElement(_groupView);
        }

        public void RemoveGroups()
        {
            foreach (var groupView in GroupViews)
                RemoveElement(groupView);
            GroupViews.Clear();
        }

        public BaseStackNodeView AddStackNode(BaseStackNode _stackData)
        {
            GraphData.AddStackNode(_stackData);
            return AddStackNodeView(_stackData);
        }

        public BaseStackNodeView AddStackNodeView(BaseStackNode _stackNode)
        {
            var stackViewType = NodeEditorUtility.GetStackNodeCustomViewType(_stackNode.GetType());
            var stackView = Activator.CreateInstance(stackViewType) as BaseStackNodeView;
            stackView.Initialize(this, _stackNode);
            AddElement(stackView);
            StackNodeViews[_stackNode.GUID] = stackView;
            return stackView;
        }

        public void RemoveStackNode(BaseStackNodeView _stackNodeView)
        {
            GraphData.RemoveStackNode(_stackNodeView.stackNode);
            RemoveStackNodeView(_stackNodeView);
        }

        public void RemoveStackNodeView(BaseStackNodeView _stackNodeView)
        {
            RemoveElement(_stackNodeView);
            StackNodeViews.Remove(_stackNodeView.stackNode.GUID);
        }

        void RemoveStackNodeViews()
        {
            foreach (var stackView in StackNodeViews)
                RemoveElement(stackView.Value);
            StackNodeViews.Clear();
        }

        public bool ConnectView(EdgeView _edgeView)
        {
            var inputPortView = _edgeView.input as PortView;
            var outputPortView = _edgeView.output as PortView;
            var inputNodeView = inputPortView.node as BaseNodeView;
            var outputNodeView = outputPortView.node as BaseNodeView;

            if (!inputPortView.PortData.IsMulti)
            {
                foreach (var edge in EdgeViews.Where(ev => ev.input == _edgeView.input).ToList())
                {
                    DisconnectView(edge);
                }
            }
            if (!(_edgeView.output as PortView).PortData.IsMulti)
            {
                foreach (var edge in EdgeViews.Where(ev => ev.output == _edgeView.output).ToList())
                {
                    DisconnectView(edge);
                }
            }

            inputPortView.Connect(_edgeView);
            outputPortView.Connect(_edgeView);


            AddElement(_edgeView);
            EdgeViews.Add(_edgeView);

            inputNodeView.RefreshPorts();
            outputNodeView.RefreshPorts();

            schedule.Execute(() =>
            {
                _edgeView.UpdateEdgeControl();
            }).ExecuteLater(1);

            _edgeView.isConnected = true;
            return true;
        }

        public bool ConnectView(PortView _inputPortView, PortView _outputPortView, SerializableEdge _serializableEdge)
        {
            var edgeView = new EdgeView()
            {
                userData = _serializableEdge,
                input = _inputPortView,
                output = _outputPortView,
            };
            return ConnectView(edgeView);
        }

        public bool Connect(PortView _inputPortView, PortView _outputPortView)
        {
            if (_inputPortView.Owner.parent == null || _outputPortView.Owner.parent == null)
                return false;

            var newEdge = GraphData.Connect(_inputPortView.PortData, _outputPortView.PortData);

            var edgeView = new EdgeView()
            {
                userData = newEdge,
                input = _inputPortView,
                output = _outputPortView,
            };
            return ConnectView(edgeView);
        }

        public bool Connect(EdgeView _edgeView)
        {
            var inputPortView = _edgeView.input as PortView;
            var outputPortView = _edgeView.output as PortView;
            var inputNodeView = inputPortView.node as BaseNodeView;
            var outputNodeView = outputPortView.node as BaseNodeView;
            inputNodeView.NodeData.TryGetPort(inputPortView.FieldName, out NodePort inputPort);
            outputNodeView.NodeData.TryGetPort(outputPortView.FieldName, out NodePort outputPort);

            _edgeView.userData = GraphData.Connect(inputPort, outputPort);

            ConnectView(_edgeView);

            return true;
        }

        public void Disconnect(PortView _portView)
        {
            if (_portView == null) return;

            foreach (var edgeView in _portView.Edges.ToArray())
            {
                Disconnect(edgeView);
            }
        }

        public void Disconnect(EdgeView _edgeView)
        {
            if (_edgeView == null) return;

            GraphData.Disconnect(_edgeView.serializedEdge);
            DisconnectView(_edgeView);
        }

        public void DisconnectView(EdgeView _edgeView)
        {
            RemoveElement(_edgeView);

            if (_edgeView.input != null &&
                _edgeView.input is PortView inputPortView &&
                inputPortView.node is BaseNodeView inputNodeView)
            {
                inputPortView.Disconnect(_edgeView);
                inputNodeView.RefreshPorts();
            }
            if (_edgeView.output != null &&
                _edgeView.output is PortView ouputPortView &&
                ouputPortView.node is BaseNodeView outputNodeView)
            {
                _edgeView.output.Disconnect(_edgeView);
                outputNodeView.RefreshPorts();
            }
            EdgeViews.Remove(_edgeView);
        }

        /// <summary> 不影响数据 </summary>
        public void RemoveEdgeViews()
        {
            foreach (var edge in EdgeViews)
                RemoveElement(edge);
            EdgeViews.Clear();
        }

        public void RegisterCompleteObjectUndo(string name)
        {
            //Undo.RegisterCompleteObjectUndo(GraphData, name);
        }

        public void SaveGraphToDisk(bool _immediately = false)
        {
            if (GraphData == null) return;
            SetDirty(true);
            if (_immediately)
                AssetDatabase.SaveAssets();
        }

        public void ResetPositionAndZoom()
        {
            GraphData.position = Vector3.zero;
            GraphData.scale = Vector3.one;

            UpdateViewTransform(GraphData.position, GraphData.scale);
        }

        /// <summary> Deletes the selected content, can be called form an IMGUI container </summary>
        public void DelayedDeleteSelection() => this.schedule.Execute(() => DeleteSelectionOperation("Delete", AskUser.DontAskUser)).ExecuteLater(0);

        #endregion
    }
}