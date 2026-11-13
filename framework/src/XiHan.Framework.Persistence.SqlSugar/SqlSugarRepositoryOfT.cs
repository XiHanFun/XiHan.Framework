// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Data.Common;
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
/// SqlSugar 仓储
/// </summary>
/// <remarks>
/// 读在活动工作单元内进行，首次写入开启事务，每次写入立即执行，按协议维护审计字段、租户与软删除。实体落在哪个连接由工作单元的 <see cref="IEntityConnectionSelector"/> 决定（平台目录表走默认连接、租户表走分片连接），本仓储的全部操作都在所选连接上执行。批量方法用一次主键 <c>IN</c> 查询取回目标行、逐项校验后一次 <c>InsertableByObject(list)</c> / <c>UpdateableByObject(list)</c> / <c>DeleteableByObject(list)</c>。SqlSugar 没有变更跟踪：更新与删除先在事务内加行锁取回当前行（<c>TranLock</c>，SQLite 由数据库自身的写锁保证），比对并发戳后把调用方的实例整行写入，其中创建时间、租户与删除标记先换成当前行的值。新增撞上主键约束（检查之后别的事务提交了同一标识）时另开一个不在事务里的连接核对标识确已存在，就按标识重复报告；新增、更新与软删除撞上唯一索引（实体声明的 <c>[EntityIndex(IsUnique = true)]</c> 或唯一 <c>[SugarIndex]</c>）时同样另开连接核对，按唯一键重复报告；两种情况工作单元随后都只能回滚（PostgreSQL 系语句失败后事务已中止，不能在原事务里再查）。实体类型没有 <c>new()</c> 约束，所以写入经 SqlSugar 的按对象入口而不是泛型构建器。带 SqlSugar <c>[SplitTable]</c> 的实体自动走分表入口：查询按 <see cref="EntityQuery{T}.TimeRange"/> 限定扫描的表（没有提示就扫全部分表），写入按 <c>[SplitField]</c> 的值落表；分表实体不加行锁，靠并发戳。<see cref="SqlSugarPersistenceOptions.DiffLog"/> 开启的实体，写入（含批量、软删除与分表）带 SqlSugar 差异日志，业务数据为 <see cref="SqlSugarDiffLogData"/>
/// </remarks>
/// <param name="unitOfWork">本作用域的工作单元</param>
/// <param name="timeProvider">审计字段使用的时间源</param>
/// <param name="options">Provider 选项；据此决定本实体是否带差异日志</param>
/// <param name="services">本作用域的服务提供器；随差异日志交给回调</param>
internal sealed class SqlSugarRepository<TEntity, TKey>(SqlSugarUnitOfWork unitOfWork, TimeProvider timeProvider, SqlSugarPersistenceOptions options, IServiceProvider services) : IRepository<TEntity, TKey>
    where TEntity : class, IEntity<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// 实体是否实现 <see cref="ISoftDeletable"/>
    /// </summary>
    /// <remarks>
    /// 按闭合泛型只判断一次，查询据此决定是否追加删除标记条件
    /// </remarks>
    private static readonly bool _isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(typeof(TEntity));

    /// <summary>
    /// 实体是否实现 <see cref="IMultiTenant"/>
    /// </summary>
    /// <remarks>
    /// 按闭合泛型只判断一次，查询据此决定是否追加租户条件
    /// </remarks>
    private static readonly bool _isMultiTenant = typeof(IMultiTenant).IsAssignableFrom(typeof(TEntity));

    /// <summary>
    /// 实体是否带 SqlSugar <see cref="SplitTableAttribute"/>（分表）
    /// </summary>
    /// <remarks>
    /// 按闭合泛型只判断一次，读写据此切换到分表入口
    /// </remarks>
    private static readonly bool _isSplitTable = typeof(TEntity).IsDefined(typeof(SplitTableAttribute), inherit: false);

    /// <summary>
    /// 本实体带差异日志的写入入口
    /// </summary>
    /// <remarks>
    /// 按闭合泛型在首次需要时构造一次
    /// </remarks>
    private static IDiffLoggedWrites? _diffLoggedWrites;

    /// <summary>
    /// 本作用域的工作单元
    /// </summary>
    /// <remarks>
    /// 提供客户端、事务与当前租户
    /// </remarks>
    private readonly SqlSugarUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    /// <summary>
    /// 审计字段使用的时间源
    /// </summary>
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>
    /// 差异日志的业务数据
    /// </summary>
    /// <remarks>
    /// 本实体没有开启差异日志时为 <see langword="null"/>
    /// </remarks>
    private readonly SqlSugarDiffLogData? _diffLog = (options ?? throw new ArgumentNullException(nameof(options))).DiffLog(typeof(TEntity))
        ? new SqlSugarDiffLogData { EntityType = typeof(TEntity), Services = services ?? throw new ArgumentNullException(nameof(services)) }
        : null;

    /// <summary>
    /// 本实体所用的连接
    /// </summary>
    /// <remarks>
    /// 首次使用时由工作单元按落点规则选出，之后复用（同一工作单元内落点稳定）
    /// </remarks>
    private ISqlSugarClient? _connection;

    /// <inheritdoc />
    public async ValueTask<Maybe<TEntity>> FindAsync(TKey id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        var entity = await Split(Visible(includeDeleted: false).Where(EntityExpressions.IdEquals<TEntity, TKey>(id))).FirstAsync(cancellationToken).ConfigureAwait(false);
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
        var rows = await Split(Visible(includeDeleted: false).In(distinct)).ToListAsync(cancellationToken).ConfigureAwait(false);
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
        // 用到了本实现不认识的条件即失败：不读不认识的条件，也不给出放宽了的结果（ADR-0228）
        query.Validate(EntityQuery<TEntity>.Revision).ThrowIfFailure();
        var source = Split(Filtered(query), query.TimeRange);
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
        // 用到了本实现不认识的条件即失败：不读不认识的条件，也不给出放宽了的结果（ADR-0228）
        query.Validate(EntityQuery<TEntity>.Revision).ThrowIfFailure();
        return await Split(Filtered(query), query.TimeRange).CountAsync(cancellationToken).ConfigureAwait(false);
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
        if (await Split(Client.Queryable<TEntity>().Where(EntityExpressions.IdEquals<TEntity, TKey>(entity.Id))).AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return Duplicate(entity.Id);
        }

        var applied = PersistenceConventions.ApplyOnAdd(entity, _unitOfWork.TenantId, _timeProvider.GetUtcNow());
        if (applied.IsFailure)
        {
            return applied;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (await InsertCheckedAsync(entity, [entity]).ConfigureAwait(false) is { } duplicate)
        {
            return duplicate;
        }

        _unitOfWork.DomainEvents.Track(entity);
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
        var taken = (await Split(Client.Queryable<TEntity>().In(Distinct(entities.Select(static e => e.Id)))).ToListAsync(EntityExpressions.Id<TEntity, TKey>()).ConfigureAwait(false)).ToHashSet();
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

        // 通过校验的项一次多行插入；写入阶段才撞上的标识重复或唯一键重复对这些项统一报告
        cancellationToken.ThrowIfCancellationRequested();
        if (await InsertCheckedAsync(pending, pending).ConfigureAwait(false) is { } duplicate)
        {
            foreach (var index in pendingIndexes)
            {
                results[index] = duplicate;
            }

            return results;
        }

        foreach (var index in pendingIndexes)
        {
            results[index] = Result.Success();
            _unitOfWork.DomainEvents.Track(entities[index]);
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

        var existing = await Locked(q => q.Where(EntityExpressions.IdEquals<TEntity, TKey>(entity.Id))).FirstAsync(cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return NotFound(entity.Id);
        }

        if (Stale(existing, entity))
        {
            return StaleStamp(entity.Id);
        }

        // 创建时间、租户与删除标记沿用当前行：调用方实例上的值不写入
        PersistenceConventions.ApplyOnUpdate(entity, existing, _timeProvider.GetUtcNow());
        cancellationToken.ThrowIfCancellationRequested();
        // 行已在本事务内加锁，按主键整行写入；影响 0 行只可能是行在锁定前已被删除，按冲突报告
        var updated = await UpdateCheckedAsync(entity, [entity]).ConfigureAwait(false);
        if (!updated.TryGetValue(out var affected))
        {
            return updated.Error!;
        }

        if (affected == 0)
        {
            return Error.Conflict(PersistenceErrors.Concurrency, PersistenceMessages.ConcurrencyConflict, MessageArguments.Of(("entity", typeof(TEntity).Name), ("reason", "the row changed after it was read")));
        }

        _unitOfWork.DomainEvents.Track(entity);
        return Result.Success();
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

            PersistenceConventions.ApplyOnUpdate(entity, existing, now);
            rows[entity.Id] = entity;
            pending.Add(entity);
            pendingIndexes.Add(index);
        }

        if (pending.Count == 0)
        {
            return results;
        }

        // 通过校验的项一次按主键批量更新；写入阶段撞上的唯一键重复对这些项统一报告
        cancellationToken.ThrowIfCancellationRequested();
        var written = await UpdateCheckedAsync(pending, pending).ConfigureAwait(false);
        foreach (var index in pendingIndexes)
        {
            if (written.IsFailure)
            {
                results[index] = written.Error!;
                continue;
            }

            results[index] = Result.Success();
            _unitOfWork.DomainEvents.Track(entities[index]);
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

        var existing = await Locked(q => q.Where(EntityExpressions.IdEquals<TEntity, TKey>(entity.Id))).FirstAsync(cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return NotFound(entity.Id);
        }

        cancellationToken.ThrowIfCancellationRequested();
        // 软删除在取回的当前行上打标并整行写回，调用方的实例再同步审计字段；不支持软删除的实体按主键真正删除
        if (PersistenceConventions.TrySoftDelete(existing, _timeProvider.GetUtcNow()))
        {
            // 软删除的行带着删除标记进唯一索引：同一业务键已有一行已删除时撞上，按唯一键重复报告
            if ((await UpdateCheckedAsync(existing, [existing]).ConfigureAwait(false)).Error is { } conflict)
            {
                return conflict;
            }

            CopyAuditBack(existing, entity);
        }
        else
        {
            await DeleteAsync(existing).ConfigureAwait(false);
        }

        // 调用方的实例持有领域事件；登记它而不是取回的当前行
        _unitOfWork.DomainEvents.Track(entity);
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
            var marked = tombstones.Select(static t => t.Existing).ToList();
            var written = await UpdateCheckedAsync(marked, marked).ConfigureAwait(false);
            foreach (var (index, existing) in tombstones)
            {
                if (written.IsFailure)
                {
                    results[index] = written.Error!;
                    continue;
                }

                results[index] = Result.Success();
                CopyAuditBack(existing, entities[index]);
                _unitOfWork.DomainEvents.Track(entities[index]);
            }
        }

        if (deletions.Count > 0)
        {
            await DeleteAsync(deletions).ConfigureAwait(false);
            foreach (var index in deletionIndexes)
            {
                results[index] = Result.Success();
                _unitOfWork.DomainEvents.Track(entities[index]);
            }
        }

        return results;
    }

    /// <summary>
    /// 本实体在活动工作单元里的连接
    /// </summary>
    /// <remarks>
    /// 工作单元不活动时抛出 <see cref="PersistenceErrors.NoActiveUnitOfWork"/>，每个操作都经此校验
    /// </remarks>
    private ISqlSugarClient Client => _unitOfWork.State == UnitOfWorkState.Active
        ? _connection ??= _unitOfWork.ConnectionFor(typeof(TEntity))
        : throw new FrameworkException(PersistenceErrors.NoActiveUnitOfWork, $"The unit of work is in state '{_unitOfWork.State}'; repositories must be used within the Transaction stage.");

    /// <summary>
    /// 构造实体不存在的错误
    /// </summary>
    private static Error NotFound(TKey id) => Error.NotFound(PersistenceErrors.NotFound, PersistenceMessages.NotFound, MessageArguments.Of(("entity", typeof(TEntity).Name), ("id", id)));

    /// <summary>
    /// 构造标识重复的错误
    /// </summary>
    private static Error Duplicate(TKey id) => Error.Conflict(PersistenceErrors.Duplicate, PersistenceMessages.Duplicate, MessageArguments.Of(("entity", typeof(TEntity).Name), ("id", id)));

    /// <summary>
    /// 构造唯一键重复的错误
    /// </summary>
    private static Error UniqueKeyConflict(string index) => Error.Conflict(PersistenceErrors.UniqueKeyConflict, PersistenceMessages.UniqueKeyConflict, MessageArguments.Of(("entity", typeof(TEntity).Name), ("index", index)));

    /// <summary>
    /// 构造并发戳过期的错误
    /// </summary>
    private static Error StaleStamp(TKey id) => Error.Conflict(PersistenceErrors.Concurrency, PersistenceMessages.StaleStamp, MessageArguments.Of(("entity", typeof(TEntity).Name), ("id", id)));

    /// <summary>
    /// 调用方实例的并发戳是否与当前行不同
    /// </summary>
    /// <remarks>
    /// 同一实例或不带并发戳的实体不算过期
    /// </remarks>
    private static bool Stale(TEntity existing, TEntity entity)
        => !ReferenceEquals(existing, entity)
            && existing is IHasConcurrencyStamp current && entity is IHasConcurrencyStamp incoming
            && !string.Equals(current.ConcurrencyStamp, incoming.ConcurrencyStamp, StringComparison.Ordinal);

    /// <summary>
    /// 同一结果重复给每一项（写入前置条件不满足时整批同一错误）
    /// </summary>
    private static Result[] Repeat(Result result, int count)
    {
        var results = new Result[count];
        Array.Fill(results, result);
        return results;
    }

    /// <summary>
    /// 去重并保持首次出现的顺序
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
    /// 把当前行上由协议写入的修改时间、并发戳与软删除标记复制到调用方的实例
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
    /// 一次主键 <c>IN</c> 查询在本事务内加锁取回这些实体对应的可见行，按标识索引
    /// </summary>
    private async Task<Dictionary<TKey, TEntity>> LoadLockedAsync(IReadOnlyList<TEntity> entities, CancellationToken cancellationToken)
    {
        var rows = await Locked(q => q.In(Distinct(entities.Select(static e => e.Id)))).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.ToDictionary(static e => e.Id);
    }

    /// <summary>
    /// 另开一个读主库的连接
    /// </summary>
    /// <remarks>
    /// 写入撞上约束后事务已中止，要另开连接查是谁撞了。副本上带着从库，不钉住就读到还没复制到的从库，把冲突判断错
    /// </remarks>
    private SqlSugarClient FreshOnPrimary()
    {
        var fresh = Client.CopyNew();
        fresh.Ado.IsDisableMasterSlaveSeparation = true;
        return fresh;
    }

    /// <summary>
    /// 写入路径的读取
    /// </summary>
    /// <remarks>
    /// 在可见行上施加 <paramref name="filter"/> 后加事务内行锁（<c>FOR UPDATE</c> / <c>UPDLOCK</c>），读取与写入之间别人改不了这些行。SQLite 不支持行锁，由其数据库级写锁保证；分表实体的查询是多表 <c>UNION ALL</c>，不能加行锁，靠并发戳
    /// </remarks>
    private ISugarQueryable<TEntity> Locked(Func<ISugarQueryable<TEntity>, ISugarQueryable<TEntity>> filter)
    {
        var source = Split(filter(Visible(includeDeleted: false)));
        return _isSplitTable || Client.CurrentConnectionConfig.DbType == DbType.Sqlite ? source : source.TranLock(DbLockType.Wait);
    }

    /// <summary>
    /// 分表实体的查询在全部过滤条件之后切到分表入口
    /// </summary>
    /// <remarks>
    /// 有时间范围提示只扫范围内的表（按 UTC 时刻），没有就扫全部分表；不分表的实体原样返回。必须在 <c>Where</c> / <c>In</c> 之后、排序与分页之前调用，SqlSugar 把它之前的条件下推到每张分表再 <c>UNION ALL</c>
    /// </remarks>
    private static ISugarQueryable<TEntity> Split(ISugarQueryable<TEntity> source, TimeRange? range = null)
    {
        if (!_isSplitTable)
        {
            return source;
        }

        return range is null ? source.SplitTable() : source.SplitTable(range.From.UtcDateTime, range.To.UtcDateTime);
    }

    /// <summary>
    /// 插入单个实体或实体列表，写入失败时核对是否为并发新增的同一标识或撞上了唯一索引
    /// </summary>
    /// <remarks>
    /// 新增前已核对过标识，写入仍可能撞上主键约束：检查之后另一个事务插入并提交了同一标识（PostgreSQL 等在插入时等对方事务结束再报错）。PostgreSQL 系语句失败后整个事务进入中止状态，不能在原事务里再查，所以另开一个不在事务里的连接（<c>CopyNew</c>，同一份连接配置）查这些标识：有已存在的就返回标识重复（<c>id</c> 是按入参顺序第一个撞上的标识）；没有就核对唯一索引（同一批里两项同键、已提交的另一行同键，见 <see cref="UniqueKeyConflicts"/>），撞上即返回唯一键重复（<c>index</c> 是第一个撞上的索引）。两种都把工作单元标为失败，提交时回滚并返回同一错误；都不是说明是别的原因，异常原样抛出。成功返回 <see langword="null"/>
    /// </remarks>
    private async Task<Error?> InsertCheckedAsync(object entityOrList, IReadOnlyList<TEntity> written)
    {
        try
        {
            await InsertAsync(entityOrList).ConfigureAwait(false);
            return null;
        }
        catch (DbException)
        {
            var ids = Distinct(written.Select(static e => e.Id));
            Error conflict;
            using (var fresh = FreshOnPrimary())
            {
                var taken = await Split(fresh.Queryable<TEntity>().In(ids)).ToListAsync(EntityExpressions.Id<TEntity, TKey>()).ConfigureAwait(false);
                var collided = ids.FindIndex(taken.Contains);
                if (collided >= 0)
                {
                    conflict = Duplicate(ids[collided]);
                }
                else if (!_isSplitTable && await UniqueKeyConflicts.FindAsync<TEntity, TKey>(Client, fresh, written).ConfigureAwait(false) is { } index)
                {
                    conflict = UniqueKeyConflict(index);
                }
                else
                {
                    throw;
                }
            }

            _unitOfWork.Fail(conflict);
            return conflict;
        }
    }

    /// <summary>
    /// 按主键整行更新单个实体或实体列表，写入失败时核对是否撞上了唯一索引
    /// </summary>
    /// <remarks>
    /// 更新可能把业务键改成别的行已有的值，软删除可能撞上同一业务键已删除的那一行（唯一索引带删除标记）。核对方式与 <see cref="InsertCheckedAsync"/> 相同：另开不在事务里的连接查已提交的行；撞上即返回唯一键重复并把工作单元标为失败，否则异常原样抛出。成功返回影响的行数
    /// </remarks>
    private async Task<Result<int>> UpdateCheckedAsync(object entityOrList, IReadOnlyList<TEntity> written)
    {
        try
        {
            return await UpdateAsync(entityOrList).ConfigureAwait(false);
        }
        catch (DbException)
        {
            string? index;
            using (var fresh = FreshOnPrimary())
            {
                index = _isSplitTable ? null : await UniqueKeyConflicts.FindAsync<TEntity, TKey>(Client, fresh, written).ConfigureAwait(false);
            }

            if (index is null)
            {
                throw;
            }

            var conflict = UniqueKeyConflict(index);
            _unitOfWork.Fail(conflict);
            return conflict;
        }
    }

    /// <summary>
    /// 插入单个实体或实体列表
    /// </summary>
    /// <remarks>
    /// 分表实体按 <c>[SplitField]</c> 的值落表（列表按表分组，缺少的分表由 SqlSugar 建出）
    /// </remarks>
    private Task<int> InsertAsync(object entityOrList)
    {
        if (_diffLog is { } diffLog)
        {
            return DiffLogged.InsertAsync(Client, entityOrList, _isSplitTable, diffLog);
        }

        return _isSplitTable ? Client.InsertableByObject(entityOrList).SplitTable().ExecuteCommandAsync() : Client.InsertableByObject(entityOrList).ExecuteCommandAsync();
    }

    /// <summary>
    /// 按主键整行更新单个实体或实体列表
    /// </summary>
    /// <remarks>
    /// 分表实体按 <c>[SplitField]</c> 的值定位所在的表，所以分表字段写入后不要再改
    /// </remarks>
    private Task<int> UpdateAsync(object entityOrList)
    {
        if (_diffLog is { } diffLog)
        {
            return DiffLogged.UpdateAsync(Client, entityOrList, _isSplitTable, diffLog);
        }

        return _isSplitTable ? Client.UpdateableByObject(entityOrList).SplitTable().ExecuteCommandAsync() : Client.UpdateableByObject(entityOrList).ExecuteCommandAsync();
    }

    /// <summary>
    /// 按主键物理删除单个实体或实体列表
    /// </summary>
    /// <remarks>
    /// 分表实体按 <c>[SplitField]</c> 的值定位所在的表
    /// </remarks>
    private Task<int> DeleteAsync(object entityOrList)
    {
        if (_diffLog is { } diffLog)
        {
            return DiffLogged.DeleteAsync(Client, entityOrList, _isSplitTable, diffLog);
        }

        return _isSplitTable ? Client.DeleteableByObject(entityOrList).SplitTable().ExecuteCommandAsync() : Client.DeleteableByObject(entityOrList).ExecuteCommandAsync();
    }

    /// <summary>
    /// 本实体带差异日志的写入入口
    /// </summary>
    /// <remarks>
    /// 按闭合类型构造 <see cref="DiffLoggedWrites{T}"/>，实体没有公开无参构造函数时抛出（SqlSugar 的按对象入口同样要求）
    /// </remarks>
    private static IDiffLoggedWrites DiffLogged
        => _diffLoggedWrites ??= (IDiffLoggedWrites)Activator.CreateInstance(typeof(DiffLoggedWrites<>).MakeGenericType(typeof(TEntity)))!;

    /// <summary>
    /// 写入前：要求活动工作单元、拒绝只读、确保事务已开启
    /// </summary>
    /// <remarks>
    /// 只读工作单元里返回 <see cref="PersistenceErrors.ReadOnly"/>、不触后端，并把工作单元记为失败：处理器忽略这次失败而返回成功时，提交返回同一错误
    /// </remarks>
    private async ValueTask<Error?> PrepareWriteAsync(CancellationToken cancellationToken)
    {
        // 读取 Client 即完成“必须有活动工作单元”的校验
        _ = Client;
        if (_unitOfWork.IsReadOnly)
        {
            var rejected = Error.Unexpected(PersistenceErrors.ReadOnly, PersistenceMessages.ReadOnly);
            _unitOfWork.Fail(rejected);
            return rejected;
        }

        await _unitOfWork.EnsureTransactionAsync(cancellationToken).ConfigureAwait(false);
        return null;
    }

    /// <summary>
    /// 在可见性过滤之上追加查询自身的过滤规约
    /// </summary>
    /// <remarks>
    /// 规约先经 <see cref="NullableComparisonRewriter"/>，可空值类型与 null 的比较按 <c>IS NULL</c> 语义翻译
    /// </remarks>
    private ISugarQueryable<TEntity> Filtered(EntityQuery<TEntity> query)
    {
        var source = Visible(query.IncludeDeleted);
        return query.Filter is null ? source : source.Where(NullableComparisonRewriter.Rewrite(query.Filter.Criteria));
    }

    /// <summary>
    /// 软删除与租户过滤显式施加在查询上，与写入路径对取回行的判定一致
    /// </summary>
    /// <remarks>
    /// 无租户（null）的调用匹配不到任何多租户实体
    /// </remarks>
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
