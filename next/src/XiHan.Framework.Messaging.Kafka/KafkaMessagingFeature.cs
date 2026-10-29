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

namespace XiHan.Framework.Messaging.Kafka;

/// <summary>
/// Kafka 消息的能力包入口。
/// </summary>
/// <remarks>
/// 显式 Provider，同时提供 <c>messaging.queue@1</c> 与 <c>messaging.stream@1</c>；启动时连接集群并开始消费订阅的主题，停止时先停消费再释放生产者。健康项 <see cref="HealthName"/> 按 <see cref="KafkaMessagingOptions.HealthProbeInterval"/> 周期经生产者的连接描述集群。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class KafkaMessagingFeature : IHostedFeature
{
    /// <summary>
    /// 连接绑定名。
    /// </summary>
    /// <remarks>
    /// <c>stream</c>，类型 <c>stream</c>，可选；值是引导地址。
    /// </remarks>
    public const string ConnectionBinding = "stream";

    /// <summary>
    /// 健康项名：集群是否可达。
    /// </summary>
    public const string HealthName = "messaging.kafka.connection";

    /// <summary>
    /// 运行时。
    /// </summary>
    /// <remarks>
    /// 启动后保存，停止时使用。
    /// </remarks>
    private MessagingRuntime? _runtime;

    /// <summary>
    /// 代理。
    /// </summary>
    /// <remarks>
    /// 启动后保存，停止时释放。
    /// </remarks>
    private KafkaMessageBroker? _broker;

    /// <summary>
    /// 启动时起循环的健康探针，停止时结束它。
    /// </summary>
    private PeriodicHealthContributor? _health;

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
            Health = [HealthName],
        },
        Entry = typeof(KafkaMessagingFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // 本子系统错误码的多语言目录；同名目录只注册一次，多个 Provider 同时装配也不重复
        context.Services.AddMessageCatalog(MessagingMessages.Catalog);
        var options = KafkaMessagingOptions.FromConfiguration(context.Configuration, context.TryGetBinding(ConnectionBinding));
        context.Services.AddSingleton(options);
        context.Services.AddSingleton<KafkaMessageBroker>();
        // 健康项按名字作键登记：几个 Provider 的探针各自独立，内核经 IHealthContributor 统一采集
        context.Services.AddKeyedSingleton(HealthName, (sp, _) => new PeriodicHealthContributor(HealthName, ct => sp.GetRequiredService<KafkaMessageBroker>().ProbeAsync(ct), options.HealthProbeInterval, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILoggerFactory>()));
        context.Services.AddSingleton<IHealthContributor>(static sp => sp.GetRequiredKeyedService<PeriodicHealthContributor>(HealthName));
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
