// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Leadership;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Abstractions.Storage;

namespace XiHan.Framework.Scheduling.InMemory;

/// <summary>
/// 内存调度的能力包入口：提供 <c>scheduling.jobs@1</c>（进程内作业存储 + 抽象包的引擎）与 <c>scheduling.leader@1</c>（进程内租约）。被显式 Provider 顶替的能力只留本包自己的类型。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemorySchedulingFeature : IHostedFeature
{
    /// <summary>
    /// 健康项名：领导权状态（进程内租约，持有与否都是 Ready；引擎停止为 Stopped）。
    /// </summary>
    public const string HealthName = "scheduling.inmemory.leader";

    /// <summary>
    /// 引擎；只在本包被选中提供作业能力时存在。
    /// </summary>
    private JobSchedulerEngine? _engine;

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.scheduling.inmemory",
        Package = "XiHan.Framework.Scheduling.InMemory",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Default,
        Provides =
        [
            new CapabilityDeclaration(SchedulingCapabilities.Jobs, SchedulingCapabilities.Version),
            new CapabilityDeclaration(SchedulingCapabilities.Leader, SchedulingCapabilities.Version),
        ],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions
        {
            Config = new ConfigContribution(SchedulerOptions.ConfigurationSection),
            Health = [HealthName],
        },
        Entry = typeof(InMemorySchedulingFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddMessageCatalog(SchedulingMessages.Catalog);
        context.Services.AddSingleton(InMemorySchedulingOptions.FromConfiguration(context.Configuration));
        context.Services.AddSingleton<InMemoryJobStore>();
        context.Services.AddSingleton<InMemoryLeaderElection>();
        // 健康项看引擎是否在跑；本包没被选中提供作业能力时没有引擎，取到 null
        context.Services.AddSingleton<IHealthContributor>(static sp => new InMemorySchedulingHealthContributor(sp.GetService<JobSchedulerEngine>()));
        if (context.Node.IsSelectedFor(SchedulingCapabilities.Leader))
        {
            context.Services.TryAddSingleton<ILeaderElection>(static sp => sp.GetRequiredService<InMemoryLeaderElection>());
        }

        // 引擎总是用契约里的选举：本包被顶替时就是显式 Provider 的实现（不共享的存储下集群唯一的作业只在领导者上运行）
        if (context.Node.IsSelectedFor(SchedulingCapabilities.Jobs))
        {
            context.Services.TryAddSingleton<IJobStore>(static sp => sp.GetRequiredService<InMemoryJobStore>());
            context.Services.AddJobSchedulerEngine(SchedulerOptions.FromConfiguration(context.Configuration));
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
