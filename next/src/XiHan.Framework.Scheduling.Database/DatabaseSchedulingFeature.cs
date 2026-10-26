// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Leadership;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Abstractions.Storage;
using XiHan.Framework.Scheduling.Database.Entities;
using XiHan.Framework.Scheduling.Database.Leadership;
using XiHan.Framework.Scheduling.Database.Storage;

namespace XiHan.Framework.Scheduling.Database;

/// <summary>
/// 数据库调度特性（<c>role: provider</c>）：提供 <c>scheduling.jobs@1</c>（数据库作业存储 + 抽象包的引擎）与 <c>scheduling.leader@1</c>（租约行），依赖 <c>persistence.relational@1</c>，多个节点共用一个数据库。装配后顶替默认的内存调度；被别的显式 Provider 顶替的能力只留本包自己的类型。
/// 健康项 <see cref="HealthName"/> 按 <see cref="DatabaseSchedulingOptions.HealthProbeInterval"/> 周期读一次租约行，租约表不可达时条目 Degraded。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DatabaseSchedulingFeature : IHostedFeature
{
    /// <summary>
    /// 健康项名：领导租约的存储是否可达。
    /// </summary>
    public const string HealthName = "scheduling.database.leader";

    /// <summary>
    /// 引擎；只在本包被选中提供作业能力时存在。
    /// </summary>
    private JobSchedulerEngine? _engine;

    /// <summary>
    /// 启动时起循环的健康探针，停止时结束它。
    /// </summary>
    private PeriodicHealthContributor? _health;

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.scheduling.database",
        Package = "XiHan.Framework.Scheduling.Database",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Provider,
        Provides =
        [
            new CapabilityDeclaration(SchedulingCapabilities.Jobs, SchedulingCapabilities.Version),
            new CapabilityDeclaration(SchedulingCapabilities.Leader, SchedulingCapabilities.Version),
        ],
        Requires =
        [
            new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
            new CapabilityRequirement(PersistenceCapabilities.Relational, PersistenceCapabilities.Version),
        ],
        Contributes = new Contributions
        {
            Config = new ConfigContribution(SchedulerOptions.ConfigurationSection),
            Health = [HealthName],
        },
        Entry = typeof(DatabaseSchedulingFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddMessageCatalog(SchedulingMessages.Catalog);
        var options = SchedulerOptions.FromConfiguration(context.Configuration);
        var databaseOptions = DatabaseSchedulingOptions.FromConfiguration(context.Configuration);
        // 引擎选项同时是选举的节点身份；几个 Provider 读的是同一份配置，谁先注册都一样
        context.Services.TryAddSingleton(options);
        context.Services.AddSingleton(databaseOptions);
        context.Services.AddRepository<JobEntity, string>();
        context.Services.AddRepository<TriggerEntity, string>();
        context.Services.AddRepository<JobRunEntity, string>();
        context.Services.AddRepository<SchedulerNodeEntity, string>();
        context.Services.AddRepository<SchedulerLeaseEntity, string>();
        context.Services.AddSingleton<DatabaseJobStore>();
        context.Services.AddSingleton<DatabaseLeaderElection>();
        if (context.Node.IsSelectedFor(SchedulingCapabilities.Leader))
        {
            context.Services.TryAddSingleton<ILeaderElection>(static sp => sp.GetRequiredService<DatabaseLeaderElection>());
        }

        // 引擎总是用契约里的选举：本包的领导能力被顶替时就是那个显式 Provider 的实现
        if (context.Node.IsSelectedFor(SchedulingCapabilities.Jobs))
        {
            context.Services.TryAddSingleton<IJobStore>(static sp => sp.GetRequiredService<DatabaseJobStore>());
            context.Services.AddJobSchedulerEngine(options);
        }

        // 健康项按名字作键登记：几个 Provider 的探针各自独立，内核经 IHealthContributor 统一采集；探针读引擎竞争的那一行租约
        context.Services.AddKeyedSingleton(HealthName, (sp, _) => new PeriodicHealthContributor(HealthName, new LeaseProbe(sp, options.LeaseResource.Value).ProbeAsync, databaseOptions.HealthProbeInterval, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILoggerFactory>()));
        context.Services.AddSingleton<IHealthContributor>(static sp => sp.GetRequiredKeyedService<PeriodicHealthContributor>(HealthName));
    }

    /// <inheritdoc />
    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        _engine = services.GetService<JobSchedulerEngine>();
        if (_engine is not null)
        {
            await _engine.StartAsync(cancellationToken).ConfigureAwait(false);
        }

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

        if (_engine is not null)
        {
            await _engine.StopAsync(cancellationToken).ConfigureAwait(false);
            _engine = null;
        }
    }
}
