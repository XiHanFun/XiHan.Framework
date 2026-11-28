// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Observability.OpenTelemetry.Tests;

/// <summary>
/// 启动时探测追踪源与计量仪表有没有监听者的特性
/// </summary>
/// <remarks>
/// 用来观察 OpenTelemetry 的提供者是不是在特性启动之前就已建出：建出之后，配置里登记的追踪源有监听者，登记的计量里新建的仪表处于启用状态
/// </remarks>
/// <param name="source">要探测的追踪源名</param>
/// <param name="meter">要探测的计量名</param>
internal sealed class TelemetryProbeFeature(string source, string meter) : IHostedFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.test.probe",
        Version = "5.0.0",
        Primitives = 1,
        Stability = Stability.Stable,
        Kind = ManifestKind.Capability,
    };

    /// <summary>
    /// 启动时追踪源有没有监听者
    /// </summary>
    /// <remarks>
    /// 还没启动为 <see langword="null"/>
    /// </remarks>
    public bool? TracingListened { get; private set; }

    /// <summary>
    /// 启动时新建的计数器是否启用
    /// </summary>
    /// <remarks>
    /// 还没启动为 <see langword="null"/>
    /// </remarks>
    public bool? MetricsListened { get; private set; }

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
    }

    /// <inheritdoc />
    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        using var activities = new ActivitySource(source);
        TracingListened = activities.HasListeners();
        using var instruments = new Meter(meter);
        MetricsListened = instruments.CreateCounter<long>("probe").Enabled;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
