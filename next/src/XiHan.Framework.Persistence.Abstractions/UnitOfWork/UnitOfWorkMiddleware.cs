// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.UnitOfWork;

/// <summary>
/// Transaction 阶段的中间件：用当前租户开启作用域内的 <see cref="IUnitOfWork"/>，调用成功时先派发工作单元里聚合的领域事件（经 <see cref="IDomainEventDispatcher"/>，装配了发件箱时随业务写入同一事务），再提交；失败或异常回滚。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class UnitOfWorkMiddleware : IPipelineMiddleware
{
    /// <inheritdoc />
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);

        // 工作单元是作用域服务：同一调用内的仓储取到的是同一个实例；租户来自 Tenant 阶段已写入的安全上下文
        var unitOfWork = context.Services.GetRequiredService<IUnitOfWork>();
        await unitOfWork.BeginAsync(new UnitOfWorkOptions(context.GetSecurity().Tenant?.Id), context.CancellationToken).ConfigureAwait(false);

        try
        {
            await nextStep(context).ConfigureAwait(false);
        }
        catch
        {
            // 异常可能正是取消引起的，回滚用 None 令牌保证一定执行；异常原样抛给内核转为 Unexpected 结果
            await unitOfWork.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        // 调用结果为失败：回滚并保留原失败结果，不再提交
        if (context.GetOutcome().Match(static r => r.IsFailure, static () => false))
        {
            await unitOfWork.RollbackAsync(context.CancellationToken).ConfigureAwait(false);
            return;
        }

        // 领域事件在提交前派发：装配了发件箱时事件随业务写入同一事务落进发件箱表，否则直接发布到代理；派发失败回滚并以失败结果结束，不进提交
        var dispatched = await DispatchDomainEventsAsync(context, unitOfWork).ConfigureAwait(false);
        if (dispatched.IsFailure)
        {
            await unitOfWork.RollbackAsync(context.CancellationToken).ConfigureAwait(false);
            context.SetOutcome(dispatched);
            return;
        }

        // 提交失败（并发冲突、标识重复、容量已满）覆盖调用结果，让调用方看到真实的持久化错误
        var commit = await unitOfWork.CommitAsync(context.CancellationToken).ConfigureAwait(false);
        if (commit.IsFailure)
        {
            context.SetOutcome(commit);
        }
    }

    /// <summary>
    /// 把工作单元里带有未派发事件的聚合交给派发器；有事件却没有注册派发器是装配错误，返回 <see cref="PersistenceErrors.EventsUndispatched"/> 而不是静默丢弃。
    /// </summary>
    private static async ValueTask<Result> DispatchDomainEventsAsync(InvocationContext context, IUnitOfWork unitOfWork)
    {
        var aggregates = unitOfWork.DomainEvents.Pending();
        if (aggregates.Count == 0)
        {
            return Result.Success();
        }

        if (context.Services.GetService<IDomainEventDispatcher>() is not { } dispatcher)
        {
            var count = aggregates.Sum(static a => a.DomainEvents.Count);
            return Error.Validation(PersistenceErrors.EventsUndispatched, PersistenceMessages.EventsUndispatched, MessageArguments.Of(("aggregate", aggregates[0].GetType().Name), ("count", count)));
        }

        var dispatched = await dispatcher.DispatchAsync(aggregates, unitOfWork.TenantId, context.CancellationToken).ConfigureAwait(false);
        if (dispatched.IsSuccess)
        {
            unitOfWork.DomainEvents.Clear();
        }

        return dispatched;
    }
}
