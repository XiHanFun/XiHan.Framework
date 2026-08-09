# XiHan.Framework.Data

> 基于 SqlSugar 的数据访问基础设施：仓储模式、工作单元事务接入、多租户数据隔离、审计/软删除全局过滤、启动自动建库建表与种子数据

- **NuGet**：`XiHan.Framework.Data`
- **模块类**：`XiHanDataModule`
- **所在层**：基础设施层
- **关键依赖**：**SqlSugarCore**（`5.1.4.216`，ORM，直接对接数据库）

## 概述

这个包是框架的数据访问基础设施，用 SqlSugar ORM 落地领域层定义的仓储契约。它统一注册 SqlSugar 客户端、提供多套开箱即用的仓储实现（只读 / 读写 / 软删除 / 审计 / 聚合根），并把主键生成、审计字段注入、多租户数据隔离、软删除过滤、事务接入全部下沉到 AOP 与全局过滤器里。

设计上遵循"仓储只管纯持久化、横切关注点交给基础设施"：租户连接选择由 `ISqlSugarClientResolver` 承担；租户行级过滤与软删过滤由 SqlSugar 全局 `QueryFilter` AOP 注入；创建/修改/删除审计字段与雪花主键由 `DataExecuting` AOP 自动填充；事务边界由 `XiHan.Framework.Uow` 接管。你在业务里只需注入仓储接口读写实体，其它都在幕后完成。

仓储**接口契约定义在 `XiHan.Framework.Domain`**（`IRepositoryBase<TEntity,TKey>` 等），本包提供 SqlSugar 实现并以开放泛型 `TryAddScoped` 注册——业务层可以注册自己的实现覆盖默认行为。

## 何时使用

- 需要用仓储模式访问数据库，而不想手写 SqlSugar 客户端注册、连接管理与事务
- 需要多租户数据隔离：行级租户过滤（`TenantId=0` 视为全局模板）、跨库/跨连接路由
- 需要软删除、审计字段（创建/修改/删除时间与操作人、租户 Id、TraceId）自动注入
- 需要应用启动时自动建库、`CodeFirst` 建表、写入种子数据
- 需要对多个数据库（`ConfigId` 多连接、主从读写分离）统一管理

## 安装与启用

```bash
dotnet add package XiHan.Framework.Data
```

```csharp
[DependsOn(typeof(XiHanDataModule))]
public class MyModule : XiHanModule { }
```

`XiHanDataModule` 依赖 `XiHanDomainModule`、`XiHanUowModule`、`XiHanDistributedIdsModule`、`XiHanMultiTenancyModule`、`XiHanSecurityModule`、`XiHanAuditingModule`。它在 `ConfigureServices` 里调用 `AddXiHanDataSqlSugar(config)` 注册全部服务；在 `OnApplicationInitializationAsync` 里创建作用域、取 `IDbInitializer` 执行 `InitializeAsync()` 完成数据库初始化（初始化失败会抛出中断启动）。

启用后你得到（均为 `TryAdd` 语义，可被业务覆盖）：

- `SqlSugarScope`（单例，线程安全）与按当前租户解析的 `ISqlSugarClient`（Scoped）
- 仓储开放泛型：`IReadOnlyRepositoryBase<,>`、`IRepositoryBase<,>`、`ISoftDeleteRepositoryBase<,>`、`IAuditedRepository<,>`、`IAggregateRootRepository<,>`
- `ISqlSugarClientResolver` / `ISqlSugarTenantConnectionResolver` / `ISqlSugarConnectionConfigurator`
- `SqlSugarDataExecutingHandler`（主键/审计/租户/TraceId 注入）
- `IDatabaseMetadataProvider`（库表元数据）
- `IEntityAuditContextProvider`（默认从当前用户/请求填充）、`IEntityDiffLogWriter`（默认 `Null` 零开销）——这两个契约的默认实现随依赖的 [XiHan.Framework.Auditing](./auditing) 模块一并注册，本包直接复用
- `IDbInitializer`（建库/建表/种子）
- `SqlSugarSlaveHealthCheckService`（后台从库健康探针，始终注册；由 `EnableSlaveHealthCheck` 开关决定是否实际探测，未开启时空转零成本）

## 工作原理

一次仓储读写背后的横切链路：

```
业务调用仓储方法
  └─ 仓储通过 ISqlSugarClientResolver.GetCurrentClient() 取当前客户端
       ├─ 若存在租户连接提供器且处于租户上下文 → 解析租户独立连接（库隔离，fail-closed）
       ├─ 否则 → ISqlSugarTenantConnectionResolver 解析 ConfigId（行/字段隔离）
       └─ 若当前存在事务型工作单元 → 把该连接的事务登记进 UoW（GetOrAddTransactionApi）
  └─ SqlSugar 执行 SQL
       ├─ QueryFilter 全局过滤器（AOP）：自动 AND 软删过滤 + 租户过滤
       ├─ DataExecuting AOP：Insert 注入雪花主键+创建审计+TraceId；Update/Delete 注入修改/删除审计
       └─ （可选）OnDiffLogEvent AOP：生成 before/after 快照 → IEntityDiffLogWriter 落库
```

关键约定：

- **连接选择按当前租户实时解析**：`DbClient` 每次访问都重新解析，租户上下文切换即生效。
- **静态连接与运行时动态连接一致**：`SqlSugarConnectionConfigurator` 复用同一套过滤器/AOP 装配逻辑，保证运行时新注册的租户连接与启动期静态连接行为完全一致。
- **事务不在仓储内开启**：仓储只做 before 预读保证实体在当前租户范围内，事务边界统一由工作单元接管（见 [XiHan.Framework.Uow](./uow)）。
- **写操作前的租户安全校验**：`UpdateAsync`/`DeleteAsync`/批量操作会先按当前过滤器读取实体，读不到即视为"不存在或不在当前租户范围内"并抛 `InvalidOperationException`，防止越租户改删。
- **SQL 执行自动上报链路追踪**：每条 SQL 执行完成（或异常）后，`SetSugarAop` 按已知耗时回溯生成一个 `db.query`（`ActivityKind.Client`）的 OpenTelemetry Activity，挂在当前请求 Activity 之下，携带 `db.system`/`db.statement` 标签，异常时记录状态与堆栈；基于 `XiHanActivitySources.DataSource`，无监听者时直接跳过、零开销。

## 核心能力

- **多套仓储实现**：只读、读写、软删除、审计、聚合根，全部开放泛型注册，注入接口即用。
- **多租户数据隔离**：全局 `QueryFilter` 自动过滤租户数据（当前无租户上下文时不过滤，有上下文时"本租户数据 OR 全局模板 `TenantId=0`"）；连接层支持按 `ConfigId`/前缀/名称/自定义委托解析，或由业务提供 `ISqlSugarTenantConnectionProvider` 做库级隔离。
- **软删除过滤**：实现 `ISoftDelete` 的实体自动过滤 `IsDeleted`；`ISoftDeleteRepositoryBase<,>` 额外提供 `SoftDelete`/`Restore`/`GetDeleted`/`GetAllWithDeleted` 等。
- **审计字段自动注入**：通过 SqlSugar `DataExecuting` AOP 注入雪花主键、创建/修改/删除时间与操作人、`TenantId`、`TraceId`，业务与仓储都无需手填。
- **实体差异日志**：可选启用（`EnableDiffLog`），基于 SqlSugar 原生 `OnDiffLogEvent` AOP 生成 before/after 快照，交 `IEntityDiffLogWriter` 落库。
- **分页 / 规约 / 自动查询**：内置多种分页重载（`pageIndex/pageSize`、`PageRequestDtoBase`、规约 `ISpecification<TEntity>`、`GetPagedAutoAsync` 按 DTO 自动构建条件）。
- **数据库初始化**：可选建库（自动处理 MySQL utf8mb4 归一化）、`CodeFirst` 建表（含分表 `SplitTables` 识别）、按 `Order` 顺序执行 `IDataSeeder`。
- **雪花 ID**：接入 `XiHan.Framework.DistributedIds`，通过 `StaticConfig.CustomSnowFlakeFunc` 作为 SqlSugar 全局主键生成器。
- **SQL 日志与慢查询**：可开启 SQL/异常/慢 SQL 日志，慢 SQL 阈值可配。
- **主从读写分离**：SqlSugar 原生主从能力完整放出，appsettings 声明从库即可分担读；差异化权重与更多原生定制走代码钩子；可选从库健康探针自动摘除/回填权重（详见下文）。
- **SQL 执行链路追踪**：每条 SQL 执行完成/异常自动上报 `db.query` OpenTelemetry Client Span，挂在当前请求 Activity 下；无监听者时零开销，不需要额外配置。

## 主要 API / 类型

### 仓储契约与实现

| 契约（Domain 定义） | 实现（本包） | 说明 |
| --- | --- | --- |
| `IReadOnlyRepositoryBase<TEntity,TKey>` | `SqlSugarReadOnlyRepository<,>` | 只读：查询/统计/存在性/分页 |
| `IRepositoryBase<TEntity,TKey>` | `SqlSugarRepositoryBase<,>` | 读写：在只读之上增删改 |
| `ISoftDeleteRepositoryBase<TEntity,TKey>` | `SqlSugarSoftDeleteRepository<,>` | 软删除 / 恢复 / 查询已删记录 |
| `IAuditedRepository<TEntity,TKey>` | `SqlSugarAuditedRepository<,>` | 审计仓储 |
| `IAggregateRootRepository<TEntity,TKey>` | `SqlSugarAggregateRepository<,>` | 聚合根仓储，写操作登记领域事件到工作单元 |

`SqlSugarRepositoryBase<TEntity,TKey>` 约束 `where TEntity : class, IEntityBase<TKey>, new()`、`where TKey : IEquatable<TKey>`。

只读仓储关键方法（`SqlSugarReadOnlyRepository<,>`）：

| 方法 | 说明 |
| --- | --- |
| `Task<TEntity?> GetByIdAsync(TKey id, ...)` | 按主键取单条 |
| `Task<IReadOnlyList<TEntity>> GetByIdsAsync(IEnumerable<TKey> ids, ...)` | 按主键集合取 |
| `Task<TEntity?> GetFirstAsync(Expression<Func<TEntity,bool>> predicate, ...)` | 条件取首个；另有规约重载 |
| `Task<IReadOnlyList<TEntity>> GetListAsync(Expression<Func<TEntity,bool>> predicate, ...)` | 条件列表；另有排序 / 规约 / `PageRequestDtoBase` 重载 |
| `Task<IReadOnlyList<TEntity>> GetAllAsync(...)` | 取全部 |
| `Task<long> CountAsync(...)` | 统计（无参 / 条件 / 规约） |
| `Task<bool> AnyAsync(Expression<Func<TEntity,bool>> predicate, ...)` | 存在性判断（条件 / 规约） |
| `Task<PageResultDtoBase<TEntity>> GetPagedAsync(int pageIndex, int pageSize, ...)` | 分页；多种重载（条件、条件+排序、规约、`PageRequestDtoBase`） |
| `Task<PageResultDtoBase<TEntity>> GetPagedAutoAsync(object queryDto, ...)` | 按查询 DTO 自动构建条件分页 |

读写仓储关键方法（`SqlSugarRepositoryBase<,>`）：

| 方法 | 说明 |
| --- | --- |
| `Task<TEntity> AddAsync(TEntity entity, ...)` | 新增，返回持久化后实体 |
| `Task<TKey> AddReturnIdAsync(TEntity entity, ...)` | 新增并返回主键 |
| `Task<IReadOnlyList<TEntity>> AddRangeAsync(IEnumerable<TEntity> entities, ...)` | 批量新增 |
| `Task<TEntity> UpdateAsync(TEntity entity, ...)` | 按主键更新（先租户可见性预读） |
| `Task<bool> UpdateAsync(Expression<Func<TEntity,TEntity>> columns, Expression<Func<TEntity,bool>> where, ...)` | 按条件更新指定列 |
| `Task<IReadOnlyList<TEntity>> UpdateRangeAsync(IEnumerable<TEntity> entities, ...)` | 批量更新（校验全部可见） |
| `Task<bool> AddOrUpdateAsync(TEntity entity, ...)` | 按 `IsTransient()` 决定新增/更新；另有批量重载 |
| `Task<bool> DeleteAsync(TEntity entity, ...)` | 按实体删除；另有 `DeleteByIdAsync`、按条件、批量重载 |

> 软删除过滤开启时，对实现 `ISoftDelete` 的实体，`Delete*` 走的是全局过滤器语义；`SqlSugarSoftDeleteRepository` 的 `SoftDeleteAsync` 是把 `IsDeleted=true`（并置 `DeletedTime`）走更新落库，`RestoreAsync` 反之。

### 客户端与租户解析

| 类型 | 说明 |
| --- | --- |
| `ISqlSugarClientResolver` | `GetCurrentClient()` 按当前租户解析并把连接登记进事务型 UoW；`GetClient(configId)`、`GetAllConfigIds()`、`GetAllClients()`（初始化/种子遍历各库）、`AsTenant()` |
| `ISqlSugarTenantConnectionResolver` | `ResolveCurrentConfigId()` / `ResolveConfigId(long? tenantId, string? name)` / `GetConfigIds()`——解析顺序：自定义委托 → `tenantId` 直配 → `前缀+tenantId` → 租户名 → 默认 |
| `ISqlSugarConnectionConfigurator` | `Configure(provider)` 装配过滤器+AOP；`EnsureTenantConnection(tenant, descriptor)` 运行时幂等注册租户独立连接（缺连接串 fail-closed） |
| `ISqlSugarTenantConnectionProvider` | 业务实现的租户库隔离扩展点：`Resolve(tenantId, name)` 返回连接描述符；返回 `null` 退化为 `ConfigId` 解析，抛异常则 fail-closed |
| `SqlSugarTransactionApi` | 工作单元事务适配器，`ITransactionApi`+`ISupportsRollback`，包装 `Ado.BeginTran/CommitTran/RollbackTran` |

### 初始化与种子

| 类型 | 说明 |
| --- | --- |
| `IDbInitializer` | `InitializeAsync()`（完整流程）/ `CreateDatabaseAsync()` / `CreateTablesAsync()` / `SeedDataAsync()` |
| `IDataSeeder` | 种子契约：`int Order`（越小越先）/ `string Name` / `Task SeedAsync()` |
| `DataSeederBase` | 种子基类：提供 `DbClient`、`HasDataAsync<T>(predicate)`、`BulkInsertAsync<T>(list)` 等辅助 |
| `IDatabaseMetadataProvider` | 库表结构元数据读取 |

### 审计

| 类型 | 说明 |
| --- | --- |
| `SqlSugarDataExecutingHandler` | `DataExecuting` AOP 处理器：Insert 注入雪花主键+创建审计+`TraceId`；Update 注入修改审计（含软删）；Delete 注入删除审计 |
| `IEntityAuditContextProvider` | 审计上下文来源，默认 `DefaultEntityAuditContextProvider`（从 `ICurrentUser`/HTTP 填充），可注册覆盖 |
| `IEntityDiffLogWriter` | 差异日志落库器，默认 `NullEntityDiffLogWriter`（零开销）；启用 `EnableDiffLog` 时须替换为真实实现 |

### SqlSugar 实体基类

本包提供带 `SugarColumn` 映射（snake_case 列名）的实体基类，供业务实体继承：

- `SugarEntity<TKey>` / `SugarEntityWithIdentity` — 基础实体
- `SugarCreationEntity` / `SugarModificationEntity` / `SugarDeletionEntity` / `SugarFullAuditedEntity<TKey>` — 审计实体（`Created_Time`/`Modified_Time`/`Is_Deleted`/`Deleted_Time` 等映射 + `Row_Version` 并发标识）
- `SugarMultiTenantEntity<TKey>` 及其审计变体 — 多租户实体（含 `Tenant_Id` 列，`0`=平台/全局模板）
- `SugarAggregateRoot` / `SugarMultiTenantAggregateRoot` — 聚合根基类

## 配置

配置节：`XiHan:Data:SqlSugarCore`（`XiHanSqlSugarCoreOptions.SectionName`）。

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `ConnectionConfigs` | `List<SqlSugarConnectionConfigOptions>` | `[]` | 连接配置集合 |
| `DefaultConfigId` | `string` | `"Default"` | 默认连接标识 |
| `TenantConfigIdPrefix` | `string` | `"Tenant_"` | 租户连接标识前缀（解析 `前缀+tenantId`） |
| `ThrowIfTenantConnectionNotFound` | `bool` | `false` | 租户有值但找不到连接时是否抛异常 |
| `ResolveConnectionConfigId` | `Func<long?,string?,string?>?` | `null` | 自定义租户连接解析委托 |
| `EnableTenantFilter` | `bool` | `true` | 启用租户行级过滤器 |
| `EnableSoftDeleteFilter` | `bool` | `true` | 启用软删除过滤器 |
| `EnableAutoUpdateQueryFilter` | `bool` | `true` | 更新时自动附加查询过滤条件 |
| `EnableAutoDeleteQueryFilter` | `bool` | `true` | 删除时自动附加查询过滤条件 |
| `GlobalFilters` | `Dictionary<Type,LambdaExpression>` | `{}` | 额外全局过滤器（仅代码注册，推荐 `AddGlobalFilter<TEntity>`） |
| `ConfigureDbAction` | `Action<ISqlSugarClient>?` | `null` | 客户端自定义配置钩子 |
| `EnableSqlLog` | `bool` | `false` | SQL 执行日志 |
| `EnableSqlErrorLog` | `bool` | `true` | SQL 异常日志 |
| `EnableSlowSqlLog` | `bool` | `true` | 慢 SQL 日志 |
| `SlowSqlThresholdMilliseconds` | `int` | `10000` | 慢 SQL 阈值（毫秒），纯观测用途、不影响语句执行 |
| `CommandTimeoutSeconds` | `int` | `300` | ADO 命令执行超时（秒）；0/负值不覆盖，沿用 SqlSugar 默认 300 秒 |
| `EnableDiffLog` | `bool` | `false` | 实体差异日志（需实现 `IEntityDiffLogWriter`/`IEntityAuditContextProvider`） |
| `EnableDbInitialization` | `bool` | `false` | 启动自动建库总开关 |
| `EnableTableInitialization` | `bool` | `false` | `CodeFirst` 建表 |
| `EnableDataSeeding` | `bool` | `false` | 执行种子数据 |

`SqlSugarConnectionConfigOptions` 字段：`ConfigId`（默认 `"Default"`）、`ConnectionString`、`DbType`（SqlSugar 枚举）、`IsAutoCloseConnection`（默认 `true`）、`InitKeyType`（默认 `InitKeyType.Attribute`）、`MoreSettings`、`DbLinkName`、`LanguageType`、`IndexSuffix`、`SlaveConnectionConfigs`（主从读写分离，详见下文）。

`DbType` 取 SqlSugar 的 `DbType` 枚举值，支持 **PostgreSQL / MySql / MariaDB** 等（`DbInitializer` 对 `MySql`/`MySqlConnector` 会自动把库字符集归一化为 utf8mb4）。

真实示例（取自 `XiHan.BasicApp.WebHost/appsettings.Development.json`）：

```json
{
  "XiHan": {
    "Data": {
      "SqlSugarCore": {
        "ConnectionConfigs": [
          {
            "ConfigId": "1",
            "ConnectionString": "Server=127.0.0.1;Database=XiHanBasicApp;Username=postgres;Password=postgres;TrustServerCertificate=true;",
            "DbType": "PostgreSQL",
            "IsAutoCloseConnection": true,
            "SlaveConnectionConfigs": []
          }
        ],
        "EnableSqlLog": false,
        "EnableSqlErrorLog": true,
        "EnableSlowSqlLog": true,
        "SlowSqlThresholdMilliseconds": 10000,
        "EnableDbInitialization": true,
        "EnableTableInitialization": true,
        "EnableDataSeeding": true
      }
    }
  }
}
```

## 主从读写分离

框架把 SqlSugar 的原生主从能力**完整放出来**，遵循「想改就改、不改吃默认」：SqlSugar 自动把 `SELECT` 按权重路由到从库、`INSERT/UPDATE/DELETE` 与事务走主库，无需业务感知。配置有两个通道。

### 通道 A：appsettings 声明从库（够用即可）

在连接里填 `SlaveConnectionConfigs` 即可让读走从库：

```json
{
  "ConfigId": "1",
  "ConnectionString": "主库连接串",
  "DbType": "PostgreSQL",
  "SlaveConnectionConfigs": [
    { "ConnectionString": "从库1连接串" },
    { "ConnectionString": "从库2连接串" }
  ]
}
```

::: warning HitRate 无法经 appsettings 设置（重要）
SqlSugar 的 `SlaveConnectionConfig.HitRate`（读权重）是**字段**而非属性，.NET 配置绑定器只绑属性、不绑字段，所以 appsettings 里写的 `HitRate` **绑不上、恒为 0**，会导致从库权重为 0 而**永远收不到读流量**。

框架已对此兜底：构建时把 `HitRate <= 0` 的从库**归一化**为 `DefaultSlaveHitRate`（默认 10），保证 appsettings 声明的从库能真正分担读。**要给从库设置差异化权重，请用下面的通道 B。**
:::

### 通道 B：代码钩子完整定制原生配置

`XiHanSqlSugarCoreOptions.ConfigureConnectionConfigs` 在 `SqlSugarScope` 构建前，把已填好框架默认值的**原生** `List<ConnectionConfig>` 交给你，任何原生能力都能改——差异化 `HitRate`、`ConfigureExternalServices`（如可空列处理）、自定义主从探活等：

```csharp
services.Configure<XiHanSqlSugarCoreOptions>(options =>
{
    options.ConfigureConnectionConfigs = configs =>
    {
        foreach (var config in configs.Where(c => Equals(c.ConfigId, "1")))
        {
            // HitRate 只能在代码里设（字段赋值不受配置绑定限制）
            config.SlaveConnectionConfigs =
            [
                new SlaveConnectionConfig { HitRate = 30, ConnectionString = "从库1连接串" },
                new SlaveConnectionConfig { HitRate = 10, ConnectionString = "从库2连接串" }
            ];
        }
    };
});
```

该钩子对运行时动态注册的租户连接同样生效（以单元素列表触发，请按 `ConfigId` 分支处理并保证幂等）。库隔离租户也可在 `SqlSugarTenantConnection.SlaveConnectionConfigs` 直接带从库。

> 追加 `DataExecuting` 逻辑请用 `AppendDataExecuting`，**切勿**在 `ConfigureDbAction` 里直接给 `Aop.DataExecuting` 赋值——该事件是单次赋值，直接赋值会整体冲掉框架的雪花主键/审计/租户注入。核心注入焊死、只许追加。

### 可选：从库健康探针（默认关闭）

打开 `EnableSlaveHealthCheck` 后，框架后台周期性探测从库连通性：不可用的从库自动摘除读权重（`HitRate=0`），恢复后按 `SlaveFailureCooldownSeconds` 冷却窗口回填原始权重。默认关闭、零成本；仅覆盖 appsettings 静态连接，运行时租户连接不在探测范围。

| 选项 | 默认 | 说明 |
| --- | --- | --- |
| `DefaultSlaveHitRate` | `10` | 通道 A 从库权重归一化默认值 |
| `EnableSlaveHealthCheck` | `false` | 是否启用从库健康探针 |
| `SlaveHealthCheckIntervalSeconds` | `30` | 探测周期（秒） |
| `SlaveFailureCooldownSeconds` | `120` | 故障冷却窗口（秒），避免抖动 |

## 使用示例

### 1. 应用服务里注入仓储读写

仓储接口在 `XiHan.Framework.Domain.Repositories` 中定义，本包提供实现，直接注入即可：

```csharp
public class ProductService
{
    private readonly IRepositoryBase<Product, long> _repository;

    public ProductService(IRepositoryBase<Product, long> repository)
    {
        _repository = repository;
    }

    public async Task<Product> CreateAsync(Product product)
    {
        // 雪花主键、租户 Id、创建审计字段由 DataExecuting AOP 自动注入
        return await _repository.AddAsync(product);
    }

    public async Task<Product?> GetAsync(long id)
    {
        // 自动附加当前租户过滤 + 软删除过滤
        return await _repository.GetByIdAsync(id);
    }

    public async Task<PageResultDtoBase<Product>> QueryAsync(int pageIndex, int pageSize, string keyword)
    {
        return await _repository.GetPagedAsync(
            pageIndex, pageSize,
            p => p.Name.Contains(keyword),
            p => p.CreatedTime, isAscending: false);
    }
}
```

### 2. 软删除仓储

```csharp
public class ArticleService(ISoftDeleteRepositoryBase<Article, long> repo)
{
    public Task RemoveAsync(long id) => repo.SoftDeleteAsync(id);       // 置 IsDeleted=true
    public Task RestoreAsync(long id) => repo.RestoreAsync(id);         // 恢复
    public Task<IReadOnlyList<Article>> RecycleBinAsync()               // 已删列表（跳过软删过滤）
        => repo.GetDeletedAsync();
}
```

### 3. 注册种子数据

```csharp
public class RoleSeeder : DataSeederBase
{
    public RoleSeeder(ISqlSugarClientResolver r, ILogger<RoleSeeder> l, IServiceProvider sp)
        : base(r, l, sp) { }

    public override int Order => 100;
    public override string Name => "系统角色";

    protected override async Task SeedInternalAsync()
    {
        if (await HasDataAsync<Role>(x => x.Code == "admin")) return;
        await BulkInsertAsync(new List<Role> { new() { Code = "admin", Name = "管理员" } });
    }
}

// 模块 ConfigureServices 中注册（须配合 EnableDataSeeding=true）
services.AddDataSeeder<RoleSeeder>();
// 或批量：services.AddDataSeeders(typeof(RoleSeeder), typeof(MenuSeeder));
```

## 扩展点 / 自定义

所有核心服务用 `TryAdd*` 注册，业务层可注册自定义实现覆盖：

- **审计上下文**：注册自定义 `IEntityAuditContextProvider` 替换默认从 `ICurrentUser`/HTTP 的填充逻辑。
- **差异日志落库**：启用 `EnableDiffLog` 后注册真实 `IEntityDiffLogWriter`（默认 `Null` 不产生记录）。
- **租户库隔离**：注册 `ISqlSugarTenantConnectionProvider`，`Resolve` 返回租户独立连接描述符即启用库级隔离；返回 `null` 退化为 `ConfigId`（行/字段）隔离。
- **自定义连接解析**：设置 `Options.ResolveConnectionConfigId` 委托，或用 `TenantConfigIdPrefix`/`DefaultConfigId` 约定映射。
- **额外全局过滤器**：调用 `Options.AddGlobalFilter<TEntity>(expr)` 注册表达式树（须可翻译为 SQL 的成员访问/标量比较，勿调用外部方法；构建期 fail-fast 校验，非法表达式启动即报错）。
- **客户端级配置**：`Options.ConfigureDbAction` 钩子拿到 `ISqlSugarClient` 做自定义装配。

## 注意事项与最佳实践

- **默认不建库**：`EnableDbInitialization`/`EnableTableInitialization`/`EnableDataSeeding` 默认全 `false`；本机/首次部署需在配置里显式打开。表已存在会跳过（不做迁移，遵循"重建库、无向后兼容"约定）。
- **事务靠工作单元**：仓储内不开事务；需要多写原子提交时给应用服务方法打 `[UnitOfWork(isTransactional: true)]`，`ISqlSugarClientResolver` 会自动把连接登记进 UoW 事务。见 [XiHan.Framework.Uow](./uow)。
- **越租户写会被拒**：`Update/Delete` 前的可见性预读若读不到实体（不在当前租户/已软删），抛 `InvalidOperationException`；这是安全边界，不是 bug。
- **跨租户/含软删查询**：仓储内部提供 `CreateNoTenantQueryable()`（清租户过滤）/`CreateWithDeletedQueryable()`（清软删过滤），仅用于平台运维/审计恢复且须自行做权限校验。
- **`TenantId=0` 是全局模板**：多租户实体的 `TenantId` 非空，`0` 表示平台/全局记录，对所有租户可见（配合 `UNIQUE(TenantId, Code)` 复合唯一索引对全局记录生效）。
- **审计字段勿手填**：`TenantId`、创建/修改/删除时间与操作人由 `DataExecuting` AOP 注入，业务侧手填会被覆盖或引发不一致。
- **雪花主键**：主键由 `IDistributedIdGenerator<long>` 通过 `StaticConfig.CustomSnowFlakeFunc` 全局生成；实体基类的 `BasicId` 映射为非自增主键。
- **慢 SQL 阈值与命令超时相互独立**：`SlowSqlThresholdMilliseconds` 只用于慢 SQL 日志判定；命令执行超时由 `CommandTimeoutSeconds` 单独配置（默认 300s），且应明显大于慢 SQL 阈值，否则慢 SQL 会先被驱动杀掉、慢日志记不到。

## 依赖模块

- [XiHan.Framework.Domain](./domain)（仓储接口 / 实体与聚合根抽象来源）
- [XiHan.Framework.Uow](./uow)（事务边界接管，`SqlSugarTransactionApi` 接入）
- [XiHan.Framework.MultiTenancy](./multitenancy)（`ICurrentTenant`/`ICurrentTenantAccessor` 租户上下文）
- [XiHan.Framework.Security](./security)（`ICurrentUser` 审计上下文）
- [XiHan.Framework.DistributedIds](./distributed-ids)（雪花 ID 生成器）
- [XiHan.Framework.Auditing](./auditing)（`IEntityAuditContextProvider`/`IEntityDiffLogWriter` 默认实现来源）
- [XiHan.Framework.Core](./core)（模块化基础设施，含链路追踪 `XiHanActivitySources`）
- 第三方核心：**SqlSugarCore** `5.1.4.216`

## 相关模块

- [XiHan.Framework.Uow](./uow)
- [XiHan.Framework.Domain](./domain)
- [XiHan.Framework.MultiTenancy](./multitenancy)
- [XiHan.Framework.Auditing](./auditing)
- [XiHan.Framework.Caching](./caching)
