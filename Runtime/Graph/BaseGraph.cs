﻿#region SerializedReference
/*
using CZToolKit.Core;
using CZToolKit.Core.SharedVariable;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace CZToolKit.GraphProcessor
{

    [Serializable]
    public abstract class BaseGraph : ScriptableObject, IVariableSource
    {
        /// <summary> 黑板的默认大小 </summary>
        public static readonly Vector2 DefaultBlackboardSize = new Vector2(150, 200);

        #region 变量
        public Vector3 position = Vector3.zero;
        public Vector3 scale = Vector3.one;
        public bool blackboardoVisible = true;
        public Rect blackboardPosition = new Rect(Vector2.zero, DefaultBlackboardSize);

        [SerializeField, SerializeReference] List<BaseNode> nodes = new List<BaseNode>();
        [SerializeField] List<SerializableEdge> edges = new List<SerializableEdge>();
        [SerializeField, SerializeReference] List<BaseStackNode> stacks = new List<BaseStackNode>();
        [SerializeField] List<BaseGroup> groups = new List<BaseGroup>();

        [SerializeField] ExposedParmetersDictionary blackboard = new ExposedParmetersDictionary();
        [SerializeField, HideInInspector] GUIDMapDictionary guidMap = new GUIDMapDictionary();

        Dictionary<string, BaseNode> nodesGUIDMapping = new Dictionary<string, BaseNode>();
        Dictionary<string, SerializableEdge> edgesGUIDMapping = new Dictionary<string, SerializableEdge>();
        Dictionary<string, BaseStackNode> stackNodesGUIDMapping = new Dictionary<string, BaseStackNode>();
        List<SharedVariable> variables = new List<SharedVariable>();
        #endregion

        #region 属性
        public IVariableOwner VarialbeOwner { get; private set; }
        public IReadOnlyList<BaseGroup> Groups { get { return groups; } }
        public IReadOnlyDictionary<string, BaseNode> NodesGUIDMapping { get { return nodesGUIDMapping; } }
        public IReadOnlyDictionary<string, SerializableEdge> EdgesGUIDMapping { get { return edgesGUIDMapping; } }
        public IReadOnlyDictionary<string, BaseStackNode> StackNodesGUIDMapping { get { return stackNodesGUIDMapping; } }
        public IReadOnlyDictionary<string, ExposedParameter> Blackboard { get { return blackboard; } }
        public IReadOnlyList<SharedVariable> Variables { get { return variables; } }
        #endregion

        protected virtual void OnEnable()
        {
            Flush();
            CollectionVariables();
        }

        public virtual void Initialize(GraphOwner _graphOwner)
        {
            InitializePropertyMapping(_graphOwner);
        }

        private void CollectionVariables()
        {
            if (variables == null)
                variables = new List<SharedVariable>();
            else
                variables.Clear();
            foreach (var node in nodesGUIDMapping.Values)
            {
                variables.AddRange(CollectionNodeVariables(node));
            }
        }

        private IEnumerable<SharedVariable> CollectionNodeVariables(BaseNode _node)
        {
            List<FieldInfo> fieldInfos = Utility.GetFieldInfos(_node.GetType());
            Type t = typeof(SharedVariable);
            foreach (var fieldInfo in fieldInfos)
            {
                if (!t.IsAssignableFrom(fieldInfo.FieldType)) continue;
                SharedVariable variable = fieldInfo.GetValue(_node) as SharedVariable;
                if (variable == null)
                {
                    variable = Activator.CreateInstance(fieldInfo.FieldType) as SharedVariable;
                    fieldInfo.SetValue(_node, variable);
                }
                yield return variable;
            }
        }

        public void InitializePropertyMapping(IVariableOwner _variableOwner)
        {
            VarialbeOwner = _variableOwner;
            foreach (var variable in Variables)
            {
                variable.InitializePropertyMapping(_variableOwner);
            }
        }

        /// <summary> 刷新及修复数据 </summary>
        public void Flush()
        {
            // 更新节点端口
            foreach (var node in NodesGUIDMapping.Values)
            {
                NodeDataCache.UpdateStaticPorts(node);
            }
            Clean();
        }

        /// <summary> 清理和修复 </summary>
        public void Clean()
        {
            // 清理无效数据
            for (int i = 0; i < nodes.Count; i++)
            {
                BaseNode node = nodes[i];
                if (node == null) { nodes.RemoveAt(i--); continue; }
                nodesGUIDMapping[node.GUID] = node;
                node.Initialize(this);
            }
            for (int i = 0; i < edges.Count; i++)
            {
                SerializableEdge edge = edges[i];
                if (edge == null) { nodes.RemoveAt(i--); continue; }
                edgesGUIDMapping[edge.GUID] = edge;
                edge.Initialize(this);
            }
            for (int i = 0; i < stacks.Count; i++)
            {
                BaseStackNode stack = stacks[i];
                if (stack == null) { stacks.RemoveAt(i--); continue; }
                stackNodesGUIDMapping[stack.GUID] = stack;
            }
            groups.RemoveAll(item => item == null);



            for (int i = 0; i < edges.Count; i++)
            {
                SerializableEdge edge = edges[i];

                if (edge.InputNode == null || edge.OutputNode == null || edge.InputNode == edge.OutputNode)
                {
                    Disconnect(edge.GUID);
                    edge.InputPort?.DisconnectEdge(edge);
                    edge.OutputPort?.DisconnectEdge(edge);
                    continue;
                }

                if (edge.InputPort == null || edge.OutputPort == null || edge.InputPort.Direction == edge.OutputPort.Direction)
                {
                    Disconnect(edge.GUID);
                    edge.InputPort?.DisconnectEdge(edge);
                    edge.OutputPort?.DisconnectEdge(edge);
                    continue;
                }

                if (!edge.InputPort.EdgeGUIDS.Contains(edge.GUID) || !edge.OutputPort.EdgeGUIDS.Contains(edge.GUID))
                {
                    Disconnect(edge.GUID);
                    edge.InputPort.DisconnectEdge(edge);
                    edge.OutputPort.DisconnectEdge(edge);
                    continue;
                }
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                BaseNode node = nodes[i];
                foreach (var nodePort in node.Ports.ToArray())
                {
                    if (nodePort.Value == null)
                    {
                        node.Ports.Remove(nodePort.Key);
                        continue;
                    }
                    nodePort.Value.EdgeGUIDS.RemoveAll(edgeGUID => !edgesGUIDMapping.ContainsKey(edgeGUID));
                }
            }

            for (int i = 0; i < groups.Count; i++)
            {
                BaseGroup group = groups[i];
                group.innerNodeGUIDs.RemoveAll(nodeGUID => !NodesGUIDMapping.ContainsKey(nodeGUID));
                group.innerStackGUIDs.RemoveAll(stackGUID => !stackNodesGUIDMapping.ContainsKey(stackGUID));
            }

            foreach (var item in guidMap.ToArray())
            {
                if (!Blackboard.ContainsKey(item.Value))
                    guidMap.Remove(item.Key);
            }
        }

        #region Operation
        /// <summary> 克隆 </summary>
        public BaseGraph Clone()
        {
            BaseGraph graph = Instantiate(this);
            graph.name = name;
            return graph;
        }

        /// <summary> 根据类型添加一个节点 </summary>
        public T AddNode<T>(Vector2 _nodePosition) where T : BaseNode
        {
            T node = BaseNode.CreateNew<T>(_nodePosition);
            AddNode(node);
            return node;
        }

        /// <summary> 添加节点 </summary>
        public void AddNode(BaseNode _node)
        {
            if (_node == null) return;
            _node.Initialize(this);
            nodes.Add(_node);
            nodesGUIDMapping[_node.GUID] = _node;
            NodeDataCache.UpdateStaticPorts(_node);
            IEnumerable<SharedVariable> nodeVariables = CollectionNodeVariables(_node);
            if (VarialbeOwner != null)
            {
                foreach (var variable in nodeVariables)
                {
                    variable.InitializePropertyMapping(VarialbeOwner);
                }
            }
            variables.AddRange(nodeVariables);
        }

        /// <summary> 移除指定节点 </summary>
        public void RemoveNode(BaseNode _node)
        {
            if (_node == null) return;
            // 断开这个节点的所有连接,移除节点
            Disconnect(_node);
            nodes.Remove(_node);
            nodesGUIDMapping.Remove(_node.GUID);
        }

        /// <summary> 连接两个端口 </summary>
        public SerializableEdge Connect(NodePort _inputPort, NodePort _outputPort)
        {
            // 在连接两个端口，如果端口设置为只能连接一个端口，则需要在连接前把其他所有连接断开
            if (!_inputPort.IsMulti)
                Disconnect(_inputPort);
            if (!_outputPort.IsMulti)
                Disconnect(_outputPort);

            // 创建一条连线
            SerializableEdge edge = SerializableEdge.CreateNewEdge(this, _inputPort, _outputPort);
            AddEdge(edge);

            _inputPort.ConnectEdge(edge);
            _outputPort.ConnectEdge(edge);

            _inputPort.Owner.OnConnected(_inputPort, _outputPort);
            _outputPort.Owner.OnConnected(_outputPort, _inputPort);

            return edge;
        }

        private void AddEdge(SerializableEdge _edge)
        {
            _edge.Initialize(this);
            edges.Add(_edge);
            edgesGUIDMapping[_edge.GUID] = _edge;
        }

        /// <summary> 断开指定连接 </summary>
        public void Disconnect(SerializableEdge _edge)
        {
            if (_edge != null) Disconnect(_edge.GUID);
        }

        /// <summary> 根据连接的GUID断开连接 </summary>
        public void Disconnect(string _edgeGUID)
        {
            if (EdgesGUIDMapping.TryGetValue(_edgeGUID, out SerializableEdge edge))
            {
                if (edge != null)
                {
                    edge.InputPort?.DisconnectEdge(edge);
                    edge.OutputPort?.DisconnectEdge(edge);
                    edge.InputNode?.OnDisconnected(edge.InputPort, edge.OutputPort);
                    edge.OutputNode?.OnDisconnected(edge.OutputPort, edge.InputPort);
                }
                RemoveEdge(edge);
            }
        }

        private void RemoveEdge(SerializableEdge _edge)
        {
            edges.Remove(_edge);
            edgesGUIDMapping.Remove(_edge.GUID);
        }

        /// <summary> 断开指定端口的所有连接 </summary>
        public void Disconnect(NodePort _nodePort)
        {
            while (_nodePort.IsConnected)
            {
                Disconnect(_nodePort.GetEdge(0));
            }
        }

        /// <summary> 断开指定节点的所有连接 </summary>
        public void Disconnect(BaseNode _node)
        {
            foreach (NodePort nodePort in _node.Ports.Values)
            {
                Disconnect(nodePort);
            }
        }

        /// <summary> 添加一个栈 </summary>
        public void AddStackNode(BaseStackNode _stack)
        {
            stacks.Add(_stack);
            stackNodesGUIDMapping[_stack.GUID] = _stack;
        }

        /// <summary> 移除一个栈 </summary>
        public void RemoveStackNode(BaseStackNode _stack)
        {
            stacks.Remove(_stack);
            stackNodesGUIDMapping.Remove(_stack.GUID);
        }

        /// <summary> 添加一个Group </summary>
        public void AddGroup(BaseGroup _group)
        {
            groups.Add(_group);
        }

        /// <summary> 移除一个Group </summary>
        public void RemoveGroup(BaseGroup _group)
        {
            groups.Remove(_group);
        }

        #endregion

        #region Exposed
        #region Get
        public bool TryGetExposedParameterFromName(string _name, out ExposedParameter _param)
        {
            if (guidMap.TryGetValue(_name, out string guid))
                return Blackboard.TryGetValue(guid, out _param);

            _param = null;
            return false;
        }

        public bool TryGetExposedParameterFromGUID(string _guid, out ExposedParameter _param)
        {
            return Blackboard.TryGetValue(_guid, out _param);
        }
        #endregion

        #region Set
        public void SetExposedParameterValueFromName(string _name, object _value)
        {
            if (guidMap.TryGetValue(_name, out string guid))
                SetExposedParameterValueFromGUID(guid, _value);

        }
        public void SetExposedParameterValueFromGUID(string _guid, object _value)
        {
            if (Blackboard.TryGetValue(_guid, out ExposedParameter param))
                param.Value = _value;
        }
        #endregion

        #region Add
        public ExposedParameter AddExposedParameter(ExposedParameter _param)
        {
            if (guidMap.TryGetValue(_param.Name, out string guid) && Blackboard.TryGetValue(_param.GUID, out ExposedParameter param))
                return null;
            guidMap[_param.Name] = _param.GUID;
            blackboard[_param.GUID] = _param;
            return _param;
        }
        #endregion

        public bool RenameExposeParameter(string _oldName, string _newName)
        {
            if (!guidMap.TryGetValue(_oldName, out string guid)) { Debug.LogError($"{_oldName}不被包含在黑板数据内"); return false; }
            if (string.IsNullOrEmpty(_newName)) return false;
            if (guidMap.ContainsKey(_newName)) { Debug.LogError($"黑板内已存在同名数据{_newName}"); return false; }

            guidMap[_newName] = guidMap[_oldName];
            guidMap.Remove(_oldName);
            if (Blackboard.TryGetValue(guid, out ExposedParameter param))
                param.Name = _newName;
            return true;
        }

        public bool RemoveExposedParameterFromName(string _name)
        {
            if (guidMap.TryGetValue(_name, out string guid))
                return RemoveExposedParameterFromGUID(guid);
            return true;
        }

        public bool RemoveExposedParameterFromGUID(string _guid)
        {
            if (Blackboard.TryGetValue(_guid, out ExposedParameter exposedParameter))
                return RemoveExposedParameter(exposedParameter);
            return true;
        }

        public bool RemoveExposedParameter(ExposedParameter _exposedParameter)
        {
            if (NodesGUIDMapping.Values.OfType<ParameterNode>().Where(_node => _node.paramGUID == _exposedParameter.GUID).Count() > 0)
            {
                Debug.LogWarning("该参数正被节点引用");
                return false;
            }
            guidMap.Remove(_exposedParameter.Name);
            blackboard.Remove(_exposedParameter.GUID);
            return true;
        }
        #endregion
    }
}
*/
#endregion
using CZToolKit.Core;
using CZToolKit.Core.SharedVariable;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace CZToolKit.GraphProcessor
{

    [Serializable]
    public abstract class BaseGraph : ScriptableObject, IVariableSource, ISerializationCallbackReceiver
    {
        /// <summary> 黑板的默认大小 </summary>
        public static readonly Vector2 DefaultBlackboardSize = new Vector2(150, 200);

        #region 变量
        public Vector3 position = Vector3.zero;
        public Vector3 scale = Vector3.one;
        public bool blackboardoVisible = true;
        public Rect blackboardPosition = new Rect(Vector2.zero, DefaultBlackboardSize);

        Dictionary<string, BaseNode> nodes = new Dictionary<string, BaseNode>();
        Dictionary<string, SerializableEdge> edges = new Dictionary<string, SerializableEdge>();
        Dictionary<string, BaseStackNode> stacks = new Dictionary<string, BaseStackNode>();
        List<BaseGroup> groups = new List<BaseGroup>();

        [SerializeField] ExposedParmetersDictionary blackboard = new ExposedParmetersDictionary();
        [SerializeField, HideInInspector] GUIDMapDictionary guidMap = new GUIDMapDictionary();

        List<SharedVariable> variables = new List<SharedVariable>();
        #endregion

        #region Serialize
        [SerializeField] List<JsonElement> serializedNodes = new List<JsonElement>();
        [SerializeField] List<JsonElement> serializedEdges = new List<JsonElement>();
        [SerializeField] List<JsonElement> serializedStacks = new List<JsonElement>();
        [SerializeField] List<JsonElement> serializedGroups = new List<JsonElement>();

        public virtual void OnBeforeSerialize() { Serialize(); }

        public virtual void OnAfterDeserialize() { }

        public void Serialize()
        {
            serializedNodes.Clear();
            foreach (var node in nodes.Values)
            {
                if (node == null) continue;
                serializedNodes.Add(JsonSerializer.Serialize(node));
            }

            serializedEdges.Clear();
            foreach (var edge in edges.Values)
            {
                if (edge == null) continue;
                serializedEdges.Add(JsonSerializer.Serialize(edge));
            }

            serializedStacks.Clear();
            foreach (var stack in stacks.Values)
            {
                if (stack == null) continue;
                serializedStacks.Add(JsonSerializer.Serialize(stack));
            }

            serializedGroups.Clear();
            foreach (var group in groups)
            {
                if (group == null) continue;
                serializedGroups.Add(JsonSerializer.Serialize(group));
            }
        }

        public void Deserialize()
        {
            nodes.Clear();
            foreach (var serializedData in serializedNodes)
            {
                BaseNode node = JsonSerializer.Deserialize(serializedData) as BaseNode;
                if (node == null) continue;
                AddNode(node);
            }

            edges.Clear();
            foreach (var serializedData in serializedEdges)
            {
                SerializableEdge edge = JsonSerializer.Deserialize(serializedData) as SerializableEdge;
                if (edge == null) continue;
                AddEdge(edge);
            }

            stacks.Clear();
            foreach (var serializedData in serializedStacks)
            {
                BaseStackNode stack = JsonSerializer.Deserialize(serializedData) as BaseStackNode;
                if (stack == null) continue;
                AddStackNode(stack);
            }

            groups.Clear();
            foreach (var serializedData in serializedGroups)
            {
                BaseGroup group = JsonSerializer.Deserialize(serializedData) as BaseGroup;
                if (group == null) continue;
                AddGroup(group);
            }
        }
        #endregion

        #region 属性
        public IVariableOwner VarialbeOwner { get; private set; }
        public IReadOnlyList<BaseGroup> Groups { get { return groups; } }
        public IReadOnlyDictionary<string, BaseNode> NodesGUIDMapping { get { return nodes; } }
        public IReadOnlyDictionary<string, SerializableEdge> EdgesGUIDMapping { get { return edges; } }
        public IReadOnlyDictionary<string, BaseStackNode> StackNodesGUIDMapping { get { return stacks; } }
        public IReadOnlyDictionary<string, ExposedParameter> Blackboard { get { return blackboard; } }
        public IReadOnlyList<SharedVariable> Variables { get { return variables; } }
        #endregion

        protected virtual void OnEnable()
        {
            Deserialize();
            Flush();
            CollectionVariables();
        }

        public virtual void Initialize(GraphOwner _graphOwner)
        {
            InitializePropertyMapping(_graphOwner);
        }

        private void CollectionVariables()
        {
            if (variables == null)
                variables = new List<SharedVariable>();
            else
                variables.Clear();
            foreach (var node in nodes.Values)
            {
                variables.AddRange(CollectionNodeVariables(node));
            }
        }

        private IEnumerable<SharedVariable> CollectionNodeVariables(BaseNode _node)
        {
            List<FieldInfo> fieldInfos = Utility.GetFieldInfos(_node.GetType());
            Type t = typeof(SharedVariable);
            foreach (var fieldInfo in fieldInfos)
            {
                if (!t.IsAssignableFrom(fieldInfo.FieldType)) continue;
                SharedVariable variable = fieldInfo.GetValue(_node) as SharedVariable;
                if (variable == null)
                {
                    variable = Activator.CreateInstance(fieldInfo.FieldType) as SharedVariable;
                    fieldInfo.SetValue(_node, variable);
                }
                yield return variable;
            }
        }

        public void InitializePropertyMapping(IVariableOwner _variableOwner)
        {
            VarialbeOwner = _variableOwner;
            foreach (var variable in Variables)
            {
                variable.InitializePropertyMapping(_variableOwner);
            }
        }

        /// <summary> 刷新及修复数据 </summary>
        public void Flush()
        {
            // 更新节点端口
            foreach (var node in NodesGUIDMapping.Values)
            {
                NodeDataCache.UpdateStaticPorts(node);
            }
            Clean();
        }

        /// <summary> 清理和修复 </summary>
        public void Clean()
        {
            // 清理无效数据
            foreach (var kv in nodes.ToArray())
            {
                if (kv.Value == null) { nodes.Remove(kv.Key); continue; }
                kv.Value.Initialize(this);
            }

            foreach (var kv in edges.ToArray())
            {
                if (kv.Value == null) { nodes.Remove(kv.Key); continue; }
                kv.Value.Initialize(this);
            }

            foreach (var kv in stacks.ToArray())
            {
                if (kv.Value == null) { nodes.Remove(kv.Key); continue; }
            }

            groups.RemoveAll(item => item == null);
            foreach (var edge in edges.Values.ToArray())
            {
                if (edge.InputNode == null || edge.OutputNode == null || edge.InputNode == edge.OutputNode)
                {
                    Disconnect(edge.GUID);
                    edge.InputPort?.DisconnectEdge(edge);
                    edge.OutputPort?.DisconnectEdge(edge);
                    continue;
                }

                if (edge.InputPort == null || edge.OutputPort == null || edge.InputPort.Direction == edge.OutputPort.Direction)
                {
                    Disconnect(edge.GUID);
                    edge.InputPort?.DisconnectEdge(edge);
                    edge.OutputPort?.DisconnectEdge(edge);
                    continue;
                }

                if (!edge.InputPort.EdgeGUIDS.Contains(edge.GUID) || !edge.OutputPort.EdgeGUIDS.Contains(edge.GUID))
                {
                    Disconnect(edge.GUID);
                    edge.InputPort.DisconnectEdge(edge);
                    edge.OutputPort.DisconnectEdge(edge);
                    continue;
                }
            }

            foreach (var node in nodes.Values.ToArray())
            {
                foreach (var nodePort in node.Ports.ToArray())
                {
                    if (nodePort.Value == null)
                    {
                        node.Ports.Remove(nodePort.Key);
                        continue;
                    }
                    nodePort.Value.EdgeGUIDS.RemoveAll(edgeGUID => !edges.ContainsKey(edgeGUID));
                }
            }

            for (int i = 0; i < groups.Count; i++)
            {
                BaseGroup group = groups[i];
                group.innerNodeGUIDs.RemoveAll(nodeGUID => !NodesGUIDMapping.ContainsKey(nodeGUID));
                group.innerStackGUIDs.RemoveAll(stackGUID => !stacks.ContainsKey(stackGUID));
            }

            foreach (var item in guidMap.ToArray())
            {
                if (!Blackboard.ContainsKey(item.Value))
                    guidMap.Remove(item.Key);
            }
        }

        #region Operation
        /// <summary> 克隆 </summary>
        public BaseGraph Clone()
        {
            BaseGraph graph = Instantiate(this);
            graph.name = name;
            return graph;
        }

        /// <summary> 根据类型添加一个节点 </summary>
        public T AddNode<T>(Vector2 _nodePosition) where T : BaseNode
        {
            T node = BaseNode.CreateNew<T>(_nodePosition);
            AddNode(node);
            return node;
        }

        /// <summary> 添加节点 </summary>
        public void AddNode(BaseNode _node)
        {
            if (_node == null) return;
            _node.Initialize(this);
            nodes[_node.GUID] = _node;
            NodeDataCache.UpdateStaticPorts(_node);
            IEnumerable<SharedVariable> nodeVariables = CollectionNodeVariables(_node);
            if (VarialbeOwner != null)
            {
                foreach (var variable in nodeVariables)
                {
                    variable.InitializePropertyMapping(VarialbeOwner);
                }
            }
            variables.AddRange(nodeVariables);
        }

        /// <summary> 移除指定节点 </summary>
        public void RemoveNode(BaseNode _node)
        {
            if (_node == null) return;
            // 断开这个节点的所有连接,移除节点
            Disconnect(_node);
            nodes.Remove(_node.GUID);
        }

        /// <summary> 连接两个端口 </summary>
        public SerializableEdge Connect(NodePort _inputPort, NodePort _outputPort)
        {
            // 在连接两个端口，如果端口设置为只能连接一个端口，则需要在连接前把其他所有连接断开
            if (!_inputPort.IsMulti)
                Disconnect(_inputPort);
            if (!_outputPort.IsMulti)
                Disconnect(_outputPort);

            // 创建一条连线
            SerializableEdge edge = SerializableEdge.CreateNewEdge(this, _inputPort, _outputPort);
            AddEdge(edge);

            _inputPort.ConnectEdge(edge);
            _outputPort.ConnectEdge(edge);

            _inputPort.Owner.OnConnected(_inputPort, _outputPort);
            _outputPort.Owner.OnConnected(_outputPort, _inputPort);

            return edge;
        }

        private void AddEdge(SerializableEdge _edge)
        {
            _edge.Initialize(this);
            edges[_edge.GUID] = _edge;
        }

        /// <summary> 断开指定连接 </summary>
        public void Disconnect(SerializableEdge _edge)
        {
            if (_edge != null) Disconnect(_edge.GUID);
        }

        /// <summary> 根据连接的GUID断开连接 </summary>
        public void Disconnect(string _edgeGUID)
        {
            if (EdgesGUIDMapping.TryGetValue(_edgeGUID, out SerializableEdge edge))
            {
                if (edge != null)
                {
                    edge.InputPort?.DisconnectEdge(edge);
                    edge.OutputPort?.DisconnectEdge(edge);
                    edge.InputNode?.OnDisconnected(edge.InputPort, edge.OutputPort);
                    edge.OutputNode?.OnDisconnected(edge.OutputPort, edge.InputPort);
                }
                RemoveEdge(edge);
            }
        }

        private void RemoveEdge(SerializableEdge _edge)
        {
            edges.Remove(_edge.GUID);
        }

        /// <summary> 断开指定端口的所有连接 </summary>
        public void Disconnect(NodePort _nodePort)
        {
            while (_nodePort.IsConnected)
            {
                Disconnect(_nodePort.GetEdge(0));
            }
        }

        /// <summary> 断开指定节点的所有连接 </summary>
        public void Disconnect(BaseNode _node)
        {
            foreach (NodePort nodePort in _node.Ports.Values)
            {
                Disconnect(nodePort);
            }
        }

        /// <summary> 添加一个栈 </summary>
        public void AddStackNode(BaseStackNode _stack)
        {
            stacks[_stack.GUID] = _stack;
        }

        /// <summary> 移除一个栈 </summary>
        public void RemoveStackNode(BaseStackNode _stack)
        {
            stacks.Remove(_stack.GUID);
        }

        /// <summary> 添加一个Group </summary>
        public void AddGroup(BaseGroup _group)
        {
            groups.Add(_group);
        }

        /// <summary> 移除一个Group </summary>
        public void RemoveGroup(BaseGroup _group)
        {
            groups.Remove(_group);
        }

        #endregion

        #region Exposed
        #region Get
        public bool TryGetExposedParameterFromName(string _name, out ExposedParameter _param)
        {
            if (guidMap.TryGetValue(_name, out string guid))
                return Blackboard.TryGetValue(guid, out _param);

            _param = null;
            return false;
        }

        public bool TryGetExposedParameterFromGUID(string _guid, out ExposedParameter _param)
        {
            return Blackboard.TryGetValue(_guid, out _param);
        }
        #endregion

        #region Set
        public void SetExposedParameterValueFromName(string _name, object _value)
        {
            if (guidMap.TryGetValue(_name, out string guid))
                SetExposedParameterValueFromGUID(guid, _value);

        }
        public void SetExposedParameterValueFromGUID(string _guid, object _value)
        {
            if (Blackboard.TryGetValue(_guid, out ExposedParameter param))
                param.Value = _value;
        }
        #endregion

        #region Add
        public ExposedParameter AddExposedParameter(ExposedParameter _param)
        {
            if (guidMap.TryGetValue(_param.Name, out string guid) && Blackboard.TryGetValue(_param.GUID, out ExposedParameter param))
                return null;
            guidMap[_param.Name] = _param.GUID;
            blackboard[_param.GUID] = _param;
            return _param;
        }
        #endregion

        public bool RenameExposeParameter(string _oldName, string _newName)
        {
            if (!guidMap.TryGetValue(_oldName, out string guid)) { Debug.LogError($"{_oldName}不被包含在黑板数据内"); return false; }
            if (string.IsNullOrEmpty(_newName)) return false;
            if (guidMap.ContainsKey(_newName)) { Debug.LogError($"黑板内已存在同名数据{_newName}"); return false; }

            guidMap[_newName] = guidMap[_oldName];
            guidMap.Remove(_oldName);
            if (Blackboard.TryGetValue(guid, out ExposedParameter param))
                param.Name = _newName;
            return true;
        }

        public bool RemoveExposedParameterFromName(string _name)
        {
            if (guidMap.TryGetValue(_name, out string guid))
                return RemoveExposedParameterFromGUID(guid);
            return true;
        }

        public bool RemoveExposedParameterFromGUID(string _guid)
        {
            if (Blackboard.TryGetValue(_guid, out ExposedParameter exposedParameter))
                return RemoveExposedParameter(exposedParameter);
            return true;
        }

        public bool RemoveExposedParameter(ExposedParameter _exposedParameter)
        {
            if (NodesGUIDMapping.Values.OfType<ParameterNode>().Where(_node => _node.paramGUID == _exposedParameter.GUID).Count() > 0)
            {
                Debug.LogWarning("该参数正被节点引用");
                return false;
            }
            guidMap.Remove(_exposedParameter.Name);
            blackboard.Remove(_exposedParameter.GUID);
            return true;
        }
        #endregion
    }
}