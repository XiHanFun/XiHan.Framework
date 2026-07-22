// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.UserTasks;

/// <summary>
/// 人工任务服务（待办查询与办理动作的统一门面）
/// </summary>
public interface IWorkflowUserTaskService
{
    /// <summary>
    /// 查询受理人的待办任务
    /// </summary>
    /// <param name="assigneeId">受理人标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>待办任务列表（按创建时间升序）</returns>
    Task<List<WorkflowUserTask>> GetPendingAsync(string assigneeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询实例的待办任务
    /// </summary>
    /// <param name="instanceId">实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>待办任务列表（按创建时间升序）</returns>
    Task<List<WorkflowUserTask>> GetPendingByInstanceAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 办理任务（同意/拒绝/自定义结果）
    /// </summary>
    /// <param name="taskId">任务标识</param>
    /// <param name="actorId">办理人标识（须与任务受理人一致）</param>
    /// <param name="outcome">办理结果（见 <see cref="WorkflowUserTaskOutcomes"/>，允许自定义）</param>
    /// <param name="comment">办理意见</param>
    /// <param name="variables">附加变量（合并进恢复输入）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>办理后的流程实例</returns>
    Task<WorkflowInstance> CompleteAsync(
        string taskId,
        string actorId,
        string outcome,
        string? comment = null,
        Dictionary<string, object?>? variables = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 转办任务（把任务移交给新受理人）
    /// </summary>
    /// <param name="taskId">任务标识</param>
    /// <param name="actorId">操作人标识（须与任务受理人一致）</param>
    /// <param name="targetAssigneeId">新受理人标识</param>
    /// <param name="comment">转办意见</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task TransferAsync(
        string taskId,
        string actorId,
        string targetAssigneeId,
        string? comment = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 加签（为当前审批节点追加受理人）
    /// </summary>
    /// <remarks>
    /// 或签/会签模式立即为新增受理人生成待办；依次审批模式把新增受理人插入剩余审批队列末尾。
    /// </remarks>
    /// <param name="taskId">任务标识</param>
    /// <param name="actorId">操作人标识（须与任务受理人一致）</param>
    /// <param name="assigneeIds">新增受理人标识集合</param>
    /// <param name="comment">加签意见</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task AddAssigneesAsync(
        string taskId,
        string actorId,
        IEnumerable<string> assigneeIds,
        string? comment = null,
        CancellationToken cancellationToken = default);
}
