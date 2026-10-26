// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Storage;

namespace XiHan.Framework.Workflow.Abstractions.Engine;

/// <summary>
/// 到期书签轮询：每个周期取一批到期书签逐条经引擎恢复；实例不在运行中的把到期时间后推 <see cref="WorkflowOptions.NotResumableBackoff"/>。由 Provider 的特性在应用进入服务状态后启动，多实例部署时由 Provider 保证只有一个节点在轮询。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class DueBookmarkPoller : IDisposable
{
    /// <summary>
    /// 根服务提供者；每次轮询开新作用域取引擎。
    /// </summary>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 书签存储。
    /// </summary>
    private readonly IWorkflowBookmarkStore _bookmarks;

    /// <summary>
    /// 选项。
    /// </summary>
    private readonly WorkflowOptions _options;

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 同一时刻只有一次轮询在跑（定时触发与手动触发互斥）。
    /// </summary>
    private readonly SemaphoreSlim _polling = new(1, 1);

    /// <summary>
    /// 停止信号。
    /// </summary>
    private CancellationTokenSource? _stopping;

    /// <summary>
    /// 轮询循环。
    /// </summary>
    private Task? _loop;

    /// <summary>
    /// 上次维护时刻。
    /// </summary>
    private DateTimeOffset _lastHousekeeping = DateTimeOffset.MinValue;

    /// <summary>
    /// 创建。
    /// </summary>
    public DueBookmarkPoller(IServiceProvider services, IWorkflowBookmarkStore bookmarks, WorkflowOptions options, TimeProvider clock, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _bookmarks = bookmarks ?? throw new ArgumentNullException(nameof(bookmarks));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = loggerFactory.CreateLogger<DueBookmarkPoller>();
    }

    /// <summary>
    /// 是否在轮询。
    /// </summary>
    public bool IsRunning => _loop is not null;

    /// <summary>
    /// 启动循环：每 <paramref name="interval"/> 轮询一次，每次最多 <paramref name="batchSize"/> 条。
    /// </summary>
    public Task StartAsync(TimeSpan interval, int batchSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        if (_loop is not null)
        {
            return Task.CompletedTask;
        }

        _stopping = new CancellationTokenSource();
        _loop = LoopAsync(interval, batchSize, _stopping.Token);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 停止：不再开始新的轮询，等进行中的一次结束。
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
    }

    /// <summary>
    /// 轮询一次；返回成功恢复的书签数。
    /// </summary>
    public async ValueTask<int> PollOnceAsync(int batchSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        await _polling.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var due = await _bookmarks.DueAsync(_clock.GetUtcNow(), batchSize, cancellationToken).ConfigureAwait(false);
            if (!due.TryGetValue(out var bookmarks))
            {
                LogPollFailed(_logger, due.Error!.Code);
                return 0;
            }

            if (bookmarks.Count == 0)
            {
                return 0;
            }

            var resumed = 0;
            var deferred = new List<WorkflowBookmark>();
            await using var scope = _services.CreateAsyncScope();
            var engine = scope.ServiceProvider.GetRequiredService<IWorkflowEngine>();
            foreach (var bookmark in bookmarks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await engine.ResumeAsync(bookmark.Id, null, null, cancellationToken).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    resumed++;
                    continue;
                }

                switch (result.Error!.Code)
                {
                    case WorkflowErrors.BookmarkNotResumable:
                        // 实例挂起或已结束：把到期时间后推，免得每个周期都碰它
                        bookmark.DueAt = _clock.GetUtcNow() + _options.NotResumableBackoff;
                        deferred.Add(bookmark);
                        break;
                    case WorkflowErrors.BookmarkNotFound:
                    case WorkflowErrors.LockTimeout:
                        break;
                    default:
                        LogResumeFailed(_logger, bookmark.Id, result.Error.Code);
                        break;
                }
            }

            if (deferred.Count > 0)
            {
                var saved = await _bookmarks.SaveAsync(deferred, cancellationToken).ConfigureAwait(false);
                if (saved.IsFailure)
                {
                    LogPollFailed(_logger, saved.Error!.Code);
                }
            }

            return resumed;
        }
        finally
        {
            _polling.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stopping?.Dispose();
        _polling.Dispose();
    }

    /// <summary>
    /// 按保留期清理已结束的实例：到了维护周期且配置了保留期时执行，分批直到没有可删的。
    /// </summary>
    public async ValueTask<int> HousekeepAsync(CancellationToken cancellationToken)
    {
        if (_options.Retention <= TimeSpan.Zero)
        {
            return 0;
        }

        var purged = 0;
        await using var scope = _services.CreateAsyncScope();
        var engine = scope.ServiceProvider.GetRequiredService<IWorkflowEngine>();
        while (true)
        {
            var result = await engine.PurgeAsync(new WorkflowPurgeRequest { EndedBefore = _clock.GetUtcNow() - _options.Retention }, cancellationToken).ConfigureAwait(false);
            if (!result.TryGetValue(out var count))
            {
                LogPollFailed(_logger, result.Error!.Code);
                return purged;
            }

            purged += count;
            if (count < WorkflowInstanceQuery.MaxLimit)
            {
                return purged;
            }
        }
    }

    /// <summary>
    /// 到了维护周期才清理。
    /// </summary>
    private async ValueTask HousekeepIfDueAsync(CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        if (now - _lastHousekeeping < _options.HousekeepingInterval)
        {
            return;
        }

        _lastHousekeeping = now;
        await HousekeepAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 轮询循环。
    /// </summary>
    private async Task LoopAsync(TimeSpan interval, int batchSize, CancellationToken stopping)
    {
        using var timer = new PeriodicTimer(interval, _clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stopping).ConfigureAwait(false))
            {
                try
                {
                    await PollOnceAsync(batchSize, stopping).ConfigureAwait(false);
                    await HousekeepIfDueAsync(stopping).ConfigureAwait(false);
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
    /// 轮询读取或写回失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Polling due workflow bookmarks failed with '{ErrorCode}'.")]
    private static partial void LogPollFailed(ILogger logger, string errorCode);

    /// <summary>
    /// 恢复失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Resuming due workflow bookmark '{BookmarkId}' failed with '{ErrorCode}'.")]
    private static partial void LogResumeFailed(ILogger logger, string bookmarkId, string errorCode);

    /// <summary>
    /// 轮询抛出异常。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Polling due workflow bookmarks threw.")]
    private static partial void LogPollThrew(ILogger logger, Exception exception);
}
