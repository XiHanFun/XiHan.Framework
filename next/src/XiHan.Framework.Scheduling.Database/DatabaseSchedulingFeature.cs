// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DatabaseSchedulingFeature : IHostedFeature
{
    /// <summary>
    /// 引擎；只在本包被选中提供作业能力时存在。
    /// </summary>
    private JobSchedulerEngine? _engine;

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
            Health = ["scheduling.database.leader"],
        },
        Entry = typeof(DatabaseSchedulingFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddMessageCatalog(SchedulingMessages.Catalog);
        var options = SchedulerOptions.FromConfiguration(context.Configuration);
        // 引擎选项同时是选举的节点身份；几个 Provider 读的是同一份配置，谁先注册都一样
        context.Services.TryAddSingleton(options);
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
    }

    /// <inheritdoc />
    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        _engine = services.GetService<JobSchedulerEngine>();
        return _engine?.StartAsync(cancellationToken) ?? Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_engine is not null)
        {
            await _engine.StopAsync(cancellationToken).ConfigureAwait(false);
            _engine = null;
        }
    }
}
