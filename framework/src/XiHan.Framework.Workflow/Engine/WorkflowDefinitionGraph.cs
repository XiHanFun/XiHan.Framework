// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Exceptions;

namespace XiHan.Framework.Workflow.Engine;

/// <summary>
/// 流程定义图索引（按定义构建的节点/连线快速访问视图）
/// </summary>
internal sealed class WorkflowDefinitionGraph
{
    private readonly Dictionary<string, WorkflowNode> _nodesById;
    private readonly Dictionary<string, List<WorkflowTransition>> _outgoingByNode;
    private readonly Dictionary<string, int> _incomingCountByNode;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="definition">流程定义</param>
    public WorkflowDefinitionGraph(WorkflowDefinition definition)
    {
        Definition = definition;
        _nodesById = definition.Nodes.ToDictionary(node => node.Id, node => node);

        _outgoingByNode = [];
        _incomingCountByNode = [];

        foreach (var transition in definition.Transitions)
        {
            if (!_outgoingByNode.TryGetValue(transition.SourceNodeId, out var outgoing))
            {
                outgoing = [];
                _outgoingByNode[transition.SourceNodeId] = outgoing;
            }

            outgoing.Add(transition);
            _incomingCountByNode[transition.TargetNodeId] = _incomingCountByNode.GetValueOrDefault(transition.TargetNodeId) + 1;
        }

        // 独占网关按优先级升序求值，稳定排序保持定义顺序
        foreach (var outgoing in _outgoingByNode.Values)
        {
            var sorted = outgoing.OrderBy(transition => transition.Priority).ToList();
            outgoing.Clear();
            outgoing.AddRange(sorted);
        }

        StartNode = definition.Nodes.FirstOrDefault(node => node.ActivityType == WorkflowActivityTypes.Start)
            ?? throw new WorkflowException($"流程定义 {definition.Code} v{definition.Version} 缺少开始节点");
    }

    /// <summary>
    /// 流程定义
    /// </summary>
    public WorkflowDefinition Definition { get; }

    /// <summary>
    /// 开始节点
    /// </summary>
    public WorkflowNode StartNode { get; }

    /// <summary>
    /// 获取节点（不存在抛出异常）
    /// </summary>
    /// <param name="nodeId">节点标识</param>
    /// <returns>节点</returns>
    public WorkflowNode GetRequiredNode(string nodeId)
    {
        return _nodesById.TryGetValue(nodeId, out var node)
            ? node
            : throw new WorkflowException($"流程定义 {Definition.Code} v{Definition.Version} 不存在节点 {nodeId}");
    }

    /// <summary>
    /// 获取节点出边（按优先级升序）
    /// </summary>
    /// <param name="nodeId">节点标识</param>
    /// <returns>出边列表</returns>
    public IReadOnlyList<WorkflowTransition> GetOutgoing(string nodeId)
    {
        return _outgoingByNode.TryGetValue(nodeId, out var outgoing) ? outgoing : [];
    }

    /// <summary>
    /// 获取节点入边数量
    /// </summary>
    /// <param name="nodeId">节点标识</param>
    /// <returns>入边数量</returns>
    public int GetIncomingCount(string nodeId)
    {
        return _incomingCountByNode.GetValueOrDefault(nodeId);
    }
}
