// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Protocols.CloudEvents.Inbound;

namespace XiHan.Framework.Protocols.CloudEvents;

/// <summary>
/// CloudEvents 协议适配器选项
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Protocols:CloudEvents</c> 显式读取：<c>Inbound</c>（<c>Dispatch</c> / <c>Publish</c>）、<c>Outbound</c>（<c>Binary</c> / <c>Structured</c>），<c>Topics:&lt;事件类型&gt;</c> = 主题，<c>AcceptUnmappedTypes</c>（<c>true</c> / <c>false</c>），<c>Webhooks:&lt;主题&gt;</c> = 出站 URL，<c>PropagateTraceContext</c>（<c>true</c> / <c>false</c>）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record CloudEventsProtocolOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Protocols:CloudEvents";

    /// <summary>
    /// 入站事件的处理方式
    /// </summary>
    /// <remarks>
    /// 默认在请求内派发
    /// </remarks>
    public CloudEventsInboundMode Inbound { get; init; } = CloudEventsInboundMode.Dispatch;

    /// <summary>
    /// 入站请求体的字节上限
    /// </summary>
    /// <remarks>
    /// 默认 1 MiB。端点边读边计数，超过即 <see cref="CloudEventsErrors.BodyTooLarge"/>（HTTP 400），一个事件也不处理（ADR-0187）
    /// </remarks>
    public long MaxRequestBodyBytes
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The request body limit must be positive.");
    } = 1024 * 1024;

    /// <summary>
    /// 入站 batch 的事件数上限
    /// </summary>
    /// <remarks>
    /// 默认 100。派发模式下每个事件都经一次完整的内核管道；超过即 <see cref="CloudEventsErrors.BatchTooLarge"/>（HTTP 400），一个事件也不处理（ADR-0187）
    /// </remarks>
    public int MaxBatchSize
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The batch size limit must be positive.");
    } = 100;

    /// <summary>
    /// 出站 Webhook 的内容模式
    /// </summary>
    /// <remarks>
    /// 默认 binary，不能是 batch
    /// </remarks>
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
    /// 出站 Webhook 是否在 HTTP 头里传播追踪上下文
    /// </summary>
    /// <remarks>
    /// 缺省 <see langword="false"/>：Webhook 的命名客户端不写 <c>traceparent</c>、<c>tracestate</c>、<c>baggage</c> 头——接收方是第三方，内部的追踪 ID 与 Baggage 不外推，与信封不补内部扩展同理。为 <see langword="true"/> 时按 .NET 的全局传播器写。只作用于 Webhook 的命名客户端，不改进程全局的传播
    /// </remarks>
    public bool PropagateTraceContext { get; init; }

    /// <summary>
    /// 入站是否接受没有映射的事件类型
    /// </summary>
    /// <remarks>
    /// 缺省 <see langword="false"/>：只有 <see cref="Topics"/> 映射过的类型能进来，其余是 <see cref="CloudEventsErrors.TopicUnresolved"/>。为 <see langword="true"/> 时没有映射的类型以它本身为主题（须是合法主题），发送方能投进任何有订阅的主题，包括插件订阅的主题，只在发送方可信时打开（ADR-0200）
    /// </remarks>
    public bool AcceptUnmappedTypes { get; init; }

    /// <summary>
    /// 事件类型 → 主题
    /// </summary>
    /// <remarks>
    /// 没有映射的类型在其本身是合法主题时直接作为主题
    /// </remarks>
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
    /// 主题 → 出站 Webhook 的绝对 HTTP(S) URL
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
    /// 从配置读取
    /// </summary>
    /// <remarks>
    /// 枚举值、布尔值或上限写了值（含空串）而不合法时抛出 <see cref="ArgumentException"/>，消息带完整的配置键；枚举按成员名写，不收数字
    /// </remarks>
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
            // 写了值（含空串）就必须合法，缺失才回落到缺省值（ADR-0255）
            Inbound = ConfigurationValues.ReadEnum<CloudEventsInboundMode>(section, "Inbound") ?? CloudEventsInboundMode.Dispatch,
            MaxRequestBodyBytes = ConfigurationValues.ReadInt64(section, "MaxRequestBodyBytes", 1) ?? 1024 * 1024,
            MaxBatchSize = ConfigurationValues.ReadInt32(section, "MaxBatchSize", 1) ?? 100,
            Outbound = ConfigurationValues.ReadEnum<CloudEventsContentMode>(section, "Outbound") ?? CloudEventsContentMode.Binary,
            PropagateTraceContext = ConfigurationValues.ReadBoolean(section, "PropagateTraceContext") ?? false,
            AcceptUnmappedTypes = ConfigurationValues.ReadBoolean(section, "AcceptUnmappedTypes") ?? false,
            Topics = topics,
            Webhooks = webhooks,
        };
    }

    /// <summary>
    /// 事件类型对应的主题
    /// </summary>
    /// <remarks>
    /// 显式映射优先；没有映射时只在 <see cref="AcceptUnmappedTypes"/> 打开、类型本身是合法主题时以类型为主题，否则无值（ADR-0200）
    /// </remarks>
    public Maybe<string> TopicFor(string type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (Topics.TryGetValue(type, out var topic))
        {
            return topic;
        }

        return AcceptUnmappedTypes && TopicName.IsValid(type) ? type : default(Maybe<string>);
    }
}
