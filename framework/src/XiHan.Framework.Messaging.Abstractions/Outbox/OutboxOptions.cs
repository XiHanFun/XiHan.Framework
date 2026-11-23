// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Outbox;

/// <summary>
/// 发件箱选项
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Messaging:Outbox</c> 显式读取
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record OutboxOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Messaging:Outbox";

    /// <summary>
    /// 轮询间隔
    /// </summary>
    /// <remarks>
    /// 默认 1 秒
    /// </remarks>
    public TimeSpan PollInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The poll interval must be positive.");
    } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 每轮最多投递条数
    /// </summary>
    /// <remarks>
    /// 默认 100，1 到 <see cref="EntityQuery.DefaultMaxTake"/>：每轮按它分页查发件箱表，查询的取出条数不能超过这个上限。也是每轮最多清理的已投递行数
    /// </remarks>
    public int BatchSize
    {
        get => field;
        init => field = value is >= 1 and <= EntityQuery.DefaultMaxTake ? value : throw new ArgumentOutOfRangeException(nameof(value), value, $"The batch size must be between 1 and {EntityQuery.DefaultMaxTake}.");
    } = 100;

    /// <summary>
    /// 一条消息最多尝试投递的次数（含首次）
    /// </summary>
    /// <remarks>
    /// 默认 10。用尽后进死信，不再投递。代理抛出与返回失败一样计一次尝试（ADR-0108）；只有调用方取消（宿主停止）的一轮回滚、不计尝试
    /// </remarks>
    public int MaxAttempts
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum number of attempts must be positive.");
    } = 10;

    /// <summary>
    /// 第一次失败后的重试间隔
    /// </summary>
    /// <remarks>
    /// 默认 5 秒。之后每失败一次翻倍，不超过 <see cref="MaxRetryDelay"/>；退避中的消息不占批次
    /// </remarks>
    public TimeSpan RetryDelay
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The retry delay must be positive.");
    } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 重试间隔的上限
    /// </summary>
    /// <remarks>
    /// 默认 5 分钟
    /// </remarks>
    public TimeSpan MaxRetryDelay
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum retry delay must be positive.");
    } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 投递成功的行保留多久
    /// </summary>
    /// <remarks>
    /// 默认 1 天，过后由投递循环删除（每轮最多 <see cref="BatchSize"/> 行）；为 0 时投递成功即在同一工作单元删除。死信行不自动删除
    /// </remarks>
    public TimeSpan DispatchedRetention
    {
        get => field;
        init => field = value >= TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The dispatched retention must not be negative.");
    } = TimeSpan.FromDays(1);

    /// <summary>
    /// 是否也投递租户分片库里的发件箱行
    /// </summary>
    /// <remarks>
    /// 默认是。装配了租户分片时，投递器在默认库之外逐个投递分片库（ADR-0211）。持久化 Provider 把发件箱行留在默认库时（SqlSugar），分片轮次查到的仍是默认库，可以关掉省去多余的查询
    /// </remarks>
    public bool IncludeShards { get; init; } = true;

    /// <summary>
    /// 分片清单多久重读一次
    /// </summary>
    /// <remarks>
    /// 默认 1 分钟。投递器从租户目录读出分片库的清单，新建的分片库最迟在这之后纳入投递
    /// </remarks>
    public TimeSpan ShardRefreshInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The shard refresh interval must be positive.");
    } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 第几次失败之后的重试间隔
    /// </summary>
    /// <remarks>
    /// <see cref="RetryDelay"/> 乘以 2 的（失败次数减一）次方，不超过 <see cref="MaxRetryDelay"/>
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
    /// 从配置读取
    /// </summary>
    /// <remarks>
    /// 键：<c>PollIntervalSeconds</c>、<c>BatchSize</c>、<c>MaxAttempts</c>、<c>RetryDelaySeconds</c>、<c>MaxRetryDelaySeconds</c>、<c>DispatchedRetentionSeconds</c>、<c>IncludeShards</c>、<c>ShardRefreshIntervalSeconds</c>；非法值抛出 <see cref="ArgumentException"/>
    /// </remarks>
    public static OutboxOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSection);
        var options = new OutboxOptions();
        if (ConfigurationValues.ReadSeconds(section, "PollIntervalSeconds") is { } interval)
        {
            options = options with { PollInterval = interval };
        }

        // 这里只核对是正整数：上界 EntityQuery.DefaultMaxTake 由属性核对
        if (ConfigurationValues.ReadInt32(section, "BatchSize", 1) is { } batch)
        {
            options = options with { BatchSize = batch };
        }

        if (ConfigurationValues.ReadInt32(section, "MaxAttempts", 1) is { } attempts)
        {
            options = options with { MaxAttempts = attempts };
        }

        if (ConfigurationValues.ReadSeconds(section, "RetryDelaySeconds") is { } retry)
        {
            options = options with { RetryDelay = retry };
        }

        if (ConfigurationValues.ReadSeconds(section, "MaxRetryDelaySeconds") is { } maxRetry)
        {
            options = options with { MaxRetryDelay = maxRetry };
        }

        if (ConfigurationValues.ReadSeconds(section, "DispatchedRetentionSeconds", allowZero: true) is { } retention)
        {
            options = options with { DispatchedRetention = retention };
        }

        if (ConfigurationValues.ReadBoolean(section, "IncludeShards") is { } shards)
        {
            options = options with { IncludeShards = shards };
        }

        if (ConfigurationValues.ReadSeconds(section, "ShardRefreshIntervalSeconds") is { } refresh)
        {
            options = options with { ShardRefreshInterval = refresh };
        }

        return options;
    }
}
