// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.Abstractions.ExternalTasks;

/// <summary>
/// 单个外部任务的操作：批量方法的特例。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class WorkflowExternalTaskServiceExtensions
{
    /// <summary>
    /// 完成一个外部任务。
    /// </summary>
    public static async ValueTask<Result<WorkflowInstance>> CompleteAsync(this IWorkflowExternalTaskService service, ExternalTaskCompletion completion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        return (await service.CompleteAsync([completion], cancellationToken).ConfigureAwait(false))[0];
    }

    /// <summary>
    /// 报告一个外部任务失败。
    /// </summary>
    public static async ValueTask<Result> FailAsync(this IWorkflowExternalTaskService service, ExternalTaskFailure failure, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        return (await service.FailAsync([failure], cancellationToken).ConfigureAwait(false))[0];
    }

    /// <summary>
    /// 延长一个外部任务的锁。
    /// </summary>
    public static async ValueTask<Result> ExtendLockAsync(this IWorkflowExternalTaskService service, ExternalTaskLockExtension extension, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        return (await service.ExtendLockAsync([extension], cancellationToken).ConfigureAwait(false))[0];
    }
}
