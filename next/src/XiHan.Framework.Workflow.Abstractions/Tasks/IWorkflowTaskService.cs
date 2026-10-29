// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.Abstractions.Tasks;

/// <summary>
/// 用户任务。
/// </summary>
/// <remarks>
/// 待办查询、完成、转办、加签。查询只含当前调用租户下、实例运行中的任务；操作都是批量、逐项结果。
/// </remarks>
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
    /// 完成任务。
    /// </summary>
    /// <remarks>
    /// 返回各任务所属实例执行后的状态。操作者不是指派人 → <see cref="WorkflowErrors.TaskDenied"/>，结果不在允许范围 → <see cref="WorkflowErrors.InputInvalid"/>。
    /// </remarks>
    ValueTask<IReadOnlyList<Result<WorkflowInstance>>> CompleteAsync(IReadOnlyList<UserTaskCompletion> completions, CancellationToken cancellationToken);

    /// <summary>
    /// 转办。
    /// </summary>
    /// <remarks>
    /// 目标已是指派人 → <see cref="WorkflowErrors.AssigneeExists"/>。
    /// </remarks>
    ValueTask<IReadOnlyList<Result>> TransferAsync(IReadOnlyList<UserTaskTransfer> transfers, CancellationToken cancellationToken);

    /// <summary>
    /// 加签。
    /// </summary>
    /// <remarks>
    /// 已是指派人的忽略，全部已是 → <see cref="WorkflowErrors.AssigneeExists"/>。
    /// </remarks>
    ValueTask<IReadOnlyList<Result>> AddAssigneesAsync(IReadOnlyList<UserTaskAddition> additions, CancellationToken cancellationToken);

    /// <summary>
    /// 按条件查询待办。
    /// </summary>
    /// <remarks>
    /// 指派人或实例必填其一；只含当前租户、实例 Running 的任务。
    /// </remarks>
    ValueTask<Result<IReadOnlyList<UserTask>>> QueryAsync(UserTaskQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// 认领（<c>Any</c> 策略）。
    /// </summary>
    /// <remarks>
    /// 其他指派人的书签撤下；已被认领 → <see cref="WorkflowErrors.AssigneeExists"/>，策略不支持 → <see cref="WorkflowErrors.InputInvalid"/>。
    /// </remarks>
    ValueTask<IReadOnlyList<Result>> ClaimAsync(IReadOnlyList<UserTaskClaim> claims, CancellationToken cancellationToken);

    /// <summary>
    /// 释放认领：恢复其他指派人的书签。
    /// </summary>
    ValueTask<IReadOnlyList<Result>> ReleaseAsync(IReadOnlyList<UserTaskRelease> releases, CancellationToken cancellationToken);

    /// <summary>
    /// 退回到上游节点。
    /// </summary>
    /// <remarks>
    /// 缺省为本节点之前最近完成的 <c>UserTask</c>，没有则 <c>Start</c> 之后的首个节点。以跳转实现。
    /// </remarks>
    ValueTask<IReadOnlyList<Result<WorkflowInstance>>> ReturnAsync(IReadOnlyList<UserTaskReturn> returns, CancellationToken cancellationToken);

    /// <summary>
    /// 催办：发布 <c>workflow.task.urged</c>，不改状态。
    /// </summary>
    ValueTask<IReadOnlyList<Result>> UrgeAsync(IReadOnlyList<UserTaskUrge> urges, CancellationToken cancellationToken);
}
