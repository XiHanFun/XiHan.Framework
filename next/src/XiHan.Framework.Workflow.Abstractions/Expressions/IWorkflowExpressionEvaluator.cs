// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Expressions;

/// <summary>
/// 表达式求值器。
/// </summary>
/// <remarks>
/// 条件、属性表达式与 <c>{{ 表达式 }}</c> 模板。语法或求值失败 → <see cref="WorkflowErrors.ExpressionInvalid"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IWorkflowExpressionEvaluator
{
    /// <summary>
    /// 校验语法。
    /// </summary>
    Result Validate(string expression);

    /// <summary>
    /// 求值。
    /// </summary>
    Result<JsonNode?> Evaluate(string expression, ExpressionScope scope);

    /// <summary>
    /// 求布尔值。
    /// </summary>
    /// <remarks>
    /// 结果不是布尔即失败。
    /// </remarks>
    Result<bool> EvaluateCondition(string expression, ExpressionScope scope);

    /// <summary>
    /// 渲染模板：<c>{{ 表达式 }}</c> 占位替换为求值结果的文本（null 为空串，容器为 JSON）。
    /// </summary>
    Result<string> Render(string source, ExpressionScope scope);
}
