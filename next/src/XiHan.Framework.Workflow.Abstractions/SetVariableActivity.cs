// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 设变量：<c>Values</c>（名 → 字面量）与 <c>Expressions</c>（名 → 表达式）都成为输出。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class SetVariableActivity : IActivity
{
    /// <summary>
    /// 属性：字面量。
    /// </summary>
    public const string ValuesProperty = "Values";

    /// <summary>
    /// 属性：表达式。
    /// </summary>
    public const string ExpressionsProperty = "Expressions";

    /// <inheritdoc />
    public ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var outputs = WorkflowJson.CloneObject(context.Property(ValuesProperty) as JsonObject);
        if (context.Property(ExpressionsProperty) is JsonObject expressions)
        {
            var scope = context.Scope();
            foreach (var (name, expression) in expressions)
            {
                if (expression is not JsonValue value)
                {
                    return ValueTask.FromResult(Result.Failure<ActivityOutcome>(ActivityContext.PropertyInvalid(ExpressionsProperty, $"'{name}' is not an expression")));
                }

                var evaluated = context.Evaluator.Evaluate(WorkflowValues.AsString(value), scope);
                if (!evaluated.TryGetValue(out var result))
                {
                    return ValueTask.FromResult(Result.Failure<ActivityOutcome>(evaluated.Error!));
                }

                outputs[name] = result?.DeepClone();
            }
        }

        return ValueTask.FromResult(Result.Success(ActivityOutcome.Complete(outputs)));
    }
}
