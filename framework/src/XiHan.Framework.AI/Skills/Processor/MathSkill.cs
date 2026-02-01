#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MathSkill
// Guid:75850249-5f5f-4551-bf8c-e2e8c97c1563
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/25 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using XiHan.Framework.AI.Results;

namespace XiHan.Framework.AI.Skills.Processor;

/// <summary>
/// 数学计算技能
/// </summary>
public class MathSkill : IXiHanSkill
{
    private readonly ILogger<MathSkill> _logger;
    private readonly Regex _mathExpressionRegex = new(@"计算\s*([\d\+\-\*\/\(\)\.\s]+)");

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public MathSkill(ILogger<MathSkill> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 技能名称
    /// </summary>
    public string Name => "MathSkill";

    /// <summary>
    /// 技能描述
    /// </summary>
    public string Description => "提供基础的数学计算功能，支持加减乘除。例如：计算 1+2*3";

    /// <summary>
    /// 判断技能是否可处理指定输入
    /// </summary>
    public bool CanHandle(string input, SkillContext context)
    {
        return _mathExpressionRegex.IsMatch(input);
    }

    /// <summary>
    /// 执行技能
    /// </summary>
    public Task<XiHanSkillResult> ExecuteAsync(string input, SkillContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // 提取表达式
            var match = _mathExpressionRegex.Match(input);
            if (!match.Success || match.Groups.Count < 2)
            {
                return Task.FromResult(XiHanSkillResult.Failure("无法识别数学表达式"));
            }

            var expression = match.Groups[1].Value.Trim();

            // 计算表达式
            var result = CalculateExpression(expression);

            _logger.LogInformation("数学计算: {Expression} = {Result}", expression, result);

            return Task.FromResult(XiHanSkillResult.Success($"计算结果: {expression} = {result}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "计算表达式时出错");
            return Task.FromResult(XiHanSkillResult.Failure($"计算出错: {ex.Message}"));
        }
    }

    /// <summary>
    /// 计算数学表达式
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <returns>计算结果</returns>
    private static double CalculateExpression(string expression)
    {
        // 简单实现，实际项目中可以使用更复杂的表达式计算库
        var result = new System.Data.DataTable().Compute(expression, null);
        return Convert.ToDouble(result);
    }
}
