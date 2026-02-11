#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IGrayDecision
// Guid:3c4d5e6f-7a8b-9c0d-1e2f-3a4b5c6d7e8f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/22 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Traffic.GrayRouting.Abstractions;

/// <summary>
/// 灰度决策接口
/// </summary>
/// <remarks>
/// 表示灰度路由的最终决策结果
/// </remarks>
public interface IGrayDecision
{
    /// <summary>
    /// 是否命中灰度
    /// </summary>
    bool IsGray { get; }

    /// <summary>
    /// 目标版本
    /// </summary>
    string? TargetVersion { get; }

    /// <summary>
    /// 目标服务标识
    /// </summary>
    string? TargetServiceId { get; }

    /// <summary>
    /// 匹配的规则ID
    /// </summary>
    string? MatchedRuleId { get; }

    /// <summary>
    /// 决策原因
    /// </summary>
    string? Reason { get; }

    /// <summary>
    /// 扩展数据
    /// </summary>
    IDictionary<string, object>? ExtensionData { get; }
}
