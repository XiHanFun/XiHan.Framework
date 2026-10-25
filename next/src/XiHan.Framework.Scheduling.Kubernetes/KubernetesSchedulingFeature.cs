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
using XiHan.Framework.Scheduling.Kubernetes.Leases;

namespace XiHan.Framework.Scheduling.Kubernetes;

/// <summary>
/// Kubernetes 领导选举的能力包入口：提供 <c>scheduling.leader@1</c>，顶替内存实现的选举；作业调度仍由 <c>Scheduling.InMemory</c> 的调度器承担，它用这里的选举决定哪个 Pod 按计划运行。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class KubernetesSchedulingFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.scheduling.kubernetes",
        Package = "XiHan.Framework.Scheduling.Kubernetes",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Provider,
        Provides = [new CapabilityDeclaration(SchedulingCapabilities.Leader, SchedulingCapabilities.Version)],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions { Config = new ConfigContribution("XiHan:Scheduling") },
        Entry = typeof(KubernetesSchedulingFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddMessageCatalog(SchedulingMessages.Catalog);
        var options = KubernetesSchedulingOptions.FromConfiguration(context.Configuration);
        context.Services.AddSingleton(options);
        // 客户端可被应用（或测试）预先注册的实例替代
        context.Services.TryAddSingleton(static provider => KubernetesLeaseClient.CreateInCluster(provider.GetRequiredService<KubernetesSchedulingOptions>()));
        context.Services.AddSingleton<KubernetesLeaderElection>();
        if (context.Node.IsSelectedFor(SchedulingCapabilities.Leader))
        {
            context.Services.TryAddSingleton<ILeaderElection>(static provider => provider.GetRequiredService<KubernetesLeaderElection>());
        }
    }
}
