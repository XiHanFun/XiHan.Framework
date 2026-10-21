// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;
using XiHan.Framework.Domain;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.EntityFrameworkCore;

/// <summary>
/// EF Core 仓储：读在活动工作单元内进行，首次写入开启事务，每次写入立即保存（读己之写），按协议维护审计字段、租户与软删除。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class EntityFrameworkCoreRepository<TEntity, TKey>(EntityFrameworkCoreUnitOfWork unitOfWork, TimeProvider timeProvider) : IRepository<TEntity, TKey>
    where TEntity : class, IEntity<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private static readonly bool IsSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(typeof(TEntity));
    private static readonly bool IsMultiTenant = typeof(IMultiTenant).IsAssignableFrom(typeof(TEntity));

    private readonly EntityFrameworkCoreUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc />
    public async ValueTask<Maybe<TEntity>> FindAsync(TKey id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        var entity = await Set.FindAsync([id], cancellationToken).ConfigureAwait(false);
        return entity is not null && IsVisible(entity, includeDeleted: false) ? Maybe.Some(entity) : default;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TEntity>> ListAsync(EntityQuery<TEntity> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var source = Filtered(query);
        IOrderedQueryable<TEntity>? ordered = null;
        foreach (var ordering in query.Orderings)
        {
            ordered = ordered is null
                ? (ordering.Descending ? source.OrderByDescending(ordering.KeySelector) : source.OrderBy(ordering.KeySelector))
                : (ordering.Descending ? ordered.ThenByDescending(ordering.KeySelector) : ordered.ThenBy(ordering.KeySelector));
        }

        return await (ordered ?? source).Skip(query.Skip).Take(query.Take).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<long> CountAsync(EntityQuery<TEntity> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Filtered(query).LongCountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result> AddAsync(TEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (await PrepareWriteAsync(cancellationToken).ConfigureAwait(false) is { } rejected)
        {
            return rejected;
        }

        // 已存在的行（含已软删除、其他租户）都与新键冲突
        if (await Set.FindAsync([entity.Id], cancellationToken).ConfigureAwait(false) is not null)
        {
            return Error.Conflict(PersistenceErrors.Duplicate, $"{typeof(TEntity).Name} 已存在标识 {entity.Id}。");
        }

        var applied = PersistenceConventions.ApplyOnAdd(entity, _unitOfWork.TenantId, _timeProvider.GetUtcNow());
        if (applied.IsFailure)
        {
            return applied;
        }

        Set.Add(entity);
        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> UpdateAsync(TEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (await PrepareWriteAsync(cancellationToken).ConfigureAwait(false) is { } rejected)
        {
            return rejected;
        }

        var existing = await Set.FindAsync([entity.Id], cancellationToken).ConfigureAwait(false);
        if (existing is null || !IsVisible(existing, includeDeleted: false))
        {
            return NotFound(entity.Id);
        }

        if (!ReferenceEquals(existing, entity))
        {
            // 游离实体：先比对并发戳，再把值复制到跟踪实例
            if (existing is IHasConcurrencyStamp current && entity is IHasConcurrencyStamp incoming
                && !string.Equals(current.ConcurrencyStamp, incoming.ConcurrencyStamp, StringComparison.Ordinal))
            {
                return Error.Conflict(PersistenceErrors.Concurrency, $"{typeof(TEntity).Name} {entity.Id} 的并发戳已过期。");
            }

            Context.Entry(existing).CurrentValues.SetValues(entity);
        }

        PersistenceConventions.ApplyOnUpdate(existing, _timeProvider.GetUtcNow());
        var saved = await SaveAsync(cancellationToken).ConfigureAwait(false);
        if (saved.IsSuccess && !ReferenceEquals(existing, entity))
        {
            CopyAuditBack(existing, entity);
        }

        return saved;
    }

    /// <inheritdoc />
    public async Task<Result> RemoveAsync(TEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (await PrepareWriteAsync(cancellationToken).ConfigureAwait(false) is { } rejected)
        {
            return rejected;
        }

        var existing = await Set.FindAsync([entity.Id], cancellationToken).ConfigureAwait(false);
        if (existing is null || !IsVisible(existing, includeDeleted: false))
        {
            return NotFound(entity.Id);
        }

        if (!PersistenceConventions.TrySoftDelete(existing, _timeProvider.GetUtcNow()))
        {
            Set.Remove(existing);
        }

        var saved = await SaveAsync(cancellationToken).ConfigureAwait(false);
        if (saved.IsSuccess && !ReferenceEquals(existing, entity))
        {
            CopyAuditBack(existing, entity);
        }

        return saved;
    }

    private DbContext Context => _unitOfWork.State == UnitOfWorkState.Active
        ? _unitOfWork.Context
        : throw new FrameworkException(PersistenceErrors.NoActiveUnitOfWork, $"工作单元处于 {_unitOfWork.State}；仓储必须在 Transaction 阶段之内使用。");

    private DbSet<TEntity> Set => Context.Set<TEntity>();

    private static Error NotFound(TKey id) => Error.NotFound(PersistenceErrors.NotFound, $"{typeof(TEntity).Name} 不存在标识 {id}。");

    private static void CopyAuditBack(TEntity source, TEntity target)
    {
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
    /// 写入前：要求活动工作单元、拒绝只读、确保事务已开启。
    /// </summary>
    private async ValueTask<Error?> PrepareWriteAsync(CancellationToken cancellationToken)
    {
        _ = Context;
        if (_unitOfWork.IsReadOnly)
        {
            return Error.Validation(PersistenceErrors.InvalidUnitOfWorkState, "只读工作单元不能写入。");
        }

        await _unitOfWork.EnsureTransactionAsync(cancellationToken).ConfigureAwait(false);
        return null;
    }

    private async Task<Result> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException exception)
        {
            return Error.Conflict(PersistenceErrors.Concurrency, $"{typeof(TEntity).Name} 发生乐观并发冲突：{exception.Message}");
        }
    }

    private IQueryable<TEntity> Filtered(EntityQuery<TEntity> query)
    {
        var source = Visible(query.IncludeDeleted);
        return query.Filter is null ? source : source.Where(query.Filter.Criteria);
    }

    /// <summary>
    /// 软删除与租户过滤显式施加在查询上，与 <see cref="IsVisible"/> 对跟踪实体的判定一致。
    /// </summary>
    private IQueryable<TEntity> Visible(bool includeDeleted)
    {
        IQueryable<TEntity> source = Set;
        if (IsSoftDeletable && !includeDeleted)
        {
            source = source.Where(static e => !EF.Property<bool>(e, nameof(ISoftDeletable.IsDeleted)));
        }

        if (IsMultiTenant)
        {
            var tenantId = _unitOfWork.TenantId;
            source = source.Where(e => EF.Property<string>(e, nameof(IMultiTenant.TenantId)) == tenantId);
        }

        return source;
    }

    private bool IsVisible(TEntity entity, bool includeDeleted)
        => PersistenceConventions.IsVisibleTo(entity, _unitOfWork.TenantId) && (includeDeleted || !PersistenceConventions.IsDeleted(entity));
}
