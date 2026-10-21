// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Primitives;

namespace SimpleApp;

/// <summary>
/// 订单事件处理器：消息经完整管道派发，这里能看到租户等上下文。
/// </summary>
public sealed class OrderPlacedHandler(OrderEventCounter counter) : IMessageHandler
{
    public ValueTask<Result> HandleAsync(MessageDelivery delivery, InvocationContext context, CancellationToken cancellationToken)
    {
        counter.Increment();
        return ValueTask.FromResult(Result.Success());
    }
}
