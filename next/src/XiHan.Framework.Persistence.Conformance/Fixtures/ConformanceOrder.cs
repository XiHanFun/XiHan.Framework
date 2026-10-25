// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Domain.Events;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Conformance.Fixtures;

/// <summary>
/// 套件实体：实现全部持久化协议的聚合根。Provider 的测试需要把它映射到存储。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class ConformanceOrder : IAggregateRoot<Guid>, IHasCreationTime, IHasModificationTime, IHasConcurrencyStamp, ISoftDeletable
{
    /// <summary>
    /// 以组合方式持有的领域事件缓冲；不映射到存储。
    /// </summary>
    private readonly DomainEventBuffer _events = new();

    /// <inheritdoc />
    public Guid Id { get; init; }

    /// <summary>名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>金额（整数，避免 Provider 对 decimal 的排序差异）。</summary>
    public int Amount { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAt { get; set; }

    /// <inheritdoc />
    public string ConcurrencyStamp { get; set; } = string.Empty;

    /// <inheritdoc />
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedAt { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<IDomainEvent> DomainEvents => _events.Items;

    /// <summary>
    /// 记录事件。
    /// </summary>
    public void Raise(IDomainEvent domainEvent) => _events.Add(domainEvent);

    /// <inheritdoc />
    public void ClearDomainEvents() => _events.Clear();
}
