// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// 事务性发布者：有活动工作单元时写入发件箱表（随业务事务提交或回滚），否则直接发布到代理。批量发布用仓储的批量新增一次写入。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class OutboxPublisher(IUnitOfWork unitOfWork, IRepository<OutboxMessage, Guid> outbox, IIdGenerator<Guid> ids, IMessageBroker broker, TimeProvider timeProvider) : IMessagePublisher
{
    /// <summary>
    /// 当前作用域的工作单元；活动时消息随它提交或回滚。
    /// </summary>
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    /// <summary>
    /// 发件箱仓储。
    /// </summary>
    private readonly IRepository<OutboxMessage, Guid> _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));

    /// <summary>
    /// 发件箱消息的 ID 生成器。
    /// </summary>
    private readonly IIdGenerator<Guid> _ids = ids ?? throw new ArgumentNullException(nameof(ids));

    /// <summary>
    /// 没有活动工作单元时直接发布的目标代理。
    /// </summary>
    private readonly IMessageBroker _broker = broker ?? throw new ArgumentNullException(nameof(broker));

    /// <summary>
    /// 时间源；记消息创建时间。
    /// </summary>
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc />
    public async ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (!TopicName.IsValid(topic))
        {
            return DirectPublisher.InvalidTopic(topic);
        }

        // 没有活动工作单元就没有可依附的事务，直接发布；有则写入发件箱，随业务事务提交或回滚，由投递循环发出
        if (_unitOfWork.State != UnitOfWorkState.Active)
        {
            return await _broker.PublishAsync(topic, envelope, cancellationToken).ConfigureAwait(false);
        }

        return await _outbox.AddAsync(Message(topic, envelope, _timeProvider.GetUtcNow()), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Result>> PublishManyAsync(string topic, IReadOnlyList<MessageEnvelope> envelopes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelopes);
        if (!TopicName.IsValid(topic))
        {
            return DirectPublisher.Repeat(DirectPublisher.InvalidTopic(topic), envelopes.Count);
        }

        if (envelopes.Count == 0)
        {
            return [];
        }

        if (_unitOfWork.State != UnitOfWorkState.Active)
        {
            return await _broker.PublishManyAsync(topic, envelopes, cancellationToken).ConfigureAwait(false);
        }

        // 整批一次写入发件箱表：与业务写入同一事务，提交后由投递循环按主题批量发出
        var now = _timeProvider.GetUtcNow();
        var messages = new OutboxMessage[envelopes.Count];
        for (var index = 0; index < envelopes.Count; index++)
        {
            ArgumentNullException.ThrowIfNull(envelopes[index]);
            messages[index] = Message(topic, envelopes[index], now);
        }

        return await _outbox.AddRangeAsync(messages, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 把信封编码成一条发件箱消息。
    /// </summary>
    private OutboxMessage Message(string topic, MessageEnvelope envelope, DateTimeOffset now) => new()
    {
        Id = _ids.NewId(),
        Topic = topic,
        Payload = CloudEventsFormat.Encode(envelope),
        CreatedAt = now,
    };
}
