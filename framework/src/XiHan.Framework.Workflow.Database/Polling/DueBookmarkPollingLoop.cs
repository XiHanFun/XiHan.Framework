// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Engine;

namespace XiHan.Framework.Workflow.Database.Polling;

/// <summary>
/// 到期书签的轮询循环（单例）
/// </summary>
/// <remarks>
/// 按 <see cref="DatabaseWorkflowOptions.PollInterval"/> 定时，每个周期先经 <see cref="PollerLeadership"/> 拿或续领导权，只有持有时才调用 <see cref="DueBookmarkPoller.RunCycleAsync"/>（覆盖平台与每个启用的租户分区，到了维护周期再按保留期清理）。应用不在服务状态（启动中、排空中）的周期不拿也不续领导权，持有就立即释放：排空开始后最多一个轮询周期，另一个在服务的实例就能接手，不用等本节点的特性停止或租约到期。领导权是全局一份：平台库里的租约行 <see cref="PollerLeadership.Resource"/>，领导节点逐个分区轮询。不用 <see cref="DueBookmarkPoller.StartAsync"/> 的内置循环，领导权判定才能插在每个周期之前
/// </remarks>
internal sealed partial class DueBookmarkPollingLoop : IDisposable
{
    /// <summary>
    /// 轮询器
    /// </summary>
    private readonly DueBookmarkPoller _poller;

    /// <summary>
    /// 领导权
    /// </summary>
    private readonly PollerLeadership _leadership;

    /// <summary>
    /// 选项
    /// </summary>
    private readonly DatabaseWorkflowOptions _options;

    /// <summary>
    /// 应用：每个周期先看它是否在服务状态
    /// </summary>
    private readonly App _app;

    /// <summary>
    /// 时钟
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 同一时刻只有一个周期在跑（循环的刻度与手动调用互斥）：领导权对象不做并发保护
    /// </summary>
    private readonly SemaphoreSlim _cycle = new(1, 1);

    /// <summary>
    /// 停止信号
    /// </summary>
    private CancellationTokenSource? _stopping;

    /// <summary>
    /// 循环任务
    /// </summary>
    private Task? _loop;

    /// <summary>
    /// 创建
    /// </summary>
    public DueBookmarkPollingLoop(DueBookmarkPoller poller, PollerLeadership leadership, DatabaseWorkflowOptions options, App app, TimeProvider clock, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _poller = poller ?? throw new ArgumentNullException(nameof(poller));
        _leadership = leadership ?? throw new ArgumentNullException(nameof(leadership));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _app = app ?? throw new ArgumentNullException(nameof(app));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = loggerFactory.CreateLogger<DueBookmarkPollingLoop>();
    }

    /// <summary>
    /// 是否在循环
    /// </summary>
    public bool IsRunning => _loop is not null;

    /// <summary>
    /// 本节点当前是否持有轮询领导权
    /// </summary>
    public bool IsLeader => _leadership.IsLeader;

    /// <summary>
    /// 启动循环
    /// </summary>
    /// <remarks>
    /// 已在跑时什么也不做
    /// </remarks>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_loop is not null)
        {
            return Task.CompletedTask;
        }

        _stopping = new CancellationTokenSource();
        _loop = LoopAsync(_stopping.Token);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 停止：不再开始新的周期，等进行中的一次结束，然后释放领导权
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
            // 循环以停止信号结束是预期的
        }
        finally
        {
            _stopping.Dispose();
            _stopping = null;
            _loop = null;
        }

        // 与手动调用的周期互斥：领导权对象不做并发保护
        await _cycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _leadership.ReleaseAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _cycle.Release();
        }
    }

    /// <summary>
    /// 执行一个周期：拿或续领导权并轮询，不在服务状态时释放领导权
    /// </summary>
    /// <remarks>
    /// 应用在服务状态时拿或续领导权，持有时轮询一次；不在服务状态时释放领导权（没持有时什么也不做）。循环的每个刻度调用它；测试推进注入的时钟后调用一次即可断言，不必等计时器。与循环互斥。返回成功恢复的书签数
    /// </remarks>
    public async ValueTask<int> RunCycleAsync(CancellationToken cancellationToken)
    {
        await _cycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 不在服务状态（启动中、排空中）的周期本来就不轮询，领导权不拿也不续，持有就立即释放：
            // 排空要等依赖本特性的特性逐个停完（在途调用、各自的停止超时），这期间续着领导权，在服务的另一个实例就一直拿不到，
            // 到期的书签停在原地；释放后它下一个周期就接手。没持有时释放什么也不做
            if (!_app.State.IsServing())
            {
                await _leadership.ReleaseAsync(cancellationToken).ConfigureAwait(false);
                return 0;
            }

            // 没拿到领导权就跳过本周期：多节点部署只有一个节点在轮询
            if (!await _leadership.TryHoldAsync(cancellationToken).ConfigureAwait(false))
            {
                return 0;
            }

            return await _poller.RunCycleAsync(_options.BatchSize, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _cycle.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stopping?.Dispose();
        _stopping = null;
        _cycle.Dispose();
    }

    /// <summary>
    /// 轮询循环
    /// </summary>
    private async Task LoopAsync(CancellationToken stopping)
    {
        using var timer = new PeriodicTimer(_options.PollInterval, _clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stopping).ConfigureAwait(false))
            {
                try
                {
                    await RunCycleAsync(stopping).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stopping.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    // 一次轮询的意外不能停掉循环
                    LogPollThrew(_logger, exception);
                }
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // 停止
        }
    }

    /// <summary>
    /// 轮询抛出异常
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Polling due workflow bookmarks threw.")]
    private static partial void LogPollThrew(ILogger logger, Exception exception);
}
