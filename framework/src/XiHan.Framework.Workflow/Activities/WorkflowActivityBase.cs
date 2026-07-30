// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Expressions;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Activities;

/// <summary>
/// 工作流活动基类（节点属性读取与模板/表达式解析的便捷方法）
/// </summary>
public abstract class WorkflowActivityBase : IWorkflowActivity
{
    /// <inheritdoc />
    public abstract Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context);

    /// <summary>
    /// 读取节点属性并转换为目标类型（不存在返回默认值）
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="context">执行上下文</param>
    /// <param name="name">属性名</param>
    /// <returns>属性值</returns>
    protected static T? GetProperty<T>(ActivityExecutionContext context, string name)
    {
        return context.Node.Properties.TryGetValue(name, out var value)
            ? WorkflowValueConverter.ConvertTo<T>(value)
            : default;
    }

    /// <summary>
    /// 读取字符串属性并渲染 <c>{{ 表达式 }}</c> 模板
    /// </summary>
    /// <param name="context">执行上下文</param>
    /// <param name="name">属性名</param>
    /// <returns>渲染后的字符串（属性不存在返回 null）</returns>
    protected static async Task<string?> GetTemplatedStringAsync(ActivityExecutionContext context, string name)
    {
        var template = GetProperty<string>(context, name);
        if (template is null)
        {
            return null;
        }

        var evaluator = GetEvaluator(context);
        return await evaluator.RenderTemplateAsync(template, context.Variables.AsReadOnly, context.CancellationToken);
    }

    /// <summary>
    /// 读取字符串属性并作为表达式求值
    /// </summary>
    /// <param name="context">执行上下文</param>
    /// <param name="name">属性名</param>
    /// <returns>求值结果（属性不存在返回 null）</returns>
    protected static async Task<object?> EvaluatePropertyAsync(ActivityExecutionContext context, string name)
    {
        var expression = GetProperty<string>(context, name);
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var evaluator = GetEvaluator(context);
        return await evaluator.EvaluateAsync(expression, context.Variables.AsReadOnly, context.CancellationToken);
    }

    /// <summary>
    /// 获取表达式求值器
    /// </summary>
    /// <param name="context">执行上下文</param>
    /// <returns>表达式求值器</returns>
    protected static IWorkflowExpressionEvaluator GetEvaluator(ActivityExecutionContext context)
    {
        return context.ServiceProvider.GetRequiredService<IWorkflowExpressionEvaluator>();
    }
}
