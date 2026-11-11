// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Instances;

/// <summary>
/// 交给扩展代码的实例只读视图
/// </summary>
/// <remarks>
/// 活动、结果处理器与指派规则经它读实例；它跟随引擎正在修改的实例，状态变更只经引擎（活动经输出与结果）。
/// 变量经上下文的 <c>Variables</c> 读取，汇合记录是引擎的内部状态，都不在这里
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WorkflowInstanceView
{
    /// <summary>
    /// 被查看的实例
    /// </summary>
    private readonly WorkflowInstance _instance;

    /// <summary>
    /// 由引擎创建
    /// </summary>
    internal WorkflowInstanceView(WorkflowInstance instance)
    {
        _instance = instance;
    }

    /// <summary>
    /// 实例 ID
    /// </summary>
    public string Id => _instance.Id;

    /// <summary>
    /// 定义 ID
    /// </summary>
    public string DefinitionId => _instance.DefinitionId;

    /// <summary>
    /// 定义名
    /// </summary>
    public WorkflowName DefinitionName => _instance.DefinitionName;

    /// <summary>
    /// 定义版本
    /// </summary>
    public int DefinitionVersion => _instance.DefinitionVersion;

    /// <summary>
    /// 显示名
    /// </summary>
    public string? DisplayName => _instance.DisplayName;

    /// <summary>
    /// 显示名或定义名
    /// </summary>
    public string Title => _instance.Title;

    /// <summary>
    /// 状态
    /// </summary>
    public WorkflowInstanceStatus Status => _instance.Status;

    /// <summary>
    /// 是否已到终态
    /// </summary>
    public bool IsFinal => _instance.IsFinal;

    /// <summary>
    /// 业务关联 ID（如单据 ID）
    /// </summary>
    public string? CorrelationId => _instance.CorrelationId;

    /// <summary>
    /// 发起人
    /// </summary>
    public string? StarterId => _instance.StarterId;

    /// <summary>
    /// 父实例 ID（子流程）
    /// </summary>
    public string? ParentInstanceId => _instance.ParentInstanceId;

    /// <summary>
    /// 父实例里启动本实例的节点实例 ID
    /// </summary>
    public string? ParentNodeInstanceId => _instance.ParentNodeInstanceId;

    /// <summary>
    /// 子流程深度，顶层为 0
    /// </summary>
    public int Depth => _instance.Depth;

    /// <summary>
    /// 租户
    /// </summary>
    public string? TenantId => _instance.TenantId;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt => _instance.CreatedAt;

    /// <summary>
    /// 开始执行时间
    /// </summary>
    public DateTimeOffset? StartedAt => _instance.StartedAt;

    /// <summary>
    /// 结束时间（到达终态）
    /// </summary>
    public DateTimeOffset? EndedAt => _instance.EndedAt;

    /// <summary>
    /// 故障
    /// </summary>
    public WorkflowFault? Fault => _instance.Fault;

    /// <summary>
    /// 故障节点 ID
    /// </summary>
    public string? FaultNodeId => _instance.FaultNodeId;

    /// <summary>
    /// 故障节点实例 ID
    /// </summary>
    /// <remarks>
    /// 引擎级故障为 <see langword="null"/>
    /// </remarks>
    public string? FaultNodeInstanceId => _instance.FaultNodeInstanceId;

    /// <summary>
    /// 取消或终止的原因
    /// </summary>
    public string? CancellationReason => _instance.CancellationReason;

    /// <summary>
    /// 结束节点声明的结果
    /// </summary>
    public string? Outcome => _instance.Outcome;
}
