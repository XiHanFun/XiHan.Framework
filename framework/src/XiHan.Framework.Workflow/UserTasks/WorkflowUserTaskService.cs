// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Caching.Distributed.Abstracts;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Timing;
using XiHan.Framework.Utils.Diagnostics;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Events;
using XiHan.Framework.Workflow.Abstractions.Exceptions;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Abstractions.Stores;
using XiHan.Framework.Workflow.Abstractions.UserTasks;
using XiHan.Framework.Workflow.Engine;
using XiHan.Framework.Workflow.Events;
using XiHan.Framework.Workflow.Options;

namespace XiHan.Framework.Workflow.UserTasks;

/// <summary>
/// 人工任务服务默认实现
/// </summary>
/// <remarks>
/// 待办即人工任务书签；办理经引擎书签恢复驱动流程继续，
/// 转办与加签在实例执行锁内直接修补书签与节点私有状态。
/// </remarks>
public class WorkflowUserTaskService : IWorkflowUserTaskService
{
    private const string AssigneesStateKey = "assignees";

    private readonly IWorkflowBookmarkStore _bookmarkStore;
    private readonly IWorkflowInstanceStore _instanceStore;
    private readonly IWorkflowEngine _engine;
    private readonly IWorkflowEventPublisher _eventPublisher;
    private readonly IDistributedLock _distributedLock;
    private readonly IDistributedIdGenerator<long> _idGenerator;
    private readonly IClock _clock;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<WorkflowUserTaskService> _logger;
    private readonly XiHanWorkflowOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="bookmarkStore">书签存储</param>
    /// <param name="instanceStore">实例存储</param>
    /// <param name="engine">工作流引擎</param>
    /// <param name="eventPublisher">事件发布器</param>
    /// <param name="distributedLock">分布式锁</param>
    /// <param name="idGenerator">分布式标识生成器</param>
    /// <param name="clock">时钟</param>
    /// <param name="currentTenant">当前租户</param>
    /// <param name="options">工作流选项</param>
    /// <param name="logger">日志记录器</param>
    public WorkflowUserTaskService(
        IWorkflowBookmarkStore bookmarkStore,
        IWorkflowInstanceStore instanceStore,
        IWorkflowEngine engine,
        IWorkflowEventPublisher eventPublisher,
        IDistributedLock distributedLock,
        IDistributedIdGenerator<long> idGenerator,
        IClock clock,
        ICurrentTenant currentTenant,
        IOptions<XiHanWorkflowOptions> options,
        ILogger<WorkflowUserTaskService> logger)
    {
        _bookmarkStore = bookmarkStore;
        _instanceStore = instanceStore;
        _engine = engine;
        _eventPublisher = eventPublisher;
        _distributedLock = distributedLock;
        _idGenerator = idGenerator;
        _clock = clock;
        _currentTenant = currentTenant;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<WorkflowUserTask>> GetPendingAsync(string assigneeId, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(assigneeId, nameof(assigneeId));

        var bookmarks = await _bookmarkStore.GetByKindAndKeyAsync(WorkflowBookmarkKinds.UserTask, assigneeId, cancellationToken);
        return await BuildTasksAsync(bookmarks, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<WorkflowUserTask>> GetPendingByInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(instanceId, nameof(instanceId));

        var bookmarks = await _bookmarkStore.GetByInstanceAsync(instanceId, cancellationToken);
        return await BuildTasksAsync(bookmarks.Where(item => item.Kind == WorkflowBookmarkKinds.UserTask), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkflowInstance> CompleteAsync(
        string taskId,
        string actorId,
        string outcome,
        string? comment = null,
        Dictionary<string, object?>? variables = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(taskId, nameof(taskId));
        Guard.NotNullOrWhiteSpace(actorId, nameof(actorId));
        Guard.NotNullOrWhiteSpace(outcome, nameof(outcome));

        var bookmark = await GetRequiredUserTaskBookmarkAsync(taskId, actorId, cancellationToken);

        var inputs = new Dictionary<string, object?>(variables ?? [])
        {
            [WorkflowUserTaskInputKeys.ActorId] = actorId,
            [WorkflowUserTaskInputKeys.Outcome] = outcome,
            [WorkflowUserTaskInputKeys.Comment] = comment
        };

        // 期望键在引擎锁内二次校验：与并发转办竞争时拒绝原受理人的办理
        var instance = await _engine.ResumeBookmarkAsync(
            taskId, inputs, throwIfNotResumable: true, expectedBookmarkKey: actorId, cancellationToken);

        await _eventPublisher.PublishAsync(new WorkflowUserTaskCompletedEventData(
            taskId, bookmark.InstanceId, bookmark.NodeId, actorId, outcome, comment));

        return instance;
    }

    /// <inheritdoc />
    public async Task TransferAsync(
        string taskId,
        string actorId,
        string targetAssigneeId,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(taskId, nameof(taskId));
        Guard.NotNullOrWhiteSpace(actorId, nameof(actorId));
        Guard.NotNullOrWhiteSpace(targetAssigneeId, nameof(targetAssigneeId));

        var located = await GetRequiredUserTaskBookmarkAsync(taskId, actorId, cancellationToken);

        await using (await WorkflowInstanceLocker.AcquireAsync(_distributedLock, _options, located.InstanceId, cancellationToken))
        {
            // 锁内二次校验
            var bookmark = await GetRequiredUserTaskBookmarkAsync(taskId, actorId, cancellationToken);

            // 拒绝转办给链上已有受理人：重复受理人会破坏会签/依次审批的进度推算
            var currentNodeInstance = await _instanceStore.FindNodeInstanceAsync(bookmark.NodeInstanceId, cancellationToken);
            var currentAssignees = WorkflowValueConverter.ConvertTo<List<string>>(
                currentNodeInstance?.State.GetValueOrDefault(AssigneesStateKey)) ?? [];
            if (currentAssignees.Contains(targetAssigneeId, StringComparer.Ordinal))
            {
                throw new WorkflowException($"受理人 {targetAssigneeId} 已在任务 {taskId} 的受理人列表中，不可转办");
            }

            bookmark.Key = targetAssigneeId;
            await _bookmarkStore.UpdateAsync(bookmark, cancellationToken);
            await ReplaceStateAssigneeAsync(bookmark, actorId, targetAssigneeId, cancellationToken);

            await _eventPublisher.PublishAsync(new WorkflowUserTaskTransferredEventData(
                taskId, bookmark.InstanceId, actorId, targetAssigneeId, comment));

            _logger.LogInformation("人工任务 {TaskId} 由 {ActorId} 转办给 {TargetAssigneeId}", taskId, actorId, targetAssigneeId);
        }
    }

    /// <inheritdoc />
    public async Task AddAssigneesAsync(
        string taskId,
        string actorId,
        IEnumerable<string> assigneeIds,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(taskId, nameof(taskId));
        Guard.NotNullOrWhiteSpace(actorId, nameof(actorId));
        Guard.NotNull(assigneeIds, nameof(assigneeIds));

        var located = await GetRequiredUserTaskBookmarkAsync(taskId, actorId, cancellationToken);

        await using (await WorkflowInstanceLocker.AcquireAsync(_distributedLock, _options, located.InstanceId, cancellationToken))
        {
            var bookmark = await GetRequiredUserTaskBookmarkAsync(taskId, actorId, cancellationToken);

            var nodeInstance = await _instanceStore.FindNodeInstanceAsync(bookmark.NodeInstanceId, cancellationToken)
                ?? throw new WorkflowException($"任务 {taskId} 关联的节点实例 {bookmark.NodeInstanceId} 不存在");
            var instance = await _instanceStore.FindAsync(bookmark.InstanceId, cancellationToken)
                ?? throw new WorkflowException($"任务 {taskId} 关联的实例 {bookmark.InstanceId} 不存在");

            var assignees = WorkflowValueConverter.ConvertTo<List<string>>(
                nodeInstance.State.GetValueOrDefault(AssigneesStateKey)) ?? [];
            var added = assigneeIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .Except(assignees, StringComparer.Ordinal)
                .ToList();

            if (added.Count == 0)
            {
                return;
            }

            assignees.AddRange(added);
            nodeInstance.State[AssigneesStateKey] = assignees;
            await _instanceStore.UpdateNodeInstanceAsync(nodeInstance, cancellationToken);

            // 依次审批：加签受理人进入剩余队列，待轮到时生成待办；或签/会签：立即生成待办
            var policy = WorkflowValueConverter.ConvertTo<string>(nodeInstance.State.GetValueOrDefault("policy"));
            if (!string.Equals(policy, nameof(UserTaskCompletionPolicy.Sequential), StringComparison.OrdinalIgnoreCase))
            {
                foreach (var assignee in added)
                {
                    var newBookmark = new WorkflowBookmark
                    {
                        Id = _idGenerator.NextIdString(),
                        InstanceId = bookmark.InstanceId,
                        NodeId = bookmark.NodeId,
                        NodeInstanceId = bookmark.NodeInstanceId,
                        Kind = WorkflowBookmarkKinds.UserTask,
                        Key = assignee,
                        Payload = new Dictionary<string, object?>(bookmark.Payload),
                        CorrelationId = bookmark.CorrelationId,
                        CreationTime = _clock.Now,
                        TenantId = bookmark.TenantId
                    };
                    await _bookmarkStore.InsertAsync(newBookmark, cancellationToken);
                    await _eventPublisher.PublishAsync(new WorkflowUserTaskCreatedEventData(
                        WorkflowUserTaskMapper.ToUserTask(newBookmark, instance), []));
                }
            }

            _logger.LogInformation("人工任务 {TaskId} 由 {ActorId} 加签 {Added}（意见：{Comment}）",
                taskId, actorId, string.Join(",", added), comment);
        }
    }

    private async Task<WorkflowBookmark> GetRequiredUserTaskBookmarkAsync(string taskId, string actorId, CancellationToken cancellationToken)
    {
        var bookmark = await _bookmarkStore.FindAsync(taskId, cancellationToken)
            ?? throw new WorkflowException($"任务 {taskId} 不存在或已被办理");

        if (bookmark.Kind != WorkflowBookmarkKinds.UserTask)
        {
            throw new WorkflowException($"书签 {taskId} 不是人工任务");
        }

        return !string.Equals(bookmark.Key, actorId, StringComparison.Ordinal)
            ? throw new WorkflowException($"操作人 {actorId} 不是任务 {taskId} 的受理人")
            : bookmark;
    }

    private async Task ReplaceStateAssigneeAsync(WorkflowBookmark bookmark, string from, string to, CancellationToken cancellationToken)
    {
        var nodeInstance = await _instanceStore.FindNodeInstanceAsync(bookmark.NodeInstanceId, cancellationToken);
        if (nodeInstance is null)
        {
            return;
        }

        var assignees = WorkflowValueConverter.ConvertTo<List<string>>(
            nodeInstance.State.GetValueOrDefault(AssigneesStateKey)) ?? [];
        var index = assignees.IndexOf(from);
        if (index >= 0)
        {
            assignees[index] = to;
            nodeInstance.State[AssigneesStateKey] = assignees;
            await _instanceStore.UpdateNodeInstanceAsync(nodeInstance, cancellationToken);
        }
    }

    private async Task<List<WorkflowUserTask>> BuildTasksAsync(IEnumerable<WorkflowBookmark> bookmarks, CancellationToken cancellationToken)
    {
        var tasks = new List<WorkflowUserTask>();

        foreach (var bookmark in bookmarks)
        {
            // 租户隔离：存在环境租户时不返回其他租户的待办（跨租户重名受理人场景）
            if (_currentTenant.Id is { } tenantId && bookmark.TenantId != tenantId)
            {
                continue;
            }

            var instance = await _instanceStore.FindAsync(bookmark.InstanceId, cancellationToken);
            if (instance is null || instance.Status != WorkflowInstanceStatus.Running)
            {
                continue;
            }

            tasks.Add(WorkflowUserTaskMapper.ToUserTask(bookmark, instance));
        }

        return tasks;
    }
}
