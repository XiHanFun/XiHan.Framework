// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Expressions;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Assignees;

/// <summary>
/// 规则 <c>Expression</c>：恰好一个表达式，求值为列表或逗号分隔字符串。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class ExpressionAssigneeProvider : IAssigneeProvider
{
    /// <summary>
    /// 求值器。
    /// </summary>
    private readonly IWorkflowExpressionEvaluator _evaluator;

    /// <summary>
    /// 创建。
    /// </summary>
    public ExpressionAssigneeProvider(IWorkflowExpressionEvaluator evaluator)
    {
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
    }

    /// <inheritdoc />
    public string RuleType => AssigneeRuleTypes.Expression;

    /// <inheritdoc />
    public Result Validate(AssigneeRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (rule.Values.Count != 1)
        {
            return Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.AssigneeRuleInvalid, MessageArguments.Of(("type", RuleType), ("reason", "exactly one expression is required")));
        }

        return _evaluator.Validate(rule.Values[0]);
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<string>>> ResolveAsync(AssigneeResolution resolution, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        if (resolution.Rule.Values.Count != 1)
        {
            return ValueTask.FromResult(Result.Failure<IReadOnlyList<string>>(Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.AssigneeRuleInvalid, MessageArguments.Of(("type", RuleType), ("reason", "exactly one expression is required")))));
        }

        var evaluated = _evaluator.Evaluate(resolution.Rule.Values[0], resolution.Activity.Scope());
        return ValueTask.FromResult(evaluated.TryGetValue(out var value)
            ? Result.Success(WorkflowValues.ToStringList(value.OrNull()))
            : Result.Failure<IReadOnlyList<string>>(evaluated.Error!));
    }
}
