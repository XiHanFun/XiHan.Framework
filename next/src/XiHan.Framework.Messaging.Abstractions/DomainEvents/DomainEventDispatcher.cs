// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Events;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.DomainEvents;

/// <summary>
/// 领域事件派发器（<see cref="IDomainEventDispatcher"/> 的 Messaging 实现，作用域）。
/// </summary>
/// <remarks>
/// 把聚合里的事件按 <see cref="DomainEventRegistry"/> 映射成 CloudEvents 信封，同一主题一次 <see cref="IMessagePublisher.PublishManyAsync"/>（批量优先），全部成功后清空各聚合的缓冲。当前作用域的发布者是发件箱时，事件随业务写入同一事务。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    /// <summary>
    /// 登记表。
    /// </summary>
    private readonly DomainEventRegistry _registry;

    /// <summary>
    /// 当前作用域的发布者（直接发布或发件箱）。
    /// </summary>
    private readonly IMessagePublisher _publisher;

    /// <summary>
    /// 信封标识的生成器。
    /// </summary>
    private readonly IIdGenerator<Guid> _ids;

    /// <summary>
    /// 创建。
    /// </summary>
    public DomainEventDispatcher(DomainEventRegistry registry, IMessagePublisher publisher, IIdGenerator<Guid> ids)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _ids = ids ?? throw new ArgumentNullException(nameof(ids));
    }

    /// <inheritdoc />
    public async ValueTask<Result> DispatchAsync(IReadOnlyList<IHasDomainEvents> aggregates, string? tenantId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(aggregates);
        // 先把全部事件映射成信封并按主题分组（主题按首次出现顺序），任何一个事件未登记就整体失败、一条也不发
        var topics = new List<string>();
        var groups = new Dictionary<string, List<MessageEnvelope>>(StringComparer.Ordinal);
        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                if (!_registry.Find(domainEvent.GetType()).TryGetValue(out var registration))
                {
                    return Error.Validation(MessagingErrors.DomainEventUnregistered, MessagingMessages.DomainEventUnregistered, MessageArguments.Of(("event", domainEvent.GetType().FullName ?? domainEvent.GetType().Name)));
                }

                if (!groups.TryGetValue(registration.Topic, out var envelopes))
                {
                    envelopes = [];
                    groups[registration.Topic] = envelopes;
                    topics.Add(registration.Topic);
                }

                envelopes.Add(registration.ToEnvelope(domainEvent, _ids.NewId(), tenantId));
            }
        }

        // 同一主题一次批量发布；任一项失败即派发失败，调用方回滚，缓冲保留
        foreach (var topic in topics)
        {
            var results = await _publisher.PublishManyAsync(topic, groups[topic], cancellationToken).ConfigureAwait(false);
            foreach (var result in results)
            {
                if (result.IsFailure)
                {
                    return result;
                }
            }
        }

        foreach (var aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        return Result.Success();
    }
}
