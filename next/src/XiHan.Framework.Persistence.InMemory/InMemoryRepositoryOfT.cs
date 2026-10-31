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
/// 内存仓储。
/// </summary>
/// <remarks>
/// 在活动工作单元的覆盖层上读写，按协议维护审计字段、租户与软删除。读写都复制：查找与查询交出的是副本，写入存的是调用方实例的副本，调用方在读出的实例上的修改不经仓储写回就碰不到表与覆盖层。更新沿用当前行的创建时间、租户与删除标记，软删除在当前行的副本上打标。批量方法在同一次调用里逐项进入覆盖层，提交时由工作单元校验后原子应用。
/// </remarks>
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
    /// 本作用域的工作单元。
    /// </summary>
    /// <remarks>
    /// 提供覆盖层与当前租户。
    /// </remarks>
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
        return ValueTask.FromResult(Lookup(changes, id, out _).TryGetValue(out var entity) && IsVisible(entity, includeDeleted: false) ? Maybe.Some(EntityCopy.Of(entity)) : default);
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
            if (seen.Add(id) && Lookup(changes, id, out _).TryGetValue(out var entity) && IsVisible(entity, includeDeleted: false))
            {
                found.Add(EntityCopy.Of(entity));
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

        // 过滤、排序、分页都在表与覆盖层自己的对象上做，只复制最终交出的那一页
        return Task.FromResult<IReadOnlyList<TEntity>>(items.Skip(query.Skip).Take(query.Take).Select(EntityCopy.Of).ToList());
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
    /// 处于活动状态的工作单元。
    /// </summary>
    /// <remarks>
    /// 不活动时抛出 <see cref="PersistenceErrors.NoActiveUnitOfWork"/>，每个操作都经此校验。
    /// </remarks>
    private InMemoryUnitOfWork Active => _unitOfWork.State == UnitOfWorkState.Active
        ? _unitOfWork
        : throw new FrameworkException(PersistenceErrors.NoActiveUnitOfWork, $"The unit of work is in state '{_unitOfWork.State}'; repositories must be used within the Transaction stage.");

    /// <summary>
    /// 新增一项。
    /// </summary>
    /// <remarks>
    /// 重复判定不做可见性过滤（已软删除或属于其他租户的同键行同样与新键冲突），通过后写入审计字段并把副本放进覆盖层。别的工作单元在此之后提交了同一标识，由提交时的校验报告重复。
    /// </remarks>
    private Result Add(InMemoryUnitOfWork.PendingChanges<TEntity, TKey> changes, TEntity entity, DateTimeOffset now)
    {
        if (Lookup(changes, entity.Id, out var committed).HasValue)
        {
            return Error.Conflict(PersistenceErrors.Duplicate, PersistenceMessages.Duplicate, MessageArguments.Of(("entity", typeof(TEntity).Name), ("id", entity.Id)));
        }

        var applied = PersistenceConventions.ApplyOnAdd(entity, _unitOfWork.TenantId, now);
        if (applied.IsFailure)
        {
            return applied;
        }

        changes.Upsert(EntityCopy.Of(entity), committed);
        _unitOfWork.DomainEvents.Track(entity);
        return Result.Success();
    }

    /// <summary>
    /// 更新一项。
    /// </summary>
    /// <remarks>
    /// 目标须存在且可见；调用方实例的并发戳与当前值不同即冲突。之后沿用当前行的创建时间、租户与删除标记，刷新修改时间与并发戳，把副本放进覆盖层。
    /// </remarks>
    private Result Update(InMemoryUnitOfWork.PendingChanges<TEntity, TKey> changes, TEntity entity, DateTimeOffset now)
    {
        if (!Lookup(changes, entity.Id, out var committed).TryGetValue(out var current) || !IsVisible(current, includeDeleted: false))
        {
            return NotFound(entity.Id);
        }

        // 调用方手里的总是副本（读出时复制过），一律比对并发戳，与 SqlSugar 实现一致
        if (current is IHasConcurrencyStamp stored && entity is IHasConcurrencyStamp incoming
            && !string.Equals(stored.ConcurrencyStamp, incoming.ConcurrencyStamp, StringComparison.Ordinal))
        {
            return Error.Conflict(PersistenceErrors.Concurrency, PersistenceMessages.StaleStamp, MessageArguments.Of(("entity", typeof(TEntity).Name), ("id", entity.Id)));
        }

        PersistenceConventions.ApplyOnUpdate(entity, current, now);
        changes.Upsert(EntityCopy.Of(entity), committed);
        _unitOfWork.DomainEvents.Track(entity);
        return Result.Success();
    }

    /// <summary>
    /// 删除一项。
    /// </summary>
    /// <remarks>
    /// 目标须存在且可见。软删除在当前行的副本上打标（调用方实例上的其他改动不写入），作为一次更新进覆盖层，删除标记再同步回调用方实例；物理删除才进入覆盖层的删除集合。
    /// </remarks>
    private Result Remove(InMemoryUnitOfWork.PendingChanges<TEntity, TKey> changes, TEntity entity, DateTimeOffset now)
    {
        if (!Lookup(changes, entity.Id, out var committed).TryGetValue(out var current) || !IsVisible(current, includeDeleted: false))
        {
            return NotFound(entity.Id);
        }

        var tombstone = EntityCopy.Of(current);
        if (PersistenceConventions.TrySoftDelete(tombstone, now))
        {
            changes.Upsert(tombstone, committed);
            if (tombstone is ISoftDeletable marked && entity is ISoftDeletable target)
            {
                target.IsDeleted = marked.IsDeleted;
                target.DeletedAt = marked.DeletedAt;
            }
        }
        else
        {
            changes.Remove(entity.Id, committed);
        }

        _unitOfWork.DomainEvents.Track(entity);
        return Result.Success();
    }

    /// <summary>
    /// 构造实体不存在的错误。
    /// </summary>
    private static Error NotFound(TKey id) => Error.NotFound(PersistenceErrors.NotFound, PersistenceMessages.NotFound, MessageArguments.Of(("entity", typeof(TEntity).Name), ("id", id)));

    /// <summary>
    /// 先查覆盖层，再查表。
    /// </summary>
    /// <remarks>
    /// 覆盖层标记为删除的视为不存在。返回的是覆盖层或表自己的对象，只用于判定，交给调用方之前必须复制。<paramref name="committed"/> 是从表里读到的行（标识已在覆盖层里时为 <see langword="null"/>）：写入以它为依据，提交时据此校验。
    /// </remarks>
    private Maybe<TEntity> Lookup(InMemoryUnitOfWork.PendingChanges<TEntity, TKey> changes, TKey id, out TEntity? committed)
    {
        var (found, removed, pending) = changes.Lookup(id);
        if (found)
        {
            committed = null;
            return removed ? default : Maybe.From(pending);
        }

        committed = _store.Table<TEntity, TKey>().Row(id);
        return Maybe.From(committed);
    }

    /// <summary>
    /// 把覆盖层合并到表的行上，再施加可见性与查询过滤。
    /// </summary>
    /// <remarks>
    /// 排序与分页由调用方处理。返回的是覆盖层与表自己的对象。
    /// </remarks>
    private IEnumerable<TEntity> Visible(EntityQuery<TEntity> query)
    {
        var items = Active.Changes<TEntity, TKey>().Overlay(_store.Table<TEntity, TKey>().Rows())
            .Where(entity => IsVisible(entity, query.IncludeDeleted));
        return query.Filter is null ? items : items.Where(query.Filter.IsSatisfiedBy);
    }

    /// <summary>
    /// 实体是否对当前调用可见：租户必须匹配，且除非显式包含，已软删除的不可见。
    /// </summary>
    private bool IsVisible(TEntity entity, bool includeDeleted)
        => PersistenceConventions.IsVisibleTo(entity, _unitOfWork.TenantId) && (includeDeleted || !PersistenceConventions.IsDeleted(entity));
}
