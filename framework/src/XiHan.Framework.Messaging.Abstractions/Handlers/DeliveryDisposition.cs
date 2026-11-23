// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Handlers;

/// <summary>
/// 一次投递尝试之后代理该怎么了结这条投递
/// </summary>
/// <remarks>
/// 由 <see cref="MessageDeliveryAttempt"/> 给出，代理只把它翻译成自己的确认、重投与死信动作（RFC-0009「投递」第 3 条，ADR-0261）。<c>default</c> 是 <see cref="Abandon"/>：没有结论的投递不确认，留给代理重新投递
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public enum DeliveryDisposition
{
    /// <summary>
    /// 不了结，这次不算一次失败
    /// </summary>
    /// <remarks>
    /// 投递被停止打断：不确认、不重投、不进死信，原消息留给代理重新投递。再投递时的尝试号由代理决定：内存代理与 RabbitMQ 仍是原来的，Kafka 重启后从 1 起，Redis 的回收让投递次数加一
    /// </remarks>
    Abandon = 0,

    /// <summary>
    /// 处理成功，确认
    /// </summary>
    Complete = 1,

    /// <summary>
    /// 失败且还有尝试次数，以下一次尝试号重新投递
    /// </summary>
    Retry = 2,

    /// <summary>
    /// 竞争订阅的投递用完了尝试次数，进死信
    /// </summary>
    /// <remarks>
    /// 死信写入得到确认后才了结原消息（RFC-0009「投递」第 7 条）
    /// </remarks>
    DeadLetter = 3,

    /// <summary>
    /// 广播订阅的投递用完了尝试次数，丢弃
    /// </summary>
    /// <remarks>
    /// 了结原消息，不进死信；丢弃的日志已经记过
    /// </remarks>
    Discard = 4,
}
