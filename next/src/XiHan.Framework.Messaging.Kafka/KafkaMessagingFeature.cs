// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Messaging.Kafka;

/// <summary>
/// Kafka 消息的能力包入口：显式 Provider，同时提供 <c>messaging.queue@1</c> 与 <c>messaging.stream@1</c>；启动时连接集群并开始消费订阅的主题，停止时先停消费再释放生产者。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class KafkaMessagingFeature : IHostedFeature
{
    /// <summary>
    /// 连接绑定名：<c>stream</c>，类型 <c>stream</c>，可选；值是引导地址。
    /// </summary>
    public const string ConnectionBinding = "stream";

    /// <summary>
    /// 运行时；启动后保存，停止时使用。
    /// </summary>
    private MessagingRuntime? _runtime;

    /// <summary>
    /// 代理；启动后保存，停止时释放。
    /// </summary>
    private KafkaMessageBroker? _broker;

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.messaging.kafka",
        Package = "XiHan.Framework.Messaging.Kafka",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Provider,
        Provides =
        [
            new CapabilityDeclaration(MessagingCapabilities.Queue, MessagingCapabilities.Version),
            new CapabilityDeclaration(MessagingCapabilities.Stream, MessagingCapabilities.Version),
        ],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Bindings = [new Binding(ConnectionBinding, "stream", required: false)],
        Contributes = new Contributions
        {
            Config = new ConfigContribution("XiHan:Messaging"),
            Health = ["messaging.kafka.connection"],
        },
        Entry = typeof(KafkaMessagingFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // 本子系统错误码的多语言目录；同名目录只注册一次，多个 Provider 同时装配也不重复
        context.Services.AddMessageCatalog(MessagingMessages.Catalog);
        context.Services.AddSingleton(KafkaMessagingOptions.FromConfiguration(context.Configuration, context.TryGetBinding(ConnectionBinding)));
        context.Services.AddSingleton<KafkaMessageBroker>();
        // 契约接口只在本包被选中提供对应能力时才绑定
        if (context.Node.IsSelectedFor(MessagingCapabilities.Queue))
        {
            context.Services.TryAddSingleton<IMessageBroker>(static sp => sp.GetRequiredService<KafkaMessageBroker>());
        }

        if (context.Node.IsSelectedFor(MessagingCapabilities.Stream))
        {
            context.Services.TryAddSingleton<IMessageReplayer>(static sp => sp.GetRequiredService<KafkaMessageBroker>());
        }

        MessagingRegistration.Configure(context);
    }

    /// <inheritdoc />
    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        _broker = services.GetRequiredService<KafkaMessageBroker>();
        // 连不上视为启动失败，不降级运行
        await _broker.ConnectAsync(cancellationToken).ConfigureAwait(false);
        _runtime = services.GetRequiredService<MessagingRuntime>();
        await _runtime.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_runtime is not null)
        {
            await _runtime.StopAsync(cancellationToken).ConfigureAwait(false);
        }

        if (_broker is not null)
        {
            await _broker.DisposeAsync().ConfigureAwait(false);
            _broker = null;
        }
    }
}
