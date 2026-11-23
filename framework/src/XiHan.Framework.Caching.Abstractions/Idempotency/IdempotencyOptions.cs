// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Idempotency;

/// <summary>
/// 幂等选项
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Caching:Idempotency</c> 显式读取
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record IdempotencyOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Caching:Idempotency";

    /// <summary>
    /// 已完成结果的保留时间
    /// </summary>
    /// <remarks>
    /// 默认 24 小时
    /// </remarks>
    public TimeSpan TimeToLive
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The time to live must be positive.");
    } = TimeSpan.FromHours(24);

    /// <summary>
    /// "处理中"标记的保留时间，处理器崩溃且未能清理时的最长封锁
    /// </summary>
    /// <remarks>
    /// 默认 60 秒
    /// </remarks>
    public TimeSpan InProgressTimeToLive
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The in-progress time to live must be positive.");
    } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 从配置读取：<c>TimeToLiveSeconds</c>、<c>InProgressTimeToLiveSeconds</c>
    /// </summary>
    public static IdempotencyOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSection);
        var options = new IdempotencyOptions();
        if (ConfigurationValues.ReadSeconds(section, "TimeToLiveSeconds") is { } timeToLive)
        {
            options = options with { TimeToLive = timeToLive };
        }

        if (ConfigurationValues.ReadSeconds(section, "InProgressTimeToLiveSeconds") is { } inProgress)
        {
            options = options with { InProgressTimeToLive = inProgress };
        }

        return options;
    }
}
