// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Observability.Abstractions;

/// <summary>
/// 进程与所在主机的运行信息。
/// </summary>
/// <remarks>
/// 读取不阻塞、不抛出、不启动外部命令（RFC-0023）：读不到的项为 <see langword="null"/>，不以 0 或空串顶替；使用率与速率取后台采样器最近一次的值，启动后第一个采样间隔内为空。CPU 型号与核心、GPU、主板首次读取后缓存。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IHostMonitor
{
    /// <summary>
    /// 操作系统、.NET、进程与运行时长。
    /// </summary>
    RuntimeInfo ReadRuntime();

    /// <summary>
    /// 处理器与系统 CPU 使用率。
    /// </summary>
    ProcessorInfo ReadProcessor();

    /// <summary>
    /// 物理内存。
    /// </summary>
    MemoryInfo ReadMemory();

    /// <summary>
    /// 磁盘与挂载点。
    /// </summary>
    IReadOnlyList<DiskInfo> ReadDisks();

    /// <summary>
    /// 网卡、地址与流量。
    /// </summary>
    IReadOnlyList<NetworkInterfaceInfo> ReadNetworkInterfaces();

    /// <summary>
    /// 显卡。
    /// </summary>
    IReadOnlyList<GpuInfo> ReadGpus();

    /// <summary>
    /// 主板、整机与 BIOS。
    /// </summary>
    BoardInfo ReadBoard();
}
