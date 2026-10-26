// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Events;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Serialization;
using XiHan.Framework.Workflow.Abstractions.Storage;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.ExternalTasks;

/// <summary>
/// 外部任务服务（RFC-0021）：领取在实例锁内把锁写进 <c>External</c> 书签载荷并把书签 <c>DueAt</c> 设为锁到期时刻（到期由轮询恢复、活动原样重新挂起即自动解锁）；
/// 完成与无重试的失败先在锁内校验工作者持锁，释放锁后经引擎消费书签（引擎自己加锁，实例锁不可重入）；带重试的失败与延长锁只改书签。作用域服务：当前租户取自本次调用。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WorkflowExternalTaskService : IWorkflowExternalTaskService
{
    /// <summary>
    /// 引擎。
    /// </summary>
    private readonly IWorkflowEngine _engine;

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
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 事件发布。
    /// </summary>
    private readonly WorkflowEventPublisher _events;

    /// <summary>
    /// 本次调用的上下文访问器。
    /// </summary>
    private readonly IInvocationAccessor? _invocation;

    /// <summary>
    /// 创建（作用域服务）。
    /// </summary>
    public WorkflowExternalTaskService(IWorkflowEngine engine, IWorkflowInstanceStore instances, IWorkflowBookmarkStore bookmarks, IWorkflowInstanceLock instanceLock, TimeProvider clock, IServiceProvider services, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _instances = instances ?? throw new ArgumentNullException(nameof(instances));
        _bookmarks = bookmarks ?? throw new ArgumentNullException(nameof(bookmarks));
        _lock = instanceLock ?? throw new ArgumentNullException(nameof(instanceLock));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _events = new WorkflowEventPublisher(services.GetService<IMessagePublisher>(), loggerFactory.CreateLogger<WorkflowExternalTaskService>());
        _invocation = services.GetService<IInvocationAccessor>();
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<ExternalTask>>> FetchAsync(string topic, string workerId, TimeSpan lockDuration, int maxTasks, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        if (lockDuration <= TimeSpan.Zero)
        {
            return Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.TaskQueryInvalid, MessageArguments.Of(("reason", "lockDuration must be positive")));
        }

        if (maxTasks < 1)
        {
            return Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.TaskQueryInvalid, MessageArguments.Of(("reason", "maxTasks must be at least 1")));
        }

        var found = await _bookmarks.ByKindAndKeyAsync(BookmarkKinds.External, topic, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var bookmarks))
        {
            return found.Error!;
        }

        // 锁外先筛：本租户、未锁定或锁已过期、已到可领取时刻；实例是否运行中在锁内再确认
        var tenant = CurrentTenantId();
        var now = _clock.GetUtcNow();
        var candidates = bookmarks
            .Where(bookmark => string.Equals(bookmark.TenantId, tenant, StringComparison.Ordinal) && IsFetchable(bookmark, now))
            .OrderBy(static bookmark => bookmark.CreatedAt)
            .ToList();
        var fetched = new List<ExternalTask>();
        foreach (var peek in candidates)
        {
            if (fetched.Count >= maxTasks)
            {
                break;
            }

            cancellationToken.ThrowIfCancellationRequested();
            ExternalTask? task = null;
            var locked = await WithLockedBookmarkAsync(peek, async (bookmark, instance) =>
            {
                // 锁内重读后仍可领取才上锁：两个工作者同时领同一条时只有先拿到实例锁的成功
                var at = _clock.GetUtcNow();
                if (!IsFetchable(bookmark, at))
                {
                    return Result.Success();
                }

                var until = at + lockDuration;
                bookmark.Payload[ExternalTaskPayloadKeys.LockedBy] = workerId;
                bookmark.Payload[ExternalTaskPayloadKeys.LockedUntil] = Stamp(until);
                bookmark.Payload.Remove(ExternalTaskPayloadKeys.NotBefore);
                bookmark.DueAt = until;
                var saved = await _bookmarks.SaveAsync([bookmark], cancellationToken).ConfigureAwait(false);
                if (saved.IsFailure)
                {
                    return saved;
                }

                task = View(bookmark, instance, until);
                return Result.Success();
            }, cancellationToken).ConfigureAwait(false);
            if (locked.IsFailure)
            {
                // 书签在锁内已消失或实例已不在运行中：跳过这条；存储错误则整体失败
                if (locked.Error!.Code is WorkflowErrors.BookmarkNotFound or WorkflowErrors.InstanceNotFound or WorkflowErrors.InstanceStatus)
                {
                    continue;
                }

                return locked.Error!;
            }

            if (task is not null)
            {
                fetched.Add(task);
            }
        }

        return fetched;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Result<WorkflowInstance>>> CompleteAsync(IReadOnlyList<ExternalTaskCompletion> completions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(completions);
        var results = new List<Result<WorkflowInstance>>(completions.Count);
        foreach (var completion in completions)
        {
            results.Add(await CompleteOneAsync(completion, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Result>> FailAsync(IReadOnlyList<ExternalTaskFailure> failures, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(failures);
        var results = new List<Result>(failures.Count);
        foreach (var failure in failures)
        {
            results.Add(await FailOneAsync(failure, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Result>> ExtendLockAsync(IReadOnlyList<ExternalTaskLockExtension> extensions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        var results = new List<Result>(extensions.Count);
        foreach (var extension in extensions)
        {
            results.Add(await ExtendOneAsync(extension, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    /// <summary>
    /// 完成一项：锁内校验工作者持锁，释放锁后经引擎以 <c>outputs</c> 恢复节点（输出合并进实例变量），再发完成事件。
    /// </summary>
    private async ValueTask<Result<WorkflowInstance>> CompleteOneAsync(ExternalTaskCompletion completion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(completion);
        var verified = await VerifyLockAsync(completion.TaskId, completion.WorkerId, cancellationToken).ConfigureAwait(false);
        if (!verified.TryGetValue(out var bookmark))
        {
            return verified.Error!;
        }

        var inputs = new JsonObject
        {
            [ExternalTaskPayloadKeys.Outputs] = WorkflowJson.CloneObject(completion.Outputs),
            [ExternalTaskPayloadKeys.WorkerId] = completion.WorkerId,
        };
        var resumed = await _engine.ResumeAsync(bookmark.Id, inputs, null, cancellationToken).ConfigureAwait(false);
        if (!resumed.TryGetValue(out var instance))
        {
            return resumed.Error!;
        }

        await _events.FlushAsync([new PendingEvent(WorkflowTopics.ExternalCompleted, EventData(instance, bookmark) with { ActorId = completion.WorkerId, Payload = WorkflowJson.CloneObject(completion.Outputs) })], cancellationToken).ConfigureAwait(false);
        return instance;
    }

    /// <summary>
    /// 失败一项：有 <c>RetryAfter</c> 时在锁内解锁、尝试次数加一、后推可领取时刻并把书签到期设到那时；没有时释放锁后经引擎以 <c>error</c> 恢复节点使其故障。
    /// </summary>
    private async ValueTask<Result> FailOneAsync(ExternalTaskFailure failure, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(failure);
        if (failure.RetryAfter is { } retryAfter)
        {
            if (retryAfter < TimeSpan.Zero)
            {
                return Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.TaskQueryInvalid, MessageArguments.Of(("reason", "RetryAfter must not be negative")));
            }

            var peekFound = await FindExternalBookmarkAsync(failure.TaskId, cancellationToken).ConfigureAwait(false);
            if (!peekFound.TryGetValue(out var peek))
            {
                return peekFound.Error!;
            }

            return await WithLockedBookmarkAsync(peek, async (bookmark, instance) =>
            {
                var now = _clock.GetUtcNow();
                if (!HoldsLock(bookmark, failure.WorkerId, now))
                {
                    return Denied(bookmark.Id, failure.WorkerId);
                }

                // 解锁并后推：到期由轮询恢复，活动去掉 notBefore 原样重新挂起，之后任何工作者都可再领
                var notBefore = now + retryAfter;
                bookmark.Payload[ExternalTaskPayloadKeys.Attempt] = Attempt(bookmark) + 1;
                bookmark.Payload.Remove(ExternalTaskPayloadKeys.LockedBy);
                bookmark.Payload.Remove(ExternalTaskPayloadKeys.LockedUntil);
                bookmark.Payload[ExternalTaskPayloadKeys.NotBefore] = Stamp(notBefore);
                bookmark.DueAt = notBefore;
                var saved = await _bookmarks.SaveAsync([bookmark], cancellationToken).ConfigureAwait(false);
                if (saved.IsFailure)
                {
                    return saved;
                }

                await _events.FlushAsync([new PendingEvent(WorkflowTopics.ExternalFailed, EventData(instance, bookmark) with { ActorId = failure.WorkerId, Reason = failure.Error, WillRetry = true })], cancellationToken).ConfigureAwait(false);
                return Result.Success();
            }, cancellationToken).ConfigureAwait(false);
        }

        var verified = await VerifyLockAsync(failure.TaskId, failure.WorkerId, cancellationToken).ConfigureAwait(false);
        if (!verified.TryGetValue(out var held))
        {
            return verified.Error!;
        }

        var inputs = new JsonObject
        {
            [ExternalTaskPayloadKeys.Error] = failure.Error,
            [ExternalTaskPayloadKeys.WorkerId] = failure.WorkerId,
        };
        var resumed = await _engine.ResumeAsync(held.Id, inputs, null, cancellationToken).ConfigureAwait(false);
        if (!resumed.TryGetValue(out var instance))
        {
            return resumed.Error!;
        }

        await _events.FlushAsync([new PendingEvent(WorkflowTopics.ExternalFailed, EventData(instance, held) with { ActorId = failure.WorkerId, Reason = failure.Error, WillRetry = false })], cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <summary>
    /// 延长一项的锁：锁内校验工作者持锁后把锁到期与书签到期都设为现在加时长。
    /// </summary>
    private async ValueTask<Result> ExtendOneAsync(ExternalTaskLockExtension extension, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(extension);
        if (extension.LockDuration <= TimeSpan.Zero)
        {
            return Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.TaskQueryInvalid, MessageArguments.Of(("reason", "LockDuration must be positive")));
        }

        var peekFound = await FindExternalBookmarkAsync(extension.TaskId, cancellationToken).ConfigureAwait(false);
        if (!peekFound.TryGetValue(out var peek))
        {
            return peekFound.Error!;
        }

        return await WithLockedBookmarkAsync(peek, async (bookmark, _) =>
        {
            var now = _clock.GetUtcNow();
            if (!HoldsLock(bookmark, extension.WorkerId, now))
            {
                return Denied(bookmark.Id, extension.WorkerId);
            }

            var until = now + extension.LockDuration;
            bookmark.Payload[ExternalTaskPayloadKeys.LockedUntil] = Stamp(until);
            bookmark.DueAt = until;
            return await _bookmarks.SaveAsync([bookmark], cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 在实例锁内确认工作者持有未过期的锁；返回锁内重读的书签。锁在返回前释放：随后的引擎调用自己加锁。
    /// </summary>
    private async ValueTask<Result<WorkflowBookmark>> VerifyLockAsync(string taskId, string workerId, CancellationToken cancellationToken)
    {
        var peekFound = await FindExternalBookmarkAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (!peekFound.TryGetValue(out var peek))
        {
            return peekFound.Error!;
        }

        WorkflowBookmark? held = null;
        var verified = await WithLockedBookmarkAsync(peek, (bookmark, _) =>
        {
            if (!HoldsLock(bookmark, workerId, _clock.GetUtcNow()))
            {
                return ValueTask.FromResult<Result>(Denied(bookmark.Id, workerId));
            }

            held = bookmark;
            return ValueTask.FromResult(Result.Success());
        }, cancellationToken).ConfigureAwait(false);
        if (verified.IsFailure)
        {
            return verified.Error!;
        }

        return held!;
    }

    /// <summary>
    /// 在实例锁内重读书签与实例（要求实例运行中）后执行 <paramref name="action"/>。
    /// </summary>
    private async ValueTask<Result> WithLockedBookmarkAsync(WorkflowBookmark peek, Func<WorkflowBookmark, WorkflowInstance, ValueTask<Result>> action, CancellationToken cancellationToken)
    {
        var acquired = await _lock.AcquireAsync(peek.InstanceId, cancellationToken).ConfigureAwait(false);
        if (!acquired.TryGetValue(out var handle))
        {
            return acquired.Error!;
        }

        await using (handle.ConfigureAwait(false))
        {
            // 锁内重读：书签可能已被消费（完成、锁到期重建）或被别的工作者上锁
            var refreshed = await FindExternalBookmarkAsync(peek.Id, cancellationToken).ConfigureAwait(false);
            if (!refreshed.TryGetValue(out var bookmark))
            {
                return refreshed.Error!;
            }

            var instanceFound = await _instances.FindAsync(bookmark.InstanceId, cancellationToken).ConfigureAwait(false);
            if (!instanceFound.TryGetValue(out var instanceMaybe))
            {
                return instanceFound.Error!;
            }

            if (!instanceMaybe.TryGetValue(out var instance))
            {
                return Error.NotFound(WorkflowErrors.InstanceNotFound, WorkflowMessages.InstanceNotFound, MessageArguments.Of(("id", bookmark.InstanceId)));
            }

            if (instance.Status != WorkflowInstanceStatus.Running)
            {
                return Error.Conflict(WorkflowErrors.InstanceStatus, WorkflowMessages.InstanceStatus, MessageArguments.Of(("id", instance.Id), ("status", instance.Status.ToString())));
            }

            return await action(bookmark, instance).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 找本租户的外部任务书签；不存在、种类不对或不是本租户的都按不存在处理（不泄露别的租户的任务）。
    /// </summary>
    private async ValueTask<Result<WorkflowBookmark>> FindExternalBookmarkAsync(string taskId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return Error.NotFound(WorkflowErrors.BookmarkNotFound, WorkflowMessages.BookmarkNotFound, MessageArguments.Of(("id", taskId ?? string.Empty)));
        }

        var found = await _bookmarks.FindAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var maybe))
        {
            return found.Error!;
        }

        if (!maybe.TryGetValue(out var bookmark) || !string.Equals(bookmark.Kind, BookmarkKinds.External, StringComparison.Ordinal) || !string.Equals(bookmark.TenantId, CurrentTenantId(), StringComparison.Ordinal))
        {
            return Error.NotFound(WorkflowErrors.BookmarkNotFound, WorkflowMessages.BookmarkNotFound, MessageArguments.Of(("id", taskId)));
        }

        return bookmark;
    }

    /// <summary>
    /// 可领取：未锁定或锁已过期，且没有 <c>notBefore</c> 或已到。
    /// </summary>
    private static bool IsFetchable(WorkflowBookmark bookmark, DateTimeOffset now)
        => (Time(bookmark, ExternalTaskPayloadKeys.LockedUntil) is not { } until || until < now)
           && (Time(bookmark, ExternalTaskPayloadKeys.NotBefore) is not { } notBefore || notBefore <= now);

    /// <summary>
    /// 工作者持有未过期的锁。
    /// </summary>
    private static bool HoldsLock(WorkflowBookmark bookmark, string workerId, DateTimeOffset now)
        => !string.IsNullOrWhiteSpace(workerId)
           && string.Equals(Text(bookmark.Payload, ExternalTaskPayloadKeys.LockedBy), workerId, StringComparison.Ordinal)
           && Time(bookmark, ExternalTaskPayloadKeys.LockedUntil) is { } until && until >= now;

    /// <summary>
    /// 工作者没有持锁的错误。
    /// </summary>
    private static Error Denied(string taskId, string workerId)
        => Error.Denied(WorkflowErrors.ExternalTaskDenied, WorkflowMessages.ExternalTaskDenied, MessageArguments.Of(("task", taskId), ("worker", workerId)));

    /// <summary>
    /// 外部任务视图。
    /// </summary>
    private static ExternalTask View(WorkflowBookmark bookmark, WorkflowInstance instance, DateTimeOffset lockedUntil) => new()
    {
        Id = bookmark.Id,
        InstanceId = instance.Id,
        NodeId = bookmark.NodeId,
        Topic = bookmark.Key ?? Text(bookmark.Payload, ExternalTaskPayloadKeys.Topic) ?? string.Empty,
        Payload = bookmark.Payload.TryGetPropertyValue(ExternalTaskPayloadKeys.Payload, out var payload) && payload is JsonObject declared ? WorkflowJson.CloneObject(declared) : null,
        Variables = WorkflowJson.CloneObject(instance.Variables),
        LockedUntil = lockedUntil,
        Attempt = Attempt(bookmark),
        CorrelationId = bookmark.CorrelationId ?? instance.CorrelationId,
        TenantId = bookmark.TenantId,
    };

    /// <summary>
    /// 载荷里的尝试次数；没有为 1。
    /// </summary>
    private static int Attempt(WorkflowBookmark bookmark)
        => bookmark.Payload.TryGetPropertyValue(ExternalTaskPayloadKeys.Attempt, out var value) && WorkflowValues.TryConvert<int>(value, out var attempt) && attempt >= 1 ? attempt : 1;

    /// <summary>
    /// 载荷里的时刻；没有或不是时刻为空。
    /// </summary>
    private static DateTimeOffset? Time(WorkflowBookmark bookmark, string key)
        => bookmark.Payload.TryGetPropertyValue(key, out var value) && WorkflowValues.TryConvert<DateTimeOffset>(value, out var at) ? at : null;

    /// <summary>
    /// 载荷里的字符串；没有或不是标量为空。
    /// </summary>
    private static string? Text(JsonObject payload, string key)
        => payload.TryGetPropertyValue(key, out var value) && value is JsonValue scalar ? WorkflowValues.AsString(scalar) : null;

    /// <summary>
    /// 时刻的 ISO-8601 往返形式（存储往返后仍可解析）。
    /// </summary>
    private static string Stamp(DateTimeOffset at) => at.ToString("O", CultureInfo.InvariantCulture);

    /// <summary>
    /// 外部任务事件的数据。
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
        CorrelationId = instance.CorrelationId,
        StarterId = instance.StarterId,
        ParentInstanceId = instance.ParentInstanceId,
        TenantId = instance.TenantId,
        At = _clock.GetUtcNow(),
    };

    /// <summary>
    /// 当前调用的租户。
    /// </summary>
    private string? CurrentTenantId()
        => _invocation is not null && _invocation.Current.TryGetValue(out var context) ? context.GetSecurity().Tenant?.Id : null;
}
