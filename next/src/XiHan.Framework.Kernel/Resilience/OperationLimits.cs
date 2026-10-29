// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Threading.RateLimiting;

namespace XiHan.Framework.Kernel.Resilience;

/// <summary>
/// 一个操作生效的限制实例。
/// </summary>
/// <remarks>
/// 固定窗口限流器、并发限流器与熔断器，按策略有则建、无则空；随中间件单例活到应用结束。
/// </remarks>
internal sealed class OperationLimits : IDisposable
{
    /// <summary>
    /// 创建。
    /// </summary>
    private OperationLimits(RateLimiter? rate, RateLimiter? concurrency, CircuitBreaker? breaker)
    {
        Rate = rate;
        Concurrency = concurrency;
        Breaker = breaker;
    }

    /// <summary>
    /// 固定窗口限流器。
    /// </summary>
    /// <remarks>
    /// 不限流为 <see langword="null"/>。
    /// </remarks>
    public RateLimiter? Rate { get; }

    /// <summary>
    /// 并发限流器。
    /// </summary>
    /// <remarks>
    /// 不限并发为 <see langword="null"/>。
    /// </remarks>
    public RateLimiter? Concurrency { get; }

    /// <summary>
    /// 熔断器。
    /// </summary>
    /// <remarks>
    /// 不熔断为 <see langword="null"/>。
    /// </remarks>
    public CircuitBreaker? Breaker { get; }

    /// <summary>
    /// 是否没有任何限制。
    /// </summary>
    public bool IsEmpty => Rate is null && Concurrency is null && Breaker is null;

    /// <summary>
    /// 按策略创建。
    /// </summary>
    /// <remarks>
    /// 限流用 <see cref="FixedWindowRateLimiter"/>（自动补充、先到先得），并发用 <see cref="ConcurrencyLimiter"/>。
    /// </remarks>
    public static OperationLimits Create(ResiliencePolicy policy, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(clock);
        var rate = policy.PermitLimit is { } permits
            ? new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
            {
                PermitLimit = permits,
                Window = policy.Window,
                QueueLimit = policy.QueueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true,
            })
            : null;
        var concurrency = policy.ConcurrencyLimit is { } limit
            ? new ConcurrencyLimiter(new ConcurrencyLimiterOptions
            {
                PermitLimit = limit,
                QueueLimit = policy.ConcurrencyQueueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            })
            : null;
        var breaker = policy.FailureThreshold is { } threshold ? new CircuitBreaker(threshold, policy.BreakDuration, clock) : null;
        return new OperationLimits(rate, concurrency, breaker);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Rate?.Dispose();
        Concurrency?.Dispose();
    }
}
