#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CronExpression
// Guid:f11e77d1-bf34-466e-860f-2b626d28fbf0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/06 22:16:23
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Tasks.ScheduledJobs.Crons;

/// <summary>
/// Cron 表达式对象
/// </summary>
public class CronExpression
{
    /// <summary>
    /// 是否包含秒字段
    /// </summary>
    public bool HasSeconds { get; set; }

    /// <summary>
    /// 秒字段
    /// </summary>
    public CronField Seconds { get; set; } = new();

    /// <summary>
    /// 分钟字段
    /// </summary>
    public CronField Minutes { get; set; } = new();

    /// <summary>
    /// 小时字段
    /// </summary>
    public CronField Hours { get; set; } = new();

    /// <summary>
    /// 日期字段
    /// </summary>
    public CronField Days { get; set; } = new();

    /// <summary>
    /// 月份字段
    /// </summary>
    public CronField Months { get; set; } = new();

    /// <summary>
    /// 星期字段
    /// </summary>
    public CronField DaysOfWeek { get; set; } = new();

    /// <summary>
    /// 转换为字符串表示
    /// </summary>
    /// <returns>格式化的 Cron 表达式</returns>
    public override string ToString()
    {
        return CronHelper.FormatExpression(
            CronHelper.FormatExpression(HasSeconds
            ? $"{FormatField(Seconds)} {FormatField(Minutes)} {FormatField(Hours)} {FormatField(Days)} {FormatField(Months)} {FormatField(DaysOfWeek)}"
            : $"{FormatField(Minutes)} {FormatField(Hours)} {FormatField(Days)} {FormatField(Months)} {FormatField(DaysOfWeek)}"));
    }

    /// <summary>
    /// 格式化字段
    /// </summary>
    private static string FormatField(CronField field)
    {
        if (field.IsWildcard)
        {
            return "*";
        }

        return string.Join(",", field.Values);
    }
}
