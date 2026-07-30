// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Templating.Security;

/// <summary>
/// 模板安全报告
/// </summary>
public record TemplateSecurityReport
{
    /// <summary>
    /// 模板大小
    /// </summary>
    public int TemplateSize { get; init; }

    /// <summary>
    /// 表达式数量
    /// </summary>
    public int ExpressionCount { get; init; }

    /// <summary>
    /// 最大表达式深度
    /// </summary>
    public int MaxExpressionDepth { get; init; }

    /// <summary>
    /// 循环数量
    /// </summary>
    public int LoopCount { get; init; }

    /// <summary>
    /// 文件包含数量
    /// </summary>
    public int IncludeCount { get; init; }

    /// <summary>
    /// 使用的类型
    /// </summary>
    public ICollection<string> UsedTypes { get; init; } = [];

    /// <summary>
    /// 使用的方法
    /// </summary>
    public ICollection<string> UsedMethods { get; init; } = [];

    /// <summary>
    /// 检测到的威胁
    /// </summary>
    public ICollection<SecurityThreat> Threats { get; init; } = [];

    /// <summary>
    /// 总体风险级别
    /// </summary>
    public SecurityRiskLevel OverallRisk { get; init; }
}
