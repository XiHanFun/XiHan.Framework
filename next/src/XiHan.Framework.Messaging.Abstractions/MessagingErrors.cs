// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// 消息错误码；登记在 <c>spec/conventions/error-codes.md</c>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class MessagingErrors
{
    /// <summary>主题不合法。</summary>
    public const string InvalidTopic = "MESSAGING_VALIDATION_001";

    /// <summary>信封不合法或无法解码。</summary>
    public const string InvalidEnvelope = "MESSAGING_VALIDATION_002";

    /// <summary>领域事件类型未登记（<c>AddDomainEvent</c>）。</summary>
    public const string DomainEventUnregistered = "MESSAGING_VALIDATION_003";

    /// <summary>领域事件类型重复登记或登记表已满（以异常抛出）。</summary>
    public const string DomainEventRegistration = "MESSAGING_VALIDATION_004";

    /// <summary>代理不可达或操作失败。</summary>
    public const string BrokerUnavailable = "MESSAGING_UNAVAILABLE_001";

    /// <summary>队列已满。</summary>
    public const string QueueFull = "MESSAGING_UNAVAILABLE_002";

    /// <summary>处理器抛出未处理异常（代理侧记录）。</summary>
    public const string HandlerFailed = "MESSAGING_UNEXPECTED_001";
}
