// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 定义列表查询。
/// </summary>
/// <remarks>
/// 过滤项都可空、同时给出的取交集；结果按排序项 <see cref="Sorts"/> 排序（缺省名字升序、版本降序），跳过 <see cref="Skip"/> 条后最多 <see cref="Limit"/> 条。查询只在调用方自己的分区里进行，过滤、排序、分页与计数都由存储执行（数据库实现是 SQL，ADR-0096、ADR-0102、ADR-0104）。分页与排序项越界不在构造时抛出，由 <see cref="IWorkflowDefinitionCatalog.QueryAsync"/> 返回 <c>WORKFLOW_VALIDATION_002</c>（HTTP 400）：查询常由请求绑定而来，越界是调用方可以改正的输入。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowDefinitionQuery
{
    /// <summary>
    /// 排序项个数的上限。
    /// </summary>
    /// <remarks>
    /// 等于可排序字段（<see cref="WorkflowDefinitionSortField"/>）的个数：每个字段最多出现一次（ADR-0104）。
    /// </remarks>
    public const int MaxSorts = 6;

    /// <summary>
    /// 页大小的上限。
    /// </summary>
    /// <remarks>
    /// 与持久化的查询条数上限（<c>EntityQuery.DefaultMaxTake</c>）一致。
    /// </remarks>
    public const int MaxLimit = 1000;

    /// <summary>
    /// 关键字。
    /// </summary>
    /// <remarks>
    /// 不区分大小写地包含在名字、显示名或说明里即匹配，规则见 <see cref="WorkflowKeyword"/>；为 <see langword="null"/>、空或只有空白时不过滤。
    /// </remarks>
    public string? Keyword { get; init; }

    /// <summary>
    /// 定义名。
    /// </summary>
    public WorkflowName? Name { get; init; }

    /// <summary>
    /// 分类（精确匹配）。
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// 状态。
    /// </summary>
    public WorkflowDefinitionStatus? Status { get; init; }

    /// <summary>
    /// 创建时间下界（含）。
    /// </summary>
    public DateTimeOffset? CreatedFrom { get; init; }

    /// <summary>
    /// 创建时间上界（不含）。
    /// </summary>
    public DateTimeOffset? CreatedTo { get; init; }

    /// <summary>
    /// 排序项，有序：第一项是主排序字段，后面依次是次级排序字段。
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 或空列表时按名字升序（与原来相同）。全部排序项之后接次序键名字升序、版本降序（排序项里已有的字段跳过；名字与版本在分区里唯一，已是全序，数据库实现最后再按 ID），同值的行分页不重不漏（ADR-0102、ADR-0104）。文本按数据库列的排序规则比较，内存实现按序数；状态按生命周期的先后（草稿、已发布、已停用、已归档，ADR-0103）；没发布过的定义发布时间为空，空值的位置照数据库（内存实现当作最小）。不合法时 <see cref="IWorkflowDefinitionCatalog.QueryAsync"/> 返回 <c>WORKFLOW_VALIDATION_002</c>，按顺序检查：项数超过 <see cref="MaxSorts"/>（消息键 <c>workflow.definition.query_sorts_too_many</c>），某项为空或字段不是 <see cref="WorkflowDefinitionSortField"/> 里定义的值（<c>workflow.definition.query_sort_invalid</c>），同一字段出现两次（<c>workflow.definition.query_sort_duplicate</c>）。
    /// </remarks>
    public IReadOnlyList<WorkflowDefinitionSort>? Sorts { get; init; }

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
    /// 定义是否匹配全部过滤项（不含分页）。
    /// </summary>
    /// <remarks>
    /// 存储实现可用它在内存里过滤或复核。
    /// </remarks>
    public bool Matches(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var keyword = WorkflowKeyword.Normalize(Keyword);
        return (keyword is null || WorkflowKeyword.Matches(keyword, definition.Name.Value, definition.DisplayName, definition.Description))
            && (Name is null || definition.Name == Name.Value)
            && (Category is null || string.Equals(definition.Category, Category, StringComparison.Ordinal))
            && (Status is null || definition.Status == Status.Value)
            && (CreatedFrom is null || definition.CreatedAt >= CreatedFrom.Value)
            && (CreatedTo is null || definition.CreatedAt < CreatedTo.Value);
    }

    /// <summary>
    /// 排序项的校验错误。
    /// </summary>
    /// <remarks>
    /// 按顺序检查、遇到第一个就返回：项数超过上限，某项为空或字段不在白名单，同一字段出现两次（不论方向）。没给或空列表、全部合法时为 <see langword="null"/>。
    /// </remarks>
    internal Error? SortsError()
    {
        if (Sorts is not { Count: > 0 } sorts)
        {
            return null;
        }

        // 先看项数：上限就是可排序字段数，超出的列表必然有重复或非法的项，直接报个数最清楚
        if (sorts.Count > MaxSorts)
        {
            return Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.DefinitionQuerySortsTooMany, MessageArguments.Of(("count", sorts.Count), ("max", MaxSorts)));
        }

        foreach (var sort in sorts)
        {
            if (sort is null || !IsSortable(sort.Field))
            {
                return Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.DefinitionQuerySortInvalid, MessageArguments.Of(("sort", sort?.Field.ToString() ?? "null")));
            }
        }

        var seen = new HashSet<WorkflowDefinitionSortField>();
        foreach (var sort in sorts)
        {
            if (!seen.Add(sort.Field))
            {
                return Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.DefinitionQuerySortDuplicate, MessageArguments.Of(("sort", sort.Field.ToString())));
            }
        }

        return null;
    }

    /// <summary>
    /// 字段是否在白名单里。
    /// </summary>
    private static bool IsSortable(WorkflowDefinitionSortField field) => field is WorkflowDefinitionSortField.Name or WorkflowDefinitionSortField.Title or WorkflowDefinitionSortField.Version or WorkflowDefinitionSortField.Status or WorkflowDefinitionSortField.CreatedAt or WorkflowDefinitionSortField.PublishedAt;
}
