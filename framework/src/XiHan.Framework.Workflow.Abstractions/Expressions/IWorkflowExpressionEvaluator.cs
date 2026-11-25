// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Expressions;

/// <summary>
/// 表达式求值器
/// </summary>
/// <remarks>
/// 条件、属性表达式与 <c>{{ 表达式 }}</c> 模板。语言的文法、每个运算符与函数的语义、数字与时间的文本、上限与错误都在 RFC-0021「表达式」里逐条写明（ADR-0287），自定义的实现照它求出相同的结果。语法或求值失败 → <see cref="WorkflowErrors.ExpressionInvalid"/>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IWorkflowExpressionEvaluator
{
    /// <summary>
    /// 校验语法
    /// </summary>
    /// <remarks>
    /// 只解析：语法、函数名与实参个数、长度与嵌套的上限。变量存不存在、操作数的类型在求值时才知道
    /// </remarks>
    Result Validate(string expression);

    /// <summary>
    /// 求值
    /// </summary>
    /// <remarks>
    /// 结果是 <see langword="null"/>（字面量 <c>null</c>、缺少的成员或键、值为 null 的变量）时是成功的无值，不是失败（ADR-0059）。一次求值的工作量超过 16777216 即失败（<see cref="WorkflowMessages.ExpressionBudgetExceeded"/>，ADR-0296；读数字按它的写法的长度记，ADR-0303）；得出的数字写成 JSON 是数字的文本，不带结尾的零（ADR-0303）
    /// </remarks>
    Result<Maybe<JsonNode>> Evaluate(string expression, ExpressionScope scope);

    /// <summary>
    /// 求布尔值
    /// </summary>
    /// <remarks>
    /// 结果不是布尔（含无值）即失败
    /// </remarks>
    Result<bool> EvaluateCondition(string expression, ExpressionScope scope);

    /// <summary>
    /// 渲染模板：<c>{{ 表达式 }}</c> 占位换成求值结果的文本
    /// </summary>
    /// <remarks>
    /// 无值是空串，数字是不带多余零的规范文本，字符串原样，数组与对象是成员按名字排序的紧凑 JSON。结果总是字符串；写出的文本累计超过 4194304 个字符即失败（<see cref="WorkflowMessages.ExpressionStringTooLong"/>）；全部占位共用一份工作量（ADR-0296）
    /// </remarks>
    Result<string> Render(string source, ExpressionScope scope);
}
