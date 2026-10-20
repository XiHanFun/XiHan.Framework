// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Assignees;
using XiHan.Framework.Workflow.Abstractions.Exceptions;
using XiHan.Framework.Workflow.Abstractions.Expressions;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Expressions;

namespace XiHan.Framework.Workflow.Assignees.BuiltIn;

/// <summary>
/// 表达式解析器（参数为一条表达式，求值为标识列表或逗号分隔字符串）
/// </summary>
public class ExpressionAssigneeProvider : IWorkflowAssigneeProvider
{
    private readonly IWorkflowExpressionEvaluator _evaluator;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="evaluator">表达式求值器</param>
    public ExpressionAssigneeProvider(IWorkflowExpressionEvaluator evaluator)
    {
        _evaluator = evaluator;
    }

    /// <summary>
    /// 规则类型
    /// </summary>
    public string RuleType => WorkflowAssigneeRuleTypes.Expression;

    /// <summary>
    /// 校验规则参数（恰好一条语法合法的表达式）
    /// </summary>
    /// <param name="rule">规则</param>
    /// <returns>错误信息列表</returns>
    public IReadOnlyList<string> Validate(WorkflowAssigneeRule rule)
    {
        var expressions = rule.Values.Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
        if (expressions.Count != 1)
        {
            return ["表达式规则必须且只能配置一条表达式"];
        }

        try
        {
            WorkflowExpressionEvaluator.ValidateSyntax(expressions[0]);
            return [];
        }
        catch (WorkflowException ex)
        {
            return [$"表达式规则 {expressions[0]} 非法：{ex.Message}"];
        }
    }

    /// <summary>
    /// 解析受理人
    /// </summary>
    /// <param name="context">解析上下文</param>
    /// <returns>受理人标识列表</returns>
    public async Task<IReadOnlyList<string>> ResolveAsync(WorkflowAssigneeResolveContext context)
    {
        var expression = context.Rule.Values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? throw new WorkflowException($"人工任务节点 {context.Node.Id} 的表达式规则未配置表达式");

        var evaluated = await _evaluator.EvaluateAsync(expression, context.Variables, context.CancellationToken);
        return evaluated switch
        {
            null => [],
            string text => [.. text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
            _ => WorkflowValueConverter.ConvertTo<List<string>>(evaluated) ?? []
        };
    }
}
