// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.CloudEvents;

/// <summary>
/// CloudEvents 协议适配器选项。从配置节 <c>XiHan:Protocols:CloudEvents</c> 显式读取：<c>Inbound</c>（<c>Dispatch</c> / <c>Publish</c>）、<c>Outbound</c>（<c>Binary</c> / <c>Structured</c>），
/// <c>Topics:&lt;事件类型&gt;</c> = 主题，<c>Webhooks:&lt;主题&gt;</c> = 出站 URL。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record CloudEventsProtocolOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Protocols:CloudEvents";

    /// <summary>
    /// 入站事件的处理方式；默认在请求内派发。
    /// </summary>
    public CloudEventsInboundMode Inbound { get; init; } = CloudEventsInboundMode.Dispatch;

    /// <summary>
    /// 出站 Webhook 的内容模式；默认 binary，不能是 batch。
    /// </summary>
    public CloudEventsContentMode Outbound
    {
        get => field;
        init
        {
            if (value == CloudEventsContentMode.Batch)
            {
                throw new ArgumentException("Batch mode is only used for inbound events.", nameof(value));
            }

            field = value;
        }
    } = CloudEventsContentMode.Binary;

    /// <summary>
    /// 事件类型 → 主题；没有映射的类型在其本身是合法主题时直接作为主题。
    /// </summary>
    public IReadOnlyDictionary<string, string> Topics
    {
        get => field;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            foreach (var topic in value.Values)
            {
                TopicName.Validate(topic);
            }

            field = value;
        }
    } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// 主题 → 出站 Webhook 的绝对 HTTP(S) URL。
    /// </summary>
    public IReadOnlyDictionary<string, Uri> Webhooks
    {
        get => field;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            foreach (var (topic, url) in value)
            {
                TopicName.Validate(topic);
                if (!url.IsAbsoluteUri || url.Scheme is not ("http" or "https"))
                {
                    throw new ArgumentException($"Webhook URL '{url}' for topic '{topic}' must be an absolute http or https URL.", nameof(value));
                }
            }

            field = value;
        }
    } = new Dictionary<string, Uri>(StringComparer.Ordinal);

    /// <summary>
    /// 从配置读取；枚举值不合法时抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public static CloudEventsProtocolOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSection);
        var topics = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var child in section.GetSection("Topics").GetChildren())
        {
            if (child.Value is { } topic)
            {
                topics[child.Key] = topic;
            }
        }

        var webhooks = new Dictionary<string, Uri>(StringComparer.Ordinal);
        foreach (var child in section.GetSection("Webhooks").GetChildren())
        {
            if (child.Value is { } url)
            {
                webhooks[child.Key] = Uri.TryCreate(url, UriKind.Absolute, out var parsed) ? parsed : throw new ArgumentException($"Webhook URL '{url}' for topic '{child.Key}' is not an absolute URL.", nameof(configuration));
            }
        }

        return new CloudEventsProtocolOptions
        {
            Inbound = ParseEnum<CloudEventsInboundMode>(section["Inbound"], "Inbound"),
            Outbound = ParseEnum<CloudEventsContentMode>(section["Outbound"], "Outbound"),
            Topics = topics,
            Webhooks = webhooks,
        };
    }

    /// <summary>
    /// 事件类型对应的主题：显式映射优先，否则类型本身须是合法主题。
    /// </summary>
    public Maybe<string> TopicFor(string type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (Topics.TryGetValue(type, out var topic))
        {
            return topic;
        }

        return TopicName.IsValid(type) ? type : default(Maybe<string>);
    }

    /// <summary>
    /// 读枚举配置值；缺失用默认值，不合法抛出。
    /// </summary>
    private static TEnum ParseEnum<TEnum>(string? text, string key)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return default;
        }

        return Enum.TryParse<TEnum>(text, ignoreCase: true, out var value) && Enum.IsDefined(value)
            ? value
            : throw new ArgumentException($"'{ConfigurationSection}:{key}' value '{text}' is not one of {string.Join(", ", Enum.GetNames<TEnum>())}.", key);
    }
}
