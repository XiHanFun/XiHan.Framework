// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
    /// <remarks>
    /// 原实现把 TriggerCount++ 写在 AddOrUpdate 的 updateValueFactory 里。该委托在竞争下可能被重复
    /// 执行（ConcurrentDictionary 不保证只调一次），且 ++ 本身也不是原子操作；调度定时器回调
    /// （CheckAndFireJobs → ExecuteJobAsync）与手动 TriggerJobAsync 会同时进入这条路径，于是计数
    /// 时多时少，CompositeJobScheduler.ShouldFire 里的 RepeatCount 上限判断随之失准（任务被多触发）。
    /// 改为先 GetOrAdd 取到唯一的状态对象（工厂无副作用，重复执行也安全），再锁住该对象写入；
    /// 锁粒度与写法与 JobMetricsProvider.RecordExecution 保持一致。
    /// </remarks>
    public void RecordTrigger(string jobName, DateTimeOffset fireTime)
    {
        var state = GetOrAddState(jobName);

        lock (state)
        {
            state.LastFireTime = fireTime;
            state.TriggerCount++;
        }
    }

    /// <summary>
    /// 更新下次触发时间
    /// </summary>
    public void UpdateNextFireTime(string jobName, DateTimeOffset? nextFireTime)
    {
        var state = GetOrAddState(jobName);

        lock (state)
        {
            state.NextFireTime = nextFireTime;
        }
    }

    /// <summary>
    /// 暂停任务
    /// </summary>
    public void PauseJob(string jobName)
    {
        if (_triggerStates.TryGetValue(jobName, out var state))
        {
            lock (state)
            {
                state.IsPaused = true;
            }
        }
    }

    /// <summary>
    /// 恢复任务
    /// </summary>
    public void ResumeJob(string jobName)
    {
        if (_triggerStates.TryGetValue(jobName, out var state))
        {
            lock (state)
            {
                state.IsPaused = false;
            }
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

    /// <summary>
    /// 取到任务对应的触发状态，没有则新建一个空状态
    /// </summary>
    /// <remarks>
    /// 工厂不带任何副作用，因此即便在竞争下被重复调用也不会多算；所有调用方拿到的都是字典里
    /// 最终留存的那一个实例，可以安全地作为写入锁的锁对象。
    /// </remarks>
    private JobTriggerState GetOrAddState(string jobName)
    {
        return _triggerStates.GetOrAdd(jobName, name => new JobTriggerState { JobName = name });
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
