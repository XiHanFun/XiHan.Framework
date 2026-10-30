// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using System.Runtime.InteropServices;
using XiHan.Framework.Observability.Abstractions;

namespace XiHan.Framework.Observability.Host;

/// <summary>
/// <see cref="IHostMonitor"/> 的实现。
/// </summary>
/// <remarks>
/// 处理器的静态部分、GPU 与主板首次读取后缓存；使用率与速率取采样器的最近值。
/// </remarks>
internal sealed class HostMonitor : IHostMonitor
{
    /// <summary>
    /// 读取路径。
    /// </summary>
    private readonly HostPlatform _platform;

    /// <summary>
    /// 采样器。
    /// </summary>
    private readonly HostSampler _sampler;

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 处理器的静态部分。
    /// </summary>
    private readonly Lazy<ProcessorInfo> _processor;

    /// <summary>
    /// 显卡。
    /// </summary>
    private readonly Lazy<IReadOnlyList<GpuInfo>> _gpus;

    /// <summary>
    /// 主板。
    /// </summary>
    private readonly Lazy<BoardInfo> _board;

    /// <summary>
    /// 创建。
    /// </summary>
    public HostMonitor(HostPlatform platform, HostSampler sampler, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(sampler);
        ArgumentNullException.ThrowIfNull(clock);
        _platform = platform;
        _sampler = sampler;
        _clock = clock;
        _processor = new Lazy<ProcessorInfo>(platform.ReadProcessor);
        _gpus = new Lazy<IReadOnlyList<GpuInfo>>(platform.ReadGpus);
        _board = new Lazy<BoardInfo>(platform.ReadBoard);
    }

    /// <inheritdoc />
    public RuntimeInfo ReadRuntime()
    {
        using var process = Process.GetCurrentProcess();
        var now = _clock.GetUtcNow();
        var started = Readings.Optional(() => new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero));
        var uptime = _platform.ReadSystemUptime();
        return new RuntimeInfo
        {
            OsDescription = RuntimeInformation.OSDescription.Trim(),
            OsPlatform = HostPlatform.PlatformName(),
            OsVersion = Environment.OSVersion.VersionString,
            OsArchitecture = RuntimeInformation.OSArchitecture,
            Framework = RuntimeInformation.FrameworkDescription,
            RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier,
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture,
            MachineName = Environment.MachineName,
            UserName = Readings.Text(static () => Environment.UserName),
            Interactive = Environment.UserInteractive,
            ProcessId = Environment.ProcessId,
            ProcessName = process.ProcessName,
            ProcessStartedAt = started,
            ProcessUptime = now - started,
            SystemStartedAt = now - uptime,
            SystemUptime = uptime,
            WorkingSetBytes = Environment.WorkingSet,
            PrivateBytes = Readings.Optional(() => process.PrivateMemorySize64),
            ManagedHeapBytes = GC.GetTotalMemory(forceFullCollection: false),
            ThreadCount = Readings.Optional(() => process.Threads.Count),
            HandleCount = Readings.Optional(() => process.HandleCount),
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),
            ProcessCpuUsagePercent = _sampler.ProcessCpuUsagePercent,
        };
    }

    /// <inheritdoc />
    public ProcessorInfo ReadProcessor() => _processor.Value with { UsagePercent = _sampler.CpuUsagePercent };

    /// <inheritdoc />
    public MemoryInfo ReadMemory() => _platform.ReadMemory();

    /// <inheritdoc />
    public IReadOnlyList<DiskInfo> ReadDisks() => _platform.ReadDisks();

    /// <inheritdoc />
    public IReadOnlyList<NetworkInterfaceInfo> ReadNetworkInterfaces()
        => [.. _platform.ReadNetworkInterfaces().Select(nic =>
        {
            var (receive, send) = _sampler.NetworkRate(nic.Id);
            return nic with { ReceiveBytesPerSecond = receive, SendBytesPerSecond = send };
        })];

    /// <inheritdoc />
    public IReadOnlyList<GpuInfo> ReadGpus() => _gpus.Value;

    /// <inheritdoc />
    public BoardInfo ReadBoard() => _board.Value;
}
