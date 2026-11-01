// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Instances;

/// <summary>
/// 实例列表查询。
/// </summary>
/// <remarks>
/// 过滤项都可空、同时给出的取交集；结果按创建时间降序，跳过 <see cref="Skip"/> 条后最多 <see cref="Limit"/> 条。查询只在调用方自己的分区里进行（平台调用只查平台的实例，租户只查本租户的），没有跨租户的查询。分页越界不在构造时抛出：查询常由请求绑定而来，越界是调用方可以改正的输入，由 <see cref="Engine.IWorkflowEngine.ListInstancesAsync"/> 返回 <c>WORKFLOW_VALIDATION_002</c>（HTTP 400）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowInstanceQuery
{
    /// <summary>
    /// 上限的最大值。
    /// </summary>
    public const int MaxLimit = 1000;

    /// <summary>
    /// 状态（单个）。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="Statuses"/> 同时给出时取并集。
    /// </remarks>
    public WorkflowInstanceStatus? Status { get; init; }

    /// <summary>
    /// 状态集合。
    /// </summary>
    /// <remarks>
    /// 空集合视同未限定。
    /// </remarks>
    public IReadOnlyList<WorkflowInstanceStatus>? Statuses { get; init; }

    /// <summary>
    /// 定义名。
    /// </summary>
    public WorkflowName? DefinitionName { get; init; }

    /// <summary>
    /// 关联 ID。
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// 父实例 ID。
    /// </summary>
    public string? ParentInstanceId { get; init; }

    /// <summary>
    /// 发起人。
    /// </summary>
    public string? StarterId { get; init; }

    /// <summary>
    /// 创建时间下界（含）。
    /// </summary>
    public DateTimeOffset? CreatedFrom { get; init; }

    /// <summary>
    /// 创建时间上界（不含）。
    /// </summary>
    public DateTimeOffset? CreatedTo { get; init; }

    /// <summary>
    /// 只取在此时刻之前（不含）结束的实例。
    /// </summary>
    /// <remarks>
    /// 未结束的实例不匹配。清理历史用。
    /// </remarks>
    public DateTimeOffset? EndedBefore { get; init; }

    /// <summary>
    /// 跳过条数。
    /// </summary>
    /// <remarks>
    /// 默认 0，不能为负。
    /// </remarks>
    public int Skip { get; init; }

    /// <summary>
    /// 最多返回条数，1 到 <see cref="MaxLimit"/>。
    /// </summary>
    /// <remarks>
    /// 默认 100。
    /// </remarks>
    public int Limit { get; init; } = 100;

    /// <summary>
    /// 实例是否匹配全部过滤项（不含分页）。
    /// </summary>
    /// <remarks>
    /// 存储实现可用它在内存里复核。
    /// </remarks>
    public bool Matches(WorkflowInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        // 单个状态与状态集合取并集：任一命中即匹配；都没给出视同未限定
        var statusRestricted = Status is not null || Statuses is { Count: > 0 };
        var statusMatches = !statusRestricted
            || (Status is { } single && instance.Status == single)
            || (Statuses is { Count: > 0 } set && set.Contains(instance.Status));
        return statusMatches
            && (DefinitionName is null || instance.DefinitionName == DefinitionName.Value)
            && (CorrelationId is null || string.Equals(instance.CorrelationId, CorrelationId, StringComparison.Ordinal))
            && (ParentInstanceId is null || string.Equals(instance.ParentInstanceId, ParentInstanceId, StringComparison.Ordinal))
            && (StarterId is null || string.Equals(instance.StarterId, StarterId, StringComparison.Ordinal))
            && (CreatedFrom is null || instance.CreatedAt >= CreatedFrom.Value)
            && (CreatedTo is null || instance.CreatedAt < CreatedTo.Value)
            && (EndedBefore is null || (instance.EndedAt is { } ended && ended < EndedBefore.Value));
    }
}
