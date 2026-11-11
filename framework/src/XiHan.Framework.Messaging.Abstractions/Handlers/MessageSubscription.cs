// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Handlers;

/// <summary>
/// 一个订阅：主题加订阅方式
/// </summary>
/// <remarks>
/// 代理按它消费：同一主题的竞争订阅与广播订阅是两份独立的消费（RFC-0009「订阅方式」）。主题与订阅方式经构造给出，以后加字段只加初始化器属性、不改构造签名（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record MessageSubscription
{
    /// <summary>
    /// 创建订阅
    /// </summary>
    /// <param name="topic">主题</param>
    /// <param name="mode">订阅方式</param>
    /// <exception cref="ArgumentException"><paramref name="topic"/> 不是合法的主题名</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> 不是定义过的订阅方式</exception>
    public MessageSubscription(string topic, SubscriptionMode mode)
    {
        Topic = TopicName.Validate(topic);
        Mode = mode is SubscriptionMode.Competing or SubscriptionMode.Broadcast
            ? mode
            : throw new ArgumentOutOfRangeException(nameof(mode), mode, "The subscription mode is not defined.");
    }

    /// <summary>
    /// 主题
    /// </summary>
    public string Topic { get; }

    /// <summary>
    /// 订阅方式
    /// </summary>
    public SubscriptionMode Mode { get; }
}
