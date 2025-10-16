#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplateSecurityChecker
// Guid:2t7v1v6r-0u3w-1t7v-7v2v-1r6u3w0t7v2v
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 3:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics;
using XiHan.Framework.Templating.Contexts;

namespace XiHan.Framework.Templating.Security;

/// <summary>
/// 模板安全检查器实现
/// </summary>
public class TemplateSecurityChecker : ITemplateSecurityChecker
{
    private readonly ITemplateSecurityAnalyzer _analyzer;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="analyzer">安全分析器</param>
    public TemplateSecurityChecker(ITemplateSecurityAnalyzer analyzer)
    {
        _analyzer = analyzer;
    }

    /// <summary>
    /// 检查模板安全性
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <param name="options">安全选项</param>
    /// <returns>安全检查结果</returns>
    public TemplateSecurityResult CheckSecurity(string templateSource, TemplateSecurityOptions? options = null)
    {
        options ??= TemplateSecurityOptions.Default;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var threats = new List<SecurityThreat>();

            // 检查模板大小
            if (templateSource.Length > options.MaxTemplateSize)
            {
                threats.Add(new SecurityThreat
                {
                    Type = SecurityThreatType.MemoryExhaustionRisk,
                    RiskLevel = SecurityRiskLevel.Medium,
                    Description = $"模板大小超过限制: {templateSource.Length} > {options.MaxTemplateSize}",
                    Recommendation = "减少模板大小或增加限制"
                });
            }

            // 使用分析器检测威胁
            var detectedThreats = _analyzer.DetectThreats(templateSource);
            threats.AddRange(detectedThreats);

            // 检查文件访问
            if (!options.AllowFileAccess)
            {
                var fileIncludes = _analyzer.ScanFileIncludes(templateSource);
                if (fileIncludes.Count != 0)
                {
                    threats.Add(new SecurityThreat
                    {
                        Type = SecurityThreatType.FileAccess,
                        RiskLevel = SecurityRiskLevel.High,
                        Description = "检测到文件访问操作",
                        Code = string.Join(", ", fileIncludes),
                        Recommendation = "移除文件访问操作或启用文件访问权限"
                    });
                }
            }

            stopwatch.Stop();

            // 确定总体风险级别
            var riskLevel = threats.Count != 0
                ? threats.Max(t => t.RiskLevel)
                : SecurityRiskLevel.Low;

            return threats.Count != 0
                ? TemplateSecurityResult.Insecure(threats, riskLevel, stopwatch.ElapsedMilliseconds)
                : TemplateSecurityResult.Secure(stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var threats = new List<SecurityThreat>
            {
                new()
                {
                    Type = SecurityThreatType.CodeInjection,
                    RiskLevel = SecurityRiskLevel.Critical,
                    Description = $"安全检查失败: {ex.Message}",
                    Recommendation = "检查模板语法"
                }
            };

            return TemplateSecurityResult.Insecure(threats, SecurityRiskLevel.Critical, stopwatch.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// 异步检查模板安全性
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <param name="options">安全选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>安全检查结果</returns>
    public Task<TemplateSecurityResult> CheckSecurityAsync(string templateSource, TemplateSecurityOptions? options = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CheckSecurity(templateSource, options));
    }

    /// <summary>
    /// 检查表达式安全性
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="options">安全选项</param>
    /// <returns>安全检查结果</returns>
    public TemplateSecurityResult CheckExpression(string expression, TemplateSecurityOptions? options = null)
    {
        options ??= TemplateSecurityOptions.Default;
        var threats = new List<SecurityThreat>();

        // 检查禁止的方法
        foreach (var forbiddenMethod in options.ForbiddenMethods)
        {
            if (expression.Contains(forbiddenMethod, StringComparison.OrdinalIgnoreCase))
            {
                threats.Add(new SecurityThreat
                {
                    Type = SecurityThreatType.DangerousMethodCall,
                    RiskLevel = SecurityRiskLevel.High,
                    Description = $"检测到禁止的方法调用: {forbiddenMethod}",
                    Code = expression,
                    Recommendation = "移除或替换危险方法调用"
                });
            }
        }

        return threats.Count != 0
            ? TemplateSecurityResult.Insecure(threats, threats.Max(t => t.RiskLevel))
            : TemplateSecurityResult.Secure();
    }

    /// <summary>
    /// 验证上下文安全性
    /// </summary>
    /// <param name="context">模板上下文</param>
    /// <param name="options">安全选项</param>
    /// <returns>安全检查结果</returns>
    public TemplateSecurityResult ValidateContext(ITemplateContext context, TemplateSecurityOptions? options = null)
    {
        options ??= TemplateSecurityOptions.Default;
        var threats = new List<SecurityThreat>();

        // 检查上下文中的变量类型
        foreach (var variableName in context.GetVariableNames())
        {
            var value = context.GetVariable(variableName);
            if (value != null)
            {
                var valueType = value.GetType();

                // 检查禁止的类型
                if (options.ForbiddenTypes.Contains(valueType))
                {
                    threats.Add(new SecurityThreat
                    {
                        Type = SecurityThreatType.DangerousMethodCall,
                        RiskLevel = SecurityRiskLevel.High,
                        Description = $"上下文包含禁止的类型: {valueType.Name}",
                        Recommendation = "移除危险类型的变量"
                    });
                }
            }
        }

        return threats.Count != 0
            ? TemplateSecurityResult.Insecure(threats, threats.Max(t => t.RiskLevel))
            : TemplateSecurityResult.Secure();
    }
}
