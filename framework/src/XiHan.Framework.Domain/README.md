# XiHan.Framework.Domain

领域契约（Ring 2）。只有接口协议与零依赖的规约组合器，没有实体基类；实体按需组合协议。

| 契约 | 用途 |
| --- | --- |
| `IEntity<TKey>`、`IAggregateRoot<TKey>` | 标识与聚合根 |
| `IHasDomainEvents`、`IDomainEvent`、`DomainEventBuffer` | 领域事件的收集与清空 |
| `ISoftDeletable`、`IHasCreationTime`、`IHasModificationTime`、`IMultiTenant`、`IHasConcurrencyStamp` | 持久化子系统按协议自动维护的字段 |
| `IValueObject` | 值对象标记（用 `record` 获得值相等） |
| `ISpecification<T>`、`Specification<T>`、`Specification.All<T>()` / `Specification.Of<T>()` | 查询规约与 And / Or / Not 组合 |

```csharp
public sealed class Order : IAggregateRoot<Guid>, IMultiTenant, ISoftDeletable, IHasCreationTime
{
    private readonly DomainEventBuffer _events = new();
    public Guid Id { get; init; }
    public string TenantId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public IReadOnlyList<IDomainEvent> DomainEvents => _events.Items;
    public void ClearDomainEvents() => _events.Clear();
}
```

## 依赖方向

依赖 `XiHan.Framework.Primitives`。被持久化等子系统的抽象包引用。没有能力清单：纯编译期契约。

## 状态

P2：可用。
