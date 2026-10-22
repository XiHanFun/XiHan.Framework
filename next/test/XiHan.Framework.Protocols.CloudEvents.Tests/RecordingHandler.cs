// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.CloudEvents.Tests;

/// <summary>
/// 记录投递的处理器；主题标识为 <c>fail</c> 时返回冲突。
/// </summary>
/// <param name="log">记录。</param>
internal sealed class RecordingHandler(DeliveryLog log) : IMessageHandler
{
    /// <inheritdoc />
    public ValueTask<Result> HandleAsync(MessageDelivery delivery, InvocationContext context, CancellationToken cancellationToken)
    {
        log.Record(delivery, context.Baggage);
        return ValueTask.FromResult(delivery.Envelope.Subject == "fail"
            ? (Result)Error.Conflict("ORDERS_CONFLICT_001", "orders.order.rejected")
            : Result.Success());
    }
}
