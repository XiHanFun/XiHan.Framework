// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 可从书签恢复的活动。
/// </summary>
/// <remarks>
/// 不实现它的活动被书签恢复时：超时书签即故障，其他书签以恢复输入作为输出完成。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IResumableActivity : IActivity
{
    /// <summary>
    /// 书签被消费后恢复。
    /// </summary>
    ValueTask<Result<ActivityOutcome>> ResumeAsync(ActivityResumeContext context, CancellationToken cancellationToken);
}
