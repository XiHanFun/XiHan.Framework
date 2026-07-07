#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:InMemoryBackgroundJobStore
// Guid:15cc5d55-930c-4f89-923b-f7ad7305d62e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/07 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Models;
using XiHan.Framework.Timing;

namespace XiHan.Framework.Tasks.BackgroundJobs;

/// <summary>
/// 进程内内存后台作业存储（默认实现，单实例）
/// </summary>
/// <remarks>
/// 进程重启后作业丢失、且不跨实例。要持久化与跨实例可靠投递，请实现 <see cref="IBackgroundJobStore"/>
/// （基于数据库 / Redis）并在 DI 中替换本默认实现；其 GetWaitingJobsAsync 须做原子领取。
/// </remarks>
public class InMemoryBackgroundJobStore : IBackgroundJobStore
{
    private readonly ConcurrentDictionary<Guid, BackgroundJobInfo> _jobs = new();
    private readonly IClock _clock;

    /// <summary>
    /// 构造函数
    /// </summary>
    public InMemoryBackgroundJobStore(IClock clock)
    {
        _clock = clock;
    }

    /// <summary>
    /// 按标识查找作业
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
        _jobs[jobInfo.Id] = jobInfo;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取待执行作业（过滤 + 排序 + 限量，契约见接口）
    /// </summary>
    public Task<List<BackgroundJobInfo>> GetWaitingJobsAsync(string? applicationName, int maxResultCount)
    {
        var now = _clock.Now;

        var result = _jobs.Values
            .Where(x => string.Equals(x.ApplicationName, applicationName, StringComparison.Ordinal))
            .Where(x => !x.IsAbandoned && x.NextTryTime <= now)
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.TryCount)
            .ThenBy(x => x.NextTryTime)
            .Take(maxResultCount)
            .ToList();

        return Task.FromResult(result);
    }

    /// <summary>
    /// 删除作业
    /// </summary>
    public Task DeleteAsync(Guid jobId)
    {
        _jobs.TryRemove(jobId, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 更新作业（内存实现：对象为同一引用，字段已就地更新，此处仅确保写回）
    /// </summary>
    public Task UpdateAsync(BackgroundJobInfo jobInfo)
    {
        ArgumentNullException.ThrowIfNull(jobInfo);
        _jobs[jobInfo.Id] = jobInfo;
        return Task.CompletedTask;
    }
}
