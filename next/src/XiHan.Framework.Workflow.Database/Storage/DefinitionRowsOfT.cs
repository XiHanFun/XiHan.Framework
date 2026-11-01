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
