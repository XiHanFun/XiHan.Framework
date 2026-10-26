// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Instances;

/// <summary>
/// 实例列表查询：过滤项都可空、同时给出的取交集；结果按创建时间降序，跳过 <see cref="Skip"/> 条后最多 <see cref="Limit"/> 条。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowInstanceQuery
{
    /// <summary>
    /// 上限的最大值。
    /// </summary>
    public const int MaxLimit = 1000;

    /// <summary>
    /// 状态（单个）；与 <see cref="Statuses"/> 同时给出时取并集。
    /// </summary>
    public WorkflowInstanceStatus? Status { get; init; }

    /// <summary>
    /// 状态集合；空集合视同未限定。
    /// </summary>
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
    /// 租户。
    /// </summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// 创建时间下界（含）。
    /// </summary>
    public DateTimeOffset? CreatedFrom { get; init; }

    /// <summary>
    /// 创建时间上界（不含）。
    /// </summary>
    public DateTimeOffset? CreatedTo { get; init; }

    /// <summary>
    /// 只取在此时刻之前（不含）结束的实例；未结束的实例不匹配。清理历史用。
    /// </summary>
    public DateTimeOffset? EndedBefore { get; init; }

    /// <summary>
    /// 跳过条数；默认 0。
    /// </summary>
    public int Skip
    {
        get => field;
        init => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The skip must not be negative.");
    }

    /// <summary>
    /// 最多返回条数，1 到 <see cref="MaxLimit"/>；默认 100。
    /// </summary>
    public int Limit
    {
        get => field;
        init => field = value is >= 1 and <= MaxLimit ? value : throw new ArgumentOutOfRangeException(nameof(value), value, $"The limit must be between 1 and {MaxLimit}.");
    } = 100;

    /// <summary>
    /// 实例是否匹配全部过滤项（不含分页）；存储实现可用它在内存里复核。
    /// </summary>
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
            && (TenantId is null || string.Equals(instance.TenantId, TenantId, StringComparison.Ordinal))
            && (CreatedFrom is null || instance.CreatedAt >= CreatedFrom.Value)
            && (CreatedTo is null || instance.CreatedAt < CreatedTo.Value)
            && (EndedBefore is null || (instance.EndedAt is { } ended && ended < EndedBefore.Value));
    }
}
