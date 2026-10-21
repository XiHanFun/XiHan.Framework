// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions;

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

        var unitOfWork = context.Services.GetRequiredService<IUnitOfWork>();
        await unitOfWork.BeginAsync(new UnitOfWorkOptions(context.GetSecurity().Tenant?.Id), context.CancellationToken).ConfigureAwait(false);

        try
        {
            await nextStep(context).ConfigureAwait(false);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        if (context.GetOutcome().Match(static r => r.IsFailure, static () => false))
        {
            await unitOfWork.RollbackAsync(context.CancellationToken).ConfigureAwait(false);
            return;
        }

        var commit = await unitOfWork.CommitAsync(context.CancellationToken).ConfigureAwait(false);
        if (commit.IsFailure)
        {
            context.SetOutcome(commit);
        }
    }
}
