// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Messaging.RabbitMQ;

/// <summary>
/// RabbitMQ 消息的能力包入口。
/// </summary>
/// <remarks>
/// 提供 <c>messaging.queue@1</c>；启动时连接并消费全部订阅主题，停止时关闭。健康项 <see cref="HealthName"/> 按 <see cref="RabbitMQMessagingOptions.HealthProbeInterval"/> 周期检查连接与发布通道是否打开。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class RabbitMQMessagingFeature : IHostedFeature
{
    /// <summary>
    /// 连接绑定名：<c>queue</c>，类型 <c>queue</c>，可选。
    /// </summary>
    public const string ConnectionBinding = "queue";

    /// <summary>
    /// 健康项名：代理连接是否打开。
    /// </summary>
    public const string HealthName = "messaging.rabbitmq.connection";

    /// <summary>
    /// 启动后取得的运行时。
    /// </summary>
    /// <remarks>
    /// 停止时用它结束消费。
    /// </remarks>
    private MessagingRuntime? _runtime;

    /// <summary>
    /// 启动时连接的代理。
    /// </summary>
    /// <remarks>
    /// 停止时释放连接。
    /// </remarks>
    private RabbitMQMessageBroker? _broker;

    /// <summary>
    /// 启动时起循环的健康探针，停止时结束它。
    /// </summary>
    private PeriodicHealthContributor? _health;

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
            Health = [HealthName],
        },
        Entry = typeof(RabbitMQMessagingFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // 本子系统错误码的多语言目录；同名目录只注册一次，多个 Provider 同时装配也不重复
        context.Services.AddMessageCatalog(MessagingMessages.Catalog);
        var options = RabbitMQMessagingOptions.FromConfiguration(context.Configuration, context.TryGetBinding(ConnectionBinding));
        context.Services.AddSingleton(options);
        context.Services.AddSingleton<RabbitMQMessageBroker>();
        context.Services.TryAddSingleton<IMessageBroker>(static sp => sp.GetRequiredService<RabbitMQMessageBroker>());
        MessagingRegistration.Configure(context);
        // 健康项按名字作键登记：几个 Provider 的探针各自独立，内核经 IHealthContributor 统一采集
        context.Services.AddKeyedSingleton(HealthName, (sp, _) => new PeriodicHealthContributor(HealthName, _ => ValueTask.FromResult(sp.GetRequiredService<RabbitMQMessageBroker>().CheckConnection()), options.HealthProbeInterval, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILoggerFactory>()));
        context.Services.AddSingleton<IHealthContributor>(static sp => sp.GetRequiredKeyedService<PeriodicHealthContributor>(HealthName));
    }

    /// <inheritdoc />
    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        _broker = services.GetRequiredService<RabbitMQMessageBroker>();
        await _broker.ConnectAsync(cancellationToken).ConfigureAwait(false);
        _runtime = services.GetRequiredService<MessagingRuntime>();
        await _runtime.StartAsync(cancellationToken).ConfigureAwait(false);
        _health = services.GetRequiredKeyedService<PeriodicHealthContributor>(HealthName);
        await _health.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_health is not null)
        {
            await _health.StopAsync(cancellationToken).ConfigureAwait(false);
            _health = null;
        }

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
