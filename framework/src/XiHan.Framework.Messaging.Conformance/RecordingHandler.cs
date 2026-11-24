// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Conformance;

/// <summary>
/// 套件处理器：记录到达并按日志指示失败、抛异常或一直等到被取消
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class RecordingHandler(DeliveryLog log) : IMessageHandler
{
    /// <inheritdoc />
    public async ValueTask<Result> HandleAsync(MessageDelivery delivery, InvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(context);
        log.Record(new ReceivedMessage(delivery, new Dictionary<string, string>(context.Baggage, StringComparer.Ordinal), context.TraceId) { Origin = context.FindOrigin().TryGetValue(out var origin) ? origin : null, TraceFlags = context.TraceFlags });
        if (log.BlockingIds.ContainsKey(delivery.Envelope.Id) || (log.BlockingAttempts.TryGetValue(delivery.Envelope.Id, out var blocked) && blocked == delivery.Attempt))
        {
            // 等到调用被取消（代理停止）：取消异常由管道写成失败结果返回，代理据停止信号判定这次投递被打断
            await Task.Delay(Timeout.InfiniteTimeSpan, context.CancellationToken).ConfigureAwait(false);
        }

        if (log.ThrowingIds.ContainsKey(delivery.Envelope.Id))
        {
            throw new InvalidOperationException($"The handler threw for '{delivery.Envelope.Id}' as instructed.");
        }

        return log.FailingIds.ContainsKey(delivery.Envelope.Id)
            ? Result.Failure(Error.Literal("CONFORMANCE_VALIDATION_001", "The handler failed as instructed."))
            : Result.Success();
    }
}
