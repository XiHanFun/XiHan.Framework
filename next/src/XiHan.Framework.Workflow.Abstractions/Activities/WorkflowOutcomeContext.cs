// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 结果处理器的上下文。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WorkflowOutcomeContext
{
    /// <summary>
    /// 由结束活动创建。
    /// </summary>
    internal WorkflowOutcomeContext(ActivityContext activity, string outcome)
    {
        Activity = activity;
        Outcome = outcome;
    }

    /// <summary>
    /// 结束节点的执行上下文（定义、实例、变量、服务）。
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
    /// 结束节点。
    /// </summary>
    public WorkflowNode Node => Activity.Node;

    /// <summary>
    /// 结果。
    /// </summary>
    public string Outcome { get; }

    /// <summary>
    /// 实例变量。
    /// </summary>
    public WorkflowVariables Variables => Activity.Variables;

    /// <summary>
    /// 服务作用域。
    /// </summary>
    public IServiceProvider Services => Activity.Services;
}
