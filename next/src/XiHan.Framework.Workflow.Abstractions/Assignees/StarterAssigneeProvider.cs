// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Assignees;

/// <summary>
/// 规则 <c>Starter</c>：实例的发起人；没有发起人解析为空。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class StarterAssigneeProvider : IAssigneeProvider
{
    /// <inheritdoc />
    public string RuleType => AssigneeRuleTypes.Starter;

    /// <inheritdoc />
    public Result Validate(AssigneeRule rule) => Result.Success();

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<string>>> ResolveAsync(AssigneeResolution resolution, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        IReadOnlyList<string> starter = string.IsNullOrWhiteSpace(resolution.Instance.StarterId) ? [] : [resolution.Instance.StarterId];
        return ValueTask.FromResult(Result.Success(starter));
    }
}
