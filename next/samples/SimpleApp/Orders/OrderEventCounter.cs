// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace SimpleApp.Orders;

/// <summary>
/// 已处理的订单事件计数。
/// </summary>
public sealed class OrderEventCounter
{
    /// <summary>
    /// 计数值。
    /// </summary>
    /// <remarks>
    /// 只通过 <see cref="Interlocked"/> 读写，消息并发派发时也准确。
    /// </remarks>
    private long _handled;

    /// <summary>
    /// 已处理的事件数。
    /// </summary>
    public long Handled => Interlocked.Read(ref _handled);

    /// <summary>
    /// 计数加一。
    /// </summary>
    public void Increment() => Interlocked.Increment(ref _handled);
}
