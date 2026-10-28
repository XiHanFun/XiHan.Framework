// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 条件组求值说明。
/// </summary>
/// <param name="Key">组标识。</param>
/// <param name="Outcome">结果（命中、不命中或待定）。</param>
/// <param name="Clauses">各子句的说明。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ConditionGroupEvaluation(string? Key, ConditionOutcome Outcome, IReadOnlyList<ConditionClauseEvaluation> Clauses);
