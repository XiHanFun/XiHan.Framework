// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Activities;

namespace XiHan.Framework.Workflow.Conformance.Engine;

/// <summary>
/// 套件的结果处理器。
/// </summary>
/// <remarks>
/// 记录收到的结果；结果为 <c>fail</c> 时返回失败。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class RecordingOutcomeHandler : IWorkflowOutcomeHandler
{
    /// <summary>
    /// 让处理器失败的结果。
    /// </summary>
    public const string FailingOutcome = "fail";

    /// <summary>
    /// 记录器。
    /// </summary>
    private readonly ConformanceLog _log;

    /// <summary>
    /// 创建。
    /// </summary>
    public RecordingOutcomeHandler(ConformanceLog log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <inheritdoc />
    public ValueTask<Result> HandleAsync(WorkflowOutcomeContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _log.RecordOutcome(context.Outcome);
        return ValueTask.FromResult(string.Equals(context.Outcome, FailingOutcome, StringComparison.Ordinal)
            ? Result.Failure(Error.Conflict("CONFORMANCE_CONFLICT_001", "conformance.outcome.rejected"))
            : Result.Success());
    }
}
