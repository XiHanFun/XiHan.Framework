// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Database.Entities;
using XiHan.Framework.Scheduling.Database.Storage;

namespace XiHan.Framework.Scheduling.Database.Leadership;

/// <summary>
/// 租约行的三个原子步骤
/// </summary>
/// <remarks>
/// 获取、续约、释放，每一步都在自己的工作单元里提交（返回即提交，别的节点立刻看得见）。获取与续约是独立的管道调用，只在应用处于服务状态时进行（引擎不在服务状态的周期不拿不续）；释放不经管道，排空与停止时也能删行（ADR-0091）。跨进程的原子性来自主键唯一约束（插入）与并发戳（接管、续约）：仓储更新先加行锁读当前行再比对戳，戳不同返回冲突
/// </remarks>
internal sealed class LeaseRows
{
    /// <summary>
    /// 管道执行（获取与续约）
    /// </summary>
    private readonly StoreInvocation _invocation;

    /// <summary>
    /// 根服务提供者：释放时开新作用域
    /// </summary>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 时钟
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 创建
    /// </summary>
    public LeaseRows(IServiceProvider services, App app, TimeProvider clock)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _invocation = new StoreInvocation(services, app);
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// 尝试获取
    /// </summary>
    /// <remarks>
    /// 行不存在就插入；存在且已过期就接管；拿到返回到期时刻，被占用（含插入或接管时撞上别的节点）返回成功的无值；其余失败原样返回
    /// </remarks>
    public ValueTask<Result<Maybe<DateTimeOffset>>> TryAcquireAsync(string resource, string holder, string token, TimeSpan timeToLive, CancellationToken cancellationToken)
        => _invocation.RunAsync<Maybe<DateTimeOffset>>(async ctx =>
        {
            var repository = StoreInvocation.Repository<SchedulerLeaseEntity>(ctx);
            var now = _clock.GetUtcNow();
            var found = await repository.FindAsync(resource, ctx.CancellationToken).ConfigureAwait(false);
            if (!found.TryGetValue(out var row))
            {
                var expires = now + timeToLive;
                var added = await repository.AddAsync(new SchedulerLeaseEntity { Id = resource, Holder = holder, Token = token, ExpiresAt = expires }, ctx.CancellationToken).ConfigureAwait(false);
                return added.IsSuccess ? Until(expires) : Contended(added.Error!);
            }

            // 未过期的租约属于别人（或还没释放的自己）：不可重入，只能等
            if (row.ExpiresAt > now)
            {
                return Taken();
            }

            row.Holder = holder;
            row.Token = token;
            row.ExpiresAt = now + timeToLive;
            var updated = await repository.UpdateAsync(row, ctx.CancellationToken).ConfigureAwait(false);
            return updated.IsSuccess ? Until(row.ExpiresAt) : Contended(updated.Error!);
        }, cancellationToken);

    /// <summary>
    /// 续约
    /// </summary>
    /// <remarks>
    /// 行仍带着 <paramref name="token"/> 时把到期时刻推到现在加存活时间并返回新的到期时刻；行没了、令牌变了或撞上别的节点返回成功的无值
    /// </remarks>
    public ValueTask<Result<Maybe<DateTimeOffset>>> RenewAsync(string resource, string token, TimeSpan timeToLive, CancellationToken cancellationToken)
        => _invocation.RunAsync<Maybe<DateTimeOffset>>(async ctx =>
        {
            var repository = StoreInvocation.Repository<SchedulerLeaseEntity>(ctx);
            var found = await repository.FindAsync(resource, ctx.CancellationToken).ConfigureAwait(false);
            if (!found.TryGetValue(out var row) || !string.Equals(row.Token, token, StringComparison.Ordinal))
            {
                return Taken();
            }

            row.ExpiresAt = _clock.GetUtcNow() + timeToLive;
            var updated = await repository.UpdateAsync(row, ctx.CancellationToken).ConfigureAwait(false);
            return updated.IsSuccess ? Until(row.ExpiresAt) : Contended(updated.Error!);
        }, cancellationToken);

    /// <summary>
    /// 释放
    /// </summary>
    /// <remarks>
    /// 只删带着 <paramref name="token"/> 的行；行没了、令牌变了或撞上别的节点都视为已释放。不经管道：在根提供者的新作用域里开可写工作单元、删完即提交；异常归为存储不可达
    /// </remarks>
    public async ValueTask<Result> ReleaseAsync(string resource, string token, CancellationToken cancellationToken)
    {
        // 释放发生在应用不在服务状态的时候（排空中的周期、调度器停止），管道这时拒绝一切调用：经管道就删不掉行，
        // 另一个节点要等租约到期才能接手。租约行是平台数据，不需要 Tenant / Authorize 阶段，所以与健康探针一样直接开工作单元
        await using var scope = _services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        try
        {
            await unitOfWork.BeginAsync(UnitOfWorkOptions.Default, cancellationToken).ConfigureAwait(false);
            var repository = scope.ServiceProvider.GetRequiredService<IRepository<SchedulerLeaseEntity, string>>();
            var found = await repository.FindAsync(resource, cancellationToken).ConfigureAwait(false);
            if (!found.TryGetValue(out var row) || !string.Equals(row.Token, token, StringComparison.Ordinal))
            {
                await unitOfWork.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                return Result.Success();
            }

            var removed = await repository.RemoveAsync(row, cancellationToken).ConfigureAwait(false);
            if (removed.IsFailure)
            {
                await unitOfWork.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                return RecordQueries.IsRace(removed.Error!) ? Result.Success() : removed;
            }

            // 提交时撞上别的节点（行已被接管或删除）同样视为已释放
            var committed = await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            return committed.IsSuccess || RecordQueries.IsRace(committed.Error!) ? Result.Success() : committed;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // 回滚用 None 令牌保证一定执行；工作单元没开起来时不回滚
            if (unitOfWork.State == UnitOfWorkState.Active)
            {
                await unitOfWork.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            }

            return StoreInvocation.Unavailable(exception.Message);
        }
    }

    /// <summary>
    /// 写入失败归类：与别的节点撞上（先插入、先接管、先删除）是被占用，其余原样返回
    /// </summary>
    private static Result<Maybe<DateTimeOffset>> Contended(Error error)
        => RecordQueries.IsRace(error) ? Taken() : Result.Failure<Maybe<DateTimeOffset>>(error);

    /// <summary>
    /// 拿到了，到期时刻是 <paramref name="expiresAt"/>
    /// </summary>
    private static Result<Maybe<DateTimeOffset>> Until(DateTimeOffset expiresAt) => Result.Success(Maybe.Some(expiresAt));

    /// <summary>
    /// 被占用或已失去
    /// </summary>
    /// <remarks>
    /// 是成功的无值，不是失败：原语的成功结果不能携带 <see langword="null"/>
    /// </remarks>
    private static Result<Maybe<DateTimeOffset>> Taken() => Result.Success(Maybe.None<DateTimeOffset>());
}
