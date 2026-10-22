// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.CloudEvents;

/// <summary>
/// CloudEvents 协议适配器的错误码；登记在 <c>spec/conventions/error-codes.md</c>。信封本身不合法用消息子系统的 <c>MESSAGING_VALIDATION_002</c>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class CloudEventsErrors
{
    /// <summary>请求不是 CloudEvent：内容类型既不是结构化或批量格式，也没有 binary 模式的 <c>ce-specversion</c> 头。</summary>
    public const string UnsupportedContentType = "PROTOCOLS_VALIDATION_008";

    /// <summary>事件类型没有映射到主题，类型本身也不是合法主题。</summary>
    public const string TopicUnresolved = "PROTOCOLS_VALIDATION_009";

    /// <summary>派发模式下主题没有处理器。</summary>
    public const string NoSubscriber = "PROTOCOLS_NOTFOUND_003";

    /// <summary>主题没有配置 Webhook。</summary>
    public const string WebhookMissing = "PROTOCOLS_NOTFOUND_004";

    /// <summary>Webhook 返回了非 2xx 状态。</summary>
    public const string WebhookFailed = "PROTOCOLS_UNAVAILABLE_001";

    /// <summary>Webhook 无法连接。</summary>
    public const string WebhookUnreachable = "PROTOCOLS_UNAVAILABLE_002";
}
