// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Serialization;
using XiHan.Framework.Workflow.Abstractions.Tasks;
using XiHan.Framework.Workflow.Database.Entities;

namespace XiHan.Framework.Workflow.Database.Storage;

/// <summary>
/// 一个分区（平台或租户）的书签表的读写。
/// </summary>
/// <remarks>
/// 租户分区的表由持久化按工作单元的租户过滤，查询里不写租户条件。到期查询按到期时间升序，其余查询按创建时间升序，同刻按写入序号。待办查询下推到 SQL：书签行带所属实例的定义名与状态，写书签时从同一分区的实例行取，实例的状态变了由 <see cref="SyncAsync"/> 同步。
/// </remarks>
/// <typeparam name="TRow">行类型。</typeparam>
/// <typeparam name="TInstance">同一分区的实例行类型。</typeparam>
internal sealed class BookmarkRows<TRow, TInstance>(IRepository<TRow, string> repository, IRepository<TInstance, string> instances, WriteSequence sequence)
    where TRow : WorkflowBookmarkRow, new()
    where TInstance : WorkflowInstanceRow
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
    /// <remarks>
    /// 先按 ID 批量取回所属实例的行，把定义名与状态写进书签行：引擎的写回先写实例再写书签，同一事务里读得到刚写的实例。
    /// </remarks>
    public async ValueTask<Result> SaveAsync(IReadOnlyList<WorkflowBookmark> bookmarks, CancellationToken cancellationToken)
    {
        if (bookmarks.Count == 0)
        {
            return Result.Success();
        }

        var owners = (await RecordQueries.FindManyAsync(instances, bookmarks.Select(static bookmark => bookmark.InstanceId).Distinct(StringComparer.Ordinal), cancellationToken).ConfigureAwait(false))
            .ToDictionary(static owner => owner.Id, StringComparer.Ordinal);
        return await RecordQueries.UpsertAsync(repository, bookmarks.Select(bookmark => ToRecord(bookmark, owners.GetValueOrDefault(bookmark.InstanceId))).ToList(), Copy, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 把实例的定义名与状态同步到它们的书签行。
    /// </summary>
    /// <remarks>
    /// 实例存储在新增实例、实例的状态或定义名变化之后调用；只更新与实例不一致的行。
    /// </remarks>
    public async ValueTask<Result> SyncAsync(IReadOnlyList<TInstance> owners, CancellationToken cancellationToken)
    {
        var byId = new Dictionary<string, TInstance>(StringComparer.Ordinal);
        foreach (var owner in owners)
        {
            byId[owner.Id] = owner;
        }

        foreach (var batch in RecordQueries.Batches(byId.Keys))
        {
            var query = EntityQuery.All<TRow>()
                .Where(new Specification<TRow>(record => batch.Contains(record.InstanceId)))
                .OrderBy(static record => record.Id);
            var rows = await RecordQueries.ListAllAsync(repository, query, null, cancellationToken).ConfigureAwait(false);
            var stale = new List<TRow>();
            foreach (var row in rows)
            {
                var owner = byId[row.InstanceId];
                if (!string.Equals(row.InstanceStatus, owner.Status, StringComparison.Ordinal) || !string.Equals(row.DefinitionName, owner.DefinitionName, StringComparison.Ordinal))
                {
                    row.InstanceStatus = owner.Status;
                    row.DefinitionName = owner.DefinitionName;
                    stale.Add(row);
                }
            }

            var updated = await RecordQueries.UpdateManyAsync(repository, stale, cancellationToken).ConfigureAwait(false);
            if (updated.IsFailure)
            {
                return updated;
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// 待办查询的一页：所属实例运行中的用户任务书签。
    /// </summary>
    /// <remarks>
    /// 按创建时间排序，同刻按写入序号，再按 ID 补成全序。
    /// </remarks>
    public async ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> ListUserTasksAsync(UserTaskQuery query, CancellationToken cancellationToken)
    {
        var descending = query.Descending;
        var paged = UserTasks(query)
            .OrderBy(static record => record.CreatedAt, descending)
            .OrderBy(static record => record.Sequence, descending)
            .OrderBy(static record => record.Id, descending)
            .Page(query.Skip, query.Limit);
        var records = await repository.ListAsync(paged, cancellationToken).ConfigureAwait(false);
        return ToBookmarks([.. records]);
    }

    /// <summary>
    /// 待办查询的总数。
    /// </summary>
    public async ValueTask<Result<long>> CountUserTasksAsync(UserTaskQuery query, CancellationToken cancellationToken)
        => await repository.CountAsync(UserTasks(query), cancellationToken).ConfigureAwait(false);

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
    /// <summary>
    /// 待办查询的过滤条件。
    /// </summary>
    /// <remarks>
    /// 每个给出的条件一个规约，由查询取交集；没给的条件不写进 SQL（不写 <c>参数 == null ||</c> 这类条件，SqlSugar 对它的翻译不可靠）。
    /// </remarks>
    private static EntityQuery<TRow> UserTasks(UserTaskQuery query)
    {
        var kind = BookmarkKinds.UserTask;
        var running = nameof(WorkflowInstanceStatus.Running);
        var filtered = EntityQuery.All<TRow>().Where(new Specification<TRow>(record => record.Kind == kind && record.InstanceStatus == running));
        if (query.AssigneeId is { } assigneeId)
        {
            filtered = filtered.Where(new Specification<TRow>(record => record.Key == assigneeId));
        }

        if (query.InstanceId is { } instanceId)
        {
            filtered = filtered.Where(new Specification<TRow>(record => record.InstanceId == instanceId));
        }

        if (query.NodeId is { } nodeId)
        {
            filtered = filtered.Where(new Specification<TRow>(record => record.NodeId == nodeId));
        }

        if (query.DefinitionName is { } name)
        {
            var definitionName = name.Value;
            filtered = filtered.Where(new Specification<TRow>(record => record.DefinitionName == definitionName));
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
    /// 序号只对新增的行生效，替换已有行时 <see cref="Copy"/> 不复制它；租户列由持久化在新增时写入。所属实例的行不存在（定义级定时书签）时定义名与状态为空。
    /// </remarks>
    private TRow ToRecord(WorkflowBookmark bookmark, TInstance? owner)
    {
        ArgumentNullException.ThrowIfNull(bookmark);
        return new TRow
        {
            Id = bookmark.Id,
            Sequence = sequence.Next(),
            InstanceId = bookmark.InstanceId,
            NodeInstanceId = bookmark.NodeInstanceId,
            NodeId = bookmark.NodeId,
            DefinitionName = owner?.DefinitionName,
            InstanceStatus = owner?.Status,
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
        target.NodeId = source.NodeId;
        target.DefinitionName = source.DefinitionName;
        target.InstanceStatus = source.InstanceStatus;
        target.Kind = source.Kind;
        target.Key = source.Key;
        target.CorrelationId = source.CorrelationId;
        target.DueAt = source.DueAt;
        target.CreatedAt = source.CreatedAt;
        target.Document = source.Document;
    }
}
