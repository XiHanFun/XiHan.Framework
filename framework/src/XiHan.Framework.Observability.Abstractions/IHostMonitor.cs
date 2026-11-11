// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Observability.Abstractions;

/// <summary>
/// 进程与所在主机的运行信息
/// </summary>
/// <remarks>
/// 读取不抛出、不启动外部命令（RFC-0023）；可能阻塞的系统调用（挂起的网络盘、首次的硬件探测）不占调用方的线程，调用方取消即不再等待（ADR-0161）。读不到的项为 <see langword="null"/>，不以 0 或空串顶替；使用率与速率取后台采样器最近一次的值，启动后第一个采样间隔内为空。CPU 型号与核心、GPU、主板首次读取后缓存
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IHostMonitor
{
    /// <summary>
    /// 操作系统、.NET、进程与运行时长
    /// </summary>
    ValueTask<RuntimeInfo> ReadRuntimeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 处理器与系统 CPU 使用率
    /// </summary>
    ValueTask<ProcessorInfo> ReadProcessorAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 物理内存
    /// </summary>
    ValueTask<MemoryInfo> ReadMemoryAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 磁盘与挂载点
    /// </summary>
    ValueTask<IReadOnlyList<DiskInfo>> ReadDisksAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 网卡、地址与流量
    /// </summary>
    ValueTask<IReadOnlyList<NetworkInterfaceInfo>> ReadNetworkInterfacesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 显卡
    /// </summary>
    ValueTask<IReadOnlyList<GpuInfo>> ReadGpusAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 主板、整机与 BIOS
    /// </summary>
    ValueTask<BoardInfo> ReadBoardAsync(CancellationToken cancellationToken);
}
