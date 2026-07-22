// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Timing;

/// <summary>
/// 时钟接口
/// </summary>
public interface IClock
{
    /// <summary>
    /// 当前时间
    /// </summary>
    DateTime Now { get; }

    /// <summary>
    /// 时间类型
    /// </summary>
    DateTimeKind Kind { get; }

    /// <summary>
    /// 是否支持多时区
    /// </summary>
    bool SupportsMultipleTimezone { get; }

    /// <summary>
    /// 规范化时间
    /// </summary>
    /// <param name="dateTime">时间</param>
    /// <returns>规范化时间</returns>
    DateTime Normalize(DateTime dateTime);

    /// <summary>
    /// 转换为用户时间
    /// </summary>
    /// <param name="utcDateTime">UTC 时间</param>
    /// <returns>用户时间</returns>
    DateTime ConvertToUserTime(DateTime utcDateTime);

    /// <summary>
    /// 转换为用户时间
    /// </summary>
    /// <param name="dateTimeOffset">时间偏移</param>
    /// <returns>用户时间</returns>
    DateTimeOffset ConvertToUserTime(DateTimeOffset dateTimeOffset);

    /// <summary>
    /// 转换为 UTC 时间
    /// </summary>
    /// <param name="dateTime">时间</param>
    /// <returns>UTC 时间</returns>
    DateTime ConvertToUtc(DateTime dateTime);
}
