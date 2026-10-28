// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 条件集求值结果。
/// </summary>
/// <param name="Outcome">结果（命中、不命中或待定）。</param>
/// <param name="Groups">各组的说明；只在要求说明时填充。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ConditionEvaluationResult(ConditionOutcome Outcome, IReadOnlyList<ConditionGroupEvaluation> Groups)
{
    /// <summary>
    /// 是否命中。
    /// </summary>
    public bool IsMatched => Outcome == ConditionOutcome.Matched;

    /// <summary>
    /// 是否待定。
    /// </summary>
    public bool IsPending => Outcome == ConditionOutcome.Pending;
}
