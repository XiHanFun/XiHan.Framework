// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.EntityFrameworkCore;

/// <summary>
/// 事务型工作单元。
/// </summary>
/// <remarks>
/// 首次写入时开启数据库事务（只读调用不开事务），提交即提交事务，回滚丢弃变更跟踪与事务。延迟开启让多个只读调用与一个写入调用能在 SQLite 这类单写者数据库上并存。仓储在写入阶段发现并发新增撞上同一标识后把工作单元标为失败，此后提交只回滚并返回那个错误。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class EntityFrameworkCoreUnitOfWork(DbContext context) : IUnitOfWork, IAsyncDisposable
{
    /// <summary>
    /// 延迟开启的数据库事务。
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 表示本工作单元尚未写入。
    /// </remarks>
    private IDbContextTransaction? _transaction;

    /// <summary>
    /// 写入阶段记下的失败。
    /// </summary>
    /// <remarks>
    /// 非空时提交只回滚并返回它。
    /// </remarks>
    private Error? _failure;

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
    public DomainEventTracker DomainEvents { get; } = new();

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
    /// 仓储在每次写入前调用；只读工作单元不会调用。
    /// </remarks>
    public async ValueTask EnsureTransactionAsync(CancellationToken cancellationToken)
    {
        if (State != UnitOfWorkState.Active)
        {
            throw new FrameworkException(PersistenceErrors.NoActiveUnitOfWork, $"The unit of work is in state '{State}' and cannot begin a transaction.");
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

        // 写入阶段已失败（并发新增撞上同一标识）：调用方忽略了那次失败也不能提交
        if (_failure is { } failure)
        {
            await RollbackAsync(cancellationToken).ConfigureAwait(false);
            return failure;
        }

        // 只读工作单元里仓储已在写入前拒绝；这里兜住绕过仓储直接改跟踪实体的情况
        if (IsReadOnly && Context.ChangeTracker.HasChanges())
        {
            await RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Validation(PersistenceErrors.InvalidUnitOfWorkState, PersistenceMessages.ReadOnly);
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

            // 没有事务说明本工作单元只读过数据，无需提交
            if (_transaction is not null)
            {
                await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
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
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }

        // 清空跟踪器：已保存但被回滚的实体不能再以"未修改"状态留在上下文里
        Context.ChangeTracker.Clear();
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
    /// 标为失败。
    /// </summary>
    /// <remarks>
    /// 仓储在写入阶段发现并发新增撞上同一标识时调用；只记第一次。
    /// </remarks>
    internal void Fail(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        _failure ??= error;
    }

    /// <summary>
    /// 残余变更里的创建时间与租户回到原值。
    /// </summary>
    /// <remarks>
    /// 这两个字段只在新增时写入：绕过仓储直接改跟踪实体也不能把行改到别的租户或改掉创建时间。
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
