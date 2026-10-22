// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.CloudEvents;

/// <summary>
/// CloudEvents 协议适配器的消息键；各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>，特性装配时注册 <see cref="Catalog"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class CloudEventsMessages
{
    /// <summary>
    /// 目录名。
    /// </summary>
    public const string CatalogName = "protocols.cloudevents";

    /// <summary>请求不是 CloudEvent。参数：<c>contentType</c>。</summary>
    public const string ContentTypeUnsupported = "protocols.cloudevents.content_type_unsupported";

    /// <summary>事件类型没有映射到主题。参数：<c>type</c>。</summary>
    public const string TopicUnresolved = "protocols.cloudevents.topic_unresolved";

    /// <summary>主题没有处理器。参数：<c>topic</c>。</summary>
    public const string NoSubscriber = "protocols.cloudevents.no_subscriber";

    /// <summary>主题没有配置 Webhook。参数：<c>topic</c>。</summary>
    public const string WebhookMissing = "protocols.cloudevents.webhook_missing";

    /// <summary>Webhook 返回了非 2xx 状态。参数：<c>topic</c>、<c>status</c>。</summary>
    public const string WebhookFailed = "protocols.cloudevents.webhook_failed";

    /// <summary>Webhook 无法连接。参数：<c>topic</c>、<c>reason</c>。</summary>
    public const string WebhookUnreachable = "protocols.cloudevents.webhook_unreachable";

    /// <summary>
    /// 本包的消息目录。
    /// </summary>
    public static MessageCatalog Catalog => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(CloudEventsMessages));
}
