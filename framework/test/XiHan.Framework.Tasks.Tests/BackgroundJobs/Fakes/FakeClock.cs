// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Timing;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs.Fakes;

/// <summary>
/// 可控时钟
/// </summary>
/// <remarks>
/// 后台作业的入队时间、退避时间、放弃判定全部以 <see cref="IClock.Now"/> 为基准，
/// 用固定时钟才能对 NextTryTime 做精确到刻度的断言，而不是写成"大约多少秒"的模糊断言。
/// </remarks>
public sealed class FakeClock : IClock
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="now">当前时间</param>
    public FakeClock(DateTime now)
    {
        Now = now;
    }

    /// <summary>
    /// 当前时间（可由用例推进）
    /// </summary>
    public DateTime Now { get; set; }

    /// <summary>
    /// 时间类型
    /// </summary>
    public DateTimeKind Kind => DateTimeKind.Utc;

    /// <summary>
    /// 是否支持多时区
    /// </summary>
    public bool SupportsMultipleTimezone => false;

    /// <summary>
    /// 规范化时间
    /// </summary>
    /// <param name="dateTime">时间</param>
    /// <returns>规范化时间</returns>
    public DateTime Normalize(DateTime dateTime)
    {
        return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
    }

    /// <summary>
    /// 转换为用户时间
    /// </summary>
    /// <param name="utcDateTime">UTC 时间</param>
    /// <returns>用户时间</returns>
    public DateTime ConvertToUserTime(DateTime utcDateTime)
    {
        return utcDateTime;
    }

    /// <summary>
    /// 转换为用户时间
    /// </summary>
    /// <param name="dateTimeOffset">时间偏移</param>
    /// <returns>用户时间</returns>
    public DateTimeOffset ConvertToUserTime(DateTimeOffset dateTimeOffset)
    {
        return dateTimeOffset;
    }

    /// <summary>
    /// 转换为 UTC 时间
    /// </summary>
    /// <param name="dateTime">时间</param>
    /// <returns>UTC 时间</returns>
    public DateTime ConvertToUtc(DateTime dateTime)
    {
        return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
    }
}
