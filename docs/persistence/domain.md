# 领域模型

这一页讲怎样用 `XiHan.Framework.Domain` 定义实体与聚合：实体协议接口、值对象、领域事件缓冲与规约。读完它，你能写出一个多租户、带创建时间、可软删除、能记录领域事件的订单聚合，并用规约表达查询条件。前置知识是[核心概念](/getting-started/concepts)里的能力包与操作契约；写完实体之后怎样登记与读写，见下一页[持久化与 Provider](/persistence/overview)。

## 为什么需要

业务实体总要带一批公共字段：创建时间、修改时间、所属租户、并发戳、软删除标记。传统做法是让实体继承一个「什么都有」的基类，但 C# 只能单继承，不同组合要么拆成一串基类，要么每个实体都背着用不到的字段。更麻烦的是，一旦基类来自某个 ORM，实体就和那个 ORM 绑死了。

框架的做法是把每一项能力拆成一个接口：要创建时间就实现 `IHasCreationTime`，要软删除就实现 `ISoftDeletable`。持久化 Provider 按实体实现了哪些接口维护对应字段，实体自己不写这些逻辑。同一个实体类不继承任何基类，所以能在内存、EF Core、SqlSugar 三个 Provider 上原样运行，一致性套件对三者用的就是同一批实体。Domain 包只依赖 Primitives，领域代码不会因此引入数据库或框架运行时。

## 接口组合而不是基类

Domain 包里只有接口、一个有界的事件缓冲 `DomainEventBuffer` 和规约组合器 `Specification<T>`，没有实体基类，也没有审计基类。实体需要什么能力就实现哪个接口，持久化子系统按接口写字段、施加过滤。

| 包里有什么 | 命名空间 | 作用 |
| --- | --- | --- |
| 实体协议接口 | `XiHan.Framework.Domain.Entities` | 标识、聚合根、时间、租户、并发戳、软删除 |
| 领域事件 | `XiHan.Framework.Domain.Events` | `IDomainEvent`、`IHasDomainEvents`、`DomainEventBuffer` |
| 值对象 | `XiHan.Framework.Domain.ValueObjects` | 标记接口 `IValueObject` |
| 规约 | `XiHan.Framework.Domain.Specifications` | `ISpecification<T>`、`Specification<T>`、`Specification` |

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

这些字段由持久化子系统写入，所以接口都声明了 `set`。应用不必、也不应自己赋值：新增时 `TenantId` 会被覆盖成当前租户，`CreatedAt` 与 `ConcurrencyStamp` 会被重写；更新时创建时间、租户与删除标记一律沿用存储里的值。

`Id` 建议用 `init`：标识在创建时由 `IIdGenerator<TKey>` 分配，之后不变，见[标识生成](/persistence/identifiers)。没有公开 setter 的属性（如 `DomainEvents`）不映射成列。

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

## 领域事件缓冲

领域事件是聚合内已经发生的事实，实现 `IDomainEvent`（只有一个成员 `OccurredAt`，UTC 时间），建议用 `record`。聚合用 `DomainEventBuffer` 收集事件：

| 成员 | 说明 |
| --- | --- |
| `DomainEventBuffer()` / `DomainEventBuffer(int capacity)` | 无参构造用常量 `DomainEventBuffer.DefaultCapacity`（256）；容量小于 1 抛出 `ArgumentOutOfRangeException` |
| `Add(IDomainEvent)` | 按记录顺序追加；已达容量时抛出 `InvalidOperationException`，不静默丢弃 |
| `Items` | 尚未派发的事件，只读视图 |
| `Clear()` | 清空 |

容量上限防止聚合在一次事务里无界累积事件。超限是用法错误：通常是聚合一直在记事件却没有经过提交。

事件从记录到发出的路径：

1. 聚合方法调用 `DomainEventBuffer.Add` 记录事件。
2. 操作经仓储新增、更新或删除这个聚合；写入成功后，仓储把聚合登记到工作单元的 `DomainEvents`。
3. 处理器成功返回后、提交之前，Transaction 阶段把带有未派发事件的聚合交给 `IDomainEventDispatcher`。
4. 派发成功即清空各聚合的缓冲并提交；派发失败则回滚，调用以该失败结束。

没有经仓储写入的聚合不会被登记，它的事件也不会派发。聚合记了事件、容器里却没有 `IDomainEventDispatcher` 时，调用以 `PERSISTENCE_VALIDATION_003` 结束：派发器由 Messaging 子系统的 `AddDomainEvent<TEvent>(...)` 注册，只在进程内用缓冲的应用应在提交前自己清空。派发时机与发件箱见[工作单元与事务](/persistence/unit-of-work)，事件到消息的映射见[领域事件与发件箱](/messaging/domain-events)。

## 最小示例

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

实体写好以后，在 `Program.cs` 里登记它，并装配带内存持久化的标准组合：

```csharp
using SimpleApp.Orders;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

// 由当前装配的持久化 Provider 注册 IRepository<Order, Guid>
builder.Services.AddRepository<Order, Guid>();
builder.AddXiHan(x => x.UseStandardProfile());

var app = builder.Build();
app.UseXiHan();
app.Run();
```

运行后，任何注入 `IRepository<Order, Guid>` 的操作都能读写订单：新增时 `TenantId` 被写成当前租户、`CreatedAt` 被写成当前 UTC 时间；删除只把 `IsDeleted` 置为 `true` 并写入 `DeletedAt`；查询自动排除已删除与其他租户的订单。订单上记录的 `OrderPlaced` 在提交前派发（要装配一个派发器，见[领域事件与发件箱](/messaging/domain-events)）。

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

### 规约里能写什么

`Criteria` 最终由 Provider 执行：内存实现编译后在内存里判定，EF Core 与 SqlSugar 翻译成 SQL，所以表达式里只用 Provider 能翻译的成员与运算。两条经常用到的规则：

- 可空值类型与 `null` 的比较（`o.DeletedAt == null`）在三个 Provider 上都按是否有值判定。SqlSugar Provider 在翻译前会把它改写成能正确翻译的判空，并把逻辑非下推到叶子，判空与取反嵌套在 `&&` / `||` 里也照常翻译。
- 字符串的 `Contains`、`StartsWith`、`EndsWith` 按字面匹配，与 .NET 的语义一致：关键字里的 `%`、`_` 不是通配符。要通配时改用 Provider 自己的函数，见[仓储读写](/persistence/repositories)。

仓储查询已经自动施加软删除与租户过滤，规约里只写业务条件。`Persistence.Generators` 会为实现 `ISoftDeletable` 或 `IMultiTenant` 的实体生成 `<实体>Filters` 规约（`NotDeleted`、`ForTenant(tenantId)`、`ActiveFor(tenantId)`），用于内存判定或在 Provider 原生查询里复用同一条件，见 [Persistence.Generators 包](/packages/persistence-generators)。

## 常见问题

### 框架有没有实体基类或审计基类

没有，也不会提供。实体按需实现协议接口，持久化 Provider 按接口维护字段。如果项目里习惯写 `AuditedEntity<TKey>` 这类基类，可以在应用层自己定义一个实现了若干协议接口的抽象类，框架不关心实体是否继承了它，只看实现了哪些接口。

### 协议字段我自己赋值会怎样

不会生效，也不会报错。新增时 `TenantId`、`CreatedAt`、`ConcurrencyStamp` 被 Provider 重写；更新时创建时间、租户、删除标记沿用存储里当前行的值。这保证更新不能把实体搬到别的租户，也改不了创建时间。

### 实体可以用 `record` 吗

可以。生成器与 Provider 都接受 `class` 与 `record class`；值类型（`struct`、`record struct`）不行，仓储要求实体是类。注意 `record` 的值相等语义与实体的标识相等语义不同，用 `record` 时不要依赖 `Equals` 判断「同一实体」。

### `IHasConcurrencyStamp` 一定要实现吗

不是。没有并发戳的实体更新时不做乐观并发比对，后写覆盖先写。需要「读出后改、写回时发现别人已改过」这种保护的实体才实现它，并在 UI 上把 `ConcurrencyStamp` 一起带回来。

### 领域事件记录了却没有发出去

检查三点：聚合是否经仓储写入过（没有写入就不会登记到工作单元）；是否装配了 `IDomainEventDispatcher`（没有时调用会以 `PERSISTENCE_VALIDATION_003` 失败，不会静默吞掉）；写入是否在管道之外（管道外要自己在提交前派发，见[工作单元与事务](/persistence/unit-of-work)）。

### 值对象怎样落库

框架不自动展开值对象。EF Core 用 `OwnsOne` / 复杂类型在 `OnModelCreating` 里配置；SqlSugar 把值对象拆成实体上的多个标量属性，或序列化成一个 `[LongText]` 列。只在领域逻辑里用的值对象不需要任何配置。

## 相关包与下一步

- 相关包：[Domain](/packages/domain)、[Persistence.Abstractions](/packages/persistence-abstractions)、[Persistence.Generators](/packages/persistence-generators)
- 规范：RFC-0005《领域协议与持久化》
- 下一页：[持久化与 Provider](/persistence/overview)，讲仓储、工作单元与三个 Provider 的选择与启用
