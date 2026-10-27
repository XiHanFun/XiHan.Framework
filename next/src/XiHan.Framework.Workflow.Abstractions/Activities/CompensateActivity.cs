// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 补偿：引擎在执行本节点之前对已完成的节点（<c>NodeIds</c> 指定，缺省为本节点之前全部已完成的节点）按完成顺序逆序执行补偿，被补偿节点记为 <c>Compensated</c>，然后继续流出。补偿失败只记日志，不影响继续。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class CompensateActivity : IActivity
{
    /// <summary>
    /// 属性：要补偿的节点 ID 列表；空为全部已完成的节点。
    /// </summary>
    public const string NodeIdsProperty = "NodeIds";

    /// <summary>
    /// 节点状态：本次补偿了的节点实例 ID。
    /// </summary>
    public const string CompensatedState = "compensated";

    /// <inheritdoc />
    public ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
        => ValueTask.FromResult(Result.Success(ActivityOutcome.Complete()));
}
