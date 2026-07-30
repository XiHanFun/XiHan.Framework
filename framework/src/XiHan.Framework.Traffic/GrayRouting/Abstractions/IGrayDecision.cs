// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
