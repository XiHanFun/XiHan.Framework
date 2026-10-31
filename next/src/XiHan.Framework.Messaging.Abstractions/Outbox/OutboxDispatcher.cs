// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Outbox;

/// <summary>
/// 发件箱投递循环。
/// </summary>
/// <remarks>
/// 每轮一个工作单元：按创建时间取到期的未投递消息（未投递、未进死信、到了下一次投递时间），按主题分组批量发布到代理；成功记投递时间（保留期为 0 时直接删除），失败累加次数、记原因并按指数退避推后，次数用尽或无法解码进死信，最后批量更新；再删掉超过保留期的已投递行。退避中与死信的消息不占批次，不挡后面的消息。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed partial class OutboxDispatcher(IServiceProvider services, IMessageBroker broker, OutboxOptions options, TimeProvider timeProvider, ILoggerFactory loggerFactory)
{
    /// <summary>
    /// 根服务提供者。
    /// </summary>
    /// <remarks>
    /// 每轮从它创建新作用域以取得工作单元与仓储。
    /// </remarks>
    private readonly IServiceProvider _services = services ?? throw new ArgumentNullException(nameof(services));

    /// <summary>
    /// 目标代理。
    /// </summary>
    private readonly IMessageBroker _broker = broker ?? throw new ArgumentNullException(nameof(broker));

    /// <summary>
    /// 发件箱选项：轮询间隔、批大小、尝试上限、退避与保留期。
    /// </summary>
    private readonly OutboxOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// 时间源。
    /// </summary>
    /// <remarks>
    /// 判定到期、记投递与死信时间、推算下一次投递与驱动轮询延迟。
    /// </remarks>
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<OutboxDispatcher>();

    /// <summary>
    /// 投递一轮。
    /// </summary>
    /// <remarks>
    /// 返回成功投递的条数。代理整批抛出时本轮回滚并把异常抛给调用方，不计尝试。
    /// </remarks>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = _services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var outbox = scope.ServiceProvider.GetRequiredService<IRepository<OutboxMessage, Guid>>();
        // 一轮一个工作单元：投递结果与消息状态一起提交，中途异常则整轮回滚
        await unitOfWork.BeginAsync(UnitOfWorkOptions.Default, cancellationToken).ConfigureAwait(false);

        try
        {
            var now = _timeProvider.GetUtcNow();
            // 只取到期的：退避中的与死信的不占批次，永久失败的消息不会挡住后面的消息
            var due = new Specification<OutboxMessage>(m => m.DispatchedAt == null && m.DeadLetteredAt == null && (m.NextAttemptAt == null || m.NextAttemptAt <= now));
            var pending = await outbox.ListAsync(EntityQuery.All<OutboxMessage>().Where(due).OrderBy(static m => m.CreatedAt).Page(0, _options.BatchSize), cancellationToken).ConfigureAwait(false);
            var dispatched = pending.Count == 0 ? 0 : await DispatchAsync(pending, outbox, now, cancellationToken).ConfigureAwait(false);
            await PurgeAsync(outbox, now, cancellationToken).ConfigureAwait(false);
            (await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false)).ThrowIfFailure();
            return dispatched;
        }
        catch
        {
            // 只在工作单元仍活动时回滚；用 None 令牌保证取消引发的异常也能完成回滚
            if (unitOfWork.State == UnitOfWorkState.Active)
            {
                await unitOfWork.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            }

            throw;
        }
    }

    /// <summary>
    /// 按轮询间隔持续投递，直到取消。
    /// </summary>
    /// <remarks>
    /// 单轮异常记录后继续。
    /// </remarks>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            // 单轮异常只记录，循环继续；只有取消才退出
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogRoundFailed(_logger, exception);
            }

            try
            {
                await Task.Delay(_options.PollInterval, _timeProvider, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// 投递一批。
    /// </summary>
    /// <remarks>
    /// 解码每条消息，解码成功的按主题分组、各组一次批量发布（组内保持创建顺序），再把全部消息的结果一次批量写回：成功的更新或删除，失败的推后或进死信。
    /// </remarks>
    private async Task<int> DispatchAsync(IReadOnlyList<OutboxMessage> pending, IRepository<OutboxMessage, Guid> outbox, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var outcomes = new Result[pending.Count];
        var undecodable = new bool[pending.Count];
        var groups = new Dictionary<string, (List<int> Indexes, List<MessageEnvelope> Envelopes)>(StringComparer.Ordinal);
        var topics = new List<string>();
        for (var index = 0; index < pending.Count; index++)
        {
            var decoded = CloudEventsFormat.Decode(pending[index].Payload);
            if (!decoded.TryGetValue(out var envelope))
            {
                outcomes[index] = decoded.ToResult();
                undecodable[index] = true;
                continue;
            }

            if (!groups.TryGetValue(pending[index].Topic, out var group))
            {
                group = ([], []);
                groups[pending[index].Topic] = group;
                topics.Add(pending[index].Topic);
            }

            group.Indexes.Add(index);
            group.Envelopes.Add(envelope);
        }

        foreach (var topic in topics)
        {
            var (indexes, envelopes) = groups[topic];
            var published = await _broker.PublishManyAsync(topic, envelopes, cancellationToken).ConfigureAwait(false);
            for (var position = 0; position < indexes.Count; position++)
            {
                outcomes[indexes[position]] = published[position];
            }
        }

        // 每条都算一次尝试；成功的记投递时间（保留期为 0 时直接删除），失败的推后到退避之后，
        // 次数用尽或无法解码（重试也解不开）进死信，此后不再被取到
        var dispatched = 0;
        var updates = new List<OutboxMessage>(pending.Count);
        var removals = new List<OutboxMessage>();
        for (var index = 0; index < pending.Count; index++)
        {
            var message = pending[index];
            message.Attempts++;
            if (outcomes[index].IsSuccess)
            {
                dispatched++;
                if (_options.DispatchedRetention == TimeSpan.Zero)
                {
                    removals.Add(message);
                    continue;
                }

                message.DispatchedAt = now;
                message.LastError = null;
            }
            else
            {
                message.LastError = outcomes[index].Error?.ToString();
                var code = outcomes[index].Error?.Code ?? "?";
                if (undecodable[index] || message.Attempts >= _options.MaxAttempts)
                {
                    message.DeadLetteredAt = now;
                    LogDeadLettered(_logger, message.Id, message.Topic, message.Attempts, code);
                }
                else
                {
                    message.NextAttemptAt = now + _options.RetryDelayAfter(message.Attempts);
                    LogDispatchFailed(_logger, message.Id, message.Topic, message.Attempts, code, message.NextAttemptAt.Value);
                }
            }

            updates.Add(message);
        }

        if (updates.Count > 0)
        {
            foreach (var updated in await outbox.UpdateRangeAsync(updates, cancellationToken).ConfigureAwait(false))
            {
                updated.ThrowIfFailure();
            }
        }

        if (removals.Count > 0)
        {
            foreach (var removed in await outbox.RemoveRangeAsync(removals, cancellationToken).ConfigureAwait(false))
            {
                removed.ThrowIfFailure();
            }
        }

        return dispatched;
    }

    /// <summary>
    /// 删除超过保留期的已投递行。
    /// </summary>
    /// <remarks>
    /// 每轮最多 <see cref="OutboxOptions.BatchSize"/> 行，与投递同一工作单元；死信行不删。
    /// </remarks>
    private async Task PurgeAsync(IRepository<OutboxMessage, Guid> outbox, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cutoff = now - _options.DispatchedRetention;
        var expired = new Specification<OutboxMessage>(m => m.DispatchedAt != null && m.DispatchedAt <= cutoff);
        var rows = await outbox.ListAsync(EntityQuery.All<OutboxMessage>().Where(expired).OrderBy(static m => m.CreatedAt).Page(0, _options.BatchSize), cancellationToken).ConfigureAwait(false);
        if (rows.Count == 0)
        {
            return;
        }

        foreach (var removed in await outbox.RemoveRangeAsync(rows, cancellationToken).ConfigureAwait(false))
        {
            removed.ThrowIfFailure();
        }
    }

    /// <summary>
    /// 记录一条发件箱消息本次投递失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox message {MessageId} on topic {Topic} failed to dispatch on attempt {Attempts}: {ErrorCode}; the next attempt is due at {NextAttemptAt}")]
    private static partial void LogDispatchFailed(ILogger logger, Guid messageId, string topic, int attempts, string errorCode, DateTimeOffset nextAttemptAt);

    /// <summary>
    /// 记录一条发件箱消息进入死信。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox message {MessageId} on topic {Topic} was dead-lettered after {Attempts} attempts and will not be dispatched again: {ErrorCode}")]
    private static partial void LogDeadLettered(ILogger logger, Guid messageId, string topic, int attempts, string errorCode);

    /// <summary>
    /// 记录一轮投递因异常整体失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "An outbox dispatch round failed; the next round will continue.")]
    private static partial void LogRoundFailed(ILogger logger, Exception exception);
}
