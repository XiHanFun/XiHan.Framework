// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.CloudEvents.Inbound;

/// <summary>
/// 入站 CloudEvents 端点
/// </summary>
/// <remarks>
/// <c>POST</c> 收 binary / structured / batch 事件，按类型映射到主题，派发模式下在请求内经内核管道交给主题的处理器，发布模式下按主题分组批量交给消息代理；全部成功回 202。端点经过 <c>UseXiHan()</c>：请求的凭据先在外层认证，失败结果写成 Problem Details；限制调用方由宿主负责，见 <see cref="MapXiHanCloudEvents"/>。必须放在 <c>UseXiHan()</c> 之后
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class CloudEventsEndpointRouteBuilderExtensions
{
    /// <summary>
    /// 两种订阅方式，按派发模式的投递顺序
    /// </summary>
    private static readonly SubscriptionMode[] _modes = [SubscriptionMode.Competing, SubscriptionMode.Broadcast];

    /// <summary>
    /// 映射入站端点
    /// </summary>
    /// <remarks>
    /// 需要 <see cref="CloudEventsProtocolFeature"/> 已装配。框架不给端点加任何要求，权限要由宿主在返回的构建器上挂 <c>RequireXiHanPermission(...)</c>：扩展属性 <c>xihantenant</c>、<c>xihanprincipal</c> 是发送方给的，能调端点的人就能选租户、写审计主体（ADR-0121）
    /// </remarks>
    public static IEndpointConventionBuilder MapXiHanCloudEvents(this IEndpointRouteBuilder endpoints, string pattern = "/events")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        var options = endpoints.ServiceProvider.GetService<CloudEventsProtocolOptions>()
            ?? throw new FrameworkException(KernelErrors.InvalidAppState, "MapXiHanCloudEvents() requires the CloudEvents protocol feature; add UseFeature<CloudEventsProtocolFeature>() to the application.");

        return endpoints.MapPost(pattern, httpContext => HandleAsync(httpContext, options)).WithName("xihan.protocols.cloudevents");
    }

    /// <summary>
    /// 端点处理
    /// </summary>
    /// <remarks>
    /// 读事件，先把每个事件的类型解析成主题（任一事件没有映射整批 400，不处理任何事件），再派发或发布；派发按顺序处理、第一个失败即停止，发布按主题分组一次发布、第一个失败的结果作为响应（已处理的不回滚，发送方重试整批时处理器按消息 ID 去重）
    /// </remarks>
    private static async Task HandleAsync(HttpContext httpContext, CloudEventsProtocolOptions options)
    {
        if (!httpContext.FindXiHanContext().TryGetValue(out var context))
        {
            throw new FrameworkException(KernelErrors.InvalidAppState, "The CloudEvents endpoint requires UseXiHan() to run after routing and before the endpoint executes; the invocation context is missing.");
        }

        var read = await CloudEventsHttpBinding.ReadAsync(httpContext.Request, context.CancellationToken).ConfigureAwait(false);
        if (!read.TryGetValue(out var envelopes))
        {
            context.SetOutcome(read.Error!);
            return;
        }

        var topics = new string[envelopes.Count];
        for (var index = 0; index < envelopes.Count; index++)
        {
            if (!options.TopicFor(envelopes[index].Type).TryGetValue(out var topic))
            {
                context.SetOutcome(Error.Validation(CloudEventsErrors.TopicUnresolved, CloudEventsMessages.TopicUnresolved, MessageArguments.Of(("type", envelopes[index].Type))));
                return;
            }

            topics[index] = topic;
        }

        var services = httpContext.RequestServices;
        var outcome = options.Inbound == CloudEventsInboundMode.Publish
            ? await PublishAsync(services.GetRequiredService<IMessagePublisher>(), topics, envelopes, context.CancellationToken).ConfigureAwait(false)
            : await DispatchAsync(services, topics, envelopes, context.CancellationToken).ConfigureAwait(false);
        if (outcome.IsFailure)
        {
            context.SetOutcome(outcome);
            return;
        }

        httpContext.Response.StatusCode = StatusCodes.Status202Accepted;
    }

    /// <summary>
    /// 发布模式
    /// </summary>
    /// <remarks>
    /// 按主题分组（保持首次出现的主题顺序与组内到达顺序），每组一次批量发布；返回第一个失败的结果
    /// </remarks>
    private static async Task<Result> PublishAsync(IMessagePublisher publisher, string[] topics, IReadOnlyList<MessageEnvelope> envelopes, CancellationToken cancellationToken)
    {
        var groups = new Dictionary<string, List<MessageEnvelope>>(StringComparer.Ordinal);
        var order = new List<string>();
        for (var index = 0; index < envelopes.Count; index++)
        {
            if (!groups.TryGetValue(topics[index], out var group))
            {
                group = [];
                groups[topics[index]] = group;
                order.Add(topics[index]);
            }

            group.Add(envelopes[index]);
        }

        foreach (var topic in order)
        {
            foreach (var result in await publisher.PublishManyAsync(topic, groups[topic], cancellationToken).ConfigureAwait(false))
            {
                if (result.IsFailure)
                {
                    return result;
                }
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// 派发模式：逐个在请求内经管道交给本实例里主题的处理器，第一个失败即停止
    /// </summary>
    /// <remarks>
    /// 竞争订阅与广播订阅各一次投递，先竞争后广播（<c>spec/protocol/cloudevents.md</c>）。派发只发生在接收请求的这个实例，要让广播订阅到达每个实例用发布模式
    /// </remarks>
    private static async Task<Result> DispatchAsync(IServiceProvider services, string[] topics, IReadOnlyList<MessageEnvelope> envelopes, CancellationToken cancellationToken)
    {
        var subscriptions = services.GetRequiredService<MessageSubscriptions>();
        var dispatcher = services.GetRequiredService<MessageDispatcher>();
        for (var index = 0; index < envelopes.Count; index++)
        {
            var modes = _modes.Where(mode => subscriptions.HandlersFor(topics[index], mode).Count > 0).ToList();
            // 派发模式下两种方式都没有处理器的主题是配置错误：404 让发送方停止重试，而不是静默吞掉事件
            if (modes.Count == 0)
            {
                return Error.NotFound(CloudEventsErrors.NoSubscriber, CloudEventsMessages.NoSubscriber, MessageArguments.Of(("topic", topics[index])));
            }

            foreach (var mode in modes)
            {
                var outcome = await dispatcher.DispatchAsync(new MessageDelivery
                {
                    Topic = topics[index],
                    Envelope = envelopes[index],
                    Attempt = 1,
                    Mode = mode,
                }, cancellationToken).ConfigureAwait(false);
                if (outcome.IsFailure)
                {
                    return outcome;
                }
            }
        }

        return Result.Success();
    }
}
