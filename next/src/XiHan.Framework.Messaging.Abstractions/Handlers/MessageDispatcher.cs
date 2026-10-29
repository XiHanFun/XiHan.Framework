// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Handlers;

/// <summary>
/// 把投递变成一次内核调用。
/// </summary>
/// <remarks>
/// 新作用域、从扩展属性构造 Baggage（租户、关联）与追踪（<c>traceparent</c>），经完整管道执行该主题的处理器，第一个失败即为调用结果。不写幂等键：重试必须重新执行处理器。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed partial class MessageDispatcher(IServiceProvider services, App app, MessageSubscriptions subscriptions)
{
    /// <summary>
    /// 调用上下文里存放当前投递的键。
    /// </summary>
    private static readonly ContextItemKey<MessageDelivery> _deliveryKey = new("xihan.messaging.delivery");

    /// <summary>
    /// 根服务提供者。
    /// </summary>
    /// <remarks>
    /// 每次投递从它创建新作用域。
    /// </remarks>
    private readonly IServiceProvider _services = services ?? throw new ArgumentNullException(nameof(services));

    /// <summary>
    /// 应用。
    /// </summary>
    /// <remarks>
    /// 投递经它的完整管道执行。
    /// </remarks>
    private readonly App _app = app ?? throw new ArgumentNullException(nameof(app));

    /// <summary>
    /// 订阅表。
    /// </summary>
    /// <remarks>
    /// 决定主题有哪些处理器。
    /// </remarks>
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
    /// 派发。
    /// </summary>
    /// <remarks>
    /// 没有处理器的主题直接成功。
    /// </remarks>
    public async ValueTask<Result> DispatchAsync(MessageDelivery delivery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        var handlers = _subscriptions.HandlersFor(delivery.Topic);
        if (handlers.Count == 0)
        {
            return Result.Success();
        }

        // 每次投递一个新作用域与新调用上下文：租户与关联 ID 来自扩展属性，追踪 ID 与父跨度来自 traceparent
        await using var scope = _services.CreateAsyncScope();
        var context = App.CreateContext(scope.ServiceProvider, cancellationToken, TraceIdOf(delivery.Envelope), SpanIdOf(delivery.Envelope), null, BaggageOf(delivery.Envelope));
        context.Set(_deliveryKey, delivery);
        context.SetOrigin(new InvocationOrigin { Protocol = InvocationProtocols.Messaging, Target = delivery.Topic });

        return await _app.InvokeAsync(context, async ctx =>
        {
            // 按订阅顺序依次执行，第一个失败即终止并作为调用结果；不写幂等键，重试时必须重新执行
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

    /// <summary>
    /// 从信封扩展属性构造 Baggage：租户进 <see cref="BaggageKeys.Tenant"/>，关联 ID 进 <see cref="BaggageKeys.Correlation"/>。
    /// </summary>
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

    /// <summary>
    /// 从 <c>traceparent</c> 扩展取追踪 ID。
    /// </summary>
    /// <remarks>
    /// 缺失或格式不符返回 <see langword="null"/>。
    /// </remarks>
    private static string? TraceIdOf(MessageEnvelope envelope)
        => envelope.Extension(EnvelopeExtensions.TraceParent).TryGetValue(out var traceparent) && TraceParent().Match(traceparent) is { Success: true } match
            ? match.Groups["trace"].Value
            : null;

    /// <summary>
    /// 从 <c>traceparent</c> 扩展取父跨度 ID。
    /// </summary>
    /// <remarks>
    /// 缺失或格式不符返回 <see langword="null"/>。
    /// </remarks>
    private static string? SpanIdOf(MessageEnvelope envelope)
        => envelope.Extension(EnvelopeExtensions.TraceParent).TryGetValue(out var traceparent) && TraceParent().Match(traceparent) is { Success: true } match
            ? match.Groups["span"].Value
            : null;

    /// <summary>
    /// W3C <c>traceparent</c> 版本 00 的文法：追踪 ID 32 位、跨度 ID 16 位十六进制。
    /// </summary>
    [GeneratedRegex("^00-(?<trace>[0-9a-f]{32})-(?<span>[0-9a-f]{16})-[0-9a-f]{2}$")]
    private static partial Regex TraceParent();
}
