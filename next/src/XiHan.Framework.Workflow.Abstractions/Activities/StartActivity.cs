// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 开始节点：直接完成。延迟启动（<c>WorkflowStartRequest.StartAt</c>）时由引擎以 Timer 书签挂起，到期恢复后完成。属性 <c>SignalName</c>（信号启动）与 <c>Cron</c> / <c>TimeZone</c>（定时启动）由引擎与目录解释。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class StartActivity : IResumableActivity
{
    /// <summary>
    /// 属性：信号启动的信号名。
    /// </summary>
    public const string SignalNameProperty = "SignalName";

    /// <summary>
    /// 属性：定时启动的 cron 表达式。
    /// </summary>
    public const string CronProperty = "Cron";

    /// <summary>
    /// 属性：定时启动的时区 ID。
    /// </summary>
    public const string TimeZoneProperty = "TimeZone";

    /// <inheritdoc />
    public ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
        => ValueTask.FromResult(Result.Success(ActivityOutcome.Complete()));

    /// <inheritdoc />
    public ValueTask<Result<ActivityOutcome>> ResumeAsync(ActivityResumeContext context, CancellationToken cancellationToken)
        => ValueTask.FromResult(Result.Success(ActivityOutcome.Complete()));
}
