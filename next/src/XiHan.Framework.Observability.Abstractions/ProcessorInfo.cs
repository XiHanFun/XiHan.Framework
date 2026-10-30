// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Runtime.InteropServices;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Observability.Abstractions;

/// <summary>
/// 处理器与系统 CPU 使用率。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ProcessorInfo
{
    /// <summary>
    /// 型号。
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 厂商标识。
    /// </summary>
    /// <remarks>
    /// 如 <c>GenuineIntel</c>、<c>AuthenticAMD</c>。
    /// </remarks>
    public string? Vendor { get; init; }

    /// <summary>
    /// 架构。
    /// </summary>
    public required Architecture Architecture { get; init; }

    /// <summary>
    /// 物理核心数。
    /// </summary>
    public int? PhysicalCores { get; init; }

    /// <summary>
    /// 逻辑核心数。
    /// </summary>
    /// <remarks>
    /// .NET 看到的可用核心数，容器里受 CPU 限额约束。
    /// </remarks>
    public required int LogicalCores { get; init; }

    /// <summary>
    /// 标称频率（MHz）。
    /// </summary>
    public double? FrequencyMhz { get; init; }

    /// <summary>
    /// 最后一级缓存的字节数。
    /// </summary>
    public long? CacheBytes { get; init; }

    /// <summary>
    /// 系统 CPU 使用率（0–100）。
    /// </summary>
    /// <remarks>
    /// 启动后第一个采样间隔内为空。
    /// </remarks>
    public double? UsagePercent { get; init; }
}
