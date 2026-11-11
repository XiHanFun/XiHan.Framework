// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Redis;

/// <summary>
/// Redis Streams 消息 Provider 的选项
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Messaging:Redis</c> 与绑定 <c>queue</c> 显式读取（RFC-0016「资源绑定与配置节」）：绑定的主值覆盖 <c>Configuration</c>，绑定的属性 <c>username</c> / <c>password</c> 覆盖连接字符串里的 <c>user</c> / <c>password</c>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial record RedisMessagingOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Messaging:Redis";

    /// <summary>
    /// <see cref="BatchSize"/> 的上限
    /// </summary>
    /// <remarks>
    /// 一次取到的条目都记在本消费者的待确认列表里，取得太多会让后面的条目在处理前就空闲超时
    /// </remarks>
    public const int MaxBatchSize = 1000;

    /// <summary>
    /// StackExchange.Redis 连接字符串
    /// </summary>
    public required string Configuration
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The Redis connection string cannot be empty.", nameof(value));
    }

    /// <summary>
    /// 覆盖连接字符串里 <c>user</c> 的用户名
    /// </summary>
    /// <remarks>
    /// Redis 6 的 ACL 用户。为 <see langword="null"/> 时用连接字符串里的。<see cref="FromConfiguration"/> 从绑定 <c>queue</c> 的属性 <c>username</c> 取；连接时以 StackExchange.Redis 的选项设置覆盖，不改写连接字符串。是登录凭据的身份那一半，打印选项时与 <see cref="Password"/> 一样遮蔽（ADR-0090）
    /// </remarks>
    public string? UserName { get; init; }

    /// <summary>
    /// 覆盖连接字符串里 <c>password</c> 的密码
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 时用连接字符串里的。<see cref="FromConfiguration"/> 从绑定 <c>queue</c> 的属性 <c>password</c> 取。不进日志与错误消息
    /// </remarks>
    public string? Password { get; init; }

    /// <summary>
    /// 流键的前缀
    /// </summary>
    /// <remarks>
    /// 默认 <c>xihan:messaging:</c>。主题的流是前缀加主题，死信流再加 <c>DeadLetterSuffix</c>；发布方与订阅方要一致
    /// </remarks>
    public string KeyPrefix
    {
        get => field;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = "xihan:messaging:";

    /// <summary>
    /// 竞争订阅的消费组名
    /// </summary>
    /// <remarks>
    /// 默认 <c>xihan</c>。广播订阅的组名是它加 <c>@</c> 与实例标识。文法同 <c>MessagingOptions.InstanceId</c>：1 到 64 个字母、数字、<c>.</c>、<c>_</c>、<c>-</c>，字母或数字开头；不含 <c>@</c>，所以竞争组与广播组的名字不会混淆，维护时只清理 <c>&lt;ConsumerGroup&gt;@</c> 开头的广播组
    /// </remarks>
    public string ConsumerGroup
    {
        get => field;
        init => field = value is not null && ConsumerGroupPattern().IsMatch(value)
            ? value
            : throw new ArgumentException($"The consumer group name '{value}' must be 1 to 64 letters, digits, '.', '_' or '-', starting with a letter or digit.", nameof(value));
    } = "xihan";

    /// <summary>
    /// 每次从流里最多取的条目数
    /// </summary>
    /// <remarks>
    /// 默认 10，1 到 <see cref="MaxBatchSize"/>。一批条目在取到时就记进本消费者的待确认列表，<see cref="ClaimIdleTime"/> 要大于整批的最长处理时间
    /// </remarks>
    public int BatchSize
    {
        get => field;
        init => field = value is >= 1 and <= MaxBatchSize ? value : throw new ArgumentOutOfRangeException(nameof(value), value, $"The batch size must be between 1 and {MaxBatchSize}.");
    } = 10;

    /// <summary>
    /// 没有取到新条目时再取之前的等待
    /// </summary>
    /// <remarks>
    /// 默认 200 毫秒。StackExchange.Redis 的多路复用连接不支持阻塞读，消费是轮询：空闲时每个订阅每个周期一次往返，消息最多晚这么久被取到
    /// </remarks>
    public TimeSpan PollInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The poll interval must be positive.");
    } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// 待确认条目空闲多久后可被回收
    /// </summary>
    /// <remarks>
    /// 默认 60 秒。超过它没有确认的条目（实例崩溃、停止时没处理完、死信写入失败）由维护时的 <c>XAUTOCLAIM</c> 认领并接着投递；空闲超过它的消费者与广播组在维护时被清理。必须大于一批条目的最长处理时间，否则还在处理的条目会被别的实例认领、重复处理。维护间隔是它与 10 秒中较小的一个
    /// </remarks>
    public TimeSpan ClaimIdleTime
    {
        get => field;
        init => field = value >= TimeSpan.FromMilliseconds(1) ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The claim idle time must be at least one millisecond.");
    } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 每条流的近似长度上限
    /// </summary>
    /// <remarks>
    /// 默认 100000。发布与死信都以 <c>MAXLEN ~</c> 裁剪，维护时再去掉全部组都确认过的条目，流里只留积压；消费者落后超过它时最旧的条目在投递前被裁掉
    /// </remarks>
    public long MaxLength
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum stream length must be positive.");
    } = 100_000;

    /// <summary>
    /// 健康项 <c>messaging.redis.connection</c> 的重探间隔
    /// </summary>
    /// <remarks>
    /// 默认 15 秒，也是单次探测的超时
    /// </remarks>
    public TimeSpan HealthProbeInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The health probe interval must be positive.");
    } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// 从配置与绑定读取
    /// </summary>
    /// <remarks>
    /// <c>Configuration</c>（绑定 <c>queue</c> 的主值优先于配置，RFC-0016；绑定的属性 <c>username</c> / <c>password</c> 各自覆盖连接字符串里的 <c>user</c> / <c>password</c>）、<c>KeyPrefix</c>、<c>ConsumerGroup</c>、<c>BatchSize</c>、<c>PollIntervalSeconds</c>、<c>ClaimIdleTimeSeconds</c>、<c>MaxLength</c>、<c>HealthProbeIntervalSeconds</c>；非法值抛出 <see cref="ArgumentException"/>
    /// </remarks>
    public static RedisMessagingOptions FromConfiguration(IConfiguration configuration, Maybe<BindingValue> binding)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        // 绑定来自部署环境（Service Binding），给出的连接与凭据逐项覆盖配置节里的同类键（RFC-0016「资源绑定与配置节」），其余键照读配置节；
        // 空白的主值与属性不算给出。连接两者都没有直接报错而不是静默退回
        var connection = (binding.TryGetValue(out var bound) && !string.IsNullOrWhiteSpace(bound.Value) ? bound.Value : null)
            ?? configuration[$"{ConfigurationSection}:Configuration"]
            ?? throw new ArgumentException($"No Redis connection is configured for messaging: provide the 'queue' binding or set {ConfigurationSection}:Configuration.");

        var options = new RedisMessagingOptions
        {
            Configuration = connection,
            UserName = BoundText(binding, "username"),
            Password = BoundText(binding, "password"),
        };
        if (configuration[$"{ConfigurationSection}:KeyPrefix"] is { } prefix)
        {
            options = options with { KeyPrefix = prefix };
        }

        if (configuration[$"{ConfigurationSection}:ConsumerGroup"] is { } group)
        {
            options = ConsumerGroupPattern().IsMatch(group)
                ? options with { ConsumerGroup = group }
                : throw new ArgumentException($"{ConfigurationSection}:ConsumerGroup must be 1 to 64 letters, digits, '.', '_' or '-', starting with a letter or digit, but was '{group}'.");
        }

        var batch = configuration[$"{ConfigurationSection}:BatchSize"];
        if (batch is not null)
        {
            options = int.TryParse(batch, NumberStyles.None, CultureInfo.InvariantCulture, out var size) && size is >= 1 and <= MaxBatchSize
                ? options with { BatchSize = size }
                : throw new ArgumentException($"{ConfigurationSection}:BatchSize must be an integer between 1 and {MaxBatchSize}, but was '{batch}'.");
        }

        if (ReadSeconds(configuration, "PollIntervalSeconds") is { } poll)
        {
            options = options with { PollInterval = poll };
        }

        if (ReadSeconds(configuration, "ClaimIdleTimeSeconds") is { } claim)
        {
            options = claim >= TimeSpan.FromMilliseconds(1)
                ? options with { ClaimIdleTime = claim }
                : throw new ArgumentException($"{ConfigurationSection}:ClaimIdleTimeSeconds must be at least 0.001.");
        }

        var length = configuration[$"{ConfigurationSection}:MaxLength"];
        if (length is not null)
        {
            options = long.TryParse(length, NumberStyles.None, CultureInfo.InvariantCulture, out var maximum) && maximum >= 1
                ? options with { MaxLength = maximum }
                : throw new ArgumentException($"{ConfigurationSection}:MaxLength must be a positive integer, but was '{length}'.");
        }

        if (ReadSeconds(configuration, "HealthProbeIntervalSeconds") is { } probe)
        {
            options = options with { HealthProbeInterval = probe };
        }

        return options;
    }

    /// <summary>
    /// 读一个以秒为单位的正数键
    /// </summary>
    /// <remarks>
    /// 没有这个键为 <see langword="null"/>；不是正数抛出 <see cref="ArgumentException"/>
    /// </remarks>
    private static TimeSpan? ReadSeconds(IConfiguration configuration, string key)
    {
        var text = configuration[$"{ConfigurationSection}:{key}"];
        if (text is null)
        {
            return null;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0 && seconds <= TimeSpan.MaxValue.TotalSeconds
            ? TimeSpan.FromSeconds(seconds)
            : throw new ArgumentException($"{ConfigurationSection}:{key} must be a positive number, but was '{text}'.");
    }

    /// <summary>
    /// 读绑定的一个非空属性
    /// </summary>
    /// <remarks>
    /// 没有绑定、没有该属性或属性空白为 <see langword="null"/>
    /// </remarks>
    private static string? BoundText(Maybe<BindingValue> binding, string key)
        => binding.TryGetValue(out var bound) && bound.Property(key).TryGetValue(out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    /// <summary>
    /// 打印成员，凭据以 <see cref="Redaction.Mask"/> 代替
    /// </summary>
    /// <remarks>
    /// 记录的 <c>ToString()</c> 经它输出：对象可能整个进日志与异常消息，连接字符串（可能含 <c>password=</c>）、用户名与口令不能明文出现（RFC-0014「选项里的凭据」，ADR-0090）。其余成员同编译器生成的写法；新加属性时在这里一并补上，架构测试 <c>CredentialRedactionTests</c> 会核对
    /// </remarks>
    private bool PrintMembers(StringBuilder builder)
    {
        // 凭据只打印遮蔽文本，null 仍打印为空：看得出有没有配置，看不到值
        builder.Append("Configuration = ").Append(Redaction.Redact(Configuration))
            .Append(", UserName = ").Append(Redaction.Redact(UserName))
            .Append(", Password = ").Append(Redaction.Redact(Password))
            .Append(", KeyPrefix = ").Append(KeyPrefix)
            .Append(", ConsumerGroup = ").Append(ConsumerGroup)
            .Append(", BatchSize = ").Append(BatchSize)
            .Append(", PollInterval = ").Append(PollInterval)
            .Append(", ClaimIdleTime = ").Append(ClaimIdleTime)
            .Append(", MaxLength = ").Append(MaxLength)
            .Append(", HealthProbeInterval = ").Append(HealthProbeInterval);
        return true;
    }

    /// <summary>
    /// 消费组名的正则：字母或数字开头，共 1 到 64 个字符，不含 <c>@</c>
    /// </summary>
    /// <remarks>
    /// 结尾用 <c>\z</c> 而不是 <c>$</c>：<c>$</c> 还匹配结尾换行之前的位置，从文件或环境变量读进来的值带着换行也会被放过
    /// </remarks>
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}\z")]
    private static partial Regex ConsumerGroupPattern();
}
