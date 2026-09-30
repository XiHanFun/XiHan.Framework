// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs;
using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Models;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs.Fakes;

/// <summary>
/// 记录调用次数的租约存储替身：全部操作转交给进程内默认存储
/// </summary>
public sealed class LeaseAwareBackgroundJobStore : IBackgroundJobStore
{
    private readonly object _gate = new();
    private int _waitingCallCount;
    private int _updateCallCount;
    private int _deleteCallCount;
    private int _renewCallCount;
    private int _completeCallCount;
    private int _tryUpdateCallCount;
    private int _releaseCallCount;
    private int _renewFailuresToInject;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="inner">被包装的进程内存储</param>
    public LeaseAwareBackgroundJobStore(DefaultBackgroundJobStore inner)
    {
        Inner = inner;
    }

    /// <summary>
    /// 被包装的进程内存储（用例以其模拟其它领取者，不计入调用次数）
    /// </summary>
    public DefaultBackgroundJobStore Inner { get; }

    /// <summary>
    /// 领取待执行作业的调用次数
    /// </summary>
    public int WaitingCallCount => Read(ref _waitingCallCount);

    /// <summary>
    /// 旧的更新方法调用次数
    /// </summary>
    public int UpdateCallCount => Read(ref _updateCallCount);

    /// <summary>
    /// 旧的删除方法调用次数
    /// </summary>
    public int DeleteCallCount => Read(ref _deleteCallCount);

    /// <summary>
    /// 续租调用次数
    /// </summary>
    public int RenewCallCount => Read(ref _renewCallCount);

    /// <summary>
    /// 按令牌完成调用次数
    /// </summary>
    public int CompleteCallCount => Read(ref _completeCallCount);

    /// <summary>
    /// 按令牌回写调用次数
    /// </summary>
    public int TryUpdateCallCount => Read(ref _tryUpdateCallCount);

    /// <summary>
    /// 释放租约调用次数
    /// </summary>
    public int ReleaseCallCount => Read(ref _releaseCallCount);

    /// <summary>
    /// 接下来的续租调用中抛出异常的次数（每次抛出减一，抛出的调用也计入续租调用次数）
    /// </summary>
    public int RenewFailuresToInject
    {
        get => Read(ref _renewFailuresToInject);
        set
        {
            lock (_gate)
            {
                _renewFailuresToInject = value;
            }
        }
    }

    /// <summary>
    /// 是否支持逐作业租约
    /// </summary>
    public bool SupportsJobLease => Inner.SupportsJobLease;

    /// <summary>
    /// 是否支持作业管理
    /// </summary>
    public bool SupportsJobManagement => Inner.SupportsJobManagement;

    /// <summary>
    /// 按标识查找作业
    /// </summary>
    public Task<BackgroundJobInfo?> FindAsync(Guid jobId)
    {
        return Inner.FindAsync(jobId);
    }

    /// <summary>
    /// 插入作业
    /// </summary>
    public Task InsertAsync(BackgroundJobInfo jobInfo)
    {
        return Inner.InsertAsync(jobInfo);
    }

    /// <summary>
    /// 领取待执行作业
    /// </summary>
    public Task<List<BackgroundJobInfo>> GetWaitingJobsAsync(string? applicationName, int maxResultCount)
    {
        Increment(ref _waitingCallCount);
        return Inner.GetWaitingJobsAsync(applicationName, maxResultCount);
    }

    /// <summary>
    /// 删除作业
    /// </summary>
    public Task DeleteAsync(Guid jobId)
    {
        Increment(ref _deleteCallCount);
        return Inner.DeleteAsync(jobId);
    }

    /// <summary>
    /// 更新作业
    /// </summary>
    public Task UpdateAsync(BackgroundJobInfo jobInfo)
    {
        Increment(ref _updateCallCount);
        return Inner.UpdateAsync(jobInfo);
    }

    /// <summary>
    /// 续租
    /// </summary>
    public async Task<BackgroundJobLease?> TryRenewLeaseAsync(BackgroundJobLease lease, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_renewFailuresToInject > 0)
            {
                _renewFailuresToInject--;
                _renewCallCount++;
                throw new InvalidOperationException("模拟续租失败");
            }
        }

        var renewed = await Inner.TryRenewLeaseAsync(lease, cancellationToken);
        Increment(ref _renewCallCount);
        return renewed;
    }

    /// <summary>
    /// 按令牌完成作业
    /// </summary>
    public async Task<bool> TryCompleteAsync(BackgroundJobLease lease, CancellationToken cancellationToken = default)
    {
        var completed = await Inner.TryCompleteAsync(lease, cancellationToken);
        Increment(ref _completeCallCount);
        return completed;
    }

    /// <summary>
    /// 按令牌回写作业
    /// </summary>
    public async Task<bool> TryUpdateAsync(BackgroundJobInfo jobInfo, BackgroundJobLease lease, CancellationToken cancellationToken = default)
    {
        var updated = await Inner.TryUpdateAsync(jobInfo, lease, cancellationToken);
        Increment(ref _tryUpdateCallCount);
        return updated;
    }

    /// <summary>
    /// 按令牌释放租约
    /// </summary>
    public async Task ReleaseLeaseAsync(BackgroundJobLease lease, CancellationToken cancellationToken = default)
    {
        await Inner.ReleaseLeaseAsync(lease, cancellationToken);
        Increment(ref _releaseCallCount);
    }

    /// <summary>
    /// 重试已放弃的作业
    /// </summary>
    public Task<BackgroundJobManagementStatus> RetryAbandonedAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return Inner.RetryAbandonedAsync(jobId, cancellationToken);
    }

    /// <summary>
    /// 请求取消作业
    /// </summary>
    public Task<BackgroundJobManagementStatus> RequestCancellationAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return Inner.RequestCancellationAsync(jobId, cancellationToken);
    }

    /// <summary>
    /// 读取计数
    /// </summary>
    private int Read(ref int counter)
    {
        lock (_gate)
        {
            return counter;
        }
    }

    /// <summary>
    /// 计数加一
    /// </summary>
    private void Increment(ref int counter)
    {
        lock (_gate)
        {
            counter++;
        }
    }
}
