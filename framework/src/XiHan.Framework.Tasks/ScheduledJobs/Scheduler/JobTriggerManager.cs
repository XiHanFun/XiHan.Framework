#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JobTriggerManager
// Guid:0553b225-5b98-4135-b99a-70dadba21054
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 15:02:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;

namespace XiHan.Framework.Tasks.ScheduledJobs.Scheduler;

/// <summary>
/// 任务触发管理器
/// </summary>
public class JobTriggerManager
{
    private readonly ConcurrentDictionary<string, JobTriggerState> _triggerStates = new();

    /// <summary>
    /// 记录触发
    /// </summary>
    public void RecordTrigger(string jobName, DateTimeOffset fireTime)
    {
        _triggerStates.AddOrUpdate(
            jobName,
            _ => new JobTriggerState
            {
                JobName = jobName,
                LastFireTime = fireTime,
                NextFireTime = null,
                TriggerCount = 1,
                IsPaused = false
            },
            (_, state) =>
            {
                state.LastFireTime = fireTime;
                state.TriggerCount++;
                return state;
            });
    }

    /// <summary>
    /// 更新下次触发时间
    /// </summary>
    public void UpdateNextFireTime(string jobName, DateTimeOffset? nextFireTime)
    {
        _triggerStates.AddOrUpdate(
            jobName,
            _ => new JobTriggerState
            {
                JobName = jobName,
                NextFireTime = nextFireTime,
                IsPaused = false
            },
            (_, state) =>
            {
                state.NextFireTime = nextFireTime;
                return state;
            });
    }

    /// <summary>
    /// 暂停任务
    /// </summary>
    public void PauseJob(string jobName)
    {
        if (_triggerStates.TryGetValue(jobName, out var state))
        {
            state.IsPaused = true;
        }
    }

    /// <summary>
    /// 恢复任务
    /// </summary>
    public void ResumeJob(string jobName)
    {
        if (_triggerStates.TryGetValue(jobName, out var state))
        {
            state.IsPaused = false;
        }
    }

    /// <summary>
    /// 获取触发状态
    /// </summary>
    public JobTriggerState? GetTriggerState(string jobName)
    {
        return _triggerStates.TryGetValue(jobName, out var state) ? state : null;
    }

    /// <summary>
    /// 获取所有触发状态
    /// </summary>
    public IReadOnlyDictionary<string, JobTriggerState> GetAllTriggerStates()
    {
        return _triggerStates;
    }

    /// <summary>
    /// 移除触发状态
    /// </summary>
    public void RemoveTriggerState(string jobName)
    {
        _triggerStates.TryRemove(jobName, out _);
    }
}

/// <summary>
/// 任务触发状态
/// </summary>
public class JobTriggerState
{
    /// <summary>
    /// 任务名称
    /// </summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// 最后触发时间
    /// </summary>
    public DateTimeOffset? LastFireTime { get; set; }

    /// <summary>
    /// 下次触发时间
    /// </summary>
    public DateTimeOffset? NextFireTime { get; set; }

    /// <summary>
    /// 触发次数
    /// </summary>
    public long TriggerCount { get; set; }

    /// <summary>
    /// 是否暂停
    /// </summary>
    public bool IsPaused { get; set; }
}
