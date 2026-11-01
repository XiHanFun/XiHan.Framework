// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.RabbitMQ;

/// <summary>
/// RabbitMQ Provider 选项。
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Messaging:RabbitMQ</c> 与绑定 <c>queue</c> 显式读取（RFC-0016「资源绑定与配置节」）：绑定的主值覆盖 <c>Configuration</c>，绑定的属性 <c>username</c> / <c>password</c> 覆盖 URI 里的用户信息。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record RabbitMQMessagingOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Messaging:RabbitMQ";

    /// <summary>
    /// AMQP URI（如 <c>amqp://guest:guest@localhost:5672</c>）。
    /// </summary>
    public required string Configuration
    {
        get => field;
        init => field = Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "amqp" or "amqps"
            ? value
            : throw new ArgumentException("The RabbitMQ connection must be an 'amqp://' or 'amqps://' URI.", nameof(value));
    }

    /// <summary>
    /// 覆盖 <see cref="Configuration"/> 里用户名的用户名。
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 时用 URI 用户信息里的。<see cref="FromConfiguration"/> 从绑定 <c>queue</c> 的属性 <c>username</c> 取；连接时以客户端的用户名设置覆盖，不改写 URI（不必为特殊字符转义）。
    /// </remarks>
    public string? UserName { get; init; }

    /// <summary>
    /// 覆盖 <see cref="Configuration"/> 里密码的密码。
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 时用 URI 用户信息里的。<see cref="FromConfiguration"/> 从绑定 <c>queue</c> 的属性 <c>password</c> 取。不进日志与错误消息。
    /// </remarks>
    public string? Password { get; init; }

    /// <summary>
    /// 队列名与路由键的前缀。
    /// </summary>
    /// <remarks>
    /// 默认 <c>xihan.</c>。竞争订阅的队列与主题的路由键都是前缀加主题，前缀不同的应用共用一个交换机也互不串扰。
    /// </remarks>
    public string QueuePrefix { get; init; } = "xihan.";

    /// <summary>
    /// 发布用的交换机名。
    /// </summary>
    /// <remarks>
    /// 默认 <c>xihan</c>：持久的 direct 交换机，发布方与订阅方都声明它，订阅方的队列以路由键绑定到它（ADR-0079）。不能为空，不能以保留前缀 <c>amq.</c> 开头。
    /// </remarks>
    public string Exchange
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) && !value.StartsWith("amq.", StringComparison.Ordinal)
            ? value
            : throw new ArgumentException("The exchange name must not be empty or start with the reserved prefix 'amq.'.", nameof(value));
    } = "xihan";

    /// <summary>
    /// 每个消费者的预取数。
    /// </summary>
    /// <remarks>
    /// 默认 16。
    /// </remarks>
    public ushort PrefetchCount
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The prefetch count must be positive.");
    } = 16;

    /// <summary>
    /// 健康项 <c>messaging.rabbitmq.connection</c> 的重探间隔（也是单次探测的超时）。
    /// </summary>
    /// <remarks>
    /// 默认 15 秒。
    /// </remarks>
    public TimeSpan HealthProbeInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The health probe interval must be positive.");
    } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// 从配置与绑定读取。
    /// </summary>
    /// <remarks>
    /// <c>Configuration</c>（绑定 <c>queue</c> 的主值优先于配置，RFC-0016；绑定的属性 <c>username</c> / <c>password</c> 各自覆盖 URI 里的用户名与密码）、<c>QueuePrefix</c>、<c>Exchange</c>、<c>PrefetchCount</c>、<c>HealthProbeIntervalSeconds</c>。
    /// </remarks>
    public static RabbitMQMessagingOptions FromConfiguration(IConfiguration configuration, Maybe<BindingValue> binding)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        // 绑定来自部署环境（Service Binding），给出的连接与凭据逐项覆盖配置节里的同类键（RFC-0016「资源绑定与配置节」），其余键照读配置节；
        // 空白的主值与属性不算给出。凭据只记在选项里、连接时设置，报错不带 URI 与凭据
        var connection = (binding.TryGetValue(out var bound) && !string.IsNullOrWhiteSpace(bound.Value) ? bound.Value : null)
            ?? configuration[$"{ConfigurationSection}:Configuration"]
            ?? throw new ArgumentException($"No RabbitMQ connection is configured: provide the 'queue' binding or set {ConfigurationSection}:Configuration.");

        var options = new RabbitMQMessagingOptions
        {
            Configuration = connection,
            UserName = BoundText(binding, "username"),
            Password = BoundText(binding, "password"),
        };
        if (configuration[$"{ConfigurationSection}:QueuePrefix"] is { } prefix)
        {
            options = options with { QueuePrefix = prefix };
        }

        if (configuration[$"{ConfigurationSection}:Exchange"] is { } exchange)
        {
            options = options with { Exchange = exchange };
        }

        var prefetch = configuration[$"{ConfigurationSection}:PrefetchCount"];
        if (prefetch is not null)
        {
            options = ushort.TryParse(prefetch, NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count >= 1
                ? options with { PrefetchCount = count }
                : throw new ArgumentException($"{ConfigurationSection}:PrefetchCount must be an integer between 1 and 65535, but was '{prefetch}'.");
        }

        var probe = configuration[$"{ConfigurationSection}:HealthProbeIntervalSeconds"];
        if (probe is not null)
        {
            options = double.TryParse(probe, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? options with { HealthProbeInterval = TimeSpan.FromSeconds(seconds) }
                : throw new ArgumentException($"{ConfigurationSection}:HealthProbeIntervalSeconds must be a positive number, but was '{probe}'.");
        }

        return options;
    }

    /// <summary>
    /// 读绑定的一个非空属性。
    /// </summary>
    /// <remarks>
    /// 没有绑定、没有该属性或属性空白为 <see langword="null"/>。
    /// </remarks>
    private static string? BoundText(Maybe<BindingValue> binding, string key)
        => binding.TryGetValue(out var bound) && bound.Property(key).TryGetValue(out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    /// <summary>
    /// 打印成员，凭据以 <see cref="Redaction.Mask"/> 代替。
    /// </summary>
    /// <remarks>
    /// 记录的 <c>ToString()</c> 经它输出：对象可能整个进日志与异常消息，AMQP URI（用户信息里可能有口令）与口令不能明文出现（RFC-0014「选项里的凭据」）。其余成员同编译器生成的写法；新加属性时在这里一并补上，架构测试 <c>CredentialRedactionTests</c> 会核对。
    /// </remarks>
    private bool PrintMembers(StringBuilder builder)
    {
        // 凭据只打印遮蔽文本，null 仍打印为空：看得出有没有配置，看不到值
        builder.Append("Configuration = ").Append(Redaction.Redact(Configuration))
            .Append(", UserName = ").Append(UserName)
            .Append(", Password = ").Append(Redaction.Redact(Password))
            .Append(", QueuePrefix = ").Append(QueuePrefix)
            .Append(", Exchange = ").Append(Exchange)
            .Append(", PrefetchCount = ").Append(PrefetchCount)
            .Append(", HealthProbeInterval = ").Append(HealthProbeInterval);
        return true;
    }
}
