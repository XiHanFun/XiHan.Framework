#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ICurrentTimezoneProvider
// Guid:6172503e-cadb-4c17-a4ae-4dc0350ace3b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 05:23:52
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
