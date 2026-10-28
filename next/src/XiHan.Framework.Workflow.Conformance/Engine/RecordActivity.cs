// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Serialization;

namespace XiHan.Framework.Workflow.Conformance.Engine;

/// <summary>
/// 套件活动 <c>Record</c>：记录执行，以 <c>Outputs</c> 属性为输出、<c>Label</c> 属性为结果标签完成；<c>FailTimes</c> 属性让前几次尝试失败。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class RecordActivity : ICompensableActivity
{
    /// <summary>
    /// 类型名。
    /// </summary>
    public const string TypeName = "Record";

    /// <summary>
    /// 属性：输出。
    /// </summary>
    public const string OutputsProperty = "Outputs";

    /// <summary>
    /// 属性：结果标签。
    /// </summary>
    public const string LabelProperty = "Label";

    /// <summary>
    /// 属性：前几次尝试失败。
    /// </summary>
    public const string FailTimesProperty = "FailTimes";

    /// <summary>
    /// 记录器。
    /// </summary>
    private readonly ConformanceLog _log;

    /// <summary>
    /// 创建。
    /// </summary>
    public RecordActivity(ConformanceLog log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <inheritdoc />
    public ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _log.RecordExecution(context.Instance.Id, context.Node.Id, context.NodeInstance.Attempt);
        _log.RecordOrigin(context.Invocation.GetOrigin().TryGetValue(out var origin) ? origin : null);
        if (context.NodeInstance.Attempt <= context.Property(FailTimesProperty, 0))
        {
            return ValueTask.FromResult(Result.Failure<ActivityOutcome>(Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.ActivityFailed, details: MessageArguments.Of(("type", TypeName), ("reason", "flaky")))));
        }

        var outputs = WorkflowJson.CloneObject(context.Property(OutputsProperty) as JsonObject);
        return ValueTask.FromResult(Result.Success(ActivityOutcome.Complete(outputs, context.Property<string>(LabelProperty))));
    }

    /// <inheritdoc />
    public ValueTask<Result> CompensateAsync(ActivityContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _log.RecordCompensation(context.Node.Id);
        return ValueTask.FromResult(Result.Success());
    }
}
