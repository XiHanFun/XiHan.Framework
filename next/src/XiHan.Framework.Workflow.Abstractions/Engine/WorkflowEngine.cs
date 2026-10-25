// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Events;
using XiHan.Framework.Workflow.Abstractions.Expressions;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Serialization;
using XiHan.Framework.Workflow.Abstractions.Storage;
using XiHan.Framework.Workflow.Abstractions.Tasks;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Engine;

/// <summary>
/// 引擎（RFC-0021）：在实例锁内按波次执行节点，状态先在会话里改、波次结束一次批量写回，事件波次后发布，子流程启动与父流程通知在锁外进行。作用域服务：当前租户取自本次调用。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class WorkflowEngine : IWorkflowEngine
{
    /// <summary>
    /// 实例变量里记录最近一次"继续"错误的键。
    /// </summary>
    public const string LastErrorVariable = "lastError";

    /// <summary>
    /// 继续出错时的出边结果标签。
    /// </summary>
    public const string ErrorOutcome = "error";

    /// <summary>
    /// 子流程回调输入：子实例 ID。
    /// </summary>
    public const string ChildInstanceIdInput = "childInstanceId";

    /// <summary>
    /// 子流程回调输入：子实例状态。
    /// </summary>
    public const string ChildStatusInput = "childStatus";

    /// <summary>
    /// 子流程回调输入：子实例变量。
    /// </summary>
    public const string ChildVariablesInput = "childVariables";

    /// <summary>
    /// 子流程回调输入：子实例故障。
    /// </summary>
    public const string ChildFaultInput = "childFault";

    /// <summary>
    /// 通知父实例时等锁的尝试次数。
    /// </summary>
    private const int ParentNotifyAttempts = 3;

    /// <summary>
    /// 定义存储。
    /// </summary>
    private readonly IWorkflowDefinitionStore _definitions;

    /// <summary>
    /// 实例存储。
    /// </summary>
    private readonly IWorkflowInstanceStore _instances;

    /// <summary>
    /// 书签存储。
    /// </summary>
    private readonly IWorkflowBookmarkStore _bookmarks;

    /// <summary>
    /// 实例锁。
    /// </summary>
    private readonly IWorkflowInstanceLock _lock;

    /// <summary>
    /// 活动登记表。
    /// </summary>
    private readonly ActivityRegistry _registry;

    /// <summary>
    /// 求值器。
    /// </summary>
    private readonly IWorkflowExpressionEvaluator _evaluator;

    /// <summary>
    /// 选项。
    /// </summary>
    private readonly WorkflowOptions _options;

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 活动执行器。
    /// </summary>
    private readonly ActivityExecutor _executor;

    /// <summary>
    /// 事件发布。
    /// </summary>
    private readonly WorkflowEventPublisher _events;

    /// <summary>
    /// 本次调用的上下文访问器（取当前租户）。
    /// </summary>
    private readonly IInvocationAccessor? _invocation;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 创建（作用域服务）。
    /// </summary>
    public WorkflowEngine(IWorkflowDefinitionStore definitions, IWorkflowInstanceStore instances, IWorkflowBookmarkStore bookmarks, IWorkflowInstanceLock instanceLock, ActivityRegistry registry, IWorkflowExpressionEvaluator evaluator, WorkflowOptions options, TimeProvider clock, App app, IServiceProvider services, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        _instances = instances ?? throw new ArgumentNullException(nameof(instances));
        _bookmarks = bookmarks ?? throw new ArgumentNullException(nameof(bookmarks));
        _lock = instanceLock ?? throw new ArgumentNullException(nameof(instanceLock));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = loggerFactory.CreateLogger<WorkflowEngine>();
        // 活动在根提供者的新作用域里执行，与本引擎所在的作用域无关
        _executor = new ActivityExecutor(RootOf(services), app, registry, evaluator, clock);
        _events = new WorkflowEventPublisher(services.GetService<IMessagePublisher>(), _logger);
        _invocation = services.GetService<IInvocationAccessor>();
    }

    /// <inheritdoc />
    public async ValueTask<Result<WorkflowInstance>> StartAsync(WorkflowStartRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Depth > _options.MaxSubWorkflowDepth)
        {
            return Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.InstanceDepth, MessageArguments.Of(("depth", request.Depth), ("max", _options.MaxSubWorkflowDepth)));
        }

        var found = await FindPublishedDefinitionAsync(request, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var definition))
        {
            return found.Error!;
        }

        // 变量：声明的缺省补默认值，必填缺失即失败
        var variables = WorkflowJson.CloneObject(request.Variables);
        foreach (var declared in definition.Variables)
        {
            if (variables.ContainsKey(declared.Name))
            {
                continue;
            }

            if (declared.DefaultValue is not null)
            {
                variables[declared.Name] = declared.DefaultValue.DeepClone();
            }
            else if (declared.Required)
            {
                return Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.VariableRequired, MessageArguments.Of(("name", declared.Name)));
            }
        }

        var now = _clock.GetUtcNow();
        var instance = new WorkflowInstance
        {
            Id = string.IsNullOrWhiteSpace(request.InstanceId) ? WorkflowIdentifiers.NewId() : request.InstanceId,
            DefinitionId = definition.Id,
            DefinitionName = definition.Name,
            DefinitionVersion = definition.Version,
            DisplayName = request.DisplayName ?? definition.DisplayName,
            Status = WorkflowInstanceStatus.Running,
            Variables = variables,
            CorrelationId = request.CorrelationId,
            StarterId = request.StarterId,
            ParentInstanceId = request.ParentInstanceId,
            ParentNodeInstanceId = request.ParentNodeInstanceId,
            Depth = request.Depth,
            TenantId = request.TenantId ?? CurrentTenantId() ?? definition.TenantId,
            CreatedAt = now,
            StartedAt = now,
        };

        // 先写入再加锁执行：实例从此可被查到、可被锁住
        var saved = await _instances.SaveAsync([instance], cancellationToken).ConfigureAwait(false);
        if (saved.IsFailure)
        {
            return saved.Error!;
        }

        return await RunLockedAsync(instance.Id, async session =>
        {
            session.Publish(WorkflowTopics.InstanceStarted, session.EventData());
            if (session.Graph.StartNode is null)
            {
                FaultInstance(session, Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionInvalid, MessageArguments.Of(("errors", "no start node"))), null, null);
                return Result.Success();
            }

            session.Queue.Enqueue(new WorkItem(WorkItemKind.Fresh, session.Graph.StartNode));
            await RunBurstAsync(session).ConfigureAwait(false);
            return Result.Success();
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<Result<WorkflowInstance>> ResumeAsync(string bookmarkId, JsonObject? inputs, string? expectedKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookmarkId);
        var peek = await _bookmarks.FindAsync(bookmarkId, cancellationToken).ConfigureAwait(false);
        if (!peek.TryGetValue(out var maybe))
        {
            return peek.Error!;
        }

        if (!maybe.TryGetValue(out var found))
        {
            return BookmarkNotFound(bookmarkId);
        }

        return await RunLockedAsync(found.InstanceId, async session =>
        {
            // 锁内重读：书签可能已被别的执行者消费
            var bookmark = session.Bookmark(bookmarkId);
            if (bookmark is null)
            {
                return BookmarkNotFound(bookmarkId);
            }

            if (expectedKey is not null && !string.Equals(expectedKey, bookmark.Key, StringComparison.Ordinal))
            {
                return Error.Conflict(WorkflowErrors.BookmarkNotResumable, WorkflowMessages.BookmarkKeyMismatch, MessageArguments.Of(("id", bookmarkId)));
            }

            if (session.Instance.Status != WorkflowInstanceStatus.Running)
            {
                return Error.Conflict(WorkflowErrors.BookmarkNotResumable, WorkflowMessages.BookmarkNotResumable, MessageArguments.Of(("id", bookmarkId)));
            }

            var node = session.Graph.Node(bookmark.NodeId);
            var nodeInstance = session.NodeInstance(bookmark.NodeInstanceId);
            if (node is null || nodeInstance is null)
            {
                // 定义或节点实例对不上：书签是孤儿，消费掉
                session.RemoveBookmark(bookmarkId);
                return BookmarkNotFound(bookmarkId);
            }

            // 子流程回调只接受本轮启动的子实例，旧一轮（重试前）的回调忽略
            if (string.Equals(bookmark.Kind, BookmarkKinds.SubWorkflow, StringComparison.Ordinal)
                && inputs?[ChildInstanceIdInput] is JsonValue child
                && !WorkflowSession.ChildInstanceIds(nodeInstance).Contains(WorkflowValues.AsString(child), StringComparer.Ordinal))
            {
                return Result.Success();
            }

            session.RemoveBookmark(bookmarkId);
            session.Queue.Enqueue(string.Equals(bookmark.Kind, BookmarkKinds.Retry, StringComparison.Ordinal)
                ? new WorkItem(WorkItemKind.Retry, node, nodeInstance)
                : new WorkItem(WorkItemKind.Resume, node, nodeInstance, bookmark, inputs));
            await RunBurstAsync(session).ConfigureAwait(false);
            return Result.Success();
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<Result<int>> SignalAsync(string signalName, JsonObject? payload, string? correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signalName);
        var found = await _bookmarks.BySignalAsync(signalName, correlationId, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var bookmarks))
        {
            return found.Error!;
        }

        var tenant = CurrentTenantId();
        var consumed = 0;
        foreach (var bookmark in bookmarks)
        {
            if (!string.Equals(bookmark.TenantId, tenant, StringComparison.Ordinal))
            {
                continue;
            }

            var resumed = await ResumeAsync(bookmark.Id, payload, null, cancellationToken).ConfigureAwait(false);
            if (resumed.IsSuccess)
            {
                consumed++;
            }
            else if (resumed.Error!.Code is not (WorkflowErrors.BookmarkNotFound or WorkflowErrors.BookmarkNotResumable))
            {
                LogSignalFailed(_logger, signalName, bookmark.Id, resumed.Error.Code);
            }
        }

        return consumed;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result<WorkflowInstance>>> SuspendAsync(IReadOnlyList<string> instanceIds, string? reason, CancellationToken cancellationToken)
        => ForEachAsync(instanceIds, session =>
        {
            if (session.Instance.Status != WorkflowInstanceStatus.Running)
            {
                return StatusConflict(session.Instance);
            }

            session.Instance.Status = WorkflowInstanceStatus.Suspended;
            session.Instance.CancellationReason = reason;
            session.Publish(WorkflowTopics.InstanceSuspended, session.EventData() with { Reason = reason });
            return Result.Success();
        }, cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result<WorkflowInstance>>> ResumeInstancesAsync(IReadOnlyList<string> instanceIds, CancellationToken cancellationToken)
        => ForEachAsync(instanceIds, session =>
        {
            if (session.Instance.Status != WorkflowInstanceStatus.Suspended)
            {
                return StatusConflict(session.Instance);
            }

            session.Instance.Status = WorkflowInstanceStatus.Running;
            session.Instance.CancellationReason = null;
            session.Publish(WorkflowTopics.InstanceResumed, session.EventData());
            return Result.Success();
        }, cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result<WorkflowInstance>>> CancelAsync(IReadOnlyList<string> instanceIds, string? reason, CancellationToken cancellationToken)
        => ForEachAsync(instanceIds, session => FinishForciblyAsync(session, WorkflowInstanceStatus.Canceled, reason), cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result<WorkflowInstance>>> TerminateAsync(IReadOnlyList<string> instanceIds, string? reason, CancellationToken cancellationToken)
        => ForEachAsync(instanceIds, session => FinishForciblyAsync(session, WorkflowInstanceStatus.Terminated, reason), cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result<WorkflowInstance>>> RetryAsync(IReadOnlyList<string> instanceIds, CancellationToken cancellationToken)
        => ForEachAsync(instanceIds, async session =>
        {
            var instance = session.Instance;
            if (instance.Status != WorkflowInstanceStatus.Faulted || instance.FaultNodeInstanceId is null)
            {
                return StatusConflict(instance);
            }

            var nodeInstance = session.NodeInstance(instance.FaultNodeInstanceId);
            var node = nodeInstance is null ? null : session.Graph.Node(nodeInstance.NodeId);
            if (node is null || nodeInstance is null)
            {
                return StatusConflict(instance);
            }

            instance.Status = WorkflowInstanceStatus.Running;
            instance.EndedAt = null;
            instance.Fault = null;
            instance.FaultNodeId = null;
            instance.FaultNodeInstanceId = null;
            session.Publish(WorkflowTopics.InstanceResumed, session.EventData());
            session.Queue.Enqueue(new WorkItem(WorkItemKind.Retry, node, nodeInstance));
            await RunBurstAsync(session).ConfigureAwait(false);
            return Result.Success();
        }, cancellationToken);

    /// <summary>
    /// 在实例锁内对一组实例逐个执行 <paramref name="action"/>（同步版）。
    /// </summary>
    private ValueTask<IReadOnlyList<Result<WorkflowInstance>>> ForEachAsync(IReadOnlyList<string> instanceIds, Func<WorkflowSession, Result> action, CancellationToken cancellationToken)
        => ForEachAsync(instanceIds, session => ValueTask.FromResult(action(session)), cancellationToken);

    /// <summary>
    /// 在实例锁内对一组实例逐个执行 <paramref name="action"/>，逐项结果。
    /// </summary>
    private async ValueTask<IReadOnlyList<Result<WorkflowInstance>>> ForEachAsync(IReadOnlyList<string> instanceIds, Func<WorkflowSession, ValueTask<Result>> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instanceIds);
        var results = new List<Result<WorkflowInstance>>(instanceIds.Count);
        foreach (var id in instanceIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await RunLockedAsync(id, action, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    /// <summary>
    /// 加锁 → 加载会话 → 执行 → 写回 → 发事件 → 解锁 → 锁外动作；返回写回后的实例。
    /// </summary>
    private async ValueTask<Result<WorkflowInstance>> RunLockedAsync(string instanceId, Func<WorkflowSession, ValueTask<Result>> action, CancellationToken cancellationToken)
    {
        var acquired = await _lock.AcquireAsync(instanceId, cancellationToken).ConfigureAwait(false);
        if (!acquired.TryGetValue(out var handle))
        {
            return acquired.Error!;
        }

        WorkflowSession session;
        await using (handle.ConfigureAwait(false))
        {
            var loaded = await LoadAsync(instanceId, cancellationToken).ConfigureAwait(false);
            if (!loaded.TryGetValue(out session!))
            {
                return loaded.Error!;
            }

            var result = await action(session).ConfigureAwait(false);
            if (result.IsFailure)
            {
                return result.Error!;
            }

            // 波次里改的状态一次写回：实例、节点实例、书签；写回失败就是本次操作失败（锁内没有别人改过）
            var flushed = await FlushAsync(session, cancellationToken).ConfigureAwait(false);
            if (flushed.IsFailure)
            {
                return flushed.Error!;
            }

            await _events.FlushAsync(session.Events, cancellationToken).ConfigureAwait(false);
        }

        await RunPostActionsAsync(session, cancellationToken).ConfigureAwait(false);
        return session.Instance;
    }

    /// <summary>
    /// 加载实例、定义、节点实例与书签。
    /// </summary>
    private async ValueTask<Result<WorkflowSession>> LoadAsync(string instanceId, CancellationToken cancellationToken)
    {
        var found = await _instances.FindAsync(instanceId, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var maybe))
        {
            return found.Error!;
        }

        if (!maybe.TryGetValue(out var instance))
        {
            return Error.NotFound(WorkflowErrors.InstanceNotFound, WorkflowMessages.InstanceNotFound, MessageArguments.Of(("id", instanceId)));
        }

        var definitionFound = await _definitions.FindAsync(instance.DefinitionId, cancellationToken).ConfigureAwait(false);
        if (!definitionFound.TryGetValue(out var definitionMaybe))
        {
            return definitionFound.Error!;
        }

        if (!definitionMaybe.TryGetValue(out var definition))
        {
            return Error.NotFound(WorkflowErrors.DefinitionNotFound, WorkflowMessages.DefinitionNotFound, MessageArguments.Of(("name", instance.DefinitionName.Value)));
        }

        var nodeInstances = await _instances.NodeInstancesAsync(instanceId, cancellationToken).ConfigureAwait(false);
        if (!nodeInstances.TryGetValue(out var nodes))
        {
            return nodeInstances.Error!;
        }

        var bookmarks = await _bookmarks.ByInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);
        if (!bookmarks.TryGetValue(out var marks))
        {
            return bookmarks.Error!;
        }

        return new WorkflowSession(definition, instance, nodes, marks, _clock.GetUtcNow());
    }

    /// <summary>
    /// 写回会话里的改动。
    /// </summary>
    private async ValueTask<Result> FlushAsync(WorkflowSession session, CancellationToken cancellationToken)
    {
        var saved = await _instances.SaveAsync([session.Instance], cancellationToken).ConfigureAwait(false);
        if (saved.IsFailure)
        {
            return saved;
        }

        var dirtyNodes = session.DirtyNodeInstances();
        if (dirtyNodes.Count > 0)
        {
            saved = await _instances.SaveNodeInstancesAsync(dirtyNodes, cancellationToken).ConfigureAwait(false);
            if (saved.IsFailure)
            {
                return saved;
            }
        }

        var deleted = session.DeletedBookmarkIds();
        if (deleted.Count > 0)
        {
            saved = await _bookmarks.DeleteAsync(deleted, cancellationToken).ConfigureAwait(false);
            if (saved.IsFailure)
            {
                return saved;
            }
        }

        var dirtyBookmarks = session.DirtyBookmarks();
        if (dirtyBookmarks.Count > 0)
        {
            saved = await _bookmarks.SaveAsync(dirtyBookmarks, cancellationToken).ConfigureAwait(false);
        }

        return saved;
    }

    /// <summary>
    /// 锁外动作：启动子流程、通知父实例、级联子实例。
    /// </summary>
    private async ValueTask RunPostActionsAsync(WorkflowSession session, CancellationToken cancellationToken)
    {
        var instance = session.Instance;
        foreach (var child in session.ChildStarts)
        {
            var started = await StartAsync(child, cancellationToken).ConfigureAwait(false);
            if (started.IsFailure && child.ParentNodeInstanceId is not null)
            {
                // 子流程没起来：以故障回调父节点，父节点决定是否故障
                var inputs = new JsonObject
                {
                    [ChildInstanceIdInput] = child.InstanceId,
                    [ChildStatusInput] = WorkflowInstanceStatus.Faulted.ToString(),
                    [ChildFaultInput] = JsonValue.Create(started.Error!.Code + ": " + started.Error.Message),
                };
                await NotifyParentAsync(instance.Id, child.ParentNodeInstanceId, inputs, cancellationToken).ConfigureAwait(false);
            }
        }

        if (session.NotifyParent && instance.ParentInstanceId is not null && instance.ParentNodeInstanceId is not null)
        {
            var inputs = new JsonObject
            {
                [ChildInstanceIdInput] = instance.Id,
                [ChildStatusInput] = instance.Status.ToString(),
                [ChildVariablesInput] = WorkflowJson.CloneObject(instance.Variables),
                [ChildFaultInput] = instance.Fault is null ? null : JsonValue.Create(instance.Fault.Code + ": " + instance.Fault.Message),
            };
            await NotifyParentAsync(instance.ParentInstanceId, instance.ParentNodeInstanceId, inputs, cancellationToken).ConfigureAwait(false);
        }

        if (session.CascadeToChildren is { } cascade)
        {
            var children = await _instances.ListAsync(new WorkflowInstanceQuery { ParentInstanceId = instance.Id, Limit = WorkflowInstanceQuery.MaxLimit }, cancellationToken).ConfigureAwait(false);
            if (children.TryGetValue(out var list))
            {
                var open = list.Where(static child => !child.IsFinal).Select(static child => child.Id).ToList();
                if (open.Count > 0)
                {
                    _ = cascade == WorkflowInstanceStatus.Canceled
                        ? await CancelAsync(open, instance.CancellationReason, cancellationToken).ConfigureAwait(false)
                        : await TerminateAsync(open, instance.CancellationReason, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// 以子流程书签回调父实例；等锁超时重试几次。
    /// </summary>
    private async ValueTask NotifyParentAsync(string parentInstanceId, string parentNodeInstanceId, JsonObject inputs, CancellationToken cancellationToken)
    {
        var found = await _bookmarks.ByKindAndKeyAsync(BookmarkKinds.SubWorkflow, parentNodeInstanceId, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var bookmarks))
        {
            return;
        }

        var bookmark = bookmarks.FirstOrDefault(candidate => string.Equals(candidate.InstanceId, parentInstanceId, StringComparison.Ordinal));
        if (bookmark is null)
        {
            return;
        }

        for (var attempt = 1; attempt <= ParentNotifyAttempts; attempt++)
        {
            var resumed = await ResumeAsync(bookmark.Id, inputs, null, cancellationToken).ConfigureAwait(false);
            if (resumed.IsSuccess || resumed.Error!.Code != WorkflowErrors.LockTimeout)
            {
                if (resumed.IsFailure && resumed.Error!.Code is not (WorkflowErrors.BookmarkNotFound or WorkflowErrors.BookmarkNotResumable))
                {
                    LogParentNotifyFailed(_logger, parentInstanceId, resumed.Error.Code);
                }

                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), _clock, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 波次：在锁内逐个执行队列里的节点直到队列空或实例不再运行，然后收尾。
    /// </summary>
    private async ValueTask RunBurstAsync(WorkflowSession session)
    {
        while (session.Queue.Count > 0 && session.Instance.Status == WorkflowInstanceStatus.Running)
        {
            var item = session.Queue.Dequeue();
            // 死循环保护：一个波次执行的节点数有上限
            if (++session.Executed > _options.MaxNodeExecutionsPerBurst)
            {
                FaultInstance(session, Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.InstanceRunaway, details: MessageArguments.Of(("id", session.Instance.Id), ("limit", _options.MaxNodeExecutionsPerBurst))), null, null);
                break;
            }

            await ExecuteNodeAsync(session, item).ConfigureAwait(false);
        }

        FinalizeBurst(session);
    }

    /// <summary>
    /// 执行一项工作。
    /// </summary>
    private async ValueTask ExecuteNodeAsync(WorkflowSession session, WorkItem item)
    {
        var node = item.Node;
        WorkflowNodeInstance nodeInstance;
        switch (item.Kind)
        {
            case WorkItemKind.Fresh:
                nodeInstance = session.AddNodeInstance(node);
                break;
            case WorkItemKind.Resume:
                nodeInstance = item.NodeInstance!;
                nodeInstance.Status = WorkflowNodeInstanceStatus.Running;
                nodeInstance.Inputs = WorkflowJson.CloneObject(item.Inputs);
                session.Touch(nodeInstance);
                break;
            default:
                nodeInstance = item.NodeInstance!;
                nodeInstance.Status = WorkflowNodeInstanceStatus.Running;
                nodeInstance.Attempt++;
                nodeInstance.Fault = null;
                nodeInstance.EndedAt = null;
                nodeInstance.State.Remove(WorkflowSession.ChildInstanceIdsKey);
                session.RemoveBookmarksOf(nodeInstance.Id);
                session.Touch(nodeInstance);
                break;
        }

        session.Publish(WorkflowTopics.NodeExecuting, session.EventData(nodeInstance));
        var result = await _executor.RunAsync(session, node, nodeInstance, item.Bookmark, item.Inputs, CancellationToken.None).ConfigureAwait(false);
        if (!result.TryGetValue(out var outcome))
        {
            HandleFault(session, node, nodeInstance, result.Error!);
            return;
        }

        switch (outcome.Kind)
        {
            case ActivityOutcomeKind.Completed:
            case ActivityOutcomeKind.EndInstance:
                CompleteNode(session, nodeInstance, outcome);
                if (outcome.Kind == ActivityOutcomeKind.EndInstance)
                {
                    session.Instance.Outcome = outcome.Label ?? session.Instance.Outcome;
                    session.EndRequested = true;
                    session.Queue.Clear();
                    return;
                }

                FlowOutgoing(session, node, outcome.Label);
                return;
            case ActivityOutcomeKind.Terminated:
                CompleteNode(session, nodeInstance, outcome);
                session.Instance.Status = WorkflowInstanceStatus.Terminated;
                session.Instance.CancellationReason = outcome.Reason;
                session.Queue.Clear();
                return;
            default:
                SuspendNode(session, node, nodeInstance, outcome, item.Kind == WorkItemKind.Resume);
                return;
        }
    }

    /// <summary>
    /// 节点完成：记输出与结果标签、合并变量、登记子流程、清掉剩余书签。
    /// </summary>
    private static void CompleteNode(WorkflowSession session, WorkflowNodeInstance nodeInstance, ActivityOutcome outcome)
    {
        nodeInstance.Status = WorkflowNodeInstanceStatus.Completed;
        nodeInstance.EndedAt = session.Now;
        var outputs = WorkflowJson.CloneObject(outcome.Outputs);
        if (outcome.Label is not null)
        {
            outputs[UserTaskPayloadKeys.Outcome] = outcome.Label;
        }

        nodeInstance.Outputs = outputs;
        session.Variables.Merge(outcome.Outputs);
        session.ScheduleChildren(nodeInstance, outcome.Children);
        session.RemoveBookmarksOf(nodeInstance.Id);
        session.Touch(nodeInstance);
        session.Publish(WorkflowTopics.NodeExecuted, session.EventData(nodeInstance) with { Outcome = outcome.Label });
    }

    /// <summary>
    /// 节点挂起：建书签（用户任务发创建事件）、首次挂起且有超时时加超时书签。
    /// </summary>
    private static void SuspendNode(WorkflowSession session, WorkflowNode node, WorkflowNodeInstance nodeInstance, ActivityOutcome outcome, bool resumed)
    {
        nodeInstance.Status = WorkflowNodeInstanceStatus.Suspended;
        session.Variables.Merge(outcome.Outputs);
        session.ScheduleChildren(nodeInstance, outcome.Children);
        session.Touch(nodeInstance);
        foreach (var request in outcome.Bookmarks)
        {
            var bookmark = session.AddBookmark(nodeInstance, request);
            if (string.Equals(bookmark.Kind, BookmarkKinds.UserTask, StringComparison.Ordinal))
            {
                PublishTaskCreated(session, nodeInstance, bookmark);
            }
        }

        // 超时书签只在首次挂起（含重试后的首次）时加一次；恢复后再次挂起时沿用原来的
        if (!resumed && node.Timeout is { } timeout)
        {
            session.AddBookmark(nodeInstance, new BookmarkRequest { Kind = BookmarkKinds.NodeTimeout, DueAt = session.Now + timeout });
        }

        session.Publish(WorkflowTopics.NodeExecuted, session.EventData(nodeInstance));
    }

    /// <summary>
    /// 发用户任务创建事件（抄送人来自载荷）。
    /// </summary>
    internal static void PublishTaskCreated(WorkflowSession session, WorkflowNodeInstance nodeInstance, WorkflowBookmark bookmark)
        => session.Publish(WorkflowTopics.TaskCreated, session.EventData(nodeInstance) with
        {
            TaskId = bookmark.Id,
            AssigneeId = bookmark.Key,
            CcUserIds = WorkflowValues.ToStringList(bookmark.Payload.TryGetPropertyValue(UserTaskPayloadKeys.CcUserIds, out var cc) ? cc : null),
            Payload = WorkflowJson.CloneObject(bookmark.Payload),
        });

    /// <summary>
    /// 节点失败：重试策略 → 继续出错 → 实例故障。
    /// </summary>
    private void HandleFault(WorkflowSession session, WorkflowNode node, WorkflowNodeInstance nodeInstance, Error error)
    {
        nodeInstance.Fault = WorkflowFault.From(error);
        nodeInstance.Status = WorkflowNodeInstanceStatus.Faulted;
        session.Touch(nodeInstance);
        if (node.RetryPolicy is { } policy && policy.CanRetry(nodeInstance.Attempt))
        {
            session.AddBookmark(nodeInstance, new BookmarkRequest { Kind = BookmarkKinds.Retry, DueAt = session.Now + policy.DelayAfter(nodeInstance.Attempt) });
            session.Publish(WorkflowTopics.NodeFaulted, session.EventData(nodeInstance) with { WillRetry = true });
            return;
        }

        session.Publish(WorkflowTopics.NodeFaulted, session.EventData(nodeInstance) with { WillRetry = false });
        if (node.ContinueOnError)
        {
            nodeInstance.EndedAt = session.Now;
            session.RemoveBookmarksOf(nodeInstance.Id);
            session.Variables.Set(LastErrorVariable, new JsonObject { ["code"] = error.Code, ["message"] = error.Message });
            FlowOutgoing(session, node, ErrorOutcome);
            return;
        }

        FaultInstance(session, error, node.Id, nodeInstance.Id);
    }

    /// <summary>
    /// 实例故障：记录故障与位置，清空队列，发事件，通知父实例。
    /// </summary>
    private static void FaultInstance(WorkflowSession session, Error error, string? nodeId, string? nodeInstanceId)
    {
        var instance = session.Instance;
        instance.Status = WorkflowInstanceStatus.Faulted;
        instance.Fault = WorkflowFault.From(error);
        instance.FaultNodeId = nodeId;
        instance.FaultNodeInstanceId = nodeInstanceId;
        instance.EndedAt = session.Now;
        session.Queue.Clear();
        session.Publish(WorkflowTopics.InstanceFaulted, session.EventData());
        session.NotifyParent = true;
    }

    /// <summary>
    /// 按出边选择方式把后继节点入队。
    /// </summary>
    private void FlowOutgoing(WorkflowSession session, WorkflowNode node, string? label)
    {
        // 结果标签写进实例变量 outcome：后继的决策节点也能按上一节点的结果选边；没有标签的节点不覆盖
        if (label is not null)
        {
            session.Variables.Set(UserTaskPayloadKeys.Outcome, JsonValue.Create(label));
        }

        var outgoing = _registry.Find(node.ActivityType).Map(static definition => definition.Outgoing).ValueOr(ActivityOutgoing.AllMatched);
        if (outgoing == ActivityOutgoing.None)
        {
            return;
        }

        var transitions = session.Graph.Outgoing(node.Id);
        if (transitions.Count == 0)
        {
            return;
        }

        var scope = new ExpressionScope(session.Instance.Variables, _clock);
        var selected = new List<WorkflowTransition>();
        WorkflowTransition? fallback = null;
        foreach (var transition in transitions)
        {
            if (outgoing == ActivityOutgoing.All)
            {
                selected.Add(transition);
                continue;
            }

            if (transition.IsDefault)
            {
                fallback ??= transition;
                continue;
            }

            if (string.IsNullOrWhiteSpace(transition.Condition))
            {
                selected.Add(transition);
            }
            else
            {
                var evaluated = _evaluator.EvaluateCondition(transition.Condition, scope);
                if (!evaluated.TryGetValue(out var holds))
                {
                    FaultInstance(session, evaluated.Error!, node.Id, null);
                    return;
                }

                if (holds)
                {
                    selected.Add(transition);
                }
            }

            if (outgoing == ActivityOutgoing.Exclusive && selected.Count > 0)
            {
                break;
            }
        }

        // 默认出边只作兜底：条件边一条都没选中时才走
        if (selected.Count == 0 && fallback is not null)
        {
            selected.Add(fallback);
        }

        if (selected.Count == 0)
        {
            FaultInstance(session, Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.InstanceNoTransition, MessageArguments.Of(("node", node.Id))), node.Id, null);
            return;
        }

        foreach (var transition in selected)
        {
            if (!EnqueueTarget(session, transition))
            {
                return;
            }
        }
    }

    /// <summary>
    /// 把转移的目标入队；目标是汇合节点时按模式计数。返回 <see langword="false"/> 表示实例已故障。
    /// </summary>
    private static bool EnqueueTarget(WorkflowSession session, WorkflowTransition transition)
    {
        var target = session.Graph.Node(transition.TargetNodeId);
        if (target is null)
        {
            FaultInstance(session, Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.InstanceNoTransition, MessageArguments.Of(("node", transition.TargetNodeId))), null, null);
            return false;
        }

        if (!string.Equals(target.ActivityType.Value, ActivityTypes.Join, StringComparison.Ordinal))
        {
            session.Queue.Enqueue(new WorkItem(WorkItemKind.Fresh, target));
            return true;
        }

        var mode = target.Properties.TryGetPropertyValue(JoinActivity.ModeProperty, out var modeNode) && modeNode is JsonValue modeValue ? WorkflowValues.AsString(modeValue) : JoinActivity.WaitAll;
        var waitAny = string.Equals(mode, JoinActivity.WaitAny, StringComparison.OrdinalIgnoreCase);
        if (!waitAny && !string.Equals(mode, JoinActivity.WaitAll, StringComparison.OrdinalIgnoreCase))
        {
            FaultInstance(session, ActivityContext.PropertyInvalid(JoinActivity.ModeProperty, mode), target.Id, null);
            return false;
        }

        if (!session.Instance.JoinStates.TryGetValue(target.Id, out var state))
        {
            state = new WorkflowJoinState();
            session.Instance.JoinStates[target.Id] = state;
        }

        if (!state.Arrived.Contains(transition.Id, StringComparer.Ordinal))
        {
            state.Arrived.Add(transition.Id);
        }

        var incoming = session.Graph.IncomingCount(target.Id);
        if (waitAny)
        {
            // WaitAny：首个到达触发，其余到达忽略；全部到齐后清状态开始下一轮
            if (!state.Fired)
            {
                state.Fired = true;
                session.Queue.Enqueue(new WorkItem(WorkItemKind.Fresh, target));
            }

            if (state.Arrived.Count >= incoming)
            {
                session.Instance.JoinStates.Remove(target.Id);
            }

            return true;
        }

        if (state.Arrived.Count >= incoming)
        {
            session.Instance.JoinStates.Remove(target.Id);
            session.Queue.Enqueue(new WorkItem(WorkItemKind.Fresh, target));
        }

        return true;
    }

    /// <summary>
    /// 波次收尾：结束请求或没有书签时完成（有未凑齐的汇合则故障）；终止活动置的状态在这里清理。
    /// </summary>
    private static void FinalizeBurst(WorkflowSession session)
    {
        var instance = session.Instance;
        if (instance.Status == WorkflowInstanceStatus.Running && session.EndRequested)
        {
            session.CancelOpenNodeInstances();
            session.RemoveAllBookmarks();
            instance.JoinStates.Clear();
            Complete(session);
            session.CascadeToChildren = WorkflowInstanceStatus.Terminated;
            return;
        }

        if (instance.Status == WorkflowInstanceStatus.Running && !session.HasBookmarks)
        {
            if (instance.JoinStates.Count > 0)
            {
                FaultInstance(session, Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.InstanceJoinDead, MessageArguments.Of(("id", instance.Id))), null, null);
                return;
            }

            Complete(session);
            return;
        }

        if (instance.Status == WorkflowInstanceStatus.Terminated && instance.EndedAt is null)
        {
            instance.EndedAt = session.Now;
            session.CancelOpenNodeInstances();
            session.RemoveAllBookmarks();
            session.Publish(WorkflowTopics.InstanceTerminated, session.EventData());
            session.NotifyParent = true;
            session.CascadeToChildren = WorkflowInstanceStatus.Terminated;
        }
    }

    /// <summary>
    /// 实例完成。
    /// </summary>
    private static void Complete(WorkflowSession session)
    {
        session.Instance.Status = WorkflowInstanceStatus.Completed;
        session.Instance.EndedAt = session.Now;
        session.Publish(WorkflowTopics.InstanceCompleted, session.EventData());
        session.NotifyParent = true;
    }

    /// <summary>
    /// 强制结束（取消或终止）：已结束的幂等返回，故障的允许；清非终态节点与全部书签；取消且启用补偿时补偿；级联子实例。
    /// </summary>
    private async ValueTask<Result> FinishForciblyAsync(WorkflowSession session, WorkflowInstanceStatus status, string? reason)
    {
        var instance = session.Instance;
        if (instance.Status is WorkflowInstanceStatus.Completed or WorkflowInstanceStatus.Canceled or WorkflowInstanceStatus.Terminated)
        {
            return Result.Success();
        }

        instance.Status = status;
        instance.CancellationReason = reason;
        instance.EndedAt = session.Now;
        session.CancelOpenNodeInstances();
        session.RemoveAllBookmarks();
        if (status == WorkflowInstanceStatus.Canceled && session.Definition.EnableCompensation)
        {
            await CompensateAsync(session).ConfigureAwait(false);
        }

        session.Publish(status == WorkflowInstanceStatus.Canceled ? WorkflowTopics.InstanceCanceled : WorkflowTopics.InstanceTerminated, session.EventData() with { Reason = reason });
        session.NotifyParent = true;
        session.CascadeToChildren = status;
        return Result.Success();
    }

    /// <summary>
    /// 按完成顺序逆序补偿已完成的节点；单个补偿失败只记日志、继续。
    /// </summary>
    private async ValueTask CompensateAsync(WorkflowSession session)
    {
        foreach (var nodeInstance in session.NodeInstances.Reverse())
        {
            if (nodeInstance.Status != WorkflowNodeInstanceStatus.Completed)
            {
                continue;
            }

            var node = session.Graph.Node(nodeInstance.NodeId);
            if (node is null)
            {
                continue;
            }

            var compensated = await _executor.CompensateAsync(session, node, nodeInstance, CancellationToken.None).ConfigureAwait(false);
            if (compensated.IsFailure)
            {
                LogCompensationFailed(_logger, session.Instance.Id, nodeInstance.Id, compensated.Error!.Code);
                continue;
            }

            nodeInstance.Status = WorkflowNodeInstanceStatus.Compensated;
            nodeInstance.CompensatedAt = session.Now;
            session.Touch(nodeInstance);
        }
    }

    /// <summary>
    /// 找请求指向的已发布定义。
    /// </summary>
    private async ValueTask<Result<WorkflowDefinition>> FindPublishedDefinitionAsync(WorkflowStartRequest request, CancellationToken cancellationToken)
    {
        Result<Maybe<WorkflowDefinition>> found;
        string name;
        if (!string.IsNullOrWhiteSpace(request.DefinitionId))
        {
            name = request.DefinitionId;
            found = await _definitions.FindAsync(request.DefinitionId, cancellationToken).ConfigureAwait(false);
        }
        else if (request.Name is { } workflowName)
        {
            name = workflowName.Value;
            found = request.Version is { } version
                ? await _definitions.FindAsync(workflowName, version, cancellationToken).ConfigureAwait(false)
                : await _definitions.FindLatestPublishedAsync(workflowName, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            return Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.DefinitionNotFound, MessageArguments.Of(("name", string.Empty)));
        }

        if (!found.TryGetValue(out var maybe))
        {
            return found.Error!;
        }

        if (!maybe.TryGetValue(out var definition) || definition.Status != WorkflowDefinitionStatus.Published)
        {
            return Error.NotFound(WorkflowErrors.DefinitionNotFound, WorkflowMessages.DefinitionNotFound, MessageArguments.Of(("name", name)));
        }

        return definition;
    }

    /// <summary>
    /// 当前调用的租户。
    /// </summary>
    private string? CurrentTenantId()
        => _invocation is not null && _invocation.Current.TryGetValue(out var context) ? context.GetSecurity().Tenant?.Id : null;

    /// <summary>
    /// 实例状态不允许该操作。
    /// </summary>
    private static Error StatusConflict(WorkflowInstance instance)
        => Error.Conflict(WorkflowErrors.InstanceStatus, WorkflowMessages.InstanceStatus, MessageArguments.Of(("id", instance.Id), ("status", instance.Status.ToString())));

    /// <summary>
    /// 书签不存在。
    /// </summary>
    private static Error BookmarkNotFound(string id)
        => Error.NotFound(WorkflowErrors.BookmarkNotFound, WorkflowMessages.BookmarkNotFound, MessageArguments.Of(("id", id)));

    /// <summary>
    /// 根服务提供者：作用域提供者解析不到根，退回自身（测试里的裸容器）。
    /// </summary>
    private static IServiceProvider RootOf(IServiceProvider services)
        => services.GetService<IServiceScopeFactory>() is { } factory ? new ScopeFactoryProvider(factory) : services;

    /// <summary>
    /// 只提供 <see cref="IServiceScopeFactory"/> 的提供者：活动执行器只需要开新作用域。
    /// </summary>
    private sealed class ScopeFactoryProvider(IServiceScopeFactory factory) : IServiceProvider
    {
        /// <inheritdoc />
        public object? GetService(Type serviceType) => serviceType == typeof(IServiceScopeFactory) ? factory : null;
    }

    /// <summary>
    /// 信号恢复失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Resuming bookmark '{BookmarkId}' for signal '{Signal}' failed with '{ErrorCode}'.")]
    private static partial void LogSignalFailed(ILogger logger, string signal, string bookmarkId, string errorCode);

    /// <summary>
    /// 通知父实例失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Notifying parent instance '{ParentInstanceId}' failed with '{ErrorCode}'.")]
    private static partial void LogParentNotifyFailed(ILogger logger, string parentInstanceId, string errorCode);

    /// <summary>
    /// 补偿失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Compensating node instance '{NodeInstanceId}' of instance '{InstanceId}' failed with '{ErrorCode}'.")]
    private static partial void LogCompensationFailed(ILogger logger, string instanceId, string nodeInstanceId, string errorCode);
}
