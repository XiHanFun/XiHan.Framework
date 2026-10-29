// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Database.Entities;
using XiHan.Framework.Scheduling.Database.Storage;

namespace XiHan.Framework.Scheduling.Database.Leadership;

/// <summary>
/// 租约行的三个原子步骤。
/// </summary>
/// <remarks>
/// 每一步都是一次独立的管道调用（返回即提交，别的节点立刻看得见）。获取、续约、释放。跨进程的原子性来自主键唯一约束（插入）与并发戳（接管、续约）：仓储更新先加行锁读当前行再比对戳，戳不同返回冲突。
/// </remarks>
internal sealed class LeaseRows
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
    public LeaseRows(IServiceProvider services, App app, TimeProvider clock)
    {
        _invocation = new StoreInvocation(services, app);
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// 尝试获取。
    /// </summary>
    /// <remarks>
    /// 行不存在就插入；存在且已过期就接管；拿到返回到期时刻，被占用（含插入或接管时撞上别的节点）返回空值；其余失败原样返回。
    /// </remarks>
    public ValueTask<Result<DateTimeOffset?>> TryAcquireAsync(string resource, string holder, string token, TimeSpan timeToLive, CancellationToken cancellationToken)
        => _invocation.RunAsync<DateTimeOffset?>(async ctx =>
        {
            var repository = StoreInvocation.Repository<SchedulerLeaseEntity>(ctx);
            var now = _clock.GetUtcNow();
            var found = await repository.FindAsync(resource, ctx.CancellationToken).ConfigureAwait(false);
            if (!found.TryGetValue(out var row))
            {
                var expires = now + timeToLive;
                var added = await repository.AddAsync(new SchedulerLeaseEntity { Id = resource, Holder = holder, Token = token, ExpiresAt = expires }, ctx.CancellationToken).ConfigureAwait(false);
                return added.IsSuccess ? Result.Success<DateTimeOffset?>(expires) : Contended(added.Error!);
            }

            // 未过期的租约属于别人（或还没释放的自己）：不可重入，只能等
            if (row.ExpiresAt > now)
            {
                return Result.Success<DateTimeOffset?>(null);
            }

            row.Holder = holder;
            row.Token = token;
            row.ExpiresAt = now + timeToLive;
            var updated = await repository.UpdateAsync(row, ctx.CancellationToken).ConfigureAwait(false);
            return updated.IsSuccess ? Result.Success<DateTimeOffset?>(row.ExpiresAt) : Contended(updated.Error!);
        }, cancellationToken);

    /// <summary>
    /// 续约。
    /// </summary>
    /// <remarks>
    /// 行仍带着 <paramref name="token"/> 时把到期时刻推到现在加存活时间并返回新的到期时刻；行没了、令牌变了或撞上别的节点返回空值。
    /// </remarks>
    public ValueTask<Result<DateTimeOffset?>> RenewAsync(string resource, string token, TimeSpan timeToLive, CancellationToken cancellationToken)
        => _invocation.RunAsync<DateTimeOffset?>(async ctx =>
        {
            var repository = StoreInvocation.Repository<SchedulerLeaseEntity>(ctx);
            var found = await repository.FindAsync(resource, ctx.CancellationToken).ConfigureAwait(false);
            if (!found.TryGetValue(out var row) || !string.Equals(row.Token, token, StringComparison.Ordinal))
            {
                return Result.Success<DateTimeOffset?>(null);
            }

            row.ExpiresAt = _clock.GetUtcNow() + timeToLive;
            var updated = await repository.UpdateAsync(row, ctx.CancellationToken).ConfigureAwait(false);
            return updated.IsSuccess ? Result.Success<DateTimeOffset?>(row.ExpiresAt) : Contended(updated.Error!);
        }, cancellationToken);

    /// <summary>
    /// 释放。
    /// </summary>
    /// <remarks>
    /// 只删带着 <paramref name="token"/> 的行；行没了、令牌变了或撞上别的节点都视为已释放。
    /// </remarks>
    public ValueTask<Result> ReleaseAsync(string resource, string token, CancellationToken cancellationToken)
        => _invocation.RunAsync(async ctx =>
        {
            var repository = StoreInvocation.Repository<SchedulerLeaseEntity>(ctx);
            var found = await repository.FindAsync(resource, ctx.CancellationToken).ConfigureAwait(false);
            if (!found.TryGetValue(out var row) || !string.Equals(row.Token, token, StringComparison.Ordinal))
            {
                return Result.Success();
            }

            var removed = await repository.RemoveAsync(row, ctx.CancellationToken).ConfigureAwait(false);
            return removed.IsSuccess || RecordQueries.IsRace(removed.Error!) ? Result.Success() : removed;
        }, cancellationToken);

    /// <summary>
    /// 写入失败归类：与别的节点撞上（先插入、先接管、先删除）是被占用，其余原样返回。
    /// </summary>
    private static Result<DateTimeOffset?> Contended(Error error)
        => RecordQueries.IsRace(error) ? Result.Success<DateTimeOffset?>(null) : Result.Failure<DateTimeOffset?>(error);
}
