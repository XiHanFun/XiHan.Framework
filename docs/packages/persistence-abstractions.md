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

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[快速上手](/getting-started/installation)。`Profiles.Standard` 已包含本包与内存实现。

本包的契约不需要启用；雪花标识需要显式装配：

```csharp
builder.AddXiHan(x => x.UseStandardProfile().UseFeature<SnowflakeIdFeature>());
```

这样装配时机器位经 Standard 里 `InMemoryCachingFeature` 的进程内锁租约分配，只适合单实例。多实例部署配置 `WorkerId`，或装配 Redis 等跨进程的缓存 Provider（先后不限，能力图谱让它先启后停），见[标识生成](/persistence/identifiers)。

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
| `IUnitOfWork`、`UnitOfWorkState`、`UnitOfWorkOptions` | `…Abstractions.UnitOfWork` | 事务边界、状态、是否已开数据库事务（`HasTransaction`）、开启时的选项（`Options`）；选项是初始化器记录（`TenantId`、`IsReadOnly`、`ReadsPrimary`：只读但读主库；`Connection`：绑定一个命名连接；`Default`：无租户、可写、不绑定连接） |
| `UnitOfWorkMiddleware` | `…Abstractions.UnitOfWork` | Transaction 阶段的中间件，由 Provider 登记 |
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

`ImportAsync` 接受部分成功：通过校验的订单随调用提交。要全有或全无，把失败项作为操作结果返回，见[持久化](/persistence/repositories)；管道外的工作单元写法见[工作单元与事务](/persistence/unit-of-work)。

## 行为与限制

- 仓储只在活动的工作单元里工作；在 Transaction 阶段之外直接使用抛出携带 `PERSISTENCE_UNAVAILABLE_001` 的 `FrameworkException`。
- 查询缺省 `Skip` 0、`Take` 1000，`Take` 的上限也是 1000（`EntityQuery.DefaultMaxTake`）；`take` 超过上限抛出携带 `PERSISTENCE_VALIDATION_005` 的 `FrameworkException`，在调用里由 Transaction 阶段回滚并转为同码的校验结果（HTTP 400，参数 `take`、`max`），Transaction 之外仍是异常；`skip` 小于 0、`take` 小于 1 是编程错误，抛出 `ArgumentOutOfRangeException`；都不静默截断，要更多行就分页。`Within` 只是分表扫描范围的提示，不过滤行。
- 批量方法的结果与入参等长、顺序一一对应；空集合不访问后端但仍要求活动的工作单元；批内重复项按入参顺序逐项施加语义。
- 工作单元状态机 `NotStarted → Active → Committed | RolledBack`，违反顺序抛出携带 `PERSISTENCE_VALIDATION_002` 的 `FrameworkException`；同一服务作用域里再经管道的嵌套调用由 `UnitOfWorkMiddleware` 以同码的结果结束（消息键 `persistence.unit_of_work.nested`），处理器不执行。
- `UnitOfWorkMiddleware` 为只读操作（`OperationContract.IsReadOnly`：`get` / `list` 或 `ReadOnly = true`）开只读工作单元：不开数据库事务，仓储写入返回 `PERSISTENCE_UNEXPECTED_001`（操作实现的缺陷，HTTP 500）并把工作单元记为失败，Provider 直接写入入口 `EnsureTransactionAsync` 抛出的同码异常在 Transaction 阶段转为结果（ADR-0073、ADR-0081）。
- 不带操作描述的调用（消息处理器、作业、直接交给 `App.InvokeAsync` 的处理器）开可写工作单元；只读工作单元拒绝写入的错误消息键是 `persistence.unit_of_work.read_only`，处理器忽略这次失败而返回成功时，提交返回同一错误，调用以它结束；空集合的批量写入不算写入。
- 嵌套调用（在调用里经管道再调操作）新建服务作用域（`CreateAsyncScope()` + `App.CreateContext`），内层按自己的契约开自己的工作单元：只读不传播，只读操作里调用的写操作在自己的事务里提交。管道外自己开的工作单元由 `UnitOfWorkOptions { TenantId, IsReadOnly, Connection, ReadsPrimary }` 决定，`ReadsPrimary` 让只读工作单元读主库，其余仍是只读的语义，没有从库的 Provider 上没有作用（ADR-0122）。
- 数据库事务延迟到第一次写入才开，`IUnitOfWork.HasTransaction` 报告此刻开没开（ADR-0109）：可写工作单元里仓储的单条写入或非空批量写入一经调用即为真（不论结果），`EnsureTransactionAsync` 同样；提交（成功或返回失败）与回滚后为假，只读工作单元恒为假；多连接一个值，绑定命名连接的工作单元只报告那个连接；内存实现第一次写入调用起同样为真。调用里要立即对外产生、不随回滚撤回的副作用之前据此判断，见[工作单元与事务](/persistence/unit-of-work)。
- `HasTransaction` 的边界：查找、查询、计数与空批量不开事务；不同服务作用域的工作单元各报各的；不经仓储、也不经 `EnsureTransactionAsync` 直接对客户端执行的写入不在事务里，也不让它变真。
- 提交前派发领域事件；有事件却没有 `IDomainEventDispatcher` 时调用以 `PERSISTENCE_VALIDATION_003` 结束，不静默丢弃。
- 领域事件在处理器成功后、提交前由中间件把带有未派发事件的聚合交给 `IDomainEventDispatcher`（Messaging 子系统实现，用发件箱时与业务写入同事务），成功即清空缓冲、失败即回滚。
- 协议字段只由 Provider 写入：更新沿用存储里的创建时间、租户与删除标记，调用方实例上的这些值不生效（`PersistenceConventions.ApplyOnUpdate(entity, stored, now)`）。
- `PersistenceConventions.NewStamp()` 生成 32 位十六进制并发戳（`Guid` 的 `N` 格式）。
- 索引声明（ADR-0072）：名字是 `IX_` / `UX_` + 表名 + 每列缩写（属性名每个词的前两个字符），不超过 63 个字符、超过不截断而报错；软删除实体的唯一索引是只约束未删除行的部分唯一索引（ADR-0081，PostgreSQL、SQLite、SQL Server 支持，其余数据库启动失败；列里显式写 `IsDeleted` 则是普通唯一索引）；声明错误（没有列、列名重复、不是公开属性、名字超长、同名或同列）在建模 / 建表时抛 `InvalidOperationException`。关系型 Provider 写入撞上唯一索引返回 `PERSISTENCE_CONFLICT_003`，内存实现不建索引、不校验。
- 索引声明的补充：框架自己的表（发件箱、审计、授予、翻译、调度、工作流）也用 `[EntityIndex]` 声明，不直接标 SqlSugar 的 `[SugarIndex]` 或 EF Core 的 `[Index]`，实体只依赖本包；索引全部升序，一个实体上可以声明多个、多列、可唯一。缩写例：`TenantId → TeId`、`NextFireAt → NeFiAt`，缩写用下划线连接，表名里字母、数字、下划线以外的字符换成下划线。
- 部分唯一索引的列就是声明的列，条件是 `IsDeleted = false`：同一业务键删除、重建、再删除任意多次都不冲突，未删除的行之间唯一；不支持的数据库不退化成普通唯一索引，启动失败的错误码是 `PERSISTENCE_UNAVAILABLE_008`，要唯一约束就把 `IsDeleted` 显式写进列（同一业务键最多一行已删除），或不声明唯一。
- 唯一索引里有可空列时，PostgreSQL、SQLite、MySQL 不约束含 NULL 的行（SQL Server 把 NULL 当相等；EF Core 在 SQL Server 上给这种唯一索引自动加 `IS NOT NULL` 过滤）；要让「全局」这类空值参与唯一性，存成非空的哨兵值（如授予表的空字符串租户）。
- 唯一键冲突的核对与报告：关系型 Provider 在写入失败后核对（同一批里两项同键、库里另一行同键；更新把键改成已有的值同样算；部分唯一索引只比较未删除的行），返回 `PERSISTENCE_CONFLICT_003`（消息键 `persistence.entity.unique_key_conflict`，参数 `entity`、`index`），这次写入的项统一报告，工作单元随后只能回滚、调用以同一错误结束（与写入阶段才发现的标识重复相同）。核对不到的（大小写不敏感排序规则下只差大小写、SqlSugar 上与本工作单元先前未提交的行冲突、键含空值）异常原样抛出。
- 雪花生成器单实例每毫秒最多 4096 个标识；时钟回拨超过阈值抛 `PERSISTENCE_UNAVAILABLE_004`，没有机器位抛 `PERSISTENCE_UNAVAILABLE_005`，在调用里由特性的中间件转为同码的 UNAVAILABLE 结果（HTTP 503）；租约状态以健康项 `persistence.snowflake.worker` 上报。
- 雪花位布局：符号位 0、41 位自 epoch 起的毫秒（约 69.7 年）、10 位机器位、12 位序列，用尽等下一毫秒，线程安全；时钟早于 epoch 是配置错误，启动即失败。`generator.Parse(id)` 或 `SnowflakeId.Parse(id, epoch)` 还原生成时刻、机器位与序列，排障用。
- 机器位租约：没有配置 `WorkerId` 时启动期从 0 起逐个尝试锁 `persistence:snowflake:worker:<n>`，取第一个空位；每三分之一存活时间经 `LockHandle.TryExtendAsync` 原子续约，锁条目一直在、别的实例拿不到这个机器位，续约失败（存储侧已到期或被他人持有）或锁存储不可达都按丢失处理并重新分配；本地按获取时刻加存活时间判定有效，到期未续上即没有机器位。容器里没有 `IDistributedLock`、或 1024 个机器位都被占用时启动失败，不做随机机器位。健康项 `persistence.snowflake.worker`：持有为 Ready，没有为 Degraded（`PERSISTENCE_UNAVAILABLE_005`）。
- 启停顺序：清单声明可选依赖 `caching.distributed@1`，装配了缓存 Provider 时它先于雪花特性启动、后于雪花特性停止，租约在锁存储可用时取得与释放，与 `UseFeature` 的先后无关；固定机器位又没有任何 `caching.distributed` Provider 时按可选依赖缺失处理，特性是 Degraded，生成不受影响。
- 雪花失败的转换：`NewId()` 返回 `long`，失败只能抛出 `FrameworkException`；回拨超过阈值在时钟追上后自动恢复。转换成结果的中间件由本特性登记在 Resilience 阶段末尾，在幂等与工作单元之外，工作单元已回滚、幂等标记已清除；消息键 `persistence.snowflake.clock_moved_backwards` / `persistence.snowflake.worker_unavailable`，HTTP 映射为可重试的 503、gRPC 为 `Unavailable`。管道之外直接调用生成器仍是异常（ADR-0063）。
- 雪花标识在 JSON 里保持 number（后端标准类型，不加字符串转换器）。超过 2^53（约 9.007 × 10^15）的整数在 JavaScript 的 `number` 里会丢精度，而雪花标识从 epoch 之后约 25 天起就全部超过 2^53：前端必须用能保留大整数的方式解析（`JSON.parse` 的 BigInt reviver、按字符串读取原文），以字符串或 `BigInt` 保存与回传，不能 `Number()` 换算。
- 命名连接（ADR-0084）：实体可以登记到一个命名连接，工作单元可以绑定一个连接（只在它上面读写、开事务），给管道外与业务事务互不等待的平台数据用（SQLite 上的工作流租约）。SqlSugar 两样都支持；EF Core 只支持绑定连接的工作单元（ADR-0087），普通工作单元里取用命名连接上的实体抛 `PERSISTENCE_UNAVAILABLE_008`（已定，ADR-0091）；内存实现在装配时或 `BeginAsync` 抛 `PERSISTENCE_UNAVAILABLE_008`。用法见[持久化 · 命名连接](/persistence/connections)。
- 命名连接的细节：SQLite 只有一个写者，业务事务写过之后同一个库文件上的另一个事务要等它结束，所以工作流的租约行要放在另一个库文件。`AddRepository<TEntity, TKey>(connection)` 里 `null` 是 Provider 的缺省落点，非空不能是空白；同一实体登记到两个连接（含一次为空、一次非空）抛 `ArgumentException`；`RepositoryRegistrations.ConnectionOf(type)` 与 `RegisteredEntity.Connection` 取登记的连接。普通工作单元里这样的实体照常参与整体事务，只是落在那个连接上，建表也建在那里。`UnitOfWorkOptions { Connection = name }` 的连接名空白抛 `ArgumentException`，实体不落在它上面时 `PERSISTENCE_VALIDATION_004`，事务只开在它上面，不牵动作用域里的其他连接（包括默认连接）。SqlSugar 的命名连接是客户端里同名 `ConfigId` 的连接（特性创建客户端时在 `XiHan:Persistence:SqlSugar:Connections:<name>` 声明）；EF Core 在 `XiHan:Persistence:EntityFrameworkCore:Connections:<name>` 声明，一个 `DbContext` 一个连接，绑定连接的工作单元把本作用域的 `DbContext` 建在那个连接上。
- 运行时数据源（ADR-0078）：与应用自己的连接互不相干（不进作用域客户端、不参与实体落点、不进工作单元）；失效靠 `RemoveAsync` 与每次以记录里当前的定义 `GetOrAddAsync`，不用配置变化令牌；登记数有上限、满了不淘汰；每个数据源一条健康项 `persistence.datasource.<name>`；连接串不进日志、错误与健康详情。EF Core 与内存实现不支持，取用返回 `PERSISTENCE_UNAVAILABLE_008`。用法见[持久化 · 运行时数据源](/persistence/connections)。
- 运行时数据源的约定：Provider 经 `AddDataSourceProvider<T>(configuration)` 登记 `IDataSourceProvider`（目前是 SqlSugar）；名字 `^[a-z0-9][a-z0-9-]{0,63}$`，`default` 保留给缺省连接；数据库类型不区分大小写，认得哪些由 Provider 决定；`DataSourceDefinition.Describes` 比名字、类型（不区分大小写）与连接串（区分大小写）。注册表是进程内的，多实例时别的实例在下一次以新定义 `GetOrAddAsync` 时替换，所以每次使用都以记录里当前的定义 `GetOrAddAsync`，不要只按名字 `GetAsync`；约定连接 `default` / `shard` 与租户分片照旧。
- 运行时数据源的健康与失败：健康项数据 `databaseType`、`provider`，在后台按 `HealthProbeIntervalSeconds` 探连通性（关系型 Provider 走一次数据库往返，ADR-0123）；尚未探过 Started，成功 Ready，失败或超时 Degraded（`PERSISTENCE_UNAVAILABLE_006`），不影响 readiness。连不上 `PERSISTENCE_UNAVAILABLE_006`（参数 `name`、`reason`）；数据库类型不认识或连接串格式错 `PERSISTENCE_VALIDATION_006`，同名的已登记实例不受影响；`GetAsync` 没有登记 `PERSISTENCE_NOTFOUND_002`；替换同名的不占名额。
- 运行时数据源的机密：`reason` 取驱动异常的消息链，经 `ConnectionStrings.Redact` / `Describe` 把整串连接串与口令类键（键名含 `password`、`pwd`、`secret`、`token`、`key`）的值换成 `***`；主机名、端口、库名、用户名不算机密，驱动消息里有就保留（脱敏范围已定，ADR-0084；选项打印遮蔽登录用户名不扩到这里，ADR-0093）。日志只记名字与数据库类型。
- 库结构读取与实体目录（ADR-0078）：读结构一次最多 100 张表、列表 `Take` 最多 1000；分表按已登记实体的模板折叠；实体目录按登记顺序、重复登记只一条。EF Core 与内存实现读结构返回 `PERSISTENCE_UNAVAILABLE_008`。用法见[持久化 · 库结构读取与实体目录](/persistence/connections)。
- 读结构的细节：列表按名字排序，`Keyword` 在名字与注释里包含匹配，返回过滤后的总数；分页越界不在构造 `TableListQuery` 时抛出，读结构的 Provider 先经 `Validate()` 核对（都是 400 的结果，不连库、不登记数据源，ADR-0084）。批量读超过 100 张整批 `PERSISTENCE_VALIDATION_005`，逐项结果与入参一一对应，找不到的表该项 `PERSISTENCE_NOTFOUND_003`；连接或读取失败整批 `PERSISTENCE_UNAVAILABLE_006`（原因已脱敏）；空集合不连库；`GetTableAsync` 是单项特例。读运行时数据源的结构先按定义登记再读，`dataSource` 传 `null` 读应用的缺省连接。
- 列的内容：类型名（不含长度与精度）、序号、主键（一律不可空）、自增、可空、长度（只给字符与二进制类型的声明长度；没有上限的与其余类型为 null）、精度与小数位（`decimal` / `numeric` / `number` / `money` 族）、注释、缺省值原文；数据库没有的项为 null。已登记的分表实体（SqlSugar `[SplitTable]`）按表名模板把物理表折叠成一张逻辑表（`Order_{year}{month}{day}` → `Order`），`PhysicalTables` 新到旧，列取最新的物理表。实体目录里表名与列名是映射声明的写法，分表是逻辑名；`Protocols` 标出聚合根、创建 / 修改时间、并发戳、软删除、多租户。

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
| `PERSISTENCE_VALIDATION_005` | `PersistenceErrors.TakeExceeded` | 查询的取出条数超过上限（仓储查询构造时抛异常、调用里转为校验结果；表列表与读结构由读取器以结果返回；HTTP 400） |
| `PERSISTENCE_UNAVAILABLE_001` | `PersistenceErrors.NoActiveUnitOfWork` | 没有活动的工作单元（抛异常） |
| `PERSISTENCE_UNAVAILABLE_002` | `PersistenceErrors.CapacityExceeded` | 存储容量已满（内存实现） |
| `PERSISTENCE_UNAVAILABLE_003` | `PersistenceErrors.StoreUnavailable` | 存储不可达 |
| `PERSISTENCE_VALIDATION_006` | `PersistenceErrors.DataSourceInvalid` | 运行时数据源的数据库类型不认识、连接串格式错 |
| `PERSISTENCE_VALIDATION_007` | `PersistenceErrors.ListPageInvalid` | 库结构表列表的分页越界：`Skip` 为负或 `Take` 小于 1（读取器以结果返回，HTTP 400） |
| `PERSISTENCE_VALIDATION_008` | `PersistenceErrors.StorageFormatMismatch` | 库里已有数据的存储形式与模型不符：EF Core + SQLite 上 UTC ticks 时间列还是旧的文本形式（特性启动时抛异常、启动失败；ADR-0094；租户分片库在首次绑定时核对，工作单元开始时抛异常，ADR-0097） |
| `PERSISTENCE_NOTFOUND_002` | `PersistenceErrors.DataSourceNotFound` | 运行时数据源没有在本进程登记 |
| `PERSISTENCE_NOTFOUND_003` | `PersistenceErrors.TableNotFound` | 要读结构的表不在库里（逐项结果） |
| `PERSISTENCE_UNAVAILABLE_006` | `PersistenceErrors.DataSourceUnavailable` | 运行时数据源连不上（参数 `name`、`reason`，已脱敏） |
| `PERSISTENCE_UNAVAILABLE_007` | `PersistenceErrors.DataSourceCapacityExceeded` | 运行时数据源登记已满 |
| `PERSISTENCE_UNAVAILABLE_008` | `PersistenceErrors.NotSupported` | 当前的持久化 Provider 不支持该能力 |
| `PERSISTENCE_UNAVAILABLE_004` | `PersistenceErrors.ClockMovedBackwards` | 雪花生成器时钟回拨超过阈值（抛异常；调用里转为结果，HTTP 503） |
| `PERSISTENCE_UNAVAILABLE_005` | `PersistenceErrors.WorkerIdUnavailable` | 雪花生成器没有可用的机器位（抛异常；调用里转为结果，HTTP 503；健康项上报） |

错误文案的消息键在 `PersistenceMessages`（目录名 `persistence`），10 种区域的译文嵌入在本包，Provider 装配时注册目录。

## 相关

- [持久化](/persistence/overview)
- [工作单元与事务](/persistence/unit-of-work)
- [标识生成](/persistence/identifiers)
- [Domain](./domain)
- [Persistence.InMemory](./persistence-inmemory)、[Persistence.EntityFrameworkCore](./persistence-entityframeworkcore)、[Persistence.SqlSugar](./persistence-sqlsugar)
- [Persistence.Generators](./persistence-generators)
- 规范：RFC-0005《领域协议与持久化》、`conventions/batch-first.md`《批量优先》、`conventions/error-codes.md`《错误码》
