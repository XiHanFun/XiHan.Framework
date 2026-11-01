// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Kafka;

/// <summary>
/// Kafka Provider 的选项。
/// </summary>
/// <remarks>
/// 配置节 <c>XiHan:Messaging:Kafka</c>：<c>BootstrapServers</c>（绑定 <c>stream</c> 的主值优先）、<c>GroupId</c>、<c>TopicPrefix</c>、<c>HealthProbeIntervalSeconds</c>、<c>Properties:&lt;key&gt;</c>（透传给 librdkafka 的配置，如 <c>security.protocol</c>、<c>sasl.mechanism</c>；绑定的属性 <c>security.protocol</c>、<c>sasl.mechanism</c>、<c>sasl.username</c>、<c>sasl.password</c> 覆盖同名键）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record KafkaMessagingOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Messaging:Kafka";

    /// <summary>
    /// 绑定 <c>stream</c> 里覆盖 <see cref="Properties"/> 同名键的属性。
    /// </summary>
    /// <remarks>
    /// 只认 SASL 的凭据与用法（RFC-0016「资源绑定与配置节」）；绑定目录里的其余文件（<c>type</c>、<c>provider</c>、<c>host</c> 等）不是 librdkafka 的配置项，透传会让客户端拒绝启动。
    /// </remarks>
    private static readonly string[] _boundProperties = ["security.protocol", "sasl.mechanism", "sasl.username", "sasl.password"];

    /// <summary>
    /// 引导地址，逗号分隔的 <c>host:port</c>。
    /// </summary>
    public required string BootstrapServers
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The bootstrap servers must not be empty.", nameof(value));
    }

    /// <summary>
    /// 消费组。
    /// </summary>
    /// <remarks>
    /// 默认 <c>xihan</c>。同一消费组的实例分摊分区。
    /// </remarks>
    public string GroupId
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The group ID must not be empty.", nameof(value));
    } = "xihan";

    /// <summary>
    /// 主题前缀。
    /// </summary>
    /// <remarks>
    /// 默认 <c>xihan.</c>。
    /// </remarks>
    public string TopicPrefix { get; init; } = "xihan.";

    /// <summary>
    /// 透传给 librdkafka 的配置（生产者与消费者共用）。
    /// </summary>
    /// <remarks>
    /// 如 <c>security.protocol</c>、<c>sasl.mechanism</c>、<c>sasl.username</c>、<c>ssl.ca.location</c>。
    /// </remarks>
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// 健康项 <c>messaging.kafka.connection</c> 的重探间隔（也是单次探测的超时）。
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
    /// 投递没有完成时重新投递前的等待。
    /// </summary>
    /// <remarks>
    /// 默认 5 秒。死信发布没有得到确认（或投递因代理不可达等异常没有完成）时，消费者不提交位点、回退到这条消息，等这么久再重新投递，期间该分区不往后读。
    /// </remarks>
    public TimeSpan RedeliveryDelay
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The redelivery delay must be positive.");
    } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 从配置与绑定读取。
    /// </summary>
    /// <remarks>
    /// <c>BootstrapServers</c>（绑定 <c>stream</c> 的主值优先于配置，RFC-0016）、<c>GroupId</c>、<c>TopicPrefix</c>、<c>HealthProbeIntervalSeconds</c>、<c>RedeliveryDelaySeconds</c>、<c>Properties:*</c>；绑定的属性 <c>security.protocol</c>、<c>sasl.mechanism</c>、<c>sasl.username</c>、<c>sasl.password</c> 各自覆盖 <c>Properties</c> 里的同名键，空白的不算给出。
    /// </remarks>
    public static KafkaMessagingOptions FromConfiguration(IConfiguration configuration, Maybe<BindingValue> binding)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        // 绑定来自部署环境（Service Binding），给出的引导地址覆盖配置节里的同一个键（RFC-0016「资源绑定与配置节」），其余键照读配置节；
        // 主值为空的绑定不算给出了地址
        var servers = (binding.TryGetValue(out var bound) && !string.IsNullOrWhiteSpace(bound.Value) ? bound.Value : null)
            ?? configuration[$"{ConfigurationSection}:BootstrapServers"]
            ?? throw new ArgumentException($"No Kafka bootstrap servers are configured: provide the 'stream' binding or set {ConfigurationSection}:BootstrapServers.");
        var options = new KafkaMessagingOptions { BootstrapServers = servers };
        if (configuration[$"{ConfigurationSection}:GroupId"] is { } group)
        {
            options = options with { GroupId = group };
        }

        if (configuration[$"{ConfigurationSection}:TopicPrefix"] is { } prefix)
        {
            options = options with { TopicPrefix = prefix };
        }

        var probe = configuration[$"{ConfigurationSection}:HealthProbeIntervalSeconds"];
        if (probe is not null)
        {
            options = double.TryParse(probe, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? options with { HealthProbeInterval = TimeSpan.FromSeconds(seconds) }
                : throw new ArgumentException($"{ConfigurationSection}:HealthProbeIntervalSeconds must be a positive number, but was '{probe}'.");
        }

        var redelivery = configuration[$"{ConfigurationSection}:RedeliveryDelaySeconds"];
        if (redelivery is not null)
        {
            options = double.TryParse(redelivery, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? options with { RedeliveryDelay = TimeSpan.FromSeconds(seconds) }
                : throw new ArgumentException($"{ConfigurationSection}:RedeliveryDelaySeconds must be a positive number, but was '{redelivery}'.");
        }

        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var child in configuration.GetSection($"{ConfigurationSection}:Properties").GetChildren())
        {
            if (child.Value is { } value)
            {
                properties[child.Key] = value;
            }
        }

        // 绑定给出的 SASL 凭据与用法逐项覆盖配置节的同名键（RFC-0016）：部署环境换成带认证的集群时不必改应用配置
        if (binding.TryGetValue(out var credentials))
        {
            foreach (var key in _boundProperties)
            {
                if (credentials.Property(key).TryGetValue(out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    properties[key] = value;
                }
            }
        }

        return properties.Count == 0 ? options : options with { Properties = properties };
    }
}
