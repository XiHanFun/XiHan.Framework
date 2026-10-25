// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 汇合节点：到达计数由引擎按 <c>Mode</c>（<c>WaitAll</c> / <c>WaitAny</c>）处理，活动本身立即完成。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class JoinActivity : IActivity
{
    /// <summary>
    /// 属性：模式。
    /// </summary>
    public const string ModeProperty = "Mode";

    /// <summary>
    /// 模式：全部入边到达才执行（默认）。
    /// </summary>
    public const string WaitAll = "WaitAll";

    /// <summary>
    /// 模式：首个入边到达即执行。
    /// </summary>
    public const string WaitAny = "WaitAny";

    /// <inheritdoc />
    public ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
        => ValueTask.FromResult(Result.Success(ActivityOutcome.Complete()));
}
