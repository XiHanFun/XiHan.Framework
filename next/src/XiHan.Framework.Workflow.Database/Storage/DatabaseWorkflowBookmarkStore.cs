// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Serialization;
using XiHan.Framework.Workflow.Abstractions.Storage;
using XiHan.Framework.Workflow.Database.Entities;

namespace XiHan.Framework.Workflow.Database.Storage;

/// <summary>
/// 数据库书签存储（单例）。
/// </summary>
/// <remarks>
/// 每个操作在新作用域里经完整管道执行；到期查询按到期时间升序，其余查询按创建时间升序，同刻按写入序号。管道失败 → <see cref="WorkflowErrors.StoreUnavailable"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DatabaseWorkflowBookmarkStore : IWorkflowBookmarkStore
{
    /// <summary>
    /// 管道执行。
    /// </summary>
    private readonly StoreInvocation _invocation;

    /// <summary>
    /// 写入序号。
    /// </summary>
    private readonly WriteSequence _sequence;

    /// <summary>
    /// 创建。
    /// </summary>
    public DatabaseWorkflowBookmarkStore(IServiceProvider services, App app, TimeProvider clock)
    {
        _invocation = new StoreInvocation(services, app);
        _sequence = new WriteSequence(clock);
    }

    /// <inheritdoc />
    public async ValueTask<Result<Maybe<WorkflowBookmark>>> FindAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(async ctx =>
        {
            var found = await StoreInvocation.Repository<WorkflowBookmarkRecord>(ctx).FindAsync(id, ctx.CancellationToken).ConfigureAwait(false);
            return Result.Success(found.TryGetValue(out var record) ? Maybe.Some(WorkflowJson.DeserializeBookmark(record.Document)) : Maybe.None<WorkflowBookmark>());
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> FindManyAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(async ctx =>
        {
            var records = await RecordQueries.FindManyAsync(StoreInvocation.Repository<WorkflowBookmarkRecord>(ctx), ids, ctx.CancellationToken).ConfigureAwait(false);
            return ToBookmarks(records);
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> ByInstanceAsync(string instanceId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        return ListAsync(ByCreation(new Specification<WorkflowBookmarkRecord>(record => record.InstanceId == instanceId)), null, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> DueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        if (limit <= 0)
        {
            return ValueTask.FromResult(Result.Success<IReadOnlyList<WorkflowBookmark>>([]));
        }

        // 到期时间升序，同刻按写入序号升序。不写 DueAt != null：SQL 里 NULL <= 值 本来就不成立，而 SqlSugar 会把该比较翻译成 `<> @参数(NULL)`，永远不匹配
        var query = EntityQuery.All<WorkflowBookmarkRecord>()
            .Where(new Specification<WorkflowBookmarkRecord>(record => record.DueAt <= now))
            .OrderBy(static record => record.DueAt)
            .OrderBy(static record => record.Sequence)
            .OrderBy(static record => record.Id);
        return ListAsync(query, limit, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> ByKindAndKeyAsync(string kind, string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(key);
        return ListAsync(ByCreation(new Specification<WorkflowBookmarkRecord>(record => record.Kind == kind && record.Key == key)), null, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> BySignalAsync(string signalName, string? correlationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(signalName);
        var kind = BookmarkKinds.Signal;
        // 给了关联 ID 时取关联为空或相等的；没给时取该信号的全部
        var filter = correlationId is null
            ? new Specification<WorkflowBookmarkRecord>(record => record.Kind == kind && record.Key == signalName)
            : new Specification<WorkflowBookmarkRecord>(record => record.Kind == kind && record.Key == signalName && (record.CorrelationId == null || record.CorrelationId == correlationId));
        return ListAsync(ByCreation(filter), null, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<Result> SaveAsync(IReadOnlyList<WorkflowBookmark> bookmarks, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bookmarks);
        var rows = bookmarks.Select(ToRecord).ToList();
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(
            ctx => RecordQueries.UpsertAsync(StoreInvocation.Repository<WorkflowBookmarkRecord>(ctx), rows, Copy, ctx.CancellationToken),
            cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result> DeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(async ctx =>
        {
            var repository = StoreInvocation.Repository<WorkflowBookmarkRecord>(ctx);
            var rows = await RecordQueries.FindManyAsync(repository, ids, ctx.CancellationToken).ConfigureAwait(false);
            return await RecordQueries.RemoveManyAsync(repository, rows, ctx.CancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<Result> DeleteByInstanceAsync(IReadOnlyList<string> instanceIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instanceIds);
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(async ctx =>
        {
            var repository = StoreInvocation.Repository<WorkflowBookmarkRecord>(ctx);
            foreach (var batch in RecordQueries.Batches(instanceIds))
            {
                var query = EntityQuery.All<WorkflowBookmarkRecord>()
                    .Where(new Specification<WorkflowBookmarkRecord>(record => batch.Contains(record.InstanceId)))
                    .OrderBy(static record => record.Id);
                var owned = await RecordQueries.ListAllAsync(repository, query, null, ctx.CancellationToken).ConfigureAwait(false);
                var removed = await RecordQueries.RemoveManyAsync(repository, owned, ctx.CancellationToken).ConfigureAwait(false);
                if (removed.IsFailure)
                {
                    return removed;
                }
            }

            return Result.Success();
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// 创建时间升序、同刻按写入序号升序、再按 ID 补成全序的查询。
    /// </summary>
    private static EntityQuery<WorkflowBookmarkRecord> ByCreation(Specification<WorkflowBookmarkRecord> filter)
        => EntityQuery.All<WorkflowBookmarkRecord>().Where(filter).OrderBy(static record => record.CreatedAt).OrderBy(static record => record.Sequence).OrderBy(static record => record.Id);

    /// <summary>
    /// 按查询读完（或读到上限）并反序列化。
    /// </summary>
    private async ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> ListAsync(EntityQuery<WorkflowBookmarkRecord> query, int? limit, CancellationToken cancellationToken)
    {
        return StoreInvocation.OrUnavailable(await _invocation.RunAsync(async ctx =>
        {
            var records = await RecordQueries.ListAllAsync(StoreInvocation.Repository<WorkflowBookmarkRecord>(ctx), query, limit, ctx.CancellationToken).ConfigureAwait(false);
            return ToBookmarks(records);
        }, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// 多行 → 书签。
    /// </summary>
    private static Result<IReadOnlyList<WorkflowBookmark>> ToBookmarks(List<WorkflowBookmarkRecord> records)
        => Result.Success<IReadOnlyList<WorkflowBookmark>>(records.Select(static record => WorkflowJson.DeserializeBookmark(record.Document)).ToList());

    /// <summary>
    /// 书签 → 行。
    /// </summary>
    /// <remarks>
    /// 序号只对新增的行生效，替换已有行时 <see cref="Copy"/> 不复制它。
    /// </remarks>
    private WorkflowBookmarkRecord ToRecord(WorkflowBookmark bookmark)
    {
        ArgumentNullException.ThrowIfNull(bookmark);
        return new WorkflowBookmarkRecord
        {
            Id = bookmark.Id,
            Sequence = _sequence.Next(),
            InstanceId = bookmark.InstanceId,
            NodeInstanceId = bookmark.NodeInstanceId,
            Kind = bookmark.Kind,
            Key = bookmark.Key,
            CorrelationId = bookmark.CorrelationId,
            DueAt = bookmark.DueAt,
            CreatedAt = bookmark.CreatedAt,
            TenantId = bookmark.TenantId,
            Document = WorkflowJson.Serialize(bookmark),
        };
    }

    /// <summary>
    /// 把新行的内容复制到已有行上。
    /// </summary>
    private static void Copy(WorkflowBookmarkRecord source, WorkflowBookmarkRecord target)
    {
        target.InstanceId = source.InstanceId;
        target.NodeInstanceId = source.NodeInstanceId;
        target.Kind = source.Kind;
        target.Key = source.Key;
        target.CorrelationId = source.CorrelationId;
        target.DueAt = source.DueAt;
        target.CreatedAt = source.CreatedAt;
        target.TenantId = source.TenantId;
        target.Document = source.Document;
    }
}
