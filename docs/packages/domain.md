# XiHan.Framework.Domain

领域契约包：实体协议、值对象标记、领域事件与规约。只有接口与零依赖的组合器，没有实体基类，供定义聚合的应用与持久化子系统共同引用。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Domain` |
| 环层 | Ring 2（抽象） |
| 能力 | —（纯编译期契约，没有清单） |
| 特性入口 | — |
| 配置节 | — |
| 依赖 | `XiHan.Framework.Primitives` |
| AOT | 兼容 |
| 稳定性 | Stable |

## 概述

实体按需实现协议接口，持久化 Provider 按实体实现的接口维护对应字段：`IHasCreationTime` 的创建时间、`IHasModificationTime` 的修改时间、`IHasConcurrencyStamp` 的并发戳、`ISoftDeletable` 的删除标记、`IMultiTenant` 的租户。聚合根 `IAggregateRoot<TKey>` 用组合方式持有 `DomainEventBuffer` 收集领域事件，提交前由 Transaction 阶段派发。`Specification<T>` 把条件表示成表达式，同一条件既能在内存判定，也能交给查询提供器翻译。

本包不提供实体基类、审计基类或仓储；仓储与查询在 [Persistence.Abstractions](./persistence-abstractions)。

## 何时使用

定义实体、聚合、值对象与查询条件时引用它。只用持久化的应用不必单独引用：`Persistence.Abstractions` 与 `Profiles.Standard` 已传递引用本包。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Domain" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。本包没有特性，不需要 `UseFeature`。

## 主要类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `IEntity<TKey>` | `XiHan.Framework.Domain.Entities` | 有标识的实体，成员 `TKey Id { get; }`，`TKey : notnull` |
| `IAggregateRoot<TKey>` | `XiHan.Framework.Domain.Entities` | 聚合根：`IEntity<TKey>` + `IHasDomainEvents` |
| `IHasCreationTime` | `XiHan.Framework.Domain.Entities` | `DateTimeOffset CreatedAt`，新增时写入 |
| `IHasModificationTime` | `XiHan.Framework.Domain.Entities` | `DateTimeOffset? ModifiedAt`，更新时写入 |
| `IHasConcurrencyStamp` | `XiHan.Framework.Domain.Entities` | `string ConcurrencyStamp`，新增与更新时刷新，更新时比对 |
| `ISoftDeletable` | `XiHan.Framework.Domain.Entities` | `bool IsDeleted`、`DateTimeOffset? DeletedAt`，删除改为打标 |
| `IMultiTenant` | `XiHan.Framework.Domain.Entities` | `string TenantId`，新增时写入当前租户，查询按当前租户过滤 |
| `IDomainEvent` | `XiHan.Framework.Domain.Events` | 领域事件，成员 `DateTimeOffset OccurredAt`（UTC） |
| `IHasDomainEvents` | `XiHan.Framework.Domain.Events` | `DomainEvents`（尚未派发的事件）与 `ClearDomainEvents()` |
| `DomainEventBuffer` | `XiHan.Framework.Domain.Events` | 有界事件缓冲：`Add`、`Items`、`Clear`，缺省容量 `DefaultCapacity`（256） |
| `IValueObject` | `XiHan.Framework.Domain.ValueObjects` | 值对象标记，配合 `record` 取得值相等 |
| `ISpecification<T>` | `XiHan.Framework.Domain.Specifications` | `Criteria` 表达式与 `IsSatisfiedBy` |
| `Specification<T>` | `XiHan.Framework.Domain.Specifications` | 规约实现：`And`、`Or`、`Not`，可继承 |
| `Specification` | `XiHan.Framework.Domain.Specifications` | 工厂：`All<T>()`（恒真，按类型缓存）、`Of<T>(criteria)` |

## 使用示例

带软删除、创建时间与领域事件的聚合：

```csharp
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Domain.Events;

namespace SimpleApp.Payments;

public sealed class Payment : IAggregateRoot<Guid>, IHasCreationTime, ISoftDeletable
{
    // 一笔支付的事件很少，容量给小一些也够
    private readonly DomainEventBuffer _events = new(capacity: 16);

    public Guid Id { get; init; }

    public decimal Amount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _events.Items;

    public void Refund(DateTimeOffset now) => _events.Add(new PaymentRefunded(Id, Amount, now));

    public void ClearDomainEvents() => _events.Clear();
}

public sealed record PaymentRefunded(Guid PaymentId, decimal Amount, DateTimeOffset OccurredAt) : IDomainEvent;
```

规约的组合与内存判定：

```csharp
using SimpleApp.Payments;
using XiHan.Framework.Domain.Specifications;

var large = Specification.Of<Payment>(p => p.Amount >= 1000m);
var recent = Specification.Of<Payment>(p => p.CreatedAt >= DateTimeOffset.UtcNow.AddDays(-7));
var largeOrOld = large.Or(recent.Not());

var payment = new Payment { Amount = 50m, CreatedAt = DateTimeOffset.UtcNow.AddDays(-30) };
var matched = largeOrOld.IsSatisfiedBy(payment);   // true：不是近 7 天的
```

## 行为与限制

- `DomainEventBuffer` 的容量小于 1 时构造抛出 `ArgumentOutOfRangeException`；已达容量时 `Add` 抛出 `InvalidOperationException`，不丢弃事件。缓冲不是线程安全的，聚合在一个调用里使用。
- `Specification<T>` 第一次 `IsSatisfiedBy` 时编译 `Criteria` 并缓存委托；只交给查询提供器的规约不编译。`And` / `Or` 把右侧表达式的参数替换为左侧参数，组合结果只有一个参数，能被 EF Core 等 LINQ 提供器翻译。
- 规约里只用目标 Provider 能翻译的成员与运算；可空值类型与 `null` 的比较在三个持久化 Provider 上都按是否有值判定。
- 接口只描述字段，不含行为：字段何时写入、怎样过滤由持久化 Provider 按 RFC-0005 执行，见[持久化](../guide/persistence)。
- `IValueObject` 不参与任何持久化映射。

## 相关

- [领域模型](../guide/domain)
- [持久化](../guide/persistence)
- [工作单元与事务](../guide/uow)
- [Persistence.Abstractions](./persistence-abstractions)
- [Persistence.Generators](./persistence-generators)
- 规范：RFC-0005《领域协议与持久化》
