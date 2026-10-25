// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 用户任务：待办查询、完成、转办、加签。查询只含当前调用租户下、实例运行中的任务；操作都是批量、逐项结果。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IWorkflowTaskService
{
    /// <summary>
    /// 某人的待办。
    /// </summary>
    ValueTask<Result<IReadOnlyList<UserTask>>> PendingAsync(string assigneeId, CancellationToken cancellationToken);

    /// <summary>
    /// 某实例的待办。
    /// </summary>
    ValueTask<Result<IReadOnlyList<UserTask>>> PendingByInstanceAsync(string instanceId, CancellationToken cancellationToken);

    /// <summary>
    /// 完成任务；返回各任务所属实例执行后的状态。操作者不是指派人 → <see cref="WorkflowErrors.TaskDenied"/>，结果不在允许范围 → <see cref="WorkflowErrors.InputInvalid"/>。
    /// </summary>
    ValueTask<IReadOnlyList<Result<WorkflowInstance>>> CompleteAsync(IReadOnlyList<UserTaskCompletion> completions, CancellationToken cancellationToken);

    /// <summary>
    /// 转办；目标已是指派人 → <see cref="WorkflowErrors.AssigneeExists"/>。
    /// </summary>
    ValueTask<IReadOnlyList<Result>> TransferAsync(IReadOnlyList<UserTaskTransfer> transfers, CancellationToken cancellationToken);

    /// <summary>
    /// 加签；已是指派人的忽略，全部已是 → <see cref="WorkflowErrors.AssigneeExists"/>。
    /// </summary>
    ValueTask<IReadOnlyList<Result>> AddAssigneesAsync(IReadOnlyList<UserTaskAddition> additions, CancellationToken cancellationToken);
}
