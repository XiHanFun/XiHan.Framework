// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Conditions;

/// <summary>
/// 子句求值说明
/// </summary>
/// <param name="Key">子句标识</param>
/// <param name="AttributeName">属性名（原文）</param>
/// <param name="CanonicalName">规范名；属性没登记时为空</param>
/// <param name="Operator">运算符</param>
/// <param name="IsNegated">是否取反</param>
/// <param name="Value">条件值原文</param>
/// <param name="Outcome">结果</param>
/// <param name="AttributeValues">取到的属性值（文本）</param>
/// <param name="Message">无法判定或待定的原因</param>
public sealed record ConditionClauseEvaluation(
    string? Key,
    string AttributeName,
    string? CanonicalName,
    ConditionOperator Operator,
    bool IsNegated,
    string? Value,
    ConditionOutcome Outcome,
    IReadOnlyList<string> AttributeValues,
    string? Message);
