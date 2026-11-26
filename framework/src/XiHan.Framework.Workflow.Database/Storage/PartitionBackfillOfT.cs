// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Serialization;
using XiHan.Framework.Workflow.Database.Entities;
using XiHan.Framework.Workflow.Database.Locking;

namespace XiHan.Framework.Workflow.Database.Storage;

/// <summary>
/// 一个分区（平台或租户）的查询列回填
/// </summary>
/// <remarks>
/// <see cref="DatabaseWorkflowBackfill"/> 的一次分区回填（ADR-0304、ADR-0326）：先定义、后实例，各按创建时间升序、同刻按 ID 每批读至多 <c>batchSize</c> 行，再按页读这些行的书签（书签以所属实例的 ID 或定义级定时书签的定义 ID 归属，按它找有索引）。游标只用创建时间的 <c>&gt;=</c> 与同刻行的偏移：持久化的规约不保证字符串的大小比较能翻译，创建时间写入后不变，新写入的行由存储按新规则算好，扫到也只是不必改写
/// </remarks>
/// <typeparam name="TDefinition">定义行类型</typeparam>
/// <typeparam name="TInstance">实例行类型</typeparam>
/// <typeparam name="TNode">节点实例行类型</typeparam>
/// <typeparam name="TBookmark">书签行类型</typeparam>
internal sealed class PartitionBackfill<TDefinition, TInstance, TNode, TBookmark>
    where TDefinition : WorkflowDefinitionRow, new()
    where TInstance : WorkflowInstanceRow, new()
    where TNode : WorkflowNodeInstanceRow, new()
    where TBookmark : WorkflowBookmarkRow, new()
{
    /// <summary>
    /// 根服务提供者
    /// </summary>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 实例锁
    /// </summary>
    private readonly DatabaseWorkflowInstanceLock _locks;

    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 分区，空为平台
    /// </summary>
    private readonly string? _tenantId;

    /// <summary>
    /// 每批读的行数
    /// </summary>
    private readonly int _batchSize;

    /// <summary>
    /// 读过的行数
    /// </summary>
    private long _scanned;

    /// <summary>
    /// 改写的行数
    /// </summary>
    private long _rewritten;

    /// <summary>
    /// 锁被占用而跳过的实例数
    /// </summary>
    private long _busy;

    /// <summary>
    /// 读到了行的批数
    /// </summary>
    private int _batches;

    /// <summary>
    /// 创建
    /// </summary>
    public PartitionBackfill(IServiceProvider services, DatabaseWorkflowInstanceLock locks, ILogger logger, string? tenantId, int batchSize)
    {
        _services = services;
        _locks = locks;
        _logger = logger;
        _tenantId = tenantId;
        _batchSize = batchSize;
    }

    /// <summary>
    /// 回填这个分区
    /// </summary>
    public async ValueTask<Result<DatabaseWorkflowBackfillReport>> RunAsync(CancellationToken cancellationToken)
    {
        var definitions = await RunDefinitionsAsync(cancellationToken).ConfigureAwait(false);
        if (definitions.IsFailure)
        {
            return definitions.Error!;
        }

        var instances = await RunInstancesAsync(cancellationToken).ConfigureAwait(false);
        if (instances.IsFailure)
        {
            return instances.Error!;
        }

        return new DatabaseWorkflowBackfillReport { Scanned = _scanned, Rewritten = _rewritten, Busy = _busy, Batches = _batches };
    }

    /// <summary>
    /// 下一个游标
    /// </summary>
    /// <remarks>
    /// 下一批从这一批最后的创建时间读起（含），跳过同刻已经读过的行：这一批末尾与最后一行同刻的行在下一次查询里排在最前（同刻按 ID），整批都与上一个游标同刻时累加
    /// </remarks>
    private static (DateTimeOffset From, int Skip) Advance(DateTimeOffset? from, int skip, IReadOnlyList<DateTimeOffset> createdAt)
    {
        var last = createdAt[^1];
        var tail = 0;
        for (var index = createdAt.Count - 1; index >= 0 && createdAt[index] == last; index--)
        {
            tail++;
        }

        return (last, from == last ? skip + tail : tail);
    }

    /// <summary>
    /// 回填定义行与定义级定时书签的行
    /// </summary>
    private async ValueTask<Result> RunDefinitionsAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset? from = null;
        var skip = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scan = await InUnitOfWorkAsync<Scan>(readOnly: true, async (scope, token) =>
            {
                var query = EntityQuery.All<TDefinition>();
                if (from is { } since)
                {
                    query = query.Where(new Specification<TDefinition>(record => record.CreatedAt >= since));
                }

                var rows = await scope.GetRequiredService<IRepository<TDefinition, string>>().ListAsync(query.OrderBy(static record => record.CreatedAt).OrderBy(static record => record.Id).Page(skip, _batchSize), token).ConfigureAwait(false);
                var stale = new List<string>();
                foreach (var row in rows)
                {
                    var definition = WorkflowJson.DeserializeDefinition(row.Document);
                    if (!definition.TryGetValue(out var value))
                    {
                        return definition.Error!;
                    }

                    if (DefinitionRows<TDefinition>.RefreshQueryColumns(row, value, apply: false))
                    {
                        stale.Add(row.Id);
                    }
                }

                // 定义级定时书签以定义 ID 归属，没有所属实例的行；自己要改写的定义连同书签一起在锁内处理，扫描时不必再看它们的书签
                var ids = rows.Select(static row => row.Id).ToList();
                var owners = await RecordQueries.FindManyAsync(scope.GetRequiredService<IRepository<TInstance, string>>(), ids, token).ConfigureAwait(false);
                var bookmarks = await ScanBookmarksAsync(scope, ids, owners, [.. stale], token).ConfigureAwait(false);
                return new Scan(rows.Count + bookmarks.Scanned, rows.Select(static row => row.CreatedAt).ToList(), [.. stale.Union(bookmarks.StaleOwners, StringComparer.Ordinal)]);
            }, cancellationToken).ConfigureAwait(false);
            if (!scan.TryGetValue(out var batch))
            {
                return scan.Error!;
            }

            if (batch.CreatedAt.Count == 0)
            {
                return Result.Success();
            }

            _batches++;
            _scanned += batch.Scanned;
            var rewritten = await RewriteOwnersAsync(batch.StaleOwners, OwnerKind.Definition, cancellationToken).ConfigureAwait(false);
            if (rewritten.IsFailure || batch.CreatedAt.Count < _batchSize)
            {
                return rewritten;
            }

            (from, skip) = Advance(from, skip, batch.CreatedAt);
        }
    }

    /// <summary>
    /// 回填实例行与它们的书签行
    /// </summary>
    private async ValueTask<Result> RunInstancesAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset? from = null;
        var skip = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scan = await InUnitOfWorkAsync<Scan>(readOnly: true, async (scope, token) =>
            {
                var query = EntityQuery.All<TInstance>();
                if (from is { } since)
                {
                    query = query.Where(new Specification<TInstance>(record => record.CreatedAt >= since));
                }

                var rows = await scope.GetRequiredService<IRepository<TInstance, string>>().ListAsync(query.OrderBy(static record => record.CreatedAt).OrderBy(static record => record.Id).Page(skip, _batchSize), token).ConfigureAwait(false);
                var stale = new List<string>();
                foreach (var row in rows)
                {
                    if (InstanceRows<TInstance, TNode>.RefreshQueryColumns(row, WorkflowJson.DeserializeInstance(row.Document), apply: false))
                    {
                        stale.Add(row.Id);
                    }
                }

                // 自己要改写的实例连同书签一起在锁内处理，扫描时不必再看它们的书签
                var bookmarks = await ScanBookmarksAsync(scope, [.. rows.Select(static row => row.Id)], [.. rows], [.. stale], token).ConfigureAwait(false);
                return new Scan(rows.Count + bookmarks.Scanned, rows.Select(static row => row.CreatedAt).ToList(), [.. stale.Union(bookmarks.StaleOwners, StringComparer.Ordinal)]);
            }, cancellationToken).ConfigureAwait(false);
            if (!scan.TryGetValue(out var batch))
            {
                return scan.Error!;
            }

            if (batch.CreatedAt.Count == 0)
            {
                return Result.Success();
            }

            _batches++;
            _scanned += batch.Scanned;
            var rewritten = await RewriteOwnersAsync(batch.StaleOwners, OwnerKind.Instance, cancellationToken).ConfigureAwait(false);
            if (rewritten.IsFailure || batch.CreatedAt.Count < _batchSize)
            {
                return rewritten;
            }

            (from, skip) = Advance(from, skip, batch.CreatedAt);
        }
    }

    /// <summary>
    /// 扫描这些归属的书签，找出查询列不是新值的书签所归属的键
    /// </summary>
    /// <remarks>
    /// 按页读（每页至多 <c>batchSize</c> 行），一个实例的书签再多内存也有界；<paramref name="skipped"/> 里的键整个要改写，不再看它们的书签
    /// </remarks>
    private async ValueTask<(int Scanned, IReadOnlyList<string> StaleOwners)> ScanBookmarksAsync(IServiceProvider scope, List<string> ownerIds, IReadOnlyList<TInstance> owners, IReadOnlyCollection<string> skipped, CancellationToken cancellationToken)
    {
        if (ownerIds.Count == 0)
        {
            return (0, []);
        }

        var repository = scope.GetRequiredService<IRepository<TBookmark, string>>();
        var byId = owners.ToDictionary(static owner => owner.Id, StringComparer.Ordinal);
        var stale = new HashSet<string>(StringComparer.Ordinal);
        var scanned = 0;
        var skip = 0;
        while (true)
        {
            var query = EntityQuery.All<TBookmark>()
                .Where(new Specification<TBookmark>(record => ownerIds.Contains(record.InstanceId)))
                .OrderBy(static record => record.Id)
                .Page(skip, _batchSize);
            var page = await repository.ListAsync(query, cancellationToken).ConfigureAwait(false);
            foreach (var row in page)
            {
                if (skipped.Contains(row.InstanceId) || stale.Contains(row.InstanceId))
                {
                    continue;
                }

                if (BookmarkRows<TBookmark, TInstance>.RefreshQueryColumns(row, WorkflowJson.DeserializeBookmark(row.Document), byId.GetValueOrDefault(row.InstanceId), apply: false))
                {
                    stale.Add(row.InstanceId);
                }
            }

            scanned += page.Count;
            if (page.Count < _batchSize)
            {
                return (scanned, [.. stale]);
            }

            skip += page.Count;
        }
    }

    /// <summary>
    /// 逐个键在锁内重读并改写它的定义行或实例行与归属它的书签行
    /// </summary>
    /// <remarks>
    /// 键是实例 ID，或定义的 ID（定义行与定义级定时书签）——与引擎、目录写这些行时取的锁相同（实例锁串行化写入，RFC-0021「存储契约」；目录改写已有的定义时取定义 ID 的锁、持有到调用结束，ADR-0326）。一次只持有一个锁：引擎的一次调用可能先后持有几个实例的锁，回填同时持有多个就可能与它互相等到超时。锁在等待上限内拿不到的跳过、计入 <see cref="DatabaseWorkflowBackfillReport.Busy"/>，别的失败让这个分区失败
    /// </remarks>
    private async ValueTask<Result> RewriteOwnersAsync(IReadOnlyList<string> keys, OwnerKind kind, CancellationToken cancellationToken)
    {
        foreach (var key in keys.Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var acquired = await _locks.AcquireAsync(key, cancellationToken).ConfigureAwait(false);
            if (!acquired.TryGetValue(out var handle))
            {
                if (string.Equals(acquired.Error!.Code, WorkflowErrors.LockTimeout, StringComparison.Ordinal))
                {
                    _busy++;
                    continue;
                }

                return acquired.Error!;
            }

            Result<int> rewritten;
            await using (handle.ConfigureAwait(false))
            {
                rewritten = await InUnitOfWorkAsync(readOnly: false, (scope, token) => RewriteOwnerAsync(scope, key, kind, token), cancellationToken).ConfigureAwait(false);
            }

            if (!rewritten.TryGetValue(out var count))
            {
                return rewritten.Error!;
            }

            _rewritten += count;
        }

        return Result.Success();
    }

    /// <summary>
    /// 在持有锁的可写事务里改写一个键的行
    /// </summary>
    /// <remarks>
    /// 锁内重读：扫描之后引擎或目录可能已经写过这些行，按重读的行与文档算，已是新值的不改写。书签的实例一侧的列取自改写之后的实例行；定义级定时书签没有所属实例，这一侧的列为空
    /// </remarks>
    private static async ValueTask<Result<int>> RewriteOwnerAsync(IServiceProvider scope, string key, OwnerKind kind, CancellationToken cancellationToken)
    {
        var bookmarks = scope.GetRequiredService<IRepository<TBookmark, string>>();
        TInstance? owner = null;
        var rewritten = 0;
        if (kind == OwnerKind.Definition)
        {
            var definitions = scope.GetRequiredService<IRepository<TDefinition, string>>();
            if ((await definitions.FindAsync(key, cancellationToken).ConfigureAwait(false)).TryGetValue(out var row))
            {
                var definition = WorkflowJson.DeserializeDefinition(row.Document);
                if (!definition.TryGetValue(out var value))
                {
                    return definition.Error!;
                }

                if (DefinitionRows<TDefinition>.RefreshQueryColumns(row, value, apply: true))
                {
                    var updated = await RecordQueries.UpdateManyAsync(definitions, [row], cancellationToken).ConfigureAwait(false);
                    if (updated.IsFailure)
                    {
                        return StorePartition.Unavailable(updated.Error!);
                    }

                    rewritten++;
                }
            }
        }
        else
        {
            var instances = scope.GetRequiredService<IRepository<TInstance, string>>();
            owner = (await instances.FindAsync(key, cancellationToken).ConfigureAwait(false)).TryGetValue(out var found) ? found : null;
            if (owner is not null && InstanceRows<TInstance, TNode>.RefreshQueryColumns(owner, WorkflowJson.DeserializeInstance(owner.Document), apply: true))
            {
                var updated = await RecordQueries.UpdateManyAsync(instances, [owner], cancellationToken).ConfigureAwait(false);
                if (updated.IsFailure)
                {
                    return StorePartition.Unavailable(updated.Error!);
                }

                rewritten++;
            }
        }

        var query = EntityQuery.All<TBookmark>()
            .Where(new Specification<TBookmark>(record => record.InstanceId == key))
            .OrderBy(static record => record.Id);
        var changed = new List<TBookmark>();
        foreach (var row in await RecordQueries.ListAllAsync(bookmarks, query, null, cancellationToken).ConfigureAwait(false))
        {
            if (BookmarkRows<TBookmark, TInstance>.RefreshQueryColumns(row, WorkflowJson.DeserializeBookmark(row.Document), owner, apply: true))
            {
                changed.Add(row);
            }
        }

        var saved = await RecordQueries.UpdateManyAsync(bookmarks, changed, cancellationToken).ConfigureAwait(false);
        return saved.IsSuccess ? rewritten + changed.Count : StorePartition.Unavailable(saved.Error!);
    }

    /// <summary>
    /// 在新作用域的工作单元里执行
    /// </summary>
    /// <remarks>
    /// 租户分区先在新作用域里绑定租户的分片再开工作单元（持久化客户端建出之后不能再绑）；没有装配分片的只用工作单元的租户过滤。返回的失败原样交出（仓储写入的失败在动作里已归为存储不可达）；抛出的异常回滚后记日志、归为存储不可达，调用方的取消原样抛出
    /// </remarks>
    private async ValueTask<Result<T>> InUnitOfWorkAsync<T>(bool readOnly, Func<IServiceProvider, CancellationToken, ValueTask<Result<T>>> body, CancellationToken cancellationToken)
    {
        try
        {
            return await UnitOfWorkScope.RunAsync(_services, BindShardAsync, new UnitOfWorkOptions { TenantId = _tenantId, IsReadOnly = readOnly }, body, _logger, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // 驱动的消息可能带连接串与主机名：错误里只写类型，完整异常记进日志（ADR-0259）
            DatabaseWorkflowBackfill.LogBackfillThrew(_logger, _tenantId ?? string.Empty, exception);
            return StorePartition.Unavailable(exception);
        }
    }

    /// <summary>
    /// 租户分区绑定租户的分片
    /// </summary>
    private async ValueTask<Result> BindShardAsync(IServiceProvider scope, CancellationToken cancellationToken)
    {
        if (_tenantId is null || scope.GetService<TenantShardScope>() is not { } shards)
        {
            return Result.Success();
        }

        var bound = await shards.BindTenantAsync(_tenantId, cancellationToken).ConfigureAwait(false);
        return bound.IsSuccess ? Result.Success() : bound.Error!;
    }

    /// <summary>
    /// 一批扫描的结果
    /// </summary>
    /// <param name="Scanned">读过的行数</param>
    /// <param name="CreatedAt">这一批行的创建时间，按读出的次序</param>
    /// <param name="StaleOwners">要在锁内改写的键：定义或实例的 ID</param>
    private sealed record Scan(int Scanned, IReadOnlyList<DateTimeOffset> CreatedAt, IReadOnlyList<string> StaleOwners);

    /// <summary>
    /// 锁内改写的键归属的行
    /// </summary>
    private enum OwnerKind
    {
        /// <summary>
        /// 定义：定义行与定义级定时书签
        /// </summary>
        Definition = 1,

        /// <summary>
        /// 实例：实例行与它的书签
        /// </summary>
        Instance = 2,
    }
}
