// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 定义列表查询。
/// </summary>
/// <remarks>
/// 过滤项都可空、同时给出的取交集；结果按名字升序、版本降序，跳过 <see cref="Skip"/> 条后最多 <see cref="Limit"/> 条。查询只在调用方自己的分区里进行，过滤、排序、分页与计数都由存储执行（数据库实现是 SQL，ADR-0096）。分页越界不在构造时抛出，由 <see cref="IWorkflowDefinitionCatalog.QueryAsync"/> 返回 <c>WORKFLOW_VALIDATION_002</c>（HTTP 400）：查询常由请求绑定而来，越界是调用方可以改正的输入。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowDefinitionQuery
{
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
            && (Status is null || definition.Status == Status.Value);
    }
}
