// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Templating.Security;

/// <summary>
/// 模板安全检查结果
/// </summary>
public record TemplateSecurityResult
{
    /// <summary>
    /// 是否安全
    /// </summary>
    public bool IsSecure { get; init; }

    /// <summary>
    /// 风险级别
    /// </summary>
    public SecurityRiskLevel RiskLevel { get; init; }

    /// <summary>
    /// 安全威胁
    /// </summary>
    public ICollection<SecurityThreat> Threats { get; init; } = [];

    /// <summary>
    /// 检查时间（毫秒）
    /// </summary>
    public long CheckTimeMs { get; init; }

    /// <summary>
    /// 建议措施
    /// </summary>
    public ICollection<string> Recommendations { get; init; } = [];

    /// <summary>
    /// 安全结果
    /// </summary>
    /// <param name="checkTime">检查时间</param>
    /// <returns>安全结果</returns>
    public static TemplateSecurityResult Secure(long checkTime = 0)
        => new() { IsSecure = true, RiskLevel = SecurityRiskLevel.Low, CheckTimeMs = checkTime };

    /// <summary>
    /// 不安全结果
    /// </summary>
    /// <param name="threats">安全威胁</param>
    /// <param name="riskLevel">风险级别</param>
    /// <param name="checkTime">检查时间</param>
    /// <returns>不安全结果</returns>
    public static TemplateSecurityResult Insecure(ICollection<SecurityThreat> threats, SecurityRiskLevel riskLevel = SecurityRiskLevel.High, long checkTime = 0)
        => new() { IsSecure = false, Threats = threats, RiskLevel = riskLevel, CheckTimeMs = checkTime };
}
