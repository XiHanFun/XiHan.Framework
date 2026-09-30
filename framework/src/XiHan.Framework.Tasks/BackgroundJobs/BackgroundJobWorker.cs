// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
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
/// 存储支持逐作业租约时，每个作业执行前确认租约、执行中按间隔续租、按令牌回写；续租失败即取消执行令牌且不回写。
/// </remarks>
public class BackgroundJobWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDistributedLock _distributedLock;
    private readonly ILogger<BackgroundJobWorker> _logger;
    private readonly BackgroundJobWorkerOptions _options;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 构造函数（续租等待使用系统时间）
    /// </summary>
    /// <param name="scopeFactory">作用域工厂</param>
    /// <param name="distributedLock">分布式锁</param>
    /// <param name="options">Worker 选项</param>
    /// <param name="logger">日志</param>
    public BackgroundJobWorker(
        IServiceScopeFactory scopeFactory,
        IDistributedLock distributedLock,
        IOptions<BackgroundJobWorkerOptions> options,
        ILogger<BackgroundJobWorker> logger)
        : this(scopeFactory, distributedLock, options, logger, TimeProvider.System)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="scopeFactory">作用域工厂</param>
    /// <param name="distributedLock">分布式锁</param>
    /// <param name="options">Worker 选项</param>
    /// <param name="logger">日志</param>
    /// <param name="timeProvider">续租等待使用的时间提供器</param>
    public BackgroundJobWorker(
        IServiceScopeFactory scopeFactory,
        IDistributedLock distributedLock,
        IOptions<BackgroundJobWorkerOptions> options,
        ILogger<BackgroundJobWorker> logger,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        _scopeFactory = scopeFactory;
        _distributedLock = distributedLock;
        _logger = logger;
        _options = options.Value;
        _timeProvider = timeProvider;
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

        var useJobLease = store.SupportsJobLease;
        var index = 0;

        try
        {
            var clock = serviceProvider.GetRequiredService<IClock>();
            var serializer = serviceProvider.GetRequiredService<IBackgroundJobSerializer>();
            var executer = serviceProvider.GetRequiredService<IBackgroundJobExecuter>();
            var currentTenant = serviceProvider.GetRequiredService<ICurrentTenant>();
            var jobOptions = serviceProvider.GetRequiredService<IOptions<BackgroundJobOptions>>().Value;

            // 作业之间按分布式锁 TTL 的一半为周期续期，计时使用 IClock
            var lockExpiry = TimeSpan.FromSeconds(_options.DistributedLockExpirySeconds);
            var renewInterval = lockExpiry / 2;
            var lastRenewTime = clock.Now;

            for (; index < jobs.Count; index++)
            {
                var job = jobs[index];
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (renewInterval > TimeSpan.Zero && clock.Now - lastRenewTime >= renewInterval)
                {
                    lastRenewTime = clock.Now;
                    if (!await TryExtendLockAsync(handle, lockExpiry, cancellationToken))
                    {
                        // 已经不确定自己还持有锁，继续跑就可能与抢到锁的另一实例重复执行；
                        // 剩下的作业留在存储里，下一轮重新抢锁后接着处理
                        break;
                    }
                }

                if (useJobLease && TryCreateLease(job, out var lease))
                {
                    await ExecuteLeasedJobAsync(serviceProvider, store, clock, serializer, executer, currentTenant, jobOptions, job, lease, cancellationToken);
                }
                else
                {
                    await TryExecuteJobAsync(serviceProvider, store, clock, serializer, executer, currentTenant, jobOptions, job, cancellationToken);
                }
            }
        }
        finally
        {
            if (useJobLease)
            {
                await ReleasePendingLeasesAsync(store, jobs, index);
            }
        }
    }

    /// <summary>
    /// 容错续期（续不上或续期出错都视为已失去锁，由调用方结束本轮）
    /// </summary>
    private async Task<bool> TryExtendLockAsync(IDistributedLockHandle handle, TimeSpan expiry, CancellationToken cancellationToken)
    {
        try
        {
            if (await handle.ExtendAsync(expiry, cancellationToken))
            {
                return true;
            }

            _logger.LogWarning("后台作业分布式锁续期失败（已不再持有），本轮提前结束以避免与其它实例重复执行");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "后台作业分布式锁续期异常，本轮提前结束以避免与其它实例重复执行");
            return false;
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
        catch (Exception ex) when (cancellationToken.IsCancellationRequested && IsCancellation(ex))
        {
            _logger.LogInformation(ex, "宿主停止中断了后台作业 {JobName}({JobId})，不累计失败也不回写", job.JobName, job.Id);
        }
        catch (Exception ex)
        {
            MarkFailed(job, clock, ex);
            await TryUpdateAsync(store, job);
        }
    }

    /// <summary>
    /// 按租约执行单个作业：执行前确认租约、执行中按间隔续租、按令牌回写
    /// </summary>
    /// <remarks>
    /// 执行前确认未命中时跳过作业：按本地时钟租约已到期记 Debug 日志，租约未到期（已被其它领取者换走或作业已不存在）记 Warning 日志。
    /// </remarks>
    private async Task ExecuteLeasedJobAsync(
        IServiceProvider serviceProvider,
        IBackgroundJobStore store,
        IClock clock,
        IBackgroundJobSerializer serializer,
        IBackgroundJobExecuter executer,
        ICurrentTenant currentTenant,
        BackgroundJobOptions jobOptions,
        BackgroundJobInfo job,
        BackgroundJobLease lease,
        CancellationToken stoppingToken)
    {
        BackgroundJobLease? confirmed;
        try
        {
            confirmed = await store.TryRenewLeaseAsync(lease, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "确认后台作业租约失败，跳过：{JobName}({JobId})", job.JobName, job.Id);
            return;
        }

        if (confirmed is null)
        {
            if (clock.Now >= lease.ExpiresAt)
            {
                _logger.LogDebug("后台作业 {JobName}({JobId}) 的租约已到期，跳过", job.JobName, job.Id);
            }
            else
            {
                _logger.LogWarning("后台作业 {JobName}({JobId}) 的租约已不属于本 Worker，跳过", job.JobName, job.Id);
            }

            return;
        }

        if (confirmed.IsCancellationRequested)
        {
            _logger.LogInformation("后台作业 {JobName}({JobId}) 已被请求取消，标记为放弃", job.JobName, job.Id);
            job.IsAbandoned = true;
            job.IsCancellationRequested = true;
            await TryWriteBackAsync(store, job, confirmed);
            return;
        }

        job.TryCount++;
        job.LastTryTime = clock.Now;

        var configuration = jobOptions.GetJobOrNull(job.JobName);
        if (configuration is null)
        {
            _logger.LogError("找不到作业配置：{JobName}（{JobId}），标记为放弃", job.JobName, job.Id);
            job.IsAbandoned = true;
            await TryWriteBackAsync(store, job, confirmed);
            return;
        }

        var interval = CalculateRenewalInterval(confirmed.ExpiresAt - clock.Now);
        if (interval <= TimeSpan.Zero)
        {
            _logger.LogWarning("后台作业 {JobName}({JobId}) 的租约剩余时间不足，释放租约并跳过", job.JobName, job.Id);
            await TryReleaseLeaseAsync(store, confirmed);
            return;
        }

        var renewal = new LeaseRenewalState(confirmed);
        using var executionCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        using var renewCts = new CancellationTokenSource();
        var renewTask = RenewLeaseLoopAsync(store, clock, job, renewal, interval, executionCts, renewCts.Token);

        Exception? failure = null;
        try
        {
            var args = serializer.Deserialize(job.JobArgs, configuration.ArgsType);

            using (currentTenant.Change(job.TenantId))
            {
                var context = new BackgroundJobExecutionContext(serviceProvider, configuration.JobType, args, executionCts.Token);
                await executer.ExecuteAsync(context);
            }
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            await TryCancelAsync(renewCts, job);
            await renewTask;
        }

        var current = renewal.Current;
        if (renewal.IsLost)
        {
            _logger.LogWarning("后台作业 {JobName}({JobId}) 已失去租约，不回写执行结果", job.JobName, job.Id);
            return;
        }

        if (failure is null)
        {
            await TryCompleteLeasedAsync(store, job, current);
            return;
        }

        if (renewal.IsCancellationRequested)
        {
            _logger.LogInformation("后台作业 {JobName}({JobId}) 已按取消请求停止，标记为放弃", job.JobName, job.Id);
            job.IsAbandoned = true;
            job.IsCancellationRequested = true;
            await TryWriteBackAsync(store, job, current);
            return;
        }

        if (stoppingToken.IsCancellationRequested && IsCancellation(failure))
        {
            _logger.LogInformation("宿主停止中断了后台作业 {JobName}({JobId})，释放租约", job.JobName, job.Id);
            await TryReleaseLeaseAsync(store, current);
            return;
        }

        MarkFailed(job, clock, failure);
        await TryWriteBackAsync(store, job, current);
    }

    /// <summary>
    /// 续租循环：每隔 <paramref name="interval"/> 续租一次；续租未命中或租约已到期时标记失租并取消执行，续租带回取消请求时取消执行
    /// </summary>
    private async Task RenewLeaseLoopAsync(
        IBackgroundJobStore store,
        IClock clock,
        BackgroundJobInfo job,
        LeaseRenewalState renewal,
        TimeSpan interval,
        CancellationTokenSource executionCts,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                await Task.Delay(interval, _timeProvider, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            BackgroundJobLease? renewed;
            try
            {
                renewed = await store.TryRenewLeaseAsync(renewal.Current, CancellationToken.None);
            }
            catch (Exception ex)
            {
                if (clock.Now >= renewal.Current.ExpiresAt)
                {
                    _logger.LogWarning(ex, "后台作业 {JobName}({JobId}) 续租异常且租约已到期，取消执行", job.JobName, job.Id);
                    renewal.IsLost = true;
                    await TryCancelAsync(executionCts, job);
                    return;
                }

                _logger.LogWarning(ex, "后台作业 {JobName}({JobId}) 续租异常，下个间隔重试", job.JobName, job.Id);
                continue;
            }

            if (renewed is null)
            {
                _logger.LogWarning("后台作业 {JobName}({JobId}) 续租未命中，已失去租约，取消执行", job.JobName, job.Id);
                renewal.IsLost = true;
                await TryCancelAsync(executionCts, job);
                return;
            }

            renewal.Current = renewed;
            if (renewed.IsCancellationRequested && !renewal.IsCancellationRequested)
            {
                _logger.LogInformation("后台作业 {JobName}({JobId}) 收到取消请求，取消执行", job.JobName, job.Id);
                renewal.IsCancellationRequested = true;
                await TryCancelAsync(executionCts, job);
            }
        }
    }

    /// <summary>
    /// 计算续租间隔：配置值大于 0 且小于租约时长三分之一时取配置值，否则取租约时长的四分之一
    /// </summary>
    private TimeSpan CalculateRenewalInterval(TimeSpan leaseDuration)
    {
        var configured = TimeSpan.FromSeconds(_options.JobLeaseRenewalIntervalSeconds);
        return configured > TimeSpan.Zero && configured < leaseDuration / 3
            ? configured
            : leaseDuration / 4;
    }

    /// <summary>
    /// 按失败类型更新作业：业务失败退避重试或累计超时放弃，其它异常直接放弃
    /// </summary>
    private void MarkFailed(BackgroundJobInfo job, IClock clock, Exception exception)
    {
        if (exception is BackgroundJobExecutionException)
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

            return;
        }

        // 致命错误（反序列化失败 / 配置错误等）：直接放弃
        _logger.LogError(exception, "作业 {JobName}({JobId}) 遇致命错误，放弃", job.JobName, job.Id);
        job.IsAbandoned = true;
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
    /// 判断异常或其内部异常链中是否包含取消异常
    /// </summary>
    private static bool IsCancellation(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is OperationCanceledException)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 由作业上的令牌与到期时间构造租约
    /// </summary>
    private static bool TryCreateLease(BackgroundJobInfo job, [NotNullWhen(true)] out BackgroundJobLease? lease)
    {
        if (job.ClaimToken is not null && job.LeaseExpiresAt.HasValue)
        {
            lease = new BackgroundJobLease(job.Id, job.ClaimToken, job.LeaseExpiresAt.Value, job.IsCancellationRequested);
            return true;
        }

        lease = null;
        return false;
    }

    /// <summary>
    /// 释放从 <paramref name="startIndex"/> 起尚未执行的作业租约
    /// </summary>
    private async Task ReleasePendingLeasesAsync(IBackgroundJobStore store, List<BackgroundJobInfo> jobs, int startIndex)
    {
        for (var i = startIndex; i < jobs.Count; i++)
        {
            if (TryCreateLease(jobs[i], out var lease))
            {
                await TryReleaseLeaseAsync(store, lease);
            }
        }
    }

    /// <summary>
    /// 容错按令牌完成（未命中或出错仅记日志）
    /// </summary>
    private async Task TryCompleteLeasedAsync(IBackgroundJobStore store, BackgroundJobInfo job, BackgroundJobLease lease)
    {
        try
        {
            if (!await store.TryCompleteAsync(lease, CancellationToken.None))
            {
                _logger.LogWarning("后台作业 {JobName}({JobId}) 按令牌完成未命中，租约已不属于本 Worker", job.JobName, job.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "按令牌完成后台作业失败：{JobId}", job.Id);
        }
    }

    /// <summary>
    /// 容错按令牌回写（未命中或出错仅记日志）
    /// </summary>
    private async Task TryWriteBackAsync(IBackgroundJobStore store, BackgroundJobInfo job, BackgroundJobLease lease)
    {
        try
        {
            if (!await store.TryUpdateAsync(job, lease, CancellationToken.None))
            {
                _logger.LogWarning("后台作业 {JobName}({JobId}) 按令牌回写未命中，租约已不属于本 Worker", job.JobName, job.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "按令牌回写后台作业状态失败：{JobId}", job.Id);
        }
    }

    /// <summary>
    /// 容错取消（取消回调抛出的异常仅记日志）
    /// </summary>
    private async Task TryCancelAsync(CancellationTokenSource cancellationTokenSource, BackgroundJobInfo job)
    {
        try
        {
            await cancellationTokenSource.CancelAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "取消后台作业执行时回调出错：{JobName}({JobId})", job.JobName, job.Id);
        }
    }

    /// <summary>
    /// 容错释放租约（出错仅记日志）
    /// </summary>
    private async Task TryReleaseLeaseAsync(IBackgroundJobStore store, BackgroundJobLease lease)
    {
        try
        {
            await store.ReleaseLeaseAsync(lease, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "释放后台作业租约失败：{JobId}", lease.JobId);
        }
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

    /// <summary>
    /// 单个作业的续租状态
    /// </summary>
    private sealed class LeaseRenewalState
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="lease">初始租约</param>
        public LeaseRenewalState(BackgroundJobLease lease)
        {
            Current = lease;
        }

        /// <summary>
        /// 当前租约
        /// </summary>
        public BackgroundJobLease Current { get; set; }

        /// <summary>
        /// 是否已失去租约
        /// </summary>
        public bool IsLost { get; set; }

        /// <summary>
        /// 是否已收到取消请求
        /// </summary>
        public bool IsCancellationRequested { get; set; }
    }
}
