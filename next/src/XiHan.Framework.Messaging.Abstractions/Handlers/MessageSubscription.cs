// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Handlers;

/// <summary>
/// 一个订阅：主题加订阅方式。
/// </summary>
/// <remarks>
/// 代理按它消费：同一主题的竞争订阅与广播订阅是两份独立的消费（RFC-0009「订阅方式」）。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record MessageSubscription(string Topic, SubscriptionMode Mode)
{
    /// <summary>
    /// 主题。
    /// </summary>
    public string Topic { get; } = TopicName.Validate(Topic);

    /// <summary>
    /// 订阅方式。
    /// </summary>
    public SubscriptionMode Mode { get; } = Mode is SubscriptionMode.Competing or SubscriptionMode.Broadcast
        ? Mode
        : throw new ArgumentOutOfRangeException(nameof(Mode), Mode, "The subscription mode is not defined.");
}
