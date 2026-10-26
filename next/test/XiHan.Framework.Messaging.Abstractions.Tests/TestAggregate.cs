// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Events;

namespace XiHan.Framework.Messaging.Abstractions.Tests;

/// <summary>
/// 测试聚合：只持有领域事件缓冲。
/// </summary>
internal sealed class TestAggregate : IHasDomainEvents
{
    /// <summary>
    /// 事件缓冲。
    /// </summary>
    private readonly DomainEventBuffer _events = new();

    /// <inheritdoc />
    public IReadOnlyList<IDomainEvent> DomainEvents => _events.Items;

    /// <summary>
    /// 记录事件。
    /// </summary>
    public void Raise(IDomainEvent domainEvent) => _events.Add(domainEvent);

    /// <inheritdoc />
    public void ClearDomainEvents() => _events.Clear();
}
