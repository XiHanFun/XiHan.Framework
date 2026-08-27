// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Models;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs.Fakes;

/// <summary>
/// 可编排的后台作业存储替身
/// </summary>
/// <remarks>
/// 待执行作业按"批"投喂：第一次轮询拿到第一批，之后一律返回空列表，
/// 保证 Worker 不会把同一个作业反复执行，用例断言的调用次数才有确定值。
/// </remarks>
public sealed class RecordingBackgroundJobStore : IBackgroundJobStore
{
    private readonly object _gate = new();
    private readonly Queue<List<BackgroundJobInfo>> _batches = new();
    private readonly List<BackgroundJobInfo> _inserted = [];
    private readonly List<BackgroundJobInfo> _updated = [];
    private readonly List<Guid> _deleted = [];
    private int _waitingCallCount;
    private string? _lastApplicationName;
    private int _lastMaxResultCount;

    /// <summary>
    /// 是否在更新时抛异常（验证 Worker 的容错更新不会杀死轮询主循环）
    /// </summary>
    public bool ThrowOnUpdate { get; set; }

    /// <summary>
    /// 领取待执行作业的调用次数
    /// </summary>
    public int WaitingCallCount
    {
        get
        {
            lock (_gate)
            {
                return _waitingCallCount;
            }
        }
    }

    /// <summary>
    /// 最近一次领取时传入的应用名
    /// </summary>
    public string? LastApplicationName
    {
        get
        {
            lock (_gate)
            {
                return _lastApplicationName;
            }
        }
    }

    /// <summary>
    /// 最近一次领取时传入的最大数量
    /// </summary>
    public int LastMaxResultCount
    {
        get
        {
            lock (_gate)
            {
                return _lastMaxResultCount;
            }
        }
    }

    /// <summary>
    /// 已插入的作业
    /// </summary>
    public IReadOnlyList<BackgroundJobInfo> Inserted
    {
        get
        {
            lock (_gate)
            {
                return [.. _inserted];
            }
        }
    }

    /// <summary>
    /// 已回写的作业
    /// </summary>
    public IReadOnlyList<BackgroundJobInfo> Updated
    {
        get
        {
            lock (_gate)
            {
                return [.. _updated];
            }
        }
    }

    /// <summary>
    /// 已删除的作业标识
    /// </summary>
    public IReadOnlyList<Guid> Deleted
    {
        get
        {
            lock (_gate)
            {
                return [.. _deleted];
            }
        }
    }

    /// <summary>
    /// 追加一批待领取的作业
    /// </summary>
    /// <param name="jobs">作业列表</param>
    public void EnqueueWaitingBatch(params BackgroundJobInfo[] jobs)
    {
        lock (_gate)
        {
            _batches.Enqueue([.. jobs]);
        }
    }

    /// <summary>
    /// 按标识查找作业
    /// </summary>
    /// <param name="jobId">作业标识</param>
    /// <returns>作业信息</returns>
    public Task<BackgroundJobInfo?> FindAsync(Guid jobId)
    {
        lock (_gate)
        {
            return Task.FromResult<BackgroundJobInfo?>(_inserted.Find(x => x.Id == jobId));
        }
    }

    /// <summary>
    /// 插入作业
    /// </summary>
    /// <param name="jobInfo">作业信息</param>
    /// <returns>任务</returns>
    public Task InsertAsync(BackgroundJobInfo jobInfo)
    {
        lock (_gate)
        {
            _inserted.Add(jobInfo);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 领取待执行作业
    /// </summary>
    /// <param name="applicationName">应用名</param>
    /// <param name="maxResultCount">最大数量</param>
    /// <returns>待执行作业列表</returns>
    public Task<List<BackgroundJobInfo>> GetWaitingJobsAsync(string? applicationName, int maxResultCount)
    {
        lock (_gate)
        {
            _waitingCallCount++;
            _lastApplicationName = applicationName;
            _lastMaxResultCount = maxResultCount;

            List<BackgroundJobInfo> batch = _batches.Count > 0 ? _batches.Dequeue() : [];
            return Task.FromResult(batch);
        }
    }

    /// <summary>
    /// 删除作业
    /// </summary>
    /// <param name="jobId">作业标识</param>
    /// <returns>任务</returns>
    public Task DeleteAsync(Guid jobId)
    {
        lock (_gate)
        {
            _deleted.Add(jobId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 更新作业
    /// </summary>
    /// <param name="jobInfo">作业信息</param>
    /// <returns>任务</returns>
    public Task UpdateAsync(BackgroundJobInfo jobInfo)
    {
        lock (_gate)
        {
            _updated.Add(jobInfo);
        }

        return ThrowOnUpdate
            ? Task.FromException(new InvalidOperationException("模拟存储写回失败"))
            : Task.CompletedTask;
    }
}
