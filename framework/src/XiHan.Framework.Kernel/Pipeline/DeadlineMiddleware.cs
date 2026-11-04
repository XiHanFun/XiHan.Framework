// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Pipeline;

/// <summary>
/// Resilience 阶段的内置中间件
/// </summary>
/// <remarks>
/// 把 <see cref="InvocationContext.Deadline"/> 落为取消，超时记为 <see cref="KernelErrors.DeadlineExceeded"/>，外部取消记为 <see cref="KernelErrors.Cancelled"/>。归因只看令牌、两条路径同一规则：截止令牌已触发即超时，不论外部令牌是否也已触发；下游抛出的取消与下游接住取消、自己写下的 <see cref="KernelErrors.Cancelled"/> 结果（插件运行时把 gRPC 的取消映射成结果）都这样归因（RFC-0003 第 1 条，RFC-0004「行为变化」）
/// </remarks>
/// <param name="timeProvider">时间源</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class DeadlineMiddleware(TimeProvider timeProvider) : IPipelineMiddleware
{
    /// <summary>
    /// 时间源
    /// </summary>
    /// <remarks>
    /// 判断是否超时与创建定时取消都用它，便于测试推进虚拟时间
    /// </remarks>
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc />
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);

        // 进入时已过期就直接短路，不再执行后续中间件与处理器
        var now = _timeProvider.GetUtcNow();
        if (context.IsExpired(now))
        {
            context.SetOutcome(new Error(KernelErrors.DeadlineExceeded, KernelMessages.DeadlineBeforeStart));
            return;
        }

        // 没有截止时间就原样透传上下文，不派生新实例、不创建定时器
        var remaining = context.RemainingTime(now);
        if (remaining is null)
        {
            await Invoke(context, context, nextStep, CancellationToken.None).ConfigureAwait(false);
            return;
        }

        // 截止时间落为定时取消，并与外部令牌链接；下游拿到的是派生上下文，原上下文的令牌不变
        using var deadline = new CancellationTokenSource(remaining.Value, _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, deadline.Token);
        await Invoke(context, context.WithCancellation(linked.Token), nextStep, deadline.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// 执行下游并把取消翻译为结果
    /// </summary>
    /// <remarks>
    /// 先看截止令牌（超时），再看原上下文的令牌（外部取消）；其余取消原样抛出。下游正常返回、写下的是取消结果时，截止令牌已触发即纠正成超时。两条路径同一规则
    /// </remarks>
    /// <param name="original">原上下文，结果写回这里</param>
    /// <param name="scoped">传给下游的上下文，可能是带链接令牌的派生实例</param>
    /// <param name="nextStep">下一步</param>
    /// <param name="deadlineToken">截止时间触发的令牌</param>
    private static async ValueTask Invoke(InvocationContext original, InvocationContext scoped, PipelineStep nextStep, CancellationToken deadlineToken)
    {
        try
        {
            await nextStep(scoped).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (deadlineToken.IsCancellationRequested)
        {
            // 截止令牌已触发即超时，外部令牌是否也已触发不论：截止时间到了是调用方给定的事实，回答要确定
            original.SetOutcome(new Error(KernelErrors.DeadlineExceeded, KernelMessages.DeadlineExceeded));
            return;
        }
        catch (OperationCanceledException) when (original.CancellationToken.IsCancellationRequested)
        {
            original.SetOutcome(new Error(KernelErrors.Cancelled, KernelMessages.Cancelled));
            return;
        }

        // 下游把取消接住并写成了取消结果（不抛异常）：只有它知道取消发生了，不知道是哪个令牌触发的。下游若按时钟判断
        // 是否已过截止时间，会与这里的定时器差几毫秒——定时器按系统节拍触发，可能早于时钟读数到达截止时间——把超时报成取消
        // 这里知道：截止令牌已触发，取消就按超时报告。与抛出路径同一规则，外部令牌也已触发时同样是超时——只认外部令牌没有
        // 触发的情形时，客户端在截止时间到点前后断开，抛出的取消报超时、写成结果的报取消，同一件事两种回答（ADR-0092）
        if (deadlineToken.IsCancellationRequested
            && original.GetOutcome().TryGetValue(out var outcome)
            && outcome.Error?.Code == KernelErrors.Cancelled)
        {
            original.SetOutcome(new Error(KernelErrors.DeadlineExceeded, KernelMessages.DeadlineExceeded));
        }
    }
}
