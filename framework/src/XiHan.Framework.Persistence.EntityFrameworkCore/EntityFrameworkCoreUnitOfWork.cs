// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.EntityFrameworkCore;

/// <summary>
/// 事务型工作单元
/// </summary>
/// <remarks>
/// 首次写入时开启数据库事务（只读调用不开事务；开没开见 <see cref="HasTransaction"/>），提交即提交事务，回滚丢弃变更跟踪与事务。延迟开启让多个只读调用与一个写入调用能在 SQLite 这类单写者数据库上并存。仓储在写入阶段发现并发新增撞上同一标识、或在只读工作单元里拒绝写入后把工作单元标为失败，此后提交只回滚并返回那个错误。只读工作单元从不开事务：仓储写入返回 <see cref="PersistenceErrors.ReadOnly"/>，<see cref="EnsureTransactionAsync"/> 抛出携带它的异常，提交时有未保存的跟踪变更也以它回滚。以 <see cref="UnitOfWorkOptions.Connection"/> 绑定命名连接（<see cref="EntityFrameworkCorePersistenceOptions.Connections"/> 里声明的）时，本作用域的 DbContext 建在那个连接上：实体必须登记在它上面，事务只开在它上面，默认连接不被触及（RFC-0005「命名连接」）。一个 DbContext 一个连接，所以登记到命名连接的实体只能在绑定那个连接的工作单元里读写。DbContext 建在租户分片上而那个分片库还没有核对通过时，开始之前先核对 SQLite 的时间列，不符即抛出携带 <see cref="PersistenceErrors.StorageFormatMismatch"/> 的 <see cref="FrameworkException"/>、工作单元保持未开始（RFC-0005「EF Core 在 SQLite 上的时间列核对」，ADR-0097）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class EntityFrameworkCoreUnitOfWork : IUnitOfWork, IAsyncDisposable
{
    /// <summary>
    /// 取本作用域的 DbContext
    /// </summary>
    /// <remarks>
    /// 由作用域容器解析；建 DbContext 选项的回调按 <see cref="_connection"/> 选连接
    /// </remarks>
    private readonly Func<DbContext> _resolve;

    /// <summary>
    /// 本作用域的 DbContext 建在哪个连接上
    /// </summary>
    private readonly ContextConnection _connection;

    /// <summary>
    /// 声明的命名连接：名字 → 连接串
    /// </summary>
    private readonly IReadOnlyDictionary<string, string> _connections;

    /// <summary>
    /// 登记的实体与它们的落点
    /// </summary>
    private readonly RepositoryRegistrations _registrations;

    /// <summary>
    /// 租户分片库的时间列核对
    /// </summary>
    private readonly ShardTimestampChecks _shards;

    /// <summary>
    /// 本作用域的 DbContext
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 表示尚未建出
    /// </remarks>
    private DbContext? _context;

    /// <summary>
    /// 绑定的命名连接
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 表示没有绑定，实体落缺省连接
    /// </remarks>
    private string? _bound;

    /// <summary>
    /// 延迟开启的数据库事务
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 表示本工作单元尚未写入
    /// </remarks>
    private IDbContextTransaction? _transaction;

    /// <summary>
    /// 写入阶段记下的失败
    /// </summary>
    /// <remarks>
    /// 非空时提交只回滚并返回它
    /// </remarks>
    private Error? _failure;

    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="context">取本作用域的 DbContext；工作单元开始时才调用，那时已知道要不要绑定命名连接</param>
    /// <param name="connection">本作用域的 DbContext 建在哪个连接上</param>
    /// <param name="options">Provider 选项，取声明的命名连接</param>
    /// <param name="registrations">登记的实体与它们的落点</param>
    /// <param name="shards">租户分片库的时间列核对（单例）</param>
    internal EntityFrameworkCoreUnitOfWork(Func<DbContext> context, ContextConnection connection, EntityFrameworkCorePersistenceOptions options, RepositoryRegistrations registrations, ShardTimestampChecks shards)
    {
        ArgumentNullException.ThrowIfNull(options);
        _resolve = context ?? throw new ArgumentNullException(nameof(context));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _connections = options.Connections;
        _registrations = registrations ?? throw new ArgumentNullException(nameof(registrations));
        _shards = shards ?? throw new ArgumentNullException(nameof(shards));
    }

    /// <summary>
    /// 本作用域的 DbContext
    /// </summary>
    /// <remarks>
    /// 工作单元开始时建出：绑定了命名连接就建在那个连接上，否则建在缺省连接（带租户分片的调用是分片连接）。开始之前取用即按缺省建出，之后就不能再绑定命名连接
    /// </remarks>
    public DbContext Context => _context ??= _resolve();

    /// <inheritdoc />
    public UnitOfWorkState State { get; private set; } = UnitOfWorkState.NotStarted;

    /// <inheritdoc />
    public string? TenantId { get; private set; }

    /// <inheritdoc />
    public bool IsReadOnly { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// 第一次 <see cref="EnsureTransactionAsync"/>（仓储的每次写入、提交时保存残余变更都先经它）在 DbContext 上开启事务时为真，提交或回滚了事务即复位
    /// </remarks>
    public bool HasTransaction => _transaction is not null;

    /// <inheritdoc />
    public DomainEventTracker DomainEvents { get; } = new();

    /// <inheritdoc />
    public Task BeginAsync(UnitOfWorkOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (State != UnitOfWorkState.NotStarted)
        {
            throw new FrameworkException(PersistenceErrors.InvalidUnitOfWorkState, $"The unit of work is in state '{State}' and cannot be started again.");
        }

        if (options.Connection is { } connection)
        {
            // 先确认连接已声明再开始：没声明是装配错误，不退回缺省连接
            if (!_connections.ContainsKey(connection))
            {
                throw new FrameworkException(PersistenceErrors.UnknownConnection, $"The unit of work is bound to the connection '{connection}', but '{EntityFrameworkCorePersistenceOptions.ConfigurationSection}:Connections' does not declare it.");
            }

            // 一个 DbContext 一个连接：要在建出之前记下绑定，选项回调才会按这个连接建；本作用域已经按别的连接建出时拒绝
            _connection.Bind(connection);
        }

        // 开始即建出 DbContext，与此前在构造时建出一样落在 Transaction 阶段（带分片的调用在这时按租户分片建）；建不出时工作单元保持未开始
        _ = Context;
        // 建在租户分片上：分片库启动时枚举不到，在首次绑定时核对时间列，不符即抛出、工作单元保持未开始
        // 命名连接的绑定优先于分片，DbContext 建在命名连接上时没有分片要核对
        if (_connection.Shard is { } shard)
        {
            return BeginOnShardAsync(options, shard, cancellationToken);
        }

        Activate(options);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 核对 DbContext 所在的租户分片库之后开始
    /// </summary>
    /// <remarks>
    /// 范围与默认连接相同：没有登记到命名连接的实体。核对通过的分片只查一次（<see cref="ShardTimestampChecks"/>）
    /// </remarks>
    private async Task BeginOnShardAsync(UnitOfWorkOptions options, BindingValue shard, CancellationToken cancellationToken)
    {
        await _shards.VerifyAsync(Context, shard, type => _registrations.ConnectionOf(type) is null, cancellationToken).ConfigureAwait(false);
        Activate(options);
    }

    /// <summary>
    /// 记下选项，进入活动状态
    /// </summary>
    private void Activate(UnitOfWorkOptions options)
    {
        _bound = options.Connection;
        TenantId = options.TenantId;
        IsReadOnly = options.IsReadOnly;
        State = UnitOfWorkState.Active;
    }

    /// <summary>
    /// 确保数据库事务已开启
    /// </summary>
    /// <remarks>
    /// 写入的声明：仓储在每次写入前调用，直接用 <see cref="Context"/> 写入（<c>SaveChanges</c>、<c>ExecuteUpdate</c> 等）之前也要先调用它，写入才在本工作单元的事务里。只读工作单元里抛出携带 <see cref="PersistenceErrors.ReadOnly"/> 的 <see cref="FrameworkException"/> 并把工作单元记为失败，写入不会执行；Transaction 阶段把它转为同码结果
    /// </remarks>
    public async ValueTask EnsureTransactionAsync(CancellationToken cancellationToken)
    {
        if (State != UnitOfWorkState.Active)
        {
            throw new FrameworkException(PersistenceErrors.NoActiveUnitOfWork, $"The unit of work is in state '{State}' and cannot begin a transaction.");
        }

        // 只读工作单元不开事务：直接写入的调用方拿到异常就不会执行写入；同时记下失败，捕获异常后仍去提交的调用方得到同一错误
        if (IsReadOnly)
        {
            Fail(Error.Unexpected(PersistenceErrors.ReadOnly, PersistenceMessages.ReadOnly));
            throw new FrameworkException(PersistenceErrors.ReadOnly, "The unit of work is read-only and does not accept writes.");
        }

        // 只开一次：第一次写入开启事务，之后的写入复用同一事务
        _transaction ??= await Context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result> CommitAsync(CancellationToken cancellationToken)
    {
        if (State != UnitOfWorkState.Active)
        {
            throw new FrameworkException(PersistenceErrors.InvalidUnitOfWorkState, $"The unit of work is in state '{State}' and cannot be committed.");
        }

        // 写入阶段已失败（并发新增撞上同一标识、只读工作单元拒绝了写入）：调用方忽略了那次失败也不能提交
        if (_failure is { } failure)
        {
            await RollbackAsync(cancellationToken).ConfigureAwait(false);
            return failure;
        }

        // 只读工作单元里仓储已在写入前拒绝；这里兜住绕过仓储直接改跟踪实体的情况：那是一次没有保存的写入，拒绝而不是丢弃
        if (IsReadOnly && Context.ChangeTracker.HasChanges())
        {
            await RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Unexpected(PersistenceErrors.ReadOnly, PersistenceMessages.ReadOnly);
        }

        try
        {
            // 仓储每次写入都已保存；这里只处理调用方直接修改跟踪实体而未经仓储的残余变更，并保证它们落在事务内
            if (Context.ChangeTracker.HasChanges())
            {
                KeepStored(Context.ChangeTracker);
                await EnsureTransactionAsync(cancellationToken).ConfigureAwait(false);
                await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            // 没有事务说明本工作单元只读过数据，无需提交；提交成功即释放并摘下事务，HasTransaction 随之为假
            if (_transaction is { } transaction)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                _transaction = null;
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (DbUpdateConcurrencyException exception)
        {
            await RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Conflict(PersistenceErrors.Concurrency, PersistenceMessages.CommitConflict, MessageArguments.Of(("reason", exception.Message)));
        }

        State = UnitOfWorkState.Committed;
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        // 回滚成功即释放并摘下事务，HasTransaction 随之为假；回滚抛出时事务留给作用域释放时处理
        if (_transaction is { } transaction)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            _transaction = null;
            await transaction.DisposeAsync().ConfigureAwait(false);
        }

        // 清空跟踪器：已保存但被回滚的实体不能再以"未修改"状态留在上下文里；还没建出 DbContext 就没有可清的
        _context?.ChangeTracker.Clear();
        State = UnitOfWorkState.RolledBack;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // 作用域结束时若事务仍未提交（例如中间件之外的直接使用），释放即回滚
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync().ConfigureAwait(false);
            _transaction = null;
        }
    }

    /// <summary>
    /// 仓储取实体所在的 DbContext
    /// </summary>
    /// <remarks>
    /// 实体登记的落点要与本工作单元的连接一致：绑定了命名连接而实体落在别处，抛出携带 <see cref="PersistenceErrors.UnknownConnection"/> 的 <see cref="FrameworkException"/>；没有绑定而实体登记在命名连接上，抛出携带 <see cref="PersistenceErrors.NotSupported"/> 的 <see cref="FrameworkException"/>
    /// </remarks>
    internal DbContext ContextFor(Type entityType)
    {
        var placed = _registrations.ConnectionOf(entityType);
        if (string.Equals(placed, _bound, StringComparison.Ordinal))
        {
            return Context;
        }

        // 绑定连接的工作单元只认这一个连接：实体落在别处时读写会碰到不在事务里、也不该碰的库，按落点错误拒绝
        if (_bound is not null)
        {
            throw new FrameworkException(PersistenceErrors.UnknownConnection, $"Entity '{entityType.FullName}' is placed on {(placed is null ? "the default connection" : $"the connection '{placed}'")}, but the unit of work is bound to the connection '{_bound}'.");
        }

        // 普通工作单元的 DbContext 建在缺省连接上，命名连接上的实体在这里读写不到它的库：不静默落到缺省连接
        throw new FrameworkException(PersistenceErrors.NotSupported, $"Entity '{entityType.FullName}' is placed on the named connection '{placed}'; the EF Core persistence provider reads and writes it only in a unit of work bound to that connection (one DbContext has one connection).");
    }

    /// <summary>
    /// 标为失败
    /// </summary>
    /// <remarks>
    /// 仓储在写入阶段发现并发新增撞上同一标识、只读工作单元拒绝写入时调用；只记第一次
    /// </remarks>
    internal void Fail(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        _failure ??= error;
    }

    /// <summary>
    /// 残余变更里的创建时间与租户回到原值
    /// </summary>
    /// <remarks>
    /// 这两个字段只在新增时写入：绕过仓储直接改跟踪实体也不能把行改到别的租户或改掉创建时间
    /// </remarks>
    private static void KeepStored(ChangeTracker tracker)
    {
        foreach (var entry in tracker.Entries())
        {
            if (entry.State != EntityState.Modified)
            {
                continue;
            }

            if (entry.Entity is IHasCreationTime)
            {
                Restore(entry, nameof(IHasCreationTime.CreatedAt));
            }

            if (entry.Entity is IMultiTenant)
            {
                Restore(entry, nameof(IMultiTenant.TenantId));
            }
        }

        // 按属性名访问映射的列，与仓储一致
        static void Restore(EntityEntry entry, string name)
        {
            var property = entry.Property(name);
            property.CurrentValue = property.OriginalValue;
        }
    }
}
