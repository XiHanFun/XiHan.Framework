// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Instances;

/// <summary>
/// 实例列表查询。
/// </summary>
/// <remarks>
/// 过滤项都可空、同时给出的取交集；结果按创建时间降序，跳过 <see cref="Skip"/> 条后最多 <see cref="Limit"/> 条。查询只在调用方自己的分区里进行（平台调用只查平台的实例，租户只查本租户的），没有跨租户的查询；过滤、排序、分页与计数都由存储执行（数据库实现是 SQL）。分页与关联 ID 集合越界不在构造时抛出：查询常由请求绑定而来，越界是调用方可以改正的输入，由 <see cref="Engine.IWorkflowEngine.ListInstancesAsync"/> 与 <see cref="Engine.IWorkflowEngine.CountInstancesAsync"/> 返回 <c>WORKFLOW_VALIDATION_002</c>（HTTP 400）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowInstanceQuery
{
    /// <summary>
    /// 上限的最大值。
    /// </summary>
    public const int MaxLimit = 1000;

    /// <summary>
    /// 关联 ID 集合的最大个数。
    /// </summary>
    /// <remarks>
    /// 与一页的上限相同：一页业务单据各查一次进度，一次查询就够；数据库实现翻译成一个 <c>IN</c>（ADR-0096）。
    /// </remarks>
    public const int MaxCorrelationIds = 1000;

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
    /// 关联 ID 集合：实例的关联 ID 是其中之一即匹配。
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> 不过滤；空集合不匹配任何实例（要找的业务键一个都没有，列表为空、计数为 0，不进存储）——与 <see cref="Statuses"/> 的「空集合视同未限定」不同，免得一页没有单据时查出全部实例。重复的 ID 只算一个。最多 <see cref="MaxCorrelationIds"/> 个、每个都不能为空白，否则 <c>WORKFLOW_VALIDATION_002</c>（消息键 <c>workflow.instance.query_correlations_invalid</c>）。子流程继承父实例的关联 ID：按单据查进度时配合 <see cref="TopLevelOnly"/>。
    /// </remarks>
    public IReadOnlyList<string>? CorrelationIds { get; init; }

    /// <summary>
    /// 父实例 ID。
    /// </summary>
    public string? ParentInstanceId { get; init; }

    /// <summary>
    /// 是否只取顶层实例（没有父实例）。
    /// </summary>
    /// <remarks>
    /// 子流程继承父实例的发起人与关联 ID，「我发起的」与按单据查进度要排除它们（ADR-0096）。与 <see cref="ParentInstanceId"/> 同时给出时取交集，结果为空。
    /// </remarks>
    public bool TopLevelOnly { get; init; }

    /// <summary>
    /// 标题关键字。
    /// </summary>
    /// <remarks>
    /// 不区分大小写地包含在实例标题（<see cref="WorkflowInstance.Title"/>：启动时给的显示名，没给取定义的显示名，再没有取定义名）里即匹配，规则见 <see cref="WorkflowKeyword"/>；为 <see langword="null"/>、空或只有空白时不过滤。
    /// </remarks>
    public string? Keyword { get; init; }

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
        var keyword = WorkflowKeyword.Normalize(Keyword);
        return statusMatches
            && (DefinitionName is null || instance.DefinitionName == DefinitionName.Value)
            && (CorrelationIds is null || (instance.CorrelationId is { } correlation && CorrelationIds.Contains(correlation, StringComparer.Ordinal)))
            && (ParentInstanceId is null || string.Equals(instance.ParentInstanceId, ParentInstanceId, StringComparison.Ordinal))
            && (!TopLevelOnly || instance.ParentInstanceId is null)
            && (keyword is null || WorkflowKeyword.Matches(keyword, instance.Title))
            && (StarterId is null || string.Equals(instance.StarterId, StarterId, StringComparison.Ordinal))
            && (CreatedFrom is null || instance.CreatedAt >= CreatedFrom.Value)
            && (CreatedTo is null || instance.CreatedAt < CreatedTo.Value)
            && (EndedBefore is null || (instance.EndedAt is { } ended && ended < EndedBefore.Value));
    }
}
