// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Observability.Host.Tests;

/// <summary>
/// 计数可控的读取路径替身。
/// </summary>
/// <remarks>
/// 采样器只读 CPU 时间、进程处理器时间与网卡计数，用例逐次设定它们。
/// </remarks>
internal sealed class FakeHostPlatform : HostPlatform
{
    /// <summary>
    /// 下一次读到的系统 CPU 时间。
    /// </summary>
    public CpuTimes? Cpu { get; set; }

    /// <summary>
    /// 下一次读到的进程处理器时间。
    /// </summary>
    public TimeSpan ProcessCpu { get; set; }

    /// <summary>
    /// 下一次读到的网卡计数。
    /// </summary>
    public IReadOnlyList<NetworkCounters> Network { get; set; } = [];

    /// <inheritdoc />
    public override CpuTimes? ReadCpuTimes() => Cpu;

    /// <inheritdoc />
    public override TimeSpan ReadProcessCpuTime() => ProcessCpu;

    /// <inheritdoc />
    public override IReadOnlyList<NetworkCounters> ReadNetworkCounters() => Network;
}
