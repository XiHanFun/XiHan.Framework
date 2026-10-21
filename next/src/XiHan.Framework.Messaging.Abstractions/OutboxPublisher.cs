// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// 事务性发布者：有活动工作单元时写入发件箱表（随业务事务提交或回滚），否则直接发布到代理。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class OutboxPublisher(IUnitOfWork unitOfWork, IRepository<OutboxMessage, Guid> outbox, IIdGenerator<Guid> ids, IMessageBroker broker, TimeProvider timeProvider) : IMessagePublisher
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    private readonly IRepository<OutboxMessage, Guid> _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
    private readonly IIdGenerator<Guid> _ids = ids ?? throw new ArgumentNullException(nameof(ids));
    private readonly IMessageBroker _broker = broker ?? throw new ArgumentNullException(nameof(broker));
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc />
    public async ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (!TopicName.IsValid(topic))
        {
            return Error.Validation(MessagingErrors.InvalidTopic, $"主题 '{topic}' 不合法。");
        }

        if (_unitOfWork.State != UnitOfWorkState.Active)
        {
            return await _broker.PublishAsync(topic, envelope, cancellationToken).ConfigureAwait(false);
        }

        var message = new OutboxMessage
        {
            Id = _ids.NewId(),
            Topic = topic,
            Payload = CloudEventsFormat.Encode(envelope),
            CreatedAt = _timeProvider.GetUtcNow(),
        };
        return await _outbox.AddAsync(message, cancellationToken).ConfigureAwait(false);
    }
}
