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
/// 发件箱投递循环：按创建时间取未投递的消息，按主题分组批量发布到代理，成功记投递时间，失败记次数与原因，最后批量更新；每轮一个工作单元。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed partial class OutboxDispatcher(IServiceProvider services, IMessageBroker broker, OutboxOptions options, TimeProvider timeProvider, ILoggerFactory loggerFactory)
{
    /// <summary>
    /// 未投递消息的规约：投递时间为空。
    /// </summary>
    private static readonly Specification<OutboxMessage> _pending = new(static m => m.DispatchedAt == null);

    /// <summary>
    /// 根服务提供者；每轮从它创建新作用域以取得工作单元与仓储。
    /// </summary>
    private readonly IServiceProvider _services = services ?? throw new ArgumentNullException(nameof(services));

    /// <summary>
    /// 目标代理。
    /// </summary>
    private readonly IMessageBroker _broker = broker ?? throw new ArgumentNullException(nameof(broker));

    /// <summary>
    /// 发件箱选项：轮询间隔与批大小。
    /// </summary>
    private readonly OutboxOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// 时间源；记投递时间与驱动轮询延迟。
    /// </summary>
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<OutboxDispatcher>();

    /// <summary>
    /// 投递一轮；返回成功投递的条数。
    /// </summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = _services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var outbox = scope.ServiceProvider.GetRequiredService<IRepository<OutboxMessage, Guid>>();
        // 一轮一个工作单元：投递结果与消息状态一起提交，中途异常则整轮回滚
        await unitOfWork.BeginAsync(UnitOfWorkOptions.Default, cancellationToken).ConfigureAwait(false);

        try
        {
            var pending = await outbox.ListAsync(EntityQuery.All<OutboxMessage>().Where(_pending).OrderBy(static m => m.CreatedAt).Page(0, _options.BatchSize), cancellationToken).ConfigureAwait(false);
            var dispatched = pending.Count == 0 ? 0 : await DispatchAsync(pending, outbox, cancellationToken).ConfigureAwait(false);
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
    /// 按轮询间隔持续投递，直到取消。单轮异常记录后继续。
    /// </summary>
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
    /// 投递一批：解码每条消息，解码成功的按主题分组、各组一次批量发布（组内保持创建顺序），再把全部消息的尝试次数与结果一次批量更新。
    /// </summary>
    private async Task<int> DispatchAsync(IReadOnlyList<OutboxMessage> pending, IRepository<OutboxMessage, Guid> outbox, CancellationToken cancellationToken)
    {
        var outcomes = new Result[pending.Count];
        var groups = new Dictionary<string, (List<int> Indexes, List<MessageEnvelope> Envelopes)>(StringComparer.Ordinal);
        var topics = new List<string>();
        for (var index = 0; index < pending.Count; index++)
        {
            var decoded = CloudEventsFormat.Decode(pending[index].Payload);
            if (!decoded.TryGetValue(out var envelope))
            {
                outcomes[index] = decoded.ToResult();
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

        // 解码失败与发布失败都算一次尝试；失败不标记已投递，下一轮会再取到，只有成功才写投递时间
        var dispatched = 0;
        var now = _timeProvider.GetUtcNow();
        for (var index = 0; index < pending.Count; index++)
        {
            var message = pending[index];
            message.Attempts++;
            if (outcomes[index].IsSuccess)
            {
                message.DispatchedAt = now;
                message.LastError = null;
                dispatched++;
            }
            else
            {
                message.LastError = outcomes[index].Error?.ToString();
                LogDispatchFailed(_logger, message.Id, message.Topic, message.Attempts, outcomes[index].Error?.Code ?? "?");
            }
        }

        foreach (var updated in await outbox.UpdateRangeAsync(pending, cancellationToken).ConfigureAwait(false))
        {
            updated.ThrowIfFailure();
        }

        return dispatched;
    }

    /// <summary>
    /// 记录一条发件箱消息本次投递失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox message {MessageId} on topic {Topic} failed to dispatch on attempt {Attempts}: {ErrorCode}")]
    private static partial void LogDispatchFailed(ILogger logger, Guid messageId, string topic, int attempts, string errorCode);

    /// <summary>
    /// 记录一轮投递因异常整体失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "An outbox dispatch round failed; the next round will continue.")]
    private static partial void LogRoundFailed(ILogger logger, Exception exception);
}
