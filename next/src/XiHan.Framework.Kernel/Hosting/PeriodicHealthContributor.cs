// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 周期探测、缓存结果的健康贡献者：Provider 把连通性探针（PING、HEAD、打开连接）交给它，由它在后台按 <see cref="Interval"/> 重探并缓存最近一次结果，
/// <see cref="Check"/> 只返回缓存、立即返回且不抛出。<see cref="StartAsync"/> 先同步探一次（超时取 <see cref="Interval"/>）再起循环，
/// 探针抛出的异常记为 <see cref="KernelErrors.HealthProbeFailed"/>；探针返回的失败结果原样作为条目错误。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class PeriodicHealthContributor : IHealthContributor, IAsyncDisposable
{
    /// <summary>
    /// 探针。
    /// </summary>
    private readonly Func<CancellationToken, ValueTask<Result>> _probe;

    /// <summary>
    /// 时钟；循环的计时器与每次探测的超时都按它计时。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 最近一次结果；探测完成后整体替换，<see cref="Check"/> 读它不加锁。
    /// </summary>
    private volatile ContributorStatus _status;

    /// <summary>
    /// 停止信号；非空表示循环在跑。
    /// </summary>
    private CancellationTokenSource? _stopping;

    /// <summary>
    /// 后台循环。
    /// </summary>
    private Task? _loop;

    /// <summary>
    /// 创建。
    /// </summary>
    /// <param name="name">条目名，即清单 <c>contributes.health</c> 里声明的名字。</param>
    /// <param name="probe">探针：成功表示可达，失败结果原样作为条目错误，抛出的异常记为 <see cref="KernelErrors.HealthProbeFailed"/>。</param>
    /// <param name="interval">重探间隔，也是单次探测的超时；必须为正。</param>
    /// <param name="clock">时钟。</param>
    /// <param name="loggerFactory">日志工厂。</param>
    public PeriodicHealthContributor(string name, Func<CancellationToken, ValueTask<Result>> probe, TimeSpan interval, TimeProvider clock, ILoggerFactory loggerFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "The probe interval must be positive.");
        }

        Name = name;
        Interval = interval;
        _probe = probe;
        _clock = clock;
        _logger = loggerFactory.CreateLogger<PeriodicHealthContributor>();
        _status = new ContributorStatus(name, LifecycleState.Started);
    }

    /// <summary>
    /// 条目名。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 重探间隔，也是单次探测的超时。
    /// </summary>
    public TimeSpan Interval { get; }

    /// <summary>
    /// 最近一次探测完成的时刻；还没探过为 <see langword="null"/>。
    /// </summary>
    public DateTimeOffset? LastProbedAt { get; private set; }

    /// <summary>
    /// 同步探一次（受 <see cref="Interval"/> 上限约束的超时），然后起后台循环按 <see cref="Interval"/> 重探。重复调用不再起第二个循环。
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_loop is not null)
        {
            return;
        }

        // 首探同步完成：宿主 Ready 时条目已是真实状态，不会先报一轮 Started
        await ProbeAsync(cancellationToken).ConfigureAwait(false);
        _stopping = new CancellationTokenSource();
        var stopping = _stopping.Token;
        // 计时器在这里同步创建而不是在循环里：循环任务还没被调度时时钟已经推进（假时钟）也不会丢掉第一个周期
        var timer = new PeriodicTimer(Interval, _clock);
        _loop = Task.Run(() => LoopAsync(timer, stopping), CancellationToken.None);
    }

    /// <summary>
    /// 停止后台循环；缓存的最近结果保留。
    /// </summary>
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
            // 循环按取消退出，或调用方等不及：两种情况都不再探测
        }

        _stopping.Dispose();
        _stopping = null;
        _loop = null;
    }

    /// <summary>
    /// 立即探测一次并更新缓存。探针抛出的异常（含超时）不传播，记为 <see cref="LifecycleState.Degraded"/>；只有 <paramref name="cancellationToken"/> 被取消时才抛出。
    /// </summary>
    public async ValueTask ProbeAsync(CancellationToken cancellationToken)
    {
        // 每次探测各有超时：探针卡住不能拖住循环，也不能让首探拖住宿主启动；超时与间隔取同一个值
        using var timeout = new CancellationTokenSource(Interval, _clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        Result outcome;
        try
        {
            outcome = await _probe(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 停止或调用方取消：不改缓存，按异步约定抛出
            throw;
        }
        catch (Exception exception)
        {
            // 超时与探针异常都是"探不到"：条目 Degraded，原因是异常消息；应用仍在服务
            outcome = Error.Unavailable(KernelErrors.HealthProbeFailed, KernelMessages.HealthProbeFailed, MessageArguments.Of(("name", Name), ("reason", exception.Message)));
        }

        Apply(outcome);
    }

    /// <inheritdoc />
    public IReadOnlyList<ContributorStatus> Check() => [_status];

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// 把探测结果写成条目状态：成功 Ready、失败 Degraded；状态或错误码变化时记日志。
    /// </summary>
    private void Apply(Result outcome)
    {
        var previous = _status;
        var current = outcome.IsSuccess
            ? new ContributorStatus(Name, LifecycleState.Ready)
            : new ContributorStatus(Name, LifecycleState.Degraded, outcome.Error);
        _status = current;
        LastProbedAt = _clock.GetUtcNow();

        // 只在状态或错误码变化时记日志，稳定的失败不刷屏
        if (previous.State == current.State && string.Equals(previous.Error?.Code, current.Error?.Code, StringComparison.Ordinal))
        {
            return;
        }

        if (current.State == LifecycleState.Degraded)
        {
            LogDegraded(_logger, Name, previous.State, current.Error!.Code, current.Error.Message);
        }
        else
        {
            LogRecovered(_logger, Name, previous.State);
        }
    }

    /// <summary>
    /// 按间隔重探，直到停止；计时器由本方法释放。
    /// </summary>
    private async Task LoopAsync(PeriodicTimer timer, CancellationToken stopping)
    {
        using (timer)
        {
            try
            {
                while (await timer.WaitForNextTickAsync(stopping).ConfigureAwait(false))
                {
                    await ProbeAsync(stopping).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // 停止
            }
        }
    }

    /// <summary>
    /// 条目进入 Degraded。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Health item '{Name}' changed from {Previous} to Degraded: {Code} {Message}")]
    private static partial void LogDegraded(ILogger logger, string name, LifecycleState previous, string code, string message);

    /// <summary>
    /// 条目回到 Ready。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Health item '{Name}' changed from {Previous} to Ready.")]
    private static partial void LogRecovered(ILogger logger, string name, LifecycleState previous);
}
