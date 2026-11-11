// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Messaging.Conformance;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Redis.Tests;

/// <summary>
/// 记下投递后一直等到取消的处理器
/// </summary>
/// <remarks>
/// 模拟宿主停止时还在处理的投递
/// </remarks>
public sealed class BlockingHandler(DeliveryLog log) : IMessageHandler
{
    /// <inheritdoc />
    public async ValueTask<Result> HandleAsync(MessageDelivery delivery, InvocationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(context);
        log.Record(new ReceivedMessage(delivery, new Dictionary<string, string>(context.Baggage, StringComparer.Ordinal), context.TraceId));
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return Result.Success();
    }
}
