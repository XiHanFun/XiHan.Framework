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
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc />
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);

        var now = _timeProvider.GetUtcNow();
        if (context.IsExpired(now))
        {
            context.SetOutcome(new Error(KernelErrors.DeadlineExceeded, "调用在开始前已超过截止时间。"));
            return;
        }

        var remaining = context.RemainingTime(now);
        if (remaining is null)
        {
            await Invoke(context, context, nextStep, CancellationToken.None).ConfigureAwait(false);
            return;
        }

        using var deadline = new CancellationTokenSource(remaining.Value, _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, deadline.Token);
        await Invoke(context, context.WithCancellation(linked.Token), nextStep, deadline.Token).ConfigureAwait(false);
    }

    private static async ValueTask Invoke(InvocationContext original, InvocationContext scoped, PipelineStep nextStep, CancellationToken deadlineToken)
    {
        try
        {
            await nextStep(scoped).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (deadlineToken.IsCancellationRequested)
        {
            original.SetOutcome(new Error(KernelErrors.DeadlineExceeded, "调用超过截止时间。"));
        }
        catch (OperationCanceledException) when (original.CancellationToken.IsCancellationRequested)
        {
            original.SetOutcome(new Error(KernelErrors.Cancelled, "调用被取消。"));
        }
    }
}
