# XiHan.Framework.Persistence.Abstractions

持久化契约（Ring 2）。能力 `persistence.relational@1`、`persistence.document@1` 由 Provider 提供。

| 契约 | 用途 |
| --- | --- |
| `IRepository<TEntity, TKey>` | 查找、查询、计数、新增、更新、删除，以及按标识集合查找与批量新增 / 更新 / 删除（`FindManyAsync`、`AddRangeAsync` / `UpdateRangeAsync` / `RemoveRangeAsync`：逐项 `Result` 与入参一一对应、通过校验的项一次写入）；可恢复失败用 `Result` 返回 |
| `EntityQuery<T>` | 过滤（规约）、排序、分页、是否包含已删除、时间范围提示（`Within`：只影响分表 / 分区 Provider 扫描的表，不过滤行）；`Take` 取 1 到 `EntityQuery.DefaultMaxTake`（1000），不截断：超过上限在构造时抛出携带 `PERSISTENCE_VALIDATION_005` 的 `FrameworkException`，调用里由 Transaction 阶段转为同码的校验结果（HTTP 400，消息键 `persistence.query.take_exceeded`，参数 `take`、`max`）；`Skip` 为负、`Take` 小于 1 是编程错误，抛 `ArgumentOutOfRangeException` |
| `IUnitOfWork` | 事务边界与当前租户；由 Transaction 阶段的 `UnitOfWorkMiddleware` 开启、提交或回滚；`DomainEvents`（`DomainEventTracker`）登记本工作单元写过的聚合；只读操作（`OperationContract.IsReadOnly`）开只读工作单元，见下文 |
| `IDomainEventDispatcher` | 领域事件的派发端口：处理器成功后、提交前，中间件把带有未派发事件的聚合交给它（Messaging 子系统实现，发件箱时与业务写入同事务），成功即清空缓冲、失败即回滚；有事件却没有派发器返回 `PERSISTENCE_VALIDATION_003`，不静默丢弃 |
| `IIdGenerator<TKey>`、`GuidV7IdGenerator` | 标识生成端口；Provider 注册 `IIdGenerator<Guid>`（UUID 版本 7，时间有序） |
| `SnowflakeIdFeature`、`SnowflakeIdGenerator`、`SnowflakeOptions`、`SnowflakeId`、`IWorkerIdSource`（`FixedWorkerIdSource` / `LeasedWorkerIdSource`） | 雪花 `long` 标识（ADR-0033）：应用显式装配特性后得到 `IIdGenerator<long>`，41 位毫秒 + 10 位机器位 + 12 位序列；机器位显式配置或经 `IDistributedLock` 租约分配；`SnowflakeId.Parse` 还原时间戳、机器位与序列，见下文 |
| `PersistenceConventions` | Provider 共用的协议约定：创建时间、修改时间、租户、并发戳、软删除；`ApplyOnUpdate(entity, stored, now)` 让更新沿用存储行的创建时间、租户与删除标记（协议字段只由 Provider 写入） |
| `LongTextAttribute`、`EntityIndexAttribute`、`EntityIndexes`、`EntityIndex` | Provider 中立的建表声明：`[LongText]` 长文本列；`[EntityIndex(nameof(A), nameof(B), IsUnique = true)]` 在实体类上声明索引（可多个、可多列、可唯一，全部升序），`EntityIndexes.Resolve(type, table)` 按统一规则推出名字与列——SqlSugar 与 EF Core 两个 Provider 都经它建出同名索引，见下文「索引声明」 |
| `IRepositoryRegistrar`、`AddRepository<TEntity, TKey>()` | 应用显式登记实体，Provider 据此注册闭合泛型的仓储；换 Provider 不改业务代码 |
| `ISchemaReader`、`TableListQuery`、`TablePage`、`TableInfo`、`TableSchema`、`ColumnInfo`、`SchemaLimits`、`IEntityCatalog`、`EntityMapping`、`EntityColumnMapping`、`EntityProtocols`、`RegisteredEntity` | 库结构读取与实体目录（ADR-0078）：读缺省连接或运行时数据源的表与列（主键、自增、可空、长度、精度与小数位、注释、缺省值），分表折叠成逻辑表；枚举 `AddRepository` 登记的实体（表、列、协议、`[EntityIndex]` 索引），见下文 |
| `DataSourceRegistry`、`DataSourceDefinition`、`IDataSource`、`IDataSourceProvider`、`DataSourceOptions`、`DatabaseTypes`、`ConnectionStrings` | 运行时数据源（ADR-0078）：按应用记录里的定义（名字、数据库类型、连接串）连外部库，按名缓存、定义变了替换、`RemoveAsync` 显式失效、登记数有上限、每个数据源一条健康项、测试连接不登记；连接串不进日志、错误与健康详情，见下文 |

```csharp
builder.Services.AddRepository<Order, Guid>();          // 应用登记实体
builder.AddXiHan(x => x.UseFeature<InMemoryPersistenceFeature>());   // 或任一 Provider 的特性
```

## 只读工作单元与嵌套调用

`UnitOfWorkMiddleware` 按本次调用的操作契约开工作单元（ADR-0073、ADR-0081）：动词 `get` / `list` 或声明了 `ReadOnly = true` 的操作开只读工作单元，写操作与不带操作描述的调用（消息处理器、作业、直接交给 `App.InvokeAsync` 的处理器）开可写工作单元。

- 只读工作单元不开数据库事务，查找、查询、计数照常。
- 仓储的新增、更新、删除（含批量，空集合除外）返回 `PERSISTENCE_UNEXPECTED_001`（消息键 `persistence.unit_of_work.read_only`，HTTP 500：只读操作写数据是代码缺陷，不是调用方的输入或权限），并把工作单元记为失败：处理器忽略这次失败而返回成功时，提交返回同一错误，调用以它结束。
- Provider 的直接写入入口（EF Core / SqlSugar 工作单元的 `EnsureTransactionAsync`）在只读工作单元里抛出携带同一码的 `FrameworkException`，Transaction 阶段回滚并转为同码结果。
- 嵌套调用（在调用里经管道再调操作）新建服务作用域（`CreateAsyncScope()` + `App.CreateContext`），内层按自己的契约开自己的工作单元：只读不传播，只读操作里调用的写操作在自己的事务里提交。用同一个服务作用域再经管道时，作用域的工作单元已经开始过，内层以 `PERSISTENCE_VALIDATION_002`（消息键 `persistence.unit_of_work.nested`）结束、处理器不执行。
- 管道外自己开的工作单元由 `UnitOfWorkOptions(TenantId, IsReadOnly)` 决定，`UnitOfWorkOptions.Default` 可写。
## 索引声明

框架自己的表（发件箱、审计、授予、翻译、调度、工作流）与应用实体一样，用本包的 `[EntityIndex]` 声明索引，不直接标 SqlSugar 的 `[SugarIndex]` 或 EF Core 的 `[Index]`：实体只依赖本包，换 Provider 不改实体（RFC-0005「索引声明」，ADR-0072）。

```csharp
[EntityIndex(nameof(TenantId), nameof(Code), IsUnique = true)]   // UX_Order_TeId_Co
[EntityIndex(nameof(Status), nameof(CreatedAt))]                 // IX_Order_St_CrAt
public sealed class Order : IEntity<Guid>, IMultiTenant, ISoftDeletable { … }
```

- 命名：`IX_`（唯一 `UX_`）+ 表名 + 每列的缩写（属性名每个词取前两个字符：`TenantId → TeId`、`NextFireAt → NeFiAt`），下划线连接；表名里字母、数字、下划线以外的字符换成下划线。全名不超过 `EntityIndexes.MaxNameLength`（63，PostgreSQL 的标识符上限），超过即报错、不截断。
- 软删除：软删除实体（`ISoftDeletable`）的唯一索引只约束未删除的行（部分唯一索引：列就是声明的列，条件 `IsDeleted = false`，ADR-0081）——同一业务键删除、重建、再删除任意多次都不冲突，未删除的行之间唯一。只有 PostgreSQL、SQLite、SQL Server 支持部分唯一索引，其余数据库（MySQL、Oracle 等）上有这样的声明即启动失败（`PERSISTENCE_UNAVAILABLE_008`），不退化成普通唯一索引；在那些数据库上要唯一约束，就把 `IsDeleted` 显式写进列（普通唯一索引，同一业务键最多一行已删除），或不声明唯一。`EntityIndex.ExcludesDeleted` 标出这类索引。
- 唯一索引里有可空列时，PostgreSQL、SQLite、MySQL 不约束含 NULL 的行（SQL Server 把 NULL 当相等；EF Core 在 SQL Server 上给这种唯一索引自动加 `IS NOT NULL` 过滤）；要让"全局"这类空值参与唯一性，存成非空的哨兵值（如授予表的空字符串租户）。
- 写入撞上唯一索引：关系型 Provider 在写入失败后核对（同一批里两项同键、库里另一行同键；更新把键改成已有的值同样算；部分唯一索引只比较未删除的行），返回 `PERSISTENCE_CONFLICT_003`（消息键 `persistence.entity.unique_key_conflict`，参数 `entity`、`index`），这次写入的项统一报告，工作单元随后只能回滚、调用以同一错误结束（与写入阶段才发现的标识重复相同）。核对不到的（大小写不敏感排序规则下只差大小写、SqlSugar 上与本工作单元先前未提交的行冲突、键含空值）异常原样抛出。
- 声明错误（没有列、列名空白或重复、列不是公开实例属性、名字超长、同一实体两个索引同名或同列）在 Provider 建模 / 建表时抛 `InvalidOperationException`。内存实现不建索引、不校验唯一性。

## 运行时数据源

应用按自己的记录连外部库（代码生成的外部数据源、零代码运行时等），由单例 `DataSourceRegistry` 管（RFC-0005「运行时数据源」，ADR-0078）。支持它的持久化 Provider 经 `AddDataSourceProvider<T>(configuration)` 登记自己的 `IDataSourceProvider`（目前是 SqlSugar）；EF Core 与内存实现只登记注册表，取用与测试连接返回 `PERSISTENCE_UNAVAILABLE_008`。运行时数据源与应用自己的连接互不相干：不进作用域客户端、不参与实体落点、不进工作单元，约定连接 `default` / `shard` 与租户分片照旧。

```csharp
var definition = new DataSourceDefinition("codegen-42", DatabaseTypes.PostgreSql, decryptedConnectionString);
var tested = await registry.TestAsync(definition, ct);           // 测试连接：不登记、不占名额
var source = await registry.GetOrAddAsync(definition, ct);       // 同定义复用，定义变了替换并释放旧的
await registry.RemoveAsync([definition.Name], ct);               // 记录改了、停用或删了：显式失效（批量）
```

- 定义：名字 `^[a-z0-9][a-z0-9-]{0,63}$`（`default` 保留给缺省连接）；数据库类型不区分大小写，常用名在 `DatabaseTypes`，Provider 认得哪些由 Provider 决定；`Describes` 比名字、类型（不区分大小写）与连接串（区分大小写）。
- 失效：显式 `RemoveAsync` 加定义比对，不用配置变化令牌（定义来自应用的库）。注册表是进程内的，多实例时别的实例在下一次以新定义 `GetOrAddAsync` 时替换；所以每次使用都以记录里当前的定义 `GetOrAddAsync`，不要只按名字 `GetAsync`。
- 容量：`XiHan:Persistence:DataSources:MaxCount`（缺省 64，1–1024），满了再登记新名字返回 `PERSISTENCE_UNAVAILABLE_007`，不淘汰已有的；替换同名的不占名额。
- 健康：每个数据源一条 `persistence.datasource.<name>`（数据 `databaseType`、`provider`），按 `XiHan:Persistence:DataSources:HealthProbeIntervalSeconds`（缺省 15，也是单次探测超时）在后台打开连接再关闭；尚未探过 Started，成功 Ready，失败或超时 Degraded（`PERSISTENCE_UNAVAILABLE_006`），不影响 readiness。
- 失败：连不上 `PERSISTENCE_UNAVAILABLE_006`（参数 `name`、`reason`）；数据库类型不认识或连接串格式错 `PERSISTENCE_VALIDATION_006`（同名的已登记实例不受影响）；`GetAsync` 没有登记 `PERSISTENCE_NOTFOUND_002`。
- 机密：`DataSourceDefinition.ToString()` 不含连接串；`reason` 取驱动异常的消息链，经 `ConnectionStrings.Redact` / `Describe` 把整串连接串与口令类键（键名含 `password`、`pwd`、`secret`、`token`、`key`）的值换成 `***`；主机名、端口、库名、用户名不算机密，驱动消息里有就保留。日志只记名字与数据库类型。

## 库结构读取与实体目录

`ISchemaReader` 读关系库的结构，`IEntityCatalog` 枚举已登记的实体（RFC-0005「库结构读取与实体目录」，ADR-0078）。SqlSugar 实现两者；EF Core 与内存实现只实现实体目录，读结构返回 `PERSISTENCE_UNAVAILABLE_008`。

```csharp
var page = await schema.ListTablesAsync(dataSource: null, new TableListQuery { Keyword = "order", Take = 50 }, ct);   // null = 应用的缺省连接
var tables = await schema.GetTablesAsync(definition, ["LegacyOrder", "Sys_Customer"], ct);                       // 运行时数据源：先按定义登记再读
var entities = await catalog.ListAsync(ct);                                                                       // 已登记实体的表、列、协议、索引
```

- 列表：按名字排序，`Keyword` 在名字与注释里包含匹配，`Skip` / `Take` 分页（`Take` 最多 1000，超过 `PERSISTENCE_VALIDATION_005`），返回过滤后的总数。
- 批量读结构：一次最多 `SchemaLimits.MaxTablesPerRead`（100）张，超过整批 `PERSISTENCE_VALIDATION_005`；逐项结果与入参一一对应，找不到的表该项 `PERSISTENCE_NOTFOUND_003`；连接或读取失败整批 `PERSISTENCE_UNAVAILABLE_006`（原因已脱敏）；空集合不连库。`GetTableAsync` 是单项特例。
- 列：类型名、序号、主键（一律不可空）、自增、可空、长度（定点数族以外）、精度与小数位（`decimal` / `numeric` / `number` / `money` 族）、注释、缺省值原文；数据库没有的项为 null。
- 分表：已登记的分表实体（SqlSugar `[SplitTable]`）按表名模板把物理表折叠成一张逻辑表（`Order_{year}{month}{day}` → `Order`），`PhysicalTables` 新到旧，列取最新的物理表。
- 实体目录：每个登记的实体一条（重复登记只一条），表名与列名是映射声明的写法，分表是逻辑名；`Protocols` 标出聚合根、创建 / 修改时间、并发戳、软删除、多租户；`Indexes` 是 `[EntityIndex]` 推出的索引。

## 雪花标识

Profile 缺省只有 GuidV7；要 `long` 标识的应用显式装配 `SnowflakeIdFeature`：

```csharp
// 机器位经分布式锁租约分配：装配任一 caching.distributed 的 Provider（先后不限）
builder.AddXiHan(x => x.UseFeature<InMemoryCachingFeature>().UseFeature<SnowflakeIdFeature>());
// 或配置 XiHan:Persistence:Snowflake:WorkerId=7 固定机器位，不需要缓存 Provider
```

> 警告：上例的 `InMemoryCachingFeature` 只在一个进程内加锁，只适合单实例。多实例部署时每个实例都会租到机器位 0，生成重复的标识。多实例要装配跨进程的 `caching.distributed` Provider（如 `Caching.Redis`），或给每个实例配置互不相同的 `WorkerId`（如 StatefulSet 序号）。

| 配置键（`XiHan:Persistence:Snowflake`） | 缺省 | 说明 |
| --- | --- | --- |
| `Epoch` | `2026-01-01T00:00:00Z` | 时间戳起点（ISO 8601，无时区按 UTC）；同一套数据一经写入不得再改 |
| `WorkerId` | 无 | 0–1023；指定即固定机器位，运维保证集群内不重复（如 StatefulSet 序号） |
| `ClockDriftToleranceMilliseconds` | 100 | 时钟回拨不超过它时等待追平 |
| `LeaseTimeToLiveSeconds` | 30 | 租约分配时每个机器位租约的存活时间，每三分之一续约一次 |

- 位布局：符号位 0，41 位自 epoch 起的毫秒（约 69.7 年）、10 位机器位、12 位序列；单实例每毫秒最多 4096 个，用尽等下一毫秒；线程安全。
- 机器位租约：没有配置 `WorkerId` 时启动期从 0 起逐个尝试锁 `persistence:snowflake:worker:<n>`，取第一个空位；锁不续约，续约 = 释放再获取同一机器位，空位被别的实例拿走或锁存储不可达都按丢失处理并重新分配；本地按获取时刻 + 存活时间判定有效，到期未续上即没有机器位。容器里没有 `IDistributedLock`、或 1024 个机器位都被占用时启动失败，不做随机机器位。健康项 `persistence.snowflake.worker`：持有 Ready，没有 Degraded（`PERSISTENCE_UNAVAILABLE_005`）。
- 启停顺序：清单声明可选依赖 `caching.distributed@1`，装配了缓存 Provider 时它先于雪花特性启动、后于雪花特性停止，租约在锁存储可用时取得与释放，与 `UseFeature` 的先后无关；固定机器位又没有任何 `caching.distributed` Provider 时按可选依赖缺失，特性是 Degraded（生成不受影响）。
- 失败：`NewId()` 返回 `long`，失败只能抛出 `FrameworkException`——回拨超过阈值 `PERSISTENCE_UNAVAILABLE_004`（等时钟追上后自动恢复），没有机器位 `PERSISTENCE_UNAVAILABLE_005`；两者都是 UNAVAILABLE。调用里抛出时由本特性登记在 Resilience 阶段末尾的中间件转为同码的结果（消息键 `persistence.snowflake.clock_moved_backwards` / `persistence.snowflake.worker_unavailable`），HTTP 映射为可重试的 503、gRPC 为 `Unavailable`；它在幂等与工作单元之外，工作单元已回滚、幂等标记已清除。管道之外直接调用生成器仍是异常（ADR-0063）。时钟早于 epoch 是配置错误，启动即失败。
- 排障：`generator.Parse(id)` 或 `SnowflakeId.Parse(id, epoch)` 还原生成时刻、机器位与序列。
- 线格式与前端精度：标识在 JSON 里保持 number（后端标准类型，不加字符串转换器）。超过 2^53（约 9.007 × 10^15）的整数在 JavaScript 的 `number` 里会丢精度，而雪花标识从 epoch 之后约 25 天起就全部超过 2^53——前端必须用能保留大整数的方式解析（`JSON.parse` 的 BigInt reviver、按字符串读取原文）并以字符串或 `BigInt` 保存与回传，不能 `Number()` 换算。

## 依赖方向

依赖 `XiHan.Framework.Kernel`、`XiHan.Framework.Domain` 与 `XiHan.Framework.Caching.Abstractions`（雪花机器位租约用它的 `IDistributedLock`）。被 `Persistence.InMemory`、`Persistence.EntityFrameworkCore`、`Persistence.SqlSugar` 等实现包引用。

## 状态

P2：可用。
