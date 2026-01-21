#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:GrayDecision
// Guid:6f7a8b9c-0d1e-2f3a-4b5c-6d7e8f9a0b1c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/1/22 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Traffic.GrayRouting.Abstractions;

namespace XiHan.Framework.Traffic.GrayRouting.Models;

/// <summary>
/// 灰度决策
/// </summary>
public class GrayDecision : IGrayDecision
{
    /// <summary>
    /// 是否命中灰度
    /// </summary>
    public bool IsGray { get; set; }

    /// <summary>
    /// 目标版本
    /// </summary>
    public string? TargetVersion { get; set; }

    /// <summary>
    /// 目标服务标识
    /// </summary>
    public string? TargetServiceId { get; set; }

    /// <summary>
    /// 匹配的规则ID
    /// </summary>
    public string? MatchedRuleId { get; set; }

    /// <summary>
    /// 决策原因
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// 扩展数据
    /// </summary>
    public IDictionary<string, object>? ExtensionData { get; set; }

    /// <summary>
    /// 创建一个未命中灰度的决策
    /// </summary>
    /// <returns></returns>
    public static GrayDecision NotGray(string? reason = null)
    {
        return new GrayDecision
        {
            IsGray = false,
            Reason = reason ?? "未命中任何灰度规则"
        };
    }

    /// <summary>
    /// 创建一个命中灰度的决策
    /// </summary>
    /// <param name="targetVersion">目标版本</param>
    /// <param name="matchedRuleId">匹配的规则ID</param>
    /// <param name="reason">决策原因</param>
    /// <returns></returns>
    public static GrayDecision Gray(string targetVersion, string? matchedRuleId = null, string? reason = null)
    {
        return new GrayDecision
        {
            IsGray = true,
            TargetVersion = targetVersion,
            MatchedRuleId = matchedRuleId,
            Reason = reason ?? $"命中灰度规则: {matchedRuleId}"
        };
    }
}
