// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 事务型工作单元。
/// </summary>
/// <remarks>
/// 首次写入时经 <see cref="ITenant"/> 在客户端的全部连接上开启数据库事务（只读调用不开事务），提交即逐连接提交，回滚即逐连接回滚。客户端可以带多个连接（默认连接与租户分片连接），每种实体由 <see cref="IEntityConnectionSelector"/> 决定落在哪个连接；跨连接的提交是顺序提交，不是两阶段提交。延迟开启让多个只读调用与一个写入调用能在 SQLite 这类单写者数据库上并存。SqlSugar 没有变更跟踪，仓储的每次写入都立即执行。仓储在写入阶段发现并发新增撞上同一标识后把工作单元标为失败（PostgreSQL 系此时事务已中止），此后提交只回滚并返回那个错误，不会提交一个已中止的事务。只读工作单元从不开事务：仓储写入返回 <see cref="PersistenceErrors.ReadOnly"/> 并同样把工作单元标为失败，<see cref="EnsureTransactionAsync"/> 抛出携带它的异常。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class SqlSugarUnitOfWork : IUnitOfWork, IAsyncDisposable
{
    /// <summary>
    /// 实体落点规则。
    /// </summary>
    private readonly IEntityConnectionSelector _selector;

    /// <summary>
    /// 是否已在客户端连接上开启事务。
    /// </summary>
    /// <remarks>
    /// 为假表示本工作单元尚未写入。
    /// </remarks>
    private bool _transactionOpen;

    /// <summary>
    /// 写入阶段记下的失败。
    /// </summary>
    /// <remarks>
    /// 非空时提交只回滚并返回它。
    /// </remarks>
    private Error? _failure;

    /// <summary>
    /// 创建。
    /// </summary>
    /// <param name="client">本作用域的客户端。</param>
    /// <param name="selector">实体落点规则。</param>
    public SqlSugarUnitOfWork(ISqlSugarClient client, IEntityConnectionSelector selector)
    {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
    }

    /// <summary>
    /// 本作用域的 SqlSugar 客户端。
    /// </summary>
    /// <remarks>
    /// 根连接；多连接时经 <c>AsTenant()</c> 取其他连接。
    /// </remarks>
    public ISqlSugarClient Client { get; }

    /// <inheritdoc />
    public UnitOfWorkState State { get; private set; } = UnitOfWorkState.NotStarted;

    /// <inheritdoc />
    public string? TenantId { get; private set; }

    /// <inheritdoc />
    public bool IsReadOnly { get; private set; }

    /// <inheritdoc />
    public DomainEventTracker DomainEvents { get; } = new();

    /// <summary>
    /// 取实体类型应使用的连接。
    /// </summary>
    /// <remarks>
    /// 由 <see cref="IEntityConnectionSelector"/> 决定。仓储的每个操作都经此取连接。
    /// </remarks>
    public ISqlSugarClient ConnectionFor(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return _selector.ConnectionFor(entityType, Client);
    }

    /// <inheritdoc />
    public Task BeginAsync(UnitOfWorkOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (State != UnitOfWorkState.NotStarted)
        {
            throw new FrameworkException(PersistenceErrors.InvalidUnitOfWorkState, $"The unit of work is in state '{State}' and cannot be started again.");
        }

        TenantId = options.TenantId;
        IsReadOnly = options.IsReadOnly;
        State = UnitOfWorkState.Active;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 确保数据库事务已开启。
    /// </summary>
    /// <remarks>
    /// 写入的声明：仓储在每次写入前调用，直接用 <see cref="Client"/> 或 <see cref="ConnectionFor"/> 写入（<c>Insertable</c>、<c>Updateable</c>、<c>Deleteable</c> 等）之前也要先调用它，写入才在本工作单元的事务里。只读工作单元里抛出携带 <see cref="PersistenceErrors.ReadOnly"/> 的 <see cref="FrameworkException"/> 并把工作单元记为失败，写入不会执行；Transaction 阶段把它转为同码结果。
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
            Fail(Error.Denied(PersistenceErrors.ReadOnly, PersistenceMessages.ReadOnly));
            throw new FrameworkException(PersistenceErrors.ReadOnly, "The unit of work is read-only and does not accept writes.");
        }

        // 只开一次：第一次写入经 ITenant 在已打开的连接上开启事务并置"全连接事务"标记，之后首次触及的连接由 SqlSugar 在取连接时补开事务；
        // 只有一个连接时与 Ado.BeginTran 等价，不另留单连接路径。SqlSugar 的事务 API 不接受取消令牌，进入前先检查取消
        cancellationToken.ThrowIfCancellationRequested();
        if (!_transactionOpen)
        {
            await Client.AsTenant().BeginTranAsync().ConfigureAwait(false);
            _transactionOpen = true;
        }
    }

    /// <inheritdoc />
    public async Task<Result> CommitAsync(CancellationToken cancellationToken)
    {
        if (State != UnitOfWorkState.Active)
        {
            throw new FrameworkException(PersistenceErrors.InvalidUnitOfWorkState, $"The unit of work is in state '{State}' and cannot be committed.");
        }

        // 写入阶段已失败（并发新增撞上同一标识）：调用方忽略了那次失败也不能提交；PostgreSQL 对已中止的事务执行 COMMIT 会静默回滚，提交成功是假象
        if (_failure is { } failure)
        {
            await RollbackAsync(cancellationToken).ConfigureAwait(false);
            return failure;
        }

        // 没有事务说明本工作单元只读过数据，无需提交；多连接按顺序逐个提交，后一个失败时前一个已经提交（顺序提交，不是两阶段提交）
        if (_transactionOpen)
        {
            await Client.AsTenant().CommitTranAsync().ConfigureAwait(false);
            _transactionOpen = false;
        }

        State = UnitOfWorkState.Committed;
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (_transactionOpen)
        {
            await Client.AsTenant().RollbackTranAsync().ConfigureAwait(false);
            _transactionOpen = false;
        }

        State = UnitOfWorkState.RolledBack;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // 作用域结束时若事务仍未提交（例如中间件之外的直接使用），释放即回滚
        if (_transactionOpen)
        {
            await Client.AsTenant().RollbackTranAsync().ConfigureAwait(false);
            _transactionOpen = false;
        }
    }

    /// <summary>
    /// 标为失败。
    /// </summary>
    /// <remarks>
    /// 仓储在写入阶段发现并发新增撞上同一标识、只读工作单元拒绝写入时调用；只记第一次。
    /// </remarks>
    internal void Fail(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        _failure ??= error;
    }
}
