// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain;

namespace SimpleApp;

/// <summary>
/// 订单：多租户、带创建时间、软删除的聚合根。持久化子系统按协议自动维护这些字段。
/// </summary>
public sealed class Order : IAggregateRoot<Guid>, IMultiTenant, IHasCreationTime, ISoftDeletable
{
    /// <summary>
    /// 待发布的领域事件缓冲；工作单元提交后由持久化子系统取走并清空。
    /// </summary>
    private readonly DomainEventBuffer _events = new();

    /// <summary>
    /// 订单 ID；由 <c>IIdGenerator{Guid}</c> 在创建时分配。
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 所属租户；写入时由持久化子系统按当前调用上下文填充，查询时自动过滤。
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// 订单名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 订单金额。
    /// </summary>
    public int Amount { get; set; }

    /// <summary>
    /// 创建时间；写入时由持久化子系统填充。
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// 是否已软删除；删除操作只置位，不物理移除。
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// 软删除时间；未删除时为 <c>null</c>。
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// 尚未发布的领域事件。
    /// </summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _events.Items;

    /// <summary>
    /// 下单：记录一条 <see cref="OrderPlaced"/> 事件，事件随订单写入一起在工作单元内发布。
    /// </summary>
    public void Place(DateTimeOffset now) => _events.Add(new OrderPlaced(Id, now));

    /// <summary>
    /// 清空已发布的领域事件；由持久化子系统在事件取走后调用。
    /// </summary>
    public void ClearDomainEvents() => _events.Clear();
}
