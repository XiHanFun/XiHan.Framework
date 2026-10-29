// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using XiHan.Framework.Domain.Events;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.DomainEvents;

/// <summary>
/// 一种领域事件如何成为消息。
/// </summary>
/// <remarks>
/// 发布到哪个主题、CloudEvents <c>type</c>、源生成的序列化元数据（不经反射）与可选的 <c>subject</c> 选择器。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DomainEventRegistration
{
    /// <summary>
    /// 创建。
    /// </summary>
    /// <remarks>
    /// 主题须满足主题文法，类型不能为空。
    /// </remarks>
    /// <param name="eventType">事件的 CLR 类型。</param>
    /// <param name="topic">主题。</param>
    /// <param name="cloudEventType">CloudEvents <c>type</c>（如 <c>orders.placed</c>）。</param>
    /// <param name="info">事件类型的序列化元数据。</param>
    /// <param name="subject">从事件算出 <c>subject</c>；为 <see langword="null"/> 时信封不带 <c>subject</c>。</param>
    public DomainEventRegistration(Type eventType, string topic, string cloudEventType, JsonTypeInfo info, Func<IDomainEvent, string?>? subject)
    {
        EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
        if (!TopicName.IsValid(topic))
        {
            throw new ArgumentException($"Topic '{topic}' is invalid: it must match ^[a-z0-9][a-z0-9._-]{{0,127}}$.", nameof(topic));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(cloudEventType);
        Topic = topic;
        CloudEventType = cloudEventType;
        Info = info ?? throw new ArgumentNullException(nameof(info));
        Subject = subject;
    }

    /// <summary>
    /// 事件的 CLR 类型。
    /// </summary>
    public Type EventType { get; }

    /// <summary>
    /// 主题。
    /// </summary>
    public string Topic { get; }

    /// <summary>
    /// CloudEvents <c>type</c>。
    /// </summary>
    public string CloudEventType { get; }

    /// <summary>
    /// 事件类型的序列化元数据。
    /// </summary>
    public JsonTypeInfo Info { get; }

    /// <summary>
    /// <c>subject</c> 选择器。
    /// </summary>
    public Func<IDomainEvent, string?>? Subject { get; }

    /// <summary>
    /// 把一个事件映射成信封。
    /// </summary>
    /// <remarks>
    /// <c>id</c> 为给定标识的 <c>N</c> 格式、<c>source</c> 为 <c>/{主题}</c>、<c>time</c> 为事件发生时间、<c>data</c> 为事件的 JSON；有租户时带 <c>xihantenant</c> 扩展。
    /// </remarks>
    public MessageEnvelope ToEnvelope(IDomainEvent domainEvent, Guid id, string? tenantId)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var envelope = new MessageEnvelope
        {
            Id = id.ToString("N"),
            Source = "/" + Topic,
            Type = CloudEventType,
            Time = domainEvent.OccurredAt,
            Subject = Subject?.Invoke(domainEvent),
            Data = JsonSerializer.SerializeToUtf8Bytes(domainEvent, Info),
        };
        return tenantId is null ? envelope : envelope.WithExtension(EnvelopeExtensions.Tenant, tenantId);
    }
}
