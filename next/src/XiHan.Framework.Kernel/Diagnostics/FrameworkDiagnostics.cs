// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Diagnostics;

/// <summary>
/// 内核的诊断源。
/// </summary>
/// <remarks>
/// 直接基于 <see cref="System.Diagnostics"/>，OpenTelemetry 桥接包只需订阅同名的 ActivitySource 与 Meter。
/// </remarks>
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
    public static Counter<long> Invocations { get; } = Meter.CreateCounter<long>("xihan.invocations", "{invocation}", "Number of pipeline invocations.");

    /// <summary>
    /// 调用耗时直方图。
    /// </summary>
    public static Histogram<double> InvocationDuration { get; } = Meter.CreateHistogram<double>("xihan.invocation.duration", "ms", "Duration of pipeline invocations.");

    /// <summary>
    /// Activity 的采样标志是否是一次采样决定。
    /// </summary>
    /// <remarks>
    /// 经具名 ActivitySource 创建（监听器做过采样）或有父级（标志随父级）时是；不经 ActivitySource 直接构造的根 Activity（如没有监听器时 ASP.NET Core 为日志创建的请求 Activity）的标志只是缺省值，不代表「不采样」。
    /// </remarks>
    internal static bool HasSamplingDecision(Activity activity)
        => activity.Source.Name.Length > 0 || activity.ParentSpanId != default;
}
