// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Serialization;
using XiHan.Framework.Workflow.Database.Entities;

namespace XiHan.Framework.Workflow.Database.Storage;

/// <summary>
/// 一个分区（平台或租户）的书签表的读写。
/// </summary>
/// <remarks>
/// 租户分区的表由持久化按工作单元的租户过滤，查询里不写租户条件。到期查询按到期时间升序，其余查询按创建时间升序，同刻按写入序号。
/// </remarks>
/// <typeparam name="TRow">行类型。</typeparam>
internal sealed class BookmarkRows<TRow>(IRepository<TRow, string> repository, WriteSequence sequence)
    where TRow : WorkflowBookmarkRow, new()
{
    /// <summary>
    /// 按 ID 找。
    /// </summary>
    public async ValueTask<Result<Maybe<WorkflowBookmark>>> FindAsync(string id, CancellationToken cancellationToken)
    {
        var found = await repository.FindAsync(id, cancellationToken).ConfigureAwait(false);
        return Result.Success(found.TryGetValue(out var record) ? Maybe.Some(WorkflowJson.DeserializeBookmark(record.Document)) : Maybe.None<WorkflowBookmark>());
    }

    /// <summary>
    /// 按 ID 批量找。
    /// </summary>
    public async ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> FindManyAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
        => ToBookmarks(await RecordQueries.FindManyAsync(repository, ids, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 实例的全部书签。
    /// </summary>
    public ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> ByInstanceAsync(string instanceId, CancellationToken cancellationToken)
        => ListAsync(ByCreation(new Specification<TRow>(record => record.InstanceId == instanceId)), null, cancellationToken);

    /// <summary>
    /// 到期的书签。
    /// </summary>
    public ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> DueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        if (limit <= 0)
        {
            return ValueTask.FromResult(Result.Success<IReadOnlyList<WorkflowBookmark>>([]));
        }

        // 到期时间升序，同刻按写入序号升序。不写 DueAt != null：SQL 里 NULL <= 值 本来就不成立，而 SqlSugar 会把该比较翻译成 `<> @参数(NULL)`，永远不匹配
        var query = EntityQuery.All<TRow>()
            .Where(new Specification<TRow>(record => record.DueAt <= now))
            .OrderBy(static record => record.DueAt)
            .OrderBy(static record => record.Sequence)
            .OrderBy(static record => record.Id);
        return ListAsync(query, limit, cancellationToken);
    }

    /// <summary>
    /// 按种类与键找。
    /// </summary>
    public ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> ByKindAndKeyAsync(string kind, string key, CancellationToken cancellationToken)
        => ListAsync(ByCreation(new Specification<TRow>(record => record.Kind == kind && record.Key == key)), null, cancellationToken);

    /// <summary>
    /// 此刻可以领取的书签。
    /// </summary>
    public ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> AvailableAsync(string kind, string key, DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        if (limit <= 0)
        {
            return ValueTask.FromResult(Result.Success<IReadOnlyList<WorkflowBookmark>>([]));
        }

        // 到期条件先写比较再写判空：SqlSugar 把可空 DateTimeOffset 的 `== null || <= 参数` 这一写法翻译失败（KERNEL_UNEXPECTED_001），反过来写正常
        var filter = new Specification<TRow>(record => record.Kind == kind && record.Key == key && (record.DueAt <= now || record.DueAt == null));
        return ListAsync(ByCreation(filter), limit, cancellationToken);
    }

    /// <summary>
    /// 信号书签。
    /// </summary>
    public ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> BySignalAsync(string signalName, string? correlationId, CancellationToken cancellationToken)
    {
        var kind = BookmarkKinds.Signal;
        // 给了关联 ID 时取关联为空或相等的；没给时取该信号的全部
        var filter = correlationId is null
            ? new Specification<TRow>(record => record.Kind == kind && record.Key == signalName)
            : new Specification<TRow>(record => record.Kind == kind && record.Key == signalName && (record.CorrelationId == null || record.CorrelationId == correlationId));
        return ListAsync(ByCreation(filter), null, cancellationToken);
    }

    /// <summary>
    /// 新增或整体替换。
    /// </summary>
    public ValueTask<Result> SaveAsync(IReadOnlyList<WorkflowBookmark> bookmarks, CancellationToken cancellationToken)
        => RecordQueries.UpsertAsync(repository, bookmarks.Select(ToRecord).ToList(), Copy, cancellationToken);

    /// <summary>
    /// 按 ID 删除。
    /// </summary>
    public async ValueTask<Result> DeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var rows = await RecordQueries.FindManyAsync(repository, ids, cancellationToken).ConfigureAwait(false);
        return await RecordQueries.RemoveManyAsync(repository, rows, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 删除这些实例的全部书签。
    /// </summary>
    public async ValueTask<Result> DeleteByInstanceAsync(IReadOnlyList<string> instanceIds, CancellationToken cancellationToken)
    {
        foreach (var batch in RecordQueries.Batches(instanceIds))
        {
            var query = EntityQuery.All<TRow>()
                .Where(new Specification<TRow>(record => batch.Contains(record.InstanceId)))
                .OrderBy(static record => record.Id);
            var owned = await RecordQueries.ListAllAsync(repository, query, null, cancellationToken).ConfigureAwait(false);
            var removed = await RecordQueries.RemoveManyAsync(repository, owned, cancellationToken).ConfigureAwait(false);
            if (removed.IsFailure)
            {
                return removed;
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// 创建时间升序、同刻按写入序号升序、再按 ID 补成全序的查询。
    /// </summary>
    private static EntityQuery<TRow> ByCreation(Specification<TRow> filter)
        => EntityQuery.All<TRow>().Where(filter).OrderBy(static record => record.CreatedAt).OrderBy(static record => record.Sequence).OrderBy(static record => record.Id);

    /// <summary>
    /// 按查询读完（或读到上限）并反序列化。
    /// </summary>
    private async ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> ListAsync(EntityQuery<TRow> query, int? limit, CancellationToken cancellationToken)
        => ToBookmarks(await RecordQueries.ListAllAsync(repository, query, limit, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 多行 → 书签。
    /// </summary>
    private static Result<IReadOnlyList<WorkflowBookmark>> ToBookmarks(List<TRow> records)
        => Result.Success<IReadOnlyList<WorkflowBookmark>>(records.Select(static record => WorkflowJson.DeserializeBookmark(record.Document)).ToList());

    /// <summary>
    /// 书签 → 行。
    /// </summary>
    /// <remarks>
    /// 序号只对新增的行生效，替换已有行时 <see cref="Copy"/> 不复制它；租户列由持久化在新增时写入。
    /// </remarks>
    private TRow ToRecord(WorkflowBookmark bookmark)
    {
        ArgumentNullException.ThrowIfNull(bookmark);
        return new TRow
        {
            Id = bookmark.Id,
            Sequence = sequence.Next(),
            InstanceId = bookmark.InstanceId,
            NodeInstanceId = bookmark.NodeInstanceId,
            Kind = bookmark.Kind,
            Key = bookmark.Key,
            CorrelationId = bookmark.CorrelationId,
            DueAt = bookmark.DueAt,
            CreatedAt = bookmark.CreatedAt,
            Document = WorkflowJson.Serialize(bookmark),
        };
    }

    /// <summary>
    /// 把新行的内容复制到已有行上。
    /// </summary>
    private static void Copy(TRow source, TRow target)
    {
        target.InstanceId = source.InstanceId;
        target.NodeInstanceId = source.NodeInstanceId;
        target.Kind = source.Kind;
        target.Key = source.Key;
        target.CorrelationId = source.CorrelationId;
        target.DueAt = source.DueAt;
        target.CreatedAt = source.CreatedAt;
        target.Document = source.Document;
    }
}
