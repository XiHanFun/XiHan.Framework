#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IntervalScheduler
// Guid:55eefa6e-00a5-4f72-91c0-79e21a066c37
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 16:21:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Scheduler;

/// <summary>
/// 固定间隔调度器
/// </summary>
public class IntervalScheduler
{
    /// <summary>
    /// 获取下次执行时间
    /// </summary>
    public static DateTimeOffset? GetNextFireTime(TimeSpan interval, DateTimeOffset? from = null)
    {
        if (interval <= TimeSpan.Zero)
        {
            return null;
        }

        var fromTime = from ?? DateTimeOffset.UtcNow;
        return fromTime.Add(interval);
    }

    /// <summary>
    /// 判断是否应该触发
    /// </summary>
    public static bool ShouldFire(JobInfo jobInfo, DateTimeOffset lastFireTime)
    {
        if (!jobInfo.Interval.HasValue || jobInfo.Interval.Value <= TimeSpan.Zero)
        {
            return false;
        }

        var nextFireTime = GetNextFireTime(jobInfo.Interval.Value, lastFireTime);
        return nextFireTime.HasValue && nextFireTime.Value <= DateTimeOffset.UtcNow;
    }
}
