// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;

namespace XiHan.Framework.Tasks.BackgroundServices;

/// <summary>
/// 后台服务统计信息
/// </summary>
public class BackgroundServiceStatistics
{
    /// <summary>
    /// 处理时间样本上限
    /// </summary>
    private const int MaxProcessingTimeSamples = 1000;

    private readonly Lock _lock = new();
    private readonly ConcurrentDictionary<string, long> _taskProcessingTimes = new();

    // 记录处理时间样本的插入次序，专供溢出淘汰用：ConcurrentDictionary 的键枚举顺序与插入顺序无关，
    // 只靠字典无法回答"哪一条最旧"
    private readonly ConcurrentQueue<string> _taskProcessingOrder = new();

    private long _totalTasksProcessed;
    private long _totalTasksFailed;
    private long _totalTasksRetried;
    private int _currentRunningTasks;

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
    /// <remarks>
    /// 原先是自动属性，RecordTaskStarted / RecordTaskCompleted 直接 ++ / --。这两个方法是从
    /// XiHanBackgroundServiceBase.ProcessItemWithRetryAsync 的并发 Task.Run 任务体里调用的，
    /// 而 ++ / -- 是读-改-写三步，并发下会丢失更新，运行中任务数长期偏差甚至变负。
    /// 同类的 _totalTasksProcessed / _totalTasksFailed 早就用了 Interlocked，唯独漏了这个字段，
    /// 这里补齐：字段改为私有 int + Interlocked 增减，属性只读取，公开形状不变。
    /// </remarks>
    public int CurrentRunningTasks => Volatile.Read(ref _currentRunningTasks);

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
        Interlocked.Increment(ref _currentRunningTasks);
        LastActivityTime = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// 记录任务完成
    /// </summary>
    /// <param name="taskId">任务唯一标识</param>
    /// <param name="processingTimeMs">处理时间（毫秒）</param>
    /// <param name="success">是否成功</param>
    public void RecordTaskCompleted(string taskId, long processingTimeMs, bool success)
    {
        Interlocked.Decrement(ref _currentRunningTasks);
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

            // 保持最近 1000 个任务的处理时间记录
            // 原先用 _taskProcessingTimes.Keys.FirstOrDefault() 当"最旧的一项"：ConcurrentDictionary
            // 的键枚举顺序由内部分桶决定，与插入顺序无关，实际淘汰的是任意一项，"最近 1000 个"这个
            // 口径根本不成立——平均处理时间会掺进任意时间窗口的旧样本。改为用一条 FIFO 队列显式记录
            // 插入次序，溢出时按插入次序淘汰真正最旧的那条。
            // 只有 TryAdd 成功才入队：同一 taskId 重复上报时字典按首次记录为准（既有契约），
            // 若照样入队会让队列里出现重复键，把仍在用的样本提前淘汰掉。
            if (_taskProcessingTimes.TryAdd(taskId, processingTimeMs))
            {
                _taskProcessingOrder.Enqueue(taskId);
            }

            while (_taskProcessingTimes.Count > MaxProcessingTimeSamples && _taskProcessingOrder.TryDequeue(out var oldestKey))
            {
                _taskProcessingTimes.TryRemove(oldestKey, out _);
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
            Interlocked.Exchange(ref _currentRunningTasks, 0);
            _taskProcessingTimes.Clear();
            _taskProcessingOrder.Clear();
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
