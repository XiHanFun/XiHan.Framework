// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Jobs;

namespace XiHan.Framework.Scheduling.Abstractions.Schedulers;

/// <summary>
/// 引擎选项（配置节 <c>XiHan:Scheduling</c>）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record SchedulerOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Scheduling";

    /// <summary>
    /// 本进程的默认节点 ID：机器名 + 进程 ID + 随机，进程内固定。
    /// </summary>
    private static readonly string _defaultNodeId = Environment.MachineName + ":" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + ":" + Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// 轮询周期；默认 1 秒。
    /// </summary>
    public TimeSpan PollInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The poll interval must be positive.");
    } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 节点 ID；默认机器名 + 进程 ID + 随机。
    /// </summary>
    public string NodeId
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The node ID must not be empty.", nameof(value));
    } = _defaultNodeId;

    /// <summary>
    /// 本节点同时运行的上限；默认 32。
    /// </summary>
    public int MaxConcurrentRuns
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum concurrent runs must be at least 1.");
    } = 32;

    /// <summary>
    /// 一个周期最多抢占的触发器数；默认 16。
    /// </summary>
    public int AcquireBatchSize
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The acquire batch size must be at least 1.");
    } = 16;

    /// <summary>
    /// misfire 阈值：到期时刻落后当前时刻超过它才算 misfire；默认 60 秒。
    /// </summary>
    public TimeSpan MisfireThreshold
    {
        get => field;
        init => field = value >= TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The misfire threshold must not be negative.");
    } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 领导租约的资源名；默认 <c>scheduling.jobs</c>。
    /// </summary>
    public JobName LeaseResource { get; init; } = new("scheduling.jobs");

    /// <summary>
    /// 领导租约的存活时间；默认 30 秒。
    /// </summary>
    public TimeSpan LeaseTimeToLive
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The lease time to live must be positive.");
    } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 节点失联判定：心跳超过它即失联；默认 90 秒。
    /// </summary>
    public TimeSpan NodeTimeout
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The node timeout must be positive.");
    } = TimeSpan.FromSeconds(90);

    /// <summary>
    /// 领导者维护（失联恢复、历史清理）的周期；默认 60 秒。
    /// </summary>
    public TimeSpan HousekeepingInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The housekeeping interval must be positive.");
    } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 运行历史保留期；默认 7 天，零为不删。
    /// </summary>
    public TimeSpan HistoryRetention
    {
        get => field;
        init => field = value >= TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The history retention must not be negative.");
    } = TimeSpan.FromDays(7);

    /// <summary>
    /// 从配置读取。
    /// </summary>
    public static SchedulerOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new SchedulerOptions();
        if (Seconds(configuration, "PollIntervalSeconds") is { } poll)
        {
            options = options with { PollInterval = poll };
        }

        if (configuration[$"{ConfigurationSection}:NodeId"] is { } node && !string.IsNullOrWhiteSpace(node))
        {
            options = options with { NodeId = node };
        }

        if (Integer(configuration, "MaxConcurrentRuns") is { } runs)
        {
            options = options with { MaxConcurrentRuns = runs };
        }

        if (Integer(configuration, "AcquireBatchSize") is { } batch)
        {
            options = options with { AcquireBatchSize = batch };
        }

        if (Seconds(configuration, "MisfireThresholdSeconds") is { } misfire)
        {
            options = options with { MisfireThreshold = misfire };
        }

        if (configuration[$"{ConfigurationSection}:LeaseResource"] is { } resource && !string.IsNullOrWhiteSpace(resource))
        {
            options = options with { LeaseResource = new JobName(resource) };
        }

        if (Seconds(configuration, "LeaseTimeToLiveSeconds") is { } ttl)
        {
            options = options with { LeaseTimeToLive = ttl };
        }

        if (Seconds(configuration, "NodeTimeoutSeconds") is { } timeout)
        {
            options = options with { NodeTimeout = timeout };
        }

        if (Seconds(configuration, "HousekeepingIntervalSeconds") is { } housekeeping)
        {
            options = options with { HousekeepingInterval = housekeeping };
        }

        if (configuration[$"{ConfigurationSection}:HistoryRetentionDays"] is { } days)
        {
            options = double.TryParse(days, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0
                ? options with { HistoryRetention = TimeSpan.FromDays(parsed) }
                : throw new ArgumentException($"{ConfigurationSection}:HistoryRetentionDays must be a non-negative number, but was '{days}'.");
        }

        return options;
    }

    /// <summary>
    /// 读秒数配置；不合法抛出。
    /// </summary>
    private static TimeSpan? Seconds(IConfiguration configuration, string key)
    {
        var raw = configuration[$"{ConfigurationSection}:{key}"];
        if (raw is null)
        {
            return null;
        }

        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0
            ? TimeSpan.FromSeconds(seconds)
            : throw new ArgumentException($"{ConfigurationSection}:{key} must be a non-negative number, but was '{raw}'.");
    }

    /// <summary>
    /// 读整数配置；不合法抛出。
    /// </summary>
    private static int? Integer(IConfiguration configuration, string key)
    {
        var raw = configuration[$"{ConfigurationSection}:{key}"];
        if (raw is null)
        {
            return null;
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new ArgumentException($"{ConfigurationSection}:{key} must be an integer, but was '{raw}'.");
    }
}
