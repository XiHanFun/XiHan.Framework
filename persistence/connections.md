来源：https://framework.docs.xihanfun.com/persistence/connections

# 连接与运行时数据源

这一页讲持久化的「连到哪个库」：默认连接怎样经绑定 `db` 配置、命名连接给谁用、运行时按应用自己的记录连外部库的 `DataSourceRegistry`、读库结构的 `ISchemaReader` 与实体目录 `IEntityCatalog`。读完它，你能为应用配置主库、把一部分平台数据放到另一个库，并在代码生成这类场景里连上用户登记的外部数据源。前置知识是[持久化与 Provider](/persistence/overview)与[资源绑定](/configuration/bindings)。

## 为什么需要

一个应用往往不止一个库：业务库、要在业务事务进行中另行提交的租约库、按租户拆开的分片库，还有代码生成或零代码运行时要去读的用户自己的库。它们的生命周期与事务语义各不相同：默认连接随调用开事务；命名连接要能独立提交；运行时数据源完全由应用的记录决定、不参与任何工作单元。框架把这几种连接分开管，各有明确的配置位置、落点规则与失败方式，不让一类连接的事务意外牵动另一类。

## 连接的几种来源

| 连接 | 由谁决定 | 参与工作单元 | 适用 |
| --- | --- | --- | --- |
| 默认连接 `default` | 绑定 `db`（配置 `XiHan:Bindings:db`）交给 Provider 的配置回调 | 是，每次调用的事务开在它上面 | 业务数据 |
| 命名连接 | `XiHan:Persistence:<Provider>:Connections:<name>`，实体用 `AddRepository<TEntity, TKey>(connection)` 登记到它 | 是；可以单独绑定一个工作单元 | 要与业务事务互不等待的平台数据 |
| 分片连接 `shard` | 装配 `multitenancy.sharding@1` 后由租户目录给出，带分片的调用交给同一个回调 | 是，与默认连接同一调用一起提交或回滚 | 按租户分库 |
| 运行时数据源 | 应用的记录（名字、数据库类型、连接串）经 `DataSourceRegistry.GetOrAddAsync` 登记 | 否，不进作用域客户端、不参与实体落点 | 代码生成导入的外部库、零代码运行时查的外部表 |

按租户分片选连接的细节见[租户分片与分库](/multi-tenancy/sharding)，本页只讲其余三种。

## 默认连接

EF Core 与 SqlSugar Provider 声明一个可选的连接绑定 `db`（类型 `connection:relational`），宿主从配置节 `XiHan:Bindings:db` 解析它，交给特性的配置回调：

```json
{
  "XiHan": {
    "Bindings": {
      "db": "Data Source=app.db"
    }
  }
}
```

回调的第一个参数是 `Maybe<BindingValue>`：绑定缺失时为空，由回调自己决定连接（`binding.ValueOrThrow()` 在缺失时抛出）。两个 Provider 的回调签名：

| Provider | 回调 | 回调里做什么 |
| --- | --- | --- |
| EF Core | `Action<Maybe<BindingValue>, DbContextOptionsBuilder>` | `options.UseSqlite(binding.ValueOrThrow().Value)` 这类选择驱动与连接串 |
| SqlSugar | `Action<Maybe<BindingValue>, ConnectionConfig>` | 设置 `config.DbType` 与 `config.ConnectionString`；`ConfigId` 由特性定 |

装配了 `multitenancy.sharding@1` 时，带分片的调用把租户的连接作为绑定交给同一个回调，主值是租户连接串，属性含 `tenant`、`provider` 与透传属性。回调里按 `ConfigId`（SqlSugar）或绑定属性区分是哪个连接。绑定的通用机制、从环境变量与密钥源取值见[资源绑定](/configuration/bindings)。

## 最小示例

一个 SqlSugar 应用：默认连接走绑定 `db`，再声明一个命名连接 `leases` 给工作流的租约表（SQLite 只有一个写者，租约表放到另一个库文件才能在业务事务进行中另行提交）：

```json
{
  "XiHan": {
    "Bindings": { "db": "Data Source=app.db" },
    "Persistence": {
      "SqlSugar": {
        "Connections": { "leases": "Data Source=app.leases.db" }
      }
    },
    "Workflow": { "Database": { "LockConnection": "leases" } }
  }
}
```

```csharp
using SqlSugar;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.SqlSugar;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

// 应用自己的实体也可以登记到命名连接
builder.Services.AddRepository<SyncCursor, string>(connection: "leases");
builder.AddXiHan(x => x
    .UseStandardProfile()
    .UseFeature(new SqlSugarPersistenceFeature(static (binding, config) =>
    {
        // 回调对默认连接与每个命名连接各调用一次，绑定主值是各自的连接串
        config.DbType = DbType.Sqlite;
        config.ConnectionString = binding.ValueOrThrow().Value;
    })));

var app = builder.Build();
app.UseXiHan();
app.Run();
```

运行后每个作用域的客户端带两个连接：`default` 指向 `app.db`，`leases` 指向 `app.leases.db`。`SyncCursor` 的读写与建表落在 `leases`；业务调用不碰它，那个库文件就不被业务事务锁住。管道外要单独写 `leases` 时开绑定它的工作单元：

```csharp
await using var scope = services.CreateAsyncScope();
var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
// 事务只开在 leases 上，不牵动默认连接
await unitOfWork.BeginAsync(new UnitOfWorkOptions(Connection: "leases"), ct);
// …经 IRepository<SyncCursor, string> 读写，然后 CommitAsync
```

## 命名连接

命名连接解决的是「一部分平台数据要放在另一个库里、并在业务事务进行中另行提交」，用两样东西：

- 实体登记到命名连接：`services.AddRepository<TEntity, TKey>(connection: "leases")`。它在那个连接上读写、建表。`connection` 为 `null` 是 Provider 的缺省落点，非空不能是空白；同一实体登记到两个不同的连接（含一次为空、一次非空）抛 `ArgumentException`。`RepositoryRegistrations.ConnectionOf(type)` 与 `RegisteredEntity.Connection` 取登记的连接。
- 绑定连接的工作单元：`new UnitOfWorkOptions(Connection: "leases")`，工作单元只用这个连接，事务只开在它上面，不牵动默认连接；操作落在别处的实体抛 `PERSISTENCE_VALIDATION_004`；连接名空白抛 `ArgumentException`。

名字合 `^[a-z0-9][a-z0-9-]{0,63}$`，不能是 `default` 或 `shard`；值不能空白。不合法时装配即抛 `ArgumentException`（消息只带名字，不带连接串）。

三个 Provider 的支持程度不同：

| Provider | 声明位置 | 普通工作单元里的命名连接实体 | 绑定连接的工作单元 |
| --- | --- | --- | --- |
| SqlSugar | `XiHan:Persistence:SqlSugar:Connections:<name>`（方式一）；方式二由应用把同名 `ConfigId` 的连接加进客户端 | 照常参与整体事务，落在那个连接上，顺序提交 | 支持；在那个连接上直接开事务 |
| EF Core | `XiHan:Persistence:EntityFrameworkCore:Connections:<name>` | 不支持：取用其仓储抛 `PERSISTENCE_UNAVAILABLE_008`（一个 `DbContext` 一个连接） | 支持；本作用域的 `DbContext` 建在那个连接上 |
| 内存 | — | 不支持：登记到命名连接在装配时抛 `PERSISTENCE_UNAVAILABLE_008` | 不支持：`BeginAsync` 抛 `PERSISTENCE_UNAVAILABLE_008` |

EF Core 不做跨上下文顺序提交的多上下文工作单元，要在业务事务里一起读写命名连接上的实体，用 SqlSugar。两个 Provider 的细节见 [EF Core Provider](/persistence/entity-framework-core) 与 [SqlSugar Provider](/persistence/sqlsugar)；工作流租约表的配置见 [Workflow.Database](/packages/workflow-database)。

## 运行时数据源

应用在运行时按自己的记录（名字、数据库类型、连接串，通常存在应用自己的库里、口令加密保存）连外部库，比如代码生成要导入的外部数据源、零代码运行时要查的外部表。这些连接由单例 `DataSourceRegistry` 管，与上面的约定连接互不相干：不进作用域客户端、不参与实体落点、不进工作单元。

```csharp
using SqlSugar;
using XiHan.Framework.Persistence.Abstractions.DataSources;
using XiHan.Framework.Persistence.SqlSugar;
using XiHan.Framework.Primitives;

namespace SimpleApp.CodeGen;

public sealed class CodeGenDataSources(DataSourceRegistry registry)
{
    // 记录里读出的定义：名字用记录的标识，连接串里放解密后的口令
    public ValueTask<Result> TestAsync(DataSourceDefinition definition, CancellationToken ct)
        => registry.TestAsync(definition, ct);                      // 走一次数据库往返，不登记

    public async Task<Result<ISqlSugarClient>> ClientAsync(DataSourceDefinition definition, CancellationToken ct)
        => (await registry.GetOrAddAsync(definition, ct)).Map(static s => s.GetSqlSugarClient());

    // 记录改了、停用或删了：显式失效，旧连接串不再被本进程使用
    public ValueTask<IReadOnlyList<bool>> InvalidateAsync(IReadOnlyList<string> names, CancellationToken ct)
        => registry.RemoveAsync(names, ct);
}
```

`DataSourceDefinition(name, databaseType, connectionString)` 是定义：名字合 `^[a-z0-9][a-z0-9-]{0,63}$`，`default` 保留给缺省连接；数据库类型不区分大小写，`DatabaseTypes` 列出常用的（`sqlite`、`postgresql`、`mysql`、`sqlserver`、`oracle`），认得哪些由 Provider 决定；`ToString` 不含连接串，`Redact(text)` 把文本里的连接串脱敏。

| 项 | 规则 |
| --- | --- |
| 取用 | `GetOrAddAsync(definition)`：同名同定义复用；定义变了建新实例替换旧的并释放旧的。建实例不连库，连不上的库照样登记，使用时报告 |
| 失效 | 显式 `RemoveAsync(names)`（批量，逐项返回是否登记过）加定义比对，不用配置变化令牌。注册表是进程内的：每次使用都以记录里当前的定义调 `GetOrAddAsync`，别的实例改了记录，本实例下一次取用即替换；只按名字 `GetAsync` 看不到别的实例上的修改 |
| 容量 | `XiHan:Persistence:DataSources:MaxCount`（缺省 64），满了再登记新名字返回 `PERSISTENCE_UNAVAILABLE_007`，不淘汰已有的；替换同名的不占名额 |
| 测试连接 | `TestAsync(definition)`：建临时实例、走一次数据库往返、释放；不登记、不占名额。单次上限是 `HealthProbeIntervalSeconds`（缺省 15 秒），冷启动慢的库要调大；调用方取消照常抛出 |
| 健康 | 每个数据源一条 `persistence.datasource.<name>`，后台按间隔走一次数据库往返；尚未探过 Started，成功 Ready，连不上或超时 Degraded（`PERSISTENCE_UNAVAILABLE_006`），不影响 readiness |
| 机密 | 连接串不进日志、错误与健康详情：错误原因是驱动异常的消息链，整串连接串与口令类键（`password`、`pwd`、`secret`、`token`、`key`）的值换成 `***`；主机、端口、库名、用户名保留，排查要用 |
| Provider | SqlSugar 支持：数据库类型是 SqlSugar `DbType` 的名字，客户端是每个数据源一个的 `SqlSugarScope`，不施加实体约定、不挂过滤器与 AOP、PostgreSQL 不自动转小写、Oracle 不自动转大写。EF Core 与内存实现不支持，取用与测试连接返回 `PERSISTENCE_UNAVAILABLE_008` |

| 错误码 | 何时出现 |
| --- | --- |
| `PERSISTENCE_VALIDATION_006` | 数据库类型 Provider 不认识、连接串格式错；同名的已登记实例不受影响 |
| `PERSISTENCE_NOTFOUND_002` | `GetAsync` 的名字没有在本进程登记 |
| `PERSISTENCE_UNAVAILABLE_006` | 连不上或读取失败（测试连接、健康探测、库结构读取），参数 `name`、`reason`（已脱敏） |
| `PERSISTENCE_UNAVAILABLE_007` | 登记已满 |
| `PERSISTENCE_UNAVAILABLE_008` | 当前 Provider 不支持运行时数据源 |

## 库结构读取与实体目录

代码生成要列出库里的表、读出列结构（导入与同步结构），还要认出哪些表由已登记的实体建出（沿用已有实体）。`ISchemaReader` 与 `IEntityCatalog` 提供这两样：

```csharp
using XiHan.Framework.Persistence.Abstractions.DataSources;
using XiHan.Framework.Persistence.Abstractions.Schema;
using XiHan.Framework.Primitives;

namespace SimpleApp.CodeGen;

public sealed class CodeGenImporter(ISchemaReader schema, IEntityCatalog catalog)
{
    // dataSource 为 null 读应用的缺省连接，否则读运行时数据源（先按定义登记再读）
    public Task<Result<TablePage>> ListAsync(DataSourceDefinition? dataSource, string? keyword, int skip, int take, CancellationToken ct)
        => schema.ListTablesAsync(dataSource, new TableListQuery { Keyword = keyword, Skip = skip, Take = take }, ct);

    public Task<Result<IReadOnlyList<Result<TableSchema>>>> ReadAsync(DataSourceDefinition? dataSource, IReadOnlyList<string> tables, CancellationToken ct)
        => schema.GetTablesAsync(dataSource, tables, ct);           // 一次最多 100 张，逐项结果

    public async Task<EntityMapping?> ExistingEntityAsync(string table, CancellationToken ct)
        => (await catalog.ListAsync(ct)).FirstOrDefault(m => string.Equals(m.Table, table, StringComparison.OrdinalIgnoreCase));
}
```

| 项 | 规则 |
| --- | --- |
| 列表 | 按名字排序；`Keyword` 在名字与注释里不区分大小写地包含匹配；`Skip` / `Take` 分页（`Skip` ≥ 0，`Take` 1～1000），返回过滤后的总数；库里的表一次读出、在内存里分页。分页越界以结果返回（HTTP 400）：`Skip` 为负或 `Take` 小于 1 是 `PERSISTENCE_VALIDATION_007`，`Take` 超过 1000 是 `PERSISTENCE_VALIDATION_005`，不连库 |
| 读结构 | 一次最多 `SchemaLimits.MaxTablesPerRead`（100）张，超过整批 `PERSISTENCE_VALIDATION_005`；逐项结果与入参一一对应，找不到的表该项 `PERSISTENCE_NOTFOUND_003`；连接或读取失败整批 `PERSISTENCE_UNAVAILABLE_006`；空集合不连库；`GetTableAsync` 是单项特例 |
| 列 | 类型名（不含长度与精度）、序号、主键、自增、可空、长度（只给字符与二进制类型的声明长度，整数、日期、`text` / `nvarchar(max)` / `CLOB` 为 null）、精度与小数位（定点数族）、注释、缺省值（数据库报告的原文）；主键列一律不可空 |
| 分表 | 已登记的分表实体按表名模板把 `Order_20261101` 这类物理表折叠成逻辑表 `Order`，`PhysicalTables` 新到旧，列取最新的物理表 |
| 实体目录 | 每个登记的实体一条：表名（映射声明的写法，分表是逻辑名）、列映射、`Protocols`（聚合根、创建 / 修改时间、并发戳、软删除、多租户）、`[EntityIndex]` 推出的索引；PostgreSQL 的物理名可能是小写，对照时不区分大小写 |
| 缺省连接 | 另开一个不在事务里的连接读，PostgreSQL 系语句失败不会中止调用的事务 |
| SQLite 的限制 | 没有表与列注释；`INTEGER PRIMARY KEY` 报告可空（按主键改为不可空）；自增只认 `AUTOINCREMENT` |
| 实测过的数据库 | SQLite、PostgreSQL、MySQL、SQL Server、Oracle（12c 及以上；读连接用户自己的表，不加引号的名字是大写）。PostgreSQL 衍生库没有验证 |
| 不支持的数据库 | Oracle 11g 及更早：读列以 `PERSISTENCE_UNAVAILABLE_006` 失败（数据字典没有 `IDENTITY_COLUMN`）。达梦、OceanBase Oracle 模式等 Oracle 兼容库走 SqlSugar 原来的读列，读到的结构不作保证 |
| Provider | SqlSugar 两者都支持。EF Core 的实体目录取 `DbContext` 模型、读结构不支持；内存实现的实体目录表名取类型名、读结构不支持；不支持的返回 `PERSISTENCE_UNAVAILABLE_008` |

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Bindings:db` | 字符串 | — | 默认连接的绑定主值 |
| `XiHan:Persistence:EntityFrameworkCore:Connections:<name>` | 字符串 | — | EF Core 的命名连接 |
| `XiHan:Persistence:SqlSugar:Connections:<name>` | 字符串 | — | SqlSugar 方式一的命名连接 |
| `XiHan:Persistence:DataSources:MaxCount` | 1–1024 | `64` | 运行时数据源的登记数上限 |
| `XiHan:Persistence:DataSources:HealthProbeIntervalSeconds` | 正数 | `15` | 运行时数据源健康项的重探间隔，也是单次探测与 `TestAsync` 的超时 |

## 常见问题

### 连接串放在哪里最合适

放在绑定 `db` 里，生产环境用环境变量或密钥源覆盖 `XiHan:Bindings:db`，不写进仓库里的 `appsettings.json`。命名连接同理。运行时数据源的连接串在应用自己的记录里，口令加密保存、读出时解密再构造 `DataSourceDefinition`。

### 命名连接与运行时数据源怎么选

命名连接是部署期已知、结构由应用管理、要进工作单元的库（租约表、审计库）。运行时数据源是用户在界面上登记的外部库，数量不定、结构由对方管理、只读或只做直连查询，不进工作单元。

### 为什么 EF Core 的命名连接实体不能在普通工作单元里用

EF Core 一个 `DbContext` 一个连接，普通工作单元的 `DbContext` 建在默认连接上，命名连接上的实体不在它的模型连接里。框架不做多上下文顺序提交。要在业务事务里一起读写多个库用 SqlSugar。

### 运行时数据源的连接串改了，怎样让旧连接失效

每次使用都以记录里当前的定义调 `GetOrAddAsync`：定义变了就建新实例替换旧的并释放旧的，不用额外动作。记录停用或删除时调 `RemoveAsync(names)` 显式失效。多实例部署下别的实例改了记录，本实例在下一次 `GetOrAddAsync` 时替换，所以不要只按名字 `GetAsync`。

### 读到的库结构里缺省值格式不一致

缺省值是数据库报告的原文：MySQL 是 `x`、SQL Server 是 `('x')`、Oracle 是 `'x'`。代码生成按目标数据库解析，框架不统一格式。

## 相关包与下一步

- 相关包：[Persistence.Abstractions](/packages/persistence-abstractions)、[Persistence.EntityFrameworkCore](/packages/persistence-entityframeworkcore)、[Persistence.SqlSugar](/packages/persistence-sqlsugar)、[Persistence.InMemory](/packages/persistence-inmemory)、[Workflow.Database](/packages/workflow-database)
- 规范：RFC-0005《领域协议与持久化》
- 下一页：[标识生成](/persistence/identifiers)，讲 UUID v7 与雪花标识
