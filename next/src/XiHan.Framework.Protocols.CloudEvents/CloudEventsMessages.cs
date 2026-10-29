// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.CloudEvents;

/// <summary>
/// CloudEvents 协议适配器的消息键。
/// </summary>
/// <remarks>
/// 各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>，特性装配时注册 <see cref="Catalog"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class CloudEventsMessages
{
    /// <summary>
    /// 目录名。
    /// </summary>
    public const string CatalogName = "protocols.cloudevents";

    /// <summary>
    /// 请求不是 CloudEvent。
    /// </summary>
    /// <remarks>
    /// 参数：<c>contentType</c>。
    /// </remarks>
    public const string ContentTypeUnsupported = "protocols.cloudevents.content_type_unsupported";

    /// <summary>
    /// 事件类型没有映射到主题。
    /// </summary>
    /// <remarks>
    /// 参数：<c>type</c>。
    /// </remarks>
    public const string TopicUnresolved = "protocols.cloudevents.topic_unresolved";

    /// <summary>
    /// 主题没有处理器。
    /// </summary>
    /// <remarks>
    /// 参数：<c>topic</c>。
    /// </remarks>
    public const string NoSubscriber = "protocols.cloudevents.no_subscriber";

    /// <summary>
    /// 主题没有配置 Webhook。
    /// </summary>
    /// <remarks>
    /// 参数：<c>topic</c>。
    /// </remarks>
    public const string WebhookMissing = "protocols.cloudevents.webhook_missing";

    /// <summary>
    /// Webhook 返回了非 2xx 状态。
    /// </summary>
    /// <remarks>
    /// 参数：<c>topic</c>、<c>status</c>。
    /// </remarks>
    public const string WebhookFailed = "protocols.cloudevents.webhook_failed";

    /// <summary>
    /// Webhook 无法连接。
    /// </summary>
    /// <remarks>
    /// 参数：<c>topic</c>、<c>reason</c>。
    /// </remarks>
    public const string WebhookUnreachable = "protocols.cloudevents.webhook_unreachable";

    /// <summary>
    /// 本包的消息目录。
    /// </summary>
    public static MessageCatalog Catalog => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(CloudEventsMessages));
}
