// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 事务型工作单元：首次写入时在客户端连接上开启数据库事务（只读调用不开事务），提交即提交事务，回滚即回滚事务。
/// 延迟开启让多个只读调用与一个写入调用能在 SQLite 这类单写者数据库上并存。SqlSugar 没有变更跟踪，仓储的每次写入都立即执行。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class SqlSugarUnitOfWork(ISqlSugarClient client) : IUnitOfWork, IAsyncDisposable
{
    /// <summary>
    /// 是否已在客户端连接上开启事务；为假表示本工作单元尚未写入。
    /// </summary>
    private bool _transactionOpen;

    /// <summary>
    /// 本作用域的 SqlSugar 客户端。
    /// </summary>
    public ISqlSugarClient Client { get; } = client ?? throw new ArgumentNullException(nameof(client));

    /// <inheritdoc />
    public UnitOfWorkState State { get; private set; } = UnitOfWorkState.NotStarted;

    /// <inheritdoc />
    public string? TenantId { get; private set; }

    /// <inheritdoc />
    public bool IsReadOnly { get; private set; }

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

        // 只开一次：第一次写入开启事务，之后的写入复用同一事务；SqlSugar 的事务 API 不接受取消令牌，进入前先检查取消
        cancellationToken.ThrowIfCancellationRequested();
        if (!_transactionOpen)
        {
            await Client.Ado.BeginTranAsync().ConfigureAwait(false);
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

        // 没有事务说明本工作单元只读过数据，无需提交
        if (_transactionOpen)
        {
            await Client.Ado.CommitTranAsync().ConfigureAwait(false);
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
            await Client.Ado.RollbackTranAsync().ConfigureAwait(false);
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
            await Client.Ado.RollbackTranAsync().ConfigureAwait(false);
            _transactionOpen = false;
        }
    }
}
