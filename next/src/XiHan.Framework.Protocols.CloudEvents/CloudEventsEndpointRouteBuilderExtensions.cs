// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.CloudEvents;

/// <summary>
/// 入站 CloudEvents 端点：<c>POST</c> 收 binary / structured / batch 事件，按类型映射到主题，派发模式下在请求内经内核管道交给主题的处理器，发布模式下交给消息代理；全部成功回 202。
/// 端点经过 <c>UseXiHan()</c>：请求的凭据先在外层认证，失败结果写成 Problem Details；可用 <c>RequireXiHanAuthentication()</c> 等约定限制调用方。必须放在 <c>UseXiHan()</c> 之后。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class CloudEventsEndpointRouteBuilderExtensions
{
    /// <summary>
    /// 映射入站端点；需要 <see cref="CloudEventsProtocolFeature"/> 已装配。
    /// </summary>
    public static IEndpointConventionBuilder MapXiHanCloudEvents(this IEndpointRouteBuilder endpoints, string pattern = "/events")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        var options = endpoints.ServiceProvider.GetService<CloudEventsProtocolOptions>()
            ?? throw new FrameworkException(KernelErrors.InvalidAppState, "MapXiHanCloudEvents() requires the CloudEvents protocol feature; add UseFeature<CloudEventsProtocolFeature>() to the application.");

        return endpoints.MapPost(pattern, httpContext => HandleAsync(httpContext, options)).WithName("xihan.protocols.cloudevents");
    }

    /// <summary>
    /// 端点处理：读事件，逐个解析主题并派发或发布，按顺序处理、第一个失败即停止（已处理的不回滚，发送方重试整批时处理器按消息 ID 去重）。
    /// </summary>
    private static async Task HandleAsync(HttpContext httpContext, CloudEventsProtocolOptions options)
    {
        if (!httpContext.GetXiHanContext().TryGetValue(out var context))
        {
            throw new FrameworkException(KernelErrors.InvalidAppState, "The CloudEvents endpoint requires UseXiHan() to run before routing; the invocation context is missing.");
        }

        var read = await CloudEventsHttpBinding.ReadAsync(httpContext.Request, context.CancellationToken).ConfigureAwait(false);
        if (!read.TryGetValue(out var envelopes))
        {
            context.SetOutcome(read.Error!);
            return;
        }

        var services = httpContext.RequestServices;
        foreach (var envelope in envelopes)
        {
            if (!options.TopicFor(envelope.Type).TryGetValue(out var topic))
            {
                context.SetOutcome(Error.Validation(CloudEventsErrors.TopicUnresolved, CloudEventsMessages.TopicUnresolved, MessageArguments.Of(("type", envelope.Type))));
                return;
            }

            Result outcome;
            if (options.Inbound == CloudEventsInboundMode.Publish)
            {
                outcome = await services.GetRequiredService<IMessagePublisher>().PublishAsync(topic, envelope, context.CancellationToken).ConfigureAwait(false);
            }
            else
            {
                // 派发模式下没有处理器的主题是配置错误：404 让发送方停止重试，而不是静默吞掉事件
                if (services.GetRequiredService<MessageSubscriptions>().HandlersFor(topic).Count == 0)
                {
                    context.SetOutcome(Error.NotFound(CloudEventsErrors.NoSubscriber, CloudEventsMessages.NoSubscriber, MessageArguments.Of(("topic", topic))));
                    return;
                }

                outcome = await services.GetRequiredService<MessageDispatcher>().DispatchAsync(new MessageDelivery(topic, envelope, 1), context.CancellationToken).ConfigureAwait(false);
            }

            if (outcome.IsFailure)
            {
                context.SetOutcome(outcome);
                return;
            }
        }

        httpContext.Response.StatusCode = StatusCodes.Status202Accepted;
    }
}
