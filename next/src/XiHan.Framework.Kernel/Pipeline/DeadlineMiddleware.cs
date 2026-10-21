// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Pipeline;

/// <summary>
/// Resilience 阶段的内置中间件：把 <see cref="InvocationContext.Deadline"/> 落为取消，超时记为 <see cref="KernelErrors.DeadlineExceeded"/>，
/// 外部取消记为 <see cref="KernelErrors.Cancelled"/>。
/// </summary>
/// <param name="timeProvider">时间源。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class DeadlineMiddleware(TimeProvider timeProvider) : IPipelineMiddleware
{
    /// <summary>
    /// 时间源；判断是否超时与创建定时取消都用它，便于测试推进虚拟时间。
    /// </summary>
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc />
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);

        // 进入时已过期就直接短路，不再执行后续中间件与处理器。
        var now = _timeProvider.GetUtcNow();
        if (context.IsExpired(now))
        {
            context.SetOutcome(new Error(KernelErrors.DeadlineExceeded, KernelMessages.DeadlineBeforeStart));
            return;
        }

        // 没有截止时间就原样透传上下文，不派生新实例、不创建定时器。
        var remaining = context.RemainingTime(now);
        if (remaining is null)
        {
            await Invoke(context, context, nextStep, CancellationToken.None).ConfigureAwait(false);
            return;
        }

        // 截止时间落为定时取消，并与外部令牌链接；下游拿到的是派生上下文，原上下文的令牌不变。
        using var deadline = new CancellationTokenSource(remaining.Value, _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, deadline.Token);
        await Invoke(context, context.WithCancellation(linked.Token), nextStep, deadline.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// 执行下游并把取消翻译为结果：先看截止令牌（超时），再看原上下文的令牌（外部取消）；其余取消原样抛出。
    /// </summary>
    /// <param name="original">原上下文，结果写回这里。</param>
    /// <param name="scoped">传给下游的上下文，可能是带链接令牌的派生实例。</param>
    /// <param name="nextStep">下一步。</param>
    /// <param name="deadlineToken">截止时间触发的令牌。</param>
    private static async ValueTask Invoke(InvocationContext original, InvocationContext scoped, PipelineStep nextStep, CancellationToken deadlineToken)
    {
        try
        {
            await nextStep(scoped).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (deadlineToken.IsCancellationRequested)
        {
            // 超时与外部取消可能同时触发，超时优先：这是调用方更需要知道的原因。
            original.SetOutcome(new Error(KernelErrors.DeadlineExceeded, KernelMessages.DeadlineExceeded));
        }
        catch (OperationCanceledException) when (original.CancellationToken.IsCancellationRequested)
        {
            original.SetOutcome(new Error(KernelErrors.Cancelled, KernelMessages.Cancelled));
        }
    }
}
