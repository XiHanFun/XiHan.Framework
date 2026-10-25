// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Linq.Expressions;
using SqlSugar;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// SqlSugar 仓储：读在活动工作单元内进行，首次写入开启事务，每次写入立即执行，按协议维护审计字段、租户与软删除。
/// 批量方法用一次主键 <c>IN</c> 查询取回目标行、逐项校验后一次 <c>InsertableByObject(list)</c> / <c>UpdateableByObject(list)</c> / <c>DeleteableByObject(list)</c>。
/// SqlSugar 没有变更跟踪：更新与删除先在事务内加行锁取回当前行（<c>TranLock</c>，SQLite 由数据库自身的写锁保证），比对并发戳后把调用方的实例整行写入。
/// 实体类型没有 <c>new()</c> 约束，所以写入经 SqlSugar 的按对象入口而不是泛型构建器。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class SqlSugarRepository<TEntity, TKey>(SqlSugarUnitOfWork unitOfWork, TimeProvider timeProvider) : IRepository<TEntity, TKey>
    where TEntity : class, IEntity<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// 实体是否实现 <see cref="ISoftDeletable"/>；按闭合泛型只判断一次，查询据此决定是否追加删除标记条件。
    /// </summary>
    private static readonly bool _isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(typeof(TEntity));

    /// <summary>
    /// 实体是否实现 <see cref="IMultiTenant"/>；按闭合泛型只判断一次，查询据此决定是否追加租户条件。
    /// </summary>
    private static readonly bool _isMultiTenant = typeof(IMultiTenant).IsAssignableFrom(typeof(TEntity));

    /// <summary>
    /// 本作用域的工作单元；提供客户端、事务与当前租户。
    /// </summary>
    private readonly SqlSugarUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    /// <summary>
    /// 审计字段使用的时间源。
    /// </summary>
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc />
    public async ValueTask<Maybe<TEntity>> FindAsync(TKey id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        var entity = await Visible(includeDeleted: false).Where(EntityExpressions.IdEquals<TEntity, TKey>(id)).FirstAsync(cancellationToken).ConfigureAwait(false);
        return entity is null ? default : Maybe.Some(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TEntity>> FindManyAsync(IReadOnlyList<TKey> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        _ = Client;
        if (ids.Count == 0)
        {
            return [];
        }

        var distinct = Distinct(ids);
        var rows = await Visible(includeDeleted: false).In(distinct).ToListAsync(cancellationToken).ConfigureAwait(false);
        var byId = rows.ToDictionary(static e => e.Id);
        var found = new List<TEntity>(distinct.Count);
        foreach (var id in distinct)
        {
            if (byId.TryGetValue(id, out var entity))
            {
                found.Add(entity);
            }
        }

        return found;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TEntity>> ListAsync(EntityQuery<TEntity> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var source = Filtered(query);
        // SqlSugar 的 OrderBy 可以叠加，多级排序按声明顺序追加；排序键重新包成不带可空标注的委托类型，表达式体不变
        foreach (var ordering in query.Orderings)
        {
            var key = Expression.Lambda<Func<TEntity, object>>(ordering.KeySelector.Body, ordering.KeySelector.Parameters);
            source = source.OrderBy(key, ordering.Descending ? OrderByType.Desc : OrderByType.Asc);
        }

        return await source.Skip(query.Skip).Take(query.Take).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<long> CountAsync(EntityQuery<TEntity> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await Filtered(query).CountAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result> AddAsync(TEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (await PrepareWriteAsync(cancellationToken).ConfigureAwait(false) is { } rejected)
        {
            return rejected;
        }

        // 已存在的行（含已软删除、其他租户）都与新键冲突，所以不加可见性过滤
        if (await Client.Queryable<TEntity>().Where(EntityExpressions.IdEquals<TEntity, TKey>(entity.Id)).AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return Duplicate(entity.Id);
        }

        var applied = PersistenceConventions.ApplyOnAdd(entity, _unitOfWork.TenantId, _timeProvider.GetUtcNow());
        if (applied.IsFailure)
        {
            return applied;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await Client.InsertableByObject(entity).ExecuteCommandAsync().ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Result>> AddRangeAsync(IReadOnlyList<TEntity> entities, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entities);
        _ = Client;
        if (entities.Count == 0)
        {
            return [];
        }

        if (await PrepareWriteAsync(cancellationToken).ConfigureAwait(false) is { } rejected)
        {
            return Repeat(rejected, entities.Count);
        }

        // 一次主键 IN 查询拿到库里已存在的标识（含已软删除、其他租户）；之后每个通过校验的项把标识加进同一集合，批内重复就能被看见
        var taken = (await Client.Queryable<TEntity>().In(Distinct(entities.Select(static e => e.Id))).ToListAsync(EntityExpressions.Id<TEntity, TKey>()).ConfigureAwait(false)).ToHashSet();
        var now = _timeProvider.GetUtcNow();
        var results = new Result[entities.Count];
        var pending = new List<TEntity>(entities.Count);
        var pendingIndexes = new List<int>(entities.Count);
        for (var index = 0; index < entities.Count; index++)
        {
            var entity = entities[index];
            ArgumentNullException.ThrowIfNull(entity);
            if (taken.Contains(entity.Id))
            {
                results[index] = Duplicate(entity.Id);
                continue;
            }

            var applied = PersistenceConventions.ApplyOnAdd(entity, _unitOfWork.TenantId, now);
            if (applied.IsFailure)
            {
                results[index] = applied;
                continue;
            }

            taken.Add(entity.Id);
            pending.Add(entity);
            pendingIndexes.Add(index);
        }

        if (pending.Count == 0)
        {
            return results;
        }

        // 通过校验的项一次多行插入
        cancellationToken.ThrowIfCancellationRequested();
        await Client.InsertableByObject(pending).ExecuteCommandAsync().ConfigureAwait(false);
        foreach (var index in pendingIndexes)
        {
            results[index] = Result.Success();
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<Result> UpdateAsync(TEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (await PrepareWriteAsync(cancellationToken).ConfigureAwait(false) is { } rejected)
        {
            return rejected;
        }

        var existing = await Locked().Where(EntityExpressions.IdEquals<TEntity, TKey>(entity.Id)).FirstAsync(cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return NotFound(entity.Id);
        }

        if (Stale(existing, entity))
        {
            return StaleStamp(entity.Id);
        }

        PersistenceConventions.ApplyOnUpdate(entity, _timeProvider.GetUtcNow());
        cancellationToken.ThrowIfCancellationRequested();
        // 行已在本事务内加锁，按主键整行写入；影响 0 行只可能是行在锁定前已被删除，按冲突报告
        return await Client.UpdateableByObject(entity).ExecuteCommandAsync().ConfigureAwait(false) == 0
            ? Error.Conflict(PersistenceErrors.Concurrency, PersistenceMessages.ConcurrencyConflict, MessageArguments.Of(("entity", typeof(TEntity).Name), ("reason", "the row changed after it was read")))
            : Result.Success();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Result>> UpdateRangeAsync(IReadOnlyList<TEntity> entities, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entities);
        _ = Client;
        if (entities.Count == 0)
        {
            return [];
        }

        if (await PrepareWriteAsync(cancellationToken).ConfigureAwait(false) is { } rejected)
        {
            return Repeat(rejected, entities.Count);
        }

        var rows = await LoadLockedAsync(entities, cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        var results = new Result[entities.Count];
        var pending = new List<TEntity>(entities.Count);
        var pendingIndexes = new List<int>(entities.Count);
        for (var index = 0; index < entities.Count; index++)
        {
            var entity = entities[index];
            ArgumentNullException.ThrowIfNull(entity);
            if (!rows.TryGetValue(entity.Id, out var existing))
            {
                results[index] = NotFound(entity.Id);
                continue;
            }

            // 前一项已刷新同一标识的戳：同一标识的第二个游离实例在这里因戳过期而冲突
            if (Stale(existing, entity))
            {
                results[index] = StaleStamp(entity.Id);
                continue;
            }

            PersistenceConventions.ApplyOnUpdate(entity, now);
            rows[entity.Id] = entity;
            pending.Add(entity);
            pendingIndexes.Add(index);
        }

        if (pending.Count == 0)
        {
            return results;
        }

        // 通过校验的项一次按主键批量更新
        cancellationToken.ThrowIfCancellationRequested();
        await Client.UpdateableByObject(pending).ExecuteCommandAsync().ConfigureAwait(false);
        foreach (var index in pendingIndexes)
        {
            results[index] = Result.Success();
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<Result> RemoveAsync(TEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (await PrepareWriteAsync(cancellationToken).ConfigureAwait(false) is { } rejected)
        {
            return rejected;
        }

        var existing = await Locked().Where(EntityExpressions.IdEquals<TEntity, TKey>(entity.Id)).FirstAsync(cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return NotFound(entity.Id);
        }

        cancellationToken.ThrowIfCancellationRequested();
        // 软删除在取回的当前行上打标并整行写回，调用方的实例再同步审计字段；不支持软删除的实体按主键真正删除
        if (PersistenceConventions.TrySoftDelete(existing, _timeProvider.GetUtcNow()))
        {
            await Client.UpdateableByObject(existing).ExecuteCommandAsync().ConfigureAwait(false);
            CopyAuditBack(existing, entity);
        }
        else
        {
            await Client.DeleteableByObject(existing).ExecuteCommandAsync().ConfigureAwait(false);
        }

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Result>> RemoveRangeAsync(IReadOnlyList<TEntity> entities, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entities);
        _ = Client;
        if (entities.Count == 0)
        {
            return [];
        }

        if (await PrepareWriteAsync(cancellationToken).ConfigureAwait(false) is { } rejected)
        {
            return Repeat(rejected, entities.Count);
        }

        var rows = await LoadLockedAsync(entities, cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        var results = new Result[entities.Count];
        var tombstones = new List<(int Index, TEntity Existing)>(entities.Count);
        var deletions = new List<TEntity>(entities.Count);
        var deletionIndexes = new List<int>(entities.Count);
        // 同一实体第二次出现时已从行集合移除，自然成为 NotFound
        for (var index = 0; index < entities.Count; index++)
        {
            var entity = entities[index];
            ArgumentNullException.ThrowIfNull(entity);
            if (!rows.Remove(entity.Id, out var existing))
            {
                results[index] = NotFound(entity.Id);
                continue;
            }

            if (PersistenceConventions.TrySoftDelete(existing, now))
            {
                tombstones.Add((index, existing));
            }
            else
            {
                deletions.Add(existing);
                deletionIndexes.Add(index);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        // 软删除的行一次批量更新，物理删除的行一次按主键批量删除
        if (tombstones.Count > 0)
        {
            await Client.UpdateableByObject(tombstones.Select(static t => t.Existing).ToList()).ExecuteCommandAsync().ConfigureAwait(false);
            foreach (var (index, existing) in tombstones)
            {
                results[index] = Result.Success();
                CopyAuditBack(existing, entities[index]);
            }
        }

        if (deletions.Count > 0)
        {
            await Client.DeleteableByObject(deletions).ExecuteCommandAsync().ConfigureAwait(false);
            foreach (var index in deletionIndexes)
            {
                results[index] = Result.Success();
            }
        }

        return results;
    }

    /// <summary>
    /// 活动工作单元的客户端；工作单元不活动时抛出 <see cref="PersistenceErrors.NoActiveUnitOfWork"/>，每个操作都经此校验。
    /// </summary>
    private ISqlSugarClient Client => _unitOfWork.State == UnitOfWorkState.Active
        ? _unitOfWork.Client
        : throw new FrameworkException(PersistenceErrors.NoActiveUnitOfWork, $"The unit of work is in state '{_unitOfWork.State}'; repositories must be used within the Transaction stage.");

    /// <summary>
    /// 构造实体不存在的错误。
    /// </summary>
    private static Error NotFound(TKey id) => Error.NotFound(PersistenceErrors.NotFound, PersistenceMessages.NotFound, MessageArguments.Of(("entity", typeof(TEntity).Name), ("id", id)));

    /// <summary>
    /// 构造标识重复的错误。
    /// </summary>
    private static Error Duplicate(TKey id) => Error.Conflict(PersistenceErrors.Duplicate, PersistenceMessages.Duplicate, MessageArguments.Of(("entity", typeof(TEntity).Name), ("id", id)));

    /// <summary>
    /// 构造并发戳过期的错误。
    /// </summary>
    private static Error StaleStamp(TKey id) => Error.Conflict(PersistenceErrors.Concurrency, PersistenceMessages.StaleStamp, MessageArguments.Of(("entity", typeof(TEntity).Name), ("id", id)));

    /// <summary>
    /// 调用方实例的并发戳是否与当前行不同；同一实例或不带并发戳的实体不算过期。
    /// </summary>
    private static bool Stale(TEntity existing, TEntity entity)
        => !ReferenceEquals(existing, entity)
            && existing is IHasConcurrencyStamp current && entity is IHasConcurrencyStamp incoming
            && !string.Equals(current.ConcurrencyStamp, incoming.ConcurrencyStamp, StringComparison.Ordinal);

    /// <summary>
    /// 同一结果重复给每一项（写入前置条件不满足时整批同一错误）。
    /// </summary>
    private static Result[] Repeat(Result result, int count)
    {
        var results = new Result[count];
        Array.Fill(results, result);
        return results;
    }

    /// <summary>
    /// 去重并保持首次出现的顺序。
    /// </summary>
    private static List<TKey> Distinct(IEnumerable<TKey> ids)
    {
        var seen = new HashSet<TKey>();
        var distinct = new List<TKey>();
        foreach (var id in ids)
        {
            ArgumentNullException.ThrowIfNull(id);
            if (seen.Add(id))
            {
                distinct.Add(id);
            }
        }

        return distinct;
    }

    /// <summary>
    /// 把当前行上由协议写入的修改时间、并发戳与软删除标记复制到调用方的实例。
    /// </summary>
    private static void CopyAuditBack(TEntity source, TEntity target)
    {
        if (ReferenceEquals(source, target))
        {
            return;
        }

        if (source is IHasModificationTime sourceModified && target is IHasModificationTime targetModified)
        {
            targetModified.ModifiedAt = sourceModified.ModifiedAt;
        }

        if (source is IHasConcurrencyStamp sourceStamp && target is IHasConcurrencyStamp targetStamp)
        {
            targetStamp.ConcurrencyStamp = sourceStamp.ConcurrencyStamp;
        }

        if (source is ISoftDeletable sourceDeleted && target is ISoftDeletable targetDeleted)
        {
            targetDeleted.IsDeleted = sourceDeleted.IsDeleted;
            targetDeleted.DeletedAt = sourceDeleted.DeletedAt;
        }
    }

    /// <summary>
    /// 一次主键 <c>IN</c> 查询在本事务内加锁取回这些实体对应的可见行，按标识索引。
    /// </summary>
    private async Task<Dictionary<TKey, TEntity>> LoadLockedAsync(IReadOnlyList<TEntity> entities, CancellationToken cancellationToken)
    {
        var rows = await Locked().In(Distinct(entities.Select(static e => e.Id))).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.ToDictionary(static e => e.Id);
    }

    /// <summary>
    /// 写入路径的读取：可见行加事务内行锁（<c>FOR UPDATE</c> / <c>UPDLOCK</c>），读取与写入之间别人改不了这些行；SQLite 不支持行锁，由其数据库级写锁保证。
    /// </summary>
    private ISugarQueryable<TEntity> Locked()
    {
        var source = Visible(includeDeleted: false);
        return _unitOfWork.Client.CurrentConnectionConfig.DbType == DbType.Sqlite ? source : source.TranLock(DbLockType.Wait);
    }

    /// <summary>
    /// 写入前：要求活动工作单元、拒绝只读、确保事务已开启。
    /// </summary>
    private async ValueTask<Error?> PrepareWriteAsync(CancellationToken cancellationToken)
    {
        // 读取 Client 即完成“必须有活动工作单元”的校验
        _ = Client;
        if (_unitOfWork.IsReadOnly)
        {
            return Error.Validation(PersistenceErrors.InvalidUnitOfWorkState, PersistenceMessages.ReadOnly);
        }

        await _unitOfWork.EnsureTransactionAsync(cancellationToken).ConfigureAwait(false);
        return null;
    }

    /// <summary>
    /// 在可见性过滤之上追加查询自身的过滤规约。
    /// </summary>
    private ISugarQueryable<TEntity> Filtered(EntityQuery<TEntity> query)
    {
        var source = Visible(query.IncludeDeleted);
        return query.Filter is null ? source : source.Where(query.Filter.Criteria);
    }

    /// <summary>
    /// 软删除与租户过滤显式施加在查询上，与写入路径对取回行的判定一致；无租户（null）的调用匹配不到任何多租户实体。
    /// </summary>
    private ISugarQueryable<TEntity> Visible(bool includeDeleted)
    {
        var source = Client.Queryable<TEntity>();
        if (_isSoftDeletable && !includeDeleted)
        {
            source = source.Where(EntityExpressions.NotDeleted<TEntity>());
        }

        if (_isMultiTenant)
        {
            source = _unitOfWork.TenantId is { } tenantId
                ? source.Where(EntityExpressions.TenantIs<TEntity>(tenantId))
                : source.Where("1 = 0", (object?)null);
        }

        return source;
    }
}
