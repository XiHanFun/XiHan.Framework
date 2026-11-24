// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Messaging.Abstractions.Outbox;

/// <summary>
/// 事务性发件箱特性
/// </summary>
/// <remarks>
/// 需要 <c>messaging.queue@1</c> 与 <c>persistence.relational@1</c>；把 <see cref="IMessagePublisher"/> 换成 <see cref="OutboxPublisher"/>，登记 <see cref="OutboxMessage"/> 实体，启动后运行投递循环。健康项 <see cref="HealthName"/> 按 <see cref="OutboxOptions.HealthProbeInterval"/> 周期数一遍发件箱：未投递的行数或最老一条的等待时间超过阈值、或死信行数超过阈值时 Degraded，读不出时同样 Degraded；死信用 <see cref="OutboxDeadLetters"/> 重投（ADR-0272）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class OutboxFeature : IHostedFeature, IDisposable
{
    /// <summary>
    /// 健康项名：发件箱的积压与死信是否在阈值之内
    /// </summary>
    public const string HealthName = "messaging.outbox.backlog";

    /// <summary>
    /// 停止信号
    /// </summary>
    /// <remarks>
    /// <see cref="StopAsync"/> 取消它让投递循环退出
    /// </remarks>
    private CancellationTokenSource? _stopping;

    /// <summary>
    /// 正在运行的投递循环
    /// </summary>
    /// <remarks>
    /// 停止时等待它结束
    /// </remarks>
    private Task? _loop;

    /// <summary>
    /// 启动时起循环的健康探针，停止时结束它
    /// </summary>
    private PeriodicHealthContributor? _health;

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.messaging.outbox",
        Package = "XiHan.Framework.Messaging.Abstractions",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Stable,
        Kind = ManifestKind.Capability,
        Requires =
        [
            new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
            new CapabilityRequirement(MessagingCapabilities.Queue, MessagingCapabilities.Version),
            new CapabilityRequirement(PersistenceCapabilities.Relational, PersistenceCapabilities.Version),
        ],
        Contributes = new Contributions
        {
            Config = new ConfigContribution(OutboxOptions.ConfigurationSection),
            Health = [HealthName],
        },
        Entry = typeof(OutboxFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = OutboxOptions.FromConfiguration(context.Configuration);
        context.Services.AddSingleton(options);
        context.Services.AddRepository<OutboxMessage, Guid>();
        // 用 Replace 而不是 TryAdd：Provider 已注册的 DirectPublisher 必须让位给发件箱发布者
        context.Services.Replace(ServiceDescriptor.Scoped<IMessagePublisher, OutboxPublisher>());
        // 投递循环、健康探针与死信重投共用一份库的清单：分片清单只读一份，三处覆盖的库总是同一批
        context.Services.TryAddSingleton(static sp => new OutboxDatabases(sp, sp.GetRequiredService<OutboxOptions>(), sp.GetRequiredService<TimeProvider>()));
        context.Services.TryAddSingleton(static sp => new OutboxDispatcher(sp.GetRequiredService<OutboxDatabases>(), sp.GetRequiredService<IMessageBroker>(), sp.GetRequiredService<OutboxOptions>(), sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILoggerFactory>()));
        context.Services.TryAddSingleton(static sp => new OutboxDeadLetters(sp.GetRequiredService<OutboxDatabases>(), sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILoggerFactory>()));
        context.Services.TryAddSingleton(static sp => new OutboxBacklogProbe(sp.GetRequiredService<OutboxDatabases>(), sp.GetRequiredService<OutboxOptions>(), sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILoggerFactory>().CreateLogger<OutboxBacklogProbe>()));
        // 健康项按名字作键登记：周期探测缓存状态，条目的数据由探针最近一次数出的数字补上
        context.Services.AddKeyedSingleton(HealthName, (sp, _) => new PeriodicHealthContributor(HealthName, sp.GetRequiredService<OutboxBacklogProbe>().ProbeAsync, options.HealthProbeInterval, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILoggerFactory>()));
        context.Services.AddSingleton<IHealthContributor>(static sp => new OutboxHealth(sp.GetRequiredKeyedService<PeriodicHealthContributor>(HealthName), sp.GetRequiredService<OutboxBacklogProbe>()));
    }

    /// <inheritdoc />
    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        _stopping = new CancellationTokenSource();
        _loop = services.GetRequiredService<OutboxDispatcher>().RunAsync(_stopping.Token);
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

        if (_stopping is null || _loop is null)
        {
            return;
        }

        await _stopping.CancelAsync().ConfigureAwait(false);
        await _loop.ConfigureAwait(false);
        _stopping.Dispose();
        _stopping = null;
        _loop = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stopping?.Dispose();
        _stopping = null;
    }
}
