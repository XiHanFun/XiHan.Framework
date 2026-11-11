// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using System.Runtime.InteropServices;
using XiHan.Framework.Observability.Abstractions;

namespace XiHan.Framework.Observability.Host;

/// <summary>
/// <see cref="IHostMonitor"/> 的实现
/// </summary>
/// <remarks>
/// 处理器的静态部分、GPU 与主板首次读取后缓存；使用率与速率取采样器的最近值
/// </remarks>
internal sealed class HostMonitor : IHostMonitor
{
    /// <summary>
    /// 读取路径
    /// </summary>
    private readonly HostPlatform _platform;

    /// <summary>
    /// 采样器
    /// </summary>
    private readonly HostSampler _sampler;

    /// <summary>
    /// 时钟
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 处理器的静态部分
    /// </summary>
    private readonly Lazy<ProcessorInfo> _processor;

    /// <summary>
    /// 显卡
    /// </summary>
    private readonly Lazy<IReadOnlyList<GpuInfo>> _gpus;

    /// <summary>
    /// 主板
    /// </summary>
    private readonly Lazy<BoardInfo> _board;

    /// <summary>
    /// 创建
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
    public ValueTask<RuntimeInfo> ReadRuntimeAsync(CancellationToken cancellationToken)
    {
        // 运行时信息都来自本进程与 BCL，不会阻塞，不必换线程
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ReadRuntime());
    }

    /// <summary>
    /// 同步读运行时信息
    /// </summary>
    private RuntimeInfo ReadRuntime()
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
    public ValueTask<ProcessorInfo> ReadProcessorAsync(CancellationToken cancellationToken)
        => _processor.IsValueCreated
            ? ValueTask.FromResult(_processor.Value with { UsagePercent = _sampler.CpuUsagePercent })
            : Offload(() => _processor.Value with { UsagePercent = _sampler.CpuUsagePercent }, cancellationToken);

    /// <inheritdoc />
    public ValueTask<MemoryInfo> ReadMemoryAsync(CancellationToken cancellationToken) => Offload(_platform.ReadMemory, cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<DiskInfo>> ReadDisksAsync(CancellationToken cancellationToken) => Offload(_platform.ReadDisks, cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<NetworkInterfaceInfo>> ReadNetworkInterfacesAsync(CancellationToken cancellationToken)
        => Offload<IReadOnlyList<NetworkInterfaceInfo>>(() => [.. _platform.ReadNetworkInterfaces().Select(nic =>
        {
            var (receive, send) = _sampler.NetworkRate(nic.Id);
            return nic with { ReceiveBytesPerSecond = receive, SendBytesPerSecond = send };
        })], cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<GpuInfo>> ReadGpusAsync(CancellationToken cancellationToken)
        => _gpus.IsValueCreated ? ValueTask.FromResult(_gpus.Value) : Offload(() => _gpus.Value, cancellationToken);

    /// <inheritdoc />
    public ValueTask<BoardInfo> ReadBoardAsync(CancellationToken cancellationToken)
        => _board.IsValueCreated ? ValueTask.FromResult(_board.Value) : Offload(() => _board.Value, cancellationToken);

    /// <summary>
    /// 在线程池上执行可能阻塞的读取
    /// </summary>
    /// <remarks>
    /// 磁盘（挂起的网络盘）、网卡与首次的硬件探测可能长时间阻塞：放到线程池，调用方取消时立即不再等待；已经开始的系统调用无法中断，会在后台自行结束
    /// </remarks>
    private static ValueTask<T> Offload<T>(Func<T> read, CancellationToken cancellationToken)
        => new(Task.Run(read, cancellationToken).WaitAsync(cancellationToken));
}
