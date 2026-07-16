#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IWorkflowExpressionEvaluator
// Guid:2fd68a05-91c3-4b7e-8d24-f60c53a17e98
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:35:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Workflow.Abstractions.Expressions;

/// <summary>
/// 工作流表达式求值器
/// </summary>
/// <remarks>
/// 默认实现为内置轻量表达式语言：变量引用（含点号导航与索引）、字面量、
/// 算术/比较/逻辑运算、内置函数；应用侧可替换为脚本引擎实现。
/// </remarks>
public interface IWorkflowExpressionEvaluator
{
    /// <summary>
    /// 求值表达式
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="variables">变量上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>求值结果</returns>
    Task<object?> EvaluateAsync(
        string expression,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 求值表达式并转换为目标类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="expression">表达式</param>
    /// <param name="variables">变量上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>求值结果</returns>
    Task<T?> EvaluateAsync<T>(
        string expression,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 求值条件表达式（结果必须为布尔值，否则视为定义错误抛出异常）
    /// </summary>
    /// <param name="expression">条件表达式</param>
    /// <param name="variables">变量上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>条件结果</returns>
    Task<bool> EvaluateConditionAsync(
        string expression,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 渲染模板（把文本中的 <c>{{ 表达式 }}</c> 占位替换为求值结果）
    /// </summary>
    /// <param name="template">模板文本</param>
    /// <param name="variables">变量上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>渲染结果</returns>
    Task<string> RenderTemplateAsync(
        string template,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken = default);
}
