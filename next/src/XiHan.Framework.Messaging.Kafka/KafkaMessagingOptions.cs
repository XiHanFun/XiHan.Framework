// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Kafka;

/// <summary>
/// Kafka Provider 的选项。配置节 <c>XiHan:Messaging:Kafka</c>：<c>BootstrapServers</c>（或绑定 <c>stream</c> 的值）、<c>GroupId</c>、<c>TopicPrefix</c>、<c>Properties:&lt;key&gt;</c>（透传给 librdkafka 的配置，如 <c>security.protocol</c>、<c>sasl.mechanism</c>）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record KafkaMessagingOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Messaging:Kafka";

    /// <summary>
    /// 引导地址，逗号分隔的 <c>host:port</c>。
    /// </summary>
    public required string BootstrapServers
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The bootstrap servers must not be empty.", nameof(value));
    }

    /// <summary>
    /// 消费组；默认 <c>xihan</c>。同一消费组的实例分摊分区。
    /// </summary>
    public string GroupId
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The group ID must not be empty.", nameof(value));
    } = "xihan";

    /// <summary>
    /// 主题前缀；默认 <c>xihan.</c>。
    /// </summary>
    public string TopicPrefix { get; init; } = "xihan.";

    /// <summary>
    /// 透传给 librdkafka 的配置（生产者与消费者共用），如 <c>security.protocol</c>、<c>sasl.mechanism</c>、<c>sasl.username</c>、<c>ssl.ca.location</c>。
    /// </summary>
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// 从配置与绑定读取：<c>BootstrapServers</c>（配置优先于绑定值）、<c>GroupId</c>、<c>TopicPrefix</c>、<c>Properties:*</c>。
    /// </summary>
    public static KafkaMessagingOptions FromConfiguration(IConfiguration configuration, Maybe<BindingValue> binding)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var servers = configuration[$"{ConfigurationSection}:BootstrapServers"]
            ?? (binding.TryGetValue(out var bound) ? bound.Value : null)
            ?? throw new ArgumentException($"No Kafka bootstrap servers are configured: set {ConfigurationSection}:BootstrapServers or provide the 'stream' binding.");
        var options = new KafkaMessagingOptions { BootstrapServers = servers };
        if (configuration[$"{ConfigurationSection}:GroupId"] is { } group)
        {
            options = options with { GroupId = group };
        }

        if (configuration[$"{ConfigurationSection}:TopicPrefix"] is { } prefix)
        {
            options = options with { TopicPrefix = prefix };
        }

        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var child in configuration.GetSection($"{ConfigurationSection}:Properties").GetChildren())
        {
            if (child.Value is { } value)
            {
                properties[child.Key] = value;
            }
        }

        return properties.Count == 0 ? options : options with { Properties = properties };
    }
}
