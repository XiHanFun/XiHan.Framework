// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Resilience;

/// <summary>
/// 一组 Resilience 阶段的限制
/// </summary>
/// <remarks>
/// 固定窗口限流、并发上限与熔断；每项为空表示不启用。默认策略与按操作 ID 的覆盖都是它
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ResiliencePolicy
{
    /// <summary>
    /// 固定窗口内允许的调用数
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> 不限流
    /// </remarks>
    public int? PermitLimit
    {
        get => field;
        init => field = value is null or >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "'PermitLimit' must be positive.");
    }

    /// <summary>
    /// 限流窗口
    /// </summary>
    /// <remarks>
    /// 默认 1 秒
    /// </remarks>
    public TimeSpan Window
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "'Window' must be positive.");
    } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 窗口用尽时最多排队等待下一窗口的调用数
    /// </summary>
    /// <remarks>
    /// 默认 0，超出直接拒绝
    /// </remarks>
    public int QueueLimit
    {
        get => field;
        init => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "'QueueLimit' must not be negative.");
    }

    /// <summary>
    /// 同时执行的调用数上限
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> 不限
    /// </remarks>
    public int? ConcurrencyLimit
    {
        get => field;
        init => field = value is null or >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "'ConcurrencyLimit' must be positive.");
    }

    /// <summary>
    /// 并发用尽时最多排队等待的调用数
    /// </summary>
    /// <remarks>
    /// 默认 0，超出直接拒绝
    /// </remarks>
    public int ConcurrencyQueueLimit
    {
        get => field;
        init => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "'ConcurrencyQueueLimit' must not be negative.");
    }

    /// <summary>
    /// 连续失败（UNAVAILABLE / TIMEOUT / UNEXPECTED 结果或未处理异常）多少次后熔断
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> 不熔断
    /// </remarks>
    public int? FailureThreshold
    {
        get => field;
        init => field = value is null or >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "'FailureThreshold' must be positive.");
    }

    /// <summary>
    /// 熔断后拒绝调用的时长，到期放一次试探
    /// </summary>
    /// <remarks>
    /// 默认 30 秒
    /// </remarks>
    public TimeSpan BreakDuration
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "'BreakDuration' must be positive.");
    } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 是否没有任何限制
    /// </summary>
    public bool IsEmpty => PermitLimit is null && ConcurrencyLimit is null && FailureThreshold is null;

    /// <summary>
    /// 从配置节读取
    /// </summary>
    /// <remarks>
    /// <c>PermitLimit</c>、<c>WindowSeconds</c>、<c>QueueLimit</c>、<c>ConcurrencyLimit</c>、<c>ConcurrencyQueueLimit</c>、<c>FailureThreshold</c>、<c>BreakSeconds</c>；缺省的键沿用 <paramref name="fallback"/>（按操作覆盖时是默认策略，否则是无限制）；非法值抛出 <see cref="ArgumentException"/>
    /// </remarks>
    public static ResiliencePolicy FromConfiguration(IConfigurationSection section, ResiliencePolicy? fallback = null)
    {
        ArgumentNullException.ThrowIfNull(section);
        var policy = fallback ?? new ResiliencePolicy();
        return policy with
        {
            PermitLimit = ConfigurationValues.ReadInt32(section, "PermitLimit", 1) ?? policy.PermitLimit,
            Window = ConfigurationValues.ReadSeconds(section, "WindowSeconds") ?? policy.Window,
            QueueLimit = ConfigurationValues.ReadInt32(section, "QueueLimit", 0) ?? policy.QueueLimit,
            ConcurrencyLimit = ConfigurationValues.ReadInt32(section, "ConcurrencyLimit", 1) ?? policy.ConcurrencyLimit,
            ConcurrencyQueueLimit = ConfigurationValues.ReadInt32(section, "ConcurrencyQueueLimit", 0) ?? policy.ConcurrencyQueueLimit,
            FailureThreshold = ConfigurationValues.ReadInt32(section, "FailureThreshold", 1) ?? policy.FailureThreshold,
            BreakDuration = ConfigurationValues.ReadSeconds(section, "BreakSeconds") ?? policy.BreakDuration,
        };
    }
}
