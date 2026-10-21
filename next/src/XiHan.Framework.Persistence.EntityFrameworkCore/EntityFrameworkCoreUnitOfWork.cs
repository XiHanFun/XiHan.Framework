// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.EntityFrameworkCore;

/// <summary>
/// 事务型工作单元：首次写入时开启数据库事务（只读调用不开事务），提交即提交事务，回滚丢弃变更跟踪与事务。
/// 延迟开启让多个只读调用与一个写入调用能在 SQLite 这类单写者数据库上并存。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class EntityFrameworkCoreUnitOfWork(DbContext context) : IUnitOfWork, IAsyncDisposable
{
    private IDbContextTransaction? _transaction;

    /// <summary>
    /// 本作用域的 DbContext。
    /// </summary>
    public DbContext Context { get; } = context ?? throw new ArgumentNullException(nameof(context));

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
            throw new FrameworkException(PersistenceErrors.InvalidUnitOfWorkState, $"工作单元处于 {State}，不能再次开始。");
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
            throw new FrameworkException(PersistenceErrors.NoActiveUnitOfWork, $"工作单元处于 {State}，不能开启事务。");
        }

        _transaction ??= await Context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result> CommitAsync(CancellationToken cancellationToken)
    {
        if (State != UnitOfWorkState.Active)
        {
            throw new FrameworkException(PersistenceErrors.InvalidUnitOfWorkState, $"工作单元处于 {State}，不能提交。");
        }

        if (IsReadOnly && Context.ChangeTracker.HasChanges())
        {
            await RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Validation(PersistenceErrors.InvalidUnitOfWorkState, "只读工作单元不能写入。");
        }

        try
        {
            if (Context.ChangeTracker.HasChanges())
            {
                await EnsureTransactionAsync(cancellationToken).ConfigureAwait(false);
                await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            if (_transaction is not null)
            {
                await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (DbUpdateConcurrencyException exception)
        {
            await RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Conflict(PersistenceErrors.Concurrency, $"提交时发生乐观并发冲突：{exception.Message}");
        }

        State = UnitOfWorkState.Committed;
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }

        Context.ChangeTracker.Clear();
        State = UnitOfWorkState.RolledBack;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync().ConfigureAwait(false);
            _transaction = null;
        }
    }
}
