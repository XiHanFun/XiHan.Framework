// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.UnitOfWork;

/// <summary>
/// Transaction 阶段的中间件：用当前租户开启作用域内的 <see cref="IUnitOfWork"/>，调用成功提交，失败或异常回滚。
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

        // 提交失败（并发冲突、标识重复、容量已满）覆盖调用结果，让调用方看到真实的持久化错误
        var commit = await unitOfWork.CommitAsync(context.CancellationToken).ConfigureAwait(false);
        if (commit.IsFailure)
        {
            context.SetOutcome(commit);
        }
    }
}
