// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Runtime.InteropServices;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Observability.Abstractions;

/// <summary>
/// 操作系统、.NET、进程与运行时长。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record RuntimeInfo
{
    /// <summary>
    /// 操作系统描述。
    /// </summary>
    public required string OsDescription { get; init; }

    /// <summary>
    /// 平台名。
    /// </summary>
    /// <remarks>
    /// <c>windows</c>、<c>linux</c>、<c>macos</c>、<c>freebsd</c> 或 <c>other</c>。
    /// </remarks>
    public required string OsPlatform { get; init; }

    /// <summary>
    /// 操作系统版本。
    /// </summary>
    public required string OsVersion { get; init; }

    /// <summary>
    /// 操作系统架构。
    /// </summary>
    public required Architecture OsArchitecture { get; init; }

    /// <summary>
    /// .NET 描述。
    /// </summary>
    public required string Framework { get; init; }

    /// <summary>
    /// 运行时标识（RID）。
    /// </summary>
    public required string RuntimeIdentifier { get; init; }

    /// <summary>
    /// 进程架构。
    /// </summary>
    public required Architecture ProcessArchitecture { get; init; }

    /// <summary>
    /// 机器名。
    /// </summary>
    public required string MachineName { get; init; }

    /// <summary>
    /// 运行进程的用户名。
    /// </summary>
    public string? UserName { get; init; }

    /// <summary>
    /// 是否在交互式会话里运行。
    /// </summary>
    public required bool Interactive { get; init; }

    /// <summary>
    /// 进程 ID。
    /// </summary>
    public required int ProcessId { get; init; }

    /// <summary>
    /// 进程名。
    /// </summary>
    public required string ProcessName { get; init; }

    /// <summary>
    /// 进程启动时刻。
    /// </summary>
    public DateTimeOffset? ProcessStartedAt { get; init; }

    /// <summary>
    /// 进程运行时长。
    /// </summary>
    public TimeSpan? ProcessUptime { get; init; }

    /// <summary>
    /// 系统启动时刻。
    /// </summary>
    public DateTimeOffset? SystemStartedAt { get; init; }

    /// <summary>
    /// 系统运行时长。
    /// </summary>
    public TimeSpan? SystemUptime { get; init; }

    /// <summary>
    /// 进程工作集字节数。
    /// </summary>
    public required long WorkingSetBytes { get; init; }

    /// <summary>
    /// 进程私有字节数。
    /// </summary>
    public long? PrivateBytes { get; init; }

    /// <summary>
    /// 托管堆字节数。
    /// </summary>
    public required long ManagedHeapBytes { get; init; }

    /// <summary>
    /// 进程线程数。
    /// </summary>
    public int? ThreadCount { get; init; }

    /// <summary>
    /// 进程句柄数。
    /// </summary>
    public int? HandleCount { get; init; }

    /// <summary>
    /// 第 0 代 GC 次数。
    /// </summary>
    public required int Gen0Collections { get; init; }

    /// <summary>
    /// 第 1 代 GC 次数。
    /// </summary>
    public required int Gen1Collections { get; init; }

    /// <summary>
    /// 第 2 代 GC 次数。
    /// </summary>
    public required int Gen2Collections { get; init; }

    /// <summary>
    /// 进程 CPU 使用率（0–100）。
    /// </summary>
    /// <remarks>
    /// 按全部逻辑核心折算；启动后第一个采样间隔内为空。
    /// </remarks>
    public double? ProcessCpuUsagePercent { get; init; }
}
