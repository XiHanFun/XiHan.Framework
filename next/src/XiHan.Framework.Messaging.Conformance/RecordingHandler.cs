// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Conformance;

/// <summary>
/// 套件处理器：记录到达并按日志指示失败或抛异常。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class RecordingHandler(DeliveryLog log) : IMessageHandler
{
    /// <inheritdoc />
    public ValueTask<Result> HandleAsync(MessageDelivery delivery, InvocationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(context);
        log.Record(new ReceivedMessage(delivery, new Dictionary<string, string>(context.Baggage, StringComparer.Ordinal), context.TraceId));
        if (log.ThrowingIds.ContainsKey(delivery.Envelope.Id))
        {
            throw new InvalidOperationException($"处理器按指示对 {delivery.Envelope.Id} 抛异常。");
        }

        return ValueTask.FromResult(log.FailingIds.ContainsKey(delivery.Envelope.Id)
            ? Result.Failure(Error.Validation("CONFORMANCE_VALIDATION_001", "处理器按指示失败。"))
            : Result.Success());
    }
}
