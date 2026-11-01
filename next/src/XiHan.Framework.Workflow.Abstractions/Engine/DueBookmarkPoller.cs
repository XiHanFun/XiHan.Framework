// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Storage;

namespace XiHan.Framework.Workflow.Abstractions.Engine;

/// <summary>
/// 到期书签轮询。
/// </summary>
/// <remarks>
/// 覆盖平台与每个租户的分区：每个周期从租户目录（<see cref="ITenantStore"/>，装配了多租户时）取启用的租户，逐个分区取到期书签，合并后按到期时间取一批，逐条在以书签租户开的调用里经引擎恢复——每次恢复一个事务，Tenant 阶段为它绑定租户的分片；实例不在运行中的把到期时间后推 <see cref="WorkflowOptions.NotResumableBackoff"/>。停用的租户不轮询，它的到期书签等重新启用后再接续；新租户在它进入租户目录后的下一个周期纳入。由 Provider 的特性在应用进入服务状态后启动，多实例部署时由 Provider 保证只有一个节点在轮询。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class DueBookmarkPoller : IDisposable
{
    /// <summary>
    /// 根服务提供者。
    /// </summary>
    /// <remarks>
    /// 每次扫描、恢复与清理都开新作用域、新调用；租户目录从这里取（没有装配多租户时为空）。
    /// </remarks>
    private readonly IServiceProvider _services;

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
    public DueBookmarkPoller(IServiceProvider services, WorkflowOptions options, TimeProvider clock, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = loggerFactory.CreateLogger<DueBookmarkPoller>();
    }

    /// <summary>
    /// 是否在轮询。
    /// </summary>
    public bool IsRunning => _loop is not null;

    /// <summary>
    /// 启动循环：每 <paramref name="interval"/> 执行一个周期（<see cref="RunCycleAsync"/>），每次最多恢复 <paramref name="batchSize"/> 条。
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
    /// 一个周期：轮询一次，到了维护周期再按保留期清理。
    /// </summary>
    /// <remarks>
    /// Provider 自己驱动循环时（例如先判定轮询领导权）每个周期调用它。返回成功恢复的书签数。
    /// </remarks>
    public async ValueTask<int> RunCycleAsync(int batchSize, CancellationToken cancellationToken)
    {
        var resumed = await PollOnceAsync(batchSize, cancellationToken).ConfigureAwait(false);
        await HousekeepIfDueAsync(cancellationToken).ConfigureAwait(false);
        return resumed;
    }

    /// <summary>
    /// 轮询一次。
    /// </summary>
    /// <remarks>
    /// 逐个分区取最多 <paramref name="batchSize"/> 条到期书签，合并后按到期时间取前 <paramref name="batchSize"/> 条逐条恢复。返回成功恢复的书签数。
    /// </remarks>
    public async ValueTask<int> PollOnceAsync(int batchSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        await _polling.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = _clock.GetUtcNow();
            var candidates = new List<WorkflowBookmark>();
            foreach (var tenantId in await PartitionsAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                // 每个分区一次只读的扫描调用：租户的书签在它自己的库里，Tenant 阶段为这次调用绑定分片
                IReadOnlyList<WorkflowBookmark> due = [];
                var scanned = await InvokeAsync(tenantId, "workflow.poll.scan", async services =>
                {
                    var found = await services.GetRequiredService<IWorkflowBookmarkStore>().DueAsync(tenantId, now, batchSize, cancellationToken).ConfigureAwait(false);
                    if (!found.TryGetValue(out var list))
                    {
                        return found.Error!;
                    }

                    due = list;
                    return Result.Success();
                }, cancellationToken).ConfigureAwait(false);
                if (scanned.IsFailure)
                {
                    // 一个分区不可达（库连不上、租户校验失败）不能挡住别的分区
                    LogPartitionFailed(_logger, tenantId ?? string.Empty, scanned.Error!.Code);
                    continue;
                }

                candidates.AddRange(due);
            }

            var resumed = 0;
            foreach (var bookmark in candidates.OrderBy(static bookmark => bookmark.DueAt).Take(batchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await ResumeAsync(bookmark, cancellationToken).ConfigureAwait(false))
                {
                    resumed++;
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
    /// 按保留期清理已结束的实例。
    /// </summary>
    /// <remarks>
    /// 配置了保留期时逐个分区执行，每个分区分批直到没有可删的。每一批是以该分区租户开的一次调用（一个事务）。返回删掉的实例数。
    /// </remarks>
    public async ValueTask<int> HousekeepAsync(CancellationToken cancellationToken)
    {
        if (_options.Retention <= TimeSpan.Zero)
        {
            return 0;
        }

        var purged = 0;
        foreach (var tenantId in await PartitionsAsync(cancellationToken).ConfigureAwait(false))
        {
            while (true)
            {
                var count = 0;
                var result = await InvokeAsync(tenantId, "workflow.poll.housekeep", async services =>
                {
                    var deleted = await services.GetRequiredService<IWorkflowEngine>().PurgeAsync(new WorkflowPurgeRequest { EndedBefore = _clock.GetUtcNow() - _options.Retention }, cancellationToken).ConfigureAwait(false);
                    if (!deleted.TryGetValue(out count))
                    {
                        return deleted.Error!;
                    }

                    return Result.Success();
                }, cancellationToken).ConfigureAwait(false);
                if (result.IsFailure)
                {
                    LogPollFailed(_logger, result.Error!.Code);
                    break;
                }

                purged += count;
                if (count < WorkflowInstanceQuery.MaxLimit)
                {
                    break;
                }
            }
        }

        return purged;
    }

    /// <summary>
    /// 要覆盖的分区：平台（空）加上租户目录里启用的租户。
    /// </summary>
    /// <remarks>
    /// 没有装配多租户时只有平台。租户目录读取失败记日志、本周期只覆盖平台，下个周期再试。
    /// </remarks>
    private async ValueTask<IReadOnlyList<string?>> PartitionsAsync(CancellationToken cancellationToken)
    {
        var partitions = new List<string?> { null };
        if (_services.GetService<ITenantStore>() is not { } tenants)
        {
            return partitions;
        }

        try
        {
            foreach (var tenant in await tenants.ListAsync(cancellationToken).ConfigureAwait(false))
            {
                // 停用的租户不轮询：恢复会在 Tenant 阶段被拒绝，它的到期书签等租户重新启用后再接续
                if (tenant.IsEnabled)
                {
                    partitions.Add(tenant.Id);
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogTenantsFailed(_logger, exception);
        }

        return partitions;
    }

    /// <summary>
    /// 在以书签租户开的一次调用里恢复它。
    /// </summary>
    /// <remarks>
    /// 实例不在运行中时在同一调用里（仍持有实例锁）重读书签并把到期时间后推，免得每个周期都碰它。返回是否恢复成功。
    /// </remarks>
    private async ValueTask<bool> ResumeAsync(WorkflowBookmark bookmark, CancellationToken cancellationToken)
    {
        var resumed = false;
        var outcome = await InvokeAsync(bookmark.TenantId, "workflow.poll.resume", async services =>
        {
            var result = await services.GetRequiredService<IWorkflowEngine>().ResumeAsync(bookmark.Id, null, null, cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                resumed = true;
                return Result.Success();
            }

            if (!string.Equals(result.Error!.Code, WorkflowErrors.BookmarkNotResumable, StringComparison.Ordinal))
            {
                return result.Error!;
            }

            // 引擎在锁内发现实例不在运行中后返回：锁仍由本次调用的作用域持有，重读后再写，不会把别人刚消费的书签写回来
            var store = services.GetRequiredService<IWorkflowBookmarkStore>();
            var current = await store.FindAsync(bookmark.TenantId, bookmark.Id, cancellationToken).ConfigureAwait(false);
            if (!current.TryGetValue(out var maybe))
            {
                return current.Error!;
            }

            if (!maybe.TryGetValue(out var deferred))
            {
                return Result.Success();
            }

            deferred.DueAt = _clock.GetUtcNow() + _options.NotResumableBackoff;
            return await store.SaveAsync(bookmark.TenantId, [deferred], cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
        if (outcome.IsFailure && outcome.Error!.Code is not (WorkflowErrors.BookmarkNotFound or WorkflowErrors.LockTimeout))
        {
            LogResumeFailed(_logger, bookmark.Id, outcome.Error.Code);
        }

        return resumed;
    }

    /// <summary>
    /// 在新作用域里以 <paramref name="tenantId"/> 开一次调用执行 <paramref name="handler"/>。
    /// </summary>
    private ValueTask<Result> InvokeAsync(string? tenantId, string target, Func<IServiceProvider, ValueTask<Result>> handler, CancellationToken cancellationToken)
        => WorkflowInvocations.InvokeAsync(_services.GetRequiredService<IServiceScopeFactory>(), _services.GetRequiredService<App>(), tenantId, target, handler, cancellationToken);

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
                    await RunCycleAsync(batchSize, stopping).ConfigureAwait(false);
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
    /// 清理失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Purging ended workflow instances failed with '{ErrorCode}'.")]
    private static partial void LogPollFailed(ILogger logger, string errorCode);

    /// <summary>
    /// 一个分区的扫描失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Scanning due workflow bookmarks of tenant '{TenantId}' failed with '{ErrorCode}'; other partitions continue.")]
    private static partial void LogPartitionFailed(ILogger logger, string tenantId, string errorCode);

    /// <summary>
    /// 租户目录读取失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Listing tenants for workflow polling threw; only the platform partition is polled this cycle.")]
    private static partial void LogTenantsFailed(ILogger logger, Exception exception);

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
