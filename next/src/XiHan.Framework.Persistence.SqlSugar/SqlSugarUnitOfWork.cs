// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 事务型工作单元：首次写入时经 <see cref="ITenant"/> 在客户端的全部连接上开启数据库事务（只读调用不开事务），提交即逐连接提交，回滚即逐连接回滚。
/// 客户端可以带多个连接（默认连接与租户分片连接），每种实体由 <see cref="IEntityConnectionSelector"/> 决定落在哪个连接；跨连接的提交是顺序提交，不是两阶段提交。
/// 延迟开启让多个只读调用与一个写入调用能在 SQLite 这类单写者数据库上并存。SqlSugar 没有变更跟踪，仓储的每次写入都立即执行。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class SqlSugarUnitOfWork : IUnitOfWork, IAsyncDisposable
{
    /// <summary>
    /// 实体落点规则。
    /// </summary>
    private readonly IEntityConnectionSelector _selector;

    /// <summary>
    /// 是否已在客户端连接上开启事务；为假表示本工作单元尚未写入。
    /// </summary>
    private bool _transactionOpen;

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
    /// 本作用域的 SqlSugar 客户端（根连接；多连接时经 <c>AsTenant()</c> 取其他连接）。
    /// </summary>
    public ISqlSugarClient Client { get; }

    /// <inheritdoc />
    public UnitOfWorkState State { get; private set; } = UnitOfWorkState.NotStarted;

    /// <inheritdoc />
    public string? TenantId { get; private set; }

    /// <inheritdoc />
    public bool IsReadOnly { get; private set; }

    /// <summary>
    /// 取实体类型应使用的连接；由 <see cref="IEntityConnectionSelector"/> 决定。仓储的每个操作都经此取连接。
    /// </summary>
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
    /// 确保数据库事务已开启。仓储在每次写入前调用；只读工作单元不会调用。
    /// </summary>
    public async ValueTask EnsureTransactionAsync(CancellationToken cancellationToken)
    {
        if (State != UnitOfWorkState.Active)
        {
            throw new FrameworkException(PersistenceErrors.NoActiveUnitOfWork, $"The unit of work is in state '{State}' and cannot begin a transaction.");
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
}
