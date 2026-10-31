// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.Abstractions.ExternalTasks;

/// <summary>
/// 外部任务服务（RFC-0021）。
/// </summary>
/// <remarks>
/// 外部工作者按主题领取 <c>ExternalTask</c> 活动挂起的任务、完成或报告失败。作用域服务：当前租户取自本次调用。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IWorkflowExternalTaskService
{
    /// <summary>
    /// 一次领取的任务数上限。
    /// </summary>
    public const int MaxTasksPerFetch = 100;

    /// <summary>
    /// 领取。
    /// </summary>
    /// <remarks>
    /// 当前租户下主题为 <paramref name="topic"/>、未锁定（或锁已过期）且已到 <c>notBefore</c> 的任务，按创建时间最多 <paramref name="maxTasks"/> 条，锁 <paramref name="lockDuration"/>。<paramref name="maxTasks"/> 取 1 到 <see cref="MaxTasksPerFetch"/>，超出返回 <c>WORKFLOW_VALIDATION_002</c>。租户、可领取时刻与条数下推到存储查询，候选在实例锁内重读确认，被别的工作者抢先或实例已不在运行中的跳过，所以可能少于 <paramref name="maxTasks"/> 条。
    /// </remarks>
    ValueTask<Result<IReadOnlyList<ExternalTask>>> FetchAsync(string topic, string workerId, TimeSpan lockDuration, int maxTasks, CancellationToken cancellationToken);

    /// <summary>
    /// 完成。
    /// </summary>
    /// <remarks>
    /// 输出合并进实例变量后节点完成。工作者必须持有未过期的锁 → 否则 <see cref="WorkflowErrors.ExternalTaskDenied"/>。
    /// </remarks>
    ValueTask<IReadOnlyList<Result<WorkflowInstance>>> CompleteAsync(IReadOnlyList<ExternalTaskCompletion> completions, CancellationToken cancellationToken);

    /// <summary>
    /// 失败。
    /// </summary>
    /// <remarks>
    /// 有 <see cref="ExternalTaskFailure.RetryAfter"/> 时解锁并后推可领取时刻、尝试次数加一；没有时节点以该错误故障（按节点重试策略处理）。
    /// </remarks>
    ValueTask<IReadOnlyList<Result>> FailAsync(IReadOnlyList<ExternalTaskFailure> failures, CancellationToken cancellationToken);

    /// <summary>
    /// 延长锁。
    /// </summary>
    ValueTask<IReadOnlyList<Result>> ExtendLockAsync(IReadOnlyList<ExternalTaskLockExtension> extensions, CancellationToken cancellationToken);
}
