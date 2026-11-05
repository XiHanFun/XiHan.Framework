// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Events;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Serialization;
using XiHan.Framework.Workflow.Abstractions.Storage;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Tasks;

/// <summary>
/// 用户任务服务
/// </summary>
/// <remarks>
/// 待办按书签查、完成经引擎消费书签、转办、加签、认领与释放在实例锁内改书签与节点状态、退回经引擎跳转、催办只发事件。作用域服务：每个操作在调用方的事务里执行（没有可加入的事务时自己开一次调用），只读写调用方租户的分区
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WorkflowTaskService : IWorkflowTaskService
{
    /// <summary>
    /// 退回写进节点历史的结果
    /// </summary>
    private const string ReturnedOutcome = "returned";

    /// <summary>
    /// 引擎
    /// </summary>
    private readonly IWorkflowEngine _engine;

    /// <summary>
    /// 定义存储
    /// </summary>
    private readonly IWorkflowDefinitionStore _definitions;

    /// <summary>
    /// 实例存储
    /// </summary>
    private readonly IWorkflowInstanceStore _instances;

    /// <summary>
    /// 书签存储
    /// </summary>
    private readonly IWorkflowBookmarkStore _bookmarks;


    /// <summary>
    /// 时钟
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 事件发布
    /// </summary>
    private readonly WorkflowEventPublisher _events;

    /// <summary>
    /// 执行范围：事务的加入或自开、调用方租户、实例锁的持有
    /// </summary>
    private readonly WorkflowScope _scope;

    /// <summary>
    /// 创建（作用域服务）
    /// </summary>
    public WorkflowTaskService(IWorkflowEngine engine, IWorkflowDefinitionStore definitions, IWorkflowInstanceStore instances, IWorkflowBookmarkStore bookmarks, TimeProvider clock, IServiceProvider services, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        _instances = instances ?? throw new ArgumentNullException(nameof(instances));
        _bookmarks = bookmarks ?? throw new ArgumentNullException(nameof(bookmarks));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _events = new WorkflowEventPublisher(services.GetService<IMessagePublisher>(), loggerFactory.CreateLogger<WorkflowTaskService>());
        _scope = services.GetRequiredService<WorkflowScope>();
    }

    /// <summary>
    /// 调用方的分区：调用的租户，空为平台
    /// </summary>
    private string? Tenant => _scope.TenantId;

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<UserTask>>> PendingAsync(string assigneeId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assigneeId);
        return _scope.RunAsync<IWorkflowTaskService, IReadOnlyList<UserTask>>(false, "workflow.task.pending", async () =>
        {
            var found = await _bookmarks.ByKindAndKeyAsync(Tenant, BookmarkKinds.UserTask, assigneeId, cancellationToken).ConfigureAwait(false);
            return found.TryGetValue(out var bookmarks) ? await ToTasksAsync(bookmarks, cancellationToken).ConfigureAwait(false) : found.Error!;
        }, service => service.PendingAsync(assigneeId, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<UserTask>>> PendingByInstanceAsync(string instanceId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        return _scope.RunAsync<IWorkflowTaskService, IReadOnlyList<UserTask>>(false, "workflow.task.pending-by-instance", async () =>
        {
            var found = await _bookmarks.ByInstanceAsync(Tenant, instanceId, cancellationToken).ConfigureAwait(false);
            return found.TryGetValue(out var bookmarks)
                ? await ToTasksAsync(bookmarks.Where(static bookmark => string.Equals(bookmark.Kind, BookmarkKinds.UserTask, StringComparison.Ordinal)).ToList(), cancellationToken).ConfigureAwait(false)
                : found.Error!;
        }, service => service.PendingByInstanceAsync(instanceId, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<Result<UserTaskPage>> QueryAsync(UserTaskQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _scope.RunAsync<IWorkflowTaskService, UserTaskPage>(false, "workflow.task.query", () => QueryCoreAsync(query, cancellationToken), service => service.QueryAsync(query, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// 按条件查询待办（在事务里）
    /// </summary>
    /// <remarks>
    /// 过滤、排序与分页都下推到存储；这里只校验条件、把书签换成任务视图
    /// </remarks>
    private async ValueTask<Result<UserTaskPage>> QueryCoreAsync(UserTaskQuery query, CancellationToken cancellationToken)
    {
        var assigneeId = query.AssigneeId?.Trim();
        var instanceId = query.InstanceId?.Trim();
        if (string.IsNullOrEmpty(assigneeId) && string.IsNullOrEmpty(instanceId))
        {
            return Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.TaskQueryTargetMissing);
        }

        // 分页越界是调用方可以改正的输入（查询常由请求绑定），以校验错误返回而不是抛出；上限与持久化的查询条数上限一致
        if (query.Skip < 0 || query.Limit is < 1 or > UserTaskQuery.MaxLimit)
        {
            return Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.TaskQueryPageInvalid, MessageArguments.Of(("skip", query.Skip), ("limit", query.Limit), ("max", UserTaskQuery.MaxLimit)));
        }

        var nodeId = query.NodeId?.Trim();
        var normalized = query with
        {
            AssigneeId = string.IsNullOrEmpty(assigneeId) ? null : assigneeId,
            InstanceId = string.IsNullOrEmpty(instanceId) ? null : instanceId,
            NodeId = string.IsNullOrEmpty(nodeId) ? null : nodeId,
        };
        var listed = await _bookmarks.ListUserTasksAsync(Tenant, normalized, cancellationToken).ConfigureAwait(false);
        if (!listed.TryGetValue(out var bookmarks))
        {
            return listed.Error!;
        }

        var counted = await _bookmarks.CountUserTasksAsync(Tenant, normalized, cancellationToken).ConfigureAwait(false);
        if (!counted.TryGetValue(out var total))
        {
            return counted.Error!;
        }

        // 存储已经只给运行中实例的书签；换视图时再按实例过滤一次只为防御（数据库实现在同一事务里，两次读一致）
        var tasks = await ToTasksAsync(bookmarks, cancellationToken).ConfigureAwait(false);
        return tasks.TryGetValue(out var items) ? new UserTaskPage(items, total) : tasks.Error!;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result<WorkflowInstance>>> CompleteAsync(IReadOnlyList<UserTaskCompletion> completions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(completions);
        return _scope.RunBatchAsync<IWorkflowTaskService, WorkflowInstance>(completions.Count, "workflow.task.complete", () => EachAsync(completions, CompleteOneAsync, cancellationToken), service => service.CompleteAsync(completions, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> TransferAsync(IReadOnlyList<UserTaskTransfer> transfers, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transfers);
        return _scope.RunBatchAsync<IWorkflowTaskService>(transfers.Count, "workflow.task.transfer", () => EachAsync(transfers, TransferOneAsync, cancellationToken), service => service.TransferAsync(transfers, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> AddAssigneesAsync(IReadOnlyList<UserTaskAddition> additions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(additions);
        return _scope.RunBatchAsync<IWorkflowTaskService>(additions.Count, "workflow.task.add-assignees", () => EachAsync(additions, AddOneAsync, cancellationToken), service => service.AddAssigneesAsync(additions, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> ClaimAsync(IReadOnlyList<UserTaskClaim> claims, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claims);
        return _scope.RunBatchAsync<IWorkflowTaskService>(claims.Count, "workflow.task.claim", () => EachAsync(claims, ClaimOneAsync, cancellationToken), service => service.ClaimAsync(claims, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> ReleaseAsync(IReadOnlyList<UserTaskRelease> releases, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(releases);
        return _scope.RunBatchAsync<IWorkflowTaskService>(releases.Count, "workflow.task.release", () => EachAsync(releases, ReleaseOneAsync, cancellationToken), service => service.ReleaseAsync(releases, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result<WorkflowInstance>>> ReturnAsync(IReadOnlyList<UserTaskReturn> returns, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(returns);
        return _scope.RunBatchAsync<IWorkflowTaskService, WorkflowInstance>(returns.Count, "workflow.task.return", () => EachAsync(returns, ReturnOneAsync, cancellationToken), service => service.ReturnAsync(returns, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Result>> UrgeAsync(IReadOnlyList<UserTaskUrge> urges, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(urges);
        return _scope.RunBatchAsync<IWorkflowTaskService>(urges.Count, "workflow.task.urge", () => EachAsync(urges, UrgeOneAsync, cancellationToken), service => service.UrgeAsync(urges, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// 按入参顺序逐项执行，逐项结果
    /// </summary>
    private static async ValueTask<IReadOnlyList<TResult>> EachAsync<TItem, TResult>(IReadOnlyList<TItem> items, Func<TItem, CancellationToken, ValueTask<TResult>> action, CancellationToken cancellationToken)
    {
        var results = new List<TResult>(items.Count);
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await action(item, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    /// <summary>
    /// 完成一个任务：校验操作者与结果后经引擎消费书签，再发任务完成事件
    /// </summary>
    private async ValueTask<Result<WorkflowInstance>> CompleteOneAsync(UserTaskCompletion completion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(completion);
        var found = await FindTaskBookmarkAsync(completion.TaskId, completion.ActorId, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var bookmark))
        {
            return found.Error!;
        }

        var allowed = await AllowedOutcomesAsync(bookmark, cancellationToken).ConfigureAwait(false);
        if (!allowed.TryGetValue(out var outcomes))
        {
            return allowed.Error!;
        }

        if (string.IsNullOrWhiteSpace(completion.Outcome) || !outcomes.Contains(completion.Outcome, StringComparer.OrdinalIgnoreCase))
        {
            return Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.TaskOutcomeInvalid, MessageArguments.Of(("outcome", completion.Outcome)));
        }

        var inputs = WorkflowJson.CloneObject(completion.Variables);
        inputs[UserTaskPayloadKeys.ActorId] = completion.ActorId;
        inputs[UserTaskPayloadKeys.Outcome] = completion.Outcome;
        inputs[UserTaskPayloadKeys.Comment] = completion.Comment;
        var resumed = await _engine.ResumeAsync(bookmark.Id, inputs, completion.ActorId, cancellationToken).ConfigureAwait(false);
        if (!resumed.TryGetValue(out var instance))
        {
            return resumed.Error!;
        }

        await _events.FlushAsync([new PendingEvent(WorkflowTopics.TaskCompleted, EventData(instance, bookmark) with { ActorId = completion.ActorId, Outcome = completion.Outcome, Reason = completion.Comment })], cancellationToken).ConfigureAwait(false);
        return instance;
    }

    /// <summary>
    /// 转办一个任务：在实例锁内把书签的键换成目标并更新节点状态里的指派人
    /// </summary>
    private async ValueTask<Result> TransferOneAsync(UserTaskTransfer transfer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        if (string.IsNullOrWhiteSpace(transfer.TargetAssigneeId))
        {
            return Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.TaskAssigneeExists, MessageArguments.Of(("assignee", string.Empty)));
        }

        var found = await FindTaskBookmarkAsync(transfer.TaskId, transfer.ActorId, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var peek))
        {
            return found.Error!;
        }

        return await WithLockedTaskAsync(peek, transfer.ActorId, async (bookmark, nodeInstance, instance) =>
        {
            var target = transfer.TargetAssigneeId.Trim();
            var assignees = WorkflowValues.ToStringList(nodeInstance.State[UserTaskPayloadKeys.Assignees]).ToList();
            if (assignees.Contains(target, StringComparer.Ordinal))
            {
                return Error.Conflict(WorkflowErrors.AssigneeExists, WorkflowMessages.TaskAssigneeExists, MessageArguments.Of(("assignee", target)));
            }

            var index = assignees.FindIndex(assignee => string.Equals(assignee, transfer.ActorId, StringComparison.Ordinal));
            if (index >= 0)
            {
                assignees[index] = target;
            }
            else
            {
                assignees.Add(target);
            }

            nodeInstance.State[UserTaskPayloadKeys.Assignees] = StringArray(assignees);
            if (string.Equals(ClaimedBy(nodeInstance), transfer.ActorId, StringComparison.Ordinal))
            {
                // 认领人把任务转出去：认领随任务一起转给目标
                nodeInstance.State[UserTaskPayloadKeys.ClaimedBy] = target;
                bookmark.Payload[UserTaskPayloadKeys.ClaimedBy] = target;
            }

            bookmark.Key = target;
            var saved = await _bookmarks.SaveAsync(Tenant, [bookmark], cancellationToken).ConfigureAwait(false);
            if (saved.IsFailure)
            {
                return saved;
            }

            saved = await _instances.SaveNodeInstancesAsync(Tenant, [nodeInstance], cancellationToken).ConfigureAwait(false);
            if (saved.IsFailure)
            {
                return saved;
            }

            await _events.FlushAsync([new PendingEvent(WorkflowTopics.TaskTransferred, EventData(instance, bookmark) with { ActorId = transfer.ActorId, AssigneeId = target, Reason = transfer.Comment })], cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 加签
    /// </summary>
    /// <remarks>
    /// 在实例锁内追加指派人；Any / All 立即建书签（已认领的任务只有认领人还持有书签，所以只有认领人能加签），Sequential 排到队尾
    /// </remarks>
    private async ValueTask<Result> AddOneAsync(UserTaskAddition addition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(addition);
        var found = await FindTaskBookmarkAsync(addition.TaskId, addition.ActorId, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var peek))
        {
            return found.Error!;
        }

        return await WithLockedTaskAsync(peek, addition.ActorId, async (bookmark, nodeInstance, instance) =>
        {
            var assignees = WorkflowValues.ToStringList(nodeInstance.State[UserTaskPayloadKeys.Assignees]).ToList();
            var added = (addition.AssigneeIds ?? []).Select(static assignee => assignee.Trim()).Where(static assignee => assignee.Length > 0).Distinct(StringComparer.Ordinal)
                .Where(assignee => !assignees.Contains(assignee, StringComparer.Ordinal)).ToList();
            if (added.Count == 0)
            {
                return Error.Conflict(WorkflowErrors.AssigneeExists, WorkflowMessages.TaskAssigneeExists, MessageArguments.Of(("assignee", string.Join(", ", addition.AssigneeIds ?? []))));
            }

            assignees.AddRange(added);
            nodeInstance.State[UserTaskPayloadKeys.Assignees] = StringArray(assignees);
            var policy = WorkflowValues.ConvertOr(nodeInstance.State[UserTaskPayloadKeys.Policy], UserTaskCompletionPolicy.Any);
            List<WorkflowBookmark> created = policy != UserTaskCompletionPolicy.Sequential
                ? added.Select(assignee => CloneBookmark(bookmark, assignee)).ToList()
                : [];
            var saved = await _instances.SaveNodeInstancesAsync(Tenant, [nodeInstance], cancellationToken).ConfigureAwait(false);
            if (saved.IsFailure)
            {
                return saved;
            }

            if (created.Count > 0)
            {
                saved = await _bookmarks.SaveAsync(Tenant, created, cancellationToken).ConfigureAwait(false);
                if (saved.IsFailure)
                {
                    return saved;
                }

                await _events.FlushAsync(created.Select(task => new PendingEvent(WorkflowTopics.TaskCreated, EventData(instance, task) with { ActorId = addition.ActorId, Reason = addition.Comment, Payload = WorkflowJson.CloneObject(task.Payload) })).ToList(), cancellationToken).ConfigureAwait(false);
            }

            return Result.Success();
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 认领
    /// </summary>
    /// <remarks>
    /// 只对 <c>Any</c> 策略；在实例锁内记认领人、撤下其他指派人的书签、本人书签记 <c>claimedBy</c>
    /// </remarks>
    private async ValueTask<Result> ClaimOneAsync(UserTaskClaim claim, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        var found = await FindTaskBookmarkAsync(claim.TaskId, claim.ActorId, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var peek))
        {
            return found.Error!;
        }

        return await WithLockedTaskAsync(peek, claim.ActorId, async (bookmark, nodeInstance, instance) =>
        {
            var policy = WorkflowValues.ConvertOr(nodeInstance.State[UserTaskPayloadKeys.Policy], UserTaskCompletionPolicy.Any);
            if (policy != UserTaskCompletionPolicy.Any)
            {
                return Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.TaskNotClaimable, MessageArguments.Of(("task", bookmark.Id)));
            }

            // 已被别人认领即冲突；本人重复认领幂等
            var claimedBy = ClaimedBy(nodeInstance);
            if (!string.IsNullOrEmpty(claimedBy) && !string.Equals(claimedBy, claim.ActorId, StringComparison.Ordinal))
            {
                return Error.Conflict(WorkflowErrors.AssigneeExists, WorkflowMessages.TaskClaimed, MessageArguments.Of(("task", bookmark.Id), ("actor", claimedBy)));
            }

            var others = await OtherTaskBookmarksAsync(bookmark, cancellationToken).ConfigureAwait(false);
            if (!others.TryGetValue(out var siblings))
            {
                return others.Error!;
            }

            nodeInstance.State[UserTaskPayloadKeys.ClaimedBy] = claim.ActorId;
            bookmark.Payload[UserTaskPayloadKeys.ClaimedBy] = claim.ActorId;
            var saved = await _instances.SaveNodeInstancesAsync(Tenant, [nodeInstance], cancellationToken).ConfigureAwait(false);
            if (saved.IsFailure)
            {
                return saved;
            }

            saved = await _bookmarks.SaveAsync(Tenant, [bookmark], cancellationToken).ConfigureAwait(false);
            if (saved.IsFailure)
            {
                return saved;
            }

            if (siblings.Count > 0)
            {
                saved = await _bookmarks.DeleteAsync(Tenant, siblings.Select(static sibling => sibling.Id).ToList(), cancellationToken).ConfigureAwait(false);
                if (saved.IsFailure)
                {
                    return saved;
                }
            }

            await _events.FlushAsync([new PendingEvent(WorkflowTopics.TaskClaimed, EventData(instance, bookmark) with { ActorId = claim.ActorId })], cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 释放认领
    /// </summary>
    /// <remarks>
    /// 操作者必须是认领人；在实例锁内清认领人、按节点状态里的指派人为其他人重建书签
    /// </remarks>
    private async ValueTask<Result> ReleaseOneAsync(UserTaskRelease release, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        var found = await FindTaskBookmarkAsync(release.TaskId, release.ActorId, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var peek))
        {
            return found.Error!;
        }

        return await WithLockedTaskAsync(peek, release.ActorId, async (bookmark, nodeInstance, instance) =>
        {
            if (!string.Equals(ClaimedBy(nodeInstance), release.ActorId, StringComparison.Ordinal))
            {
                return Error.Denied(WorkflowErrors.TaskDenied, WorkflowMessages.TaskDenied, MessageArguments.Of(("task", bookmark.Id), ("actor", release.ActorId)));
            }

            var others = await OtherTaskBookmarksAsync(bookmark, cancellationToken).ConfigureAwait(false);
            if (!others.TryGetValue(out var siblings))
            {
                return others.Error!;
            }

            nodeInstance.State[UserTaskPayloadKeys.ClaimedBy] = null;
            bookmark.Payload.Remove(UserTaskPayloadKeys.ClaimedBy);
            var existing = siblings.Select(static sibling => sibling.Key).ToHashSet(StringComparer.Ordinal);
            var created = WorkflowValues.ToStringList(nodeInstance.State[UserTaskPayloadKeys.Assignees])
                .Where(assignee => !string.Equals(assignee, release.ActorId, StringComparison.Ordinal) && !existing.Contains(assignee))
                .Select(assignee => CloneBookmark(bookmark, assignee))
                .ToList();
            var saved = await _instances.SaveNodeInstancesAsync(Tenant, [nodeInstance], cancellationToken).ConfigureAwait(false);
            if (saved.IsFailure)
            {
                return saved;
            }

            saved = await _bookmarks.SaveAsync(Tenant, [bookmark, .. created], cancellationToken).ConfigureAwait(false);
            if (saved.IsFailure)
            {
                return saved;
            }

            var events = new List<PendingEvent> { new(WorkflowTopics.TaskReleased, EventData(instance, bookmark) with { ActorId = release.ActorId }) };
            events.AddRange(created.Select(task => new PendingEvent(WorkflowTopics.TaskCreated, EventData(instance, task) with { ActorId = release.ActorId, Payload = WorkflowJson.CloneObject(task.Payload) })));
            await _events.FlushAsync(events, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 退回
    /// </summary>
    /// <remarks>
    /// 在实例锁内定目标并把 <c>returned</c> 记进节点历史，释放锁后经引擎跳转到目标节点，再发退回事件
    /// </remarks>
    private async ValueTask<Result<WorkflowInstance>> ReturnOneAsync(UserTaskReturn request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var found = await FindTaskBookmarkAsync(request.TaskId, request.ActorId, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var peek))
        {
            return found.Error!;
        }

        string? target = null;
        var prepared = await WithLockedTaskAsync(peek, request.ActorId, async (bookmark, nodeInstance, instance) =>
        {
            var resolved = await ReturnTargetAsync(request, bookmark, nodeInstance, instance, cancellationToken).ConfigureAwait(false);
            if (!resolved.TryGetValue(out var nodeId))
            {
                return resolved.Error!;
            }

            target = nodeId;
            var history = (nodeInstance.State[UserTaskPayloadKeys.History] as JsonArray)?.DeepClone() as JsonArray ?? [];
            history.Add((JsonNode?)new JsonObject
            {
                [UserTaskPayloadKeys.ActorId] = request.ActorId,
                [UserTaskPayloadKeys.Outcome] = ReturnedOutcome,
                [UserTaskPayloadKeys.Comment] = request.Comment,
                ["at"] = _clock.GetUtcNow(),
            });
            nodeInstance.State[UserTaskPayloadKeys.History] = history;
            return await _instances.SaveNodeInstancesAsync(Tenant, [nodeInstance], cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
        if (prepared.IsFailure)
        {
            return prepared.Error!;
        }

        // 跳转由引擎自己加锁执行：上一步交还的锁在本作用域里空闲，引擎复用它；跳转会取消本节点实例与它的书签
        var jumped = (await _engine.JumpAsync([new WorkflowJump(peek.InstanceId, target!, request.Comment, null, request.ActorId)], cancellationToken).ConfigureAwait(false))[0];
        if (!jumped.TryGetValue(out var result))
        {
            return jumped.Error!;
        }

        await _events.FlushAsync([new PendingEvent(WorkflowTopics.TaskReturned, EventData(result, peek) with { ActorId = request.ActorId, NodeId = target, Reason = request.Comment })], cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// 催办
    /// </summary>
    /// <remarks>
    /// 任务存在且属于本租户、实例运行中即发布催办事件；不要求操作者是指派人
    /// </remarks>
    private async ValueTask<Result> UrgeOneAsync(UserTaskUrge urge, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(urge);
        var found = await FindTenantTaskBookmarkAsync(urge.TaskId, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var bookmark))
        {
            return found.Error!;
        }

        var instanceFound = await FindRunningInstanceAsync(bookmark.InstanceId, cancellationToken).ConfigureAwait(false);
        if (!instanceFound.TryGetValue(out var instance))
        {
            return instanceFound.Error!;
        }

        await _events.FlushAsync([new PendingEvent(WorkflowTopics.TaskUrged, EventData(instance, bookmark) with { ActorId = urge.ActorId, Reason = urge.Comment })], cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <summary>
    /// 退回目标
    /// </summary>
    /// <remarks>
    /// 给了就用给的；否则本节点实例之前最近完成的 <c>UserTask</c> 节点，再没有则 <c>Start</c> 的首条出边目标
    /// </remarks>
    private async ValueTask<Result<string>> ReturnTargetAsync(UserTaskReturn request, WorkflowBookmark bookmark, WorkflowNodeInstance current, WorkflowInstance instance, CancellationToken cancellationToken)
    {
        var definitionFound = await _definitions.FindAsync(Tenant, instance.DefinitionId, cancellationToken).ConfigureAwait(false);
        if (!definitionFound.TryGetValue(out var definitionMaybe))
        {
            return definitionFound.Error!;
        }

        definitionMaybe.TryGetValue(out var definition);
        if (!string.IsNullOrWhiteSpace(request.TargetNodeId))
        {
            // 给定目标先对定义核一遍：目标不存在时不写历史、不跳转
            var target = request.TargetNodeId.Trim();
            return definition is not null && definition.FindNode(target).TryGetValue(out _)
                ? target
                : Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.NodeNotFound, MessageArguments.Of(("id", instance.Id), ("node", target)));
        }

        var nodesFound = await _instances.NodeInstancesAsync(Tenant, instance.Id, cancellationToken).ConfigureAwait(false);
        if (!nodesFound.TryGetValue(out var nodeInstances))
        {
            return nodesFound.Error!;
        }

        // 节点实例按开始时间升序：取本节点实例之前最后一个已完成的用户任务
        string? previous = null;
        foreach (var candidate in nodeInstances)
        {
            if (string.Equals(candidate.Id, current.Id, StringComparison.Ordinal))
            {
                break;
            }

            if (candidate.Status == WorkflowNodeInstanceStatus.Completed && string.Equals(candidate.ActivityType.Value, ActivityTypes.UserTask, StringComparison.Ordinal))
            {
                previous = candidate.NodeId;
            }
        }

        if (previous is not null)
        {
            return previous;
        }

        if (definition is not null
            && definition.Nodes.FirstOrDefault(static node => string.Equals(node.ActivityType.Value, ActivityTypes.Start, StringComparison.Ordinal)) is { } start
            && definition.Transitions.Where(transition => string.Equals(transition.SourceNodeId, start.Id, StringComparison.Ordinal)).OrderBy(static transition => transition.Priority).FirstOrDefault() is { } first)
        {
            return first.TargetNodeId;
        }

        return Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.TaskNoReturnTarget, MessageArguments.Of(("task", bookmark.Id)));
    }

    /// <summary>
    /// 在实例锁内重读书签与节点实例后执行 <paramref name="action"/>
    /// </summary>
    private async ValueTask<Result> WithLockedTaskAsync(WorkflowBookmark peek, string actorId, Func<WorkflowBookmark, WorkflowNodeInstance, WorkflowInstance, ValueTask<Result>> action, CancellationToken cancellationToken)
    {
        var acquired = await _scope.LockAsync(peek.InstanceId, cancellationToken).ConfigureAwait(false);
        if (!acquired.TryGetValue(out var handle))
        {
            return acquired.Error!;
        }

        await using (handle.ConfigureAwait(false))
        {
            // 锁内重读：书签可能已被消费、转办
            var refreshed = await FindTaskBookmarkAsync(peek.Id, actorId, cancellationToken).ConfigureAwait(false);
            if (!refreshed.TryGetValue(out var bookmark))
            {
                return refreshed.Error!;
            }

            var instanceFound = await FindRunningInstanceAsync(bookmark.InstanceId, cancellationToken).ConfigureAwait(false);
            if (!instanceFound.TryGetValue(out var instance))
            {
                return instanceFound.Error!;
            }

            var nodeFound = await _instances.FindNodeInstanceAsync(Tenant, bookmark.NodeInstanceId, cancellationToken).ConfigureAwait(false);
            if (!nodeFound.TryGetValue(out var nodeMaybe))
            {
                return nodeFound.Error!;
            }

            if (!nodeMaybe.TryGetValue(out var nodeInstance))
            {
                return Error.NotFound(WorkflowErrors.BookmarkNotFound, WorkflowMessages.BookmarkNotFound, MessageArguments.Of(("id", bookmark.Id)));
            }

            return await action(bookmark, nodeInstance, instance).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 找实例并要求运行中：不存在 → <see cref="WorkflowErrors.InstanceNotFound"/>，不在运行中 → <see cref="WorkflowErrors.InstanceStatus"/>
    /// </summary>
    private async ValueTask<Result<WorkflowInstance>> FindRunningInstanceAsync(string instanceId, CancellationToken cancellationToken)
    {
        var instanceFound = await _instances.FindAsync(Tenant, instanceId, cancellationToken).ConfigureAwait(false);
        if (!instanceFound.TryGetValue(out var instanceMaybe))
        {
            return instanceFound.Error!;
        }

        if (!instanceMaybe.TryGetValue(out var instance))
        {
            return Error.NotFound(WorkflowErrors.InstanceNotFound, WorkflowMessages.InstanceNotFound, MessageArguments.Of(("id", instanceId)));
        }

        if (instance.Status != WorkflowInstanceStatus.Running)
        {
            return WorkflowStatusConflicts.Of(instance);
        }

        return instance;
    }

    /// <summary>
    /// 找用户任务书签并校验操作者是它的指派人
    /// </summary>
    private async ValueTask<Result<WorkflowBookmark>> FindTaskBookmarkAsync(string taskId, string actorId, CancellationToken cancellationToken)
    {
        var found = await FindTenantTaskBookmarkAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var bookmark))
        {
            return found.Error!;
        }

        // 认证 → 租户 → 授权：租户已在上一步确认，这里确认操作者就是指派人
        if (string.IsNullOrWhiteSpace(actorId) || !string.Equals(bookmark.Key, actorId, StringComparison.Ordinal))
        {
            return Error.Denied(WorkflowErrors.TaskDenied, WorkflowMessages.TaskDenied, MessageArguments.Of(("task", taskId), ("actor", actorId ?? string.Empty)));
        }

        return bookmark;
    }

    /// <summary>
    /// 找调用方分区的用户任务书签
    /// </summary>
    /// <remarks>
    /// 不存在、种类不对或在别的分区的都按不存在处理（不泄露别的租户的任务）
    /// </remarks>
    private async ValueTask<Result<WorkflowBookmark>> FindTenantTaskBookmarkAsync(string taskId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return Error.NotFound(WorkflowErrors.BookmarkNotFound, WorkflowMessages.BookmarkNotFound, MessageArguments.Of(("id", taskId ?? string.Empty)));
        }

        var found = await _bookmarks.FindAsync(Tenant, taskId, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var maybe))
        {
            return found.Error!;
        }

        if (!maybe.TryGetValue(out var bookmark) || !string.Equals(bookmark.Kind, BookmarkKinds.UserTask, StringComparison.Ordinal))
        {
            return Error.NotFound(WorkflowErrors.BookmarkNotFound, WorkflowMessages.BookmarkNotFound, MessageArguments.Of(("id", taskId)));
        }

        return bookmark;
    }

    /// <summary>
    /// 同一节点实例上其他指派人的用户任务书签
    /// </summary>
    private async ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> OtherTaskBookmarksAsync(WorkflowBookmark bookmark, CancellationToken cancellationToken)
    {
        var found = await _bookmarks.ByInstanceAsync(Tenant, bookmark.InstanceId, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var bookmarks))
        {
            return found.Error!;
        }

        return bookmarks.Where(candidate =>
                string.Equals(candidate.Kind, BookmarkKinds.UserTask, StringComparison.Ordinal)
                && string.Equals(candidate.NodeInstanceId, bookmark.NodeInstanceId, StringComparison.Ordinal)
                && !string.Equals(candidate.Id, bookmark.Id, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// 任务所在节点允许的结果
    /// </summary>
    /// <remarks>
    /// 缺省 <c>approved</c> / <c>rejected</c>
    /// </remarks>
    private async ValueTask<Result<IReadOnlyList<string>>> AllowedOutcomesAsync(WorkflowBookmark bookmark, CancellationToken cancellationToken)
    {
        var instanceFound = await _instances.FindAsync(Tenant, bookmark.InstanceId, cancellationToken).ConfigureAwait(false);
        if (!instanceFound.TryGetValue(out var instanceMaybe))
        {
            return instanceFound.Error!;
        }

        if (!instanceMaybe.TryGetValue(out var instance))
        {
            return Error.NotFound(WorkflowErrors.InstanceNotFound, WorkflowMessages.InstanceNotFound, MessageArguments.Of(("id", bookmark.InstanceId)));
        }

        var definitionFound = await _definitions.FindAsync(Tenant, instance.DefinitionId, cancellationToken).ConfigureAwait(false);
        if (!definitionFound.TryGetValue(out var definitionMaybe))
        {
            return definitionFound.Error!;
        }

        IReadOnlyList<string> allowed = [];
        if (definitionMaybe.TryGetValue(out var definition) && definition.FindNode(bookmark.NodeId).TryGetValue(out var node))
        {
            allowed = WorkflowValues.ToStringList(node.Properties.TryGetPropertyValue(UserTaskActivity.AllowedOutcomesProperty, out var outcomes) ? outcomes : null);
        }

        return allowed.Count == 0 ? Result.Success<IReadOnlyList<string>>([UserTaskOutcomes.Approved, UserTaskOutcomes.Rejected]) : Result.Success(allowed);
    }

    /// <summary>
    /// 书签 → 任务视图：只保留实例运行中的（书签本来就只来自调用方的分区）
    /// </summary>
    private async ValueTask<Result<IReadOnlyList<UserTask>>> ToTasksAsync(IReadOnlyList<WorkflowBookmark> bookmarks, CancellationToken cancellationToken)
    {
        var mine = bookmarks;
        if (mine.Count == 0)
        {
            return Result.Success<IReadOnlyList<UserTask>>([]);
        }

        var found = await _instances.FindManyAsync(Tenant, mine.Select(static bookmark => bookmark.InstanceId).Distinct(StringComparer.Ordinal).ToList(), cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var instances))
        {
            return found.Error!;
        }

        var running = instances.Where(static instance => instance.Status == WorkflowInstanceStatus.Running).ToDictionary(static instance => instance.Id, StringComparer.Ordinal);
        var tasks = new List<UserTask>(mine.Count);
        foreach (var bookmark in mine)
        {
            if (running.TryGetValue(bookmark.InstanceId, out var instance))
            {
                tasks.Add(UserTask.From(bookmark, instance));
            }
        }

        return tasks;
    }

    /// <summary>
    /// 以同一节点实例、同样载荷给另一个指派人建书签（载荷不带认领人）
    /// </summary>
    private WorkflowBookmark CloneBookmark(WorkflowBookmark bookmark, string assignee)
    {
        var payload = WorkflowJson.CloneObject(bookmark.Payload);
        payload.Remove(UserTaskPayloadKeys.ClaimedBy);
        return new WorkflowBookmark
        {
            Id = WorkflowIdentifiers.NewId(),
            InstanceId = bookmark.InstanceId,
            NodeId = bookmark.NodeId,
            NodeInstanceId = bookmark.NodeInstanceId,
            Kind = BookmarkKinds.UserTask,
            Key = assignee,
            Payload = payload,
            CorrelationId = bookmark.CorrelationId,
            CreatedAt = _clock.GetUtcNow(),
            TenantId = bookmark.TenantId,
        };
    }

    /// <summary>
    /// 任务事件的数据
    /// </summary>
    private WorkflowEventData EventData(WorkflowInstance instance, WorkflowBookmark bookmark) => new()
    {
        InstanceId = instance.Id,
        DefinitionId = instance.DefinitionId,
        DefinitionName = instance.DefinitionName.Value,
        DefinitionVersion = instance.DefinitionVersion,
        Status = instance.Status,
        Title = instance.Title,
        NodeId = bookmark.NodeId,
        NodeInstanceId = bookmark.NodeInstanceId,
        TaskId = bookmark.Id,
        AssigneeId = bookmark.Key,
        CorrelationId = instance.CorrelationId,
        StarterId = instance.StarterId,
        ParentInstanceId = instance.ParentInstanceId,
        TenantId = instance.TenantId,
        CcUserIds = WorkflowValues.ToStringList(bookmark.Payload.TryGetPropertyValue(UserTaskPayloadKeys.CcUserIds, out var cc) ? cc : null),
        At = _clock.GetUtcNow(),
    };

    /// <summary>
    /// 节点状态里的认领人
    /// </summary>
    /// <remarks>
    /// 没有为空
    /// </remarks>
    private static string? ClaimedBy(WorkflowNodeInstance nodeInstance)
        => nodeInstance.State.TryGetPropertyValue(UserTaskPayloadKeys.ClaimedBy, out var value) && value is JsonValue scalar ? WorkflowValues.AsString(scalar) : null;

    /// <summary>
    /// 字符串数组
    /// </summary>
    private static JsonArray StringArray(IEnumerable<string> values) => new(values.Select(static value => (JsonNode?)JsonValue.Create(value)).ToArray());
}
