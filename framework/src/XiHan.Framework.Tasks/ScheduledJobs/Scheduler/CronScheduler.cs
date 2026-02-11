#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CronScheduler
// Guid:f9fcde39-b447-433b-88b8-9ecb9ab85d70
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 16:13:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Tasks.ScheduledJobs.Crons;
using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Scheduler;

/// <summary>
/// Cron 调度器
/// </summary>
public class CronScheduler
{
    /// <summary>
    /// 获取下次执行时间
    /// </summary>
    public static DateTimeOffset? GetNextFireTime(string cronExpression, DateTimeOffset? from = null)
    {
        try
        {
            var fromTime = from ?? DateTimeOffset.UtcNow;
            var nextTime = CronHelper.GetNextOccurrence(cronExpression, fromTime.DateTime);
            return nextTime.HasValue ? new DateTimeOffset(nextTime.Value, TimeSpan.Zero) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 验证 Cron 表达式是否有效
    /// </summary>
    public static bool IsValidExpression(string cronExpression)
    {
        return CronHelper.IsValidExpression(cronExpression);
    }

    /// <summary>
    /// 判断是否应该触发
    /// </summary>
    public static bool ShouldFire(JobInfo jobInfo, DateTimeOffset lastFireTime)
    {
        if (string.IsNullOrWhiteSpace(jobInfo.CronExpression))
        {
            return false;
        }

        var nextFireTime = GetNextFireTime(jobInfo.CronExpression, lastFireTime);
        return nextFireTime.HasValue && nextFireTime.Value <= DateTimeOffset.UtcNow;
    }
}
