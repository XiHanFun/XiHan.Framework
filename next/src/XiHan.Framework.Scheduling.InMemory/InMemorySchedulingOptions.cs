// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions;

namespace XiHan.Framework.Scheduling.InMemory;

/// <summary>
/// 内存调度选项。从配置节 <c>XiHan:Scheduling:InMemory</c> 显式读取：<c>PollIntervalSeconds</c>、<c>LeaseResource</c>、<c>LeaseTimeToLiveSeconds</c>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record InMemorySchedulingOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Scheduling:InMemory";

    /// <summary>
    /// 轮询周期：检查到期作业与续约的间隔；默认 1 秒。
    /// </summary>
    public TimeSpan PollInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The poll interval must be positive.");
    } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 调度器竞争的领导资源；默认 <c>scheduling.jobs</c>。
    /// </summary>
    public JobName LeaseResource { get; init; } = new("scheduling.jobs");

    /// <summary>
    /// 领导租约的存活时间；默认 30 秒，须大于轮询周期。
    /// </summary>
    public TimeSpan LeaseTimeToLive
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The lease time to live must be positive.");
    } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 从配置读取。
    /// </summary>
    public static InMemorySchedulingOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new InMemorySchedulingOptions();
        var poll = configuration[$"{ConfigurationSection}:PollIntervalSeconds"];
        if (poll is not null)
        {
            options = double.TryParse(poll, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? options with { PollInterval = TimeSpan.FromSeconds(seconds) }
                : throw new ArgumentException($"{ConfigurationSection}:PollIntervalSeconds must be a positive number, but was '{poll}'.");
        }

        if (configuration[$"{ConfigurationSection}:LeaseResource"] is { } resource && !string.IsNullOrWhiteSpace(resource))
        {
            options = options with { LeaseResource = new JobName(resource) };
        }

        var ttl = configuration[$"{ConfigurationSection}:LeaseTimeToLiveSeconds"];
        if (ttl is not null)
        {
            options = double.TryParse(ttl, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? options with { LeaseTimeToLive = TimeSpan.FromSeconds(seconds) }
                : throw new ArgumentException($"{ConfigurationSection}:LeaseTimeToLiveSeconds must be a positive number, but was '{ttl}'.");
        }

        return options;
    }
}
