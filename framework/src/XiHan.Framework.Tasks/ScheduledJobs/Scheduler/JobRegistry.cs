#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JobRegistry
// Guid:970f2bac-e482-4859-a210-13e5fde92c15
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 15:59:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Scheduler;

/// <summary>
/// 任务注册表
/// </summary>
public class JobRegistry
{
    private readonly ConcurrentDictionary<string, JobInfo> _jobs = new();

    /// <summary>
    /// 获取任务数量
    /// </summary>
    public int Count => _jobs.Count;

    /// <summary>
    /// 注册任务
    /// </summary>
    public void Register(JobInfo jobInfo)
    {
        ArgumentNullException.ThrowIfNull(jobInfo);

        if (string.IsNullOrWhiteSpace(jobInfo.JobName))
        {
            throw new ArgumentException("任务名称不能为空", nameof(jobInfo));
        }

        _jobs.AddOrUpdate(jobInfo.JobName, jobInfo, (_, _) => jobInfo);
    }

    /// <summary>
    /// 取消注册任务
    /// </summary>
    public bool Unregister(string jobName)
    {
        return _jobs.TryRemove(jobName, out _);
    }

    /// <summary>
    /// 获取任务信息
    /// </summary>
    public JobInfo? GetJob(string jobName)
    {
        return _jobs.TryGetValue(jobName, out var jobInfo) ? jobInfo : null;
    }

    /// <summary>
    /// 获取所有任务
    /// </summary>
    public IReadOnlyList<JobInfo> GetAllJobs()
    {
        return [.. _jobs.Values];
    }

    /// <summary>
    /// 任务是否存在
    /// </summary>
    public bool Exists(string jobName)
    {
        return _jobs.ContainsKey(jobName);
    }
}
