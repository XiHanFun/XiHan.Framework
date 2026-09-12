// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Store;

/// <summary>
/// 默认任务存储（有界进程内实现）
/// </summary>
public class DefaultJobStore : IJobStore
{
    private const int MaxCompletedInstanceCount = 10000;
    private const int MaxInstanceCount = 20000;
    private const int MaxHistoryCount = 100000;

    private readonly ConcurrentDictionary<string, JobInstance> _instances = new();
    private readonly ConcurrentDictionary<string, JobHistory> _histories = new();
    private readonly ConcurrentDictionary<string, byte> _completedInstanceIds = new();
    private readonly ConcurrentQueue<string> _completedInstanceOrder = new();
    private readonly Lock _instanceWriteLock = new();
    private readonly Lock _historyWriteLock = new();

    /// <summary>
    /// 保存任务实例
    /// </summary>
    public Task SaveJobInstanceAsync(JobInstance jobInstance)
    {
        ArgumentNullException.ThrowIfNull(jobInstance);

        lock (_instanceWriteLock)
        {
            if (!_instances.ContainsKey(jobInstance.InstanceId) && _instances.Count >= MaxInstanceCount)
            {
                throw new InvalidOperationException($"默认任务实例存储已达到 {MaxInstanceCount} 条上限，请替换为应用级持久化实现。");
            }

            _instances.AddOrUpdate(jobInstance.InstanceId, jobInstance, (_, _) => jobInstance);
        }
        if (jobInstance.Status is JobStatus.Succeeded or JobStatus.Failed or JobStatus.Canceled)
        {
            jobInstance.CompletedAt ??= DateTimeOffset.UtcNow;
            TrackCompletedInstance(jobInstance.InstanceId);
        }
        else
        {
            _completedInstanceIds.TryRemove(jobInstance.InstanceId, out _);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 更新任务实例状态
    /// </summary>
    public Task UpdateJobStatusAsync(string instanceId, JobStatus status)
    {
        if (_instances.TryGetValue(instanceId, out var instance))
        {
            instance.Status = status;
            if (status is JobStatus.Succeeded or JobStatus.Failed or JobStatus.Canceled)
            {
                instance.CompletedAt = DateTimeOffset.UtcNow;
                TrackCompletedInstance(instanceId);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 保存任务执行历史
    /// </summary>
    public Task SaveJobHistoryAsync(JobHistory history)
    {
        ArgumentNullException.ThrowIfNull(history);

        var historyId = string.IsNullOrWhiteSpace(history.HistoryId)
            ? Guid.NewGuid().ToString("N")
            : history.HistoryId;

        history.HistoryId = historyId;
        lock (_historyWriteLock)
        {
            if (!_histories.ContainsKey(historyId) && _histories.Count >= MaxHistoryCount)
            {
                throw new InvalidOperationException($"默认任务历史存储已达到 {MaxHistoryCount} 条上限，请替换为应用级持久化实现。");
            }

            _histories.AddOrUpdate(historyId, history, (_, _) => history);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取任务实例
    /// </summary>
    public Task<JobInstance?> GetJobInstanceAsync(string instanceId)
    {
        _instances.TryGetValue(instanceId, out var instance);
        return Task.FromResult(instance);
    }

    /// <summary>
    /// 获取任务执行历史
    /// </summary>
    public Task<IReadOnlyList<JobHistory>> GetJobHistoryAsync(string jobName, int pageIndex = 1, int pageSize = 20)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);

        if (pageIndex < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), pageIndex, "页码必须大于等于 1。");
        }

        if (pageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "页大小必须大于等于 1。");
        }

        var histories = _histories.Values
            .Where(h => h.JobName == jobName)
            .OrderByDescending(h => h.StartedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult<IReadOnlyList<JobHistory>>(histories);
    }

    /// <summary>
    /// 获取运行中的任务实例
    /// </summary>
    public Task<IReadOnlyList<JobInstance>> GetRunningInstancesAsync(string jobName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);

        var runningInstances = _instances.Values
            .Where(i => i.JobName == jobName && i.Status == JobStatus.Running)
            .ToList();

        return Task.FromResult<IReadOnlyList<JobInstance>>(runningInstances);
    }

    /// <summary>
    /// 清理过期的历史记录
    /// </summary>
    public Task CleanupHistoryAsync(int retentionDays)
    {
        if (retentionDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retentionDays), retentionDays, "保留天数不能小于 0。");
        }

        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-retentionDays);

        foreach (var history in _histories.Values)
        {
            if (history.StartedAt < cutoffDate)
            {
                _histories.TryRemove(history.HistoryId, out _);
            }
        }

        return Task.CompletedTask;
    }

    private void TrackCompletedInstance(string instanceId)
    {
        if (_completedInstanceIds.TryAdd(instanceId, 0))
        {
            _completedInstanceOrder.Enqueue(instanceId);
        }

        while (_completedInstanceIds.Count > MaxCompletedInstanceCount && _completedInstanceOrder.TryDequeue(out var oldestId))
        {
            if (_completedInstanceIds.TryRemove(oldestId, out _))
            {
                _instances.TryRemove(oldestId, out _);
            }
        }
    }
}
