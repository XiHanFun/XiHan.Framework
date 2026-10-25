// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 决策节点：立即完成，由出边（Exclusive）选分支。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DecisionActivity : IActivity
{
    /// <inheritdoc />
    public ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
        => ValueTask.FromResult(Result.Success(ActivityOutcome.Complete()));
}
