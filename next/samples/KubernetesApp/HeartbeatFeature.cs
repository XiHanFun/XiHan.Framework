// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Scheduling.Abstractions;

namespace KubernetesApp;

/// <summary>
/// 登记心跳作业的能力包：依赖调度能力，作业由当前选中的调度器按计划运行。
/// </summary>
public sealed class HeartbeatFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.samples.heartbeat",
        Package = "KubernetesApp",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Experimental,
        Kind = ManifestKind.Capability,
        Requires =
        [
            new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
            new CapabilityRequirement(SchedulingCapabilities.Jobs, SchedulingCapabilities.Version),
        ],
        Entry = typeof(HeartbeatFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        context.Services.AddSingleton<HeartbeatCounter>();
        context.Services.AddJob<HeartbeatJob>("heartbeat", JobSchedule.Every(TimeSpan.FromSeconds(10)));
    }
}
