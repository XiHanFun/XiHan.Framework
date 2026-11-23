# 持久化与 Provider

这一页讲持久化子系统的骨架：仓储与工作单元是什么、能力 `persistence.relational` 由谁提供、内存、EF Core、SqlSugar 三个 Provider 怎么选怎么启用、实体怎样登记，以及配置、健康项与错误码各在哪里查。读完它，你能让一个带实体的应用在内存实现上跑起来，并知道换成真实数据库要改哪几行。前置知识是上一页[领域模型](/persistence/domain)与[能力图谱与 Provider](/startup/capabilities)。

## 为什么需要

业务代码直接依赖某个 ORM，会把查询、事务与审计字段的维护散落在每个操作里：换数据库要改一遍，写测试要先起数据库，多租户过滤漏写一处就是数据泄露。框架把这些收进一层稳定契约：操作里只注入 `IRepository<TEntity, TKey>` 读写实体，事务由管道的 Transaction 阶段自动开启与提交，创建时间、租户、软删除、并发戳按实体实现的协议由 Provider 维护。契约在 `Persistence.Abstractions` 里，三个 Provider 通过同一套一致性用例，业务代码换 Provider 不改。

## 概念

| 术语 | 类型 | 说明 |
| --- | --- | --- |
| 仓储 | `IRepository<TEntity, TKey>` | 聚合的集合视图：查找、查询、计数、新增、更新、删除，以及对应的批量方法。不暴露 `IQueryable` |
| 查询 | `EntityQuery<TEntity>` | 不可变的过滤、排序、分页描述，过滤条件是规约 |
| 工作单元 | `IUnitOfWork` | 一次调用的事务边界，作用域生命周期；仓储只在活动的工作单元里工作 |
| 标识生成 | `IIdGenerator<TKey>` | 应用侧生成主键；Provider 缺省注册 UUID v7 |
| 协议约定 | `PersistenceConventions` | Provider 共用的规则：按实体实现的接口写创建时间、修改时间、租户、并发戳，执行软删除与租户匹配 |
| 能力 ID | `persistence.relational@1` | 由 Provider 提供，业务特性在清单里声明依赖它；`persistence.document@1` 预留，框架没有它的 Provider |

仓储按实体实现的协议维护公共字段，可恢复的失败（标识重复、不存在、并发冲突、缺租户）以 `Result` 返回，不抛异常。复杂查询交给 Provider 专属的入口（EF Core 的 `DbContext`、SqlSugar 的 `ISqlSugarClient`），它们与仓储共享同一个事务。仓储只在活动的工作单元里工作：管道的 Transaction 阶段为每次调用开启工作单元，操作契约里注入的仓储直接可用；在它之外使用仓储抛出携带 `PERSISTENCE_UNAVAILABLE_001` 的 `FrameworkException`，后台任务里怎样自己开工作单元见[工作单元与事务](/persistence/unit-of-work)。

## 选择 Provider

| Provider | 包 | 清单角色 | 适用 | Native AOT |
| --- | --- | --- | --- | --- |
| 内存 | `XiHan.Framework.Persistence.InMemory` | `default` | 开发、原型与测试；进程退出即丢失 | 兼容 |
| EF Core | `XiHan.Framework.Persistence.EntityFrameworkCore` | `provider` | 新项目；应用持有自己的 `DbContext`，用 EF Core 迁移管理表结构 | 不兼容 |
| SqlSugar | `XiHan.Framework.Persistence.SqlSugar` | `provider` | 已有 SqlSugar 代码的项目；多库、按时间分表、差异日志、读写分离、运行时数据源 | 不兼容 |

`Profiles.Standard` 已包含内存实现。应用再 `UseFeature` 一个显式 Provider 就顶替它，被顶替的默认实现不装配、不启动。三个 Provider 通过同一套 `Persistence.Conformance` 用例，业务代码（实体、仓储调用、查询）换 Provider 时不改。

::: warning 内存仓储只用于开发与测试
内存实现每种实体一张有界的表（缺省 100000 条），数据只在进程内；读写交出与存入的都是浅复制的副本，引用类型字段不在隔离范围内。生产环境装配 EF Core 或 SqlSugar Provider。
:::

### 启用内存实现

什么都不用做：`UseStandardProfile()` 已经装配了 `InMemoryPersistenceFeature`。不用 Standard 组合时单独 `UseFeature<InMemoryPersistenceFeature>()`（命名空间 `XiHan.Framework.Persistence.InMemory`）。

### 启用 EF Core

安装 `XiHan.Framework.Persistence.EntityFrameworkCore` 与一个 EF Core 数据库驱动（如 `Microsoft.EntityFrameworkCore.Sqlite`），定义 `DbContext`，在装配时传入配置回调：

```csharp
using Microsoft.EntityFrameworkCore;
using XiHan.Framework.Persistence.EntityFrameworkCore;

builder.AddXiHan(x => x
    .UseStandardProfile()
    // 显式 Provider 顶替 Standard 里的内存默认实现
    .UseFeature(new EntityFrameworkCorePersistenceFeature<AppDbContext>(
        static (binding, options) => options.UseSqlite(binding.ValueOrThrow().Value))));
```

细节见 [EF Core Provider](/persistence/entity-framework-core)。

### 启用 SqlSugar

安装 `XiHan.Framework.Persistence.SqlSugar`，在装配时传入配置回调设置数据库类型与连接串：

```csharp
using SqlSugar;
using XiHan.Framework.Persistence.SqlSugar;

builder.AddXiHan(x => x
    .UseStandardProfile()
    .UseFeature(new SqlSugarPersistenceFeature(static (binding, config) =>
    {
        config.DbType = DbType.PostgreSQL;
        config.ConnectionString = binding.ValueOrThrow().Value;
    })));
```

细节见 [SqlSugar Provider](/persistence/sqlsugar)。两个关系型 Provider 的连接串都来自绑定 `db`（配置键 `XiHan:Bindings:db`），见[连接与运行时数据源](/persistence/connections)。

## 登记实体

Provider 不扫描程序集，也不注册开放泛型：应用逐个登记实体，Provider 在配置阶段为每个登记注册闭合泛型的 `IRepository<TEntity, TKey>`。这样换 Provider 不改登记代码，也不需要反射，Native AOT 可用。

1. 实体实现 `IEntity<TKey>`（或 `IAggregateRoot<TKey>`）。
2. 调用 `services.AddRepository<TEntity, TKey>()`。可以在 `AddXiHan()` 之前调用，也可以在特性的 `Configure` 里调用；Provider 装配之后再登记的实体立即注册。
3. 装配一个持久化 Provider 的特性。

实体多了以后，以分析器形式引用源生成器 `Persistence.Generators`：它扫描本项目里实现 `IEntity<TKey>` 的非抽象顶层类，生成 `AddGeneratedRepositories()`，在特性的 `Configure` 里调用 `context.Services.AddGeneratedRepositories()` 就登记了本程序集的全部实体（SimpleApp 的 `OrdersFeature` 就是这样做的）。它还生成 EF Core 的模型登记与软删除、租户过滤规约，生成物、安装方式与诊断 `XHP001`–`XHP004` 见 [Persistence.Generators](/packages/persistence-generators)。

## 最小示例

把上一页的 `Order` 聚合、一个下单操作和一行实体登记组合起来，就是一个能跑的持久化应用。`PlaceOrderRequest`（`Name`、`Amount`）与 `OrderResponse`（`Id`、`Name`、`Amount`、`CreatedAt`）是 SimpleApp 里的请求与响应记录，请求上的校验写法见[数据校验](/web/validation)。

```csharp
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;

namespace SimpleApp.Orders;

[Operation(Id, Expose = Expose.Http | Expose.Grpc | Expose.Mcp, Permission = "orders.write")]
public sealed class CreateOrder(IRepository<Order, Guid> orders, IIdGenerator<Guid> ids, TimeProvider clock) : IOperation<PlaceOrderRequest, OrderResponse>
{
    public const string Id = "orders.create";

    public async ValueTask<Result<OrderResponse>> HandleAsync(PlaceOrderRequest request, InvocationContext context)
    {
        // 标识在应用侧生成，领域事件里就能带上它
        var order = new Order { Id = ids.NewId(), Name = request.Name, Amount = request.Amount };
        order.Place(clock.GetUtcNow());

        // 仓储返回 Result 而不是抛异常：失败直接作为操作结果，Transaction 阶段据此回滚
        var added = await orders.AddAsync(order, context.CancellationToken);
        return added.IsFailure ? added.Error! : new OrderResponse(order.Id, order.Name, order.Amount, order.CreatedAt);
    }
}
```

接在[第一个应用](/getting-started/first-app)换成 Standard 组合之后的 `Program.cs` 上，只多一行实体登记；`CreateOrder` 与它的请求、响应类型照那一页的做法登记（特性里 `AddGeneratedOperations()`，类型进 `JsonSerializerContext`）：

```csharp
using SimpleApp.Orders;
using XiHan.Framework.Persistence.Abstractions.Repositories;

// 写在 builder.AddXiHan(...) 之前；UseStandardProfile() 已装配内存实现，由它注册 IRepository<Order, Guid>
builder.Services.AddRepository<Order, Guid>();
```

运行后 HTTP 适配器按操作 ID 推出 `POST /orders`。带租户与权限调用它，响应里的 `CreatedAt` 是 Provider 写入的当前 UTC 时间；再调 `GET /orders`（SimpleApp 的 `orders.list`）只看到当前租户的订单。换成 EF Core 或 SqlSugar 时操作代码一行不改，只改 `Program.cs` 里的 `UseFeature` 与连接配置。操作契约的声明方式见[操作契约](/web/operations)。

## 配置

本页的示例只用到一个键：关系型 Provider 的连接绑定 `XiHan:Bindings:db`，见[连接与运行时数据源](/persistence/connections)。其余的键各在所属包的参考页：

- 内存实现的容量上限：[Persistence.InMemory](/packages/persistence-inmemory#配置)
- 建表开关、健康探测间隔、命名连接，以及 SqlSugar 的命令超时与从库：[Persistence.EntityFrameworkCore](/packages/persistence-entityframeworkcore#配置)、[Persistence.SqlSugar](/packages/persistence-sqlsugar#配置)
- 运行时数据源与雪花标识：[Persistence.Abstractions](/packages/persistence-abstractions#配置)

非法值在启动配置阶段抛出 `ArgumentException`，不回退到缺省值。配置模型本身见[配置模型与选项](/configuration/options)。

## 健康项

每个 Provider 登记一个健康项：内存实现 `persistence.inmemory.capacity`（任一张表满即 Degraded），EF Core `persistence.entityframeworkcore.database` 与 SqlSugar `persistence.sqlsugar.database`（启动时探一次，之后按间隔在后台重探，连不上即 Degraded，应用仍在服务）。健康项的汇总见[生命周期与健康](/startup/lifecycle)。

## 与其他能力的关系

- 事务的开启、提交与领域事件派发：[工作单元与事务](/persistence/unit-of-work)。
- 实体协议与规约：[领域模型](/persistence/domain)。
- 当前租户从哪里来、按租户分库怎样选连接：[租户解析与隔离](/multi-tenancy/resolution)、[租户分片与分库](/multi-tenancy/sharding)。
- 发件箱用仓储与工作单元把消息与业务写入放进同一事务：[领域事件与发件箱](/messaging/domain-events)。
- 自己实现 Provider 时要通过的用例：[一致性套件与门禁](/testing/conformance)。

## 常见问题

### 为什么不暴露 `IQueryable`

`IQueryable` 把 ORM 的表达式翻译能力泄漏进业务契约，换 Provider 时查询语义会变。仓储用规约与 `EntityQuery` 表达常见查询；仓储覆盖不了的查询直接注入 `DbContext` 或 `ISqlSugarClient`，它们不带仓储施加的软删除与租户过滤，缺的条件要自己加，见[仓储读写](/persistence/repositories#软删除与租户过滤)。

## 相关包与下一步

- 相关包：[Persistence.Abstractions](/packages/persistence-abstractions)、[Persistence.InMemory](/packages/persistence-inmemory)、[Persistence.EntityFrameworkCore](/packages/persistence-entityframeworkcore)、[Persistence.SqlSugar](/packages/persistence-sqlsugar)、[Persistence.Generators](/packages/persistence-generators)
- 错误码：[Persistence.Abstractions 的错误码表](/packages/persistence-abstractions#错误码)，含常量与失败到达调用方的形式
- 规范：RFC-0005《领域协议与持久化》、`conventions/batch-first.md`《批量优先》
- 下一页：[仓储读写](/persistence/repositories)，讲查找、查询、分页、写入与批量方法
