// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.UnitOfWork;

/// <summary>
/// Transaction 阶段的中间件。
/// </summary>
/// <remarks>
/// 用当前租户开启作用域内的 <see cref="IUnitOfWork"/>：调用带操作描述且契约只读（<see cref="OperationContract.IsReadOnly"/>）时开只读工作单元，其余调用开可写工作单元。调用成功时先派发工作单元里聚合的领域事件（经 <see cref="IDomainEventDispatcher"/>，装配了发件箱时随业务写入同一事务），再提交；失败或异常回滚。
/// <para>
/// 只读工作单元里 Provider 直接写入入口抛出的 <see cref="PersistenceErrors.ReadOnly"/> 在这里回滚并转为同码结果，不作为未处理异常离开管道。同一服务作用域里再经管道发起的嵌套调用遇到已经开始过的工作单元，以 <see cref="PersistenceErrors.InvalidUnitOfWorkState"/> 结束、不执行处理器：嵌套调用要新建服务作用域，各开自己的工作单元（RFC-0005）。
/// </para>
/// </remarks>
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

        // 作用域里的工作单元已经开始过：这是用同一个服务作用域再经管道发起的嵌套调用。工作单元一次调用一个、只能开始一次；
        // 不加入外层的工作单元——加入会让内层写入的成败取决于外层是否只读，内层失败时已写的部分又会随外层提交——
        // 以结果结束内层调用、不执行处理器，外层工作单元的状态不受影响
        if (unitOfWork.State != UnitOfWorkState.NotStarted)
        {
            context.SetOutcome(Error.Validation(PersistenceErrors.InvalidUnitOfWorkState, PersistenceMessages.NestedInvocation));
            return;
        }

        // 只读只看本次调用自己的契约：不带操作描述的调用（消息处理器、作业、直接交给 App.InvokeAsync 的处理器）可写；
        // 只读不跨调用传播，嵌套调用在自己的作用域里按自己的契约开工作单元
        var readOnly = context.GetOperation().Match(static operation => operation.Contract.IsReadOnly, static () => false);
        await unitOfWork.BeginAsync(new UnitOfWorkOptions(context.GetSecurity().Tenant?.Id, readOnly), context.CancellationToken).ConfigureAwait(false);

        try
        {
            await nextStep(context).ConfigureAwait(false);
        }
        catch (FrameworkException exception) when (string.Equals(exception.Code, PersistenceErrors.ReadOnly, StringComparison.Ordinal))
        {
            // 只读工作单元里 Provider 的直接写入入口（EnsureTransactionAsync）抛出：写入没有执行。它是操作实现的缺陷而不是基础设施故障，
            // 转为同码结果（HTTP 403），不让 Observe 阶段包成 KERNEL_UNEXPECTED_001；回滚用 None 令牌保证一定执行
            await unitOfWork.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            context.SetOutcome(Error.Denied(PersistenceErrors.ReadOnly, PersistenceMessages.ReadOnly));
            return;
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

        // 提交失败（并发冲突、标识重复、容量已满；含写入阶段已失败却被处理器忽略的标识重复）覆盖调用结果，让调用方看到真实的持久化错误
        var commit = await unitOfWork.CommitAsync(context.CancellationToken).ConfigureAwait(false);
        if (commit.IsFailure)
        {
            context.SetOutcome(commit);
        }
    }

    /// <summary>
    /// 把工作单元里带有未派发事件的聚合交给派发器。
    /// </summary>
    /// <remarks>
    /// 有事件却没有注册派发器是装配错误，返回 <see cref="PersistenceErrors.EventsUndispatched"/> 而不是静默丢弃。
    /// </remarks>
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
