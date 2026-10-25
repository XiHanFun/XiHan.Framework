// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.InMemory;

/// <summary>
/// 内存仓储：在活动工作单元的覆盖层上读写，按协议维护审计字段、租户与软删除。批量方法在同一次调用里逐项进入覆盖层，提交时由表原子应用。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryRepository<TEntity, TKey>(InMemoryStore store, InMemoryUnitOfWork unitOfWork, TimeProvider timeProvider) : IRepository<TEntity, TKey>
    where TEntity : class, IEntity<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// 已提交数据所在的存储。
    /// </summary>
    private readonly InMemoryStore _store = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>
    /// 本作用域的工作单元；提供覆盖层与当前租户。
    /// </summary>
    private readonly InMemoryUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    /// <summary>
    /// 审计字段使用的时间源。
    /// </summary>
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc />
    public ValueTask<Maybe<TEntity>> FindAsync(TKey id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        var changes = Active.Changes<TEntity, TKey>();
        return ValueTask.FromResult(Lookup(changes, id).TryGetValue(out var entity) && IsVisible(entity, includeDeleted: false) ? Maybe.Some(entity) : default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TEntity>> FindManyAsync(IReadOnlyList<TKey> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var changes = Active.Changes<TEntity, TKey>();
        var found = new List<TEntity>(ids.Count);
        var seen = new HashSet<TKey>();
        foreach (var id in ids)
        {
            ArgumentNullException.ThrowIfNull(id);
            // 重复标识只返回一次，顺序按入参
            if (seen.Add(id) && Lookup(changes, id).TryGetValue(out var entity) && IsVisible(entity, includeDeleted: false))
            {
                found.Add(entity);
            }
        }

        return Task.FromResult<IReadOnlyList<TEntity>>(found);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TEntity>> ListAsync(EntityQuery<TEntity> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        IEnumerable<TEntity> items = Visible(query);
        // 第一项排序用 OrderBy，后续用 ThenBy 叠加，保证多级排序按声明顺序生效而不是互相覆盖
        foreach (var ordering in query.Orderings)
        {
            var key = ordering.KeySelector.Compile();
            items = items is IOrderedEnumerable<TEntity> ordered
                ? (ordering.Descending ? ordered.ThenByDescending(key) : ordered.ThenBy(key))
                : (ordering.Descending ? items.OrderByDescending(key) : items.OrderBy(key));
        }

        return Task.FromResult<IReadOnlyList<TEntity>>(items.Skip(query.Skip).Take(query.Take).ToList());
    }

    /// <inheritdoc />
    public Task<long> CountAsync(EntityQuery<TEntity> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Task.FromResult(Visible(query).LongCount());
    }

    /// <inheritdoc />
    public Task<Result> AddAsync(TEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var changes = Active.Changes<TEntity, TKey>();
        return Task.FromResult(Add(changes, entity, _timeProvider.GetUtcNow()));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Result>> AddRangeAsync(IReadOnlyList<TEntity> entities, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var changes = Active.Changes<TEntity, TKey>();
        cancellationToken.ThrowIfCancellationRequested();
        var now = _timeProvider.GetUtcNow();
        var results = new Result[entities.Count];
        // 逐项进入覆盖层：前一项已写入，后一项的同键判定就能看见它，批内重复自然成为 Duplicate
        for (var index = 0; index < entities.Count; index++)
        {
            ArgumentNullException.ThrowIfNull(entities[index]);
            results[index] = Add(changes, entities[index], now);
        }

        return Task.FromResult<IReadOnlyList<Result>>(results);
    }

    /// <inheritdoc />
    public Task<Result> UpdateAsync(TEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var changes = Active.Changes<TEntity, TKey>();
        return Task.FromResult(Update(changes, entity, _timeProvider.GetUtcNow()));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Result>> UpdateRangeAsync(IReadOnlyList<TEntity> entities, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var changes = Active.Changes<TEntity, TKey>();
        cancellationToken.ThrowIfCancellationRequested();
        var now = _timeProvider.GetUtcNow();
        var results = new Result[entities.Count];
        for (var index = 0; index < entities.Count; index++)
        {
            ArgumentNullException.ThrowIfNull(entities[index]);
            results[index] = Update(changes, entities[index], now);
        }

        return Task.FromResult<IReadOnlyList<Result>>(results);
    }

    /// <inheritdoc />
    public Task<Result> RemoveAsync(TEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var changes = Active.Changes<TEntity, TKey>();
        return Task.FromResult(Remove(changes, entity, _timeProvider.GetUtcNow()));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Result>> RemoveRangeAsync(IReadOnlyList<TEntity> entities, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var changes = Active.Changes<TEntity, TKey>();
        cancellationToken.ThrowIfCancellationRequested();
        var now = _timeProvider.GetUtcNow();
        var results = new Result[entities.Count];
        // 逐项施加：软删除的实体打标后立即不可见，物理删除的进入删除集合，同一实体第二次就是 NotFound
        for (var index = 0; index < entities.Count; index++)
        {
            ArgumentNullException.ThrowIfNull(entities[index]);
            results[index] = Remove(changes, entities[index], now);
        }

        return Task.FromResult<IReadOnlyList<Result>>(results);
    }

    /// <summary>
    /// 处于活动状态的工作单元；不活动时抛出 <see cref="PersistenceErrors.NoActiveUnitOfWork"/>，每个操作都经此校验。
    /// </summary>
    private InMemoryUnitOfWork Active => _unitOfWork.State == UnitOfWorkState.Active
        ? _unitOfWork
        : throw new FrameworkException(PersistenceErrors.NoActiveUnitOfWork, $"The unit of work is in state '{_unitOfWork.State}'; repositories must be used within the Transaction stage.");

    /// <summary>
    /// 新增一项：重复判定不做可见性过滤（已软删除或属于其他租户的同键行同样与新键冲突），通过后写入审计字段并进覆盖层。
    /// </summary>
    private Result Add(InMemoryUnitOfWork.PendingChanges<TEntity, TKey> changes, TEntity entity, DateTimeOffset now)
    {
        if (Lookup(changes, entity.Id).HasValue)
        {
            return Error.Conflict(PersistenceErrors.Duplicate, PersistenceMessages.Duplicate, MessageArguments.Of(("entity", typeof(TEntity).Name), ("id", entity.Id)));
        }

        var applied = PersistenceConventions.ApplyOnAdd(entity, _unitOfWork.TenantId, now);
        if (applied.IsFailure)
        {
            return applied;
        }

        changes.Upsert(entity);
        return Result.Success();
    }

    /// <summary>
    /// 更新一项：目标须存在且可见；游离实例的并发戳与当前值不同即冲突，之后刷新审计字段并进覆盖层。
    /// </summary>
    private Result Update(InMemoryUnitOfWork.PendingChanges<TEntity, TKey> changes, TEntity entity, DateTimeOffset now)
    {
        if (!Lookup(changes, entity.Id).TryGetValue(out var existing) || !IsVisible(existing, includeDeleted: false))
        {
            return NotFound(entity.Id);
        }

        // 同一实例（读己之写拿到的引用）不比对；游离实例先比对并发戳，与 EF Core 实现一致
        if (!ReferenceEquals(existing, entity)
            && existing is IHasConcurrencyStamp current && entity is IHasConcurrencyStamp incoming
            && !string.Equals(current.ConcurrencyStamp, incoming.ConcurrencyStamp, StringComparison.Ordinal))
        {
            return Error.Conflict(PersistenceErrors.Concurrency, PersistenceMessages.StaleStamp, MessageArguments.Of(("entity", typeof(TEntity).Name), ("id", entity.Id)));
        }

        PersistenceConventions.ApplyOnUpdate(entity, now);
        changes.Upsert(entity);
        return Result.Success();
    }

    /// <summary>
    /// 删除一项：目标须存在且可见；软删除是一次打标更新，走 Upsert，物理删除才进入覆盖层的删除集合。
    /// </summary>
    private Result Remove(InMemoryUnitOfWork.PendingChanges<TEntity, TKey> changes, TEntity entity, DateTimeOffset now)
    {
        if (!Exists(changes, entity.Id))
        {
            return NotFound(entity.Id);
        }

        if (PersistenceConventions.TrySoftDelete(entity, now))
        {
            changes.Upsert(entity);
        }
        else
        {
            changes.Remove(entity.Id);
        }

        return Result.Success();
    }

    /// <summary>
    /// 构造实体不存在的错误。
    /// </summary>
    private static Error NotFound(TKey id) => Error.NotFound(PersistenceErrors.NotFound, PersistenceMessages.NotFound, MessageArguments.Of(("entity", typeof(TEntity).Name), ("id", id)));

    /// <summary>
    /// 先查覆盖层，再查表；覆盖层标记为删除的视为不存在。
    /// </summary>
    private Maybe<TEntity> Lookup(InMemoryUnitOfWork.PendingChanges<TEntity, TKey> changes, TKey id)
    {
        var (found, removed, pending) = changes.Lookup(id);
        return found ? (removed ? default : Maybe.From(pending)) : _store.Table<TEntity, TKey>().Find(id);
    }

    /// <summary>
    /// 实体是否存在且对当前调用可见（未软删除、租户匹配）；删除以此判定目标。
    /// </summary>
    private bool Exists(InMemoryUnitOfWork.PendingChanges<TEntity, TKey> changes, TKey id)
        => Lookup(changes, id).TryGetValue(out var entity) && IsVisible(entity, includeDeleted: false);

    /// <summary>
    /// 把覆盖层合并到表快照上，再施加可见性与查询过滤；排序与分页由调用方处理。
    /// </summary>
    private IEnumerable<TEntity> Visible(EntityQuery<TEntity> query)
    {
        var items = Active.Changes<TEntity, TKey>().Overlay(_store.Table<TEntity, TKey>().Snapshot())
            .Where(entity => IsVisible(entity, query.IncludeDeleted));
        return query.Filter is null ? items : items.Where(query.Filter.IsSatisfiedBy);
    }

    /// <summary>
    /// 实体是否对当前调用可见：租户必须匹配，且除非显式包含，已软删除的不可见。
    /// </summary>
    private bool IsVisible(TEntity entity, bool includeDeleted)
        => PersistenceConventions.IsVisibleTo(entity, _unitOfWork.TenantId) && (includeDeleted || !PersistenceConventions.IsDeleted(entity));
}
