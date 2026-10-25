// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Assignees;

/// <summary>
/// 一次指派解析的上下文：规则与所在的节点执行上下文。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class AssigneeResolution
{
    /// <summary>
    /// 创建。
    /// </summary>
    public AssigneeResolution(AssigneeRule rule, ActivityContext activity)
    {
        Rule = rule ?? throw new ArgumentNullException(nameof(rule));
        Activity = activity ?? throw new ArgumentNullException(nameof(activity));
    }

    /// <summary>
    /// 规则。
    /// </summary>
    public AssigneeRule Rule { get; }

    /// <summary>
    /// 节点执行上下文（定义、实例、节点、变量、服务）。
    /// </summary>
    public ActivityContext Activity { get; }

    /// <summary>
    /// 定义。
    /// </summary>
    public WorkflowDefinition Definition => Activity.Definition;

    /// <summary>
    /// 实例。
    /// </summary>
    public WorkflowInstance Instance => Activity.Instance;

    /// <summary>
    /// 节点。
    /// </summary>
    public WorkflowNode Node => Activity.Node;

    /// <summary>
    /// 实例变量。
    /// </summary>
    public WorkflowVariables Variables => Activity.Variables;

    /// <summary>
    /// 服务作用域。
    /// </summary>
    public IServiceProvider Services => Activity.Services;
}
