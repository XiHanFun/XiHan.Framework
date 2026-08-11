# XiHan.Framework.Domain

> DDD 领域层：实体/聚合根基类、审计与多租户变体、持久化无关的仓储抽象、领域服务、领域事件、规约、业务规则、值对象与领域异常。

- **NuGet**：`XiHan.Framework.Domain`
- **模块类**：`XiHanDomainModule`（`[DependsOn(typeof(XiHanDomainSharedModule))]`，`ConfigureServices` 为空）
- **所在层**：领域与应用层
- **关键依赖**：仅 .NET 原生 + 框架内部依赖（唯一 `ProjectReference` 是 `XiHan.Framework.Domain.Shared`）

## 概述

这个包提供领域驱动设计（DDD）的一整套战术构件：实体、聚合根、值对象、领域服务、领域事件、规约、业务规则与领域异常。它 **只定义抽象与基类**，不涉及任何具体数据库实现——仓储接口的落地由基础设施层（`XiHan.Framework.Data`，基于 SqlSugar）完成。

你在建模业务领域时继承这里的基类，就能获得主键、并发版本、创建/修改/删除审计、多租户、领域事件收集等通用能力；用 `` `IRepositoryBase<TEntity, TKey>` `` 之类的抽象接口把业务与持久化解耦；把复杂查询封装为规约（`` `ISpecification<T>` ``），把业务约束显式化为业务规则（`IBusinessRule`）。

`XiHanDomainModule.ConfigureServices` 是空实现——本包不注册服务、不接中间件。领域服务的自动注册来自 `IDomainService : ITransientDependency` 这个标记接口（由 Core 的约定注册扫描接管），仓储的具体实现与注册在 `Data` 包里完成。

## 何时使用

- 用 DDD 方式建模业务：需要聚合根、实体、值对象的标准基类
- 需要 **持久化无关** 的仓储抽象（`` `IRepositoryBase<TEntity, TKey>` `` 及其只读/软删除/审计/聚合根变体）来解耦业务与数据库
- 想把复杂查询或业务约束封装为规约（`Specification`）或业务规则（`IBusinessRule`）
- 需要在聚合根内收集本地/分布式领域事件，交由发件箱/事件总线后续派发

## 安装与启用

```bash
dotnet add package XiHan.Framework.Domain
```

```csharp
[DependsOn(typeof(XiHanDomainModule))]
public class MyModule : XiHanModule { }
```

启用后你得到的是一批 **可继承的基类与可实现的契约**（外加领域服务标记接口带来的自动注册约定）。真正把仓储接口绑定到实现，需要引用并启用 `XiHan.Framework.Data`。

## 工作原理

### 实体与审计接口的继承体系

审计能力被切成正交的小接口，按需组合：

```text
IEntityBase                      —— RowVersion（并发版本，long）
IEntityBase<TKey>                —— BasicId（主键）+ IsTransient()
ICreationEntity / <TKey>         —— CreatedTime (+ CreatedId / CreatedBy)
IModificationEntity / <TKey>     —— ModifiedTime (+ ModifiedId / ModifiedBy)
ISoftDelete → IDeletionEntity / <TKey> —— IsDeleted / DeletedTime (+ DeletedId / DeletedBy)
IFullAuditedEntity / <TKey>      —— 创建 + 修改 + 删除 全审计（组合以上三者）
IMultiTenantEntity               —— TenantId（long，非空，0=平台租户，业务租户从 1 起）
ITraceableEntity                 —— TraceId（W3C/HttpContext 链路追踪串联）
ISplitTableEntity : ICreationEntity —— 分表标记（框架按 CreatedTime 时间范围分表）
```

对应的具体基类（可直接继承）：`EntityBase` / `` `EntityBase<TKey>` ``、`CreationEntityBase`、`ModificationEntityBase`、`DeletionEntityBase`、`FullAuditedEntityBase` / `` `FullAuditedEntityBase<TKey>` `` 及它们的 `MultiTenant*` 变体。审计字段的自动填充由基础设施/拦截层负责，本包只定义结构。

配套的 `ITraceIdProvider`（`` string? GetCurrentTraceId() ``）为不依赖 ASP.NET Core 的层（领域服务、后台任务等）提供获取当前链路追踪 ID 的抽象，与 `ITraceableEntity` 搭配使用：中间件/拦截器可借此把 `TraceId` 自动写入实现了 `ITraceableEntity` 的实体。

### 主键相等性与临时实体

`` `EntityBase<TKey>` `` 用 `BasicId` 做相等性判定并重载了 `== / !=`：类型不同、或任一方 `BasicId` 为默认值（临时实体）都判为不相等；`IsTransient()` 判断实体是否尚未持久化；`GetHashCode()` 对临时实体退回 `base.GetHashCode()`，对持久化实体用 `HashCode.Combine(GetType(), BasicId)`。

### 聚合根与领域事件

`AggregateRootBase` / `` `AggregateRootBase<TKey>` `` 在全审计实体基础上内嵌一个 `DomainEventsManagerBase`，区分 **本地事件** 与 **分布式事件** 两条队列（线程安全 `ConcurrentQueue`）。聚合内用受保护的 `AddLocalEvent` / `AddDistributedEvent` 收集事件，外部通过 `GetLocalEvents` / `GetDistributedEvents` / `ClearLocalEvents` / `ClearDistributedEvents` 读取与清空。事件按全局递增的 `EventOrder`（`EventOrderGenerator`）排序，`DomainEventRecord` 封装 `EventData + EventOrder`。

## 核心能力

- **实体基类体系**：`EntityBase`（`RowVersion`）/ `` `EntityBase<TKey>` ``（`BasicId`、基于主键的相等性、`IsTransient()`），派生出创建/修改/删除/全审计及其多租户变体
- **聚合根**：`IAggregateRoot`（= 全审计 + 领域事件管理器）/ `AggregateRootBase` / `` `AggregateRootBase<TKey>` `` / `` `MultiTenantAggregateRootBase<TKey>` ``
- **仓储抽象**：`IReadOnlyRepositoryBase` 提供查询/分页/规约；`IRepositoryBase` 增补增删改；另有 `ISoftDeleteRepositoryBase`（软删除/恢复/查已删）、`IAuditedRepository`（按创建/修改/删除人与时间段查）、`IAggregateRootRepository`（聚合根一致性 + 事件）
- **领域服务**：`DomainService` 基类，内置业务规则检查、结构化日志与性能监控辅助方法
- **领域事件**：`IDomainEvent` / `DomainEventBase`（`EventId` + `OccurredOn`）、`IDomainEventsManager` / `DomainEventsManagerBase`、`AggregateRootExtensions`（聚合事件统计/快照/批处理）
- **审计事件**：`AuditEvent` 抽象基类（`Entity`/`EntityType`/`EntityId`/`Timestamp`）及其派生的 `EntityCreatedEvent` / `EntityModifiedEvent`（另含 `OriginalEntity`）/ `EntityDeletedEvent`，用于描述实体生命周期中的关键操作；它们不实现 `IDomainEvent`，是独立于领域事件总线的审计专用事件类型
- **规约模式**：`` `ISpecification<T>` `` / `` `Specification<T>` ``（`And`/`Or`/`Not` + 运算符 `& | !` + 隐式转表达式）/ `` `AsyncSpecification<T>` ``（异步判定 + 短路组合）
- **业务规则**：`IBusinessRule`（`Message` + `IsBroken()`）、`BusinessRuleExtensions`（`CheckRule`/`CheckRules`/`Validate`）、`EntityExtensions`（实体上直接 `CheckRule`）
- **值对象**：`ValueObject`（按 `GetEqualityComponents()` 判等）与 `` `SingleValueObject<T>` ``（单值 + 隐式转基础类型）
- **领域异常**：`DomainException`（`Code` / `Details`）与 `BusinessRuleValidationException`（携带被违反的 `BrokenRule`）
- **审计特性**：`AuditableAttribute` / `NoAuditAttribute` / `IgnoreAuditAttribute` 控制实体是否参与各类审计与事件

## 主要 API / 类型

### 实体与聚合根

| 类型 | 说明 |
| --- | --- |
| `EntityBase` | 非泛型实体基类，含并发版本 `RowVersion`（`long`，构造置 0） |
| `` `EntityBase<TKey>` `` | 泛型主键实体：`BasicId`（`protected set`）、`== / != / Equals / GetHashCode`（基于主键）、`IsTransient()`；约束 `` where TKey : IEquatable<TKey> `` |
| `FullAuditedEntityBase` / `<TKey>` | 全审计基类：`CreatedTime` / `ModifiedTime?` / `IsDeleted` / `DeletedTime?`；泛型版另含 `CreatedId/By`、`ModifiedId/By`、`DeletedId/By` |
| `AggregateRootBase` / `<TKey>` | 聚合根基类，= 全审计实体 + 内嵌领域事件管理器；受保护 `AddLocalEvent/AddDistributedEvent`、公开 `GetLocalEvents/GetDistributedEvents/Clear*` |
| `` `MultiTenantAggregateRootBase<TKey>` `` | 多租户聚合根，实现 `IMultiTenantEntity`（`TenantId`，`long`，0=平台租户） |

### 仓储抽象（Repositories）

| 接口 | 关键方法签名（节选） |
| --- | --- |
| `` `IReadOnlyRepositoryBase<TEntity,TKey>` `` | `` Task<TEntity?> GetByIdAsync(TKey id, …) `` / `` Task<IReadOnlyList<TEntity>> GetByIdsAsync(IEnumerable<TKey>, …) `` / `` Task<TEntity?> GetFirstAsync(Expression<Func<TEntity,bool>>, …) `` 及规约重载 / `GetAllAsync` / `GetListAsync`（表达式 / 表达式+排序 / 规约 / `PageRequestDtoBase` 条件）/ `CountAsync` / `AnyAsync` / 一组 `GetPagedAsync(...)`（页码页大小 / +谓词 / +排序 / +规约 / `PageRequestDtoBase`）/ `GetPagedAutoAsync(object queryDto, …)`（自动构建条件） |
| `` `IRepositoryBase<TEntity,TKey>` `` | 继承只读接口，增补：`` Task<TEntity> AddAsync(TEntity, …) `` / `` Task<TKey> AddReturnIdAsync(…) `` / `AddRangeAsync` / `` Task<TEntity> UpdateAsync(TEntity, …) `` / `` Task<bool> UpdateAsync(Expression<Func<TEntity,TEntity>> columns, Expression<Func<TEntity,bool>> where, …) `` / `UpdateRangeAsync` / `AddOrUpdateAsync(Range)` / `DeleteAsync(entity)` / `DeleteByIdAsync(id)` / `DeleteRangeAsync(entities/ids)` / `DeleteAsync(predicate)` |
| `` `ISoftDeleteRepositoryBase<TEntity,TKey>` `` | 约束实体实现 `ISoftDelete`；`SoftDeleteAsync`/`SoftDeleteRangeAsync`（实体/主键/谓词/规约）/ `RestoreAsync`/`RestoreRangeAsync` / `GetAllWithDeletedAsync` / `GetDeletedAsync`（无参/谓词/规约） |
| `` `IAuditedRepository<TEntity,TKey>` `` | 约束实体 `` IFullAuditedEntity<TKey> ``；`GetByCreatorAsync/GetByModifierAsync/GetByDeleterAsync` / `GetCreatedBetweenAsync/GetModifiedBetweenAsync/GetDeletedBetweenAsync` / `` GetByAuditAsync(AuditQueryOptions<TKey>, …) `` |
| `` `IAggregateRootRepository<TAggregateRoot,TKey>` `` | 约束 `` IAggregateRoot<TKey>, new() ``；`new AddAsync/UpdateAsync/DeleteAsync`（触发聚合根领域事件）/ `SaveAggregateAsync` / `GetAggregateAsync` / `GetWithEventsAsync` |
| `` `AuditQueryOptions<TKey>` `` | 审计查询参数：`CreatedId/ModifiedId/DeletedId`、`Created/Modified/DeletedTimeStart/End`、`IncludeSoftDeleted`、`OnlySoftDeleted`（`init` 只读） |

> 仓储方法约定：查询集合统一返回 `` `IReadOnlyList<TEntity>` ``；不存在的单实体返回 `null`；`Count` 返回 `long`；所有异步方法带 `CancellationToken`。分页统一产出 `` `PageResultDtoBase<TEntity>` ``（来自 Domain.Shared）。

### 领域服务 / 规约 / 事件 / 规则

| 类型 | 说明 |
| --- | --- |
| `IDomainService` | 领域服务标记接口，继承 `ITransientDependency`（自动瞬态注册） |
| `DomainService` | 领域服务基类。`CheckBusinessRule(s)` / `CheckBusinessRule(s)Async`、`LogDomainOperation(Completed)`、`ExecuteWithPerformanceMonitoring[Async]<T>`；日志经 `ITransientCachedServiceProvider` 惰性解析 |
| `` `ISpecification<T>` `` | 规约契约：`ToExpression()` / `IsSatisfiedBy(entity)` / `And` / `Or` / `Not` |
| `` `Specification<T>` `` | 规约抽象基类：实现 `And/Or/Not`（内部 `AndSpecification/OrSpecification/NotSpecification`，用 `ParameterReplacer` 合并参数），重载 `& \| !`，隐式转 `` Expression<Func<T,bool>> `` |
| `` `IAsyncSpecification<T>` `` / `` `AsyncSpecification<T>` `` | 异步规约：`IsSatisfiedByAsync` / `AndAsync/OrAsync/NotAsync`（短路求值） |
| `IDomainEvent` / `DomainEventBase` | 领域事件契约与基类：`Guid EventId` + `DateTimeOffset OccurredOn`（基类构造自动填充） |
| `IDomainEventsManager` / `DomainEventsManagerBase` | 事件管理器：本地/分布式双队列，`Add*/Get*/Clear*/HasPendingEvents/MarkEventsAsCommitted` |
| `DomainEventRecord` | 事件记录包装：`EventData`（`IDomainEvent`）+ `EventOrder`（`long`） |
| `AuditEvent` / `EntityCreatedEvent` / `EntityModifiedEvent` / `EntityDeletedEvent` | 审计事件基类与三个派生类：`Entity`（`object`）/ `EntityType` / `EntityId` / `Timestamp`（UTC）；`EntityModifiedEvent` 额外携带可选的 `OriginalEntity`。用于审计管道记录创建/修改/删除，独立于 `IDomainEvent` 体系 |
| `IBusinessRule` | 业务规则契约：`string Message` + `bool IsBroken()` |
| `BusinessRuleExtensions` | `CheckRule`/`CheckRules`（违反抛 `BusinessRuleValidationException`）/ `CheckRuleAsync`/`CheckRulesAsync` / `Validate`/`ValidateAll`（返回 `BusinessRuleValidationResult`） |
| `EntityExtensions` | 实体上直接校验：`CheckRule`/`CheckRules`/`TryCheckRule`/`ValidateRules` |
| `AggregateRootExtensions` | 聚合事件辅助：`GetAllEvents`/`ClearAllEvents`/`` `GetEventsOfType<TEvent>` ``/`` `HasEventOfType<TEvent>` ``/`GetEventStatistics`/`ProcessAllEventsAsync`/`CreateSnapshot` |

### 值对象与异常

| 类型 | 说明 |
| --- | --- |
| `ValueObject` | 值对象基类，按 `GetEqualityComponents()`（子类实现）判等；重载 `== / != / Equals / GetHashCode / ToString`；延迟求值 + 早退优化 |
| `` `SingleValueObject<T>` `` | 单值值对象（`` where T : notnull ``）：`Value` 只读、隐式转 `T`、`ToString()` 转发到值 |
| `DomainException` | 领域异常基类：`Code?` / `Details?`；静态 `Create(message, code?, details?)` |
| `BusinessRuleValidationException` | 业务规则验证异常，继承 `DomainException`；携带被违反的 `IBusinessRule? BrokenRule` |

## 使用示例

### 定义聚合根并收集领域事件

```csharp
public class Order : AggregateRootBase<long>
{
    public string Status { get; private set; } = "Created";

    public void Pay()
    {
        // 业务约束显式化
        this.CheckRule(new OrderMustBeUnpaidRule(Status));
        Status = "Paid";
        AddLocalEvent(new OrderPaidEvent(BasicId)); // 收集，交由后续发件箱/事件总线派发
    }
}

public sealed class OrderPaidEvent(long orderId) : DomainEventBase
{
    public long OrderId { get; } = orderId;
}
```

### 业务规则

```csharp
public sealed class OrderMustBeUnpaidRule(string status) : IBusinessRule
{
    public string Message => "订单已支付，不能重复支付";
    public bool IsBroken() => status == "Paid";
}
```

### 规约组合并交给仓储

```csharp
public sealed class ActiveUserSpec : Specification<User>
{
    public override Expression<Func<User, bool>> ToExpression() => u => !u.IsDeleted;
}

// 组合：活跃 且 属于租户 1
var spec = new ActiveUserSpec().And(new TenantSpec(1));
var users = await userRepository.GetListAsync(spec, cancellationToken);
var page  = await userRepository.GetPagedAsync(1, 20, spec, cancellationToken);
```

### 领域服务中的性能监控与规则校验

```csharp
public class TransferService : DomainService
{
    public Task TransferAsync(Account from, Account to, decimal amount, CancellationToken ct)
        => ExecuteWithPerformanceMonitoringAsync("Transfer", async token =>
        {
            await CheckBusinessRuleAsync(new SufficientBalanceRule(from, amount), cancellationToken: token);
            from.Withdraw(amount);
            to.Deposit(amount);
        }, new { amount }, ct);
}
```

## 扩展点 / 自定义

- **选择合适的实体基类**：只需并发控制用 `` `EntityBase<TKey>` ``；需要全审计用 `` `FullAuditedEntityBase<TKey>` ``；需要租户隔离用 `MultiTenant*` 变体；聚合根用 `` `AggregateRootBase<TKey>` `` / `` `MultiTenantAggregateRootBase<TKey>` ``。
- **实现自定义仓储**：面向业务定义继承 `IRepositoryBase`/`ISoftDeleteRepositoryBase`/`IAggregateRootRepository` 的专用接口，具体实现与 DI 绑定由 `XiHan.Framework.Data` 提供（本包不含实现）。
- **自定义规约**：继承 `` `Specification<T>` `` 实现 `ToExpression()` 即可获得 `And/Or/Not` 组合与运算符重载；需要异步判定时继承 `` `AsyncSpecification<T>` `` 并重写 `IsSatisfiedByAsync`。
- **领域服务自动注册**：让服务实现 `IDomainService`（间接是 `ITransientDependency`）即被约定扫描注册为瞬态；`DomainService` 基类提供 `ITransientCachedServiceProvider` 惰性解析日志等依赖。
- **审计开关**：用 `AuditableAttribute`（`EnableCreation/Modification/DeletionAudit`、`EmitEvents`）/ `NoAuditAttribute` / `IgnoreAuditAttribute` 精细控制实体的审计行为与属性级忽略。

## 注意事项与最佳实践

- **临时实体不相等**：`BasicId` 为默认值时 `Equals` 恒返回 `false`，两个未持久化的实体永远不相等——不要依赖内存中未落库实体的相等性做去重。
- **`BasicId` 是 `protected set`**：主键只能在实体内部/构造时赋值，外部不可随意改写，避免破坏相等性契约。
- **领域事件不会自动派发**：`AddLocalEvent`/`AddDistributedEvent` 只是把事件排入聚合内的队列；真正的发布（发件箱/事件总线）由基础设施在保存聚合时读取并 `MarkEventsAsCommitted`。本包只负责收集与排序。
- **`CheckRuleAsync` 是包装同步**：`BusinessRuleExtensions.CheckRuleAsync` 通过 `Task.Run` 包装同步 `CheckRule`，并非真正的异步 I/O——规则里别做重 I/O，重逻辑请在领域服务里处理。
- **多租户 `TenantId` 非空约定**：`TenantId` 为 `long` 非空，`0` 表示平台/全局租户、业务租户从 `1` 起；这样 `UNIQUE(TenantId, XxCode)` 复合唯一索引对全局记录也成立（NULL 在 MySQL/PG 唯一约束中不视为相等）。
- **规约到 SQL 的可翻译性**：`` `Specification<T>.ToExpression()` `` 会被仓储下推到数据库，务必写成 provider 可翻译的表达式（避免闭包外的 CLR 方法调用）；`IsSatisfiedBy` 走 `Compile()` 在内存执行，二者场景不同别混用。
- **仓储实现来自 Data 包**：本包只有接口，直接依赖它无法工作——运行期必须引用 `XiHan.Framework.Data` 才能拿到 SqlSugar 仓储实现。

## 依赖模块

- [XiHan.Framework.Domain.Shared](./domain-shared) — 唯一直接依赖，复用其分页查询契约（`PageRequestDtoBase` / `` `PageResultDtoBase<T>` ``）

## 相关模块

- [XiHan.Framework.Domain.Shared](./domain-shared) — 领域共享的分页/查询模型
- [XiHan.Framework.Data](./data) — 基础设施层，提供本包仓储抽象的 SqlSugar 具体实现与注册
- [XiHan.Framework.Application](./application) — 应用层，编排领域对象并暴露为 API
- [XiHan.Framework.MultiTenancy](./multitenancy) — 多租户上下文，与 `IMultiTenantEntity` 协同做租户过滤
