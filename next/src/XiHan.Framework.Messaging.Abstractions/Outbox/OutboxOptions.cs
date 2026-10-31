// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Outbox;

/// <summary>
/// 发件箱选项。
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Messaging:Outbox</c> 显式读取。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record OutboxOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Messaging:Outbox";

    /// <summary>
    /// 轮询间隔。
    /// </summary>
    /// <remarks>
    /// 默认 1 秒。
    /// </remarks>
    public TimeSpan PollInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The poll interval must be positive.");
    } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 每轮最多投递条数。
    /// </summary>
    /// <remarks>
    /// 默认 100。也是每轮最多清理的已投递行数。
    /// </remarks>
    public int BatchSize
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The batch size must be positive.");
    } = 100;

    /// <summary>
    /// 一条消息最多尝试投递的次数（含首次）。
    /// </summary>
    /// <remarks>
    /// 默认 10。用尽后进死信，不再投递；代理整批抛出（不可达）的一轮回滚，不计尝试。
    /// </remarks>
    public int MaxAttempts
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum number of attempts must be positive.");
    } = 10;

    /// <summary>
    /// 第一次失败后的重试间隔。
    /// </summary>
    /// <remarks>
    /// 默认 5 秒。之后每失败一次翻倍，不超过 <see cref="MaxRetryDelay"/>；退避中的消息不占批次。
    /// </remarks>
    public TimeSpan RetryDelay
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The retry delay must be positive.");
    } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 重试间隔的上限。
    /// </summary>
    /// <remarks>
    /// 默认 5 分钟。
    /// </remarks>
    public TimeSpan MaxRetryDelay
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum retry delay must be positive.");
    } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 投递成功的行保留多久。
    /// </summary>
    /// <remarks>
    /// 默认 1 天，过后由投递循环删除（每轮最多 <see cref="BatchSize"/> 行）；为 0 时投递成功即在同一工作单元删除。死信行不自动删除。
    /// </remarks>
    public TimeSpan DispatchedRetention
    {
        get => field;
        init => field = value >= TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The dispatched retention must not be negative.");
    } = TimeSpan.FromDays(1);

    /// <summary>
    /// 第几次失败之后的重试间隔。
    /// </summary>
    /// <remarks>
    /// <see cref="RetryDelay"/> 乘以 2 的（失败次数减一）次方，不超过 <see cref="MaxRetryDelay"/>。
    /// </remarks>
    public TimeSpan RetryDelayAfter(int attempts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempts, 1);
        // 指数到 30 次时已远超任何合理上限，截断指数避免溢出
        var factor = Math.Pow(2, Math.Min(attempts - 1, 30));
        var ticks = RetryDelay.Ticks * factor;
        return ticks >= MaxRetryDelay.Ticks ? MaxRetryDelay : TimeSpan.FromTicks((long)ticks);
    }

    /// <summary>
    /// 从配置读取。
    /// </summary>
    /// <remarks>
    /// 键：<c>PollIntervalSeconds</c>、<c>BatchSize</c>、<c>MaxAttempts</c>、<c>RetryDelaySeconds</c>、<c>MaxRetryDelaySeconds</c>、<c>DispatchedRetentionSeconds</c>；非法值抛出 <see cref="ArgumentException"/>。
    /// </remarks>
    public static OutboxOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new OutboxOptions();
        if (ReadSeconds(configuration, "PollIntervalSeconds", allowZero: false) is { } interval)
        {
            options = options with { PollInterval = interval };
        }

        if (ReadPositive(configuration, "BatchSize") is { } batch)
        {
            options = options with { BatchSize = batch };
        }

        if (ReadPositive(configuration, "MaxAttempts") is { } attempts)
        {
            options = options with { MaxAttempts = attempts };
        }

        if (ReadSeconds(configuration, "RetryDelaySeconds", allowZero: false) is { } retry)
        {
            options = options with { RetryDelay = retry };
        }

        if (ReadSeconds(configuration, "MaxRetryDelaySeconds", allowZero: false) is { } maxRetry)
        {
            options = options with { MaxRetryDelay = maxRetry };
        }

        if (ReadSeconds(configuration, "DispatchedRetentionSeconds", allowZero: true) is { } retention)
        {
            options = options with { DispatchedRetention = retention };
        }

        return options;
    }

    /// <summary>
    /// 读取配置节下的秒数。
    /// </summary>
    /// <remarks>
    /// 未配置返回 <see langword="null"/>，不是正数（允许 0 时不是非负数）抛出 <see cref="ArgumentException"/>。
    /// </remarks>
    private static TimeSpan? ReadSeconds(IConfiguration configuration, string name, bool allowZero)
    {
        var raw = configuration[$"{ConfigurationSection}:{name}"];
        if (raw is null)
        {
            return null;
        }

        // 用不变区域性解析，避免小数点随宿主区域设置变化
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && (seconds > 0 || (allowZero && seconds == 0))
            ? TimeSpan.FromSeconds(seconds)
            : throw new ArgumentException($"{ConfigurationSection}:{name} must be a {(allowZero ? "non-negative" : "positive")} number, but was '{raw}'.");
    }

    /// <summary>
    /// 读取配置节下的正整数。
    /// </summary>
    /// <remarks>
    /// 未配置返回 <see langword="null"/>，不是正整数抛出 <see cref="ArgumentException"/>。
    /// </remarks>
    private static int? ReadPositive(IConfiguration configuration, string name)
    {
        var raw = configuration[$"{ConfigurationSection}:{name}"];
        if (raw is null)
        {
            return null;
        }

        return int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 1
            ? value
            : throw new ArgumentException($"{ConfigurationSection}:{name} must be a positive integer, but was '{raw}'.");
    }
}
