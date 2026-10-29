// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Protocols.CloudEvents.Outbound;

namespace XiHan.Framework.Protocols.CloudEvents;

/// <summary>
/// CloudEvents 协议适配器的能力包入口。
/// </summary>
/// <remarks>
/// 提供 <c>protocols.cloudevents@1</c>，需要 <c>messaging.queue@1</c>（派发器、订阅表与发布者由消息 Provider 提供）；注册选项、消息目录、出站 Webhook 的 HttpClient 与 <see cref="CloudEventsWebhookPublisher"/>；入站端点由应用在路由阶段调用 <c>MapXiHanCloudEvents()</c> 映射。
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
        Contributes = new Contributions { Config = new ConfigContribution("XiHan:Protocols") },
        Entry = typeof(CloudEventsProtocolFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddMessageCatalog(CloudEventsMessages.Catalog);
        context.Services.AddSingleton(CloudEventsProtocolOptions.FromConfiguration(context.Configuration));
        context.Services.AddHttpClient(CloudEventsWebhookPublisher.HttpClientName);
        context.Services.AddSingleton<CloudEventsWebhookPublisher>();
    }
}
