// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Messaging.RabbitMQ;

/// <summary>
/// RabbitMQ 消息的能力包入口：提供 <c>messaging.queue@1</c>；启动时连接并消费全部订阅主题，停止时关闭。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class RabbitMQMessagingFeature : IHostedFeature
{
    /// <summary>
    /// 连接绑定名：<c>queue</c>，类型 <c>queue</c>，可选。
    /// </summary>
    public const string ConnectionBinding = "queue";

    private MessagingRuntime? _runtime;
    private RabbitMQMessageBroker? _broker;

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.messaging.rabbitmq",
        Package = "XiHan.Framework.Messaging.RabbitMQ",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Provider,
        Provides = [new CapabilityDeclaration(MessagingCapabilities.Queue, MessagingCapabilities.Version)],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Bindings = [new Binding(ConnectionBinding, "queue", required: false)],
        Contributes = new Contributions
        {
            Config = new ConfigContribution("XiHan:Messaging"),
            Health = ["messaging.rabbitmq.connection"],
        },
        Entry = typeof(RabbitMQMessagingFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddSingleton(RabbitMQMessagingOptions.FromConfiguration(context.Configuration, context.TryGetBinding(ConnectionBinding)));
        context.Services.AddSingleton<RabbitMQMessageBroker>();
        context.Services.TryAddSingleton<IMessageBroker>(static sp => sp.GetRequiredService<RabbitMQMessageBroker>());
        MessagingRegistration.Configure(context);
    }

    /// <inheritdoc />
    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        _broker = services.GetRequiredService<RabbitMQMessageBroker>();
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
