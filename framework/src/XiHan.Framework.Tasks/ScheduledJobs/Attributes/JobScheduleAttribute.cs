#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JobScheduleAttribute
// Guid:6e0ac153-a853-42ee-86c0-576a9a9581ee
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 15:46:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Attributes;

/// <summary>
/// 任务调度特性
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class JobScheduleAttribute : Attribute
{
    /// <summary>
    /// 构造函数（Cron）
    /// </summary>
    /// <param name="cronExpression">Cron 表达式</param>
    public JobScheduleAttribute(string cronExpression)
    {
        TriggerType = JobTriggerType.Cron;
        CronExpression = cronExpression;
    }

    /// <summary>
    /// 构造函数（Interval）
    /// </summary>
    /// <param name="intervalSeconds">间隔时间（秒）</param>
    public JobScheduleAttribute(int intervalSeconds)
    {
        TriggerType = JobTriggerType.Interval;
        IntervalSeconds = intervalSeconds;
    }

    /// <summary>
    /// 构造函数（手动触发）
    /// </summary>
    public JobScheduleAttribute()
    {
        TriggerType = JobTriggerType.Manual;
    }

    /// <summary>
    /// 触发类型
    /// </summary>
    public JobTriggerType TriggerType { get; }

    /// <summary>
    /// Cron 表达式
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// 间隔时间（秒）
    /// </summary>
    public int IntervalSeconds { get; set; }

    /// <summary>
    /// 延迟时间（秒）
    /// </summary>
    public int DelaySeconds { get; set; }
}
