// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Database.Entities;
using XiHan.Framework.Workflow.Database.Storage;

namespace XiHan.Framework.Workflow.Database.Locking;

/// <summary>
/// 租约行的三个原子步骤，每一步都是一次独立的管道调用（返回即提交，别的节点立刻看得见）：获取、续约、释放。
/// 跨进程的原子性来自主键唯一约束（插入）与并发戳（接管、续约）：仓储更新先加行锁读当前行再比对戳，戳不同返回 <see cref="PersistenceErrors.Concurrency"/>。
/// </summary>
internal sealed class LeaseTable
{
    /// <summary>
    /// 管道执行。
    /// </summary>
    private readonly StoreInvocation _invocation;

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 创建。
    /// </summary>
    public LeaseTable(IServiceProvider services, App app, TimeProvider clock)
    {
        _invocation = new StoreInvocation(services, app);
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// 尝试一次：行不存在就插入；存在且已过期就接管；否则 <see cref="LeaseAttempt.Held"/>。持久化冲突归为 <see cref="LeaseAttempt.Contended"/>，其余失败原样返回。
    /// </summary>
    public ValueTask<Result<LeaseAttempt>> TryAcquireAsync(string id, string holder, TimeSpan lease, CancellationToken cancellationToken)
        => _invocation.RunAsync<LeaseAttempt>(async ctx =>
        {
            var repository = StoreInvocation.Repository<WorkflowLockRecord>(ctx);
            var now = _clock.GetUtcNow();
            var found = await repository.FindAsync(id, ctx.CancellationToken).ConfigureAwait(false);
            if (!found.TryGetValue(out var row))
            {
                var added = await repository.AddAsync(new WorkflowLockRecord { Id = id, Holder = holder, ExpiresAt = now + lease }, ctx.CancellationToken).ConfigureAwait(false);
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
            var updated = await repository.UpdateAsync(row, ctx.CancellationToken).ConfigureAwait(false);
            if (updated.IsSuccess)
            {
                return LeaseAttempt.Acquired;
            }

            return IsRace(updated.Error!) ? LeaseAttempt.Contended : updated.Error!;
        }, cancellationToken);

    /// <summary>
    /// 续约：行仍由 <paramref name="holder"/> 持有时把到期时间推到现在加租约并返回 <see langword="true"/>；行没了或持有者变了返回 <see langword="false"/>。
    /// </summary>
    public ValueTask<Result<bool>> RenewAsync(string id, string holder, TimeSpan lease, CancellationToken cancellationToken)
        => _invocation.RunAsync<bool>(async ctx =>
        {
            var repository = StoreInvocation.Repository<WorkflowLockRecord>(ctx);
            var found = await repository.FindAsync(id, ctx.CancellationToken).ConfigureAwait(false);
            if (!found.TryGetValue(out var row) || !string.Equals(row.Holder, holder, StringComparison.Ordinal))
            {
                return false;
            }

            row.ExpiresAt = _clock.GetUtcNow() + lease;
            var updated = await repository.UpdateAsync(row, ctx.CancellationToken).ConfigureAwait(false);
            if (updated.IsSuccess)
            {
                return true;
            }

            return IsRace(updated.Error!) ? false : updated.Error!;
        }, cancellationToken);

    /// <summary>
    /// 释放：只删自己持有的行；行没了或持有者变了视为已释放。
    /// </summary>
    public ValueTask<Result> ReleaseAsync(string id, string holder, CancellationToken cancellationToken)
        => _invocation.RunAsync(async ctx =>
        {
            var repository = StoreInvocation.Repository<WorkflowLockRecord>(ctx);
            var found = await repository.FindAsync(id, ctx.CancellationToken).ConfigureAwait(false);
            if (!found.TryGetValue(out var row) || !string.Equals(row.Holder, holder, StringComparison.Ordinal))
            {
                return Result.Success();
            }

            var removed = await repository.RemoveAsync(row, ctx.CancellationToken).ConfigureAwait(false);
            return removed.IsSuccess || IsRace(removed.Error!) ? Result.Success() : removed;
        }, cancellationToken);

    /// <summary>
    /// 是否是与别的节点的竞争：并发戳冲突或行已被删。
    /// </summary>
    private static bool IsRace(Error error)
        => error.Code is PersistenceErrors.Concurrency or PersistenceErrors.NotFound;
}
