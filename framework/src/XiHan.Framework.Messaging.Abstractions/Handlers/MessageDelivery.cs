// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Handlers;

/// <summary>
/// 一次投递：主题、信封、第几次尝试与订阅方式
/// </summary>
/// <remarks>
/// 尝试次数从 1 起。订阅方式说明投递来自哪种订阅：派发器只执行该方式的处理器，同一条消息到达竞争订阅与广播订阅是两次投递（RFC-0009「订阅方式」）。成员都经初始化器给出，以后加字段只加属性（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record MessageDelivery
{
    /// <summary>
    /// 主题
    /// </summary>
    /// <exception cref="ArgumentException">不是合法的主题名</exception>
    public required string Topic
    {
        get;
        init => field = TopicName.Validate(value);
    }

    /// <summary>
    /// 信封
    /// </summary>
    /// <exception cref="ArgumentNullException">为 <see langword="null"/></exception>
    public required MessageEnvelope Envelope
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>
    /// 第几次尝试
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">小于 1</exception>
    public required int Attempt
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            field = value;
        }
    }

    /// <summary>
    /// 订阅方式
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">不是定义过的订阅方式</exception>
    public required SubscriptionMode Mode
    {
        get;
        init => field = value is SubscriptionMode.Competing or SubscriptionMode.Broadcast
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "The subscription mode is not defined.");
    }
}
