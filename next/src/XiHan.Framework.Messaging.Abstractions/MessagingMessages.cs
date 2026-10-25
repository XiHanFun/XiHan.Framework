// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// 消息子系统的消息键；各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>，Provider 装配时注册 <see cref="Catalog"/>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class MessagingMessages
{
    /// <summary>
    /// 目录名。
    /// </summary>
    public const string CatalogName = "messaging";

    /// <summary>文档不是合法的 CloudEvents JSON。参数：<c>reason</c>。</summary>
    public const string EnvelopeInvalidJson = "messaging.envelope.invalid_json";

    /// <summary><c>specversion</c> 不符。参数：<c>specVersion</c>。</summary>
    public const string EnvelopeSpecVersion = "messaging.envelope.spec_version";

    /// <summary>缺少必需属性。</summary>
    public const string EnvelopeAttributesRequired = "messaging.envelope.attributes_required";

    /// <summary><c>time</c> 不是 RFC 3339 时间戳。参数：<c>time</c>。</summary>
    public const string EnvelopeInvalidTime = "messaging.envelope.invalid_time";

    /// <summary><c>data_base64</c> 不是合法 Base64。</summary>
    public const string EnvelopeInvalidBase64 = "messaging.envelope.invalid_base64";

    /// <summary>扩展属性名不合法。参数：<c>name</c>。</summary>
    public const string EnvelopeInvalidExtension = "messaging.envelope.invalid_extension";

    /// <summary>信封不合法。参数：<c>reason</c>。</summary>
    public const string EnvelopeInvalid = "messaging.envelope.invalid";

    /// <summary>主题不合法。参数：<c>topic</c>。</summary>
    public const string InvalidTopic = "messaging.topic.invalid";

    /// <summary>内存队列已满。参数：<c>topic</c>、<c>capacity</c>。</summary>
    public const string QueueFull = "messaging.queue.full";

    /// <summary>处理器抛出未处理异常。参数：<c>reason</c>。</summary>
    public const string HandlerFailed = "messaging.handler.failed";

    /// <summary>消息代理不可达。参数：<c>reason</c>。</summary>
    public const string BrokerUnavailable = "messaging.broker.unavailable";

    /// <summary>
    /// 延迟装载的目录；首次访问时读嵌入的语言文件，之后复用同一实例。
    /// </summary>
    private static readonly Lazy<MessageCatalog> _catalog = new(static () => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(MessagingMessages)));

    /// <summary>
    /// 目录实例；首次访问时从嵌入资源装载。
    /// </summary>
    public static MessageCatalog Catalog => _catalog.Value;
}
