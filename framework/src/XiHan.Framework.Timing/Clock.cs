#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:Clock
// Guid:c7669140-da1e-4835-9c9c-ee1931721586
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 5:19:02
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Timing;

/// <summary>
/// 时钟
/// </summary>
public class Clock : IClock, ITransientDependency
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">时钟选项</param>
    /// <param name="currentTimezoneProvider">当前时区提供器</param>
    /// <param name="timezoneProvider">时区提供器</param>
    public Clock(
        IOptions<XiHanClockOptions> options,
        ICurrentTimezoneProvider currentTimezoneProvider,
        ITimezoneProvider timezoneProvider)
    {
        CurrentTimezoneProvider = currentTimezoneProvider;
        TimezoneProvider = timezoneProvider;
        Options = options.Value;
    }

    /// <summary>
    /// 当前时间
    /// </summary>
    public virtual DateTime Now => Options.Kind == DateTimeKind.Utc ? DateTime.UtcNow : DateTime.Now;

    /// <summary>
    /// 时间类型
    /// </summary>
    public virtual DateTimeKind Kind => Options.Kind;

    /// <summary>
    /// 是否支持多时区 （是否使用 UTC 时间）
    /// </summary>
    public virtual bool SupportsMultipleTimezone => Options.Kind == DateTimeKind.Utc;

    /// <summary>
    /// 时钟选项
    /// </summary>
    protected XiHanClockOptions Options { get; }

    /// <summary>
    /// 当前时区提供器
    /// </summary>
    protected ICurrentTimezoneProvider CurrentTimezoneProvider { get; }

    /// <summary>
    /// 时区提供器
    /// </summary>
    protected ITimezoneProvider TimezoneProvider { get; }

    /// <summary>
    /// 规范化时间
    /// </summary>
    /// <param name="dateTime">时间</param>
    /// <returns>规范化时间</returns>
    public virtual DateTime Normalize(DateTime dateTime)
    {
        if (Kind == DateTimeKind.Unspecified || Kind == dateTime.Kind)
        {
            return dateTime;
        }

        if (Kind == DateTimeKind.Local && dateTime.Kind == DateTimeKind.Utc)
        {
            return dateTime.ToLocalTime();
        }

        if (Kind == DateTimeKind.Utc && dateTime.Kind == DateTimeKind.Local)
        {
            return dateTime.ToUniversalTime();
        }

        return DateTime.SpecifyKind(dateTime, Kind);
    }

    /// <summary>
    /// 转换为用户时间
    /// </summary>
    /// <param name="utcDateTime">UTC 时间</param>
    /// <returns>用户时间</returns>
    public virtual DateTime ConvertToUserTime(DateTime utcDateTime)
    {
        if (!SupportsMultipleTimezone ||
            utcDateTime.Kind != DateTimeKind.Utc ||
            CurrentTimezoneProvider.TimeZone.IsNullOrWhiteSpace())
        {
            return utcDateTime;
        }

        var timezoneInfo = TimezoneProvider.GetTimeZoneInfo(CurrentTimezoneProvider.TimeZone);
        return TimeZoneInfo.ConvertTime(utcDateTime, timezoneInfo);
    }

    /// <summary>
    /// 转换为用户时间
    /// </summary>
    /// <param name="dateTimeOffset">时间偏移</param>
    /// <returns>用户时间</returns>
    public virtual DateTimeOffset ConvertToUserTime(DateTimeOffset dateTimeOffset)
    {
        if (!SupportsMultipleTimezone ||
            CurrentTimezoneProvider.TimeZone.IsNullOrWhiteSpace())
        {
            return dateTimeOffset;
        }

        var timezoneInfo = TimezoneProvider.GetTimeZoneInfo(CurrentTimezoneProvider.TimeZone);
        return TimeZoneInfo.ConvertTime(dateTimeOffset, timezoneInfo);
    }

    /// <summary>
    /// 转换为 UTC 时间
    /// </summary>
    /// <param name="dateTime">时间</param>
    /// <returns>UTC 时间</returns>
    public DateTime ConvertToUtc(DateTime dateTime)
    {
        if (!SupportsMultipleTimezone ||
            dateTime.Kind == DateTimeKind.Utc ||
            CurrentTimezoneProvider.TimeZone.IsNullOrWhiteSpace())
        {
            return dateTime;
        }

        var timezoneInfo = TimezoneProvider.GetTimeZoneInfo(CurrentTimezoneProvider.TimeZone);
        dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(dateTime, timezoneInfo);
    }
}
