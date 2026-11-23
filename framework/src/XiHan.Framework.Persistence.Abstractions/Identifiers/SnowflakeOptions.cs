// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Identifiers;

/// <summary>
/// 雪花标识生成器的选项
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Persistence:Snowflake</c> 显式逐键读取，不经反射绑定
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record SnowflakeOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Persistence:Snowflake";

    /// <summary>
    /// 缺省 epoch：<c>2026-01-01T00:00:00Z</c>（ADR-0033）
    /// </summary>
    public static DateTimeOffset DefaultEpoch { get; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 时间戳的起点（UTC）
    /// </summary>
    /// <remarks>
    /// 同一套数据一经写入不得再改，否则已有标识解析出的时间会错位
    /// </remarks>
    public DateTimeOffset Epoch
    {
        get => field;
        init => field = value.ToUniversalTime();
    } = DefaultEpoch;

    /// <summary>
    /// 显式指定的机器位（0–1023）
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> 表示启动时经分布式锁租约分配
    /// </remarks>
    public int? WorkerId
    {
        get => field;
        init => field = value is null or (>= 0 and <= SnowflakeId.MaxWorkerId)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, $"The worker ID must be between 0 and {SnowflakeId.MaxWorkerId}.");
    }

    /// <summary>
    /// 时钟回拨的容忍上限：回拨不超过它时等待时钟追平，超过时生成失败
    /// </summary>
    public TimeSpan ClockDriftTolerance
    {
        get => field;
        init => field = value >= TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The clock drift tolerance must not be negative.");
    } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// 租约分配时每个机器位租约的存活时间
    /// </summary>
    /// <remarks>
    /// 每三分之一存活时间续约一次
    /// </remarks>
    public TimeSpan LeaseTimeToLive
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The lease time to live must be positive.");
    } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 从配置读取
    /// </summary>
    /// <remarks>
    /// <c>Epoch</c>（ISO 8601）、<c>WorkerId</c>、<c>ClockDriftToleranceMilliseconds</c>、<c>LeaseTimeToLiveSeconds</c>；缺省键沿用默认值，非法值抛出 <see cref="ArgumentException"/>
    /// </remarks>
    public static SnowflakeOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSection);
        var options = new SnowflakeOptions();

        var epoch = section["Epoch"];
        if (epoch is not null)
        {
            // 没有时区的值按 UTC 理解：epoch 是全集群共用的常量，不能随进程时区漂移
            options = DateTimeOffset.TryParse(epoch, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
                ? options with { Epoch = parsed }
                : throw new ArgumentException($"'{ConfigurationSection}:Epoch' must be an ISO 8601 date-time, but was '{epoch}'.");
        }

        if (ConfigurationValues.ReadInt32(section, "WorkerId", 0, SnowflakeId.MaxWorkerId) is { } workerId)
        {
            options = options with { WorkerId = workerId };
        }

        var tolerance = section["ClockDriftToleranceMilliseconds"];
        if (tolerance is not null)
        {
            options = int.TryParse(tolerance, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                ? options with { ClockDriftTolerance = TimeSpan.FromMilliseconds(value) }
                : throw new ArgumentException($"'{ConfigurationSection}:ClockDriftToleranceMilliseconds' must be a non-negative integer, but was '{tolerance}'.");
        }

        // 这个键是整秒数：按正整数读，不收小数
        if (ConfigurationValues.ReadInt32(section, "LeaseTimeToLiveSeconds", 1) is { } timeToLive)
        {
            options = options with { LeaseTimeToLive = TimeSpan.FromSeconds(timeToLive) };
        }

        return options;
    }
}
