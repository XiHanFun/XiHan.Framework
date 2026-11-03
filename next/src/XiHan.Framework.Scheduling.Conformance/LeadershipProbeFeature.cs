// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Scheduling.Abstractions;

namespace XiHan.Framework.Scheduling.Conformance;

/// <summary>
/// 应用生命周期用例的探针特性
/// </summary>
/// <remarks>
/// 依赖 <c>scheduling.jobs</c>：在调度特性（已起引擎）之后启动、之前停止，启动与停止两个钩子都正赶上应用不在服务状态（启动中、排空中），用例在钩子里直接执行引擎的周期
/// </remarks>
internal sealed class LeadershipProbeFeature : IHostedFeature
{
    /// <summary>
    /// 启动钩子：收到宿主的服务
    /// </summary>
    private readonly Func<IServiceProvider, CancellationToken, Task> _onStart;

    /// <summary>
    /// 停止钩子
    /// </summary>
    private readonly Func<CancellationToken, Task> _onStop;

    /// <summary>
    /// 创建
    /// </summary>
    public LeadershipProbeFeature(Func<IServiceProvider, CancellationToken, Task> onStart, Func<CancellationToken, Task> onStop)
    {
        _onStart = onStart;
        _onStop = onStop;
    }

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.scheduling.conformance",
        Package = "XiHan.Framework.Scheduling.Conformance",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(SchedulingCapabilities.Jobs, SchedulingCapabilities.Version)],
        Entry = typeof(LeadershipProbeFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context) => ArgumentNullException.ThrowIfNull(context);

    /// <inheritdoc />
    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken) => _onStart(services, cancellationToken);

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => _onStop(cancellationToken);
}
