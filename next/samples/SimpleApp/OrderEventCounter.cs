// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace SimpleApp;

/// <summary>
/// 已处理的订单事件计数。
/// </summary>
public sealed class OrderEventCounter
{
    private long _handled;

    public long Handled => Interlocked.Read(ref _handled);

    public void Increment() => Interlocked.Increment(ref _handled);
}
