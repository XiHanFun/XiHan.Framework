// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Domain.Events;

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

    /// <summary>
    /// 尚未派发的事件，按记录顺序保存。
    /// </summary>
    private readonly List<IDomainEvent> _events = [];

    /// <summary>
    /// 容量上限。
    /// </summary>
    /// <remarks>
    /// 达到后 <see cref="Add"/> 拒绝继续记录。
    /// </remarks>
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
        // 容量上限防止聚合在一次事务内无界累积事件；超限视为用法错误而不是静默丢弃
        if (_events.Count >= _capacity)
        {
            throw new InvalidOperationException($"The aggregate has accumulated {_capacity} undispatched domain events; commit them before adding more.");
        }

        _events.Add(domainEvent);
    }

    /// <summary>
    /// 清空。
    /// </summary>
    public void Clear() => _events.Clear();
}
