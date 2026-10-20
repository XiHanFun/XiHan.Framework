// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Diagnostics;

/// <summary>
/// 内核的诊断源。直接基于 <see cref="System.Diagnostics"/>，OpenTelemetry 桥接包只需订阅同名的 ActivitySource 与 Meter。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class FrameworkDiagnostics
{
    /// <summary>
    /// ActivitySource 与 Meter 的名称。
    /// </summary>
    public const string SourceName = "XiHan.Framework";

    /// <summary>
    /// 内核的 ActivitySource。
    /// </summary>
    public static ActivitySource ActivitySource { get; } = new(SourceName, FrameworkInfo.Version);

    /// <summary>
    /// 内核的 Meter。
    /// </summary>
    public static Meter Meter { get; } = new(SourceName, FrameworkInfo.Version);

    /// <summary>
    /// 调用计数，按结果标记。
    /// </summary>
    public static Counter<long> Invocations { get; } = Meter.CreateCounter<long>("xihan.invocations", "{invocation}", "管道调用次数");

    /// <summary>
    /// 调用耗时直方图。
    /// </summary>
    public static Histogram<double> InvocationDuration { get; } = Meter.CreateHistogram<double>("xihan.invocation.duration", "ms", "管道调用耗时");
}
