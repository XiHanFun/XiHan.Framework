# XiHan.Framework.Persistence.Abstractions

持久化抽象包：仓储、工作单元、查询、实体登记、Transaction 阶段的中间件、协议约定与标识生成。定义 `persistence.relational@1` 的契约，由 InMemory、EF Core、SqlSugar 等 Provider 实现；业务代码只依赖本包，换 Provider 不改代码。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Persistence.Abstractions` |
| 环层 | Ring 2（抽象） |
| 能力 | 定义 `persistence.relational@1`（`persistence.document@1` 预留），由 Provider 提供；可选特性 `SnowflakeIdFeature` 依赖 `kernel.hosting@1`，可选依赖 `caching.distributed@1`（只为启停顺序） |
| 特性入口 | `SnowflakeIdFeature`（可选，雪花标识） |
| 配置节 | `XiHan:Persistence:Snowflake` |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Domain`、`XiHan.Framework.Caching.Abstractions`、`XiHan.Framework.Localization.Abstractions`、`Microsoft.Extensions.DependencyInjection.Abstractions` |
| AOT | 兼容 |
| 稳定性 | 仓储、工作单元、查询、错误码为 Stable；`IDomainEventDispatcher`、`LongTextAttribute`、`EntityIndexAttribute` / `EntityIndexes` / `EntityIndex` 与雪花标识相关类型为 Preview |

## 概述

`IRepository<TEntity, TKey>` 是聚合的集合视图，可恢复的失败以 `Result` 返回；`EntityQuery<T>` 描述过滤、排序、分页；`IUnitOfWork` 是一次调用的事务边界，由各 Provider 登记到 Transaction 阶段的 `UnitOfWorkMiddleware` 开启、提交或回滚。`PersistenceConventions` 是 Provider 共用的协议约定：按实体实现的 Domain 接口写入创建时间、修改时间、租户、并发戳，执行软删除与租户匹配。

实体登记不用反射：应用调用 `AddRepository<TEntity, TKey>()`，登记进 `RepositoryRegistrations`；Provider 的特性实现 `IRepositoryRegistrar`，配置阶段为每个登记注册闭合泛型的仓储。

标识生成端口 `IIdGenerator<TKey>` 与两个实现也在本包：Provider 注册 `GuidV7IdGenerator`，应用可选装配 `SnowflakeIdFeature` 得到 `long` 标识。雪花标识的机器位租约建立在 `Caching.Abstractions` 的 `IDistributedLock` 上，这是本包引用缓存契约的原因（ADR-0033）。

本包不暴露 `IQueryable`，不提供按条件的批量更新 / 删除，也不提供跨 Provider 的分布式事务。

## 何时使用

写操作契约、领域服务、后台任务等需要读写实体的代码引用本包。装配哪个 Provider 是应用入口的事，见[持久化](/persistence/overview)。实现自己的 Provider 时实现 `IRepository<TEntity, TKey>`、`IUnitOfWork` 与 `IRepositoryRegistrar`，并通过 [Conformance 一致性套件](./conformance)。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Persistence.Abstractions --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[环境与安装](/getting-started/installation)。`Profiles.Standard` 已包含本包与内存实现。

本包的契约不需要启用；雪花标识需要显式装配：

```csharp
builder.AddXiHan(x => x.UseStandardProfile().UseFeature<SnowflakeIdFeature>());
```

这样装配时机器位经 Standard 里 `InMemoryCachingFeature` 的进程内锁租约分配，只适合单实例。多实例部署配置 `WorkerId`，或装配 Redis 等跨进程的缓存 Provider（先后不限，能力图谱让它先启后停），见[标识生成](/persistence/identifiers)。

## 配置

`SnowflakeIdFeature` 读取 `XiHan:Persistence:Snowflake`（`SnowflakeOptions.ConfigurationSection`），逐键读取、不经反射绑定：

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `Epoch` | ISO 8601 日期时间 | `2026-01-01T00:00:00Z` | 时间戳起点，没有时区按 UTC；同一套数据一经写入不得再改，否则已有标识解析出的时间会错位 |
| `WorkerId` | 整数 0–1023 | 无 | 指定即固定机器位，运维保证集群内不重复；不指定则启动时经 `IDistributedLock` 租约分配 |
| `ClockDriftToleranceMilliseconds` | 非负整数 | `100` | 时钟回拨不超过它时等待时钟追平，超过时拒绝生成 |
| `LeaseTimeToLiveSeconds` | 正整数 | `30` | 租约分配时每个机器位租约的存活时间，每三分之一续约一次 |

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

非法值（`WorkerId` 越界或带符号、`Epoch` 不是日期时间）在配置阶段抛出 `ArgumentException`，启动失败。epoch 晚于当前时钟也是配置错误：特性在启动时构造生成器，构造即抛出 `ArgumentException`，不会等到第一次生成。

运行时数据源的注册表读取 `XiHan:Persistence:DataSources`（`DataSourceOptions.ConfigurationSection`）：

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `MaxCount` | 整数 1–1024 | `64` | 登记数上限，满了再登记新名字返回 `PERSISTENCE_UNAVAILABLE_007`，不淘汰已有的；替换同名的不占名额 |
| `HealthProbeIntervalSeconds` | 正数 | `15` | 每个数据源健康项的重探间隔，也是单次探测与测试连接（`TestAsync`）的超时 |

非法值同样在配置阶段抛出 `ArgumentException`，不回退到缺省值。连接绑定 `XiHan:Bindings:db` 与各 Provider 自己的键见 [Persistence.InMemory](./persistence-inmemory#配置)、[Persistence.EntityFrameworkCore](./persistence-entityframeworkcore#配置)、[Persistence.SqlSugar](./persistence-sqlsugar#配置)。

## 主要类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `IRepository<TEntity, TKey>` | `…Abstractions.Repositories` | `FindAsync`、`FindManyAsync`、`ListAsync`、`CountAsync`、`AddAsync` / `AddRangeAsync`、`UpdateAsync` / `UpdateRangeAsync`、`RemoveAsync` / `RemoveRangeAsync` |
| `RepositoryServiceCollectionExtensions` | `…Abstractions.Repositories` | `AddRepository<TEntity, TKey>(connection)`（`connection` 可选：登记到命名连接）、`GetRepositoryRegistrations()` |
| `IRepositoryRegistrar`、`RepositoryRegistrations` | `…Abstractions.Repositories` | Provider 注册闭合泛型仓储的入口与登记集合；`ConnectionOf(type)` 取实体登记的命名连接 |
| `EntityQuery<T>`、`EntityQuery` | `…Abstractions.Queries` | 不可变查询：`Where`、`OrderBy`、`Page`、`Within`、`IncludeDeleted`；`EntityQuery.All<T>()`、`DefaultMaxTake`（1000） |
| `Ordering<T>`、`TimeRange` | `…Abstractions.Queries` | 一项排序；时间范围提示（两端都含，`From` 不能晚于 `To`） |
| `IUnitOfWork`、`UnitOfWorkState`、`UnitOfWorkOptions` | `…Abstractions.UnitOfWork` | 事务边界、状态、是否已开数据库事务（`HasTransaction`）、开启时的选项（`Options`）；选项是初始化器记录（`TenantId`、`IsReadOnly`、`ReadsPrimary`：只读但读主库；`Connection`：绑定一个命名连接；`Default`：无租户、可写、不绑定连接） |
| `UnitOfWorkMiddleware` | `…Abstractions.UnitOfWork` | Transaction 阶段的中间件，由 Provider 登记 |
| `UnitOfWorkScope` | `…Abstractions.UnitOfWork` | 管道之外在新作用域里开工作单元：`RunAsync` 成功提交、失败或抛出回滚；带 `fault` 的重载把异常映射成调用方的错误，带 `prepare` 的重载在取工作单元之前对作用域做准备（绑定租户分片） |
| `DomainEventTracker`、`IDomainEventDispatcher` | `…Abstractions.UnitOfWork` | 写过的聚合登记表；领域事件派发端口（Messaging 实现） |
| `PersistenceConventions` | `…Abstractions` | `ApplyOnAdd`、`ApplyOnUpdate`（带存储行的重载让更新沿用存储的创建时间、租户与删除标记）、`TrySoftDelete`、`IsVisibleTo`、`IsDeleted`、`NewStamp` |
| `LongTextAttribute` | `…Abstractions` | 标记长文本列（`[LongText]`） |
| `EntityIndexAttribute`、`EntityIndexes`、`EntityIndex` | `…Abstractions` | 在实体类上声明索引（`[EntityIndex(nameof(A), nameof(B), IsUnique = true)]`）；`EntityIndexes.Resolve(type, table)` 按统一规则推出索引名与列（软删除实体的唯一索引只约束未删除的行：`EntityIndex.ExcludesDeleted`；多租户实体的唯一索引必须含 `TenantId`，跨租户唯一要显式写 `AcrossTenants = true`，ADR-0198），`MaxNameLength` 63；Provider 都经它建索引 |
| `PersistenceCapabilities`、`PersistenceErrors`、`PersistenceMessages` | `…Abstractions` | 能力 ID、错误码、消息键与消息目录 |
| `IIdGenerator<TKey>`、`GuidV7IdGenerator` | `…Abstractions.Identifiers` | 标识生成端口；UUID v7 实现 |
| `SnowflakeIdFeature`、`SnowflakeIdGenerator`、`SnowflakeOptions`、`SnowflakeId` | `…Abstractions.Identifiers` | 雪花标识的特性、生成器、选项、位布局与解析 |
| `IWorkerIdSource`、`FixedWorkerIdSource` | `…Abstractions.Identifiers` | 机器位来源：固定配置；分布式锁租约由 `SnowflakeIdFeature` 在内部装配 |
| `ISchemaReader`、`SchemaReaderExtensions`、`SchemaLimits` | `…Abstractions.Schema` | 库结构读取：`ListTablesAsync`（关键字、分页、总数）、`GetTablesAsync`（批量，最多 100 张，逐项结果）、`GetTableAsync` |
| `TableListQuery`、`TablePage`、`TableInfo`、`TableSchema`、`ColumnInfo` | `…Abstractions.Schema` | 列表查询（`Validate()` 核对分页，越界是 `PERSISTENCE_VALIDATION_007` / `005`）、一页表、表（分表的逻辑名与物理表）、表结构、列 |
| `IEntityCatalog`、`EntityMapping`、`EntityColumnMapping`、`EntityProtocols` | `…Abstractions.Schema` | 已登记实体的目录：表、列映射、协议、`[EntityIndex]` 索引 |
| `RegisteredEntity` | `…Abstractions.Repositories` | `RepositoryRegistrations.Entities` 的一项：实体类型、标识类型与登记的命名连接（`Connection`） |
| `SchemaServiceCollectionExtensions` | `…Abstractions.Schema` | `AddUnsupportedSchemaReader()`：不能读结构的 Provider 登记，返回 `PERSISTENCE_UNAVAILABLE_008` |
| `DataSourceRegistry` | `…Abstractions.DataSources` | 运行时数据源的注册表（单例，健康项贡献者）：`GetOrAddAsync`（同定义复用、变了替换）、`GetAsync`、`RemoveAsync`（批量失效）、`TestAsync`（测试连接，不登记）、`ProbeAsync`、`Names`、`IsSupported` |
| `DataSourceDefinition`、`DatabaseTypes` | `…Abstractions.DataSources` | 定义：名字、数据库类型、连接串（`ToString` 不含连接串，`Redact` 脱敏）；常用的数据库类型名 |
| `IDataSource`、`IDataSourceProvider`、`DataSourceFailures` | `…Abstractions.DataSources` | 已建的数据源；按定义建数据源（`CreateAsync` → `Result<IDataSource>`，定义不合法用 `DataSourceFailures.Invalid`，原因统一脱敏）与探测的 Provider 端口（SqlSugar 实现） |
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

`ImportAsync` 接受部分成功：通过校验的订单随调用提交。要全有或全无，把失败项作为操作结果返回，见[持久化](/persistence/repositories)；管道外的工作单元写法见[工作单元与事务](/persistence/unit-of-work#在管道外开工作单元)。

## 行为与限制

- 仓储只在活动的工作单元里工作；在 Transaction 阶段之外直接使用抛出携带 `PERSISTENCE_UNAVAILABLE_001` 的 `FrameworkException`。
- 查询缺省 `Skip` 0、`Take` 1000，`Take` 的上限也是 1000（`EntityQuery.DefaultMaxTake`），越界不静默截断，要更多行就分页：`take` 超过上限是 `PERSISTENCE_VALIDATION_005`（调用里由 Transaction 阶段回滚并转为校验结果，参数 `take`、`max`；Transaction 之外仍是异常），负的 `skip` 与小于 1 的 `take` 是编程错误、抛出 `ArgumentOutOfRangeException`。`Within` 只是分表扫描范围的提示，不过滤行。
- 批量方法的结果与入参等长、顺序一一对应；空集合不访问后端但仍要求活动的工作单元；批内重复项按入参顺序逐项施加语义。
- 工作单元状态机 `NotStarted → Active → Committed | RolledBack`，违反顺序抛出携带 `PERSISTENCE_VALIDATION_002` 的 `FrameworkException`；同一服务作用域里再经管道的嵌套调用由 `UnitOfWorkMiddleware` 以同码的结果结束（消息键 `persistence.unit_of_work.nested`），处理器不执行。
- `UnitOfWorkMiddleware` 为只读操作（`OperationContract.IsReadOnly`：`get` / `list` 或 `ReadOnly = true`）开只读工作单元：不开数据库事务，仓储写入返回 `PERSISTENCE_UNEXPECTED_001`（操作实现的缺陷，HTTP 500）并把工作单元记为失败，Provider 直接写入入口 `EnsureTransactionAsync` 抛出的同码异常在 Transaction 阶段转为结果（ADR-0073、ADR-0081）。
- 不带操作描述的调用（消息处理器、作业、直接交给 `App.InvokeAsync` 的处理器）开可写工作单元；只读工作单元拒绝写入的错误消息键是 `persistence.unit_of_work.read_only`，处理器忽略这次失败而返回成功时，提交返回同一错误，调用以它结束；空集合的批量写入不算写入。
- 嵌套调用（在调用里经管道再调操作）新建服务作用域（`CreateAsyncScope()` + `App.CreateContext`），内层按自己的契约开自己的工作单元：只读不传播，只读操作里调用的写操作在自己的事务里提交。管道外自己开的工作单元由 `UnitOfWorkOptions { TenantId, IsReadOnly, Connection, ReadsPrimary }` 决定，`ReadsPrimary` 让只读工作单元读主库，其余仍是只读的语义，没有从库的 Provider 上没有作用（ADR-0122）。
- 数据库事务延迟到第一次写入才开，`IUnitOfWork.HasTransaction` 报告此刻开没开（ADR-0109），不同服务作用域的工作单元各报各的。何时为真、何时为假，以及调用里据此安排不随回滚撤回的副作用，见[工作单元与事务](/persistence/unit-of-work#是否已开数据库事务)。
- 管道之外（单例存储、后台循环、健康探针）用 `UnitOfWorkScope.RunAsync` 开工作单元（ADR-0259）：回滚与释放作用域自身的失败只记警告、不盖掉已确定的结果或异常，取消只认令牌已取消的取消异常，收尾规则的全表见[在管道外开工作单元](/persistence/unit-of-work#在管道外开工作单元)；它不派发领域事件，容器里没有 `IUnitOfWork` 时原样抛出。
- 领域事件在处理器成功后、提交前由中间件把带有未派发事件的聚合交给 `IDomainEventDispatcher`（Messaging 子系统实现，用发件箱时与业务写入同事务），成功即清空缓冲、失败即回滚；有事件却没有派发器时调用以 `PERSISTENCE_VALIDATION_003` 结束，不静默丢弃。
- 协议字段只由 Provider 写入：更新沿用存储里的创建时间、租户与删除标记，调用方实例上的这些值不生效（`PersistenceConventions.ApplyOnUpdate(entity, stored, now)`）。
- `PersistenceConventions.NewStamp()` 生成 32 位十六进制并发戳（`Guid` 的 `N` 格式）。
- 索引声明（ADR-0072）：实体用 `[EntityIndex]` 声明索引，框架自己的表（发件箱、审计、授予、翻译、调度、工作流）也一样，不直接标 SqlSugar 的 `[SugarIndex]` 或 EF Core 的 `[Index]`，实体只依赖本包；内存实现不建索引、不校验。名字规则（ADR-0249）、声明错误、软删除实体的部分唯一索引（ADR-0081）在各数据库上的建法与可空列的唯一性，见[建表约定与索引](/persistence/schema#索引)。
- 唯一键冲突：关系型 Provider 在写入失败后核对，返回 `PERSISTENCE_CONFLICT_003`（消息键 `persistence.entity.unique_key_conflict`，参数 `entity`、`index`），这次写入的项统一报告，工作单元随后只能回滚、调用以同一错误结束（与写入阶段才发现的标识重复相同）。核对的范围与核对不到的情形见[建表约定与索引](/persistence/schema#唯一键冲突)。
- 雪花标识：单实例每毫秒最多 4096 个标识；`NewId()` 返回 `long`，失败只能抛出 `FrameworkException`，调用里由特性登记在 Resilience 阶段末尾的中间件转为同码的 UNAVAILABLE 结果（ADR-0063）；租约状态以健康项 `persistence.snowflake.worker` 上报。位布局、机器位租约、启停顺序、时钟回拨与前端精度见[标识生成](/persistence/identifiers)。
- 命名连接（ADR-0084）：实体可以登记到一个命名连接，工作单元可以绑定一个连接（只在它上面读写、开事务），给管道外与业务事务互不等待的平台数据用（SQLite 上的工作流租约）。SqlSugar 两样都支持；EF Core 只支持绑定连接的工作单元（ADR-0087），普通工作单元里取用命名连接上的实体抛 `PERSISTENCE_UNAVAILABLE_008`（已定，ADR-0091）；内存实现在装配时或 `BeginAsync` 抛 `PERSISTENCE_UNAVAILABLE_008`。登记与绑定的规则、各 Provider 的声明位置见[连接与运行时数据源 · 命名连接](/persistence/connections#命名连接)。
- 运行时数据源（ADR-0078）：与应用自己的连接互不相干（不进作用域客户端、不参与实体落点、不进工作单元）；失效靠 `RemoveAsync` 与每次以记录里当前的定义 `GetOrAddAsync`，不用配置变化令牌；登记数有上限、满了不淘汰；每个数据源一条健康项 `persistence.datasource.<name>`；连接串不进日志、错误与健康详情。EF Core 与内存实现不支持，取用返回 `PERSISTENCE_UNAVAILABLE_008`。用法、健康状态与各项规则见[连接与运行时数据源 · 运行时数据源](/persistence/connections#运行时数据源)。
- 运行时数据源的约定：Provider 经 `AddDataSourceProvider<T>(configuration)` 登记 `IDataSourceProvider`（目前是 SqlSugar）；`DataSourceDefinition.Describes` 比名字、类型（不区分大小写）与连接串（区分大小写）；约定连接 `default` / `shard` 与租户分片照旧。健康项数据是 `databaseType`、`provider`，关系型 Provider 的探测走一次数据库往返（ADR-0123）。
- 运行时数据源的机密：`reason` 取驱动异常的消息链，经 `ConnectionStrings.Redact` / `Describe` 把整串连接串与口令类键（键名含 `password`、`pwd`、`secret`、`token`、`key`）的值换成 `***`；主机名、端口、库名、用户名不算机密，驱动消息里有就保留（脱敏范围已定，ADR-0084；选项打印遮蔽登录用户名不扩到这里，ADR-0093）。日志只记名字与数据库类型。
- 库结构读取与实体目录（ADR-0078）：读结构一次最多 100 张表、列表 `Take` 最多 1000；分表按已登记实体的模板折叠；实体目录按登记顺序、重复登记只一条。EF Core 与内存实现读结构返回 `PERSISTENCE_UNAVAILABLE_008`。
- 读结构的细节：分页越界不在构造 `TableListQuery` 时抛出，读结构的 Provider 先经 `Validate()` 核对（都是 400 的结果，不连库、不登记数据源，ADR-0084）。列表的排序与匹配、批量读的逐项结果、列的内容与分表的折叠见[连接与运行时数据源 · 库结构读取与实体目录](/persistence/connections#库结构读取与实体目录)。

## 错误码

常量在 `PersistenceErrors`（表里省去类名）。「形式」一列是失败怎样到达调用方：`Result` 指仓储、注册表或读取器的返回值，调用结果与提交结果由 Transaction 阶段给出，「抛异常」是携带该码的 `FrameworkException`。

| 错误码 | 常量 | 何时出现 | 形式 |
| --- | --- | --- | --- |
| `PERSISTENCE_NOTFOUND_001` | `NotFound` | 更新或删除的实体不存在（含已软删除、不属于当前租户） | `Result` |
| `PERSISTENCE_NOTFOUND_002` | `DataSourceNotFound` | 运行时数据源没有在本进程登记（`GetAsync` 的名字没有登记过或已移除） | `Result` |
| `PERSISTENCE_NOTFOUND_003` | `TableNotFound` | 要读结构的表不在库里 | 逐项 `Result` |
| `PERSISTENCE_CONFLICT_001` | `Concurrency` | 乐观并发冲突：并发戳不匹配；内存实现提交时发现行已被别人改过或删掉 | `Result`；后者是提交结果 |
| `PERSISTENCE_CONFLICT_002` | `Duplicate` | 新增的标识已存在，含并发新增同一标识 | `Result`；内存实现的并发新增是提交结果 |
| `PERSISTENCE_CONFLICT_003` | `UniqueKeyConflict` | 唯一键重复：写入撞上实体声明的唯一索引（关系型 Provider） | `Result`，工作单元随后只能回滚 |
| `PERSISTENCE_VALIDATION_001` | `TenantRequired` | 多租户实体在没有当前租户的调用里新增 | `Result` |
| `PERSISTENCE_VALIDATION_002` | `InvalidUnitOfWorkState` | 工作单元状态不允许该操作（重复开始、未开始就提交）；同一服务作用域里再经管道的嵌套调用 | 状态错误抛异常；嵌套调用是调用结果 |
| `PERSISTENCE_VALIDATION_003` | `EventsUndispatched` | 聚合有未派发的领域事件，但没有注册派发器 | 调用结果 |
| `PERSISTENCE_VALIDATION_004` | `UnknownConnection` | 实体落点不成立（装配错误）：SqlSugar `[Tenant]` 或登记的命名连接不存在、两者不一致；绑定的连接不存在，或绑定连接的工作单元里操作的实体不落在它上面 | 启动失败；运行时抛异常 |
| `PERSISTENCE_VALIDATION_005` | `TakeExceeded` | 查询或表列表的 `Take` 超过 1000；一次读结构超过 100 张表 | 仓储查询在构造时抛异常，调用里转为校验结果（HTTP 400）；表列表与读结构由读取器以 `Result` 返回（HTTP 400） |
| `PERSISTENCE_VALIDATION_006` | `DataSourceInvalid` | 运行时数据源的数据库类型 Provider 不认识、连接串格式错；同名的已登记实例不受影响 | `Result`（HTTP 400） |
| `PERSISTENCE_VALIDATION_007` | `ListPageInvalid` | 库结构表列表的分页越界：`Skip` 为负或 `Take` 小于 1 | 读取器的 `Result`（HTTP 400） |
| `PERSISTENCE_VALIDATION_008` | `StorageFormatMismatch` | 库里已有数据的存储形式与模型不符：EF Core + SQLite 上 UTC ticks 时间列在库里还是旧的文本形式（ADR-0094） | 特性启动时抛异常、启动失败；租户分片库在首次绑定时核对（ADR-0097），工作单元开始时抛异常，那次调用是 `KERNEL_UNEXPECTED_001` |
| `PERSISTENCE_VALIDATION_009` | `ReplicaMisplaced` | 主库不同的连接共用同一个从库（装配错误） | 固定连接启动失败；分片连接在作用域取客户端时抛异常，那次调用失败 |
| `PERSISTENCE_VALIDATION_010` | `ShardTenantMismatch` | 工作单元的租户与客户端绑定的分片的租户不一致（含不带租户，ADR-0223） | 开始工作单元时抛异常，工作单元保持未开始 |
| `PERSISTENCE_UNAVAILABLE_001` | `NoActiveUnitOfWork` | 没有活动的工作单元：仓储在 Transaction 阶段之外使用 | 抛异常 |
| `PERSISTENCE_UNAVAILABLE_002` | `CapacityExceeded` | 存储容量已满：内存实现的表已满 | 提交结果、健康项 |
| `PERSISTENCE_UNAVAILABLE_003` | `StoreUnavailable` | 存储不可达（EF Core 健康项） | 健康项 |
| `PERSISTENCE_UNAVAILABLE_004` | `ClockMovedBackwards` | 雪花生成器时钟回拨超过阈值 | 抛异常；调用里转为结果（HTTP 503） |
| `PERSISTENCE_UNAVAILABLE_005` | `WorkerIdUnavailable` | 雪花生成器没有可用的机器位 | 抛异常；调用里转为结果（HTTP 503）；健康项 |
| `PERSISTENCE_UNAVAILABLE_006` | `DataSourceUnavailable` | 数据源连不上或读取失败：运行时数据源的测试连接与健康探测，缺省连接与运行时数据源的库结构读取（参数 `name`、`reason`，已脱敏） | `Result`、健康项 |
| `PERSISTENCE_UNAVAILABLE_007` | `DataSourceCapacityExceeded` | 运行时数据源登记已满 | `Result` |
| `PERSISTENCE_UNAVAILABLE_008` | `NotSupported` | 当前 Provider 不支持该能力：EF Core 与内存实现的运行时数据源与库结构读取；内存实现的命名连接；EF Core 普通工作单元里取用命名连接上的实体；软删除实体的部分唯一索引遇上不支持的数据库 | `Result`；命名连接相关的抛异常；部分唯一索引是启动失败 |
| `PERSISTENCE_UNAVAILABLE_009` | `ReplicaUnavailable` | 从库不可读：读落到别的从库或主库上，调用不受影响 | 只出现在健康项里 |
| `PERSISTENCE_UNEXPECTED_001` | `ReadOnly` | 只读工作单元里新增、更新、删除：操作实现的缺陷（HTTP 500） | 仓储写入返回 `Result` 且提交返回同一错误；`EnsureTransactionAsync` 抛出的同码异常在调用里转为结果 |

错误文案的消息键在 `PersistenceMessages`（目录名 `persistence`），10 种区域的译文嵌入在本包，Provider 装配时注册目录。错误怎样经协议适配器变成 HTTP 状态与 Problem Details，见[结果与错误处理](/web/errors)。

## 相关

- [持久化](/persistence/overview)
- [工作单元与事务](/persistence/unit-of-work)
- [标识生成](/persistence/identifiers)
- [Domain](./domain)
- [Persistence.InMemory](./persistence-inmemory)、[Persistence.EntityFrameworkCore](./persistence-entityframeworkcore)、[Persistence.SqlSugar](./persistence-sqlsugar)
- [Persistence.Generators](./persistence-generators)
- 规范：RFC-0005《领域协议与持久化》、`conventions/batch-first.md`《批量优先》、`conventions/error-codes.md`《错误码》
