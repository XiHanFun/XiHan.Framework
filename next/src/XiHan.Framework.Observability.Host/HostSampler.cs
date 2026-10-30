// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Observability.Host;

/// <summary>
/// 使用率与速率的后台采样器。
/// </summary>
/// <remarks>
/// 按间隔采 CPU 时间、进程处理器时间与网卡累计字节数，用相邻两次之差算出使用率与速率并缓存；读取只返回缓存，不阻塞（RFC-0023 的采样）。第一次采样之后、第二次之前各值为空。
/// </remarks>
internal sealed partial class HostSampler : IAsyncDisposable
{
    /// <summary>
    /// 读取路径。
    /// </summary>
    private readonly HostPlatform _platform;

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 逻辑核心数。
    /// </summary>
    /// <remarks>
    /// 进程使用率按它折算。
    /// </remarks>
    private readonly int _logicalCores;

    /// <summary>
    /// 采样与停止的互斥。
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 上一次的原始采样。
    /// </summary>
    private Snapshot? _previous;

    /// <summary>
    /// 最近一次算出的值。
    /// </summary>
    private volatile Rates _rates = Rates.Empty;

    /// <summary>
    /// 停止信号。
    /// </summary>
    private CancellationTokenSource? _stopping;

    /// <summary>
    /// 后台循环。
    /// </summary>
    private Task? _loop;

    /// <summary>
    /// 创建。
    /// </summary>
    public HostSampler(HostPlatform platform, TimeSpan interval, TimeProvider clock, ILogger logger, int logicalCores)
    {
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(logicalCores, 1);
        _platform = platform;
        Interval = interval;
        _clock = clock;
        _logger = logger;
        _logicalCores = logicalCores;
    }

    /// <summary>
    /// 采样间隔。
    /// </summary>
    public TimeSpan Interval { get; }

    /// <summary>
    /// 系统 CPU 使用率（0–100）。
    /// </summary>
    public double? CpuUsagePercent => _rates.Cpu;

    /// <summary>
    /// 进程 CPU 使用率（0–100）。
    /// </summary>
    public double? ProcessCpuUsagePercent => _rates.Process;

    /// <summary>
    /// 一块网卡的收发速率（字节/秒）。
    /// </summary>
    /// <remarks>
    /// 没有采到这块网卡时两项都为空。
    /// </remarks>
    public (double? Receive, double? Send) NetworkRate(string id)
        => _rates.Network.TryGetValue(id, out var rate) ? rate : (null, null);

    /// <summary>
    /// 采一次样，与上一次求差。
    /// </summary>
    public void Sample()
    {
        lock (_gate)
        {
            var current = new Snapshot(_clock.GetTimestamp(), _platform.ReadCpuTimes(), _platform.ReadProcessCpuTime(), _platform.ReadNetworkCounters());
            if (_previous is { } previous)
            {
                _rates = Compute(previous, current);
            }

            _previous = current;
        }
    }

    /// <summary>
    /// 同步采第一次样，再起后台循环。
    /// </summary>
    /// <remarks>
    /// 重复调用不再起第二个循环。
    /// </remarks>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_loop is not null)
        {
            return Task.CompletedTask;
        }

        Sample();
        _stopping = new CancellationTokenSource();
        var stopping = _stopping.Token;
        // 计时器在这里同步创建：循环任务还没被调度时时钟已经推进（假时钟）也不会丢掉第一个周期
        var timer = new PeriodicTimer(Interval, _clock);
        _loop = Task.Run(() => LoopAsync(timer, stopping), CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 停止后台循环。
    /// </summary>
    /// <remarks>
    /// 最近算出的值保留。
    /// </remarks>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_loop is null || _stopping is null)
        {
            return;
        }

        await _stopping.CancelAsync().ConfigureAwait(false);
        try
        {
            await _loop.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 循环按取消退出，或调用方等不及：两种情况都不再采样
        }

        _stopping.Dispose();
        _stopping = null;
        _loop = null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// 两次采样之差。
    /// </summary>
    /// <remarks>
    /// 计数变小或总增量为零时该项为空；百分比限制在 0–100。
    /// </remarks>
    private Rates Compute(Snapshot previous, Snapshot current)
    {
        var elapsed = _clock.GetElapsedTime(previous.Timestamp, current.Timestamp);
        if (elapsed <= TimeSpan.Zero)
        {
            return Rates.Empty;
        }

        double? cpu = null;
        if (previous.Cpu is { } before && current.Cpu is { } after && after.Total > before.Total && after.Idle >= before.Idle)
        {
            var total = after.Total - before.Total;
            var idle = Math.Min(after.Idle - before.Idle, total);
            cpu = Percent(1d - ((double)idle / total));
        }

        var processed = current.ProcessCpu - previous.ProcessCpu;
        double? process = processed >= TimeSpan.Zero ? Percent(processed / (elapsed * _logicalCores)) : null;

        var network = new Dictionary<string, (double?, double?)>(StringComparer.Ordinal);
        var earlier = previous.Network.ToDictionary(static c => c.Id, StringComparer.Ordinal);
        foreach (var counters in current.Network)
        {
            if (earlier.TryGetValue(counters.Id, out var last))
            {
                network[counters.Id] = (PerSecond(last.Received, counters.Received, elapsed), PerSecond(last.Sent, counters.Sent, elapsed));
            }
        }

        return new Rates(cpu, process, network);
    }

    /// <summary>
    /// 比例换成 0–100 的百分比。
    /// </summary>
    private static double Percent(double ratio) => Math.Clamp(ratio * 100d, 0d, 100d);

    /// <summary>
    /// 累计值的每秒增量。
    /// </summary>
    /// <remarks>
    /// 变小或读不到为空。
    /// </remarks>
    private static double? PerSecond(long? before, long? after, TimeSpan elapsed)
        => before is { } b && after is { } a && a >= b ? (a - b) / elapsed.TotalSeconds : null;

    /// <summary>
    /// 按间隔采样，直到停止。
    /// </summary>
    /// <remarks>
    /// 计时器由本方法释放；一次采样失败记警告后继续。
    /// </remarks>
    private async Task LoopAsync(PeriodicTimer timer, CancellationToken stopping)
    {
        using (timer)
        {
            try
            {
                while (await timer.WaitForNextTickAsync(stopping).ConfigureAwait(false))
                {
                    try
                    {
                        Sample();
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        LogSampleFailed(_logger, exception);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 停止
            }
        }
    }

    /// <summary>
    /// 一次采样失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Host sampling failed; the previous usage values are kept")]
    private static partial void LogSampleFailed(ILogger logger, Exception exception);

    /// <summary>
    /// 一次原始采样。
    /// </summary>
    /// <param name="Timestamp">时钟的时间戳。</param>
    /// <param name="Cpu">系统 CPU 时间；读不到为空。</param>
    /// <param name="ProcessCpu">进程处理器时间。</param>
    /// <param name="Network">各网卡的累计字节数。</param>
    private sealed record Snapshot(long Timestamp, CpuTimes? Cpu, TimeSpan ProcessCpu, IReadOnlyList<NetworkCounters> Network);

    /// <summary>
    /// 一组算出的值。
    /// </summary>
    /// <param name="Cpu">系统 CPU 使用率。</param>
    /// <param name="Process">进程 CPU 使用率。</param>
    /// <param name="Network">各网卡的收发速率。</param>
    private sealed record Rates(double? Cpu, double? Process, IReadOnlyDictionary<string, (double? Receive, double? Send)> Network)
    {
        /// <summary>
        /// 还没算出任何值。
        /// </summary>
        public static Rates Empty { get; } = new(null, null, new Dictionary<string, (double?, double?)>(StringComparer.Ordinal));
    }
}
