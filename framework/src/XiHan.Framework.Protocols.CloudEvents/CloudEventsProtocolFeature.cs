// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Protocols.CloudEvents.Outbound;

namespace XiHan.Framework.Protocols.CloudEvents;

/// <summary>
/// CloudEvents 协议适配器的能力包入口
/// </summary>
/// <remarks>
/// 提供 <c>protocols.cloudevents@1</c>，需要 <c>messaging.queue@1</c>（派发器、订阅表与发布者由消息 Provider 提供）；注册选项、消息目录、出站 Webhook 的 HttpClient（过出站地址策略，缺省不写追踪上下文头，见 <see cref="CloudEventsProtocolOptions.PropagateTraceContext"/>）与 <see cref="CloudEventsWebhookPublisher"/>；入站端点由应用在路由阶段调用 <c>MapXiHanCloudEvents()</c> 映射
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class CloudEventsProtocolFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.protocols.cloudevents",
        Package = "XiHan.Framework.Protocols.CloudEvents",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Protocol,
        Provides = [new CapabilityDeclaration(CloudEventsProtocolCapabilities.CloudEvents, CloudEventsProtocolCapabilities.Version)],
        Requires =
        [
            new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
            new CapabilityRequirement(MessagingCapabilities.Queue, MessagingCapabilities.Version),
        ],
        // 清单只声明领域级配置节 XiHan:Protocols；本适配器读其下的 CloudEvents 子节
        Contributes = new Contributions { Config = new ConfigContribution(CloudEventsProtocolOptions.ConfigurationSection) },
        Entry = typeof(CloudEventsProtocolFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddMessageCatalog(CloudEventsMessages.Catalog);
        var options = CloudEventsProtocolOptions.FromConfiguration(context.Configuration);
        context.Services.AddSingleton(options);
        var client = context.Services.AddHttpClient(CloudEventsWebhookPublisher.HttpClientName);
        // 地址是运维配的，但接收方是第三方，它的 3xx 能把请求引到内网：过出站地址策略，重定向也在连接时检查（ADR-0117）
        client.UseSocketsHttpHandler(static (handler, services) => services.GetRequiredService<OutboundAddressPolicy>().Apply(handler));
        if (!options.PropagateTraceContext)
        {
            // Webhook 的接收方是第三方：启用追踪时 HttpClient 会按当前 Activity 写 traceparent / tracestate / baggage 头，把内部追踪 ID 与 Baggage 推出去，
            // 与信封不补内部扩展同理缺省关掉。只改这个命名客户端主处理器的传播器，不动进程全局的 DistributedContextPropagator.Current；
            // 主处理器换成 SocketsHttpHandler 才有这个开关，应用之后用 UseSocketsHttpHandler 配置的是同一个实例，设置保留
            client.UseSocketsHttpHandler(static (handler, _) => handler.ActivityHeadersPropagator = null);
        }

        context.Services.AddSingleton<CloudEventsWebhookPublisher>();
    }
}
