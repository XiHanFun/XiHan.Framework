// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.Abstractions.Tasks;

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

    /// <summary>
    /// 认领一个任务。
    /// </summary>
    public static async ValueTask<Result> ClaimAsync(this IWorkflowTaskService service, UserTaskClaim claim, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        return (await service.ClaimAsync([claim], cancellationToken).ConfigureAwait(false))[0];
    }

    /// <summary>
    /// 释放一个任务的认领。
    /// </summary>
    public static async ValueTask<Result> ReleaseAsync(this IWorkflowTaskService service, UserTaskRelease release, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        return (await service.ReleaseAsync([release], cancellationToken).ConfigureAwait(false))[0];
    }

    /// <summary>
    /// 退回一个任务。
    /// </summary>
    public static async ValueTask<Result<WorkflowInstance>> ReturnAsync(this IWorkflowTaskService service, UserTaskReturn request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        return (await service.ReturnAsync([request], cancellationToken).ConfigureAwait(false))[0];
    }

    /// <summary>
    /// 催办一个任务。
    /// </summary>
    public static async ValueTask<Result> UrgeAsync(this IWorkflowTaskService service, UserTaskUrge urge, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        return (await service.UrgeAsync([urge], cancellationToken).ConfigureAwait(false))[0];
    }
}
