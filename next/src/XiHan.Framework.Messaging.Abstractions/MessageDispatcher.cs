// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// 把投递变成一次内核调用：新作用域、从扩展属性构造 Baggage（租户、关联）与追踪（<c>traceparent</c>），
/// 经完整管道执行该主题的处理器，第一个失败即为调用结果。不写幂等键：重试必须重新执行处理器。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed partial class MessageDispatcher(IServiceProvider services, App app, MessageSubscriptions subscriptions)
{
    private static readonly ContextItemKey<MessageDelivery> _deliveryKey = new("xihan.messaging.delivery");

    private readonly IServiceProvider _services = services ?? throw new ArgumentNullException(nameof(services));
    private readonly App _app = app ?? throw new ArgumentNullException(nameof(app));
    private readonly MessageSubscriptions _subscriptions = subscriptions ?? throw new ArgumentNullException(nameof(subscriptions));

    /// <summary>
    /// 读取本次调用正在处理的投递。
    /// </summary>
    public static Maybe<MessageDelivery> GetDelivery(InvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Get(_deliveryKey);
    }

    /// <summary>
    /// 派发。没有处理器的主题直接成功。
    /// </summary>
    public async ValueTask<Result> DispatchAsync(MessageDelivery delivery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        var handlers = _subscriptions.HandlersFor(delivery.Topic);
        if (handlers.Count == 0)
        {
            return Result.Success();
        }

        await using var scope = _services.CreateAsyncScope();
        var context = App.CreateContext(scope.ServiceProvider, cancellationToken, TraceIdOf(delivery.Envelope), SpanIdOf(delivery.Envelope), null, BaggageOf(delivery.Envelope));
        context.Set(_deliveryKey, delivery);

        return await _app.InvokeAsync(context, async ctx =>
        {
            foreach (var handlerType in handlers)
            {
                var handler = (IMessageHandler)ctx.Services.GetRequiredService(handlerType);
                var handled = await handler.HandleAsync(delivery, ctx, ctx.CancellationToken).ConfigureAwait(false);
                if (handled.IsFailure)
                {
                    ctx.SetOutcome(handled);
                    return;
                }
            }
        }).ConfigureAwait(false);
    }

    private static Dictionary<string, string> BaggageOf(MessageEnvelope envelope)
    {
        var baggage = new Dictionary<string, string>(StringComparer.Ordinal);
        if (envelope.Extension(EnvelopeExtensions.Tenant).TryGetValue(out var tenant))
        {
            baggage[BaggageKeys.Tenant] = tenant;
        }

        if (envelope.Extension(EnvelopeExtensions.Correlation).TryGetValue(out var correlation))
        {
            baggage[BaggageKeys.Correlation] = correlation;
        }

        return baggage;
    }

    private static string? TraceIdOf(MessageEnvelope envelope)
        => envelope.Extension(EnvelopeExtensions.TraceParent).TryGetValue(out var traceparent) && TraceParent().Match(traceparent) is { Success: true } match
            ? match.Groups["trace"].Value
            : null;

    private static string? SpanIdOf(MessageEnvelope envelope)
        => envelope.Extension(EnvelopeExtensions.TraceParent).TryGetValue(out var traceparent) && TraceParent().Match(traceparent) is { Success: true } match
            ? match.Groups["span"].Value
            : null;

    [GeneratedRegex("^00-(?<trace>[0-9a-f]{32})-(?<span>[0-9a-f]{16})-[0-9a-f]{2}$")]
    private static partial Regex TraceParent();
}
