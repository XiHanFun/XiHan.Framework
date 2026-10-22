// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Conditions;

/// <summary>
/// 默认条件求值器（无状态，线程安全）
/// </summary>
public class DefaultConditionEvaluator : IConditionEvaluator
{
    private static readonly ConditionEvaluationResult MatchedResult = new(ConditionOutcome.Matched, []);
    private static readonly ConditionEvaluationResult NotMatchedResult = new(ConditionOutcome.NotMatched, []);
    private static readonly ConditionEvaluationResult PendingResult = new(ConditionOutcome.Pending, []);

    /// <inheritdoc />
    public async ValueTask<ConditionEvaluationResult> EvaluateAsync(
        CompiledConditionSet set,
        IConditionAttributeSource source,
        bool explain = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();

        if (set.IsEmpty)
        {
            return MatchedResult;
        }

        var anyPending = false;
        var anyMatched = false;
        var groupEvaluations = explain ? new List<ConditionGroupEvaluation>(set.Groups.Count) : null;
        foreach (var group in set.Groups)
        {
            var (outcome, clauses) = await EvaluateGroupAsync(group, set.TimeZone, source, explain, cancellationToken);
            groupEvaluations?.Add(new ConditionGroupEvaluation(group.Key, outcome, clauses));
            if (outcome == ConditionOutcome.Matched)
            {
                anyMatched = true;
                if (!explain)
                {
                    return MatchedResult;
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
            return setOutcome == ConditionOutcome.Pending ? PendingResult : NotMatchedResult;
        }

        return new ConditionEvaluationResult(setOutcome, groupEvaluations);
    }

    private static async ValueTask<(ConditionOutcome Outcome, IReadOnlyList<ConditionClauseEvaluation> Clauses)> EvaluateGroupAsync(
        CompiledConditionGroup group,
        TimeZoneInfo timeZone,
        IConditionAttributeSource source,
        bool explain,
        CancellationToken cancellationToken)
    {
        var anyPending = false;
        var failed = false;
        var clauseEvaluations = explain ? new List<ConditionClauseEvaluation>(group.Clauses.Count) : null;
        foreach (var clause in group.Clauses)
        {
            var evaluation = await EvaluateClauseAsync(clause, timeZone, source, explain, cancellationToken);
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

    private static async ValueTask<ConditionClauseEvaluation> EvaluateClauseAsync(
        CompiledConditionClause clause,
        TimeZoneInfo timeZone,
        IConditionAttributeSource source,
        bool explain,
        CancellationToken cancellationToken)
    {
        if (clause.Error is not null || clause.Resolution is null)
        {
            return Describe(clause, ConditionOutcome.Indeterminate, [], clause.Error ?? "属性未登记", explain);
        }

        var descriptor = clause.Resolution.Descriptor;
        var value = await source.GetAsync(clause.Resolution.CanonicalName, cancellationToken);
        if (value.State == ConditionAttributeValueState.Deferred)
        {
            return Describe(clause, ConditionOutcome.Pending, [], "属性暂不可得，稍后补算", explain);
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
            return Describe(clause, ConditionOutcome.Indeterminate, [], "取不到属性值", explain);
        }

        var elements = new List<object>(value.Values.Count);
        foreach (var raw in value.Values)
        {
            if (!ConditionValues.TryConvertAttribute(raw, descriptor.Kind, timeZone, out var converted))
            {
                return Describe(clause, ConditionOutcome.Indeterminate, value.Values, $"属性值不是合法的{descriptor.Kind}：{ConditionValues.Format(raw)}", explain);
            }

            elements.Add(converted);
        }

        var result = Apply(@operator, descriptor, clause, elements);
        return Describe(clause, ToOutcome(result, clause.Source.IsNegated), elements, null, explain);
    }

    private static bool Apply(ConditionOperator @operator, ConditionAttributeDescriptor descriptor, CompiledConditionClause clause, List<object> elements)
    {
        var kind = descriptor.Kind;
        switch (@operator)
        {
            case ConditionOperator.Equals:
                return elements.Any(element => ConditionValues.AreEqual(element, clause.Scalar!, kind));
            case ConditionOperator.NotEquals:
                return !elements.Any(element => ConditionValues.AreEqual(element, clause.Scalar!, kind));
            case ConditionOperator.GreaterThan:
                return elements.Any(element => ConditionValues.Compare(element, clause.Scalar!, kind) > 0);
            case ConditionOperator.GreaterThanOrEquals:
                return elements.Any(element => ConditionValues.Compare(element, clause.Scalar!, kind) >= 0);
            case ConditionOperator.LessThan:
                return elements.Any(element => ConditionValues.Compare(element, clause.Scalar!, kind) < 0);
            case ConditionOperator.LessThanOrEquals:
                return elements.Any(element => ConditionValues.Compare(element, clause.Scalar!, kind) <= 0);
            case ConditionOperator.Contains:
                return Contains(descriptor, clause, elements);
            case ConditionOperator.NotContains:
                return !Contains(descriptor, clause, elements);
            case ConditionOperator.In:
                return elements.Any(element => clause.Items!.Any(item => ConditionValues.AreEqual(element, item, kind)));
            case ConditionOperator.NotIn:
                return !elements.Any(element => clause.Items!.Any(item => ConditionValues.AreEqual(element, item, kind)));
            case ConditionOperator.Between:
                var (min, max) = clause.Range!.Value;
                return elements.Any(element => InRange(element, min, max, kind));
            case ConditionOperator.StartsWith:
                return elements.Any(element => ((string)element).StartsWith((string)clause.Scalar!, StringComparison.OrdinalIgnoreCase));
            case ConditionOperator.EndsWith:
                return elements.Any(element => ((string)element).EndsWith((string)clause.Scalar!, StringComparison.OrdinalIgnoreCase));
            default:
                throw new InvalidOperationException($"不支持的运算符：{@operator}");
        }
    }

    private static bool Contains(ConditionAttributeDescriptor descriptor, CompiledConditionClause clause, List<object> elements)
    {
        // 多值属性：列表含该值；单值字符串：子串
        return descriptor.IsMultiValued
            ? elements.Any(element => ConditionValues.AreEqual(element, clause.Scalar!, descriptor.Kind))
            : elements.Any(element => ((string)element).Contains((string)clause.Scalar!, StringComparison.OrdinalIgnoreCase));
    }

    private static bool InRange(object value, object min, object max, ConditionValueKind kind)
    {
        var lower = ConditionValues.Compare(min, max, kind);
        // 时刻的下限大于上限表示跨零点
        if (lower > 0)
        {
            return ConditionValues.Compare(value, min, kind) >= 0 || ConditionValues.Compare(value, max, kind) <= 0;
        }

        return ConditionValues.Compare(value, min, kind) >= 0 && ConditionValues.Compare(value, max, kind) <= 0;
    }

    private static ConditionOutcome ToOutcome(bool result, bool isNegated)
    {
        return result != isNegated ? ConditionOutcome.Matched : ConditionOutcome.NotMatched;
    }

    private static ConditionClauseEvaluation Describe(
        CompiledConditionClause clause,
        ConditionOutcome outcome,
        IReadOnlyList<object> values,
        string? message,
        bool explain)
    {
        return new ConditionClauseEvaluation(
            clause.Source.Key,
            clause.Source.AttributeName,
            clause.Resolution?.CanonicalName,
            clause.Source.Operator,
            clause.Source.IsNegated,
            clause.Source.Value,
            outcome,
            explain ? [.. values.Select(ConditionValues.Format)] : [],
            message);
    }
}
