// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Primitives;

namespace SimpleApp.Orders;

/// <summary>
/// 订单事件处理器。
/// </summary>
/// <remarks>
/// 收到的是 <see cref="OrderPlaced"/> 领域事件派发成的 <c>orders.placed</c> 消息；消息经完整管道派发，这里能看到租户等上下文。
/// </remarks>
public sealed class OrderPlacedHandler(OrderEventCounter counter) : IMessageHandler
{
    /// <summary>
    /// 每收到一条订单消息计数加一，总是返回成功。
    /// </summary>
    /// <remarks>
    /// 失败时返回的错误会让消息进入重试与死信流程。
    /// </remarks>
    public ValueTask<Result> HandleAsync(MessageDelivery delivery, InvocationContext context, CancellationToken cancellationToken)
    {
        counter.Increment();
        return ValueTask.FromResult(Result.Success());
    }
}
