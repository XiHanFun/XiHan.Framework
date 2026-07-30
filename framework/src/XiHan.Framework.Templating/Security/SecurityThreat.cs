// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Templating.Compilers;

namespace XiHan.Framework.Templating.Security;

/// <summary>
/// 安全威胁
/// </summary>
public record SecurityThreat
{
    /// <summary>
    /// 威胁类型
    /// </summary>
    public SecurityThreatType Type { get; init; }

    /// <summary>
    /// 风险级别
    /// </summary>
    public SecurityRiskLevel RiskLevel { get; init; }

    /// <summary>
    /// 威胁描述
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 威胁位置
    /// </summary>
    public TemplateSourceLocation? Location { get; init; }

    /// <summary>
    /// 相关代码
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    /// 修复建议
    /// </summary>
    public string? Recommendation { get; init; }
}
