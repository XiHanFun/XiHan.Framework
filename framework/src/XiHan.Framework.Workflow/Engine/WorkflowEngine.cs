// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Caching.Distributed.Abstracts;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Timing;
using XiHan.Framework.Utils.Diagnostics;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Events;
using XiHan.Framework.Workflow.Abstractions.Exceptions;
using XiHan.Framework.Workflow.Abstractions.Expressions;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Abstractions.Stores;
using XiHan.Framework.Workflow.Activities;
using XiHan.Framework.Workflow.Events;
using XiHan.Framework.Workflow.Options;
using XiHan.Framework.Workflow.UserTasks;

namespace XiHan.Framework.Workflow.Engine;

/// <summary>
/// 工作流引擎默认实现（令牌驱动的图解释执行）
/// </summary>
/// <remarks>
/// 并发模型：对同一实例的所有操作先获取实例级分布式锁（单写者，持锁自动续期），执行批次内逐节点推进并持久化；
/// 子流程启动与父实例回调等跨实例动作收集为后置动作，待释放锁后执行，避免父子锁重入死锁。
/// 取消语义：调用方取消令牌仅在书签消费前生效；批次一旦开始即以引擎内部令牌推进并保证收尾持久化，
/// 途中取消按节点故障处理（可重试），杜绝"书签已消费但状态未落盘"的悬空实例。
/// 引擎不接管数据库事务，业务活动自行管理事务边界。
/// </remarks>
public class WorkflowEngine : IWorkflowEngine
{
    private const int NotifyParentLockRetryCount = 5;

    private readonly IWorkflowDefinitionStore _definitionStore;
    private readonly IWorkflowInstanceStore _instanceStore;
    private readonly IWorkflowBookmarkStore _bookmarkStore;
    private readonly IWorkflowActivityRegistry _activityRegistry;
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly IWorkflowEventPublisher _eventPublisher;
    private readonly IDistributedIdGenerator<long> _idGenerator;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;
    private readonly ICurrentTenant _currentTenant;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkflowEngine> _logger;
    private readonly XiHanWorkflowOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="definitionStore">定义存储</param>
    /// <param name="instanceStore">实例存储</param>
    /// <param name="bookmarkStore">书签存储</param>
    /// <param name="activityRegistry">活动注册表</param>
    /// <param name="expressionEvaluator">表达式求值器</param>
    /// <param name="eventPublisher">事件发布器</param>
    /// <param name="idGenerator">分布式标识生成器</param>
    /// <param name="distributedLock">分布式锁</param>
    /// <param name="clock">时钟</param>
    /// <param name="currentTenant">当前租户</param>
    /// <param name="serviceProvider">服务提供者</param>
    /// <param name="options">工作流选项</param>
    /// <param name="logger">日志记录器</param>
    public WorkflowEngine(
        IWorkflowDefinitionStore definitionStore,
        IWorkflowInstanceStore instanceStore,
        IWorkflowBookmarkStore bookmarkStore,
        IWorkflowActivityRegistry activityRegistry,
        IWorkflowExpressionEvaluator expressionEvaluator,
        IWorkflowEventPublisher eventPublisher,
        IDistributedIdGenerator<long> idGenerator,
        IDistributedLock distributedLock,
        IClock clock,
        ICurrentTenant currentTenant,
        IServiceProvider serviceProvider,
        IOptions<XiHanWorkflowOptions> options,
        ILogger<WorkflowEngine> logger)
    {
        _definitionStore = definitionStore;
        _instanceStore = instanceStore;
        _bookmarkStore = bookmarkStore;
        _activityRegistry = activityRegistry;
        _expressionEvaluator = expressionEvaluator;
        _eventPublisher = eventPublisher;
        _idGenerator = idGenerator;
        _distributedLock = distributedLock;
        _clock = clock;
        _currentTenant = currentTenant;
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<WorkflowInstance> StartAsync(WorkflowStartRequest request, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request, nameof(request));

        if (request.Depth > _options.MaxSubWorkflowDepth)
        {
            throw new WorkflowException($"子流程嵌套深度 {request.Depth} 超过上限 {_options.MaxSubWorkflowDepth}，疑似递归定义失控");
        }

        var definition = await ResolveDefinitionAsync(request, cancellationToken);
        if (definition.Status != WorkflowDefinitionStatus.Published)
        {
            throw new WorkflowException($"流程定义 {definition.Code} v{definition.Version} 未发布，不可启动实例");
        }

        var now = _clock.Now;
        var variables = BuildStartVariables(definition, request);

        var instance = new WorkflowInstance
        {
            Id = string.IsNullOrWhiteSpace(request.InstanceId) ? _idGenerator.NextIdString() : request.InstanceId,
            DefinitionId = definition.Id,
            DefinitionCode = definition.Code,
            DefinitionVersion = definition.Version,
            Name = string.IsNullOrWhiteSpace(request.Name) ? definition.Name : request.Name,
            Status = WorkflowInstanceStatus.Running,
            Variables = variables,
            CorrelationId = request.CorrelationId,
            StarterId = request.StarterId,
            ParentInstanceId = request.ParentInstanceId,
            ParentNodeInstanceId = request.ParentNodeInstanceId,
            Depth = request.Depth,
            TenantId = _currentTenant.Id ?? definition.TenantId,
            CreationTime = now,
            StartTime = now
        };

        await _instanceStore.InsertAsync(instance, cancellationToken);

        var postActions = new List<Func<Task>>();

        await using (await AcquireInstanceLockAsync(instance.Id, cancellationToken))
        {
            var session = new ExecutionSession(definition, new WorkflowDefinitionGraph(definition), instance, postActions);
            await _eventPublisher.PublishAsync(new WorkflowInstanceStartedEventData(instance));

            session.Queue.Enqueue(WorkItem.Fresh(session.Graph.StartNode.Id, null));
            await RunBurstAsync(session);
        }

        await RunPostActionsAsync(postActions);
        return instance;
    }

    /// <inheritdoc />
    public async Task<WorkflowInstance> ResumeBookmarkAsync(
        string bookmarkId,
        Dictionary<string, object?>? inputs = null,
        bool throwIfNotResumable = true,
        string? expectedBookmarkKey = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(bookmarkId, nameof(bookmarkId));

        var bookmark = await _bookmarkStore.FindAsync(bookmarkId, cancellationToken)
            ?? throw new WorkflowException($"书签 {bookmarkId} 不存在或已被处理");

        var (instance, _) = await ResumeBookmarkCoreAsync(
            bookmark.InstanceId, bookmarkId, inputs, throwIfNotResumable, expectedBookmarkKey, cancellationToken);
        return instance;
    }

    /// <inheritdoc />
    public async Task<int> PublishSignalAsync(
        string signalName,
        Dictionary<string, object?>? payload = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(signalName, nameof(signalName));

        var bookmarks = await _bookmarkStore.GetBySignalAsync(signalName, correlationId, cancellationToken);

        // 租户隔离：存在环境租户时只投递本租户的信号书签
        if (_currentTenant.Id is { } tenantId)
        {
            bookmarks = [.. bookmarks.Where(item => item.TenantId == tenantId)];
        }

        var resumedCount = 0;

        foreach (var bookmark in bookmarks)
        {
            // 锁竞争是瞬态冲突，重试而不是把一次性信号当作已消费丢弃
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    var (_, consumed) = await ResumeBookmarkCoreAsync(
                        bookmark.InstanceId, bookmark.Id, payload,
                        throwIfNotResumable: false, expectedBookmarkKey: null, cancellationToken);
                    if (consumed)
                    {
                        resumedCount++;
                    }

                    break;
                }
                catch (WorkflowLockTimeoutException) when (attempt < NotifyParentLockRetryCount)
                {
                    _logger.LogWarning("信号 {SignalName} 投递书签 {BookmarkId} 遇到锁竞争，第 {Attempt} 次重试", signalName, bookmark.Id, attempt);
                }
                catch (WorkflowException ex)
                {
                    // 书签已被并发消费或实例已消失，跳过
                    _logger.LogDebug(ex, "信号 {SignalName} 恢复书签 {BookmarkId} 被跳过", signalName, bookmark.Id);
                    break;
                }
            }
        }

        return resumedCount;
    }

    /// <inheritdoc />
    public async Task<WorkflowInstance> SuspendAsync(string instanceId, string? reason = null, CancellationToken cancellationToken = default)
    {
        await using (await AcquireInstanceLockAsync(instanceId, cancellationToken))
        {
            var instance = await GetRequiredInstanceAsync(instanceId, cancellationToken);
            if (instance.Status != WorkflowInstanceStatus.Running)
            {
                throw new WorkflowException($"实例 {instanceId} 状态为 {instance.Status}，仅运行中实例可挂起");
            }

            instance.Status = WorkflowInstanceStatus.Suspended;
            await _instanceStore.UpdateAsync(instance, cancellationToken);
            await _eventPublisher.PublishAsync(new WorkflowInstanceSuspendedEventData(instance, reason));
            return instance;
        }
    }

    /// <inheritdoc />
    public async Task<WorkflowInstance> ResumeAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        await using (await AcquireInstanceLockAsync(instanceId, cancellationToken))
        {
            var instance = await GetRequiredInstanceAsync(instanceId, cancellationToken);
            if (instance.Status != WorkflowInstanceStatus.Suspended)
            {
                throw new WorkflowException($"实例 {instanceId} 状态为 {instance.Status}，仅挂起实例可恢复运行");
            }

            instance.Status = WorkflowInstanceStatus.Running;
            await _instanceStore.UpdateAsync(instance, cancellationToken);
            await _eventPublisher.PublishAsync(new WorkflowInstanceResumedEventData(instance));
            return instance;
        }
    }

    /// <inheritdoc />
    public async Task<WorkflowInstance> CancelAsync(string instanceId, string? reason = null, CancellationToken cancellationToken = default)
    {
        return await FinishForciblyAsync(instanceId, WorkflowInstanceStatus.Canceled, reason, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkflowInstance> TerminateAsync(string instanceId, string? reason = null, CancellationToken cancellationToken = default)
    {
        return await FinishForciblyAsync(instanceId, WorkflowInstanceStatus.Terminated, reason, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkflowInstance> RetryAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        var postActions = new List<Func<Task>>();
        WorkflowInstance instance;

        await using (await AcquireInstanceLockAsync(instanceId, cancellationToken))
        {
            instance = await GetRequiredInstanceAsync(instanceId, cancellationToken);
            if (instance.Status != WorkflowInstanceStatus.Faulted)
            {
                throw new WorkflowException($"实例 {instanceId} 状态为 {instance.Status}，仅故障实例可重试");
            }

            if (string.IsNullOrEmpty(instance.FaultNodeInstanceId))
            {
                throw new WorkflowException($"实例 {instanceId} 缺少故障节点记录，无法重试");
            }

            var nodeInstance = await _instanceStore.FindNodeInstanceAsync(instance.FaultNodeInstanceId, cancellationToken)
                ?? throw new WorkflowException($"实例 {instanceId} 的故障节点实例 {instance.FaultNodeInstanceId} 不存在");

            var definition = await GetRequiredDefinitionAsync(instance, cancellationToken);
            var session = new ExecutionSession(definition, new WorkflowDefinitionGraph(definition), instance, postActions);

            instance.Status = WorkflowInstanceStatus.Running;
            instance.EndTime = null;
            instance.FaultMessage = null;
            instance.FaultNodeId = null;
            instance.FaultNodeInstanceId = null;
            await _instanceStore.UpdateAsync(instance, cancellationToken);
            await _eventPublisher.PublishAsync(new WorkflowInstanceResumedEventData(instance));

            session.Queue.Enqueue(WorkItem.Retry(nodeInstance.NodeId, nodeInstance));
            await RunBurstAsync(session);
        }

        await RunPostActionsAsync(postActions);
        return instance;
    }

    #region 书签恢复核心

    private async Task<(WorkflowInstance Instance, bool Consumed)> ResumeBookmarkCoreAsync(
        string instanceId,
        string bookmarkId,
        Dictionary<string, object?>? inputs,
        bool throwIfNotResumable,
        string? expectedBookmarkKey,
        CancellationToken cancellationToken)
    {
        var postActions = new List<Func<Task>>();
        WorkflowInstance instance;
        bool consumed;

        await using (await AcquireInstanceLockAsync(instanceId, cancellationToken))
        {
            // 锁内二次校验，避免并发消费
            var bookmark = await _bookmarkStore.FindAsync(bookmarkId, cancellationToken)
                ?? throw new WorkflowException($"书签 {bookmarkId} 不存在或已被处理");

            if (expectedBookmarkKey is not null && !string.Equals(bookmark.Key, expectedBookmarkKey, StringComparison.Ordinal))
            {
                throw new WorkflowException($"书签 {bookmarkId} 的索引键已变化（如任务已转办），恢复被拒绝");
            }

            var found = await _instanceStore.FindAsync(instanceId, cancellationToken);
            if (found is null)
            {
                // 孤儿书签清理：所属实例已不存在，保留只会让定时轮询永久空转
                await _bookmarkStore.DeleteAsync(bookmarkId, CancellationToken.None);
                throw new WorkflowException($"书签 {bookmarkId} 所属实例 {instanceId} 不存在，书签已清理");
            }

            instance = found;
            consumed = await TryConsumeBookmarkAsync(instance, bookmark, inputs, throwIfNotResumable, postActions, cancellationToken);
        }

        await RunPostActionsAsync(postActions);
        return (instance, consumed);
    }

    /// <summary>
    /// 子流程终态回调（书签可能因会签/遍历再挂起而更换标识，须在父锁内按种类与索引键重查；锁竞争重试）
    /// </summary>
    private async Task NotifyParentAsync(
        string parentInstanceId,
        string parentNodeInstanceId,
        Dictionary<string, object?> inputs)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await NotifyParentOnceAsync(parentInstanceId, parentNodeInstanceId, inputs);
                return;
            }
            catch (WorkflowLockTimeoutException) when (attempt < NotifyParentLockRetryCount)
            {
                _logger.LogWarning("父实例 {ParentId} 回调遇到锁竞争，第 {Attempt} 次重试", parentInstanceId, attempt);
                await Task.Delay(TimeSpan.FromSeconds(attempt));
            }
        }
    }

    private async Task NotifyParentOnceAsync(
        string parentInstanceId,
        string parentNodeInstanceId,
        Dictionary<string, object?> inputs)
    {
        var postActions = new List<Func<Task>>();

        await using (await AcquireInstanceLockAsync(parentInstanceId, CancellationToken.None))
        {
            var parent = await _instanceStore.FindAsync(parentInstanceId, CancellationToken.None);
            if (parent is null)
            {
                _logger.LogWarning("父实例 {ParentId} 不存在，跳过子流程回调", parentInstanceId);
                return;
            }

            var bookmarks = await _bookmarkStore.GetByKindAndKeyAsync(
                WorkflowBookmarkKinds.SubWorkflow, parentNodeInstanceId, CancellationToken.None);
            var bookmark = bookmarks.FirstOrDefault();
            if (bookmark is null)
            {
                // 父节点未等待子流程（发后不理模式或节点已结束）
                return;
            }

            await TryConsumeBookmarkAsync(parent, bookmark, inputs, throwIfNotResumable: false, postActions, CancellationToken.None);
        }

        await RunPostActionsAsync(postActions);
    }

    private Dictionary<string, object?> BuildChildCallbackInputs(WorkflowInstance childInstance)
    {
        return new Dictionary<string, object?>
        {
            [WorkflowConsts.ChildInstanceIdInputKey] = childInstance.Id,
            [WorkflowConsts.ChildStatusInputKey] = childInstance.Status.ToString(),
            [WorkflowConsts.ChildVariablesInputKey] = new Dictionary<string, object?>(childInstance.Variables),
            [WorkflowConsts.ChildFaultMessageInputKey] = childInstance.FaultMessage
        };
    }

    private async Task<bool> TryConsumeBookmarkAsync(
        WorkflowInstance instance,
        WorkflowBookmark bookmark,
        Dictionary<string, object?>? inputs,
        bool throwIfNotResumable,
        List<Func<Task>> postActions,
        CancellationToken cancellationToken)
    {
        if (instance.Status != WorkflowInstanceStatus.Running)
        {
            if (throwIfNotResumable)
            {
                throw new WorkflowException($"实例 {instance.Id} 状态为 {instance.Status}，书签 {bookmark.Id} 不可恢复");
            }

            // 定时类书签到期回退，避免挂起/故障实例的到期书签每轮占满轮询配额
            if (bookmark.DueTime.HasValue)
            {
                bookmark.DueTime = _clock.Now.AddSeconds(_options.NotResumableTimerBackoffSeconds);
                await _bookmarkStore.UpdateAsync(bookmark, CancellationToken.None);
            }

            _logger.LogDebug("实例 {InstanceId} 状态为 {Status}，保留书签 {BookmarkId} 待后续恢复", instance.Id, instance.Status, bookmark.Id);
            return false;
        }

        var nodeInstance = await _instanceStore.FindNodeInstanceAsync(bookmark.NodeInstanceId, cancellationToken)
            ?? throw new WorkflowException($"书签 {bookmark.Id} 关联的节点实例 {bookmark.NodeInstanceId} 不存在");

        // 波次隔离：旧波次子实例（重试前启动）的回调不允许消费新波次的等待点
        if (bookmark.Kind == WorkflowBookmarkKinds.SubWorkflow
            && inputs?.GetValueOrDefault(WorkflowConsts.ChildInstanceIdInputKey) is { } rawChildId
            && WorkflowValueConverter.ConvertTo<string>(rawChildId) is { } childId)
        {
            var currentChildIds = WorkflowValueConverter.ConvertTo<List<string>>(
                nodeInstance.State.GetValueOrDefault(WorkflowConsts.ChildInstanceIdsStateKey)) ?? [];
            if (!currentChildIds.Contains(childId, StringComparer.Ordinal))
            {
                _logger.LogWarning("实例 {InstanceId} 节点实例 {NodeInstanceId} 忽略旧波次子实例 {ChildId} 的回调",
                    instance.Id, nodeInstance.Id, childId);
                return false;
            }
        }

        var definition = await GetRequiredDefinitionAsync(instance, cancellationToken);
        var session = new ExecutionSession(definition, new WorkflowDefinitionGraph(definition), instance, postActions);

        // 消费书签；此后批次必须走到收尾持久化，不再受调用方取消影响
        await _bookmarkStore.DeleteAsync(bookmark.Id, CancellationToken.None);

        session.Queue.Enqueue(bookmark.Kind == WorkflowBookmarkKinds.Retry
            ? WorkItem.Retry(nodeInstance.NodeId, nodeInstance)
            : WorkItem.Resume(nodeInstance.NodeId, nodeInstance, bookmark, inputs));

        await RunBurstAsync(session);
        return true;
    }

    #endregion 书签恢复核心

    #region 执行批次

    private async Task RunBurstAsync(ExecutionSession session)
    {
        var executedCount = 0;

        while (session.Queue.Count > 0 && session.Instance.Status == WorkflowInstanceStatus.Running)
        {
            if (++executedCount > _options.MaxNodeExecutionsPerBurst)
            {
                await FaultInstanceAsync(session, null, null,
                    $"单次执行批次超过最大节点执行数 {_options.MaxNodeExecutionsPerBurst}，疑似流程定义存在失控环路");
                break;
            }

            var item = session.Queue.Dequeue();

            try
            {
                await ExecuteNodeAsync(session, item);
            }
            catch (Exception ex)
            {
                // 引擎级异常（表达式非法、活动解析失败、途中取消等）按 fail-closed 语义故障实例，保证批次可收尾
                _logger.LogError(ex, "实例 {InstanceId} 执行节点 {NodeId} 发生引擎级异常", session.Instance.Id, item.NodeId);
                await FaultInstanceAsync(session, item.NodeId, null, ex.Message);
                break;
            }
        }

        await FinalizeBurstAsync(session);
    }

    private async Task ExecuteNodeAsync(ExecutionSession session, WorkItem item)
    {
        var node = session.Graph.GetRequiredNode(item.NodeId);
        var now = _clock.Now;
        WorkflowNodeInstance nodeInstance;

        if (item.NodeInstance is null)
        {
            nodeInstance = new WorkflowNodeInstance
            {
                Id = _idGenerator.NextIdString(),
                InstanceId = session.Instance.Id,
                NodeId = node.Id,
                Name = node.Name,
                ActivityType = node.ActivityType,
                Status = WorkflowNodeInstanceStatus.Running,
                TryCount = 1,
                StartTime = now,
                Inputs = item.Inputs ?? [],
                TenantId = session.Instance.TenantId
            };
            await _instanceStore.InsertNodeInstanceAsync(nodeInstance, CancellationToken.None);
        }
        else
        {
            nodeInstance = item.NodeInstance;
            nodeInstance.Status = WorkflowNodeInstanceStatus.Running;
            if (item.IsRetry)
            {
                nodeInstance.TryCount++;
                nodeInstance.FaultMessage = null;

                // 重试是新波次：清掉旧波次残留书签与子实例登记，旧波次回调随之失效
                await DeleteNodeBookmarksAsync(nodeInstance.Id);
                nodeInstance.State.Remove(WorkflowConsts.ChildInstanceIdsStateKey);
            }

            if (item.Bookmark is not null)
            {
                nodeInstance.Inputs = item.Inputs ?? [];
            }

            await _instanceStore.UpdateNodeInstanceAsync(nodeInstance, CancellationToken.None);
        }

        await _eventPublisher.PublishAsync(new WorkflowNodeExecutingEventData(session.Instance, nodeInstance));

        var descriptor = _activityRegistry.Get(node.ActivityType);
        var activity = (IWorkflowActivity)_serviceProvider.GetRequiredService(descriptor.ClrType);

        ActivityExecutionResult result;
        try
        {
            result = await InvokeActivityAsync(session, node, nodeInstance, activity, item);
        }
        catch (Exception ex)
        {
            // 含 OperationCanceledException：活动执行途中被取消同样按节点故障处理（可重试），不允许中断批次收尾
            _logger.LogError(ex, "实例 {InstanceId} 节点 {NodeId}（{ActivityType}）执行异常",
                session.Instance.Id, node.Id, node.ActivityType);
            result = ActivityExecutionResult.Fault(ex.Message);
        }

        switch (result.Kind)
        {
            case ActivityExecutionResultKind.Completed:
                await HandleCompletedAsync(session, node, nodeInstance, descriptor, result);
                break;

            case ActivityExecutionResultKind.Suspended:
                await HandleSuspendedAsync(session, node, nodeInstance, result, isInitialSuspend: item.Bookmark is null);
                break;

            case ActivityExecutionResultKind.Faulted:
                await HandleFaultedAsync(session, node, nodeInstance, descriptor, result.FaultMessage ?? "活动执行故障");
                break;

            default:
                throw new WorkflowException($"未知的活动执行结果种类 {result.Kind}");
        }
    }

    private async Task<ActivityExecutionResult> InvokeActivityAsync(
        ExecutionSession session,
        WorkflowNode node,
        WorkflowNodeInstance nodeInstance,
        IWorkflowActivity activity,
        WorkItem item)
    {
        if (item.Bookmark is null)
        {
            var context = CreateContext(session, node, nodeInstance);
            return await activity.ExecuteAsync(context);
        }

        // 节点超时恢复统一注入超时标记，供可恢复活动识别
        var inputs = item.Inputs ?? [];
        if (item.Bookmark.Kind == WorkflowBookmarkKinds.NodeTimeout)
        {
            inputs = new Dictionary<string, object?>(inputs)
            {
                [WorkflowConsts.TimeoutInputKey] = true
            };
        }

        if (activity is IResumableWorkflowActivity resumable)
        {
            var resumeContext = new ActivityResumeContext
            {
                Definition = session.Definition,
                Instance = session.Instance,
                Node = node,
                NodeInstance = nodeInstance,
                Variables = session.Variables,
                ServiceProvider = _serviceProvider,
                CancellationToken = session.CancellationToken,
                Bookmark = item.Bookmark,
                Inputs = inputs
            };
            return await resumable.ResumeAsync(resumeContext);
        }

        // 非可恢复活动：超时书签按故障处理，其余书签按默认恢复语义（输入即输出）完成
        return item.Bookmark.Kind == WorkflowBookmarkKinds.NodeTimeout
            ? ActivityExecutionResult.Fault($"节点 {node.Id} 等待超时（{node.TimeoutSeconds} 秒）")
            : ActivityExecutionResult.Complete(new Dictionary<string, object?>(inputs));
    }

    private async Task HandleCompletedAsync(
        ExecutionSession session,
        WorkflowNode node,
        WorkflowNodeInstance nodeInstance,
        WorkflowActivityDescriptor descriptor,
        ActivityExecutionResult result)
    {
        nodeInstance.Status = WorkflowNodeInstanceStatus.Completed;
        nodeInstance.EndTime = _clock.Now;
        nodeInstance.Outputs = new Dictionary<string, object?>(result.Outputs);
        if (result.Outcome is not null)
        {
            nodeInstance.Outputs[WorkflowConsts.OutcomeVariableName] = result.Outcome;
        }

        session.Variables.Merge(result.Outputs);

        // 子流程启动登记须在节点实例持久化前写入状态
        SchedulePostBurstChildStarts(session, nodeInstance, result);

        // 节点离开挂起态，清掉剩余的兄弟书签（含超时书签）
        await DeleteNodeBookmarksAsync(nodeInstance.Id);
        await _instanceStore.UpdateNodeInstanceAsync(nodeInstance, CancellationToken.None);
        await _eventPublisher.PublishAsync(new WorkflowNodeExecutedEventData(session.Instance, nodeInstance));

        // 终止类活动会把实例置为终态，不再流转
        if (session.Instance.Status != WorkflowInstanceStatus.Running)
        {
            return;
        }

        await FlowOutgoingAsync(session, node, nodeInstance, descriptor.OutgoingBehavior, result.Outcome);
    }

    private async Task HandleSuspendedAsync(
        ExecutionSession session,
        WorkflowNode node,
        WorkflowNodeInstance nodeInstance,
        ActivityExecutionResult result,
        bool isInitialSuspend)
    {
        nodeInstance.Status = WorkflowNodeInstanceStatus.Suspended;

        // 子流程启动登记须在节点实例持久化前写入状态
        SchedulePostBurstChildStarts(session, nodeInstance, result);
        await _instanceStore.UpdateNodeInstanceAsync(nodeInstance, CancellationToken.None);

        foreach (var request in result.Bookmarks)
        {
            var bookmark = new WorkflowBookmark
            {
                Id = _idGenerator.NextIdString(),
                InstanceId = session.Instance.Id,
                NodeId = node.Id,
                NodeInstanceId = nodeInstance.Id,
                Kind = request.Kind,
                Key = request.Key,
                Payload = new Dictionary<string, object?>(request.Payload),
                DueTime = request.DueTime,
                CorrelationId = request.CorrelationId,
                CreationTime = _clock.Now,
                TenantId = session.Instance.TenantId
            };
            await _bookmarkStore.InsertAsync(bookmark, CancellationToken.None);

            if (bookmark.Kind == WorkflowBookmarkKinds.UserTask)
            {
                await PublishUserTaskCreatedAsync(session, bookmark);
            }
        }

        // 首次挂起时按节点声明加超时书签
        if (isInitialSuspend && node.TimeoutSeconds is > 0)
        {
            await _bookmarkStore.InsertAsync(new WorkflowBookmark
            {
                Id = _idGenerator.NextIdString(),
                InstanceId = session.Instance.Id,
                NodeId = node.Id,
                NodeInstanceId = nodeInstance.Id,
                Kind = WorkflowBookmarkKinds.NodeTimeout,
                DueTime = _clock.Now.AddSeconds(node.TimeoutSeconds.Value),
                CreationTime = _clock.Now,
                TenantId = session.Instance.TenantId
            }, CancellationToken.None);
        }

        await _eventPublisher.PublishAsync(new WorkflowNodeExecutedEventData(session.Instance, nodeInstance));
    }

    private async Task HandleFaultedAsync(
        ExecutionSession session,
        WorkflowNode node,
        WorkflowNodeInstance nodeInstance,
        WorkflowActivityDescriptor descriptor,
        string faultMessage)
    {
        var now = _clock.Now;
        nodeInstance.FaultMessage = faultMessage;

        // 重试策略：按指数退避排期重试书签
        var retryPolicy = node.RetryPolicy;
        if (retryPolicy is not null && nodeInstance.TryCount < retryPolicy.MaxAttempts)
        {
            var delaySeconds = retryPolicy.FirstDelaySeconds * Math.Pow(retryPolicy.BackoffFactor, nodeInstance.TryCount - 1);
            nodeInstance.Status = WorkflowNodeInstanceStatus.Faulted;
            await _instanceStore.UpdateNodeInstanceAsync(nodeInstance, CancellationToken.None);

            await _bookmarkStore.InsertAsync(new WorkflowBookmark
            {
                Id = _idGenerator.NextIdString(),
                InstanceId = session.Instance.Id,
                NodeId = node.Id,
                NodeInstanceId = nodeInstance.Id,
                Kind = WorkflowBookmarkKinds.Retry,
                DueTime = now.AddSeconds(delaySeconds),
                CreationTime = now,
                TenantId = session.Instance.TenantId
            }, CancellationToken.None);

            await _eventPublisher.PublishAsync(new WorkflowNodeFaultedEventData(session.Instance, nodeInstance, WillRetry: true));
            _logger.LogWarning("实例 {InstanceId} 节点 {NodeId} 第 {TryCount} 次执行失败，{DelaySeconds} 秒后重试：{FaultMessage}",
                session.Instance.Id, node.Id, nodeInstance.TryCount, delaySeconds, faultMessage);
            return;
        }

        // 失败续行：写入错误变量并以 error 结果继续流转
        if (node.ContinueOnError)
        {
            nodeInstance.Status = WorkflowNodeInstanceStatus.Faulted;
            nodeInstance.EndTime = now;
            await DeleteNodeBookmarksAsync(nodeInstance.Id);
            await _instanceStore.UpdateNodeInstanceAsync(nodeInstance, CancellationToken.None);
            await _eventPublisher.PublishAsync(new WorkflowNodeFaultedEventData(session.Instance, nodeInstance, WillRetry: false));

            session.Variables.Set(WorkflowConsts.LastErrorVariableName, faultMessage);
            await FlowOutgoingAsync(session, node, nodeInstance, descriptor.OutgoingBehavior, "error");
            return;
        }

        // 故障整个实例
        nodeInstance.Status = WorkflowNodeInstanceStatus.Faulted;
        nodeInstance.EndTime = now;
        await _instanceStore.UpdateNodeInstanceAsync(nodeInstance, CancellationToken.None);
        await _eventPublisher.PublishAsync(new WorkflowNodeFaultedEventData(session.Instance, nodeInstance, WillRetry: false));

        await FaultInstanceAsync(session, node.Id, nodeInstance.Id, faultMessage);
    }

    private async Task FaultInstanceAsync(ExecutionSession session, string? nodeId, string? nodeInstanceId, string faultMessage)
    {
        var instance = session.Instance;
        instance.Status = WorkflowInstanceStatus.Faulted;
        instance.FaultMessage = faultMessage;
        instance.FaultNodeId = nodeId;
        instance.FaultNodeInstanceId = nodeInstanceId;
        instance.EndTime = _clock.Now;
        session.Queue.Clear();

        // 先持久化终态再发布事件，保证订阅方回查存储可见一致状态
        await _instanceStore.UpdateAsync(instance, CancellationToken.None);
        await _eventPublisher.PublishAsync(new WorkflowInstanceFaultedEventData(instance));
        ScheduleParentNotification(session, instance);
    }

    private async Task FlowOutgoingAsync(
        ExecutionSession session,
        WorkflowNode node,
        WorkflowNodeInstance nodeInstance,
        ActivityOutgoingBehavior behavior,
        string? outcome)
    {
        if (behavior == ActivityOutgoingBehavior.None)
        {
            return;
        }

        var outgoing = session.Graph.GetOutgoing(node.Id);
        if (outgoing.Count == 0)
        {
            // 无出边即隐式结束该令牌
            return;
        }

        var scope = new Dictionary<string, object?>(session.Instance.Variables)
        {
            [WorkflowConsts.OutcomeVariableName] = outcome
        };

        var selected = new List<WorkflowTransition>();

        switch (behavior)
        {
            case ActivityOutgoingBehavior.All:
                selected.AddRange(outgoing);
                break;

            case ActivityOutgoingBehavior.Exclusive:
            {
                foreach (var transition in outgoing.Where(item => !item.IsDefault))
                {
                    if (string.IsNullOrWhiteSpace(transition.Condition)
                        || await _expressionEvaluator.EvaluateConditionAsync(transition.Condition, scope, session.CancellationToken))
                    {
                        selected.Add(transition);
                        break;
                    }
                }

                if (selected.Count == 0)
                {
                    var defaultTransition = outgoing.FirstOrDefault(item => item.IsDefault);
                    if (defaultTransition is not null)
                    {
                        selected.Add(defaultTransition);
                    }
                }

                if (selected.Count == 0)
                {
                    await FaultInstanceAsync(session, node.Id, nodeInstance.Id,
                        $"独占网关 {node.Id} 无匹配分支且未配置默认分支");
                    return;
                }

                break;
            }

            default:
            {
                foreach (var transition in outgoing)
                {
                    if (transition.IsDefault
                        || string.IsNullOrWhiteSpace(transition.Condition)
                        || await _expressionEvaluator.EvaluateConditionAsync(transition.Condition, scope, session.CancellationToken))
                    {
                        selected.Add(transition);
                    }
                }

                if (selected.Count == 0)
                {
                    await FaultInstanceAsync(session, node.Id, nodeInstance.Id,
                        $"节点 {node.Id} 存在出边但所有条件均不满足");
                    return;
                }

                break;
            }
        }

        foreach (var transition in selected)
        {
            EnqueueTarget(session, transition);
        }
    }

    private void EnqueueTarget(ExecutionSession session, WorkflowTransition transition)
    {
        var target = session.Graph.GetRequiredNode(transition.TargetNodeId);

        // 汇聚网关按波次记录到达，满足条件才真正入队
        if (target.ActivityType == WorkflowActivityTypes.Join)
        {
            if (!session.Instance.JoinStates.TryGetValue(target.Id, out var joinState))
            {
                joinState = new WorkflowJoinState();
                session.Instance.JoinStates[target.Id] = joinState;
            }

            joinState.ArrivedTransitionIds.Add(transition.Id);

            var incomingCount = session.Graph.GetIncomingCount(target.Id);
            var mode = GetJoinMode(target);
            var allArrived = joinState.ArrivedTransitionIds.Count >= incomingCount;

            if (mode == JoinMode.WaitAll)
            {
                if (allArrived)
                {
                    session.Instance.JoinStates.Remove(target.Id);
                    session.Queue.Enqueue(WorkItem.Fresh(target.Id, null));
                }

                return;
            }

            // WaitAny：首个到达触发，同波次后续到达吞掉；全部到齐后重置波次
            // 注意：环路回边穿过 WaitAny 汇聚时，补齐波次的回边令牌会被吞掉，环路场景请使用 WaitAll
            var shouldFire = !joinState.Fired;
            if (shouldFire)
            {
                joinState.Fired = true;
            }

            if (allArrived)
            {
                session.Instance.JoinStates.Remove(target.Id);
            }

            if (shouldFire)
            {
                session.Queue.Enqueue(WorkItem.Fresh(target.Id, null));
            }

            return;
        }

        session.Queue.Enqueue(WorkItem.Fresh(target.Id, null));
    }

    private static JoinMode GetJoinMode(WorkflowNode node)
    {
        if (!node.Properties.TryGetValue("Mode", out var raw) || raw is null)
        {
            return JoinMode.WaitAll;
        }

        var text = WorkflowValueConverter.ConvertTo<string>(WorkflowValueConverter.Normalize(raw));
        if (string.Equals(text, "WaitAll", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "All", StringComparison.OrdinalIgnoreCase))
        {
            return JoinMode.WaitAll;
        }

        if (string.Equals(text, "WaitAny", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "Any", StringComparison.OrdinalIgnoreCase))
        {
            return JoinMode.WaitAny;
        }

        // fail-closed：未知模式是定义错误，不允许静默按 WaitAll 处理
        throw new WorkflowException($"汇聚网关 {node.Id} 配置了未知模式 {text}（仅支持 WaitAll/WaitAny）");
    }

    private async Task FinalizeBurstAsync(ExecutionSession session)
    {
        var instance = session.Instance;

        if (instance.Status == WorkflowInstanceStatus.Running)
        {
            var bookmarks = await _bookmarkStore.GetByInstanceAsync(instance.Id, CancellationToken.None);
            if (bookmarks.Count == 0)
            {
                // fail-closed：仍有未触发的汇聚波次说明某分支已死亡，完成会静默跳过汇聚后所有节点
                if (instance.JoinStates.Count > 0)
                {
                    var stuckJoins = string.Join("、", instance.JoinStates.Keys);
                    instance.Status = WorkflowInstanceStatus.Faulted;
                    instance.FaultMessage = $"汇聚网关 {stuckJoins} 等待的分支已死亡，无法汇合";
                    instance.EndTime = _clock.Now;
                    await _instanceStore.UpdateAsync(instance, CancellationToken.None);
                    await _eventPublisher.PublishAsync(new WorkflowInstanceFaultedEventData(instance));
                    ScheduleParentNotification(session, instance);
                    return;
                }

                instance.Status = WorkflowInstanceStatus.Completed;
                instance.EndTime = _clock.Now;
                await _instanceStore.UpdateAsync(instance, CancellationToken.None);
                await _eventPublisher.PublishAsync(new WorkflowInstanceCompletedEventData(instance));
                ScheduleParentNotification(session, instance);
                return;
            }
        }
        else if (instance.Status == WorkflowInstanceStatus.Terminated && instance.EndTime is null)
        {
            // 终止活动在批次内把实例置为终止态，此处统一收尾
            instance.EndTime = _clock.Now;
            await CleanupNonFinalWorkAsync(instance, CancellationToken.None);
            await _instanceStore.UpdateAsync(instance, CancellationToken.None);
            await _eventPublisher.PublishAsync(new WorkflowInstanceTerminatedEventData(instance));
            ScheduleParentNotification(session, instance);
            ScheduleChildrenCascade(session.PostActions, instance, WorkflowInstanceStatus.Terminated);
            return;
        }

        await _instanceStore.UpdateAsync(instance, CancellationToken.None);
    }

    private void ScheduleParentNotification(ExecutionSession session, WorkflowInstance instance)
    {
        if (string.IsNullOrEmpty(instance.ParentInstanceId) || string.IsNullOrEmpty(instance.ParentNodeInstanceId))
        {
            return;
        }

        var parentInstanceId = instance.ParentInstanceId;
        var parentNodeInstanceId = instance.ParentNodeInstanceId;
        session.PostActions.Add(() => NotifyParentAsync(parentInstanceId, parentNodeInstanceId, BuildChildCallbackInputs(instance)));
    }

    private void SchedulePostBurstChildStarts(
        ExecutionSession session,
        WorkflowNodeInstance nodeInstance,
        ActivityExecutionResult result)
    {
        if (result.ChildStartRequests.Count == 0)
        {
            return;
        }

        var instance = session.Instance;

        // 预铸子实例标识并登记进节点私有状态，作为回调波次的隔离依据
        var childIds = WorkflowValueConverter.ConvertTo<List<string>>(
            nodeInstance.State.GetValueOrDefault(WorkflowConsts.ChildInstanceIdsStateKey)) ?? [];

        foreach (var request in result.ChildStartRequests)
        {
            request.InstanceId = _idGenerator.NextIdString();
            request.ParentInstanceId = instance.Id;
            request.ParentNodeInstanceId = nodeInstance.Id;
            request.Depth = instance.Depth + 1;
            childIds.Add(request.InstanceId);

            var childRequest = request;
            session.PostActions.Add(async () =>
            {
                using (_currentTenant.Change(instance.TenantId))
                {
                    try
                    {
                        await StartAsync(childRequest, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        // fail-closed：子实例启动失败按故障回调父节点，避免父实例停在无人恢复的书签上
                        _logger.LogError(ex, "实例 {InstanceId} 节点实例 {NodeInstanceId} 启动子流程 {ChildCode} 失败",
                            instance.Id, nodeInstance.Id, childRequest.DefinitionCode);

                        await NotifyParentAsync(instance.Id, nodeInstance.Id, new Dictionary<string, object?>
                        {
                            [WorkflowConsts.ChildInstanceIdInputKey] = childRequest.InstanceId,
                            [WorkflowConsts.ChildStatusInputKey] = WorkflowInstanceStatus.Faulted.ToString(),
                            [WorkflowConsts.ChildVariablesInputKey] = new Dictionary<string, object?>(),
                            [WorkflowConsts.ChildFaultMessageInputKey] = $"子流程启动失败：{ex.Message}"
                        });
                    }
                }
            });
        }

        nodeInstance.State[WorkflowConsts.ChildInstanceIdsStateKey] = childIds;
    }

    private async Task PublishUserTaskCreatedAsync(ExecutionSession session, WorkflowBookmark bookmark)
    {
        var task = WorkflowUserTaskMapper.ToUserTask(bookmark, session.Instance);
        var ccUserIds = bookmark.Payload.TryGetValue(WorkflowUserTaskPayloadKeys.CcUserIds, out var raw)
            ? WorkflowValueConverter.ConvertTo<List<string>>(raw) ?? []
            : [];
        await _eventPublisher.PublishAsync(new WorkflowUserTaskCreatedEventData(task, ccUserIds));
    }

    #endregion 执行批次

    #region 强制结束与补偿

    private async Task<WorkflowInstance> FinishForciblyAsync(
        string instanceId,
        WorkflowInstanceStatus finalStatus,
        string? reason,
        CancellationToken cancellationToken)
    {
        var postActions = new List<Func<Task>>();
        WorkflowInstance instance;

        await using (await AcquireInstanceLockAsync(instanceId, cancellationToken))
        {
            instance = await GetRequiredInstanceAsync(instanceId, cancellationToken);
            if (instance.IsFinalStatus())
            {
                return instance;
            }

            instance.Status = finalStatus;
            instance.CancellationReason = reason;
            instance.EndTime = _clock.Now;

            await CleanupNonFinalWorkAsync(instance, CancellationToken.None);

            // 取消触发补偿，终止不补偿
            if (finalStatus == WorkflowInstanceStatus.Canceled)
            {
                var definition = await _definitionStore.FindAsync(instance.DefinitionId, CancellationToken.None);
                if (definition is { EnableCompensation: true })
                {
                    await CompensateAsync(definition, instance, CancellationToken.None);
                }
            }

            await _instanceStore.UpdateAsync(instance, CancellationToken.None);

            if (finalStatus == WorkflowInstanceStatus.Canceled)
            {
                await _eventPublisher.PublishAsync(new WorkflowInstanceCanceledEventData(instance));
            }
            else
            {
                await _eventPublisher.PublishAsync(new WorkflowInstanceTerminatedEventData(instance));
            }

            if (!string.IsNullOrEmpty(instance.ParentInstanceId) && !string.IsNullOrEmpty(instance.ParentNodeInstanceId))
            {
                var parentInstanceId = instance.ParentInstanceId;
                var parentNodeInstanceId = instance.ParentNodeInstanceId;
                var callbackInstance = instance;
                postActions.Add(() => NotifyParentAsync(parentInstanceId, parentNodeInstanceId, BuildChildCallbackInputs(callbackInstance)));
            }

            // 级联结束仍在运行的子实例，避免僵尸子流程继续产生业务副作用
            ScheduleChildrenCascade(postActions, instance, finalStatus);
        }

        await RunPostActionsAsync(postActions);
        return instance;
    }

    private void ScheduleChildrenCascade(List<Func<Task>> postActions, WorkflowInstance instance, WorkflowInstanceStatus finalStatus)
    {
        var instanceId = instance.Id;
        var reason = $"父实例 {instanceId} 已{(finalStatus == WorkflowInstanceStatus.Canceled ? "取消" : "终止")}";

        postActions.Add(async () =>
        {
            var children = await _instanceStore.GetChildrenAsync(instanceId, CancellationToken.None);
            foreach (var child in children.Where(item => !item.IsFinalStatus()))
            {
                try
                {
                    if (finalStatus == WorkflowInstanceStatus.Canceled)
                    {
                        await CancelAsync(child.Id, reason, CancellationToken.None);
                    }
                    else
                    {
                        await TerminateAsync(child.Id, reason, CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "级联结束子实例 {ChildId} 失败", child.Id);
                }
            }
        });
    }

    private async Task CleanupNonFinalWorkAsync(WorkflowInstance instance, CancellationToken cancellationToken)
    {
        await _bookmarkStore.DeleteByInstanceAsync(instance.Id, cancellationToken);

        var nodeInstances = await _instanceStore.GetNodeInstancesAsync(instance.Id, cancellationToken);
        foreach (var nodeInstance in nodeInstances.Where(item =>
                     item.Status is WorkflowNodeInstanceStatus.Running or WorkflowNodeInstanceStatus.Suspended))
        {
            nodeInstance.Status = WorkflowNodeInstanceStatus.Canceled;
            nodeInstance.EndTime = _clock.Now;
            await _instanceStore.UpdateNodeInstanceAsync(nodeInstance, cancellationToken);
        }
    }

    private async Task CompensateAsync(WorkflowDefinition definition, WorkflowInstance instance, CancellationToken cancellationToken)
    {
        var graph = new WorkflowDefinitionGraph(definition);
        var nodeInstances = await _instanceStore.GetNodeInstancesAsync(instance.Id, cancellationToken);

        // 按执行顺序逆序补偿（存储契约保证节点实例按开始时间升序返回）
        var completed = nodeInstances
            .Where(item => item.Status == WorkflowNodeInstanceStatus.Completed)
            .ToList();
        completed.Reverse();

        foreach (var nodeInstance in completed)
        {
            if (!_activityRegistry.TryGet(nodeInstance.ActivityType, out var descriptor))
            {
                continue;
            }

            if (_serviceProvider.GetRequiredService(descriptor.ClrType) is not ICompensableWorkflowActivity compensable)
            {
                continue;
            }

            try
            {
                var node = graph.GetRequiredNode(nodeInstance.NodeId);
                var context = new ActivityExecutionContext
                {
                    Definition = definition,
                    Instance = instance,
                    Node = node,
                    NodeInstance = nodeInstance,
                    Variables = new WorkflowVariables(instance.Variables),
                    ServiceProvider = _serviceProvider,
                    CancellationToken = cancellationToken
                };

                await compensable.CompensateAsync(context);
                nodeInstance.Status = WorkflowNodeInstanceStatus.Compensated;
                nodeInstance.CompensatedTime = _clock.Now;
                await _instanceStore.UpdateNodeInstanceAsync(nodeInstance, cancellationToken);
            }
            catch (Exception ex)
            {
                // 补偿失败不中断补偿链
                _logger.LogError(ex, "实例 {InstanceId} 节点实例 {NodeInstanceId} 补偿失败", instance.Id, nodeInstance.Id);
            }
        }
    }

    #endregion 强制结束与补偿

    #region 辅助方法

    private ActivityExecutionContext CreateContext(ExecutionSession session, WorkflowNode node, WorkflowNodeInstance nodeInstance)
    {
        return new ActivityExecutionContext
        {
            Definition = session.Definition,
            Instance = session.Instance,
            Node = node,
            NodeInstance = nodeInstance,
            Variables = session.Variables,
            ServiceProvider = _serviceProvider,
            CancellationToken = session.CancellationToken
        };
    }

    private async Task<IAsyncDisposable> AcquireInstanceLockAsync(string instanceId, CancellationToken cancellationToken)
    {
        return await WorkflowInstanceLocker.AcquireAsync(_distributedLock, _options, instanceId, cancellationToken);
    }

    private async Task<WorkflowInstance> GetRequiredInstanceAsync(string instanceId, CancellationToken cancellationToken)
    {
        return await _instanceStore.FindAsync(instanceId, cancellationToken)
            ?? throw new WorkflowException($"流程实例 {instanceId} 不存在");
    }

    private async Task<WorkflowDefinition> GetRequiredDefinitionAsync(WorkflowInstance instance, CancellationToken cancellationToken)
    {
        return await _definitionStore.FindAsync(instance.DefinitionId, cancellationToken)
            ?? throw new WorkflowException($"实例 {instance.Id} 绑定的流程定义 {instance.DefinitionId} 不存在");
    }

    private async Task<WorkflowDefinition> ResolveDefinitionAsync(WorkflowStartRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.DefinitionId))
        {
            return await _definitionStore.FindAsync(request.DefinitionId, cancellationToken)
                ?? throw new WorkflowException($"流程定义 {request.DefinitionId} 不存在");
        }

        if (string.IsNullOrWhiteSpace(request.DefinitionCode))
        {
            throw new WorkflowException("启动请求必须指定定义编码或定义标识");
        }

        if (request.DefinitionVersion is { } version)
        {
            return await _definitionStore.FindByVersionAsync(request.DefinitionCode, version, cancellationToken)
                ?? throw new WorkflowException($"流程定义 {request.DefinitionCode} v{version} 不存在");
        }

        return await _definitionStore.FindLatestPublishedAsync(request.DefinitionCode, cancellationToken)
            ?? throw new WorkflowException($"流程编码 {request.DefinitionCode} 不存在已发布版本");
    }

    private static Dictionary<string, object?> BuildStartVariables(WorkflowDefinition definition, WorkflowStartRequest request)
    {
        var variables = new Dictionary<string, object?>(request.Variables);

        foreach (var declaration in definition.Variables)
        {
            if (variables.ContainsKey(declaration.Name))
            {
                continue;
            }

            if (declaration.Required)
            {
                throw new WorkflowException($"启动流程 {definition.Code} 缺少必填变量 {declaration.Name}");
            }

            if (declaration.DefaultValue is not null)
            {
                variables[declaration.Name] = declaration.DefaultValue;
            }
        }

        return variables;
    }

    private async Task DeleteNodeBookmarksAsync(string nodeInstanceId)
    {
        var bookmarks = await _bookmarkStore.GetByNodeInstanceAsync(nodeInstanceId, CancellationToken.None);
        foreach (var bookmark in bookmarks)
        {
            await _bookmarkStore.DeleteAsync(bookmark.Id, CancellationToken.None);
        }
    }

    private async Task RunPostActionsAsync(List<Func<Task>> postActions)
    {
        foreach (var postAction in postActions)
        {
            try
            {
                await postAction();
            }
            catch (Exception ex)
            {
                // 单个后置动作失败不影响其余动作
                _logger.LogError(ex, "工作流后置动作执行失败");
            }
        }
    }

    private enum JoinMode
    {
        WaitAll,
        WaitAny
    }

    /// <summary>
    /// 执行批次会话（单次锁内推进的共享状态）
    /// </summary>
    /// <remarks>
    /// 批次内引擎存储写入一律使用 <see cref="CancellationToken.None"/>：批次一旦开始必须完整收尾，
    /// 调用方取消只影响活动执行本身（转化为节点故障），不允许造成状态半落盘。
    /// </remarks>
    private sealed class ExecutionSession
    {
        public ExecutionSession(
            WorkflowDefinition definition,
            WorkflowDefinitionGraph graph,
            WorkflowInstance instance,
            List<Func<Task>> postActions)
        {
            Definition = definition;
            Graph = graph;
            Instance = instance;
            Variables = new WorkflowVariables(instance.Variables);
            PostActions = postActions;
        }

        public WorkflowDefinition Definition { get; }

        public WorkflowDefinitionGraph Graph { get; }

        public WorkflowInstance Instance { get; }

        public WorkflowVariables Variables { get; }

        public Queue<WorkItem> Queue { get; } = new();

        public List<Func<Task>> PostActions { get; }

        /// <summary>
        /// 活动执行取消令牌（批次收尾不受其影响）
        /// </summary>
        public CancellationToken CancellationToken => CancellationToken.None;
    }

    /// <summary>
    /// 工作项（一次节点执行的入队描述）
    /// </summary>
    private sealed record WorkItem(
        string NodeId,
        Dictionary<string, object?>? Inputs,
        WorkflowNodeInstance? NodeInstance,
        WorkflowBookmark? Bookmark,
        bool IsRetry)
    {
        public static WorkItem Fresh(string nodeId, Dictionary<string, object?>? inputs)
        {
            return new WorkItem(nodeId, inputs, null, null, false);
        }

        public static WorkItem Resume(string nodeId, WorkflowNodeInstance nodeInstance, WorkflowBookmark bookmark, Dictionary<string, object?>? inputs)
        {
            return new WorkItem(nodeId, inputs, nodeInstance, bookmark, false);
        }

        public static WorkItem Retry(string nodeId, WorkflowNodeInstance nodeInstance)
        {
            return new WorkItem(nodeId, null, nodeInstance, null, true);
        }
    }

    #endregion 辅助方法
}
