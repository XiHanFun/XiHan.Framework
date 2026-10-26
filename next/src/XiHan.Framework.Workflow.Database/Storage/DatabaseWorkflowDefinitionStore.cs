// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Serialization;
using XiHan.Framework.Workflow.Abstractions.Storage;
using XiHan.Framework.Workflow.Database.Entities;

namespace XiHan.Framework.Workflow.Database.Storage;

/// <summary>
/// 数据库定义存储（单例）：每个操作在新作用域里经完整管道执行；定义以 JSON 文档存储，读出来的对象天然是快照。管道失败 → <see cref="WorkflowErrors.StoreUnavailable"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DatabaseWorkflowDefinitionStore : IWorkflowDefinitionStore
{
    /// <summary>
    /// 管道执行。
    /// </summary>
    private readonly StoreInvocation _invocation;

    /// <summary>
    /// 创建。
    /// </summary>
    public DatabaseWorkflowDefinitionStore(IServiceProvider services, App app)
    {
        _invocation = new StoreInvocation(services, app);
    }

    /// <inheritdoc />
    public async ValueTask<Result<Maybe<WorkflowDefinition>>> FindAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(async ctx =>
        {
            var found = await StoreInvocation.Repository<WorkflowDefinitionRecord>(ctx).FindAsync(id, ctx.CancellationToken).ConfigureAwait(false);
            return found.TryGetValue(out var record) ? ToDefinition(record).Map(static definition => Maybe.Some(definition)) : Result.Success(Maybe.None<WorkflowDefinition>());
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<WorkflowDefinition>>> FindManyAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(async ctx =>
        {
            var records = await RecordQueries.FindManyAsync(StoreInvocation.Repository<WorkflowDefinitionRecord>(ctx), ids, ctx.CancellationToken).ConfigureAwait(false);
            return ToDefinitions(records);
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public ValueTask<Result<Maybe<WorkflowDefinition>>> FindAsync(WorkflowName name, int version, CancellationToken cancellationToken)
    {
        var value = name.Value;
        return FirstAsync(EntityQuery.All<WorkflowDefinitionRecord>().Where(new Specification<WorkflowDefinitionRecord>(record => record.Name == value && record.Version == version)), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<Result<Maybe<WorkflowDefinition>>> FindLatestPublishedAsync(WorkflowName name, CancellationToken cancellationToken)
    {
        var value = name.Value;
        var published = nameof(WorkflowDefinitionStatus.Published);
        return FirstAsync(
            EntityQuery.All<WorkflowDefinitionRecord>()
                .Where(new Specification<WorkflowDefinitionRecord>(record => record.Name == value && record.Status == published))
                .OrderBy(static record => record.Version, descending: true),
            cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<Result<int>> MaxVersionAsync(WorkflowName name, CancellationToken cancellationToken)
    {
        var value = name.Value;
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(async ctx =>
        {
            var query = EntityQuery.All<WorkflowDefinitionRecord>()
                .Where(new Specification<WorkflowDefinitionRecord>(record => record.Name == value))
                .OrderBy(static record => record.Version, descending: true)
                .Page(0, 1);
            var top = await StoreInvocation.Repository<WorkflowDefinitionRecord>(ctx).ListAsync(query, ctx.CancellationToken).ConfigureAwait(false);
            return Result.Success(top.Count == 0 ? 0 : top[0].Version);
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<WorkflowDefinition>>> ListAsync(WorkflowName? name, WorkflowDefinitionStatus? status, CancellationToken cancellationToken)
    {
        var query = EntityQuery.All<WorkflowDefinitionRecord>();
        if (name is { } wanted)
        {
            var value = wanted.Value;
            query = query.Where(new Specification<WorkflowDefinitionRecord>(record => record.Name == value));
        }

        if (status is { } state)
        {
            var statusName = state.ToString();
            query = query.Where(new Specification<WorkflowDefinitionRecord>(record => record.Status == statusName));
        }

        // 名字升序、版本降序；再按 ID 把排序补成全序，分页才稳定
        query = query.OrderBy(static record => record.Name).OrderBy(static record => record.Version, descending: true).OrderBy(static record => record.Id);
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(async ctx =>
        {
            var records = await RecordQueries.ListAllAsync(StoreInvocation.Repository<WorkflowDefinitionRecord>(ctx), query, null, ctx.CancellationToken).ConfigureAwait(false);
            return ToDefinitions(records);
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result> SaveAsync(IReadOnlyList<WorkflowDefinition> definitions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var rows = definitions.Select(ToRecord).ToList();
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(
            ctx => RecordQueries.UpsertAsync(StoreInvocation.Repository<WorkflowDefinitionRecord>(ctx), rows, Copy, ctx.CancellationToken),
            cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result> DeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(async ctx =>
        {
            var repository = StoreInvocation.Repository<WorkflowDefinitionRecord>(ctx);
            var rows = await RecordQueries.FindManyAsync(repository, ids, ctx.CancellationToken).ConfigureAwait(false);
            return await RecordQueries.RemoveManyAsync(repository, rows, ctx.CancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// 取查询的第一条。
    /// </summary>
    private async ValueTask<Result<Maybe<WorkflowDefinition>>> FirstAsync(EntityQuery<WorkflowDefinitionRecord> query, CancellationToken cancellationToken)
    {
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(async ctx =>
        {
            var top = await StoreInvocation.Repository<WorkflowDefinitionRecord>(ctx).ListAsync(query.Page(0, 1), ctx.CancellationToken).ConfigureAwait(false);
            return top.Count == 0 ? Result.Success(Maybe.None<WorkflowDefinition>()) : ToDefinition(top[0]).Map(static definition => Maybe.Some(definition));
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// 定义 → 行。
    /// </summary>
    private static WorkflowDefinitionRecord ToRecord(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var record = new WorkflowDefinitionRecord { Id = definition.Id };
        Fill(definition, record);
        return record;
    }

    /// <summary>
    /// 把定义的列与文档写进行。
    /// </summary>
    private static void Fill(WorkflowDefinition definition, WorkflowDefinitionRecord record)
    {
        record.Name = definition.Name.Value;
        record.Version = definition.Version;
        record.Status = definition.Status.ToString();
        record.TenantId = definition.TenantId;
        record.CreatedAt = definition.CreatedAt;
        record.PublishedAt = definition.PublishedAt;
        record.Document = WorkflowJson.Serialize(definition);
    }

    /// <summary>
    /// 把新行的内容复制到已有行上。
    /// </summary>
    private static void Copy(WorkflowDefinitionRecord source, WorkflowDefinitionRecord target)
    {
        target.Name = source.Name;
        target.Version = source.Version;
        target.Status = source.Status;
        target.TenantId = source.TenantId;
        target.CreatedAt = source.CreatedAt;
        target.PublishedAt = source.PublishedAt;
        target.Document = source.Document;
    }

    /// <summary>
    /// 行 → 定义；文档不合法视为存储损坏。
    /// </summary>
    private static Result<WorkflowDefinition> ToDefinition(WorkflowDefinitionRecord record) => WorkflowJson.DeserializeDefinition(record.Document);

    /// <summary>
    /// 多行 → 定义；任一行不合法即失败。
    /// </summary>
    private static Result<IReadOnlyList<WorkflowDefinition>> ToDefinitions(List<WorkflowDefinitionRecord> records)
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
