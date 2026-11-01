# XiHan.Framework.Persistence.Abstractions

持久化抽象包：仓储、工作单元、查询、实体登记、Transaction 阶段的中间件、协议约定与标识生成。定义 `persistence.relational@1` 的契约，由 InMemory、EF Core、SqlSugar 等 Provider 实现；业务代码只依赖本包，换 Provider 不改代码。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Persistence.Abstractions` |
| 环层 | Ring 2（抽象） |
| 能力 | 定义 `persistence.relational@1`（`persistence.document@1` 预留），由 Provider 提供；可选特性 `SnowflakeIdFeature` 依赖 `kernel.hosting@1`，可选依赖 `caching.distributed@1`（只为启停顺序） |
| 特性入口 | `SnowflakeIdFeature`（可选，雪花标识） |
| 配置节 | `XiHan:Persistence:Snowflake` |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Domain`、`XiHan.Framework.Caching.Abstractions`、`XiHan.Framework.Localization.Abstractions`、`Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.13 |
| AOT | 兼容 |
| 稳定性 | 仓储、工作单元、查询、错误码为 Stable；`DomainEventTracker`、`IDomainEventDispatcher`、`TimeRange`、`LongTextAttribute`、`EntityIndexAttribute` / `EntityIndexes` / `EntityIndex` 与雪花标识相关类型为 Preview |

## 概述

`IRepository<TEntity, TKey>` 是聚合的集合视图，可恢复的失败以 `Result` 返回；`EntityQuery<T>` 描述过滤、排序、分页；`IUnitOfWork` 是一次调用的事务边界，由各 Provider 登记到 Transaction 阶段的 `UnitOfWorkMiddleware` 开启、提交或回滚。`PersistenceConventions` 是 Provider 共用的协议约定：按实体实现的 Domain 接口写入创建时间、修改时间、租户、并发戳，执行软删除与租户匹配。

实体登记不用反射：应用调用 `AddRepository<TEntity, TKey>()`，登记进 `RepositoryRegistrations`；Provider 的特性实现 `IRepositoryRegistrar`，配置阶段为每个登记注册闭合泛型的仓储。

标识生成端口 `IIdGenerator<TKey>` 与两个实现也在本包：Provider 注册 `GuidV7IdGenerator`，应用可选装配 `SnowflakeIdFeature` 得到 `long` 标识。雪花标识的机器位租约建立在 `Caching.Abstractions` 的 `IDistributedLock` 上，这是本包引用缓存契约的原因（ADR-0033）。

本包不暴露 `IQueryable`，不提供按条件的批量更新 / 删除，也不提供跨 Provider 的分布式事务。

## 何时使用

写操作契约、领域服务、后台任务等需要读写实体的代码引用本包。装配哪个 Provider 是应用入口的事，见[持久化](../guide/persistence#选择-provider)。实现自己的 Provider 时实现 `IRepository<TEntity, TKey>`、`IUnitOfWork` 与 `IRepositoryRegistrar`，并通过 [Conformance 一致性套件](./conformance)。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Persistence.Abstractions" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。`Profiles.Standard` 已包含本包与内存实现。

本包的契约不需要启用；雪花标识需要显式装配：

```csharp
builder.AddXiHan(x => x.UseStandardProfile().UseFeature<SnowflakeIdFeature>());
```

这样装配时机器位经 Standard 里 `InMemoryCachingFeature` 的进程内锁租约分配，只适合单实例。多实例部署配置 `WorkerId`，或装配 Redis 等跨进程的缓存 Provider（先后不限，能力图谱让它先启后停），见[标识生成](../guide/distributed-ids#启用雪花标识)。

## 配置

`SnowflakeIdFeature` 读取 `XiHan:Persistence:Snowflake`（`SnowflakeOptions.ConfigurationSection`），逐键读取、不经反射绑定：

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `Epoch` | ISO 8601 日期时间 | `2026-01-01T00:00:00Z` | 时间戳起点，没有时区按 UTC；数据写入后不得再改 |
| `WorkerId` | 整数 0–1023 | 无 | 固定机器位；不配置则经 `IDistributedLock` 租约分配 |
| `ClockDriftToleranceMilliseconds` | 非负整数 | `100` | 时钟回拨不超过它时等待追平 |
| `LeaseTimeToLiveSeconds` | 正整数 | `30` | 机器位租约存活时间，每三分之一续约一次 |

```json
{
  "XiHan": {
    "Persistence": {
      "Snowflake": {
        "WorkerId": 7,
        "ClockDriftToleranceMilliseconds": 100
      }
    }
  }
}
```

运行时数据源的注册表读取 `XiHan:Persistence:DataSources`（`DataSourceOptions.ConfigurationSection`）：

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `MaxCount` | 整数 1–1024 | `64` | 登记数上限，满了再登记新名字返回 `PERSISTENCE_UNAVAILABLE_007` |
| `HealthProbeIntervalSeconds` | 正数 | `15` | 每个数据源健康项的重探间隔，也是单次探测的超时 |

## 主要类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `IRepository<TEntity, TKey>` | `…Abstractions.Repositories` | `FindAsync`、`FindManyAsync`、`ListAsync`、`CountAsync`、`AddAsync` / `AddRangeAsync`、`UpdateAsync` / `UpdateRangeAsync`、`RemoveAsync` / `RemoveRangeAsync` |
| `RepositoryServiceCollectionExtensions` | `…Abstractions.Repositories` | `AddRepository<TEntity, TKey>(connection)`（`connection` 可选：登记到命名连接）、`GetRepositoryRegistrations()` |
| `IRepositoryRegistrar`、`RepositoryRegistrations` | `…Abstractions.Repositories` | Provider 注册闭合泛型仓储的入口与登记集合；`ConnectionOf(type)` 取实体登记的命名连接 |
| `EntityQuery<T>`、`EntityQuery` | `…Abstractions.Queries` | 不可变查询：`Where`、`OrderBy`、`Page`、`Within`、`IncludeDeleted`；`EntityQuery.All<T>()`、`DefaultMaxTake`（1000） |
| `Ordering<T>`、`TimeRange` | `…Abstractions.Queries` | 一项排序；时间范围提示（两端都含，`From` 不能晚于 `To`） |
| `IUnitOfWork`、`UnitOfWorkState`、`UnitOfWorkOptions` | `…Abstractions.UnitOfWork` | 事务边界、状态、开启选项（`TenantId`、`IsReadOnly`、`Connection`：绑定一个命名连接；`Default`：无租户、可写、不绑定连接） |
| `UnitOfWorkMiddleware` | `…Abstractions.UnitOfWork` | Transaction 阶段的中间件，由 Provider 登记 |
| `DomainEventTracker`、`IDomainEventDispatcher` | `…Abstractions.UnitOfWork` | 写过的聚合登记表；领域事件派发端口（Messaging 实现） |
| `PersistenceConventions` | `…Abstractions` | `ApplyOnAdd`、`ApplyOnUpdate`（带存储行的重载让更新沿用存储的创建时间、租户与删除标记）、`TrySoftDelete`、`IsVisibleTo`、`IsDeleted`、`NewStamp` |
| `LongTextAttribute` | `…Abstractions` | 标记长文本列（`[LongText]`） |
| `EntityIndexAttribute`、`EntityIndexes`、`EntityIndex` | `…Abstractions` | 在实体类上声明索引（`[EntityIndex(nameof(A), nameof(B), IsUnique = true)]`）；`EntityIndexes.Resolve(type, table)` 按统一规则推出索引名与列（软删除实体的唯一索引只约束未删除的行：`EntityIndex.ExcludesDeleted`），`MaxNameLength` 63；Provider 都经它建索引 |
| `PersistenceCapabilities`、`PersistenceErrors`、`PersistenceMessages` | `…Abstractions` | 能力 ID、错误码、消息键与消息目录 |
| `IIdGenerator<TKey>`、`GuidV7IdGenerator` | `…Abstractions.Identifiers` | 标识生成端口；UUID v7 实现 |
| `SnowflakeIdFeature`、`SnowflakeIdGenerator`、`SnowflakeOptions`、`SnowflakeId` | `…Abstractions.Identifiers` | 雪花标识的特性、生成器、选项、位布局与解析 |
| `IWorkerIdSource`、`FixedWorkerIdSource`、`LeasedWorkerIdSource` | `…Abstractions.Identifiers` | 机器位来源：固定配置或分布式锁租约 |
| `ISchemaReader`、`SchemaReaderExtensions`、`SchemaLimits` | `…Abstractions.Schema` | 库结构读取：`ListTablesAsync`（关键字、分页、总数）、`GetTablesAsync`（批量，最多 100 张，逐项结果）、`GetTableAsync` |
| `TableListQuery`、`TablePage`、`TableInfo`、`TableSchema`、`ColumnInfo` | `…Abstractions.Schema` | 列表查询、一页表、表（分表的逻辑名与物理表）、表结构、列 |
| `IEntityCatalog`、`EntityMapping`、`EntityColumnMapping`、`EntityProtocols` | `…Abstractions.Schema` | 已登记实体的目录：表、列映射、协议、`[EntityIndex]` 索引 |
| `RegisteredEntity` | `…Abstractions.Repositories` | `RepositoryRegistrations.Entities` 的一项：实体类型、标识类型与登记的命名连接（`Connection`） |
| `SchemaServiceCollectionExtensions` | `…Abstractions.Schema` | `AddUnsupportedSchemaReader()`：不能读结构的 Provider 登记，返回 `PERSISTENCE_UNAVAILABLE_008` |
| `DataSourceRegistry` | `…Abstractions.DataSources` | 运行时数据源的注册表（单例，健康项贡献者）：`GetOrAddAsync`（同定义复用、变了替换）、`GetAsync`、`RemoveAsync`（批量失效）、`TestAsync`（测试连接，不登记）、`ProbeAsync`、`Names`、`IsSupported` |
| `DataSourceDefinition`、`DatabaseTypes` | `…Abstractions.DataSources` | 定义：名字、数据库类型、连接串（`ToString` 不含连接串，`Redact` 脱敏）；常用的数据库类型名 |
| `IDataSource`、`IDataSourceProvider` | `…Abstractions.DataSources` | 已建的数据源；按定义建数据源与探测的 Provider 端口（SqlSugar 实现） |
| `DataSourceOptions`、`DataSourceServiceCollectionExtensions` | `…Abstractions.DataSources` | 选项；`AddDataSourceProvider<T>(configuration)`、`AddDataSourceRegistry()` |
| `ConnectionStrings` | `…Abstractions.DataSources` | `Redact(text, connectionString)`、`Describe(exception, connectionString)`：把连接串与口令类键的值换成 `***` |

表中 `…` 代表 `XiHan.Framework.Persistence`。

## 使用示例

一个用仓储查询与批量导入的作用域服务。`Order` 是 SimpleApp 的订单聚合，实体先经 `builder.Services.AddRepository<Order, Guid>()` 登记，服务用 `builder.Services.AddScoped<OrderCatalog>()` 注册后在操作里注入：

```csharp
using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;

namespace SimpleApp.Orders;

public sealed class OrderCatalog(IRepository<Order, Guid> orders)
{
    // 金额不小于 minAmount 的前 20 条，按金额倒序；租户与软删除过滤由仓储施加
    public Task<IReadOnlyList<Order>> LargestAsync(int minAmount, CancellationToken cancellationToken)
        => orders.ListAsync(
            EntityQuery.All<Order>()
                .Where(Specification.Of<Order>(o => o.Amount >= minAmount))
                .OrderBy(static o => o.Amount, descending: true)
                .Page(0, 20),
            cancellationToken);

    // 返回导入失败的标识；同批里重复的标识第二次是 PERSISTENCE_CONFLICT_002
    public async Task<IReadOnlyList<Guid>> ImportAsync(IReadOnlyList<Order> imported, CancellationToken cancellationToken)
    {
        var results = await orders.AddRangeAsync(imported, cancellationToken);
        var rejected = new List<Guid>();
        for (var index = 0; index < results.Count; index++)
        {
            if (results[index].IsFailure)
            {
                rejected.Add(imported[index].Id);
            }
        }

        return rejected;
    }
}
```

`ImportAsync` 接受部分成功：通过校验的订单随调用提交。要全有或全无，把失败项作为操作结果返回，见[持久化](../guide/persistence#批量方法)；管道外的工作单元写法见[工作单元与事务](../guide/uow#在管道外开工作单元)。

## 行为与限制

- 仓储只在活动的工作单元里工作；在 Transaction 阶段之外直接使用抛出携带 `PERSISTENCE_UNAVAILABLE_001` 的 `FrameworkException`。
- 查询缺省 `Skip` 0、`Take` 1000，`Take` 的上限也是 1000（`EntityQuery.DefaultMaxTake`）；`take` 超过上限抛出携带 `PERSISTENCE_VALIDATION_005` 的 `FrameworkException`，在调用里由 Transaction 阶段回滚并转为同码的校验结果（HTTP 400，参数 `take`、`max`），Transaction 之外仍是异常；`skip` 小于 0、`take` 小于 1 是编程错误，抛出 `ArgumentOutOfRangeException`；都不静默截断，要更多行就分页。`Within` 只是分表扫描范围的提示，不过滤行。
- 批量方法的结果与入参等长、顺序一一对应；空集合不访问后端但仍要求活动的工作单元；批内重复项按入参顺序逐项施加语义。
- 工作单元状态机 `NotStarted → Active → Committed | RolledBack`，违反顺序抛出携带 `PERSISTENCE_VALIDATION_002` 的 `FrameworkException`；同一服务作用域里再经管道的嵌套调用由 `UnitOfWorkMiddleware` 以同码的结果结束（消息键 `persistence.unit_of_work.nested`），处理器不执行。
- `UnitOfWorkMiddleware` 为只读操作（`OperationContract.IsReadOnly`：`get` / `list` 或 `ReadOnly = true`）开只读工作单元：不开数据库事务，仓储写入返回 `PERSISTENCE_UNEXPECTED_001`（操作实现的缺陷，HTTP 500）并把工作单元记为失败，Provider 直接写入入口 `EnsureTransactionAsync` 抛出的同码异常在 Transaction 阶段转为结果（ADR-0073、ADR-0081）。
- 提交前派发领域事件；有事件却没有 `IDomainEventDispatcher` 时调用以 `PERSISTENCE_VALIDATION_003` 结束，不静默丢弃。
- 协议字段只由 Provider 写入：更新沿用存储里的创建时间、租户与删除标记，调用方实例上的这些值不生效（`PersistenceConventions.ApplyOnUpdate(entity, stored, now)`）。
- `PersistenceConventions.NewStamp()` 生成 32 位十六进制并发戳（`Guid` 的 `N` 格式）。
- 索引声明（ADR-0072）：名字是 `IX_` / `UX_` + 表名 + 每列缩写（属性名每个词的前两个字符），不超过 63 个字符、超过不截断而报错；软删除实体的唯一索引是只约束未删除行的部分唯一索引（ADR-0081，PostgreSQL、SQLite、SQL Server 支持，其余数据库启动失败；列里显式写 `IsDeleted` 则是普通唯一索引）；声明错误（没有列、列名重复、不是公开属性、名字超长、同名或同列）在建模 / 建表时抛 `InvalidOperationException`。关系型 Provider 写入撞上唯一索引返回 `PERSISTENCE_CONFLICT_003`，内存实现不建索引、不校验。
- 雪花生成器单实例每毫秒最多 4096 个标识；时钟回拨超过阈值抛 `PERSISTENCE_UNAVAILABLE_004`，没有机器位抛 `PERSISTENCE_UNAVAILABLE_005`，在调用里由特性的中间件转为同码的 UNAVAILABLE 结果（HTTP 503）；租约状态以健康项 `persistence.snowflake.worker` 上报。
- 命名连接（ADR-0084）：实体可以登记到一个命名连接，工作单元可以绑定一个连接（只在它上面读写、开事务），给管道外与业务事务互不等待的平台数据用（SQLite 上的工作流租约）。只有 SqlSugar 支持；EF Core 与内存实现在装配时或 `BeginAsync` 抛 `PERSISTENCE_UNAVAILABLE_008`。用法见[持久化 · 命名连接](../guide/persistence#命名连接)。
- 运行时数据源（ADR-0078）：与应用自己的连接互不相干（不进作用域客户端、不参与实体落点、不进工作单元）；失效靠 `RemoveAsync` 与每次以记录里当前的定义 `GetOrAddAsync`，不用配置变化令牌；登记数有上限、满了不淘汰；每个数据源一条健康项 `persistence.datasource.<name>`；连接串不进日志、错误与健康详情。EF Core 与内存实现不支持，取用返回 `PERSISTENCE_UNAVAILABLE_008`。用法见[持久化 · 运行时数据源](../guide/persistence#运行时数据源)。
- 库结构读取与实体目录（ADR-0078）：读结构一次最多 100 张表、列表 `Take` 最多 1000；分表按已登记实体的模板折叠；实体目录按登记顺序、重复登记只一条。EF Core 与内存实现读结构返回 `PERSISTENCE_UNAVAILABLE_008`。用法见[持久化 · 库结构读取与实体目录](../guide/persistence#库结构读取与实体目录)。

错误码：

| 错误码 | 常量 | 含义 |
| --- | --- | --- |
| `PERSISTENCE_NOTFOUND_001` | `PersistenceErrors.NotFound` | 实体不存在（含已软删除、不属于当前租户） |
| `PERSISTENCE_CONFLICT_001` | `PersistenceErrors.Concurrency` | 乐观并发冲突 |
| `PERSISTENCE_CONFLICT_002` | `PersistenceErrors.Duplicate` | 标识重复 |
| `PERSISTENCE_UNEXPECTED_001` | `PersistenceErrors.ReadOnly` | 只读工作单元拒绝写入（只读操作里的写入，是操作实现的缺陷；HTTP 500） |
| `PERSISTENCE_CONFLICT_003` | `PersistenceErrors.UniqueKeyConflict` | 唯一键重复：写入撞上实体声明的唯一索引（关系型 Provider；之后工作单元只能回滚） |
| `PERSISTENCE_VALIDATION_001` | `PersistenceErrors.TenantRequired` | 多租户实体缺少当前租户 |
| `PERSISTENCE_VALIDATION_002` | `PersistenceErrors.InvalidUnitOfWorkState` | 工作单元状态不允许该操作（抛异常）；同一作用域里再经管道的嵌套调用（结果） |
| `PERSISTENCE_VALIDATION_003` | `PersistenceErrors.EventsUndispatched` | 有未派发的领域事件但没有派发器 |
| `PERSISTENCE_VALIDATION_004` | `PersistenceErrors.UnknownConnection` | 实体落点不成立（SqlSugar `[Tenant]` 或登记的命名连接不存在、两者不一致；绑定的连接不存在或实体不落在它上面；装配错误，抛异常） |
| `PERSISTENCE_VALIDATION_005` | `PersistenceErrors.TakeExceeded` | 查询的取出条数超过上限（构造时抛异常；调用里转为校验结果，HTTP 400） |
| `PERSISTENCE_UNAVAILABLE_001` | `PersistenceErrors.NoActiveUnitOfWork` | 没有活动的工作单元（抛异常） |
| `PERSISTENCE_UNAVAILABLE_002` | `PersistenceErrors.CapacityExceeded` | 存储容量已满（内存实现） |
| `PERSISTENCE_UNAVAILABLE_003` | `PersistenceErrors.StoreUnavailable` | 存储不可达 |
| `PERSISTENCE_VALIDATION_006` | `PersistenceErrors.DataSourceInvalid` | 运行时数据源的数据库类型不认识、连接串格式错 |
| `PERSISTENCE_NOTFOUND_002` | `PersistenceErrors.DataSourceNotFound` | 运行时数据源没有在本进程登记 |
| `PERSISTENCE_NOTFOUND_003` | `PersistenceErrors.TableNotFound` | 要读结构的表不在库里（逐项结果） |
| `PERSISTENCE_UNAVAILABLE_006` | `PersistenceErrors.DataSourceUnavailable` | 运行时数据源连不上（参数 `name`、`reason`，已脱敏） |
| `PERSISTENCE_UNAVAILABLE_007` | `PersistenceErrors.DataSourceCapacityExceeded` | 运行时数据源登记已满 |
| `PERSISTENCE_UNAVAILABLE_008` | `PersistenceErrors.NotSupported` | 当前的持久化 Provider 不支持该能力 |
| `PERSISTENCE_UNAVAILABLE_004` | `PersistenceErrors.ClockMovedBackwards` | 雪花生成器时钟回拨超过阈值（抛异常；调用里转为结果，HTTP 503） |
| `PERSISTENCE_UNAVAILABLE_005` | `PersistenceErrors.WorkerIdUnavailable` | 雪花生成器没有可用的机器位（抛异常；调用里转为结果，HTTP 503；健康项上报） |

错误文案的消息键在 `PersistenceMessages`（目录名 `persistence`），10 种区域的译文嵌入在本包，Provider 装配时注册目录。

## 相关

- [持久化](../guide/persistence)
- [工作单元与事务](../guide/uow)
- [标识生成](../guide/distributed-ids)
- [Domain](./domain)
- [Persistence.InMemory](./persistence-inmemory)、[Persistence.EntityFrameworkCore](./persistence-entityframeworkcore)、[Persistence.SqlSugar](./persistence-sqlsugar)
- [Persistence.Generators](./persistence-generators)
- 规范：RFC-0005《领域协议与持久化》、`conventions/batch-first.md`《批量优先》、`conventions/error-codes.md`《错误码》
