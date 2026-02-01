#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CronExpressionBuilder
// Guid:bb2648fc-916f-4c83-aa4c-a94bafe7763f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/06 22:17:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Tasks.ScheduledJobs.Crons;

/// <summary>
/// Cron 表达式构建器
/// </summary>
public class CronExpressionBuilder
{
    private string _seconds = "*";
    private string _minutes = "*";
    private string _hours = "*";
    private string _days = "*";
    private string _months = "*";
    private string _daysOfWeek = "*";
    private bool _includeSeconds;

    /// <summary>
    /// 创建新的构建器实例
    /// </summary>
    /// <returns>构建器实例</returns>
    public static CronExpressionBuilder Create()
    {
        return new CronExpressionBuilder();
    }

    /// <summary>
    /// 设置秒
    /// </summary>
    /// <param name="seconds">秒值</param>
    /// <returns>构建器实例</returns>
    public CronExpressionBuilder Seconds(string seconds)
    {
        _seconds = seconds;
        _includeSeconds = true;
        return this;
    }

    /// <summary>
    /// 设置分钟
    /// </summary>
    /// <param name="minutes">分钟值</param>
    /// <returns>构建器实例</returns>
    public CronExpressionBuilder Minutes(string minutes)
    {
        _minutes = minutes;
        return this;
    }

    /// <summary>
    /// 设置小时
    /// </summary>
    /// <param name="hours">小时值</param>
    /// <returns>构建器实例</returns>
    public CronExpressionBuilder Hours(string hours)
    {
        _hours = hours;
        return this;
    }

    /// <summary>
    /// 设置日期
    /// </summary>
    /// <param name="days">日期值</param>
    /// <returns>构建器实例</returns>
    public CronExpressionBuilder Days(string days)
    {
        _days = days;
        return this;
    }

    /// <summary>
    /// 设置月份
    /// </summary>
    /// <param name="months">月份值</param>
    /// <returns>构建器实例</returns>
    public CronExpressionBuilder Months(string months)
    {
        _months = months;
        return this;
    }

    /// <summary>
    /// 设置星期
    /// </summary>
    /// <param name="daysOfWeek">星期值</param>
    /// <returns>构建器实例</returns>
    public CronExpressionBuilder DaysOfWeek(string daysOfWeek)
    {
        _daysOfWeek = daysOfWeek;
        return this;
    }

    /// <summary>
    /// 构建 Cron 表达式
    /// </summary>
    /// <returns>Cron 表达式字符串</returns>
    public string Build()
    {
        return _includeSeconds
            ? $"{_seconds} {_minutes} {_hours} {_days} {_months} {_daysOfWeek}"
            : $"{_minutes} {_hours} {_days} {_months} {_daysOfWeek}";
    }
}
