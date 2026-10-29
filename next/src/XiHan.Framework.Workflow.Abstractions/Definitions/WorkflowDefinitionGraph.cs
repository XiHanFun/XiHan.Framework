// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Workflow.Abstractions.Activities;

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 定义的图索引：按 ID 找节点、每个节点按优先级排好的出边、入边计数、开始节点。
/// </summary>
internal sealed class WorkflowDefinitionGraph
{
    /// <summary>
    /// 节点 ID → 节点。
    /// </summary>
    private readonly Dictionary<string, WorkflowNode> _nodes = new(StringComparer.Ordinal);

    /// <summary>
    /// 节点 ID → 出边（优先级升序，同级按定义顺序）。
    /// </summary>
    private readonly Dictionary<string, List<WorkflowTransition>> _outgoing = new(StringComparer.Ordinal);

    /// <summary>
    /// 节点 ID → 入边数。
    /// </summary>
    private readonly Dictionary<string, int> _incoming = new(StringComparer.Ordinal);

    /// <summary>
    /// 建索引。
    /// </summary>
    public WorkflowDefinitionGraph(WorkflowDefinition definition)
    {
        Definition = definition;
        foreach (var node in definition.Nodes)
        {
            _nodes.TryAdd(node.Id, node);
            if (StartNode is null && string.Equals(node.ActivityType.Value, ActivityTypes.Start, StringComparison.Ordinal))
            {
                StartNode = node;
            }
        }

        foreach (var transition in definition.Transitions)
        {
            if (!_outgoing.TryGetValue(transition.SourceNodeId, out var list))
            {
                list = [];
                _outgoing[transition.SourceNodeId] = list;
            }

            list.Add(transition);
            _incoming[transition.TargetNodeId] = _incoming.GetValueOrDefault(transition.TargetNodeId) + 1;
        }

        foreach (var list in _outgoing.Values)
        {
            // 稳定排序：优先级相同的保持定义顺序
            var ordered = list.Select(static (transition, index) => (transition, index)).OrderBy(static pair => pair.transition.Priority).ThenBy(static pair => pair.index).Select(static pair => pair.transition).ToList();
            list.Clear();
            list.AddRange(ordered);
        }
    }

    /// <summary>
    /// 定义。
    /// </summary>
    public WorkflowDefinition Definition { get; }

    /// <summary>
    /// 开始节点。
    /// </summary>
    /// <remarks>
    /// 定义不合法时为 <see langword="null"/>。
    /// </remarks>
    public WorkflowNode? StartNode { get; }

    /// <summary>
    /// 按 ID 找节点。
    /// </summary>
    public WorkflowNode? Node(string nodeId) => _nodes.GetValueOrDefault(nodeId);

    /// <summary>
    /// 出边。
    /// </summary>
    public IReadOnlyList<WorkflowTransition> Outgoing(string nodeId) => _outgoing.TryGetValue(nodeId, out var list) ? list : [];

    /// <summary>
    /// 入边数。
    /// </summary>
    public int IncomingCount(string nodeId) => _incoming.GetValueOrDefault(nodeId);
}
