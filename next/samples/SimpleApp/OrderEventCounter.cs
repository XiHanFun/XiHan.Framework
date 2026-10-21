// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace SimpleApp;

/// <summary>
/// 已处理的订单事件计数。
/// </summary>
public sealed class OrderEventCounter
{
    /// <summary>
    /// 计数值；只通过 <see cref="Interlocked"/> 读写，消息并发派发时也准确。
    /// </summary>
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
