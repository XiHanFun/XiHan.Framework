// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
