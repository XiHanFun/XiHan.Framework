// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 引擎选项（配置节 <c>XiHan:Workflow</c>）
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Workflow";

    /// <summary>
    /// 一个波次最多执行的节点数
    /// </summary>
    /// <remarks>
    /// 超过即实例故障（防死循环）。默认 1000
    /// </remarks>
    public int MaxNodeExecutionsPerBurst
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The burst limit must be at least 1.");
    } = 1000;

    /// <summary>
    /// 子流程最大深度
    /// </summary>
    /// <remarks>
    /// 默认 16
    /// </remarks>
    public int MaxSubWorkflowDepth
    {
        get => field;
        init => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The depth must not be negative.");
    } = 16;

    /// <summary>
    /// 等实例锁的上限
    /// </summary>
    /// <remarks>
    /// 默认 10 秒
    /// </remarks>
    public TimeSpan LockTimeout
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The lock timeout must be positive.");
    } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 到期书签因实例不在运行中而不能恢复时，把到期时间后推多久
    /// </summary>
    /// <remarks>
    /// 默认 5 分钟
    /// </remarks>
    public TimeSpan NotResumableBackoff
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The backoff must be positive.");
    } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 表达式缓存上限
    /// </summary>
    /// <remarks>
    /// 默认 10000
    /// </remarks>
    public int ExpressionCacheSize
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The cache size must be at least 1.");
    } = 10_000;

    /// <summary>
    /// 已结束实例的保留期
    /// </summary>
    /// <remarks>
    /// 零为不自动清理（默认）
    /// </remarks>
    public TimeSpan Retention
    {
        get => field;
        init => field = value >= TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The retention must not be negative.");
    }

    /// <summary>
    /// 轮询器维护（按保留期清理）的周期
    /// </summary>
    /// <remarks>
    /// 默认 1 小时
    /// </remarks>
    public TimeSpan HousekeepingInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The housekeeping interval must be positive.");
    } = TimeSpan.FromHours(1);

    /// <summary>
    /// <c>Http</c> 活动的默认超时
    /// </summary>
    /// <remarks>
    /// 默认 30 秒
    /// </remarks>
    public TimeSpan HttpTimeout
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The HTTP timeout must be positive.");
    } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 流程定义里 <c>Http</c> 活动的 <c>TimeoutSeconds</c> 的上限
    /// </summary>
    /// <remarks>
    /// 默认 5 分钟。流程定义由租户用户编辑：活动等响应期间占着事务、实例锁与数据库连接，时限不能由定义随意放大。定义校验与活动执行两处都核对，超过即属性错误（ADR-0181）。只限定义里写的值：<see cref="HttpTimeout"/> 是宿主自己的配置，不受它限制
    /// </remarks>
    public TimeSpan MaxHttpTimeout
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The HTTP timeout limit must be positive.");
    } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 从配置读取
    /// </summary>
    /// <remarks>
    /// <c>MaxNodeExecutionsPerBurst</c>、<c>MaxSubWorkflowDepth</c>、<c>LockTimeoutSeconds</c>、<c>NotResumableBackoffSeconds</c>、<c>ExpressionCacheSize</c>、<c>RetentionDays</c>、<c>HousekeepingIntervalSeconds</c>、<c>HttpTimeoutSeconds</c>、<c>MaxHttpTimeoutSeconds</c>。值不合法抛出 <see cref="ArgumentException"/>（消息带配置键与原值），不按缺省处理
    /// </remarks>
    public static WorkflowOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new WorkflowOptions();
        if (Integer(configuration, "MaxNodeExecutionsPerBurst", 1) is { } burst)
        {
            options = options with { MaxNodeExecutionsPerBurst = burst };
        }

        if (Integer(configuration, "MaxSubWorkflowDepth", 0) is { } depth)
        {
            options = options with { MaxSubWorkflowDepth = depth };
        }

        if (Seconds(configuration, "LockTimeoutSeconds") is { } lockTimeout)
        {
            options = options with { LockTimeout = lockTimeout };
        }

        if (Seconds(configuration, "NotResumableBackoffSeconds") is { } backoff)
        {
            options = options with { NotResumableBackoff = backoff };
        }

        if (Integer(configuration, "ExpressionCacheSize", 1) is { } cache)
        {
            options = options with { ExpressionCacheSize = cache };
        }

        if (Days(configuration, "RetentionDays") is { } retention)
        {
            options = options with { Retention = retention };
        }

        if (Seconds(configuration, "HousekeepingIntervalSeconds") is { } housekeeping)
        {
            options = options with { HousekeepingInterval = housekeeping };
        }

        if (Seconds(configuration, "HttpTimeoutSeconds") is { } http)
        {
            options = options with { HttpTimeout = http };
        }

        if (Seconds(configuration, "MaxHttpTimeoutSeconds") is { } maxHttp)
        {
            options = options with { MaxHttpTimeout = maxHttp };
        }

        return options;
    }

    /// <summary>
    /// 读取整数键
    /// </summary>
    /// <remarks>
    /// 缺省返回 <see langword="null"/>，不是整数抛出 <see cref="ArgumentException"/>，小于 <paramref name="minimum"/> 抛出 <see cref="ArgumentOutOfRangeException"/>，消息都带配置键与原值
    /// </remarks>
    private static int? Integer(IConfiguration configuration, string key, int minimum)
    {
        var raw = configuration[$"{ConfigurationSection}:{key}"];
        if (raw is null)
        {
            return null;
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new ArgumentException($"{ConfigurationSection}:{key} must be an integer, but was '{raw}'.");
        }

        return value >= minimum ? value : throw new ArgumentOutOfRangeException($"{ConfigurationSection}:{key}", raw, $"{ConfigurationSection}:{key} must be at least {minimum}.");
    }

    /// <summary>
    /// 读取以秒计的正时长键
    /// </summary>
    /// <remarks>
    /// 缺省返回 <see langword="null"/>，不是有限的数抛出 <see cref="ArgumentException"/>，不是正数抛出 <see cref="ArgumentOutOfRangeException"/>，消息都带配置键与原值
    /// </remarks>
    private static TimeSpan? Seconds(IConfiguration configuration, string key)
    {
        var raw = configuration[$"{ConfigurationSection}:{key}"];
        if (raw is null)
        {
            return null;
        }

        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) || !double.IsFinite(seconds))
        {
            throw new ArgumentException($"{ConfigurationSection}:{key} must be a number of seconds, but was '{raw}'.");
        }

        return seconds > 0 ? TimeSpan.FromSeconds(seconds) : throw new ArgumentOutOfRangeException($"{ConfigurationSection}:{key}", raw, $"{ConfigurationSection}:{key} must be positive.");
    }

    /// <summary>
    /// 读取以天计的非负时长键
    /// </summary>
    /// <remarks>
    /// 缺省返回 <see langword="null"/>，不是有限的数抛出 <see cref="ArgumentException"/>，负数抛出 <see cref="ArgumentOutOfRangeException"/>，消息都带配置键与原值
    /// </remarks>
    private static TimeSpan? Days(IConfiguration configuration, string key)
    {
        var raw = configuration[$"{ConfigurationSection}:{key}"];
        if (raw is null)
        {
            return null;
        }

        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var days) || !double.IsFinite(days))
        {
            throw new ArgumentException($"{ConfigurationSection}:{key} must be a number of days, but was '{raw}'.");
        }

        return days >= 0 ? TimeSpan.FromDays(days) : throw new ArgumentOutOfRangeException($"{ConfigurationSection}:{key}", raw, $"{ConfigurationSection}:{key} must not be negative.");
    }
}
