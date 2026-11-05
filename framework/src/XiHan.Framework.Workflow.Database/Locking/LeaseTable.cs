// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Database.Entities;
using XiHan.Framework.Workflow.Database.Storage;

namespace XiHan.Framework.Workflow.Database.Locking;

/// <summary>
/// 租约行的三个原子步骤
/// </summary>
/// <remarks>
/// 每一步都在根提供者的新作用域里开自己的可写工作单元、返回即提交，别的节点立刻看得见——与调用方的事务无关：引擎在调用方事务里取锁时，锁行仍走独立的连接与事务，既不等调用方事务里的行锁，也不随它回滚。不经 <c>App.InvokeAsync</c>：锁是平台数据，不受调用租户、管道各阶段与应用是否在服务状态的影响；新作用域里没有调用，也就没有分片，租约表在默认连接（平台库），或 <see cref="DatabaseWorkflowOptions.LockConnection"/> 指定的命名连接——那时工作单元绑定这个连接，事务只开在租约库上，SQLite 上不去等调用方事务占着的业务库写锁。跨进程的原子性来自主键唯一约束（插入）与并发戳（接管、续约）：仓储更新先加行锁读当前行再比对戳，戳不同返回 <see cref="PersistenceErrors.Concurrency"/>
/// </remarks>
internal sealed class LeaseTable
{
    /// <summary>
    /// 根服务提供者：每一步开新作用域
    /// </summary>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 时钟
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 每一步开工作单元的选项：可写，绑定 <see cref="DatabaseWorkflowOptions.LockConnection"/>（未配置为缺省落点）
    /// </summary>
    private readonly UnitOfWorkOptions _unitOfWork;

    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="services">根服务提供者</param>
    /// <param name="clock">时钟</param>
    /// <param name="lockConnection">租约表的命名连接；<see langword="null"/> 为缺省落点</param>
    public LeaseTable(IServiceProvider services, TimeProvider clock, string? lockConnection)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _unitOfWork = new UnitOfWorkOptions(Connection: lockConnection);
    }

    /// <summary>
    /// 尝试一次
    /// </summary>
    /// <remarks>
    /// 行不存在就插入；存在且已过期就接管；否则 <see cref="LeaseAttempt.Held"/>。持久化冲突归为 <see cref="LeaseAttempt.Contended"/>，其余失败原样返回
    /// </remarks>
    public ValueTask<Result<LeaseAttempt>> TryAcquireAsync(string id, string holder, TimeSpan lease, CancellationToken cancellationToken)
        => RunAsync<LeaseAttempt>(async repository =>
        {
            var now = _clock.GetUtcNow();
            var found = await repository.FindAsync(id, cancellationToken).ConfigureAwait(false);
            if (!found.TryGetValue(out var row))
            {
                var added = await repository.AddAsync(new WorkflowLockRecord { Id = id, Holder = holder, ExpiresAt = now + lease }, cancellationToken).ConfigureAwait(false);
                if (added.IsSuccess)
                {
                    return LeaseAttempt.Acquired;
                }

                return string.Equals(added.Error!.Code, PersistenceErrors.Duplicate, StringComparison.Ordinal) ? LeaseAttempt.Contended : added.Error;
            }

            // 未过期的租约属于别人（或还没释放的自己）：不可重入，只能等
            if (row.ExpiresAt > now)
            {
                return LeaseAttempt.Held;
            }

            row.Holder = holder;
            row.ExpiresAt = now + lease;
            var updated = await repository.UpdateAsync(row, cancellationToken).ConfigureAwait(false);
            if (updated.IsSuccess)
            {
                return LeaseAttempt.Acquired;
            }

            return IsRace(updated.Error!) ? LeaseAttempt.Contended : updated.Error!;
        }, cancellationToken);

    /// <summary>
    /// 续约
    /// </summary>
    /// <remarks>
    /// 行仍由 <paramref name="holder"/> 持有时把到期时间推到现在加租约并返回 <see langword="true"/>；行没了或持有者变了返回 <see langword="false"/>
    /// </remarks>
    public ValueTask<Result<bool>> RenewAsync(string id, string holder, TimeSpan lease, CancellationToken cancellationToken)
        => RunAsync<bool>(async repository =>
        {
            var found = await repository.FindAsync(id, cancellationToken).ConfigureAwait(false);
            if (!found.TryGetValue(out var row) || !string.Equals(row.Holder, holder, StringComparison.Ordinal))
            {
                return false;
            }

            row.ExpiresAt = _clock.GetUtcNow() + lease;
            var updated = await repository.UpdateAsync(row, cancellationToken).ConfigureAwait(false);
            if (updated.IsSuccess)
            {
                return true;
            }

            return IsRace(updated.Error!) ? false : updated.Error!;
        }, cancellationToken);

    /// <summary>
    /// 释放
    /// </summary>
    /// <remarks>
    /// 只删自己持有的行；行没了或持有者变了视为已释放
    /// </remarks>
    public async ValueTask<Result> ReleaseAsync(string id, string holder, CancellationToken cancellationToken)
    {
        var released = await RunAsync<bool>(async repository =>
        {
            var found = await repository.FindAsync(id, cancellationToken).ConfigureAwait(false);
            if (!found.TryGetValue(out var row) || !string.Equals(row.Holder, holder, StringComparison.Ordinal))
            {
                return true;
            }

            var removed = await repository.RemoveAsync(row, cancellationToken).ConfigureAwait(false);
            return removed.IsSuccess || IsRace(removed.Error!) ? true : removed.Error!;
        }, cancellationToken).ConfigureAwait(false);
        return released.ToResult();
    }

    /// <summary>
    /// 在新作用域的可写工作单元里执行一步：成功即提交，失败或异常即回滚
    /// </summary>
    /// <remarks>
    /// 异常（连接中断、跨节点同时插入在驱动里抛出的唯一约束冲突）归为存储不可达，由实例锁当作可重试的失败直到等锁超时
    /// </remarks>
    private async ValueTask<Result<T>> RunAsync<T>(Func<IRepository<WorkflowLockRecord, string>, ValueTask<Result<T>>> step, CancellationToken cancellationToken)
    {
        await using var scope = _services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        try
        {
            await unitOfWork.BeginAsync(_unitOfWork, cancellationToken).ConfigureAwait(false);
            var result = await step(scope.ServiceProvider.GetRequiredService<IRepository<WorkflowLockRecord, string>>()).ConfigureAwait(false);
            if (result.IsFailure)
            {
                await unitOfWork.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                return result;
            }

            // 提交失败（写入阶段的标识重复、并发戳冲突）如实返回，由调用方按竞争处理
            var committed = await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            return committed.IsSuccess ? result : Result.Failure<T>(committed.Error!);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // 回滚用 None 令牌保证一定执行；工作单元没开起来时不回滚
            if (unitOfWork.State == UnitOfWorkState.Active)
            {
                await unitOfWork.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            }

            return StorePartition.Unavailable(exception.Message);
        }
        catch
        {
            if (unitOfWork.State == UnitOfWorkState.Active)
            {
                await unitOfWork.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            }

            throw;
        }
    }

    /// <summary>
    /// 是否是与别的节点的竞争：并发戳冲突或行已被删
    /// </summary>
    private static bool IsRace(Error error)
        => error.Code is PersistenceErrors.Concurrency or PersistenceErrors.NotFound;
}
