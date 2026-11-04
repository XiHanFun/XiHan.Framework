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
/// 发件箱投递循环
/// </summary>
/// <remarks>
/// 每轮一个工作单元：按创建时间取到期的未投递消息（未投递、未进死信、到了下一次投递时间），首次投递的按主题分组批量发布，重试的逐条单独发布；成功记投递时间（保留期为 0 时直接删除），失败——代理返回失败结果，或代理抛出（调用方取消除外）——累加次数、记原因并按指数退避推后，次数用尽或无法解码进死信，最后批量更新；再删掉超过保留期的已投递行。一次发布抛出后本轮不再发布，余下的消息不计尝试、留到下一轮（ADR-0108）。退避中与死信的消息不占批次，不挡后面的消息
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed partial class OutboxDispatcher(IServiceProvider services, IMessageBroker broker, OutboxOptions options, TimeProvider timeProvider, ILoggerFactory loggerFactory)
{
    /// <summary>
    /// 根服务提供者
    /// </summary>
    /// <remarks>
    /// 每轮从它创建新作用域以取得工作单元与仓储
    /// </remarks>
    private readonly IServiceProvider _services = services ?? throw new ArgumentNullException(nameof(services));

    /// <summary>
    /// 目标代理
    /// </summary>
    private readonly IMessageBroker _broker = broker ?? throw new ArgumentNullException(nameof(broker));

    /// <summary>
    /// 发件箱选项：轮询间隔、批大小、尝试上限、退避与保留期
    /// </summary>
    private readonly OutboxOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// 时间源
    /// </summary>
    /// <remarks>
    /// 判定到期、记投递与死信时间、推算下一次投递与驱动轮询延迟
    /// </remarks>
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<OutboxDispatcher>();

    /// <summary>
    /// 投递一轮
    /// </summary>
    /// <remarks>
    /// 返回成功投递的条数。代理抛出与返回失败一样计一次尝试，本轮照常提交；只有调用方取消（<see cref="OperationCanceledException"/> 且 <paramref name="cancellationToken"/> 已取消）与存储失败让本轮回滚并把异常抛给调用方，回滚的一轮不计尝试，已发出的消息下一轮再发一次（至少一次）
    /// </remarks>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = _services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var outbox = scope.ServiceProvider.GetRequiredService<IRepository<OutboxMessage, Guid>>();
        // 一轮一个工作单元，只覆盖发件箱行的状态写回与保留期清理：代理不在事务里，发出去的收不回来
        // 所以代理抛出不回滚（DispatchAsync 把它记成这些消息的失败，照常提交）；只有取消与存储失败整轮回滚
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
    /// 按轮询间隔持续投递，直到取消
    /// </summary>
    /// <remarks>
    /// 单轮异常记录后继续
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
    /// 投递一批
    /// </summary>
    /// <remarks>
    /// 解码每条消息，排成发布单元（见 <see cref="Plan"/>）按序发布：一个单元抛出时它的每条记一次失败，本轮余下的单元不再发布。再把发布过的与无法解码的消息的结果一次批量写回：成功的更新或删除，失败的推后或进死信；没发布的不写回、不计尝试
    /// </remarks>
    private async Task<int> DispatchAsync(IReadOnlyList<OutboxMessage> pending, IRepository<OutboxMessage, Guid> outbox, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // 为空表示本轮没有发布它：排在抛出的单元之后，不计尝试，下一轮照常取到
        var outcomes = new Result?[pending.Count];
        var undecodable = new bool[pending.Count];
        foreach (var (topic, indexes, envelopes) in Plan(pending, outcomes, undecodable))
        {
            IReadOnlyList<Result> published;
            try
            {
                published = await _broker.PublishManyAsync(topic, envelopes, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // 调用方取消（宿主停止）不是这些消息的失败：抛给 RunOnceAsync 整轮回滚，不计尝试、不进死信
                throw;
            }
            catch (Exception exception)
            {
                // 代理抛出（基础设施失败，或某条消息让整批失败）与返回失败同样计入：这个单元的每条记一次失败的尝试
                // 然后本轮停止发布：代理可能整体不可用，余下的单元留到下一轮，一轮至多一次抛出的调用，停机期间不会逐条把整批都试一遍
                var thrown = Thrown(exception);
                LogPublishThrew(_logger, exception, envelopes.Count, topic);
                foreach (var index in indexes)
                {
                    outcomes[index] = thrown;
                }

                break;
            }

            for (var position = 0; position < indexes.Count; position++)
            {
                outcomes[indexes[position]] = published[position];
            }
        }

        // 发布过的每条都算一次尝试；成功的记投递时间（保留期为 0 时直接删除），失败的推后到退避之后，
        // 次数用尽或无法解码（重试也解不开）进死信，此后不再被取到
        var dispatched = 0;
        var updates = new List<OutboxMessage>(pending.Count);
        var removals = new List<OutboxMessage>();
        for (var index = 0; index < pending.Count; index++)
        {
            if (outcomes[index] is not { } outcome)
            {
                continue;
            }

            var message = pending[index];
            message.Attempts++;
            if (outcome.IsSuccess)
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
                message.LastError = outcome.Error?.ToString();
                var code = outcome.Error?.Code ?? "?";
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
    /// 把本轮要发布的消息排成发布单元
    /// </summary>
    /// <remarks>
    /// 按创建时间的顺序：首次投递的按主题合成一个单元（批量优先；单元排在该主题第一条的位置，组内保持创建顺序），重试的（尝试过至少一次）各自一个单元。解不开的载荷不进单元，直接记失败并标记无法解码
    /// </remarks>
    private static List<(string Topic, List<int> Indexes, List<MessageEnvelope> Envelopes)> Plan(IReadOnlyList<OutboxMessage> pending, Result?[] outcomes, bool[] undecodable)
    {
        var units = new List<(string Topic, List<int> Indexes, List<MessageEnvelope> Envelopes)>();
        var fresh = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < pending.Count; index++)
        {
            var decoded = CloudEventsFormat.Decode(pending[index].Payload);
            if (!decoded.TryGetValue(out var envelope))
            {
                outcomes[index] = decoded.ToResult();
                undecodable[index] = true;
                continue;
            }

            var topic = pending[index].Topic;
            // 重试逐条：代理整批抛出时分不出是哪一条，上一次失败时它可能就是让整批失败的那条；
            // 再与别的消息同批，它们会一起失败、一起退避，最后一起进死信。单独发布，同批的别的消息照常发出
            if (pending[index].Attempts > 0)
            {
                units.Add((topic, [index], [envelope]));
                continue;
            }

            if (!fresh.TryGetValue(topic, out var position))
            {
                position = units.Count;
                fresh[topic] = position;
                units.Add((topic, [], []));
            }

            units[position].Indexes.Add(index);
            units[position].Envelopes.Add(envelope);
        }

        return units;
    }

    /// <summary>
    /// 代理抛出的异常记成的失败
    /// </summary>
    /// <remarks>
    /// 与代理返回的失败同形：<see cref="MessagingErrors.BrokerUnavailable"/>，原因写进 <c>reason</c>。框架异常（代理契约里的基础设施失败）的消息是框架自己写的，照常记下；别的异常的消息可能带连接串、主机名等，不进发件箱行，原因只写异常类型名，异常本身只进告警日志
    /// </remarks>
    private static Error Thrown(Exception exception)
        => Error.Unavailable(MessagingErrors.BrokerUnavailable, MessagingMessages.BrokerUnavailable, MessageArguments.Of(("reason", exception is FrameworkException ? exception.Message : exception.GetType().FullName ?? exception.GetType().Name)));

    /// <summary>
    /// 删除超过保留期的已投递行
    /// </summary>
    /// <remarks>
    /// 每轮最多 <see cref="OutboxOptions.BatchSize"/> 行，与投递同一工作单元；死信行不删
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
    /// 记录一条发件箱消息本次投递失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox message {MessageId} on topic {Topic} failed to dispatch on attempt {Attempts}: {ErrorCode}; the next attempt is due at {NextAttemptAt}")]
    private static partial void LogDispatchFailed(ILogger logger, Guid messageId, string topic, int attempts, string errorCode, DateTimeOffset nextAttemptAt);

    /// <summary>
    /// 记录一条发件箱消息进入死信
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox message {MessageId} on topic {Topic} was dead-lettered after {Attempts} attempts and will not be dispatched again: {ErrorCode}")]
    private static partial void LogDeadLettered(ILogger logger, Guid messageId, string topic, int attempts, string errorCode);

    /// <summary>
    /// 记录一次批量发布抛出
    /// </summary>
    /// <remarks>
    /// 每次抛出的调用一条（不是每条消息一条），带异常；这些消息各自的失败与死信另有日志
    /// </remarks>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Publishing {Count} outbox message(s) on topic {Topic} threw; each counts as a failed attempt, and the rest of this round is left for the next round.")]
    private static partial void LogPublishThrew(ILogger logger, Exception exception, int count, string topic);

    /// <summary>
    /// 记录一轮投递因异常整体失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "An outbox dispatch round failed; the next round will continue.")]
    private static partial void LogRoundFailed(ILogger logger, Exception exception);
}
