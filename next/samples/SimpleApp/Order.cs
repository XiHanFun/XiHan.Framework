// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain;

namespace SimpleApp;

/// <summary>
/// 订单：多租户、带创建时间、软删除的聚合根。持久化子系统按协议自动维护这些字段。
/// </summary>
public sealed class Order : IAggregateRoot<Guid>, IMultiTenant, IHasCreationTime, ISoftDeletable
{
    private readonly DomainEventBuffer _events = new();

    public Guid Id { get; init; }

    public string TenantId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Amount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _events.Items;

    public void Place(DateTimeOffset now) => _events.Add(new OrderPlaced(Id, now));

    public void ClearDomainEvents() => _events.Clear();
}
