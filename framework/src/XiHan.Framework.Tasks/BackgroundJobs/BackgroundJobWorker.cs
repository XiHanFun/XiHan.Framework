#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BackgroundJobWorker
// Guid:bb136187-cc77-4c03-92af-567632c891cf
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/07 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Caching.Distributed.Abstracts;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Models;
using XiHan.Framework.Tasks.BackgroundJobs.Options;
using XiHan.Framework.Timing;

namespace XiHan.Framework.Tasks.BackgroundJobs;

/// <summary>
/// 后台作业轮询 Worker：周期从存储领取待执行作业，执行成功即删除、失败按指数退避重试、累计超时放弃
/// </summary>
/// <remarks>
/// 多实例下通过分布式锁保证单活（同一时刻仅一个实例处理），配合存储的应用名过滤实现隔离。
/// 一轮内任一作业异常不会杀死 Worker，下一轮继续。
/// </remarks>
public class BackgroundJobWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDistributedLock _distributedLock;
    private readonly ILogger<BackgroundJobWorker> _logger;
    private readonly BackgroundJobWorkerOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    public BackgroundJobWorker(
        IServiceScopeFactory scopeFactory,
        IDistributedLock distributedLock,
        IOptions<BackgroundJobWorkerOptions> options,
        ILogger<BackgroundJobWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _distributedLock = distributedLock;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// 后台执行主循环
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.IsJobExecutionEnabled)
        {
            _logger.LogInformation("后台作业执行已关闭（IsJobExecutionEnabled=false），Worker 空转退出");
            return;
        }

        try
        {
            await Task.Delay(_options.FirstWaitDurationMilliseconds, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _logger.LogInformation("后台作业 Worker 已启动，轮询间隔 {Period}ms", _options.JobPollPeriodMilliseconds);

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_options.JobPollPeriodMilliseconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }

                await PollOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // 永不因单轮异常而崩溃
                _logger.LogError(ex, "后台作业轮询发生异常");
            }
        }
    }

    /// <summary>
    /// 单轮轮询：抢锁 → 领取 → 逐个执行
    /// </summary>
    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        await using var handle = await _distributedLock.TryAcquireAsync(
            _options.DistributedLockName,
            TimeSpan.FromSeconds(_options.DistributedLockExpirySeconds),
            cancellationToken);

        if (handle is null)
        {
            // 其它实例正在处理，本轮跳过
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        var store = serviceProvider.GetRequiredService<IBackgroundJobStore>();

        var jobs = await store.GetWaitingJobsAsync(_options.ApplicationName, _options.MaxJobFetchCount);
        if (jobs.Count == 0)
        {
            return;
        }

        var clock = serviceProvider.GetRequiredService<IClock>();
        var serializer = serviceProvider.GetRequiredService<IBackgroundJobSerializer>();
        var executer = serviceProvider.GetRequiredService<IBackgroundJobExecuter>();
        var currentTenant = serviceProvider.GetRequiredService<ICurrentTenant>();
        var jobOptions = serviceProvider.GetRequiredService<IOptions<BackgroundJobOptions>>().Value;

        foreach (var job in jobs)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await TryExecuteJobAsync(serviceProvider, store, clock, serializer, executer, currentTenant, jobOptions, job, cancellationToken);
        }
    }

    /// <summary>
    /// 执行单个作业并处理成功/失败/放弃
    /// </summary>
    private async Task TryExecuteJobAsync(
        IServiceProvider serviceProvider,
        IBackgroundJobStore store,
        IClock clock,
        IBackgroundJobSerializer serializer,
        IBackgroundJobExecuter executer,
        ICurrentTenant currentTenant,
        BackgroundJobOptions jobOptions,
        BackgroundJobInfo job,
        CancellationToken cancellationToken)
    {
        job.TryCount++;
        job.LastTryTime = clock.Now;

        var configuration = jobOptions.GetJobOrNull(job.JobName);
        if (configuration is null)
        {
            _logger.LogError("找不到作业配置：{JobName}（{JobId}），标记为放弃", job.JobName, job.Id);
            job.IsAbandoned = true;
            await TryUpdateAsync(store, job);
            return;
        }

        try
        {
            var args = serializer.Deserialize(job.JobArgs, configuration.ArgsType);

            using (currentTenant.Change(job.TenantId))
            {
                var context = new BackgroundJobExecutionContext(serviceProvider, configuration.JobType, args, cancellationToken);
                await executer.ExecuteAsync(context);
            }

            // 成功：删除
            await store.DeleteAsync(job.Id);
        }
        catch (BackgroundJobExecutionException)
        {
            // 业务失败：退避重试或累计超时放弃
            var nextTryTime = CalculateNextTryTime(job, clock);
            if (nextTryTime.HasValue)
            {
                job.NextTryTime = nextTryTime.Value;
                _logger.LogWarning("作业 {JobName}({JobId}) 第 {TryCount} 次失败，将于 {NextTry} 重试",
                    job.JobName, job.Id, job.TryCount, job.NextTryTime);
            }
            else
            {
                job.IsAbandoned = true;
                _logger.LogWarning("作业 {JobName}({JobId}) 累计重试超时，放弃", job.JobName, job.Id);
            }

            await TryUpdateAsync(store, job);
        }
        catch (Exception ex)
        {
            // 致命错误（反序列化失败 / 配置错误等）：直接放弃
            _logger.LogError(ex, "作业 {JobName}({JobId}) 遇致命错误，放弃", job.JobName, job.Id);
            job.IsAbandoned = true;
            await TryUpdateAsync(store, job);
        }
    }

    /// <summary>
    /// 计算下次重试时间：nextWait = 首等待 × 倍率^(TryCount-1) 秒；距创建超过放弃阈值则返回 null（放弃）
    /// </summary>
    private DateTime? CalculateNextTryTime(BackgroundJobInfo job, IClock clock)
    {
        var nextWaitSeconds = _options.DefaultFirstWaitDurationSeconds
            * Math.Pow(_options.DefaultWaitFactor, job.TryCount - 1);

        var nextTryDate = (job.LastTryTime ?? clock.Now).AddSeconds(nextWaitSeconds);

        return (nextTryDate - job.CreationTime).TotalSeconds > _options.DefaultTimeoutSeconds
            ? null
            : nextTryDate;
    }

    /// <summary>
    /// 容错更新（更新失败仅记日志，不影响主循环）
    /// </summary>
    private async Task TryUpdateAsync(IBackgroundJobStore store, BackgroundJobInfo job)
    {
        try
        {
            await store.UpdateAsync(job);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "更新后台作业状态失败：{JobId}", job.Id);
        }
    }
}
