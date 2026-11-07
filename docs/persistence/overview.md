# 持久化与 Provider

这一页讲持久化子系统的骨架：仓储与工作单元是什么、能力 `persistence.relational` 由谁提供、内存、EF Core、SqlSugar 三个 Provider 怎么选怎么启用、实体怎样登记、配置节与错误码一览。读完它，你能让一个带实体的应用在内存实现上跑起来，并知道换成真实数据库要改哪几行。前置知识是上一页[领域模型](/persistence/domain)与[能力图谱与 Provider](/startup/capabilities)。

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

```csharp
builder.Services.AddRepository<Order, Guid>();
```

实体多了以后，引用源生成器 `Persistence.Generators`，由它为本程序集的每个实体生成 `AddGeneratedRepositories()`。SimpleApp 的 `OrdersFeature` 就是这样登记的：

```csharp
public void Configure(FeatureContext context)
{
    // 登记代码由 Persistence.Generators 从实体类生成（Order : IAggregateRoot<Guid>）
    context.Services.AddGeneratedRepositories();
}
```

生成器以分析器形式引用（`dotnet add package XiHan.Framework.Persistence.Generators --prerelease`），不进入运行时依赖。它扫描本项目里实现 `IEntity<TKey>` 的非抽象顶层类，生成：

| 生成物 | 内容 |
| --- | --- |
| `GeneratedEntities.AddGeneratedRepositories(this IServiceCollection)` | 为每个实体调用一次 `AddRepository<TEntity, TKey>()` |
| `GeneratedEntities.ApplyGeneratedEntities(this ModelBuilder[, database])` | 项目引用了 EF Core 时才生成：为每个实体 `modelBuilder.Entity<T>()`，引用了 `Persistence.EntityFrameworkCore` 时接着调用 `ApplyPersistenceConventions(database)` |
| `<实体>Filters` | 实体实现 `ISoftDeletable` 或 `IMultiTenant` 时生成的规约：`NotDeleted`、`ForTenant(tenantId)`、`ActiveFor(tenantId)` |

生成器的诊断：

| 诊断 | 级别 | 含义 | 处理 |
| --- | --- | --- | --- |
| `XHP001` | Error | 实体类经多个 `IEntity<TKey>` 声明了不同的键类型 | 只实现一个 `IEntity<TKey>`；该实体不生成 |
| `XHP002` | Error | 实体类是嵌套类型，或不是 `public` / `internal` | 改为顶层的 `public` 或 `internal` 类；该实体不生成 |
| `XHP003` | Warning | 要生成的 `<实体>Filters` 与已有类型同名 | 该实体的过滤规约不生成，仓储登记照常生成 |
| `XHP004` | Error | 值类型（`struct`、`record struct`）实现了 `IEntity<TKey>` | 改成 `class` 或 `record class`；该类型不生成登记 |

## 最小示例

把上一页的 `Order` 聚合、一个下单操作和 `Program.cs` 组合起来，就是一个能跑的持久化应用。`PlaceOrderRequest`（`Name`、`Amount`）与 `OrderResponse`（`Id`、`Name`、`Amount`、`CreatedAt`）是 SimpleApp 里的请求与响应记录，请求上的校验写法见[数据校验](/web/validation)。

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

```csharp
using SimpleApp.Orders;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRepository<Order, Guid>();
builder.AddXiHan(x => x.UseStandardProfile());

var app = builder.Build();
app.UseXiHan();
app.Run();
```

运行后 HTTP 适配器按操作 ID 推出 `POST /orders`。带租户与权限调用它，响应里的 `CreatedAt` 是 Provider 写入的当前 UTC 时间；再调 `GET /orders`（SimpleApp 的 `orders.list`）只看到当前租户的订单。换成 EF Core 或 SqlSugar 时操作代码一行不改，只改 `Program.cs` 里的 `UseFeature` 与连接配置。操作契约的声明方式见[操作契约](/web/operations)。

## 配置节总览

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Bindings:db` | 字符串 | — | 关系型 Provider 的连接绑定主值 |
| `XiHan:Persistence:InMemory:MaxEntitiesPerType` | 正整数 | `100000` | 内存实现每种实体的容量上限，超出时提交返回 `PERSISTENCE_UNAVAILABLE_002` |
| `XiHan:Persistence:EntityFrameworkCore:EnsureCreated` | 布尔 | `false` | 启动时对默认连接 `EnsureCreated` 建表，只用于开发与测试 |
| `XiHan:Persistence:EntityFrameworkCore:HealthProbeIntervalSeconds` | 正数 | `15` | 健康项重探间隔，也是单次探测的超时 |
| `XiHan:Persistence:EntityFrameworkCore:Connections:<name>` | 字符串 | — | 命名连接 |
| `XiHan:Persistence:SqlSugar:InitTables` | 布尔 | `false` | 启动时按已登记的实体建缺表、加缺列，只宜在开发与测试开启 |
| `XiHan:Persistence:SqlSugar:HealthProbeIntervalSeconds` | 正数 | `15` | 健康项重探间隔，也是单次探测的超时 |
| `XiHan:Persistence:SqlSugar:Connections:<name>` | 字符串 | — | 命名连接 |
| `XiHan:Persistence:SqlSugar:CommandTimeoutSeconds` | 正整数 | `300` | 命令超时 |
| `XiHan:Persistence:SqlSugar:Replicas:*` | — | — | 从库的权重与健康探测，见 [SqlSugar Provider](/persistence/sqlsugar) |
| `XiHan:Persistence:DataSources:MaxCount` | 1–1024 | `64` | 运行时数据源的登记数上限 |
| `XiHan:Persistence:DataSources:HealthProbeIntervalSeconds` | 正数 | `15` | 运行时数据源健康项的重探间隔，也是单次探测与测试连接的超时 |
| `XiHan:Persistence:Snowflake:*` | — | — | 雪花标识，见[标识生成](/persistence/identifiers) |

非法值在启动配置阶段抛出 `ArgumentException`，不回退到缺省值。配置模型本身见[配置模型与选项](/configuration/options)。

## 失败与错误码

| 错误码 | 何时出现 | 形式 |
| --- | --- | --- |
| `PERSISTENCE_VALIDATION_001` | 多租户实体在没有当前租户的调用里新增 | `Result` |
| `PERSISTENCE_VALIDATION_002` | 工作单元状态不允许（重复开始、未开始就提交）；同一服务作用域里再经管道的嵌套调用 | 状态错误抛异常；嵌套调用是调用结果 |
| `PERSISTENCE_VALIDATION_003` | 聚合有未派发的领域事件，但没有注册派发器 | 调用结果 |
| `PERSISTENCE_VALIDATION_004` | 实体落点不成立（装配错误）：`[Tenant]` 或命名连接不存在、不一致，或绑定连接的工作单元里操作的实体不落在它上面 | 启动失败；运行时抛 `FrameworkException` |
| `PERSISTENCE_VALIDATION_005` | 查询或表列表的 `Take` 超过 1000；一次读结构超过 100 张表 | 仓储查询在构造时抛异常，调用里转为校验结果（HTTP 400）；表列表与读结构以 `Result` 返回 |
| `PERSISTENCE_VALIDATION_006` / `007` | 运行时数据源的数据库类型不认识、连接串格式错 / 表列表的 `Skip` 为负或 `Take` 小于 1 | `Result`（HTTP 400） |
| `PERSISTENCE_VALIDATION_008` | EF Core + SQLite：时间列在库里还是旧的文本形式 | 启动失败；租户分片库在首次绑定时核对，那次调用是 `KERNEL_UNEXPECTED_001` |
| `PERSISTENCE_VALIDATION_009` | 主库不同的连接共用同一个从库 | 启动失败；分片连接在那次调用失败 |
| `PERSISTENCE_NOTFOUND_001` | 更新或删除的实体不存在（含已软删除、不属于当前租户） | `Result` |
| `PERSISTENCE_NOTFOUND_002` / `003` | 运行时数据源没有在本进程登记 / 要读结构的表不在库里 | `Result` / 逐项 `Result` |
| `PERSISTENCE_CONFLICT_001` | 乐观并发冲突：并发戳不匹配；内存实现提交时发现行已被别人改过或删掉 | `Result`；后者是提交结果 |
| `PERSISTENCE_CONFLICT_002` | 新增的标识已存在，含并发新增同一标识 | `Result`；内存实现的并发新增是提交结果 |
| `PERSISTENCE_CONFLICT_003` | 写入撞上实体声明的唯一索引（关系型 Provider） | `Result`，工作单元随后只能回滚 |
| `PERSISTENCE_UNEXPECTED_001` | 只读工作单元里新增、更新、删除（操作实现的缺陷，HTTP 500） | 仓储写入返回 `Result` 且提交返回同一错误 |
| `PERSISTENCE_UNAVAILABLE_001` | 仓储在 Transaction 阶段之外使用 | `FrameworkException` |
| `PERSISTENCE_UNAVAILABLE_002` | 内存实现的表已满 | 提交结果、健康项 |
| `PERSISTENCE_UNAVAILABLE_003` | 数据库不可达（EF Core 健康项） | 健康项 |
| `PERSISTENCE_UNAVAILABLE_004` | 雪花生成器时钟回拨超过阈值 | 抛异常；调用里转为结果（HTTP 503） |
| `PERSISTENCE_UNAVAILABLE_005` | 雪花生成器没有可用的机器位 | 抛异常；调用里转为结果（HTTP 503）；健康项 |
| `PERSISTENCE_UNAVAILABLE_006` / `007` | 运行时数据源连不上或读取失败 / 登记已满 | `Result`、健康项 |
| `PERSISTENCE_UNAVAILABLE_008` | 当前 Provider 不支持该能力（EF Core 与内存实现的运行时数据源与库结构读取；内存实现的命名连接；EF Core 普通工作单元里取用命名连接上的实体） | `Result`；命名连接相关的抛 `FrameworkException` |
| `PERSISTENCE_UNAVAILABLE_009` | 从库不可读，只出现在健康项里 | 健康项 |

错误码常量在 `PersistenceErrors`，消息键在 `PersistenceMessages`，10 种区域的译文嵌入在 `Persistence.Abstractions`。错误怎样经协议适配器变成 HTTP 状态与 Problem Details，见[结果与错误处理](/web/errors)。

每个 Provider 还登记一个健康项：内存实现 `persistence.inmemory.capacity`（任一张表满即 Degraded），EF Core `persistence.entityframeworkcore.database` 与 SqlSugar `persistence.sqlsugar.database`（启动时探一次，之后按间隔在后台重探，连不上即 Degraded，应用仍在服务）。健康项的汇总见[生命周期与健康](/startup/lifecycle)。

## 与其他能力的关系

- 事务的开启、提交与领域事件派发：[工作单元与事务](/persistence/unit-of-work)。
- 实体协议与规约：[领域模型](/persistence/domain)。
- 当前租户从哪里来、按租户分库怎样选连接：[租户解析与隔离](/multi-tenancy/resolution)、[租户分片与分库](/multi-tenancy/sharding)。
- 发件箱用仓储与工作单元把消息与业务写入放进同一事务：[领域事件与发件箱](/messaging/domain-events)。
- 自己实现 Provider 时要通过的用例：[一致性套件与门禁](/testing/conformance)。

## 常见问题

### 我该选哪个 Provider

开发与测试用内存实现，不用改任何代码。生产环境二选一：新项目、习惯 EF Core 迁移管理表结构的选 EF Core；已有 SqlSugar 代码、需要一次调用写多个库、按时间分表、差异日志、读写分离或运行时连外部库的选 SqlSugar。两者都不兼容 Native AOT。

### 为什么不暴露 `IQueryable`

`IQueryable` 把 ORM 的表达式翻译能力泄漏进业务契约，换 Provider 时查询语义会变。仓储用规约与 `EntityQuery` 表达常见查询；仓储覆盖不了的查询直接注入 `DbContext` 或 `ISqlSugarClient`，它们与仓储共享同一个事务，但不带软删除与租户过滤，要自己加。

### 仓储方法在后台任务里抛 `PERSISTENCE_UNAVAILABLE_001`

后台任务不经管道，没有 Transaction 阶段替它开工作单元。按[工作单元与事务](/persistence/unit-of-work)的写法：建服务作用域，取 `IUnitOfWork` 并 `BeginAsync`，再取仓储读写，最后 `CommitAsync`。

## 相关包与下一步

- 相关包：[Persistence.Abstractions](/packages/persistence-abstractions)、[Persistence.InMemory](/packages/persistence-inmemory)、[Persistence.EntityFrameworkCore](/packages/persistence-entityframeworkcore)、[Persistence.SqlSugar](/packages/persistence-sqlsugar)、[Persistence.Generators](/packages/persistence-generators)
- 规范：RFC-0005《领域协议与持久化》、`conventions/batch-first.md`《批量优先》
- 下一页：[仓储读写](/persistence/repositories)，讲查找、查询、分页、写入与批量方法
