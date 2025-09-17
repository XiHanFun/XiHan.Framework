#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BackgroundServiceStatistics
// Guid:${guid}
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/17 16:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;

namespace XiHan.Framework.Tasks.BackgroundServices;

/// <summary>
/// 后台服务统计信息
/// </summary>
public class BackgroundServiceStatistics
{
    private readonly Lock _lock = new();
    private readonly ConcurrentDictionary<string, long> _taskProcessingTimes = new();
    private long _totalTasksProcessed;
    private long _totalTasksFailed;
    private long _totalTasksRetried;

    /// <summary>
    /// 服务启动时间
    /// </summary>
    public DateTimeOffset StartTime { get; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 最后活动时间
    /// </summary>
    public DateTimeOffset LastActivityTime { get; private set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 当前运行的任务数
    /// </summary>
    public int CurrentRunningTasks { get; private set; }

    /// <summary>
    /// 总处理任务数
    /// </summary>
    public long TotalTasksProcessed => _totalTasksProcessed;

    /// <summary>
    /// 总失败任务数
    /// </summary>
    public long TotalTasksFailed => _totalTasksFailed;

    /// <summary>
    /// 总重试任务数
    /// </summary>
    public long TotalTasksRetried => _totalTasksRetried;

    /// <summary>
    /// 平均任务处理时间（毫秒）
    /// </summary>
    public double AverageProcessingTimeMs
    {
        get
        {
            lock (_lock)
            {
                if (_taskProcessingTimes.IsEmpty)
                {
                    return 0;
                }

                return _taskProcessingTimes.Values.Average();
            }
        }
    }

    /// <summary>
    /// 任务成功率
    /// </summary>
    public double SuccessRate
    {
        get
        {
            var total = _totalTasksProcessed + _totalTasksFailed;
            return total == 0 ? 0.0 : (double)_totalTasksProcessed / total * 100;
        }
    }

    /// <summary>
    /// 运行时长
    /// </summary>
    public TimeSpan Uptime => DateTimeOffset.UtcNow - StartTime;

    /// <summary>
    /// 记录任务开始
    /// </summary>
    public void RecordTaskStarted()
    {
        CurrentRunningTasks++;
        LastActivityTime = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// 记录任务完成
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <param name="processingTimeMs">处理时间（毫秒）</param>
    /// <param name="success">是否成功</param>
    public void RecordTaskCompleted(string taskId, long processingTimeMs, bool success)
    {
        CurrentRunningTasks--;
        LastActivityTime = DateTimeOffset.UtcNow;

        lock (_lock)
        {
            if (success)
            {
                Interlocked.Increment(ref _totalTasksProcessed);
            }
            else
            {
                Interlocked.Increment(ref _totalTasksFailed);
            }

            // 保持最近1000个任务的处理时间记录
            _taskProcessingTimes.TryAdd(taskId, processingTimeMs);
            if (_taskProcessingTimes.Count > 1000)
            {
                var oldestKey = _taskProcessingTimes.Keys.FirstOrDefault();
                if (oldestKey != null)
                {
                    _taskProcessingTimes.TryRemove(oldestKey, out _);
                }
            }
        }
    }

    /// <summary>
    /// 记录任务重试
    /// </summary>
    public void RecordTaskRetried()
    {
        Interlocked.Increment(ref _totalTasksRetried);
        LastActivityTime = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// 重置统计信息
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _totalTasksProcessed = 0;
            _totalTasksFailed = 0;
            _totalTasksRetried = 0;
            CurrentRunningTasks = 0;
            _taskProcessingTimes.Clear();
            LastActivityTime = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// 获取统计信息摘要
    /// </summary>
    /// <returns>统计信息</returns>
    public StatisticsSummary GetSummary()
    {
        return new StatisticsSummary
        {
            StartTime = StartTime,
            LastActivityTime = LastActivityTime,
            Uptime = Uptime,
            CurrentRunningTasks = CurrentRunningTasks,
            TotalTasksProcessed = TotalTasksProcessed,
            TotalTasksFailed = TotalTasksFailed,
            TotalTasksRetried = TotalTasksRetried,
            AverageProcessingTimeMs = AverageProcessingTimeMs,
            SuccessRate = SuccessRate
        };
    }
}

/// <summary>
/// 统计信息摘要
/// </summary>
public class StatisticsSummary
{
    /// <summary>
    /// 启动时间
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// 最后活动时间
    /// </summary>
    public DateTimeOffset LastActivityTime { get; set; }

    /// <summary>
    /// 运行时长
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// 当前运行任务数
    /// </summary>
    public int CurrentRunningTasks { get; set; }

    /// <summary>
    /// 总处理任务数
    /// </summary>
    public long TotalTasksProcessed { get; set; }

    /// <summary>
    /// 总失败任务数
    /// </summary>
    public long TotalTasksFailed { get; set; }

    /// <summary>
    /// 总重试任务数
    /// </summary>
    public long TotalTasksRetried { get; set; }

    /// <summary>
    /// 平均处理时间（毫秒）
    /// </summary>
    public double AverageProcessingTimeMs { get; set; }

    /// <summary>
    /// 成功率（百分比）
    /// </summary>
    public double SuccessRate { get; set; }
}
