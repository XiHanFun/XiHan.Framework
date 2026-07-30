// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Timing;

/// <summary>
/// 当前时区提供器
/// </summary>
public interface ICurrentTimezoneProvider
{
    /// <summary>
    /// 时区
    /// </summary>
    string? TimeZone { get; set; }
}
