#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:GrayRuleType
// Guid:4d5e6f7a-8b9c-0d1e-2f3a-4b5c6d7e8f9a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/1/22 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Traffic.GrayRouting.Enums;

/// <summary>
/// 灰度规则类型
/// </summary>
public enum GrayRuleType
{
    /// <summary>
    /// 基于百分比的流量分配
    /// </summary>
    Percentage = 1,

    /// <summary>
    /// 基于用户ID的定向灰度
    /// </summary>
    UserId = 2,

    /// <summary>
    /// 基于租户ID的定向灰度
    /// </summary>
    TenantId = 3,

    /// <summary>
    /// 基于请求头的条件灰度
    /// </summary>
    Header = 4,

    /// <summary>
    /// 基于IP地址的灰度
    /// </summary>
    IpAddress = 5,

    /// <summary>
    /// 基于自定义条件的灰度
    /// </summary>
    Custom = 99
}
