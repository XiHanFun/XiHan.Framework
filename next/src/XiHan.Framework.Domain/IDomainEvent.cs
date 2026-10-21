// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Domain;

/// <summary>
/// 领域事件：聚合内已经发生的事实。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IDomainEvent
{
    /// <summary>
    /// 发生时间（UTC）。
    /// </summary>
    DateTimeOffset OccurredAt { get; }
}

/// <summary>
/// 收集领域事件的聚合。持久化子系统在提交后取出并派发，然后清空。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IHasDomainEvents
{
    /// <summary>
    /// 尚未派发的领域事件。
    /// </summary>
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    /// <summary>
    /// 清空已派发的事件。
    /// </summary>
    void ClearDomainEvents();
}

/// <summary>
/// 领域事件的有界缓冲，供聚合以组合方式持有。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class DomainEventBuffer
{
    /// <summary>
    /// 默认容量上限。
    /// </summary>
    public const int DefaultCapacity = 256;

    private readonly List<IDomainEvent> _events = [];
    private readonly int _capacity;

    /// <summary>
    /// 创建集合。
    /// </summary>
    /// <param name="capacity">容量上限；超出时 <see cref="Add"/> 抛出 <see cref="InvalidOperationException"/>。</param>
    public DomainEventBuffer(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }

    /// <summary>
    /// 尚未派发的事件。
    /// </summary>
    public IReadOnlyList<IDomainEvent> Items => _events;

    /// <summary>
    /// 记录一个事件。
    /// </summary>
    public void Add(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (_events.Count >= _capacity)
        {
            throw new InvalidOperationException($"聚合累积了 {_capacity} 个未派发的领域事件，先提交再继续。");
        }

        _events.Add(domainEvent);
    }

    /// <summary>
    /// 清空。
    /// </summary>
    public void Clear() => _events.Clear();
}
