#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplateSecurityAnalyzer
// Guid:67edbbe4-6023-453d-87e0-3c8cb0fd5c7d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 04:07:41
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.RegularExpressions;
using XiHan.Framework.Templating.Compilers;

namespace XiHan.Framework.Templating.Security;

/// <summary>
/// 模板安全分析器实现
/// </summary>
public partial class TemplateSecurityAnalyzer : ITemplateSecurityAnalyzer
{
    private static readonly string[] DangerousPatterns =
    [
        @"System\.IO\.",
        @"System\.Diagnostics\.",
        @"System\.Reflection\.",
        @"File\.",
        @"Directory\.",
        @"Process\.",
        @"Assembly\.",
        @"Type\."
    ];

    /// <summary>
    /// 分析模板安全风险
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>安全风险报告</returns>
    public TemplateSecurityReport AnalyzeSecurity(string templateSource)
    {
        var threats = DetectThreats(templateSource);
        var usedTypes = ExtractUsedTypes(templateSource);
        var usedMethods = ExtractUsedMethods(templateSource);

        return new TemplateSecurityReport
        {
            TemplateSize = templateSource.Length,
            ExpressionCount = CountExpressions(templateSource),
            MaxExpressionDepth = CalculateMaxExpressionDepth(templateSource),
            LoopCount = CountLoops(templateSource),
            IncludeCount = ScanFileIncludes(templateSource).Count,
            UsedTypes = usedTypes,
            UsedMethods = usedMethods,
            Threats = threats,
            OverallRisk = threats.Count != 0 ? threats.Max(t => t.RiskLevel) : SecurityRiskLevel.Low
        };
    }

    /// <summary>
    /// 检测危险模式
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>危险模式集合</returns>
    public ICollection<SecurityThreat> DetectThreats(string templateSource)
    {
        var threats = new List<SecurityThreat>();

        foreach (var pattern in DangerousPatterns)
        {
            var matches = Regex.Matches(templateSource, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                threats.Add(new SecurityThreat
                {
                    Type = SecurityThreatType.DangerousMethodCall,
                    RiskLevel = SecurityRiskLevel.High,
                    Description = $"检测到危险模式: {pattern}",
                    Code = match.Value,
                    Location = new TemplateSourceLocation
                    {
                        Line = templateSource[..match.Index].Count(c => c == '\n') + 1,
                        Column = match.Index - templateSource.LastIndexOf('\n', match.Index)
                    },
                    Recommendation = "避免使用危险的系统调用"
                });
            }
        }

        return threats;
    }

    /// <summary>
    /// 验证白名单
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="whitelist">白名单</param>
    /// <returns>是否通过验证</returns>
    public bool ValidateWhitelist(string expression, ICollection<string> whitelist)
    {
        if (whitelist.Count == 0)
        {
            return true;
        }

        return whitelist.Any(item => expression.Contains(item, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 检查黑名单
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="blacklist">黑名单</param>
    /// <returns>是否存在违规</returns>
    public bool CheckBlacklist(string expression, ICollection<string> blacklist)
    {
        return blacklist.Any(item => expression.Contains(item, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 扫描文件包含
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>文件包含列表</returns>
    public ICollection<string> ScanFileIncludes(string templateSource)
    {
        var includes = new List<string>();

        // 扫描常见的文件包含模式
        var patterns = new[]
        {
            @"include\s+[""']([^""']+)[""']",
            @"render\s+[""']([^""']+)[""']",
            @"partial\s+[""']([^""']+)[""']"
        };

        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(templateSource, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                includes.Add(match.Groups[1].Value);
            }
        }

        return includes;
    }

    private static HashSet<string> ExtractUsedTypes(string templateSource)
    {
        var types = new HashSet<string>();
        var typePattern = @"([A-Z][a-zA-Z0-9_]*\.)+[A-Z][a-zA-Z0-9_]*";
        var matches = Regex.Matches(templateSource, typePattern);

        foreach (Match match in matches)
        {
            types.Add(match.Value);
        }

        return types;
    }

    private static HashSet<string> ExtractUsedMethods(string templateSource)
    {
        var methods = new HashSet<string>();
        var methodPattern = @"(\w+)\s*\(";
        var matches = Regex.Matches(templateSource, methodPattern);

        foreach (Match match in matches)
        {
            methods.Add(match.Groups[1].Value);
        }

        return methods;
    }

    private static int CountExpressions(string templateSource)
    {
        return VariableRegex().Matches(templateSource).Count;
    }

    private static int CalculateMaxExpressionDepth(string templateSource)
    {
        // 简化实现，计算最大括号嵌套深度
        var maxDepth = 0;
        var currentDepth = 0;

        foreach (var c in templateSource)
        {
            if (c == '{')
            {
                currentDepth++;
                maxDepth = Math.Max(maxDepth, currentDepth);
            }
            else if (c == '}')
            {
                currentDepth = Math.Max(0, currentDepth - 1);
            }
        }

        return maxDepth;
    }

    private static int CountLoops(string templateSource)
    {
        var loopPatterns = new[] { "for", "while", "each" };
        return loopPatterns.Sum(pattern =>
            Regex.Matches(templateSource, $@"{{\s*{pattern}\s", RegexOptions.IgnoreCase).Count);
    }

    [GeneratedRegex(@"{{.*?}}")]
    private static partial Regex VariableRegex();
}
