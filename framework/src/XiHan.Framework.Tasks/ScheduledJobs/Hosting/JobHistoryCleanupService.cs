// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Configuration;

namespace XiHan.Framework.Tasks.ScheduledJobs.Hosting;

/// <summary>
/// 任务执行历史清理后台服务
/// </summary>
/// <remarks>
/// 启用 <see cref="XiHanJobOptions.HistoryCleanupEnabled"/> 后，每隔
/// <see cref="XiHanJobOptions.HistoryCleanupIntervalMinutes"/> 分钟按截止时间分批清理执行历史与已终结实例。
/// </remarks>
public class JobHistoryCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<XiHanJobOptions> _options;
    private readonly ILogger<JobHistoryCleanupService> _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="scopeFactory">服务作用域工厂</param>
    /// <param name="options">任务调度配置选项</param>
    /// <param name="logger">日志器</param>
    /// <param name="timeProvider">时间提供者，为空时使用系统时间</param>
    public JobHistoryCleanupService(
        IServiceScopeFactory scopeFactory,
        IOptions<XiHanJobOptions> options,
        ILogger<JobHistoryCleanupService> logger,
        TimeProvider? timeProvider = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// 执行一轮清理
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本轮删除的总条数</returns>
    /// <remarks>
    /// 截止时间为当前时间减去 <see cref="XiHanJobOptions.HistoryRetentionDays"/> 天；最多执行
    /// <see cref="XiHanJobOptions.HistoryCleanupMaxBatchesPerRun"/> 批，某批删除数小于批量上限即结束。
    /// </remarks>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        var cutoff = _timeProvider.GetUtcNow().AddDays(-options.HistoryRetentionDays);

        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IJobStore>();

        var total = 0;
        for (var batch = 0; batch < options.HistoryCleanupMaxBatchesPerRun; batch++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var deleted = await store.CleanupHistoryAsync(cutoff, options.HistoryCleanupBatchSize, cancellationToken);
            total += deleted;

            if (deleted < options.HistoryCleanupBatchSize)
            {
                break;
            }
        }

        return total;
    }

    /// <summary>
    /// 按间隔循环执行清理
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;
        if (!options.HistoryCleanupEnabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.HistoryCleanupIntervalMinutes), _timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var deleted = await RunOnceAsync(stoppingToken);
                    if (deleted > 0)
                    {
                        _logger.LogInformation("任务历史清理完成，本轮删除 {DeletedCount} 条记录", deleted);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "任务历史清理失败，将在下一轮重试");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
