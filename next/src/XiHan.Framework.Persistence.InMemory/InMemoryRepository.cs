// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.InMemory;

/// <summary>
/// 内存仓储：在活动工作单元的覆盖层上读写，按协议维护审计字段、租户与软删除。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryRepository<TEntity, TKey>(InMemoryStore store, InMemoryUnitOfWork unitOfWork, TimeProvider timeProvider) : IRepository<TEntity, TKey>
    where TEntity : class, IEntity<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private readonly InMemoryStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly InMemoryUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc />
    public ValueTask<Maybe<TEntity>> FindAsync(TKey id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        var changes = Active.Changes<TEntity, TKey>();
        return ValueTask.FromResult(Lookup(changes, id).TryGetValue(out var entity) && IsVisible(entity, includeDeleted: false) ? Maybe.Some(entity) : default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TEntity>> ListAsync(EntityQuery<TEntity> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        IEnumerable<TEntity> items = Visible(query);
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
        if (Lookup(changes, entity.Id).HasValue)
        {
            return Task.FromResult<Result>(Error.Conflict(PersistenceErrors.Duplicate, $"{typeof(TEntity).Name} 已存在标识 {entity.Id}。"));
        }

        var applied = PersistenceConventions.ApplyOnAdd(entity, _unitOfWork.TenantId, _timeProvider.GetUtcNow());
        if (applied.IsFailure)
        {
            return Task.FromResult(applied);
        }

        changes.Upsert(entity);
        return Task.FromResult(Result.Success());
    }

    /// <inheritdoc />
    public Task<Result> UpdateAsync(TEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var changes = Active.Changes<TEntity, TKey>();
        if (!Exists(changes, entity.Id))
        {
            return Task.FromResult<Result>(NotFound(entity.Id));
        }

        PersistenceConventions.ApplyOnUpdate(entity, _timeProvider.GetUtcNow());
        changes.Upsert(entity);
        return Task.FromResult(Result.Success());
    }

    /// <inheritdoc />
    public Task<Result> RemoveAsync(TEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var changes = Active.Changes<TEntity, TKey>();
        if (!Exists(changes, entity.Id))
        {
            return Task.FromResult<Result>(NotFound(entity.Id));
        }

        if (PersistenceConventions.TrySoftDelete(entity, _timeProvider.GetUtcNow()))
        {
            changes.Upsert(entity);
        }
        else
        {
            changes.Remove(entity.Id);
        }

        return Task.FromResult(Result.Success());
    }

    private InMemoryUnitOfWork Active => _unitOfWork.State == UnitOfWorkState.Active
        ? _unitOfWork
        : throw new FrameworkException(PersistenceErrors.NoActiveUnitOfWork, $"工作单元处于 {_unitOfWork.State}；仓储必须在 Transaction 阶段之内使用。");

    private static Error NotFound(TKey id) => Error.NotFound(PersistenceErrors.NotFound, $"{typeof(TEntity).Name} 不存在标识 {id}。");

    /// <summary>
    /// 先查覆盖层，再查表；覆盖层标记为删除的视为不存在。
    /// </summary>
    private Maybe<TEntity> Lookup(InMemoryUnitOfWork.PendingChanges<TEntity, TKey> changes, TKey id)
    {
        var (found, removed, pending) = changes.Lookup(id);
        return found ? (removed ? default : Maybe.From(pending)) : _store.Table<TEntity, TKey>().Find(id);
    }

    private bool Exists(InMemoryUnitOfWork.PendingChanges<TEntity, TKey> changes, TKey id)
        => Lookup(changes, id).TryGetValue(out var entity) && IsVisible(entity, includeDeleted: false);

    private IEnumerable<TEntity> Visible(EntityQuery<TEntity> query)
    {
        var items = Active.Changes<TEntity, TKey>().Overlay(_store.Table<TEntity, TKey>().Snapshot())
            .Where(entity => IsVisible(entity, query.IncludeDeleted));
        return query.Filter is null ? items : items.Where(query.Filter.IsSatisfiedBy);
    }

    private bool IsVisible(TEntity entity, bool includeDeleted)
        => PersistenceConventions.IsVisibleTo(entity, _unitOfWork.TenantId) && (includeDeleted || !PersistenceConventions.IsDeleted(entity));
}
