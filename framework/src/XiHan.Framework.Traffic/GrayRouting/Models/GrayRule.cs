#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:GrayRule
// Guid:7a8b9c0d-1e2f-3a4b-5c6d-7e8f9a0b1c2d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/22 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Traffic.GrayRouting.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Enums;

namespace XiHan.Framework.Traffic.GrayRouting.Models;

/// <summary>
/// 灰度规则
/// </summary>
public class GrayRule : IGrayRule
{
    /// <summary>
    /// 规则唯一标识
    /// </summary>
    public string RuleId { get; set; } = null!;

    /// <summary>
    /// 规则名称
    /// </summary>
    public string RuleName { get; set; } = null!;

    /// <summary>
    /// 规则类型
    /// </summary>
    public GrayRuleType RuleType { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 优先级(数字越小优先级越高)
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// 目标版本
    /// </summary>
    public string? TargetVersion { get; set; }

    /// <summary>
    /// 目标服务标识
    /// </summary>
    public string? TargetServiceId { get; set; }

    /// <summary>
    /// 规则配置(JSON格式)
    /// </summary>
    /// <remarks>
    /// 根据 RuleType 不同,存储不同的配置结构:
    /// - Percentage: { "percentage": 10 }
    /// - UserId: { "userIds": ["user1", "user2"] }
    /// - TenantId: { "tenantIds": ["tenant1", "tenant2"] }
    /// - Header: { "headerName": "X-Gray", "headerValue": "true" }
    /// - IpAddress: { "ipAddresses": ["192.168.1.1", "192.168.1.2"] }
    /// </remarks>
    public string? Configuration { get; set; }

    /// <summary>
    /// 生效时间
    /// </summary>
    public DateTime? EffectiveTime { get; set; }

    /// <summary>
    /// 失效时间
    /// </summary>
    public DateTime? ExpiryTime { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedTime { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime? UpdatedTime { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}
