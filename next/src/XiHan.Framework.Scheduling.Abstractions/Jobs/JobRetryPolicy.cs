// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 失败运行的重试策略。
/// </summary>
/// <remarks>
/// 只有失败的运行重试（作业返回失败、抛出异常或超过执行时限），被取消与失联的不重试；重试是一次新运行，<see cref="JobRun.Attempt"/> 加一（RFC-0013，ADR-0060）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record JobRetryPolicy
{
    /// <summary>
    /// 重试次数的上限。
    /// </summary>
    public const int MaxRetriesLimit = 100;

    /// <summary>
    /// 等待时间的上限。
    /// </summary>
    public static readonly TimeSpan DelayLimit = TimeSpan.FromDays(30);

    /// <summary>
    /// 创建固定等待的策略。
    /// </summary>
    public JobRetryPolicy(int maxRetries, TimeSpan delay)
    {
        MaxRetries = maxRetries;
        Delay = delay;
    }

    /// <summary>
    /// 失败后最多再运行几次。
    /// </summary>
    /// <remarks>
    /// 1 到 <see cref="MaxRetriesLimit"/>。
    /// </remarks>
    public int MaxRetries
    {
        get => field;
        init => field = value is >= 1 and <= MaxRetriesLimit ? value : throw new ArgumentOutOfRangeException(nameof(value), value, $"The retry count must be between 1 and {MaxRetriesLimit}.");
    }

    /// <summary>
    /// 第一次重试前的等待。
    /// </summary>
    /// <remarks>
    /// 大于零、不超过 <see cref="DelayLimit"/>。
    /// </remarks>
    public TimeSpan Delay
    {
        get => field;
        init => field = value > TimeSpan.Zero && value <= DelayLimit ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The retry delay must be positive and at most 30 days.");
    }

    /// <summary>
    /// 等待方式。
    /// </summary>
    public JobRetryBackoff Backoff { get; init; }

    /// <summary>
    /// 指数退避的等待上限。
    /// </summary>
    /// <remarks>
    /// 空为只受 <see cref="DelayLimit"/> 约束。
    /// </remarks>
    public TimeSpan? MaxDelay
    {
        get => field;
        init => field = value is null || (value > TimeSpan.Zero && value <= DelayLimit) ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum retry delay must be positive and at most 30 days.");
    }

    /// <summary>
    /// 每次等待相同的策略。
    /// </summary>
    public static JobRetryPolicy Fixed(int maxRetries, TimeSpan delay) => new(maxRetries, delay);

    /// <summary>
    /// 指数退避的策略。
    /// </summary>
    public static JobRetryPolicy Exponential(int maxRetries, TimeSpan delay, TimeSpan? maxDelay = null)
        => new(maxRetries, delay) { Backoff = JobRetryBackoff.Exponential, MaxDelay = maxDelay };

    /// <summary>
    /// 第 <paramref name="retry"/> 次重试前的等待。
    /// </summary>
    /// <remarks>
    /// <paramref name="retry"/> 从 1 起；结果不超过 <see cref="MaxDelay"/> 与 <see cref="DelayLimit"/>。
    /// </remarks>
    public TimeSpan DelayFor(int retry)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(retry, 1);
        var limit = MaxDelay ?? DelayLimit;
        if (Backoff == JobRetryBackoff.Fixed)
        {
            return Delay;
        }

        // 2^(n-1) 倍：倍数超过上限与当前等待之比即取上限，避免溢出
        var ceiling = limit.Ticks / (double)Delay.Ticks;
        var factor = Math.Pow(2, retry - 1);
        return factor >= ceiling ? limit : TimeSpan.FromTicks((long)(Delay.Ticks * factor));
    }
}
