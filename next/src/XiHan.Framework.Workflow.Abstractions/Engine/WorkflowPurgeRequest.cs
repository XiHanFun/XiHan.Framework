// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.Abstractions.Engine;

/// <summary>
/// 清理请求：删除在 <see cref="EndedBefore"/> 之前结束的实例及其节点实例与书签。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowPurgeRequest
{
    /// <summary>
    /// 只删除在此时刻之前（不含）结束的实例。
    /// </summary>
    public required DateTimeOffset EndedBefore { get; init; }

    /// <summary>
    /// 只删除这些状态的实例。
    /// </summary>
    /// <remarks>
    /// 空为全部终态。
    /// </remarks>
    public IReadOnlyList<WorkflowInstanceStatus>? Statuses { get; init; }

    /// <summary>
    /// 只删除该定义名的实例。
    /// </summary>
    /// <remarks>
    /// 空为全部。
    /// </remarks>
    public WorkflowName? DefinitionName { get; init; }

    /// <summary>
    /// 一次最多删除条数，1 到 <see cref="WorkflowInstanceQuery.MaxLimit"/>。
    /// </summary>
    /// <remarks>
    /// 默认 1000。
    /// </remarks>
    public int Limit
    {
        get => field;
        init => field = value is >= 1 and <= WorkflowInstanceQuery.MaxLimit ? value : throw new ArgumentOutOfRangeException(nameof(value), value, $"The limit must be between 1 and {WorkflowInstanceQuery.MaxLimit}.");
    } = WorkflowInstanceQuery.MaxLimit;
}
