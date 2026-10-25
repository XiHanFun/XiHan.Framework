// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Activities;

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 流式构造定义：节点、转移、变量；<see cref="Build"/> 得到草稿（ID 与创建时间由目录在 <c>CreateAsync</c> 时分配）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WorkflowDefinitionBuilder
{
    /// <summary>
    /// 节点。
    /// </summary>
    private readonly List<WorkflowNode> _nodes = [];

    /// <summary>
    /// 转移。
    /// </summary>
    private readonly List<WorkflowTransition> _transitions = [];

    /// <summary>
    /// 变量声明。
    /// </summary>
    private readonly List<WorkflowVariableDefinition> _variables = [];

    /// <summary>
    /// 名字。
    /// </summary>
    private readonly WorkflowName _name;

    /// <summary>
    /// 显示名。
    /// </summary>
    private readonly string? _displayName;

    /// <summary>
    /// 说明。
    /// </summary>
    private string? _description;

    /// <summary>
    /// 分类。
    /// </summary>
    private string? _category;

    /// <summary>
    /// 启用补偿。
    /// </summary>
    private bool _compensation;

    /// <summary>
    /// 扩展属性。
    /// </summary>
    private JsonObject? _properties;

    /// <summary>
    /// 创建。
    /// </summary>
    private WorkflowDefinitionBuilder(WorkflowName name, string? displayName)
    {
        _name = name;
        _displayName = displayName;
    }

    /// <summary>
    /// 开始构造。
    /// </summary>
    public static WorkflowDefinitionBuilder Create(WorkflowName name, string? displayName = null) => new(name, displayName);

    /// <summary>
    /// 说明。
    /// </summary>
    public WorkflowDefinitionBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// 分类。
    /// </summary>
    public WorkflowDefinitionBuilder WithCategory(string? category)
    {
        _category = category;
        return this;
    }

    /// <summary>
    /// 取消时按完成顺序逆序补偿。
    /// </summary>
    public WorkflowDefinitionBuilder WithCompensation(bool enabled = true)
    {
        _compensation = enabled;
        return this;
    }

    /// <summary>
    /// 扩展属性。
    /// </summary>
    public WorkflowDefinitionBuilder WithProperties(JsonObject? properties)
    {
        _properties = properties;
        return this;
    }

    /// <summary>
    /// 声明变量。
    /// </summary>
    public WorkflowDefinitionBuilder AddVariable(string name, bool required = false, JsonNode? defaultValue = null, string? description = null)
    {
        _variables.Add(new WorkflowVariableDefinition { Name = name, Required = required, DefaultValue = defaultValue, Description = description });
        return this;
    }

    /// <summary>
    /// 加节点。
    /// </summary>
    public WorkflowDefinitionBuilder AddNode(string id, ActivityType activityType, string? displayName = null, Action<WorkflowNodeBuilder>? configure = null)
    {
        var builder = new WorkflowNodeBuilder(id, activityType, displayName);
        configure?.Invoke(builder);
        _nodes.Add(builder.Build());
        return this;
    }

    /// <summary>
    /// 开始节点。
    /// </summary>
    public WorkflowDefinitionBuilder AddStart(string id = "start", string? displayName = null) => AddNode(id, ActivityTypes.Start, displayName);

    /// <summary>
    /// 结束节点；<paramref name="outcome"/> 非空即带结果结束实例。
    /// </summary>
    public WorkflowDefinitionBuilder AddEnd(string id = "end", string? outcome = null, string? displayName = null)
        => AddNode(id, ActivityTypes.End, displayName, outcome is null ? null : builder => builder.WithProperty(EndActivity.OutcomeProperty, outcome));

    /// <summary>
    /// 决策节点。
    /// </summary>
    public WorkflowDefinitionBuilder AddDecision(string id, string? displayName = null) => AddNode(id, ActivityTypes.Decision, displayName);

    /// <summary>
    /// 并行网关。
    /// </summary>
    public WorkflowDefinitionBuilder AddParallel(string id, string? displayName = null) => AddNode(id, ActivityTypes.Parallel, displayName);

    /// <summary>
    /// 汇合节点。
    /// </summary>
    public WorkflowDefinitionBuilder AddJoin(string id, bool waitAny = false, string? displayName = null)
        => AddNode(id, ActivityTypes.Join, displayName, builder => builder.WithProperty(JoinActivity.ModeProperty, waitAny ? JoinActivity.WaitAny : JoinActivity.WaitAll));

    /// <summary>
    /// 延时节点。
    /// </summary>
    public WorkflowDefinitionBuilder AddDelay(string id, TimeSpan duration, string? displayName = null)
        => AddNode(id, ActivityTypes.Delay, displayName, builder => builder.WithProperty(DelayActivity.DurationProperty, JsonValue.Create(duration.TotalSeconds)));

    /// <summary>
    /// 用户任务节点。
    /// </summary>
    public WorkflowDefinitionBuilder AddUserTask(string id, string? displayName, Action<WorkflowNodeBuilder> configure) => AddNode(id, ActivityTypes.UserTask, displayName, configure);

    /// <summary>
    /// 加转移；ID 为 <c>源->目标#序号</c>。
    /// </summary>
    public WorkflowDefinitionBuilder AddTransition(string sourceNodeId, string targetNodeId, string? condition = null, int priority = 0, string? displayName = null)
    {
        _transitions.Add(new WorkflowTransition
        {
            Id = TransitionId(sourceNodeId, targetNodeId),
            SourceNodeId = sourceNodeId,
            TargetNodeId = targetNodeId,
            Condition = condition,
            Priority = priority,
            DisplayName = displayName,
        });
        return this;
    }

    /// <summary>
    /// 加默认转移（其他出边都不匹配时走）。
    /// </summary>
    public WorkflowDefinitionBuilder AddDefaultTransition(string sourceNodeId, string targetNodeId, string? displayName = null)
    {
        _transitions.Add(new WorkflowTransition
        {
            Id = TransitionId(sourceNodeId, targetNodeId),
            SourceNodeId = sourceNodeId,
            TargetNodeId = targetNodeId,
            IsDefault = true,
            Priority = int.MaxValue,
            DisplayName = displayName,
        });
        return this;
    }

    /// <summary>
    /// 生成定义（草稿，版本 1；ID 是新的 UUID v7）。
    /// </summary>
    public WorkflowDefinition Build() => new()
    {
        Id = WorkflowIdentifiers.NewId(),
        Name = _name,
        DisplayName = _displayName,
        Description = _description,
        Category = _category,
        EnableCompensation = _compensation,
        Nodes = _nodes.ToList(),
        Transitions = _transitions.ToList(),
        Variables = _variables.ToList(),
        Properties = _properties,
    };

    /// <summary>
    /// 转移 ID：同一对节点之间的第 n 条。
    /// </summary>
    private string TransitionId(string source, string target)
    {
        var count = _transitions.Count(transition => string.Equals(transition.SourceNodeId, source, StringComparison.Ordinal) && string.Equals(transition.TargetNodeId, target, StringComparison.Ordinal));
        return $"{source}-{target}.{(count + 1).ToString(CultureInfo.InvariantCulture)}";
    }
}
