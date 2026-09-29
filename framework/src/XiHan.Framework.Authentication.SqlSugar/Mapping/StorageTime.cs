// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authentication.SqlSugar.Mapping;

/// <summary>
/// 存储层时间换算
/// </summary>
/// <remarks>
/// 写入前把时间换算为 UTC，未指定时区的值按 UTC 处理；读出后把时间标记为 UTC，不做换算。
/// </remarks>
public static class StorageTime
{
    /// <summary>
    /// 把写入值换算为 UTC
    /// </summary>
    /// <param name="value">写入值</param>
    /// <returns>UTC 时间</returns>
    public static DateTime ToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value
        };
    }

    /// <summary>
    /// 把可空写入值换算为 UTC
    /// </summary>
    /// <param name="value">写入值</param>
    /// <returns>UTC 时间，输入为空时返回空</returns>
    public static DateTime? ToUtc(DateTime? value)
    {
        return value.HasValue ? ToUtc(value.Value) : null;
    }

    /// <summary>
    /// 把读出值标记为 UTC
    /// </summary>
    /// <param name="value">读出值</param>
    /// <returns>标记为 UTC 的时间</returns>
    public static DateTime FromStorage(DateTime value)
    {
        return DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    /// <summary>
    /// 把可空读出值标记为 UTC
    /// </summary>
    /// <param name="value">读出值</param>
    /// <returns>标记为 UTC 的时间，输入为空时返回空</returns>
    public static DateTime? FromStorage(DateTime? value)
    {
        return value.HasValue ? FromStorage(value.Value) : null;
    }
}
