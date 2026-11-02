// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Serialization;
using XiHan.Framework.Workflow.Database.Entities;

namespace XiHan.Framework.Workflow.Database.Storage;

/// <summary>
/// 一张定义表（平台或租户分区）的读写。
/// </summary>
/// <remarks>
/// 租户分区的表由持久化按工作单元的租户过滤，查询里不写租户条件；平台分区的表只有平台的行。定义以 JSON 文档存储，读出来的对象天然是快照。
/// </remarks>
/// <typeparam name="TRow">行类型。</typeparam>
internal sealed class DefinitionRows<TRow>(IRepository<TRow, string> repository)
    where TRow : WorkflowDefinitionRow, new()
{
    /// <summary>
    /// 按 ID 找。
    /// </summary>
    public async ValueTask<Result<Maybe<WorkflowDefinition>>> FindAsync(string id, CancellationToken cancellationToken)
    {
        var found = await repository.FindAsync(id, cancellationToken).ConfigureAwait(false);
        return found.TryGetValue(out var record) ? ToDefinition(record).Map(static definition => Maybe.Some(definition)) : Result.Success(Maybe.None<WorkflowDefinition>());
    }

    /// <summary>
    /// 按 ID 批量找。
    /// </summary>
    public async ValueTask<Result<IReadOnlyList<WorkflowDefinition>>> FindManyAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
        => ToDefinitions(await RecordQueries.FindManyAsync(repository, ids, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 按名字与版本找。
    /// </summary>
    public ValueTask<Result<Maybe<WorkflowDefinition>>> FindAsync(WorkflowName name, int version, CancellationToken cancellationToken)
    {
        var value = name.Value;
        return FirstAsync(EntityQuery.All<TRow>().Where(new Specification<TRow>(record => record.Name == value && record.Version == version)), cancellationToken);
    }

    /// <summary>
    /// 该名字下版本号最大的已发布定义。
    /// </summary>
    public ValueTask<Result<Maybe<WorkflowDefinition>>> FindLatestPublishedAsync(WorkflowName name, CancellationToken cancellationToken)
    {
        var value = name.Value;
        var published = nameof(WorkflowDefinitionStatus.Published);
        return FirstAsync(
            EntityQuery.All<TRow>()
                .Where(new Specification<TRow>(record => record.Name == value && record.Status == published))
                .OrderBy(static record => record.Version, descending: true),
            cancellationToken);
    }

    /// <summary>
    /// 该名字下的最大版本号，没有为 0。
    /// </summary>
    public async ValueTask<Result<int>> MaxVersionAsync(WorkflowName name, CancellationToken cancellationToken)
    {
        var value = name.Value;
        var query = EntityQuery.All<TRow>()
            .Where(new Specification<TRow>(record => record.Name == value))
            .OrderBy(static record => record.Version, descending: true)
            .Page(0, 1);
        var top = await repository.ListAsync(query, cancellationToken).ConfigureAwait(false);
        return top.Count == 0 ? 0 : top[0].Version;
    }

    /// <summary>
    /// 列表：名字升序、版本降序。
    /// </summary>
    public async ValueTask<Result<IReadOnlyList<WorkflowDefinition>>> ListAsync(WorkflowName? name, WorkflowDefinitionStatus? status, CancellationToken cancellationToken)
    {
        var query = EntityQuery.All<TRow>();
        if (name is { } wanted)
        {
            var value = wanted.Value;
            query = query.Where(new Specification<TRow>(record => record.Name == value));
        }

        if (status is { } state)
        {
            var statusName = state.ToString();
            query = query.Where(new Specification<TRow>(record => record.Status == statusName));
        }

        // 名字升序、版本降序；再按 ID 把排序补成全序，分页才稳定
        query = query.OrderBy(static record => record.Name).OrderBy(static record => record.Version, descending: true).OrderBy(static record => record.Id);
        return ToDefinitions(await RecordQueries.ListAllAsync(repository, query, null, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// 定义查询的一页摘要。
    /// </summary>
    /// <remarks>
    /// 过滤、排序与分页都在 SQL 里；按排序字段与方向，再接名字升序、版本降序与 ID 补成全序（<see cref="Sorted"/>）。仓储读回整行，摘要从文档取，与按 ID 找到的定义一致。
    /// </remarks>
    public async ValueTask<Result<IReadOnlyList<WorkflowDefinitionSummary>>> ListSummariesAsync(WorkflowDefinitionQuery query, CancellationToken cancellationToken)
    {
        var paged = Sorted(Filter(query), query).Page(query.Skip, query.Limit);
        var definitions = ToDefinitions([.. await repository.ListAsync(paged, cancellationToken).ConfigureAwait(false)]);
        return definitions.TryGetValue(out var values)
            ? Result.Success<IReadOnlyList<WorkflowDefinitionSummary>>(values.Select(WorkflowDefinitionSummary.From).ToList())
            : definitions.Error!;
    }

    /// <summary>
    /// 定义查询的总数。
    /// </summary>
    public async ValueTask<Result<long>> CountAsync(WorkflowDefinitionQuery query, CancellationToken cancellationToken)
        => await repository.CountAsync(Filter(query), cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// 新增或整体替换。
    /// </summary>
    public ValueTask<Result> SaveAsync(IReadOnlyList<WorkflowDefinition> definitions, CancellationToken cancellationToken)
        => RecordQueries.UpsertAsync(repository, definitions.Select(ToRecord).ToList(), Copy, cancellationToken);

    /// <summary>
    /// 删除，不存在的忽略。
    /// </summary>
    public async ValueTask<Result> DeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var rows = await RecordQueries.FindManyAsync(repository, ids, cancellationToken).ConfigureAwait(false);
        return await RecordQueries.RemoveManyAsync(repository, rows, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 把定义查询的过滤项逐项翻译成条件。
    /// </summary>
    /// <remarks>
    /// 每个给出的条件一个规约，由查询取交集；没给的条件不写进 SQL（不写 <c>参数 == null ||</c> 这类条件，SqlSugar 对它的翻译不可靠）。关键字对查询列做包含判断：两边都已小写折叠，按字面由持久化的 <c>Contains</c> 保证（RFC-0005，ADR-0098）；先判非空，升级前写入、还没回填的行不匹配。
    /// </remarks>
    private static EntityQuery<TRow> Filter(WorkflowDefinitionQuery query)
    {
        var filtered = EntityQuery.All<TRow>();
        if (WorkflowKeyword.Normalize(query.Keyword) is { } keyword)
        {
            filtered = filtered.Where(new Specification<TRow>(record => record.SearchText != null && record.SearchText.Contains(keyword)));
        }

        if (query.Name is { } name)
        {
            var value = name.Value;
            filtered = filtered.Where(new Specification<TRow>(record => record.Name == value));
        }

        if (query.Category is { } category)
        {
            filtered = filtered.Where(new Specification<TRow>(record => record.Category == category));
        }

        if (query.Status is { } status)
        {
            var statusName = status.ToString();
            filtered = filtered.Where(new Specification<TRow>(record => record.Status == statusName));
        }

        if (query.CreatedFrom is { } createdFrom)
        {
            filtered = filtered.Where(new Specification<TRow>(record => record.CreatedAt >= createdFrom));
        }

        if (query.CreatedTo is { } createdTo)
        {
            filtered = filtered.Where(new Specification<TRow>(record => record.CreatedAt < createdTo));
        }

        return filtered;
    }

    /// <summary>
    /// 按定义查询的排序字段与方向排序，再接次序键补成全序。
    /// </summary>
    /// <remarks>
    /// 次序键是名字升序、版本降序、ID 升序（ADR-0102）：名字与版本在分区里唯一，ID 只为让全序不依赖唯一索引。文本、状态名按列的排序规则比较，发布时间的空值位置照数据库（PostgreSQL、Oracle 当作最大，SQLite、MySQL、SQL Server 当作最小）。排序字段不在白名单里抛出：调用方（目录）已校验，走到这里是编程错误。
    /// </remarks>
    private static EntityQuery<TRow> Sorted(EntityQuery<TRow> query, WorkflowDefinitionQuery definitionQuery)
    {
        var descending = definitionQuery.Descending;
        var sorted = definitionQuery.SortBy switch
        {
            WorkflowDefinitionSortField.Name => query.OrderBy(static record => record.Name, descending),
            WorkflowDefinitionSortField.Title => query.OrderBy(static record => record.Title, descending),
            WorkflowDefinitionSortField.Version => query.OrderBy(static record => record.Version, descending),
            WorkflowDefinitionSortField.Status => query.OrderBy(static record => record.Status, descending),
            WorkflowDefinitionSortField.CreatedAt => query.OrderBy(static record => record.CreatedAt, descending),
            WorkflowDefinitionSortField.PublishedAt => query.OrderBy(static record => record.PublishedAt, descending),
            _ => throw new ArgumentOutOfRangeException(nameof(definitionQuery), definitionQuery.SortBy, $"The workflow definition sort field '{definitionQuery.SortBy}' is not supported."),
        };

        // 次序键跳过与排序字段相同的列：SQL Server 不许同一列在 ORDER BY 里出现两次
        if (definitionQuery.SortBy != WorkflowDefinitionSortField.Name)
        {
            sorted = sorted.OrderBy(static record => record.Name);
        }

        if (definitionQuery.SortBy != WorkflowDefinitionSortField.Version)
        {
            sorted = sorted.OrderBy(static record => record.Version, descending: true);
        }

        return sorted.OrderBy(static record => record.Id);
    }

    /// <summary>
    /// 取查询的第一条。
    /// </summary>
    private async ValueTask<Result<Maybe<WorkflowDefinition>>> FirstAsync(EntityQuery<TRow> query, CancellationToken cancellationToken)
    {
        var top = await repository.ListAsync(query.Page(0, 1), cancellationToken).ConfigureAwait(false);
        return top.Count == 0 ? Result.Success(Maybe.None<WorkflowDefinition>()) : ToDefinition(top[0]).Map(static definition => Maybe.Some(definition));
    }

    /// <summary>
    /// 定义 → 行。
    /// </summary>
    /// <remarks>
    /// 租户列由持久化在新增时按工作单元的租户写入，这里不设。
    /// </remarks>
    private static TRow ToRecord(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new TRow
        {
            Id = definition.Id,
            Name = definition.Name.Value,
            Version = definition.Version,
            Status = definition.Status.ToString(),
            Category = definition.Category,
            SearchText = WorkflowKeyword.Join(definition.Name.Value, definition.DisplayName, definition.Description),
            Title = WorkflowSortKeys.Title(definition.Title),
            CreatedAt = definition.CreatedAt,
            PublishedAt = definition.PublishedAt,
            Document = WorkflowJson.Serialize(definition),
        };
    }

    /// <summary>
    /// 把新行的内容复制到已有行上。
    /// </summary>
    private static void Copy(TRow source, TRow target)
    {
        target.Name = source.Name;
        target.Version = source.Version;
        target.Status = source.Status;
        target.Category = source.Category;
        target.SearchText = source.SearchText;
        target.Title = source.Title;
        target.CreatedAt = source.CreatedAt;
        target.PublishedAt = source.PublishedAt;
        target.Document = source.Document;
    }

    /// <summary>
    /// 行 → 定义。
    /// </summary>
    /// <remarks>
    /// 文档不合法视为存储损坏。
    /// </remarks>
    private static Result<WorkflowDefinition> ToDefinition(TRow record) => WorkflowJson.DeserializeDefinition(record.Document);

    /// <summary>
    /// 多行 → 定义，任一行不合法即失败。
    /// </summary>
    private static Result<IReadOnlyList<WorkflowDefinition>> ToDefinitions(List<TRow> records)
    {
        var definitions = new List<WorkflowDefinition>(records.Count);
        foreach (var record in records)
        {
            var definition = ToDefinition(record);
            if (!definition.TryGetValue(out var value))
            {
                return definition.Error!;
            }

            definitions.Add(value);
        }

        return definitions;
    }
}
