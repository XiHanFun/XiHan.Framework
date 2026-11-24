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
    /// 默认是。装配了租户分片时，投递器在默认库之外逐个投递分片库（ADR-0211），健康项与死信重投覆盖同一批库（ADR-0272）。持久化 Provider 把发件箱行留在默认库时（SqlSugar），分片轮次查到的仍是默认库：投递只多出查询，健康项却会把默认库的行按库数重复计数，这时应当关掉
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
    /// 健康项 <c>messaging.outbox.backlog</c> 的重探间隔（也是单次探测的超时）
    /// </summary>
    /// <remarks>
    /// 默认 15 秒。每次探测在默认库与每个分片库上各数一遍发件箱行（<see cref="IncludeShards"/>）
    /// </remarks>
    public TimeSpan HealthProbeInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The health probe interval must be positive.");
    } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// 未投递的行数超过多少时健康项降级
    /// </summary>
    /// <remarks>
    /// 默认 1000。未投递指既没有投递成功、也没有进死信的行，含退避中的；各库相加
    /// </remarks>
    public int HealthMaxPending
    {
        get => field;
        init => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The pending threshold must not be negative.");
    } = 1000;

    /// <summary>
    /// 最老的一条未投递的行等了多久之后健康项降级
    /// </summary>
    /// <remarks>
    /// 默认 5 分钟，从行的创建时间算起，取各库里最老的一条。正常情况下一条消息在一个轮询间隔内发出；代理不可达或某条消息一直失败时，它在进死信之前一直算未投递
    /// </remarks>
    public TimeSpan HealthMaxPendingAge
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The pending age threshold must be positive.");
    } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 死信行数超过多少时健康项降级
    /// </summary>
    /// <remarks>
    /// 默认 0：有死信就降级，直到它们被重投（<see cref="OutboxDeadLetters.RedriveManyAsync"/>）或删除。死信行不自动删除，各库相加
    /// </remarks>
    public int HealthMaxDeadLettered
    {
        get => field;
        init => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The dead-letter threshold must not be negative.");
    }

    /// <summary>
    /// 第几次失败之后的重试间隔
    /// </summary>
    /// <remarks>
    /// <see cref="RetryDelay"/> 乘以 2 的（失败次数减一）次方，不超过 <see cref="MaxRetryDelay"/>：次数再多、上限再大都饱和在上限，不溢出（RFC-0009「发件箱」，ADR-0283；以前把指数截在 30，上限大于 <see cref="RetryDelay"/> 的 2 的 30 次方倍时到不了上限）
    /// </remarks>
    public TimeSpan RetryDelayAfter(int attempts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempts, 1);
        var delay = RetryDelay.Ticks;
        var limit = MaxRetryDelay.Ticks;
        var doublings = attempts - 1;

        // 按整数刻度算，不经浮点，也不截断指数：间隔翻 doublings 倍超过上限，当且仅当间隔大于上限右移 doublings 位（向下取整）
        // 63 次及以上的翻倍必然超出 64 位整数能装下的任何上限；这样判断之后下面的左移不会溢出
        if (doublings >= 63 || delay > limit >> doublings)
        {
            return MaxRetryDelay;
        }

        return TimeSpan.FromTicks(delay << doublings);
    }

    /// <summary>
    /// 从配置读取
    /// </summary>
    /// <remarks>
    /// 键：<c>PollIntervalSeconds</c>、<c>BatchSize</c>、<c>MaxAttempts</c>、<c>RetryDelaySeconds</c>、<c>MaxRetryDelaySeconds</c>、<c>DispatchedRetentionSeconds</c>、<c>IncludeShards</c>、<c>ShardRefreshIntervalSeconds</c>、<c>HealthProbeIntervalSeconds</c>、<c>HealthMaxPending</c>、<c>HealthMaxPendingAgeSeconds</c>、<c>HealthMaxDeadLettered</c>；非法值抛出 <see cref="ArgumentException"/>
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

        if (ConfigurationValues.ReadSeconds(section, "HealthProbeIntervalSeconds") is { } probe)
        {
            options = options with { HealthProbeInterval = probe };
        }

        if (ConfigurationValues.ReadInt32(section, "HealthMaxPending", 0) is { } maxPending)
        {
            options = options with { HealthMaxPending = maxPending };
        }

        if (ConfigurationValues.ReadSeconds(section, "HealthMaxPendingAgeSeconds") is { } maxAge)
        {
            options = options with { HealthMaxPendingAge = maxAge };
        }

        if (ConfigurationValues.ReadInt32(section, "HealthMaxDeadLettered", 0) is { } maxDeadLettered)
        {
            options = options with { HealthMaxDeadLettered = maxDeadLettered };
        }

        return options;
    }
}
