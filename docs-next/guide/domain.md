# 领域模型

本页讲 `XiHan.Framework.Domain` 里的实体协议、值对象、领域事件与规约，以及持久化子系统怎样按这些协议工作，适用于定义聚合与查询条件的应用开发者。完整 API 见 [Domain 包](../packages/domain)。

## 接口组合而不是基类

Domain 包只有接口、一个有界的事件缓冲 `DomainEventBuffer` 和规约组合器 `Specification<T>`，没有实体基类。实体需要什么能力就实现哪个接口：要创建时间就实现 `IHasCreationTime`，要软删除就实现 `ISoftDeletable`。持久化 Provider 按实体实现的接口维护对应字段（`PersistenceConventions`），实体自己不写这些逻辑。

这样设计有三个原因。C# 只能单继承，审计、租户、软删除、并发戳的各种组合要么拆成一串基类，要么塞进一个什么都有的基类；接口可以任意组合，实体只带它真正用到的字段。同一个实体类不继承任何 ORM 的基类，所以能同时跑在 InMemory、EF Core 与 SqlSugar 三个 Provider 上，一致性套件对三者用的是同一批实体（ADR-0013）。Domain 处在 Ring 2，只依赖 Primitives，领域代码不会因此引入数据库或框架运行时。

5.0 不提供实体基类与审计基类，实体按需实现协议接口；与 4.x 的对照见[从 4.x 迁移](./upgrade)。

## 实体协议

| 接口 | 成员 | 持久化子系统的行为 |
| --- | --- | --- |
| `IEntity<TKey>` | `TKey Id { get; }` | 标识。`TKey` 不能为 null；仓储另要求 `TKey : IEquatable<TKey>`（`Guid`、`long`、`int`、`string` 都满足） |
| `IAggregateRoot<TKey>` | 继承 `IEntity<TKey>` 与 `IHasDomainEvents` | 一致性边界与领域事件的来源；仓储写入成功后把它登记到工作单元，提交前派发它的事件 |
| `IHasCreationTime` | `DateTimeOffset CreatedAt` | 新增时写入当前 UTC 时间（取自容器里的 `TimeProvider`） |
| `IHasModificationTime` | `DateTimeOffset? ModifiedAt` | 更新时写入当前 UTC 时间 |
| `IHasConcurrencyStamp` | `string ConcurrencyStamp` | 新增与更新时刷新为新的 32 位十六进制串；更新游离实例时比对，不符返回 `PERSISTENCE_CONFLICT_001` |
| `ISoftDeletable` | `bool IsDeleted`、`DateTimeOffset? DeletedAt` | 删除改为打标并写入删除时间；查询默认不返回已删除的实体 |
| `IMultiTenant` | `string TenantId` | 新增时写入当前租户；只对同一租户的调用可见；没有当前租户时新增返回 `PERSISTENCE_VALIDATION_001` |
| `IHasDomainEvents` | `DomainEvents`、`ClearDomainEvents()` | 尚未派发的事件与清空方法；通常经 `IAggregateRoot<TKey>` 获得 |

这些字段由持久化子系统写入，接口因此声明了 `set`。应用不必、也不应自己赋值：新增时 `TenantId` 会被覆盖成当前租户，`CreatedAt` 与 `ConcurrencyStamp` 会被重写。

下面是 SimpleApp 的订单聚合：多租户、带创建时间、软删除，并在下单时记录一个领域事件。

```csharp
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Domain.Events;

namespace SimpleApp.Orders;

public sealed class Order : IAggregateRoot<Guid>, IMultiTenant, IHasCreationTime, ISoftDeletable
{
    // 事件缓冲以组合方式持有，容量缺省 256
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

public sealed record OrderPlaced(Guid OrderId, DateTimeOffset OccurredAt) : IDomainEvent;
```

`Id` 用 `init`：标识在创建时由 `IIdGenerator<TKey>` 分配，之后不变，见[标识生成](./distributed-ids)。没有公开 setter 的属性（如 `DomainEvents`）不映射成列。

## 值对象

`IValueObject` 是一个空的标记接口。值对象用 `record` 声明，相等性按成员值比较，修改时用 `with` 派生新实例：

```csharp
using XiHan.Framework.Domain.ValueObjects;

namespace SimpleApp.Orders;

public sealed record Money(decimal Amount, string Currency) : IValueObject
{
    // 不同币种相加是编程错误，不做隐式换算
    public Money Add(Money other) => other.Currency == Currency
        ? this with { Amount = Amount + other.Amount }
        : throw new ArgumentException("The currencies must match.", nameof(other));
}
```

持久化约定不识别 `IValueObject`，也不自动展开值对象。值对象作为实体属性落库时，按所用 Provider 的方式配置映射（EF Core 在应用自己的 `DbContext.OnModelCreating` 里配置）；只在领域逻辑里使用、不落库的值对象不受影响。

## 领域事件

领域事件是聚合内已经发生的事实，实现 `IDomainEvent`（只有一个成员 `OccurredAt`，UTC 时间），建议用 `record`。聚合用 `DomainEventBuffer` 收集事件：

| 成员 | 说明 |
| --- | --- |
| `DomainEventBuffer(int capacity = 256)` | 容量上限，小于 1 抛出 `ArgumentOutOfRangeException`；缺省值是常量 `DomainEventBuffer.DefaultCapacity` |
| `Add(IDomainEvent)` | 按记录顺序追加；已达容量时抛出 `InvalidOperationException`，不静默丢弃 |
| `Items` | 尚未派发的事件，只读视图 |
| `Clear()` | 清空 |

容量上限防止聚合在一次事务里无界累积事件。超限是用法错误：通常是聚合一直在记事件却没有经过提交。

事件从记录到发出的路径：

1. 聚合方法调用 `DomainEventBuffer.Add` 记录事件（上例的 `Place`）。
2. 操作经仓储新增、更新或删除这个聚合；写入成功后，仓储把聚合登记到工作单元的 `DomainEvents`。
3. 处理器成功返回后、提交之前，Transaction 阶段把带有未派发事件的聚合交给 `IDomainEventDispatcher`。
4. 派发成功即清空各聚合的缓冲并提交；派发失败则回滚，调用以该失败结束。

没有经仓储写入的聚合不会被登记，它的事件也不会派发。聚合记了事件、容器里却没有 `IDomainEventDispatcher` 时，调用以 `PERSISTENCE_VALIDATION_003` 结束：派发器由 Messaging 子系统的 `AddDomainEvent<TEvent>(...)` 注册，只在进程内用缓冲的应用应在提交前自己清空。派发时机、发件箱与在管道外的写法见[工作单元与事务](./uow#领域事件与发件箱)，事件到消息的映射见[消息与事件](./messaging)。

## 规约

`ISpecification<T>` 把一个条件表示成表达式 `Criteria`，同一个条件既能用 `IsSatisfiedBy` 在内存里判定，也能交给查询提供器翻译成 SQL。`Specification<T>` 是它的实现，提供组合方法：

| 成员 | 说明 |
| --- | --- |
| `new Specification<T>(criteria)`、`Specification.Of<T>(criteria)` | 用表达式创建 |
| `Specification.All<T>()` | 恒真规约，按类型缓存同一实例 |
| `And(other)`、`Or(other)`、`Not()` | 组合成新规约；合并时把右侧表达式的参数替换成左侧参数，结果只有一个参数，EF Core 等 LINQ 提供器才能翻译 |
| `IsSatisfiedBy(candidate)` | 第一次调用时编译表达式并缓存委托；只交给提供器翻译的规约不付编译代价 |

```csharp
using XiHan.Framework.Domain.Specifications;

namespace SimpleApp.Orders;

public static class OrderSpecifications
{
    public static Specification<Order> AmountAtLeast(int amount) => Specification.Of<Order>(o => o.Amount >= amount);

    public static Specification<Order> Named(string name) => new(o => o.Name == name);
}

// 规约类也可以继承 Specification<T>，给常用条件起名字
public sealed class LargeOrders() : Specification<Order>(static o => o.Amount >= 1000);
```

组合后的规约既能判定单个对象，也能传给仓储查询：

```csharp
using SimpleApp.Orders;
using XiHan.Framework.Persistence.Abstractions.Queries;

var order = new Order { Name = "VIP", Amount = 120 };
var vip = OrderSpecifications.AmountAtLeast(100).And(OrderSpecifications.Named("VIP"));
var matched = vip.IsSatisfiedBy(order);   // true
var others = EntityQuery.All<Order>().Where(vip.Not());
```

`Criteria` 最终由 Provider 执行：InMemory 编译后在内存里判定，EF Core 与 SqlSugar 翻译成 SQL，所以表达式里只用 Provider 能翻译的成员与运算。可空值类型与 `null` 的比较（`o.DeletedAt == null`）在三个 Provider 上都按是否有值判定，SqlSugar Provider 在翻译前会把它改写成 `HasValue`（ADR-0035）。

仓储查询已经自动施加软删除与租户过滤，规约里只写业务条件。`Persistence.Generators` 会为实现 `ISoftDeletable` 或 `IMultiTenant` 的实体生成 `<实体>Filters` 规约（`NotDeleted`、`ForTenant(tenantId)`、`ActiveFor(tenantId)`），用于内存判定或在 Provider 原生查询里复用同一条件，见 [Persistence.Generators 包](../packages/persistence-generators)。

## 与其他能力的关系

- 实体如何读写、查询如何分页：[持久化](./persistence)。
- 领域事件在事务里的派发时机：[工作单元与事务](./uow)。
- `IMultiTenant` 的当前租户从哪里来：[多租户](./multi-tenancy)。
- 实体标识的生成：[标识生成](./distributed-ids)。

## 相关

- [Domain 包](../packages/domain)
- [Persistence.Abstractions 包](../packages/persistence-abstractions)
- [Persistence.Generators 包](../packages/persistence-generators)
- 规范：RFC-0005《领域协议与持久化》
- 决策：ADR-0013、ADR-0025、ADR-0035
