// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Conformance;

/// <summary>
/// 套件实体：实现全部持久化协议的聚合根。Provider 的测试需要把它映射到存储。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class ConformanceOrder : IAggregateRoot<Guid>, IHasCreationTime, IHasModificationTime, IHasConcurrencyStamp, ISoftDeletable
{
    private readonly DomainEventBuffer _events = new();

    /// <inheritdoc />
    public Guid Id { get; init; }

    /// <summary>名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>金额。</summary>
    public decimal Amount { get; set; }

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

/// <summary>
/// 套件实体：多租户。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class ConformanceNote : IEntity<Guid>, IMultiTenant
{
    /// <inheritdoc />
    public Guid Id { get; init; }

    /// <inheritdoc />
    public string TenantId { get; set; } = string.Empty;

    /// <summary>内容。</summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// 套件实体：整数标识、不实现任何协议，删除即物理删除。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class ConformanceItem : IEntity<int>
{
    /// <inheritdoc />
    public int Id { get; init; }

    /// <summary>标签。</summary>
    public string Label { get; set; } = string.Empty;
}
