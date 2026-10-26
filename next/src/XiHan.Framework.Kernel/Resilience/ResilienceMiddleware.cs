// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Resilience;

/// <summary>
/// Resilience 阶段的内置中间件（在 <see cref="DeadlineMiddleware"/> 之后）：对带操作描述的调用按 <see cref="ResilienceOptions"/> 施加熔断、固定窗口限流与并发上限，
/// 拒绝以 UNAVAILABLE 种类的结果返回（<see cref="KernelErrors.CircuitOpen"/>、<see cref="KernelErrors.RateLimited"/>、<see cref="KernelErrors.ConcurrencyExceeded"/>），不进后续阶段。
/// 限制实例按操作 ID 各一份（登记表有界，所以这里也有界）；没有操作描述的调用（宿主自定义端点、消息派发）不受限。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class ResilienceMiddleware : IPipelineMiddleware, IDisposable
{
    /// <summary>
    /// 熔断器计为失败的错误码 KIND：下游不可用、超时、意外；校验、不存在、拒绝等是调用方的问题，不算下游坏了。
    /// </summary>
    private static readonly string[] _failureKinds = ["UNAVAILABLE", "TIMEOUT", "UNEXPECTED"];

    /// <summary>
    /// 选项。
    /// </summary>
    private readonly ResilienceOptions _options;

    /// <summary>
    /// 时钟；熔断器据此判定到期。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 按操作 ID 的限制实例。
    /// </summary>
    private readonly ConcurrentDictionary<string, OperationLimits> _limits = new(StringComparer.Ordinal);

    /// <summary>
    /// 创建。
    /// </summary>
    public ResilienceMiddleware(ResilienceOptions options, TimeProvider clock)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <inheritdoc />
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);
        if (!context.GetOperation().TryGetValue(out var descriptor))
        {
            await nextStep(context).ConfigureAwait(false);
            return;
        }

        var limits = _limits.GetOrAdd(descriptor.Id, id => OperationLimits.Create(_options.PolicyFor(id), _clock));
        if (limits.IsEmpty)
        {
            await nextStep(context).ConfigureAwait(false);
            return;
        }

        // 判定顺序：先熔断（打开时连许可都不消耗），再限流，再并发；任一拒绝都不进后续阶段
        var breaker = limits.Breaker;
        if (breaker is not null && !breaker.TryEnter(out var retryAfter))
        {
            context.SetOutcome(Error.Unavailable(KernelErrors.CircuitOpen, KernelMessages.CircuitOpen, MessageArguments.Of(("id", descriptor.Id), ("retryAfter", Seconds(retryAfter)))));
            return;
        }

        var admitted = false;
        try
        {
            // 排队等待受调用的取消令牌约束：截止时间到了由 DeadlineMiddleware 按超时结束，不会无限等
            using var rateLease = limits.Rate is { } rate ? await rate.AcquireAsync(1, context.CancellationToken).ConfigureAwait(false) : null;
            if (rateLease is { IsAcquired: false })
            {
                var wait = rateLease.TryGetMetadata(MetadataName.RetryAfter, out var hint) ? hint : _options.PolicyFor(descriptor.Id).Window;
                context.SetOutcome(Error.Unavailable(KernelErrors.RateLimited, KernelMessages.RateLimited, MessageArguments.Of(("id", descriptor.Id), ("retryAfter", Seconds(wait)))));
                return;
            }

            using var concurrencyLease = limits.Concurrency is { } concurrency ? await concurrency.AcquireAsync(1, context.CancellationToken).ConfigureAwait(false) : null;
            if (concurrencyLease is { IsAcquired: false })
            {
                context.SetOutcome(Error.Unavailable(KernelErrors.ConcurrencyExceeded, KernelMessages.ConcurrencyExceeded, MessageArguments.Of(("id", descriptor.Id))));
                return;
            }

            admitted = true;
            await ExecuteAsync(context, nextStep, breaker).ConfigureAwait(false);
        }
        finally
        {
            if (!admitted)
            {
                // 放行后没执行到下游（被限流 / 并发拒绝或排队被取消）：半开态把试探名额让出来
                breaker?.Abandon();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var limits in _limits.Values)
        {
            limits.Dispose();
        }

        _limits.Clear();
    }

    /// <summary>
    /// 执行下游并把结论记给熔断器：异常与基础设施类失败结果算失败，其余算成功。
    /// </summary>
    private static async ValueTask ExecuteAsync(InvocationContext context, PipelineStep nextStep, CircuitBreaker? breaker)
    {
        try
        {
            await nextStep(context).ConfigureAwait(false);
        }
        catch
        {
            // 异常是最明确的失败信号；记完再原样抛给 Observe 阶段翻译
            breaker?.RecordFailure();
            throw;
        }

        if (breaker is null)
        {
            return;
        }

        if (context.GetOutcome().TryGetValue(out var outcome) && outcome.IsFailure && IsInfrastructureFailure(outcome.Error!))
        {
            breaker.RecordFailure();
        }
        else
        {
            breaker.RecordSuccess();
        }
    }

    /// <summary>
    /// 错误是否属于熔断器关心的基础设施类失败（按错误码的 KIND 段）。
    /// </summary>
    private static bool IsInfrastructureFailure(Error error)
        => Array.IndexOf(_failureKinds, ErrorCode.KindOf(error.Code)) >= 0;

    /// <summary>
    /// 时长按秒向上取整（至少 1 秒），作为 <c>retryAfter</c> 参数。
    /// </summary>
    private static int Seconds(TimeSpan duration) => Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds));
}
