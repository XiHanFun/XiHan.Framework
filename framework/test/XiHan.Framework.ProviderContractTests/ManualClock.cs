// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Timing;

namespace XiHan.Framework.ProviderContractTests;

/// <summary>
/// 手动推进的 UTC 时钟
/// </summary>
public sealed class ManualClock : IClock
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="utcNow">初始时间</param>
    public ManualClock(DateTime utcNow)
    {
        Now = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
    }

    /// <summary>
    /// 当前时间
    /// </summary>
    public DateTime Now { get; private set; }

    /// <summary>
    /// 时间类型，固定为 UTC
    /// </summary>
    public DateTimeKind Kind => DateTimeKind.Utc;

    /// <summary>
    /// 是否支持多时区
    /// </summary>
    public bool SupportsMultipleTimezone => false;

    /// <summary>
    /// 推进时间
    /// </summary>
    /// <param name="duration">推进的时长</param>
    public void Advance(TimeSpan duration)
    {
        Now = Now.Add(duration);
    }

    /// <summary>
    /// 规范化时间，统一标记为 UTC
    /// </summary>
    /// <param name="dateTime">时间</param>
    /// <returns>UTC 时间</returns>
    public DateTime Normalize(DateTime dateTime)
    {
        return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
    }

    /// <summary>
    /// 转换为用户时间，原样返回
    /// </summary>
    /// <param name="utcDateTime">UTC 时间</param>
    /// <returns>用户时间</returns>
    public DateTime ConvertToUserTime(DateTime utcDateTime)
    {
        return utcDateTime;
    }

    /// <summary>
    /// 转换为用户时间，原样返回
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
