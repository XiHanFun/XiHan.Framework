// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.DomainEvents;

/// <summary>
/// 领域事件登记表。
/// </summary>
/// <remarks>
/// 事件类型 → <see cref="DomainEventRegistration"/>，有界（默认 10000）；由 <c>AddDomainEvent</c> 在装配期填充，派发时按事件类型查找。重复登记同一事件类型或超出容量是装配错误，抛出携带 <see cref="MessagingErrors.DomainEventRegistration"/> 的 <see cref="FrameworkException"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DomainEventRegistry
{
    /// <summary>
    /// 默认容量。
    /// </summary>
    public const int DefaultCapacity = 10_000;

    /// <summary>
    /// 登记表。
    /// </summary>
    /// <remarks>
    /// 装配期写入、派发期并发读取。
    /// </remarks>
    private readonly ConcurrentDictionary<Type, DomainEventRegistration> _registrations = new();

    /// <summary>
    /// 容量。
    /// </summary>
    private readonly int _capacity;

    /// <summary>
    /// 创建。
    /// </summary>
    public DomainEventRegistry(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }

    /// <summary>
    /// 已登记的事件类型数。
    /// </summary>
    public int Count => _registrations.Count;

    /// <summary>
    /// 登记。
    /// </summary>
    /// <remarks>
    /// 重复或超容抛出 <see cref="FrameworkException"/>。
    /// </remarks>
    public void Add(DomainEventRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        // 先查容量再写入：容量判定与写入不在同一原子步骤里，装配期是单线程的，这里只防止无界增长
        if (_registrations.Count >= _capacity)
        {
            throw new FrameworkException(MessagingErrors.DomainEventRegistration, $"The domain event registry has reached its capacity of {_capacity}.");
        }

        if (!_registrations.TryAdd(registration.EventType, registration))
        {
            throw new FrameworkException(MessagingErrors.DomainEventRegistration, $"Domain event '{registration.EventType.FullName}' is already registered; each event type maps to one topic and type.");
        }
    }

    /// <summary>
    /// 按事件类型查找。
    /// </summary>
    public Maybe<DomainEventRegistration> Find(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        return _registrations.TryGetValue(eventType, out var registration) ? Maybe.Some(registration) : default;
    }
}
