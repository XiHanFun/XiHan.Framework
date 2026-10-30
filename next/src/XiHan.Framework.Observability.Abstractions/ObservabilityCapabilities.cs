// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Observability.Abstractions;

/// <summary>
/// 可观测性的能力 ID 与接口版本。
/// </summary>
/// <remarks>
/// 导出与运行信息两项能力（RFC-0023）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class ObservabilityCapabilities
{
    /// <summary>
    /// 导出器：把 Activity、Meter 与日志导出到后端。
    /// </summary>
    public const string Export = "observability.export";

    /// <summary>
    /// 运行信息：进程与主机的 CPU、内存、磁盘、网卡、GPU 与主板。
    /// </summary>
    public const string Host = "observability.host";

    /// <summary>
    /// 当前接口版本。
    /// </summary>
    public const int Version = 1;
}
