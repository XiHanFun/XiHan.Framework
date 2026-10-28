// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Messaging.Abstractions.Handlers;
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
        log.Record(new ReceivedMessage(delivery, new Dictionary<string, string>(context.Baggage, StringComparer.Ordinal), context.TraceId) { Origin = context.GetOrigin().TryGetValue(out var origin) ? origin : null });
        if (log.ThrowingIds.ContainsKey(delivery.Envelope.Id))
        {
            throw new InvalidOperationException($"The handler threw for '{delivery.Envelope.Id}' as instructed.");
        }

        return ValueTask.FromResult(log.FailingIds.ContainsKey(delivery.Envelope.Id)
            ? Result.Failure(Error.Literal("CONFORMANCE_VALIDATION_001", "The handler failed as instructed."))
            : Result.Success());
    }
}
