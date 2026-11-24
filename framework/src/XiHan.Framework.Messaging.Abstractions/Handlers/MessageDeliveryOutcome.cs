// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Handlers;

/// <summary>
/// 一次投递尝试的结论：了结方式与失败的错误
/// </summary>
/// <remarks>
/// 只由 <see cref="MessageDeliveryAttempt"/> 给出（ADR-0261）。<c>default</c> 是 <see cref="DeliveryDisposition.Abandon"/>、没有错误
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public readonly record struct MessageDeliveryOutcome
{
    /// <summary>
    /// 创建结论
    /// </summary>
    /// <param name="disposition">了结方式</param>
    /// <param name="error">失败的错误</param>
    internal MessageDeliveryOutcome(DeliveryDisposition disposition, Error? error)
    {
        Disposition = disposition;
        Error = error;
    }

    /// <summary>
    /// 了结方式
    /// </summary>
    public DeliveryDisposition Disposition { get; }

    /// <summary>
    /// 这次失败的错误
    /// </summary>
    /// <remarks>
    /// 重投、进死信与丢弃时是处理器返回的错误，或派发抛出的异常转成的 <see cref="MessagingErrors.HandlerFailed"/>；无法解码的消息（<see cref="MessageDeliveryAttempt.Undecodable"/>）是解码给出的 <see cref="MessagingErrors.InvalidEnvelope"/>；没有派发就判定用完尝试次数的投递（<see cref="MessageDeliveryAttempt.Exhausted"/> 没给错误）为 <see langword="null"/>。成功与不了结时为 <see langword="null"/>
    /// </remarks>
    public Error? Error { get; }
}
