// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 单个任务的操作：批量方法的特例。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class WorkflowTaskServiceExtensions
{
    /// <summary>
    /// 完成一个任务。
    /// </summary>
    public static async ValueTask<Result<WorkflowInstance>> CompleteAsync(this IWorkflowTaskService service, UserTaskCompletion completion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        return (await service.CompleteAsync([completion], cancellationToken).ConfigureAwait(false))[0];
    }

    /// <summary>
    /// 转办一个任务。
    /// </summary>
    public static async ValueTask<Result> TransferAsync(this IWorkflowTaskService service, UserTaskTransfer transfer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        return (await service.TransferAsync([transfer], cancellationToken).ConfigureAwait(false))[0];
    }

    /// <summary>
    /// 给一个任务加签。
    /// </summary>
    public static async ValueTask<Result> AddAssigneesAsync(this IWorkflowTaskService service, UserTaskAddition addition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        return (await service.AddAssigneesAsync([addition], cancellationToken).ConfigureAwait(false))[0];
    }
}
