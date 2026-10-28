// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 子句求值说明（试算时逐条给出）。
/// </summary>
/// <param name="Key">子句标识。</param>
/// <param name="AttributeName">属性名（原文）。</param>
/// <param name="CanonicalName">规范名；属性没登记时为 <see langword="null"/>。</param>
/// <param name="Operator">运算符。</param>
/// <param name="IsNegated">是否取反。</param>
/// <param name="Value">条件值原文。</param>
/// <param name="Outcome">结果。</param>
/// <param name="AttributeValues">取到的属性值（不变文化的文本）。</param>
/// <param name="Reason">无法判定或待定的原因：编译错误，或取不到（<see cref="SecurityErrors.ConditionAttributeMissing"/>）、暂不可得（<see cref="SecurityErrors.ConditionAttributePending"/>）、值不合法（<see cref="SecurityErrors.ConditionAttributeInvalid"/>）；命中与不命中时为 <see langword="null"/>。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ConditionClauseEvaluation(
    string? Key,
    string AttributeName,
    string? CanonicalName,
    ConditionOperator Operator,
    bool IsNegated,
    string? Value,
    ConditionOutcome Outcome,
    IReadOnlyList<string> AttributeValues,
    Error? Reason);
