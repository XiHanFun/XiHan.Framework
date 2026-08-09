# XiHan.Framework.Uow

> 工作单元：靠 AOP 拦截器自动管理事务边界，`[UnitOfWork]` 标注即原子提交，嵌套自动复用，通常无需手动开启/提交事务

- **NuGet**：`XiHan.Framework.Uow`
- **模块类**：`XiHanUowModule`
- **所在层**：基础设施层
- **关键依赖**：仅 .NET 原生 + 框架内部依赖（仅 `XiHan.Framework.Core`，无第三方运行时依赖）

## 概述

这个包实现工作单元（Unit of Work）模式，统一管理一次业务操作的事务边界与提交/回滚。它靠 AOP 拦截器工作：给方法或类打上 `[UnitOfWork]` 特性（或实现 `IUnitOfWorkEnabled`），进入方法时自动开启工作单元，正常返回统一提交、抛异常时回滚，通常无需在业务代码里手写 `BeginTransaction / Commit / Rollback`。

本包只定义工作单元的抽象与生命周期编排：事务的具体开启/提交/回滚由数据层实现的 `ITransactionApi` 承担（如 [XiHan.Framework.Data](./data) 的 `SqlSugarTransactionApi`）。工作单元自身通过 `IDatabaseApiContainer` / `ITransactionApiContainer` 聚合当前请求涉及的所有数据库连接与事务，`CompleteAsync` 时统一提交、`Dispose`/失败时回滚。

## 何时使用

- 希望一个业务方法内的多次数据库写操作作为一个原子事务提交
- 不想在每个 Service 方法里手写事务开启/提交/回滚
- 需要嵌套工作单元：内层方法自动挂到外层已存在的工作单元，避免嵌套事务
- 需要"预留式"工作单元：中间件先占位、拦截器再填充选项（配合 Web 层请求级 UoW）

## 安装与启用

```bash
dotnet add package XiHan.Framework.Uow
```

```csharp
[DependsOn(typeof(XiHanUowModule))]
public class MyModule : XiHanModule { }
```

`XiHanUowModule` 在 `PreConfigureServices` 阶段调用 `services.OnRegistered(UnitOfWorkInterceptorRegistrar.RegisterIfNeeded)`：每当有服务被注册，就判断其实现类型是否是"工作单元类型"（打了 `[UnitOfWork]` 特性、或某个方法打了该特性、或实现 `IUnitOfWorkEnabled`），是则为它挂上 `UnitOfWorkInterceptor` 动态代理拦截器。因此工作单元能力靠"注册即织入"生效，业务侧只管打特性。

## 工作原理

拦截到一个被标记的方法时，`UnitOfWorkInterceptor.InterceptAsync` 的处理：

```
方法进入
  └─ 非 UoW 方法 → 直接 ProceedAsync 放行
  └─ 是 UoW 方法：
       ├─ 计算 XiHanUnitOfWorkOptions（特性优先；未指定 IsTransactional → 按默认策略计算）
       ├─ 若能命中已"预留"的工作单元 → TryBeginReserved → ProceedAsync → SaveChangesAsync
       └─ 否则 → uowManager.Begin(options) → ProceedAsync → uow.CompleteAsync()
```

嵌套复用（`UnitOfWorkManager.Begin`）：

- 若当前已存在工作单元且 `requiresNew=false` → 返回一个 `ChildUnitOfWork` 包装当前 UoW，子工作单元的 `Complete` 不真正提交，提交/回滚由最外层真实 UoW 负责。
- 否则 → 创建新作用域与新的 `IUnitOfWork`，设置 `Outer` 指向环境中已有 UoW，压入 `IAmbientUnitOfWork`（`AsyncLocal` 环境栈），`Dispose` 时弹回外层。

事务是否开启的默认推断（`CreateOptions` + `XiHanUnitOfWorkDefaultOptions`）：

- 特性显式给了 `IsTransactional` → 直接用。
- 否则按 `TransactionBehavior`：`Enabled` 恒真、`Disabled` 恒假、`Auto` 用自动值——自动值来自 `IUnitOfWorkTransactionBehaviourProvider`，缺省时**按方法名启发式**：以 `Get` 开头视为只读（不开事务），其余开事务。

事务登记：真正的连接事务不在本包开启。数据层（如 Data 包）在解析连接时，若发现当前存在事务型工作单元，就通过 `GetOrAddTransactionApi(key, factory)` 把该连接的 `ITransactionApi` 登记进 UoW；`CompleteAsync` 提交全部已登记事务，异常/`Dispose` 触发回滚。

## 核心能力

- **AOP 自动事务边界**：`UnitOfWorkInterceptor` 检测 `[UnitOfWork]`/`IUnitOfWorkEnabled`，自动开启/提交/回滚。
- **嵌套复用**：内层自动挂到外层已存在 UoW（`ChildUnitOfWork`），避免嵌套事务与重复提交。
- **可控事务语义**：`[UnitOfWork]` 可配 `IsTransactional` / `IsolationLevel` / `Timeout` / `IsDisabled`。
- **默认行为可配**：`XiHanUnitOfWorkDefaultOptions`（配置节 `XiHan:Uow:Default`）统一决定 `Auto/Enabled/Disabled` 事务行为与默认隔离级别、超时。
- **工作单元管理器**：`IUnitOfWorkManager` 暴露 `Current`、`Begin`、`Reserve`、`BeginReserved`、`TryBeginReserved`，支持预留式工作单元（配合中间件）。
- **生命周期事件**：`Failed` / `Disposed` 事件，`OnCompleted` 提交后回调，以及本地/分布式事件登记（`AddOrReplaceLocalEvent` / `AddOrReplaceDistributedEvent`），用于事务提交后统一发布领域事件。
- **数据库 / 事务 API 容器抽象**：`IDatabaseApiContainer` / `ITransactionApiContainer` 由数据层接入具体连接与事务。
- **UoW 级键值缓存**：`IUnitOfWork.Items` 字典 + `UnitOfWorkExtensions`（`AddItem` / `GetItemOrDefault` / `GetOrAddItem` / `RemoveItem`），可选配合 `UnitOfWorkCacheItem<TValue>` 表达"已移除"语义，供仓储/服务在同一工作单元生命周期内缓存数据、避免重复查询。

## 主要 API / 类型

### 核心接口

| 类型 | 关键成员 |
| --- | --- |
| `IUnitOfWork` | `Guid Id`、`Dictionary<string, object> Items`（跨方法/模块共享的工作单元级上下文数据）、`IXiHanUnitOfWorkOptions Options`、`IUnitOfWork? Outer`、`bool IsReserved/IsDisposed/IsCompleted`、`Task SaveChangesAsync(...)`、`Task CompleteAsync(...)`、`Task RollbackAsync(...)`、`void OnCompleted(Func<Task>)`、`AddOrReplaceLocalEvent(...)` / `AddOrReplaceDistributedEvent(...)`；继承 `IDatabaseApiContainer`+`ITransactionApiContainer`+`IDisposable`，含 `Failed` / `Disposed` 事件 |
| `IUnitOfWorkManager` | `IUnitOfWork? Current`、`IUnitOfWork Begin(XiHanUnitOfWorkOptions options, bool requiresNew = false)`、`IUnitOfWork Reserve(string reservationName, bool requiresNew = false)`、`void BeginReserved(string, options)`、`bool TryBeginReserved(string, options)` |
| `IAmbientUnitOfWork` / `IUnitOfWorkAccessor` | 环境（`AsyncLocal`）当前工作单元的持有与访问 |
| `IUnitOfWorkEnabled` | 标记接口：实现它的类的所有方法自动视为工作单元方法（无需特性） |

### 特性与拦截

| 类型 | 说明 |
| --- | --- |
| `UnitOfWorkAttribute` | `[UnitOfWork]`，可用于方法/类/接口。构造重载：`()`、`(bool isTransactional)`、`(bool, IsolationLevel)`、`(bool, IsolationLevel, int timeout)`。属性：`bool? IsTransactional`、`int? Timeout`（毫秒）、`IsolationLevel? IsolationLevel`、`bool IsDisabled`。方法级已存在 UoW 时特性对是否"新开"无效（复用当前）。 |
| `UnitOfWorkInterceptor` | AOP 拦截器（`XiHanInterceptor`，`ITransientDependency`），实现自动事务边界 |
| `UnitOfWorkInterceptorRegistrar` | `RegisterIfNeeded(context)`：注册期判断并为工作单元类型挂拦截器 |
| `UnitOfWorkHelper` | `IsUnitOfWorkType(TypeInfo)` / `IsUnitOfWorkMethod(MethodInfo, out attr)` / `GetUnitOfWorkAttributeOrNull(...)` |

### 事务 / 数据库 API 抽象（供数据层实现）

| 类型 | 说明 |
| --- | --- |
| `ITransactionApi` | `Task CommitAsync(...)` + `IDisposable`；数据层实现具体事务提交 |
| `ISupportsRollback` | 支持回滚 `Task RollbackAsync(...)` |
| `IDatabaseApi` | 数据库 API 抽象 |
| `ITransactionApiContainer` | `FindTransactionApi(key)` / `AddTransactionApi(key, api)` / `GetOrAddTransactionApi(key, factory)` |
| `IDatabaseApiContainer` | `FindDatabaseApi(key)` / `AddDatabaseApi(key, api)` / `GetOrAddDatabaseApi(key, factory)`；继承 `IServiceProviderAccessor` |
| `IUnitOfWorkTransactionBehaviourProvider` | 提供 `Auto` 模式的自动事务判定值（默认 `NullUnitOfWorkTransactionBehaviourProvider`） |
| `IUnitOfWorkEventPublisher` | 提交后事件发布（默认 `NullUnitOfWorkEventPublisher`） |

### 扩展方法与 UoW 级缓存

| 类型 | 说明 |
| --- | --- |
| `UnitOfWorkExtensions` | `IUnitOfWork` 的静态扩展方法：`IsReservedFor(reservationName)` 判断是否为指定预留；`AddItem<TValue>(key, value)` / `GetItemOrDefault<TValue>(key)` / `GetOrAddItem<TValue>(key, factory)` / `RemoveItem(key)` 读写 `Items` 字典，用于在同一工作单元生命周期内缓存任意键值数据（如仓储内避免重复查询） |
| `UnitOfWorkCacheItem<TValue>` | 工作单元缓存项包装类：`TValue? Value`、`bool IsRemoved`、`SetValue(value)` / `RemoveValue()` / `GetUnRemovedValueOrNull()`，配合 `Items` 缓存表达"值已被移除"的软删除语义，避免只用 `null` 无法区分"未缓存"与"已删除" |
| `UnitOfWorkCacheItemExtensions` | `GetUnRemovedValueOrNull<TValue>()`：对可空 `UnitOfWorkCacheItem<TValue>?` 的扩展版本，`item` 为 `null` 或已标记移除时返回 `null` |

### 配置类型

| 类型 | 说明 |
| --- | --- |
| `XiHanUnitOfWorkOptions` | 单次工作单元选项：`bool IsTransactional`、`IsolationLevel? IsolationLevel`、`int? Timeout`（毫秒）；`Clone()` |
| `IXiHanUnitOfWorkOptions` | 只读选项视图（`IUnitOfWork.Options` 暴露） |
| `XiHanUnitOfWorkDefaultOptions` | 默认选项（配置节 `XiHan:Uow:Default`），见下 |
| `UnitOfWorkTransactionBehavior` | 枚举：`Auto` / `Enabled` / `Disabled` |

## 配置

配置节：`XiHan:Uow:Default`（`XiHanUnitOfWorkDefaultOptions.SectionName`）。

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `TransactionBehavior` | `UnitOfWorkTransactionBehavior` | `Auto` | 未在特性显式指定时的事务行为：`Auto`（按方法名启发式，`Get*` 只读）/`Enabled`（恒开）/`Disabled`（恒关） |
| `IsolationLevel` | `IsolationLevel?` | `null` | 默认事务隔离级别 |
| `Timeout` | `int?` | `null` | 默认超时（毫秒） |

```json
{
  "XiHan": {
    "Uow": {
      "Default": {
        "TransactionBehavior": "Auto",
        "IsolationLevel": "ReadCommitted",
        "Timeout": 30000
      }
    }
  }
}
```

## 使用示例

### 1. 方法级原子事务

给方法打上特性，方法内的多次写操作即成为一个事务：

```csharp
public class OrderService
{
    private readonly IRepositoryBase<Order, long> _orderRepo;
    private readonly IRepositoryBase<OrderItem, long> _itemRepo;

    public OrderService(
        IRepositoryBase<Order, long> orderRepo,
        IRepositoryBase<OrderItem, long> itemRepo)
    {
        _orderRepo = orderRepo;
        _itemRepo = itemRepo;
    }

    // 整个方法作为一个事务：任一步失败则整体回滚
    [UnitOfWork(isTransactional: true)]
    public async Task PlaceOrderAsync(Order order, OrderItem item)
    {
        await _orderRepo.AddAsync(order);
        await _itemRepo.AddAsync(item);
        // 方法正常返回时统一提交，无需手动 Commit
    }
}
```

### 2. 指定隔离级别

```csharp
[UnitOfWork(isTransactional: true, isolationLevel: IsolationLevel.Serializable)]
public async Task TransferAsync(long from, long to, decimal amount) { /* ... */ }
```

### 3. 手动控制（少数场景）

绝大多数情况用特性即可；确需手动时用 `IUnitOfWorkManager`：

```csharp
public class BatchImporter(IUnitOfWorkManager uowManager)
{
    public async Task ImportAsync()
    {
        using var uow = uowManager.Begin(new XiHanUnitOfWorkOptions(isTransactional: true));
        // ... 多次写操作 ...
        await uow.CompleteAsync();   // 提交；不调用则 Dispose 时回滚
    }
}
```

## 扩展点 / 自定义

- **默认实现替换**：`IUnitOfWorkTransactionBehaviourProvider`（默认 `Null`）注册自定义实现可统一决定 `Auto` 模式下是否开事务，覆盖"按方法名启发式"。
- **提交后事件发布**：`IUnitOfWorkEventPublisher`（默认 `Null`）配合 `AddOrReplaceLocalEvent`/`AddOrReplaceDistributedEvent`，在事务提交后统一发布领域事件（聚合根仓储即用此机制登记事件）。
- **数据层接入**：实现 `ITransactionApi`（可选 `ISupportsRollback`）并在解析连接时 `GetOrAddTransactionApi` 登记，即可让自己的数据源参与工作单元事务。
- **传统类模式**：让类实现 `IUnitOfWorkEnabled`，其所有方法自动纳入工作单元，无需逐一打特性。

## 注意事项与最佳实践

- **靠 AOP 生效，须走代理**：拦截只对经 DI 解析、被动态代理的实例生效；`new` 出来的对象、或未被识别为"工作单元类型"的类不会织入。
- **`Get*` 默认不开事务**：`Auto` 模式下方法名以 `Get`（忽略大小写）开头被判为只读不开事务；需要事务的查询请显式 `[UnitOfWork(isTransactional: true)]`，或改配置为 `Enabled`。
- **嵌套不重复提交**：内层方法命中已有 UoW 时返回 `ChildUnitOfWork`，其 `Complete` 不真正提交；提交/回滚只发生在最外层，`requiresNew=true` 才强制新开独立事务。
- **`IsDisabled`**：`[UnitOfWork(IsDisabled = true)]` 阻止为方法新开工作单元；但若已存在工作单元，该标记被忽略（仍复用外层）。
- **超时字段仅为选项**：`Timeout` 由具体事务实现决定是否/如何生效，本包只透传选项。
- **异步流转**：当前工作单元通过 `AsyncLocal`（`IAmbientUnitOfWork`）在异步调用链中流转，跨 `Task.Run`/独立线程不自动传播。

## 依赖模块

- [XiHan.Framework.Core](./core)（模块化与动态代理基础设施）—— 唯一 ProjectReference，无第三方运行时依赖。

## 相关模块

- [XiHan.Framework.Data](./data)（提供 SqlSugar 事务 API `SqlSugarTransactionApi` 接入）
- [XiHan.Framework.Domain](./domain)（聚合根领域事件借工作单元提交后发布）
- [XiHan.Framework.EventBus](./eventbus)（工作单元提交后事件发布）
- [XiHan.Framework.Core](./core)
