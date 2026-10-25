// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Primitives;

namespace SimpleApp;

/// <summary>
/// 订单事件处理器：消息经完整管道派发，这里能看到租户等上下文。
/// </summary>
public sealed class OrderPlacedHandler(OrderEventCounter counter) : IMessageHandler
{
    /// <summary>
    /// 每收到一条订单消息计数加一，总是返回成功；失败时返回的错误会让消息进入重试与死信流程。
    /// </summary>
    public ValueTask<Result> HandleAsync(MessageDelivery delivery, InvocationContext context, CancellationToken cancellationToken)
    {
        counter.Increment();
        return ValueTask.FromResult(Result.Success());
    }
}
