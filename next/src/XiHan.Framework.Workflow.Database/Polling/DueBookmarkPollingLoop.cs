// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Engine;

namespace XiHan.Framework.Workflow.Database.Polling;

/// <summary>
/// 到期书签的轮询循环（单例）。
/// </summary>
/// <remarks>
/// 按 <see cref="DatabaseWorkflowOptions.PollInterval"/> 定时，每个周期先经 <see cref="PollerLeadership"/> 拿或续领导权，只有持有时才调用 <see cref="DueBookmarkPoller.PollOnceAsync"/>。不用 <see cref="DueBookmarkPoller.StartAsync"/> 的内置循环，领导权判定才能插在每个周期之前。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class DueBookmarkPollingLoop : IDisposable
{
    /// <summary>
    /// 轮询器。
    /// </summary>
    private readonly DueBookmarkPoller _poller;

    /// <summary>
    /// 领导权。
    /// </summary>
    private readonly PollerLeadership _leadership;

    /// <summary>
    /// 选项。
    /// </summary>
    private readonly DatabaseWorkflowOptions _options;

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 停止信号。
    /// </summary>
    private CancellationTokenSource? _stopping;

    /// <summary>
    /// 循环任务。
    /// </summary>
    private Task? _loop;

    /// <summary>
    /// 创建。
    /// </summary>
    public DueBookmarkPollingLoop(DueBookmarkPoller poller, PollerLeadership leadership, DatabaseWorkflowOptions options, TimeProvider clock, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _poller = poller ?? throw new ArgumentNullException(nameof(poller));
        _leadership = leadership ?? throw new ArgumentNullException(nameof(leadership));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = loggerFactory.CreateLogger<DueBookmarkPollingLoop>();
    }

    /// <summary>
    /// 是否在循环。
    /// </summary>
    public bool IsRunning => _loop is not null;

    /// <summary>
    /// 本节点当前是否持有轮询领导权。
    /// </summary>
    public bool IsLeader => _leadership.IsLeader;

    /// <summary>
    /// 启动循环。
    /// </summary>
    /// <remarks>
    /// 已在跑时什么也不做。
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
    /// 停止：不再开始新的周期，等进行中的一次结束，然后释放领导权。
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

        await _leadership.ReleaseAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stopping?.Dispose();
        _stopping = null;
    }

    /// <summary>
    /// 轮询循环。
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
                    // 没拿到领导权就跳过本周期：多节点部署只有一个节点在轮询
                    if (!await _leadership.TryHoldAsync(stopping).ConfigureAwait(false))
                    {
                        continue;
                    }

                    await _poller.PollOnceAsync(_options.BatchSize, stopping).ConfigureAwait(false);
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
    /// 轮询抛出异常。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Polling due workflow bookmarks threw.")]
    private static partial void LogPollThrew(ILogger logger, Exception exception);
}
