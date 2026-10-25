// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Assignees;

/// <summary>
/// 规则 <c>User</c>：参数就是指派人，至少一个。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class UserAssigneeProvider : IAssigneeProvider
{
    /// <inheritdoc />
    public string RuleType => AssigneeRuleTypes.User;

    /// <inheritdoc />
    public Result Validate(AssigneeRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return rule.Values.Count > 0
            ? Result.Success()
            : Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.AssigneeRuleInvalid, MessageArguments.Of(("type", RuleType), ("reason", "at least one user is required")));
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<string>>> ResolveAsync(AssigneeResolution resolution, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        return ValueTask.FromResult(Result.Success(resolution.Rule.Values));
    }
}
