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

namespace XiHan.Framework.Scheduling.InMemory;

/// <summary>
/// 内存调度的能力包入口：默认提供 <c>scheduling.jobs@1</c> 与 <c>scheduling.leader@1</c>。只为被选中的能力绑定契约：另有显式 Provider 提供领导选举时，调度器用那个选举。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemorySchedulingFeature : IHostedFeature
{
    /// <summary>
    /// 启动后保存的调度器，供停止时使用。
    /// </summary>
    private InMemoryJobScheduler? _scheduler;

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
            Config = new ConfigContribution("XiHan:Scheduling"),
            Health = ["scheduling.inmemory.leader"],
        },
        Entry = typeof(InMemorySchedulingFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddMessageCatalog(SchedulingMessages.Catalog);
        context.Services.AddSingleton(InMemorySchedulingOptions.FromConfiguration(context.Configuration));
        context.Services.GetJobRegistry();
        context.Services.TryAddSingleton<JobDispatcher>();
        context.Services.AddSingleton<InMemoryLeaderElection>();
        if (context.Node.IsSelectedFor(SchedulingCapabilities.Leader))
        {
            context.Services.TryAddSingleton<ILeaderElection>(static sp => sp.GetRequiredService<InMemoryLeaderElection>());
        }

        // 调度器总是用契约里的选举：本包被顶替时就是显式 Provider 的实现
        if (context.Node.IsSelectedFor(SchedulingCapabilities.Jobs))
        {
            context.Services.AddSingleton<InMemoryJobScheduler>();
            context.Services.TryAddSingleton<IJobScheduler>(static sp => sp.GetRequiredService<InMemoryJobScheduler>());
        }
    }

    /// <inheritdoc />
    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        _scheduler = services.GetService<InMemoryJobScheduler>();
        return _scheduler?.StartAsync(cancellationToken) ?? Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_scheduler is not null)
        {
            await _scheduler.StopAsync(cancellationToken).ConfigureAwait(false);
            _scheduler = null;
        }
    }
}
