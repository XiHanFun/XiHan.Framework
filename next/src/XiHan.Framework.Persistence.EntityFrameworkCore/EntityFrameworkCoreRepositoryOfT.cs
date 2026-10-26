// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.EntityFrameworkCore;

/// <summary>
/// EF Core 仓储：读在活动工作单元内进行，首次写入开启事务，每次写入立即保存（读己之写），按协议维护审计字段、租户与软删除。
/// 批量方法用一次 <c>IN</c> 查询取回目标行、逐项校验后一次 <c>SaveChanges</c>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class EntityFrameworkCoreRepository<TEntity, TKey>(EntityFrameworkCoreUnitOfWork unitOfWork, TimeProvider timeProvider) : IRepository<TEntity, TKey>
    where TEntity : class, IEntity<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// 实体是否实现 <see cref="ISoftDeletable"/>；按闭合泛型只判断一次，查询过滤据此决定是否追加删除标记条件。
    /// </summary>
    private static readonly bool _isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(typeof(TEntity));

    /// <summary>
    /// 实体是否实现 <see cref="IMultiTenant"/>；按闭合泛型只判断一次，查询过滤据此决定是否追加租户条件。
    /// </summary>
    private static readonly bool _isMultiTenant = typeof(IMultiTenant).IsAssignableFrom(typeof(TEntity));

    /// <summary>
    /// 本作用域的工作单元；提供 DbContext、事务与当前租户。
    /// </summary>
    private readonly EntityFrameworkCoreUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    /// <summary>
    /// 审计字段使用的时间源。
    /// </summary>
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc />
    public async ValueTask<Maybe<TEntity>> FindAsync(TKey id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        // DbSet.FindAsync 先查变更跟踪器再查数据库，天然读己之写；可见性在内存里判定，与查询路径的过滤保持一致
        var entity = await Set.FindAsync([id], cancellationToken).ConfigureAwait(false);
        return entity is not null && IsVisible(entity, includeDeleted: false) ? Maybe.Some(entity) : default;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TEntity>> FindManyAsync(IReadOnlyList<TKey> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        _ = Context;
        if (ids.Count == 0)
        {
            return [];
        }

        var distinct = Distinct(ids);
        var rows = await LoadAsync(distinct, cancellationToken).ConfigureAwait(false);
        var found = new List<TEntity>(distinct.Count);
        foreach (var id in distinct)
        {
            if (rows.TryGetValue(id, out var entity) && IsVisible(entity, includeDeleted: false))
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
        // 第一项排序用 OrderBy，后续用 ThenBy 叠加，保证多级排序按声明顺序翻译成 SQL
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
            return Duplicate(entity.Id);
        }

        var applied = PersistenceConventions.ApplyOnAdd(entity, _unitOfWork.TenantId, _timeProvider.GetUtcNow());
        if (applied.IsFailure)
        {
            return applied;
        }

        // 立即保存：同一工作单元内后续的 Find 与查询都能看到新行，事务提交前对其他连接不可见
        Set.Add(entity);
        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _unitOfWork.DomainEvents.Track(entity);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Result>> AddRangeAsync(IReadOnlyList<TEntity> entities, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entities);
        _ = Context;
        if (entities.Count == 0)
        {
            return [];
        }

        if (await PrepareWriteAsync(cancellationToken).ConfigureAwait(false) is { } rejected)
        {
            return Repeat(rejected, entities.Count);
        }

        // 一次 IN 查询拿到库里已存在的标识（含已软删除、其他租户）；之后每个通过校验的项把标识加进同一集合，批内重复就能被看见
        var taken = (await LoadAsync(Distinct(entities.Select(static e => e.Id)), cancellationToken).ConfigureAwait(false)).Keys.ToHashSet();
        var now = _timeProvider.GetUtcNow();
        var results = new Result[entities.Count];
        var pending = new List<int>(entities.Count);
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
            pending.Add(index);
        }

        if (pending.Count == 0)
        {
            return results;
        }

        // 通过校验的项一次写入：写入阶段的失败对这些项统一报告
        Set.AddRange(pending.Select(index => entities[index]));
        var saved = await SaveAsync(cancellationToken).ConfigureAwait(false);
        foreach (var index in pending)
        {
            results[index] = saved;
            if (saved.IsSuccess)
            {
                _unitOfWork.DomainEvents.Track(entities[index]);
            }
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

        var existing = await Set.FindAsync([entity.Id], cancellationToken).ConfigureAwait(false);
        if (existing is null || !IsVisible(existing, includeDeleted: false))
        {
            return NotFound(entity.Id);
        }

        if (Stage(existing, entity, _timeProvider.GetUtcNow()) is { } conflict)
        {
            return conflict;
        }

        var saved = await SaveAsync(cancellationToken).ConfigureAwait(false);
        // 审计字段写在跟踪实例上，保存成功后同步回调用方的游离实例，让它拿到新的修改时间与并发戳
        if (saved.IsSuccess)
        {
            if (!ReferenceEquals(existing, entity))
            {
                CopyAuditBack(existing, entity);
            }

            // 调用方的实例持有领域事件；登记它而不是跟踪的当前行
            _unitOfWork.DomainEvents.Track(entity);
        }

        return saved;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Result>> UpdateRangeAsync(IReadOnlyList<TEntity> entities, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entities);
        _ = Context;
        if (entities.Count == 0)
        {
            return [];
        }

        if (await PrepareWriteAsync(cancellationToken).ConfigureAwait(false) is { } rejected)
        {
            return Repeat(rejected, entities.Count);
        }

        // 一次 IN 查询把目标行装进跟踪器（已跟踪的按标识解析为同一实例），之后逐项比对并发戳、复制值、刷新审计字段
        var rows = await LoadAsync(Distinct(entities.Select(static e => e.Id)), cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        var results = new Result[entities.Count];
        var pending = new List<(int Index, TEntity Existing)>(entities.Count);
        for (var index = 0; index < entities.Count; index++)
        {
            var entity = entities[index];
            ArgumentNullException.ThrowIfNull(entity);
            if (!rows.TryGetValue(entity.Id, out var existing) || !IsVisible(existing, includeDeleted: false))
            {
                results[index] = NotFound(entity.Id);
                continue;
            }

            // 前一项已刷新跟踪实例的并发戳：同一标识的第二个游离实例在这里因戳过期而冲突
            if (Stage(existing, entity, now) is { } conflict)
            {
                results[index] = conflict;
                continue;
            }

            pending.Add((index, existing));
        }

        if (pending.Count == 0)
        {
            return results;
        }

        var saved = await SaveAsync(cancellationToken).ConfigureAwait(false);
        foreach (var (index, existing) in pending)
        {
            results[index] = saved;
            if (saved.IsSuccess)
            {
                if (!ReferenceEquals(existing, entities[index]))
                {
                    CopyAuditBack(existing, entities[index]);
                }

                _unitOfWork.DomainEvents.Track(entities[index]);
            }
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

        var existing = await Set.FindAsync([entity.Id], cancellationToken).ConfigureAwait(false);
        if (existing is null || !IsVisible(existing, includeDeleted: false))
        {
            return NotFound(entity.Id);
        }

        StageRemoval(existing, _timeProvider.GetUtcNow());
        var saved = await SaveAsync(cancellationToken).ConfigureAwait(false);
        if (saved.IsSuccess)
        {
            if (!ReferenceEquals(existing, entity))
            {
                CopyAuditBack(existing, entity);
            }

            // 调用方的实例持有领域事件；登记它而不是跟踪的当前行
            _unitOfWork.DomainEvents.Track(entity);
        }

        return saved;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Result>> RemoveRangeAsync(IReadOnlyList<TEntity> entities, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entities);
        _ = Context;
        if (entities.Count == 0)
        {
            return [];
        }

        if (await PrepareWriteAsync(cancellationToken).ConfigureAwait(false) is { } rejected)
        {
            return Repeat(rejected, entities.Count);
        }

        var rows = await LoadAsync(Distinct(entities.Select(static e => e.Id)), cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        var results = new Result[entities.Count];
        var pending = new List<(int Index, TEntity Existing)>(entities.Count);
        // 软删除打标后跟踪实例立即不可见；物理删除的实例仍可见，用已删除集合让同一实体第二次成为 NotFound
        var removed = new HashSet<TKey>();
        for (var index = 0; index < entities.Count; index++)
        {
            var entity = entities[index];
            ArgumentNullException.ThrowIfNull(entity);
            if (!rows.TryGetValue(entity.Id, out var existing) || !IsVisible(existing, includeDeleted: false) || !removed.Add(entity.Id))
            {
                results[index] = NotFound(entity.Id);
                continue;
            }

            StageRemoval(existing, now);
            pending.Add((index, existing));
        }

        if (pending.Count == 0)
        {
            return results;
        }

        var saved = await SaveAsync(cancellationToken).ConfigureAwait(false);
        foreach (var (index, existing) in pending)
        {
            results[index] = saved;
            if (saved.IsSuccess)
            {
                if (!ReferenceEquals(existing, entities[index]))
                {
                    CopyAuditBack(existing, entities[index]);
                }

                _unitOfWork.DomainEvents.Track(entities[index]);
            }
        }

        return results;
    }

    /// <summary>
    /// 活动工作单元的 DbContext；工作单元不活动时抛出 <see cref="PersistenceErrors.NoActiveUnitOfWork"/>，每个操作都经此校验。
    /// </summary>
    private DbContext Context => _unitOfWork.State == UnitOfWorkState.Active
        ? _unitOfWork.Context
        : throw new FrameworkException(PersistenceErrors.NoActiveUnitOfWork, $"The unit of work is in state '{_unitOfWork.State}'; repositories must be used within the Transaction stage.");

    /// <summary>
    /// 实体对应的 DbSet。
    /// </summary>
    private DbSet<TEntity> Set => Context.Set<TEntity>();

    /// <summary>
    /// 构造实体不存在的错误。
    /// </summary>
    private static Error NotFound(TKey id) => Error.NotFound(PersistenceErrors.NotFound, PersistenceMessages.NotFound, MessageArguments.Of(("entity", typeof(TEntity).Name), ("id", id)));

    /// <summary>
    /// 构造标识重复的错误。
    /// </summary>
    private static Error Duplicate(TKey id) => Error.Conflict(PersistenceErrors.Duplicate, PersistenceMessages.Duplicate, MessageArguments.Of(("entity", typeof(TEntity).Name), ("id", id)));

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
    /// 一次 <c>IN</c> 查询取回这些标识对应的行（不做可见性过滤，与 <see cref="FindAsync"/> 一样在内存里判定），按标识索引；已跟踪的行由 EF 解析为同一实例。
    /// </summary>
    private async Task<Dictionary<TKey, TEntity>> LoadAsync(List<TKey> ids, CancellationToken cancellationToken)
    {
        // 用 EF.Property 按名字访问主键列，表达式树里不需要把 TEntity 转成接口；标识列表作为参数翻译成 IN
        var rows = await Set.Where(e => ids.Contains(EF.Property<TKey>(e, nameof(IEntity<TKey>.Id)))).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.ToDictionary(static e => e.Id);
    }

    /// <summary>
    /// 在跟踪实例上准备一次更新：传入的是跟踪实例时直接改它；否则先比对并发戳（不同即返回冲突），再把值复制到跟踪实例；最后刷新审计字段。
    /// </summary>
    private Error? Stage(TEntity existing, TEntity entity, DateTimeOffset now)
    {
        if (!ReferenceEquals(existing, entity))
        {
            if (existing is IHasConcurrencyStamp current && entity is IHasConcurrencyStamp incoming
                && !string.Equals(current.ConcurrencyStamp, incoming.ConcurrencyStamp, StringComparison.Ordinal))
            {
                return Error.Conflict(PersistenceErrors.Concurrency, PersistenceMessages.StaleStamp, MessageArguments.Of(("entity", typeof(TEntity).Name), ("id", entity.Id)));
            }

            Context.Entry(existing).CurrentValues.SetValues(entity);
        }

        PersistenceConventions.ApplyOnUpdate(existing, now);
        return null;
    }

    /// <summary>
    /// 在跟踪实例上准备一次删除：软删除只打标，由 SaveChanges 当作更新；不支持软删除的实体才真正删除。
    /// </summary>
    private void StageRemoval(TEntity existing, DateTimeOffset now)
    {
        if (!PersistenceConventions.TrySoftDelete(existing, now))
        {
            Set.Remove(existing);
        }
    }

    /// <summary>
    /// 把跟踪实例上由协议写入的修改时间、并发戳与软删除标记复制到调用方的游离实例。
    /// </summary>
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
        // 读取 Context 即完成“必须有活动工作单元”的校验
        _ = Context;
        if (_unitOfWork.IsReadOnly)
        {
            return Error.Validation(PersistenceErrors.InvalidUnitOfWorkState, PersistenceMessages.ReadOnly);
        }

        await _unitOfWork.EnsureTransactionAsync(cancellationToken).ConfigureAwait(false);
        return null;
    }

    /// <summary>
    /// 保存变更；并发令牌不匹配时把 <see cref="DbUpdateConcurrencyException"/> 转为 <see cref="PersistenceErrors.Concurrency"/> 结果。
    /// </summary>
    private async Task<Result> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException exception)
        {
            return Error.Conflict(PersistenceErrors.Concurrency, PersistenceMessages.ConcurrencyConflict, MessageArguments.Of(("entity", typeof(TEntity).Name), ("reason", exception.Message)));
        }
    }

    /// <summary>
    /// 在可见性过滤之上追加查询自身的过滤规约。
    /// </summary>
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
        // 用 EF.Property 按名字访问列，表达式树里不需要把 TEntity 转成接口，任何 Provider 都能翻译
        if (_isSoftDeletable && !includeDeleted)
        {
            source = source.Where(static e => !EF.Property<bool>(e, nameof(ISoftDeletable.IsDeleted)));
        }

        if (_isMultiTenant)
        {
            // 先取到局部变量再捕获，EF 会把它作为参数而不是常量，查询计划可以复用；无租户（null）的调用匹配不到任何行
            var tenantId = _unitOfWork.TenantId;
            source = source.Where(e => EF.Property<string>(e, nameof(IMultiTenant.TenantId)) == tenantId);
        }

        return source;
    }

    /// <summary>
    /// 实体是否对当前调用可见：租户必须匹配，且除非显式包含，已软删除的不可见。
    /// </summary>
    private bool IsVisible(TEntity entity, bool includeDeleted)
        => PersistenceConventions.IsVisibleTo(entity, _unitOfWork.TenantId) && (includeDeleted || !PersistenceConventions.IsDeleted(entity));
}
