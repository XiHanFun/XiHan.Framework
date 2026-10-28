// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 条件求值器（无状态，线程安全）。多值属性按"任一元素"判定（等于、比大小、包含、在集合中、在范围内、开头、结尾），否定运算符按"没有元素"判定；字符串比较不区分大小写。
/// 为空 / 不为空把取不到、空列表与全是空白的字符串当空，可以取反；其余运算符遇到取不到的属性为无法判定，取反不翻转。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class ConditionEvaluator : IConditionEvaluator
{
    /// <summary>
    /// 不带说明的命中结果。
    /// </summary>
    private static readonly ConditionEvaluationResult _matched = new(ConditionOutcome.Matched, []);

    /// <summary>
    /// 不带说明的不命中结果。
    /// </summary>
    private static readonly ConditionEvaluationResult _notMatched = new(ConditionOutcome.NotMatched, []);

    /// <summary>
    /// 不带说明的待定结果。
    /// </summary>
    private static readonly ConditionEvaluationResult _pending = new(ConditionOutcome.Pending, []);

    /// <inheritdoc />
    public async ValueTask<ConditionEvaluationResult> EvaluateAsync(CompiledConditionSet conditions, IConditionAttributeSource source, bool explain, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        if (conditions.IsEmpty)
        {
            return _matched;
        }

        var anyPending = false;
        var anyMatched = false;
        var groupEvaluations = explain ? new List<ConditionGroupEvaluation>(conditions.Groups.Count) : null;
        foreach (var group in conditions.Groups)
        {
            var (outcome, clauses) = await EvaluateGroupAsync(group, conditions.TimeZone, source, explain, cancellationToken).ConfigureAwait(false);
            groupEvaluations?.Add(new ConditionGroupEvaluation(group.Key, outcome, clauses));
            if (outcome == ConditionOutcome.Matched)
            {
                anyMatched = true;
                if (!explain)
                {
                    return _matched;
                }
            }
            else if (outcome == ConditionOutcome.Pending)
            {
                anyPending = true;
            }
        }

        var setOutcome = anyMatched ? ConditionOutcome.Matched : anyPending ? ConditionOutcome.Pending : ConditionOutcome.NotMatched;
        if (groupEvaluations is null)
        {
            return setOutcome == ConditionOutcome.Pending ? _pending : _notMatched;
        }

        return new ConditionEvaluationResult(setOutcome, groupEvaluations);
    }

    /// <summary>
    /// 组内且：任一子句不命中或无法判定即不命中（不要说明时立即返回），否则任一待定即待定，否则命中。
    /// </summary>
    private static async ValueTask<(ConditionOutcome Outcome, IReadOnlyList<ConditionClauseEvaluation> Clauses)> EvaluateGroupAsync(CompiledConditionGroup group, TimeZoneInfo timeZone, IConditionAttributeSource source, bool explain, CancellationToken cancellationToken)
    {
        var anyPending = false;
        var failed = false;
        var clauseEvaluations = explain ? new List<ConditionClauseEvaluation>(group.Clauses.Count) : null;
        foreach (var clause in group.Clauses)
        {
            var evaluation = await EvaluateClauseAsync(clause, timeZone, source, explain, cancellationToken).ConfigureAwait(false);
            clauseEvaluations?.Add(evaluation);
            switch (evaluation.Outcome)
            {
                case ConditionOutcome.NotMatched or ConditionOutcome.Indeterminate:
                    failed = true;
                    if (!explain)
                    {
                        return (ConditionOutcome.NotMatched, []);
                    }

                    break;
                case ConditionOutcome.Pending:
                    anyPending = true;
                    break;
            }
        }

        var outcome = failed ? ConditionOutcome.NotMatched : anyPending ? ConditionOutcome.Pending : ConditionOutcome.Matched;
        return (outcome, clauseEvaluations ?? (IReadOnlyList<ConditionClauseEvaluation>)[]);
    }

    /// <summary>
    /// 一个子句：编译出错为无法判定；取属性值，暂不可得为待定；为空 / 不为空直接判定；其余先把属性值转成值种类再运算。
    /// </summary>
    private static async ValueTask<ConditionClauseEvaluation> EvaluateClauseAsync(CompiledConditionClause clause, TimeZoneInfo timeZone, IConditionAttributeSource source, bool explain, CancellationToken cancellationToken)
    {
        if (clause.Error is not null || clause.Resolution is null)
        {
            // 没有登记的属性在编译时就记了错误，所以这里一定有错误
            return Describe(clause, ConditionOutcome.Indeterminate, [], clause.Error, explain);
        }

        var descriptor = clause.Resolution.Descriptor;
        var canonicalName = clause.Resolution.CanonicalName;
        var value = await source.GetAsync(canonicalName, cancellationToken).ConfigureAwait(false);
        if (value.State == ConditionAttributeValueState.Deferred)
        {
            return Describe(clause, ConditionOutcome.Pending, [], Error.Unavailable(SecurityErrors.ConditionAttributePending, SecurityMessages.ConditionAttributePending, MessageArguments.Of(("attribute", canonicalName))), explain);
        }

        var @operator = clause.Source.Operator;
        if (@operator is ConditionOperator.IsNull or ConditionOperator.IsNotNull)
        {
            var isEmpty = value.State == ConditionAttributeValueState.Missing
                || value.Values.Count == 0
                || value.Values.All(static item => item is string text && string.IsNullOrWhiteSpace(text));
            var isNullResult = @operator == ConditionOperator.IsNull ? isEmpty : !isEmpty;
            return Describe(clause, ToOutcome(isNullResult, clause.Source.IsNegated), value.Values, null, explain);
        }

        if (value.State == ConditionAttributeValueState.Missing || (!descriptor.IsMultiValued && value.Values.Count == 0))
        {
            return Describe(clause, ConditionOutcome.Indeterminate, [], Error.NotFound(SecurityErrors.ConditionAttributeMissing, SecurityMessages.ConditionAttributeMissing, MessageArguments.Of(("attribute", canonicalName))), explain);
        }

        var elements = new List<object>(value.Values.Count);
        foreach (var raw in value.Values)
        {
            if (!ConditionValues.TryConvertAttribute(raw, descriptor.Kind, timeZone, out var converted))
            {
                var invalid = Error.Validation(SecurityErrors.ConditionAttributeInvalid, SecurityMessages.ConditionAttributeInvalid, MessageArguments.Of(("attribute", canonicalName), ("kind", descriptor.Kind), ("value", ConditionValues.Format(raw))));
                return Describe(clause, ConditionOutcome.Indeterminate, value.Values, invalid, explain);
            }

            elements.Add(converted);
        }

        return Describe(clause, ToOutcome(Apply(@operator, descriptor, clause, elements), clause.Source.IsNegated), elements, null, explain);
    }

    /// <summary>
    /// 运算：多值按任一元素，否定运算符按没有元素。
    /// </summary>
    private static bool Apply(ConditionOperator @operator, ConditionAttributeDescriptor descriptor, CompiledConditionClause clause, List<object> elements)
    {
        var kind = descriptor.Kind;
        return @operator switch
        {
            ConditionOperator.Equals => elements.Exists(element => ConditionValues.AreEqual(element, clause.Scalar!, kind)),
            ConditionOperator.NotEquals => !elements.Exists(element => ConditionValues.AreEqual(element, clause.Scalar!, kind)),
            ConditionOperator.GreaterThan => elements.Exists(element => ConditionValues.Compare(element, clause.Scalar!, kind) > 0),
            ConditionOperator.GreaterThanOrEquals => elements.Exists(element => ConditionValues.Compare(element, clause.Scalar!, kind) >= 0),
            ConditionOperator.LessThan => elements.Exists(element => ConditionValues.Compare(element, clause.Scalar!, kind) < 0),
            ConditionOperator.LessThanOrEquals => elements.Exists(element => ConditionValues.Compare(element, clause.Scalar!, kind) <= 0),
            ConditionOperator.Contains => Contains(descriptor, clause, elements),
            ConditionOperator.NotContains => !Contains(descriptor, clause, elements),
            ConditionOperator.In => elements.Exists(element => clause.Items!.Any(item => ConditionValues.AreEqual(element, item, kind))),
            ConditionOperator.NotIn => !elements.Exists(element => clause.Items!.Any(item => ConditionValues.AreEqual(element, item, kind))),
            ConditionOperator.Between => elements.Exists(element => InRange(element, clause.Range!.Value.Min, clause.Range!.Value.Max, kind)),
            ConditionOperator.StartsWith => elements.Exists(element => ((string)element).StartsWith((string)clause.Scalar!, StringComparison.OrdinalIgnoreCase)),
            ConditionOperator.EndsWith => elements.Exists(element => ((string)element).EndsWith((string)clause.Scalar!, StringComparison.OrdinalIgnoreCase)),
            _ => throw new InvalidOperationException($"Condition operator '{@operator}' is not supported."),
        };
    }

    /// <summary>
    /// 包含：多值属性为列表含该值，单值字符串为子串（不区分大小写）。
    /// </summary>
    private static bool Contains(ConditionAttributeDescriptor descriptor, CompiledConditionClause clause, List<object> elements)
        => descriptor.IsMultiValued
            ? elements.Exists(element => ConditionValues.AreEqual(element, clause.Scalar!, descriptor.Kind))
            : elements.Exists(element => ((string)element).Contains((string)clause.Scalar!, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 闭区间；时刻的下限大于上限表示跨零点（落在下限之后或上限之前）。
    /// </summary>
    private static bool InRange(object value, object min, object max, ConditionValueKind kind)
    {
        if (ConditionValues.Compare(min, max, kind) > 0)
        {
            return ConditionValues.Compare(value, min, kind) >= 0 || ConditionValues.Compare(value, max, kind) <= 0;
        }

        return ConditionValues.Compare(value, min, kind) >= 0 && ConditionValues.Compare(value, max, kind) <= 0;
    }

    /// <summary>
    /// 运算结果加取反：只翻转命中与不命中。
    /// </summary>
    private static ConditionOutcome ToOutcome(bool result, bool isNegated) => result != isNegated ? ConditionOutcome.Matched : ConditionOutcome.NotMatched;

    /// <summary>
    /// 子句说明；不要说明时不格式化属性值。
    /// </summary>
    private static ConditionClauseEvaluation Describe(CompiledConditionClause clause, ConditionOutcome outcome, IReadOnlyList<object> values, Error? reason, bool explain)
        => new(
            clause.Source.Key,
            clause.Source.AttributeName,
            clause.Resolution?.CanonicalName,
            clause.Source.Operator,
            clause.Source.IsNegated,
            clause.Source.Value,
            outcome,
            explain ? [.. values.Select(ConditionValues.Format)] : [],
            reason);
}
