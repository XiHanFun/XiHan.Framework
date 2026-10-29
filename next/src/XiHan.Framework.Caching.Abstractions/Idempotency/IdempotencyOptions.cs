// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Idempotency;

/// <summary>
/// 幂等选项。
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Caching:Idempotency</c> 显式读取。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record IdempotencyOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Caching:Idempotency";

    /// <summary>
    /// 已完成结果的保留时间。
    /// </summary>
    /// <remarks>
    /// 默认 24 小时。
    /// </remarks>
    public TimeSpan TimeToLive
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The time to live must be positive.");
    } = TimeSpan.FromHours(24);

    /// <summary>
    /// "处理中"标记的保留时间，处理器崩溃且未能清理时的最长封锁。
    /// </summary>
    /// <remarks>
    /// 默认 60 秒。
    /// </remarks>
    public TimeSpan InProgressTimeToLive
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The in-progress time to live must be positive.");
    } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 从配置读取：<c>TimeToLiveSeconds</c>、<c>InProgressTimeToLiveSeconds</c>。
    /// </summary>
    public static IdempotencyOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new IdempotencyOptions();
        if (ReadSeconds(configuration, "TimeToLiveSeconds") is { } timeToLive)
        {
            options = options with { TimeToLive = timeToLive };
        }

        if (ReadSeconds(configuration, "InProgressTimeToLiveSeconds") is { } inProgress)
        {
            options = options with { InProgressTimeToLive = inProgress };
        }

        return options;
    }

    /// <summary>
    /// 读取配置节下的秒数。
    /// </summary>
    /// <remarks>
    /// 未配置返回 <see langword="null"/>，不是正数抛出 <see cref="ArgumentException"/>。
    /// </remarks>
    private static TimeSpan? ReadSeconds(IConfiguration configuration, string name)
    {
        var raw = configuration[$"{ConfigurationSection}:{name}"];
        if (raw is null)
        {
            return null;
        }

        // 用不变区域性解析，避免小数点随宿主区域设置变化
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : throw new ArgumentException($"{ConfigurationSection}:{name} must be a positive number, but was '{raw}'.");
    }
}
