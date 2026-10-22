// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Conditions;

/// <summary>
/// 条件组求值说明
/// </summary>
/// <param name="Key">组标识</param>
/// <param name="Outcome">结果（命中、不命中或待定）</param>
/// <param name="Clauses">各子句的说明</param>
public sealed record ConditionGroupEvaluation(string? Key, ConditionOutcome Outcome, IReadOnlyList<ConditionClauseEvaluation> Clauses);
