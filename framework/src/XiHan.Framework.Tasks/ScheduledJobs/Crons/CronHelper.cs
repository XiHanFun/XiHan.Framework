#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CronHelper
// Guid:f5e4d3c2-b1a0-9876-5432-10fedcba9876
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/19 11:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Tasks.ScheduledJobs.Crons;

/// <summary>
/// Cron 表达式辅助工具类
/// </summary>
/// <remarks>
/// 提供 Cron 表达式的解析、验证、下次执行时间计算等功能。
/// 支持标准的 5 位格式（分 时 日 月 周）和 6 位格式（秒 分 时 日 月 周）。
/// 支持特殊符号：* - , / ? L W # 等。
/// </remarks>
public static class CronHelper
{
    #region 常量定义

    /// <summary>
    /// 预定义的 Cron 表达式
    /// </summary>
    private static readonly Dictionary<string, string> PredefinedExpressions = new()
    {
        { "@yearly", "0 0 1 1 *" },
        { "@annually", "0 0 1 1 *" },
        { "@monthly", "0 0 1 * *" },
        { "@weekly", "0 0 * * 0" },
        { "@daily", "0 0 * * *" },
        { "@midnight", "0 0 * * *" },
        { "@hourly", "0 * * * *" }
    };

    /// <summary>
    /// 月份名称映射
    /// </summary>
    private static readonly Dictionary<string, int> MonthNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "JAN", 1 }, { "FEB", 2 }, { "MAR", 3 }, { "APR", 4 }, { "MAY", 5 }, { "JUN", 6 },
        { "JUL", 7 }, { "AUG", 8 }, { "SEP", 9 }, { "OCT", 10 }, { "NOV", 11 }, { "DEC", 12 }
    };

    /// <summary>
    /// 星期名称映射
    /// </summary>
    private static readonly Dictionary<string, int> DayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "SUN", 0 }, { "MON", 1 }, { "TUE", 2 }, { "WED", 3 }, { "THU", 4 }, { "FRI", 5 }, { "SAT", 6 }
    };

    #endregion

    #region 公开方法 - 表达式验证

    /// <summary>
    /// 验证 Cron 表达式是否有效
    /// </summary>
    /// <param name="cronExpression">Cron 表达式</param>
    /// <returns>是否有效</returns>
    public static bool IsValidExpression(string cronExpression)
    {
        try
        {
            ParseExpression(cronExpression);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 解析 Cron 表达式
    /// </summary>
    /// <param name="cronExpression">Cron 表达式</param>
    /// <returns>解析后的 Cron 对象</returns>
    /// <exception cref="ArgumentException">表达式格式无效时抛出</exception>
    public static CronExpression ParseExpression(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            throw new ArgumentException("Cron 表达式不能为空", nameof(cronExpression));
        }

        cronExpression = cronExpression.Trim();

        // 处理预定义表达式
        if (PredefinedExpressions.TryGetValue(cronExpression.ToLowerInvariant(), out var predefined))
        {
            cronExpression = predefined;
        }

        var parts = cronExpression.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

        return parts.Length switch
        {
            5 => ParseFivePartExpression(parts),
            6 => ParseSixPartExpression(parts),
            _ => throw new ArgumentException($"Cron 表达式必须包含 5 或 6 个部分，当前包含 {parts.Length} 个部分")
        };
    }

    #endregion

    #region 公开方法 - 时间计算

    /// <summary>
    /// 计算下一次执行时间
    /// </summary>
    /// <param name="cronExpression">Cron 表达式</param>
    /// <param name="fromTime">起始时间，默认为当前时间</param>
    /// <returns>下一次执行时间</returns>
    public static DateTime? GetNextOccurrence(string cronExpression, DateTime? fromTime = null)
    {
        var cron = ParseExpression(cronExpression);
        return GetNextOccurrence(cron, fromTime ?? DateTime.Now);
    }

    /// <summary>
    /// 计算下一次执行时间
    /// </summary>
    /// <param name="cron">解析后的 Cron 表达式</param>
    /// <param name="fromTime">起始时间</param>
    /// <returns>下一次执行时间</returns>
    public static DateTime? GetNextOccurrence(CronExpression cron, DateTime fromTime)
    {
        var current = new DateTime(fromTime.Year, fromTime.Month, fromTime.Day, fromTime.Hour, fromTime.Minute, 0);
        current = current.AddMinutes(1); // 从下一分钟开始查找

        for (var attempts = 0; attempts < 4 * 365 * 24 * 60; attempts++) // 最多查找4年
        {
            if (IsMatch(cron, current))
            {
                return current;
            }
            current = current.AddMinutes(1);
        }

        return null; // 未找到匹配时间
    }

    /// <summary>
    /// 计算上一次执行时间
    /// </summary>
    /// <param name="cronExpression">Cron 表达式</param>
    /// <param name="fromTime">起始时间，默认为当前时间</param>
    /// <returns>上一次执行时间</returns>
    public static DateTime? GetPreviousOccurrence(string cronExpression, DateTime? fromTime = null)
    {
        var cron = ParseExpression(cronExpression);
        return GetPreviousOccurrence(cron, fromTime ?? DateTime.Now);
    }

    /// <summary>
    /// 计算上一次执行时间
    /// </summary>
    /// <param name="cron">解析后的 Cron 表达式</param>
    /// <param name="fromTime">起始时间</param>
    /// <returns>上一次执行时间</returns>
    public static DateTime? GetPreviousOccurrence(CronExpression cron, DateTime fromTime)
    {
        var current = new DateTime(fromTime.Year, fromTime.Month, fromTime.Day, fromTime.Hour, fromTime.Minute, 0);
        current = current.AddMinutes(-1); // 从上一分钟开始查找

        for (var attempts = 0; attempts < 4 * 365 * 24 * 60; attempts++) // 最多查找4年
        {
            if (IsMatch(cron, current))
            {
                return current;
            }
            current = current.AddMinutes(-1);
        }

        return null; // 未找到匹配时间
    }

    /// <summary>
    /// 获取未来 N 次执行时间
    /// </summary>
    /// <param name="cronExpression">Cron 表达式</param>
    /// <param name="count">获取次数</param>
    /// <param name="fromTime">起始时间，默认为当前时间</param>
    /// <returns>执行时间列表</returns>
    public static List<DateTime> GetNextOccurrences(string cronExpression, int count, DateTime? fromTime = null)
    {
        var cron = ParseExpression(cronExpression);
        return GetNextOccurrences(cron, count, fromTime ?? DateTime.Now);
    }

    /// <summary>
    /// 获取未来 N 次执行时间
    /// </summary>
    /// <param name="cron">解析后的 Cron 表达式</param>
    /// <param name="count">获取次数</param>
    /// <param name="fromTime">起始时间</param>
    /// <returns>执行时间列表</returns>
    public static List<DateTime> GetNextOccurrences(CronExpression cron, int count, DateTime fromTime)
    {
        var results = new List<DateTime>();
        var current = fromTime;

        for (var i = 0; i < count; i++)
        {
            var next = GetNextOccurrence(cron, current);
            if (next == null)
            {
                break;
            }
            results.Add(next.Value);
            current = next.Value;
        }

        return results;
    }

    /// <summary>
    /// 判断指定时间是否匹配 Cron 表达式
    /// </summary>
    /// <param name="cronExpression">Cron 表达式</param>
    /// <param name="dateTime">要检查的时间</param>
    /// <returns>是否匹配</returns>
    public static bool IsMatch(string cronExpression, DateTime dateTime)
    {
        var cron = ParseExpression(cronExpression);
        return IsMatch(cron, dateTime);
    }

    /// <summary>
    /// 判断指定时间是否匹配 Cron 表达式
    /// </summary>
    /// <param name="cron">解析后的 Cron 表达式</param>
    /// <param name="dateTime">要检查的时间</param>
    /// <returns>是否匹配</returns>
    public static bool IsMatch(CronExpression cron, DateTime dateTime)
    {
        // 检查秒（如果有）
        if (cron.HasSeconds && !IsFieldMatch(cron.Seconds, dateTime.Second, 0, 59))
        {
            return false;
        }

        // 检查分钟
        if (!IsFieldMatch(cron.Minutes, dateTime.Minute, 0, 59))
        {
            return false;
        }

        // 检查小时
        if (!IsFieldMatch(cron.Hours, dateTime.Hour, 0, 23))
        {
            return false;
        }

        // 检查日期
        if (!IsFieldMatch(cron.Days, dateTime.Day, 1, DateTime.DaysInMonth(dateTime.Year, dateTime.Month)))
        {
            return false;
        }

        // 检查月份
        if (!IsFieldMatch(cron.Months, dateTime.Month, 1, 12))
        {
            return false;
        }

        // 检查星期
        var dayOfWeek = (int)dateTime.DayOfWeek;
        if (!IsFieldMatch(cron.DaysOfWeek, dayOfWeek, 0, 6))
        {
            return false;
        }

        return true;
    }

    #endregion

    #region 公开方法 - 工具方法

    /// <summary>
    /// 获取 Cron 表达式的描述
    /// </summary>
    /// <param name="cronExpression">Cron 表达式</param>
    /// <returns>可读的描述</returns>
    public static string GetDescription(string cronExpression)
    {
        try
        {
            var cron = ParseExpression(cronExpression);
            return GenerateDescription(cron);
        }
        catch
        {
            return "无效的 Cron 表达式";
        }
    }

    /// <summary>
    /// 格式化 Cron 表达式
    /// </summary>
    /// <param name="cronExpression">Cron 表达式</param>
    /// <returns>格式化后的表达式</returns>
    public static string FormatExpression(string cronExpression)
    {
        var cron = ParseExpression(cronExpression);
        var parts = new List<string>();

        if (cron.HasSeconds)
        {
            parts.Add(FormatField(cron.Seconds));
        }

        parts.Add(FormatField(cron.Minutes));
        parts.Add(FormatField(cron.Hours));
        parts.Add(FormatField(cron.Days));
        parts.Add(FormatField(cron.Months));
        parts.Add(FormatField(cron.DaysOfWeek));

        return string.Join(" ", parts);
    }

    /// <summary>
    /// 获取所有预定义表达式
    /// </summary>
    /// <returns>预定义表达式字典</returns>
    public static Dictionary<string, string> GetPredefinedExpressions()
    {
        return new Dictionary<string, string>(PredefinedExpressions);
    }

    /// <summary>
    /// 创建简单的 Cron 表达式
    /// </summary>
    /// <param name="minute">分钟</param>
    /// <param name="hour">小时</param>
    /// <param name="day">日期</param>
    /// <param name="month">月份</param>
    /// <param name="dayOfWeek">星期</param>
    /// <returns>Cron 表达式字符串</returns>
    public static string CreateExpression(string minute = "*", string hour = "*", string day = "*", string month = "*", string dayOfWeek = "*")
    {
        return $"{minute} {hour} {day} {month} {dayOfWeek}";
    }

    /// <summary>
    /// 创建带秒的 Cron 表达式
    /// </summary>
    /// <param name="second">秒</param>
    /// <param name="minute">分钟</param>
    /// <param name="hour">小时</param>
    /// <param name="day">日期</param>
    /// <param name="month">月份</param>
    /// <param name="dayOfWeek">星期</param>
    /// <returns>Cron 表达式字符串</returns>
    public static string CreateExpressionWithSeconds(string second = "*", string minute = "*", string hour = "*", string day = "*", string month = "*", string dayOfWeek = "*")
    {
        return $"{second} {minute} {hour} {day} {month} {dayOfWeek}";
    }

    #endregion

    #region 私有方法 - 表达式解析

    /// <summary>
    /// 解析 5 部分的 Cron 表达式（分 时 日 月 周）
    /// </summary>
    private static CronExpression ParseFivePartExpression(string[] parts)
    {
        return new CronExpression
        {
            HasSeconds = false,
            Minutes = ParseField(parts[0], 0, 59),
            Hours = ParseField(parts[1], 0, 23),
            Days = ParseField(parts[2], 1, 31),
            Months = ParseField(parts[3], 1, 12),
            DaysOfWeek = ParseField(parts[4], 0, 6)
        };
    }

    /// <summary>
    /// 解析 6 部分的 Cron 表达式（秒 分 时 日 月 周）
    /// </summary>
    private static CronExpression ParseSixPartExpression(string[] parts)
    {
        return new CronExpression
        {
            HasSeconds = true,
            Seconds = ParseField(parts[0], 0, 59),
            Minutes = ParseField(parts[1], 0, 59),
            Hours = ParseField(parts[2], 0, 23),
            Days = ParseField(parts[3], 1, 31),
            Months = ParseField(parts[4], 1, 12),
            DaysOfWeek = ParseField(parts[5], 0, 6)
        };
    }

    /// <summary>
    /// 解析单个字段
    /// </summary>
    private static CronField ParseField(string field, int min, int max)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            throw new ArgumentException("字段不能为空");
        }

        field = field.Trim();

        // 处理特殊符号
        if (field is "*" or "?")
        {
            return new CronField { IsWildcard = true };
        }

        var result = new CronField();

        // 处理步长值 (/)
        var stepParts = field.Split('/');
        var step = 1;
        if (stepParts.Length == 2)
        {
            if (!int.TryParse(stepParts[1], out step) || step <= 0)
            {
                throw new ArgumentException($"无效的步长值: {stepParts[1]}");
            }
            field = stepParts[0];
        }

        // 处理列表值 (,)
        var listParts = field.Split(',');
        var values = new List<int>();

        foreach (var part in listParts)
        {
            var trimmedPart = part.Trim();

            // 处理范围值 (-)
            if (trimmedPart.Contains('-'))
            {
                var rangeParts = trimmedPart.Split('-');
                if (rangeParts.Length != 2)
                {
                    throw new ArgumentException($"无效的范围值: {trimmedPart}");
                }

                var start = ParseValue(rangeParts[0].Trim(), min, max);
                var end = ParseValue(rangeParts[1].Trim(), min, max);

                if (start > end)
                {
                    throw new ArgumentException($"范围起始值不能大于结束值: {start} > {end}");
                }

                for (var i = start; i <= end; i += step)
                {
                    if (i >= min && i <= max)
                    {
                        values.Add(i);
                    }
                }
            }
            else if (trimmedPart == "*")
            {
                for (var i = min; i <= max; i += step)
                {
                    values.Add(i);
                }
            }
            else
            {
                var value = ParseValue(trimmedPart, min, max);
                values.Add(value);
            }
        }

        result.Values = [.. values.Distinct().OrderBy(v => v)];
        return result;
    }

    /// <summary>
    /// 解析单个值
    /// </summary>
    private static int ParseValue(string value, int min, int max)
    {
        if (int.TryParse(value, out var numValue))
        {
            if (numValue < min || numValue > max)
            {
                throw new ArgumentException($"值 {numValue} 超出范围 [{min}, {max}]");
            }
            return numValue;
        }

        // 处理月份名称
        if (max == 12 && MonthNames.TryGetValue(value, out var monthValue))
        {
            return monthValue;
        }

        // 处理星期名称
        if (max == 6 && DayNames.TryGetValue(value, out var dayValue))
        {
            return dayValue;
        }

        throw new ArgumentException($"无法解析值: {value}");
    }

    #endregion

    #region 私有方法 - 匹配检查

    /// <summary>
    /// 检查字段是否匹配
    /// </summary>
    private static bool IsFieldMatch(CronField field, int value, int min, int max)
    {
        if (field.IsWildcard)
        {
            return true;
        }

        return field.Values.Contains(value);
    }

    #endregion

    #region 私有方法 - 描述生成

    /// <summary>
    /// 生成 Cron 表达式的描述
    /// </summary>
    private static string GenerateDescription(CronExpression cron)
    {
        var parts = new List<string>();

        if (cron.HasSeconds)
        {
            var secondDesc = GenerateFieldDescription(cron.Seconds, "秒");
            if (!string.IsNullOrEmpty(secondDesc))
            {
                parts.Add($"在第 {secondDesc}");
            }
        }

        var minuteDesc = GenerateFieldDescription(cron.Minutes, "分钟");
        if (!string.IsNullOrEmpty(minuteDesc))
        {
            parts.Add($"在第 {minuteDesc}");
        }

        var hourDesc = GenerateFieldDescription(cron.Hours, "小时");
        if (!string.IsNullOrEmpty(hourDesc))
        {
            parts.Add($"在 {hourDesc} 点");
        }

        var dayDesc = GenerateFieldDescription(cron.Days, "日");
        if (!string.IsNullOrEmpty(dayDesc))
        {
            parts.Add($"在每月第 {dayDesc} 日");
        }

        var monthDesc = GenerateFieldDescription(cron.Months, "月");
        if (!string.IsNullOrEmpty(monthDesc))
        {
            parts.Add($"在 {monthDesc} 月");
        }

        var dowDesc = GenerateFieldDescription(cron.DaysOfWeek, "星期");
        if (!string.IsNullOrEmpty(dowDesc))
        {
            parts.Add($"在星期 {dowDesc}");
        }

        return parts.Count > 0 ? string.Join("，", parts) : "每分钟执行";
    }

    /// <summary>
    /// 生成字段描述
    /// </summary>
    private static string GenerateFieldDescription(CronField field, string unit)
    {
        if (field.IsWildcard)
        {
            return string.Empty;
        }

        if (field.Values.Count == 1)
        {
            return field.Values[0].ToString();
        }

        if (field.Values.Count <= 5)
        {
            return string.Join("、", field.Values);
        }

        return $"{field.Values[0]}-{field.Values[^1]} 等 {field.Values.Count} 个值";
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

    #endregion
}
