// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 并行网关：立即完成，全部出边流出。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class ParallelActivity : IActivity
{
    /// <inheritdoc />
    public ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
        => ValueTask.FromResult(Result.Success(ActivityOutcome.Complete()));
}
