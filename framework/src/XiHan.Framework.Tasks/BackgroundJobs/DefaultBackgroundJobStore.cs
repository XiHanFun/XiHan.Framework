// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Models;
using XiHan.Framework.Tasks.BackgroundJobs.Options;
using XiHan.Framework.Timing;

namespace XiHan.Framework.Tasks.BackgroundJobs;

/// <summary>
/// 默认后台作业存储（有界进程内实现）
/// </summary>
/// <remarks>
/// 进程重启后作业丢失、且不跨实例。要持久化与跨实例可靠投递，请实现 <see cref="IBackgroundJobStore"/>
/// （基于数据库 / Redis）并在 DI 中替换本默认实现；其 GetWaitingJobsAsync 须做原子领取。
/// 本实现支持逐作业租约与作业管理；放弃的作业即从存储移除，因此重试已放弃作业通常返回 NotFound。
/// </remarks>
public class DefaultBackgroundJobStore : IBackgroundJobStore
{
    private const int MaxJobCount = 100000;
    private const int DefaultLeaseDurationSeconds = 300;

    private readonly ConcurrentDictionary<Guid, BackgroundJobInfo> _jobs = new();
    private readonly IClock _clock;
    private readonly TimeSpan _leaseDuration;
    private readonly Lock _writeLock = new();

    /// <summary>
    /// 构造函数（租约时长取 300 秒）
    /// </summary>
    /// <param name="clock">时钟</param>
    public DefaultBackgroundJobStore(IClock clock)
    {
        _clock = clock;
        _leaseDuration = TimeSpan.FromSeconds(DefaultLeaseDurationSeconds);
    }

    /// <summary>
    /// 构造函数（租约时长取 <see cref="BackgroundJobWorkerOptions.JobLeaseDurationSeconds"/>）
    /// </summary>
    /// <param name="clock">时钟</param>
    /// <param name="options">Worker 选项</param>
    /// <exception cref="ArgumentOutOfRangeException">租约时长不大于 0</exception>
    public DefaultBackgroundJobStore(IClock clock, IOptions<BackgroundJobWorkerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var seconds = options.Value.JobLeaseDurationSeconds;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(seconds, nameof(BackgroundJobWorkerOptions.JobLeaseDurationSeconds));

        _clock = clock;
        _leaseDuration = TimeSpan.FromSeconds(seconds);
    }

    /// <summary>
    /// 是否支持逐作业租约
    /// </summary>
    public bool SupportsJobLease => true;

    /// <summary>
    /// 是否支持作业管理
    /// </summary>
    public bool SupportsJobManagement => true;

    /// <summary>
    /// 按标识查找作业（返回存储中的对象本身）
    /// </summary>
    public Task<BackgroundJobInfo?> FindAsync(Guid jobId)
    {
        return Task.FromResult(_jobs.GetValueOrDefault(jobId));
    }

    /// <summary>
    /// 插入作业
    /// </summary>
    public Task InsertAsync(BackgroundJobInfo jobInfo)
    {
        ArgumentNullException.ThrowIfNull(jobInfo);
        lock (_writeLock)
        {
            if (!_jobs.ContainsKey(jobInfo.Id) && _jobs.Count >= MaxJobCount)
            {
                throw new InvalidOperationException($"默认后台作业存储已达到 {MaxJobCount} 条上限，请替换为应用级持久化实现。");
            }

            _jobs[jobInfo.Id] = jobInfo;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取待执行作业（过滤 + 排序 + 限量，契约见接口），并为选中的作业写入新令牌与租约到期时间
    /// </summary>
    /// <returns>选中作业的副本</returns>
    public Task<List<BackgroundJobInfo>> GetWaitingJobsAsync(string? applicationName, int maxResultCount)
    {
        lock (_writeLock)
        {
            var now = _clock.Now;

            var selected = _jobs.Values
                .Where(x => string.Equals(x.ApplicationName, applicationName, StringComparison.Ordinal))
                .Where(x => !x.IsAbandoned && x.NextTryTime <= now)
                .Where(x => x.ClaimToken == null || x.LeaseExpiresAt <= now)
                .OrderByDescending(x => x.Priority)
                .ThenBy(x => x.TryCount)
                .ThenBy(x => x.NextTryTime)
                .Take(maxResultCount)
                .ToList();

            var result = new List<BackgroundJobInfo>(selected.Count);
            foreach (var job in selected)
            {
                job.ClaimToken = Guid.NewGuid().ToString("N");
                job.LeaseExpiresAt = now + _leaseDuration;
                result.Add(Clone(job));
            }

            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// 删除作业
    /// </summary>
    public Task DeleteAsync(Guid jobId)
    {
        lock (_writeLock)
        {
            _jobs.TryRemove(jobId, out _);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 更新作业（写入前清除租约；放弃的作业直接移除）
    /// </summary>
    public Task UpdateAsync(BackgroundJobInfo jobInfo)
    {
        ArgumentNullException.ThrowIfNull(jobInfo);

        jobInfo.ClaimToken = null;
        jobInfo.LeaseExpiresAt = null;

        if (jobInfo.IsAbandoned)
        {
            lock (_writeLock)
            {
                _jobs.TryRemove(jobInfo.Id, out _);
            }

            return Task.CompletedTask;
        }

        return InsertAsync(jobInfo);
    }

    /// <summary>
    /// 续租：令牌匹配且租约未到期时延长租约
    /// </summary>
    public Task<BackgroundJobLease?> TryRenewLeaseAsync(BackgroundJobLease lease, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        lock (_writeLock)
        {
            var now = _clock.Now;
            if (!TryGetLeased(lease, out var stored) || !(stored.LeaseExpiresAt > now))
            {
                return Task.FromResult<BackgroundJobLease?>(null);
            }

            var expiresAt = now + _leaseDuration;
            stored.LeaseExpiresAt = expiresAt;
            return Task.FromResult<BackgroundJobLease?>(
                new BackgroundJobLease(stored.Id, lease.Token, expiresAt, stored.IsCancellationRequested));
        }
    }

    /// <summary>
    /// 按令牌完成作业（删除）
    /// </summary>
    public Task<bool> TryCompleteAsync(BackgroundJobLease lease, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        lock (_writeLock)
        {
            if (!TryGetLeased(lease, out _))
            {
                return Task.FromResult(false);
            }

            _jobs.TryRemove(lease.JobId, out _);
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// 按令牌回写作业并结束租约；取消请求与存储中已登记的合并，已请求取消的作业按放弃处理；放弃的作业直接移除
    /// </summary>
    public Task<bool> TryUpdateAsync(BackgroundJobInfo jobInfo, BackgroundJobLease lease, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jobInfo);
        ArgumentNullException.ThrowIfNull(lease);
        lock (_writeLock)
        {
            if (!TryGetLeased(lease, out var stored))
            {
                return Task.FromResult(false);
            }

            stored.TryCount = jobInfo.TryCount;
            stored.NextTryTime = jobInfo.NextTryTime;
            stored.LastTryTime = jobInfo.LastTryTime;
            stored.IsCancellationRequested = stored.IsCancellationRequested || jobInfo.IsCancellationRequested;
            stored.IsAbandoned = jobInfo.IsAbandoned || stored.IsCancellationRequested;
            stored.ClaimToken = null;
            stored.LeaseExpiresAt = null;

            if (stored.IsAbandoned)
            {
                _jobs.TryRemove(stored.Id, out _);
            }

            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// 按令牌释放租约，作业保持待执行
    /// </summary>
    public Task ReleaseLeaseAsync(BackgroundJobLease lease, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        lock (_writeLock)
        {
            if (TryGetLeased(lease, out var stored))
            {
                stored.ClaimToken = null;
                stored.LeaseExpiresAt = null;
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 把已放弃的作业重新排入待执行
    /// </summary>
    public Task<BackgroundJobManagementStatus> RetryAbandonedAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        lock (_writeLock)
        {
            if (!_jobs.TryGetValue(jobId, out var stored))
            {
                return Task.FromResult(BackgroundJobManagementStatus.NotFound);
            }

            if (!stored.IsAbandoned)
            {
                return Task.FromResult(BackgroundJobManagementStatus.NoChange);
            }

            stored.IsAbandoned = false;
            stored.IsCancellationRequested = false;
            stored.TryCount = 0;
            stored.NextTryTime = _clock.Now;
            stored.ClaimToken = null;
            stored.LeaseExpiresAt = null;
            return Task.FromResult(BackgroundJobManagementStatus.Rescheduled);
        }
    }

    /// <summary>
    /// 请求取消作业：持有有效租约的作业登记取消请求，其余未放弃的作业直接移除
    /// </summary>
    public Task<BackgroundJobManagementStatus> RequestCancellationAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        lock (_writeLock)
        {
            if (!_jobs.TryGetValue(jobId, out var stored))
            {
                return Task.FromResult(BackgroundJobManagementStatus.NotFound);
            }

            if (stored.IsAbandoned)
            {
                return Task.FromResult(BackgroundJobManagementStatus.NoChange);
            }

            if (stored.ClaimToken != null && stored.LeaseExpiresAt > _clock.Now)
            {
                if (stored.IsCancellationRequested)
                {
                    return Task.FromResult(BackgroundJobManagementStatus.NoChange);
                }

                stored.IsCancellationRequested = true;
                return Task.FromResult(BackgroundJobManagementStatus.CancellationRequested);
            }

            _jobs.TryRemove(jobId, out _);
            return Task.FromResult(BackgroundJobManagementStatus.Cancelled);
        }
    }

    /// <summary>
    /// 查找令牌匹配的存储作业
    /// </summary>
    private bool TryGetLeased(BackgroundJobLease lease, [NotNullWhen(true)] out BackgroundJobInfo? stored)
    {
        if (_jobs.TryGetValue(lease.JobId, out var found)
            && found.ClaimToken != null
            && string.Equals(found.ClaimToken, lease.Token, StringComparison.Ordinal))
        {
            stored = found;
            return true;
        }

        stored = null;
        return false;
    }

    /// <summary>
    /// 复制作业信息的全部字段
    /// </summary>
    private static BackgroundJobInfo Clone(BackgroundJobInfo source)
    {
        return new BackgroundJobInfo
        {
            Id = source.Id,
            ApplicationName = source.ApplicationName,
            TenantId = source.TenantId,
            JobName = source.JobName,
            JobArgs = source.JobArgs,
            TryCount = source.TryCount,
            CreationTime = source.CreationTime,
            NextTryTime = source.NextTryTime,
            LastTryTime = source.LastTryTime,
            IsAbandoned = source.IsAbandoned,
            Priority = source.Priority,
            ClaimToken = source.ClaimToken,
            LeaseExpiresAt = source.LeaseExpiresAt,
            IsCancellationRequested = source.IsCancellationRequested
        };
    }
}
