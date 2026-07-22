// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
    /// <remarks>
    /// Cron 按服务器本地时区求值（"0 2 * * *"=本地 02:00），与业务侧直觉及
    /// 领域层的 LocalDateTime 口径一致；返回值携带本地偏移，调度器内部与
    /// UtcNow 的比较是绝对时刻比较，不受时区影响。
    /// </remarks>
    public static DateTimeOffset? GetNextFireTime(string cronExpression, DateTimeOffset? from = null)
    {
        try
        {
            var fromLocal = (from ?? DateTimeOffset.Now).ToLocalTime().DateTime;
            var nextTime = CronHelper.GetNextOccurrence(cronExpression, fromLocal);
            return nextTime.HasValue
                ? new DateTimeOffset(nextTime.Value, TimeZoneInfo.Local.GetUtcOffset(nextTime.Value))
                : null;
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
