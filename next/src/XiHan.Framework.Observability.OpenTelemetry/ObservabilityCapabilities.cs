// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Observability.OpenTelemetry;

/// <summary>
/// 可观测性的能力 ID 与接口版本。
/// </summary>
/// <remarks>
/// 内核直接基于 <c>System.Diagnostics</c>，没有抽象包；本能力只约定导出器的装配。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class ObservabilityCapabilities
{
    /// <summary>
    /// 导出器：把 Activity、Meter 与日志导出到后端。
    /// </summary>
    public const string Export = "observability.export";

    /// <summary>
    /// 当前接口版本。
    /// </summary>
    public const int Version = 1;
}
