// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
    /// <remarks>
    /// ToString 属于诊断路径，任何状态下都不应抛异常。原实现把拼好的串再交给 CronHelper.FormatExpression
    /// 走一遍解析（而且嵌套调用了两次，外层那次是纯粹多余的）：默认构造的对象每个字段既不是通配、
    /// Values 又是空的，拼出来是一串空白，会被 ParseExpression 判为空表达式并抛 ArgumentException，
    /// 一打日志/进调试器就炸。各字段本身已经是解析后的显式取值，直接拼接就是规范形式，不需要再解析回去。
    /// </remarks>
    /// <returns>格式化的 Cron 表达式</returns>
    public override string ToString()
    {
        return HasSeconds
            ? $"{FormatField(Seconds)} {FormatField(Minutes)} {FormatField(Hours)} {FormatField(Days)} {FormatField(Months)} {FormatField(DaysOfWeek)}"
            : $"{FormatField(Minutes)} {FormatField(Hours)} {FormatField(Days)} {FormatField(Months)} {FormatField(DaysOfWeek)}";
    }

    /// <summary>
    /// 格式化字段
    /// </summary>
    /// <remarks>
    /// 既不是通配、取值又为空的字段在 cron 里没有对应记号（它匹配不到任何时刻），用 "-" 占位，
    /// 避免拼出空白段让整串看不出有几段。"-" 单独出现不是合法字段，回头再解析会明确报错，
    /// 不会被误当成"每一刻都执行"。
    /// </remarks>
    private static string FormatField(CronField field)
    {
        if (field.IsWildcard)
        {
            return "*";
        }

        return field.Values.Count > 0 ? string.Join(",", field.Values) : "-";
    }
}
