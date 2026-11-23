# XiHan.Framework.Persistence.SqlSugar

`persistence.relational@1` 的 SqlSugar Provider：以作用域内的 `ISqlSugarClient` 实现仓储与事务型工作单元，实体不继承 SqlSugar 基类、不必标注 `[SugarColumn]`，映射由协议约定推出。面向已有 SqlSugar 代码的应用，并支持一次调用写多个库、按时间分表与差异日志。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Persistence.SqlSugar` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `persistence.relational@1`（`role: provider`）；依赖 `kernel.hosting@1`，可选依赖 `multitenancy.sharding@1`；绑定 `db`（`connection:relational`，可选） |
| 特性入口 | `SqlSugarPersistenceFeature` |
| 配置节 | `XiHan:Persistence:SqlSugar` |
| 依赖 | `XiHan.Framework.Persistence.Abstractions`、`XiHan.Framework.MultiTenancy.Abstractions`、`SqlSugarCore`（自带各数据库驱动）、`Microsoft.Data.Sqlite` |
| AOT | 不兼容（SqlSugar 的实体映射与表达式翻译基于运行时反射） |
| 稳定性 | Stable；数据源（`SqlSugarDataSource`、`SqlSugarDataSourceExtensions`）为 Preview，读写分离（`SqlSugarReplicaOptions` 与 `Replicas`）与差异日志（`SqlSugarDiffLogData` 与两处 `DiffLog`）为 Experimental |

## 概述

客户端有两种来源。方式一由特性按配置回调创建作用域内的 `SqlSugarClient`，自动施加 `SqlSugarConventions`；方式二由应用自己注册 `ISqlSugarClient`（`SqlSugarScope`、多库等），应用在创建客户端前对每个 `ConnectionConfig` 调用 `SqlSugarConventions.Apply`。两种方式下仓储、工作单元与查询语义相同。

SqlSugar 没有变更跟踪，仓储的每次写入立即执行。工作单元在第一次写入时经 `AsTenant()` 在默认连接与已经取用过的连接上开事务，此后首次取用的连接再补开，只读调用不开事务；提交逐连接提交，回滚逐连接回滚。开没开由 `HasTransaction` 报告（ADR-0109）：第一次 `EnsureTransactionAsync`（仓储每次写入前都经它）开事务即为真，多连接时一个值，提交、回滚或作用域释放时复位；绑定命名连接的工作单元只报告那个连接。更新与删除先在事务内加行锁取回当前行，比对并发戳后把调用方的实例整行写入；批量方法用一次主键 `IN` 查询取回目标行，逐项校验后一次多行插入、更新或删除。

软删除与租户过滤以表达式显式施加在每个查询上，不用 SqlSugar 的全局过滤器，不会与应用自己的过滤器互相覆盖。规约在交给 SqlSugar 前先改写（ADR-0035 与其修订）：可空值类型与 `null` 的比较换成 `SqlFunc.EqualsNull` / `HasValue`（字面量 null、求值为 null 的变量或静态成员、左右互换都算，与非 null 值的比较不动），逻辑非下推到叶子，结果在 SQL 三值逻辑下不变；改写的原因与直接用客户端时的写法见 [SqlSugar Provider](/persistence/sqlsugar#可空值类型判空)。

字符串的 `Contains`、`StartsWith`、`EndsWith` 按字面匹配（ADR-0098）：`SqlSugarConventions.Apply` 以同名的外部函数接管这三个方法的翻译，所以不只是仓储的规约，直接用施加了约定的客户端写的查询、删除条件、投影与子查询也一样；集合的 `Contains`（`IN`）不受影响。各数据库上的差异见下文「协议约定」的「字符串匹配」一行。

## 何时使用

- 已有 SqlSugar 代码的应用：实体不推倒重来，原生查询继续用自己的客户端，逐步收进仓储。
- 一次调用既写平台表又写租户分片库，或需要 `[Tenant]` 把实体固定到某个库。
- 按月 / 按日分表的日志类实体。
- 需要 SqlSugar 差异日志取写入前后值。

新项目、用 EF Core 迁移管理表结构的应用见 [Persistence.EntityFrameworkCore](./persistence-entityframeworkcore)。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Persistence.SqlSugar --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[环境与安装](/getting-started/installation)。本包依赖 `SqlSugarCore`；应用改用 `SqlSugarCoreNoDrive` 加单个驱动时要整体替换，两者不能并存。

本包还显式依赖 `Microsoft.Data.Sqlite`（ADR-0097）。`SqlSugarCore` 声明的下限版本带着连接池激活的竞态（dotnet/efcore#39008），NuGet 按下限解析就会用到它：SQLite 上并发打开连接时两个连接可能共用同一个原生连接，报 "cannot start a transaction within a transaction"，清池时正在打开的连接被释放、报 `ObjectDisposedException`。有了这条依赖，引用本包的应用解析到修复后的版本，不用自己钉版本；项目里直接引用了更低版本的，NuGet 报降级 `NU1605`，删掉那条引用或抬到本包要求的版本。

装配有两种方式，代码见 [SqlSugar Provider](/persistence/sqlsugar#最小示例)：

- 方式一：`new SqlSugarPersistenceFeature((binding, config) => …)`，特性按回调创建客户端。回调设置数据库类型与连接串，之后特性施加协议约定；默认连接的 `ConfigId` 由特性定为 `default`，回调里改了也以特性为准。
- 方式二：应用自己注册 `ISqlSugarClient`，每个 `ConnectionConfig` 先经 `SqlSugarConventions.Apply`、默认连接用 `SqlSugarConnections.Default` 命名，再以无参构造的 `SqlSugarPersistenceFeature` 装配。

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Persistence:SqlSugar:InitTables` | 布尔 | `false` | 启动时按已登记的实体建缺表、加缺列；只宜在开发与测试开启 |
| `XiHan:Persistence:SqlSugar:HealthProbeIntervalSeconds` | 正数 | `15` | 健康项的重探间隔，也是单次探测的超时 |
| `XiHan:Bindings:db` | 字符串 | — | 方式一的连接绑定主值；绑定可选，缺失时回调自行决定连接 |
| `XiHan:Persistence:SqlSugar:Connections:<name>` | 字符串 | — | 方式一的命名连接，值是连接串（见下文「命名连接」）；方式二写了这一节装配即失败 |
| `XiHan:Persistence:SqlSugar:CommandTimeoutSeconds` | 正整数 | `300` | 命令超时（秒），设在特性建的客户端的每个连接上；不是正整数即启动失败，0 不接受（ADO.NET 的 0 是无限等待）。方式二时这个键不起作用，配了与缺省不同的值也启动失败，要在自己的客户端上设 `Ado.CommandTimeOut`。个别长任务在调用点用 `Ado.CommandTimeOut` 临时调大 |
| `XiHan:Persistence:SqlSugar:Replicas:DefaultHitRate` | 正整数 | `10` | 权重不大于 0 的从库归一化成的权重 |
| `XiHan:Persistence:SqlSugar:Replicas:HealthCheck` | 布尔 | `true` | 是否周期探测从库；配了从库才有探测 |
| `XiHan:Persistence:SqlSugar:Replicas:HealthCheckIntervalSeconds` | 正数 | `30` | 从库的探测间隔 |
| `XiHan:Persistence:SqlSugar:Replicas:FailureCooldownSeconds` | 非负数 | `120` | 从库最后一次失败之后，过了这段时间并且探通才恢复读流量 |

差异日志开关 `DiffLog` 不从配置读取，在特性上设置。运行时数据源的 `XiHan:Persistence:DataSources:*` 见 [Persistence.Abstractions](./persistence-abstractions#配置)；配置 JSON 的例子见 [SqlSugar Provider](/persistence/sqlsugar#最小示例)。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `SqlSugarPersistenceFeature` | 特性入口；无参构造用应用注册的客户端，带回调的构造由特性创建客户端；`DiffLog` 选出开启差异日志的实体；常量 `ConnectionBinding`（`db`）与 `HealthName`（`persistence.sqlsugar.database`） |
| `SqlSugarPersistenceOptions` | 选项：`InitTables`、`HealthProbeInterval`、`CommandTimeout`（`TimeSpan`，缺省 `StandardCommandTimeout` 300 秒，配置键是 `CommandTimeoutSeconds`）、`Connections`（方式一的命名连接）、`DiffLog`；`FromConfiguration(configuration)` |
| `SqlSugarReplicaOptions` | 从库的权重与健康探测选项（`SqlSugarPersistenceOptions.Replicas`） |
| `SqlSugarConventions` | `Apply(config)` 在连接配置上登记协议约定（保留配置里已有的实体服务与表服务回调，先执行它们）；`Configure(property, column)` 对单个属性施加约定；`ConfigureIndexes(type, entityInfo)` 把实体上的 `[EntityIndex]` 登记为 SqlSugar 索引（软删除实体的部分唯一索引除外）；`InitTables(client, entityTypes)` 按约定建表并补建软删除实体的部分唯一索引，应用自己建表（租户独立库）用它 |
| `SqlSugarConnections` | 连接的 `ConfigId` 约定：`Default`（`default`）、`Shard`（`shard`） |
| `IEntityConnectionSelector`、`SqlSugarConnectionSelector` | 实体落在哪个连接；默认实现构造时收 `RepositoryRegistrations`，规则见 [SqlSugar Provider](/persistence/sqlsugar#多库落点) |
| `SqlSugarUnitOfWork` | 事务型工作单元；`Client` 是根连接，`ConnectionFor(Type)` 返回某种实体所用的连接；以 `UnitOfWorkOptions.Connection` 开启时只用那个命名连接 |
| `SqlSugarDiffLogData` | 差异日志的业务数据：`EntityType` 与仓储所在作用域的 `Services` |
| `SqlSugarDataSource`、`SqlSugarDataSourceExtensions` | 运行时数据源：`Client` 是每个数据源一个的 `SqlSugarScope`；`source.GetSqlSugarClient()` 从中立的 `IDataSource` 取它 |

## 使用示例

示例在教程页 [SqlSugar Provider](/persistence/sqlsugar)，按场景找：

| 场景 | 示例 |
| --- | --- |
| 两种创建客户端的方式、配置 JSON | [最小示例](/persistence/sqlsugar#最小示例) |
| 实体落在哪个连接、替换落点规则 | [多库落点](/persistence/sqlsugar#多库落点) |
| 按时间分表的实体 | [分表](/persistence/sqlsugar#分表) |
| 从库与只读工作单元 | [读写分离](/persistence/sqlsugar#读写分离) |
| 取写入前后值的回调 | [差异日志](/persistence/sqlsugar#差异日志) |
| 命名连接的配置、绑定连接的工作单元 | [连接与运行时数据源](/persistence/connections) |

教程里没有的两段代码补在这里。把审计类实体固定到 `audit` 库（客户端里要有 `ConfigId` 为 `audit` 的连接）：

```csharp
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using XiHan.Framework.Domain.Entities;

namespace SimpleApp.Auditing;

[Tenant("audit")]
public sealed class AccessLog : IEntity<Guid>, IHasCreationTime
{
    public Guid Id { get; init; }

    [StringLength(256)]
    public string Path { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
```

分表实体的查询用 `Within` 限定扫描的表，没有提示时扫全部分表（`LoginLog` 是教程里的分表实体，`loginLogs` 是注入的 `IRepository<LoginLog, Guid>`）：

```csharp
var october = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
// 两端都含：只扫 2026 年 10 月那张表；范围是扫描提示，不是过滤条件
var query = EntityQuery.All<LoginLog>().Within(october, october.AddMonths(1).AddTicks(-1));
var logs = await loginLogs.ListAsync(query, cancellationToken);
```

## 命名连接

一部分平台数据要放在另一个库里、并在业务事务进行中另行提交时（RFC-0005「命名连接」，ADR-0084），用两样东西：实体登记到命名连接（`AddRepository<TEntity, TKey>(connection)`），管道外的写入开绑定那个连接的工作单元（`UnitOfWorkOptions { Connection = name }`）。典型场景是 SQLite 上的工作流租约表——SQLite 只有一个写者，业务事务写过之后，同一个库文件上的另一个事务要等它结束（见 [Workflow.Database](./workflow-database#sqlite-上的租约表)）。配置与代码示例见[连接与运行时数据源](/persistence/connections)。

- 方式一在 `XiHan:Persistence:SqlSugar:Connections:<name>` 声明，值是连接串；回调对每个命名连接各调用一次：绑定的主值换成该连接串、属性沿用 `db` 绑定（`provider` 等），`config.ConfigId` 是连接名，回调据此区分。方式二由应用把同名 `ConfigId` 的连接加进自己注册的客户端，配置里的 `Connections` 节对它不适用，写了即装配失败。
- 名字合 `^[a-z0-9][a-z0-9-]{0,63}$`，不能是 `default` 或 `shard`；值不能空白。不合法时装配即抛 `ArgumentException`（消息只带名字，不带连接串）。
- 每个作用域的客户端都带上命名连接，排在默认连接之后，根连接仍是默认连接。工作单元首次写入时只在默认连接与已经取用过的连接上开事务，此后首次取用的连接再补开：业务调用不碰命名连接，那个库就不被业务事务锁住。
- 普通工作单元里，登记到命名连接的实体照常参与整体事务（同一调用触及的连接顺序提交）；`InitTables` 把它建在那个连接上。
- 绑定连接的工作单元只用这一个连接：操作落在别处的实体抛 `PERSISTENCE_VALIDATION_004`；连接不在客户端里，`BeginAsync` 抛出同码异常。

## 行为与限制

### 协议约定

`SqlSugarConventions.Apply` 在连接配置上登记的约定；列与索引的通用规则见[建表约定与索引](/persistence/schema)。

| 约定 | 内容 |
| --- | --- |
| 主键 | 名为 `Id` 的属性是主键；是否自增沿用实体自己的 `[SugarColumn(IsIdentity)]`，缺省不自增 |
| 并发戳 | `ConcurrencyStamp` 长度 32 |
| 字符串长度 | `[StringLength(n)]` 或 `[MaxLength(n)]` → 长度 n；`[LongText]` 或无参 `[MaxLength]` → 长文本类型；实体自己的 `[SugarColumn]` 已给长度或类型时不动；没有注解的沿用 SqlSugar 缺省（PostgreSQL 为 `varchar(255)`） |
| `DateTimeOffset` | 经转换器按 UTC 时刻往返；读取按驱动交出的 `DateTime.Kind` 还原（ADR-0037） |
| 日期时间精度 | `DateTimeOffset` 与 `DateTime` 在 MySQL 上建成 `datetime(6)`、在 Oracle 上建成 `timestamp(6)`，SQL Server 的 `DateTime` 建成 `datetime2(6)`，都到微秒；实体自己给了列类型的不动，已经建成的列不改（ADR-0231） |
| SQL Server 与 Oracle 字符串 | 有长度的字符串列建成 SQL Server `nvarchar(n)`、Oracle `nvarchar2(n)`，存 Unicode、按字符计长；Oracle 最长 2000，更长的用 `[LongText]`（ADR-0232） |
| 可空 | 可空值类型与可空引用类型的列可空 |
| 忽略 | 没有公开 setter 的属性与集合属性（字符串、字节数组除外）不映射 |
| PostgreSQL 缺省值 | 非可空的数值、枚举、布尔与 `Guid` 列配 CLR 缺省值（`0`、`false`、全零 Guid），`InitTables` 才能给已有行的表加这类列（ADR-0040） |
| 索引 | 实体上的 `[EntityIndex]` 转成 `SugarIndexAttribute`，名字按映射出的表名推出（ADR-0072；配置里已有的表服务回调先执行，表名以它们改过的为准）；实体自己的 `[SugarIndex]` 与之同名时以实体的为准；分表实体声明它抛 `NotSupportedException`，改用 `[SugarIndex]` 与 `{split_table}` 占位符 |
| 部分唯一索引 | 软删除实体的唯一索引只约束未删除的行（ADR-0081）：不交给 SqlSugar（它写不出条件），由 `SqlSugarConventions.InitTables` 建表后补建——PostgreSQL、SQLite 与 SQL Server 是带条件的索引，MySQL（8.0.13 起）与 Oracle 是函数索引（ADR-0249）；其余数据库上启动即失败（`PERSISTENCE_UNAVAILABLE_008`，名单见下文「建表与索引」），列里显式写 `IsDeleted` 的普通唯一索引不受限制 |
| 字符串匹配 | `string.Contains` / `StartsWith` / `EndsWith` 在 SQLite、PostgreSQL、MySQL、SQL Server、Oracle 上译成 `LIKE … ESCAPE '!'`（ADR-0098），`%`、`_`（SQL Server 上还有 `[`）按字面；SqlSugar 自己的翻译是不转义的 `LIKE`，`t_2` 会命中 `budgetx2026`。转义符不用反斜杠，结果与 MySQL 的 `NO_BACKSLASH_ESCAPES`、PostgreSQL 的 `standard_conforming_strings` 无关（MySQL 开了 `NO_BACKSLASH_ESCAPES` 时连接串要写 `NoBackslashEscapes=true`，这是 MySqlConnector 对所有参数的要求）。大小写照各库：SQLite 只对 ASCII 字母不区分，PostgreSQL 与 Oracle 区分，MySQL 与 SQL Server 按排序规则；`MoreSettings.EnableILike` 仍用 `ilike`；带 `StringComparison` 的重载忽略比较方式。要通配用 `SqlFunc.Like`。其余数据库（TiDB、OceanBase、openGauss、人大金仓、达梦等兼容库，没有在真实库上验证）沿用 SqlSugar 自己的翻译、按通配匹配，运行时数据源不施加约定，同样按通配匹配（ADR-0101）；应用在 `Apply` 之前自己登记了同名外部函数的以应用的为准，之后整体替换 `SqlFuncServices` 列表会丢掉这三个翻译 |
| PostgreSQL 会话 | 连接串（含从库）的启动参数 `Options` 追加 `-c standard_conforming_strings=on`（ADR-0100），服务器在建连时施加它，优先于库与角色的缺省值；原因与对原生 SQL 的影响见 [SqlSugar Provider](/persistence/sqlsugar#postgresql-注意事项)。连接串已显式写成 on 以外的值时 `Apply` 抛 `ArgumentException`，方式二要在 `Apply` 之前设好数据库类型与连接串；会话里自己 `SET … = off` 的不支持；PgBouncer 要 1.20 及以上（按其文档，没有实测）；PostgreSQL 系其他 `DbType` 与运行时数据源不加 |
| 作用范围 | 只管实体：声明在 `System.*` 类型上的属性原样交还 |
| 映射缓存 | 实体映射按数据库类型分开缓存（ADR-0248）：`Apply` 把配置的反射信息缓存包一层（应用设过的也一样），键带上数据库类型，`ConfigId` 相同、库类型不同的连接（如不同库类型的租户分片都是 `shard`）各建各的映射，同类型的共用一份 |

### 建表与索引

- `InitTables` 建缺表、加缺列，按落点分组：启动时没有调用也就没有分片，租户实体在默认连接建表（分片库的表结构由应用管理）；带 `[Tenant]` 或登记到命名连接的实体在各自的连接上建表；分表实体走 `CodeFirst.SplitTables()`。删列、改名、改类型不在范围内，生产库的结构演进走迁移脚本。
- 给已有行的表加非可空列：PostgreSQL 上数值、枚举、布尔与 `Guid` 列由约定回填缺省值，代价是此后每次 `InitTables` 都对这些列重发一次 `SET DEFAULT`；SQLite 由 SqlSugar 加成可空列，旧行读回 CLR 缺省值；非可空字符串、日期时间列与其他数据库不受支持，新列声明成可空，或重建开发库、走迁移脚本。
- 索引：`InitTables` 建表后按名字查不到才建，已有的不改不删，再次建表不重复建。表里已有重复数据时唯一索引建不出来，`InitTables` 失败、启动失败，先去重。一起建表的实体之间索引同名（不区分大小写，多半是显式的名字撞了）时，`InitTables` 在建任何表之前抛 `InvalidOperationException`：PostgreSQL、SQLite 与 Oracle 的索引名按架构唯一，按名字查到已有的就跳过，后建的那张表会静默少一个索引。
- 部分唯一索引：特性开着 `InitTables` 时经 `SqlSugarConventions.InitTables` 补建；应用自己建表（租户独立库、迁移工具）也要调它，直接调 `CodeFirst.InitTables` 建不出。键含可空列时，含 NULL 的键不受约束，各数据库一致。已有同名的不改不删：库里已有同名的带 `IsDeleted` 列的唯一索引时会被跳过，要先手动删掉那个索引。MySQL 的函数索引要 8.0.13 起，更早的版本与 MariaDB 上语句失败、`InitTables` 报错。
- 不支持部分唯一索引的数据库：MySQL 的兼容库（TiDB、OceanBase、Doris、GoldenDB、TDSQL，以及 `DbType.MySqlConnector`）、Oracle 的兼容库（达梦、OceanBase Oracle 模式）、ClickHouse、QuestDB、TDengine、DuckDB、Access、DB2、HANA，以及源自 PostgreSQL 而没有验证过的人大金仓、openGauss、GaussDB、瀚高、Vastbase、PolarDB（ADR-0084）。已登记的软删除实体声明了唯一索引（列里没有 `IsDeleted`）而它落的连接是这些数据库时，启动即失败，一次列出全部实体，不论是否开着 `InitTables`；`InitTables(client, …)` 在建任何表之前同样抛出。

### 实体落点、读写分离与差异日志

- 实体落点（规则见 [SqlSugar Provider](/persistence/sqlsugar#多库落点)）：落点不成立时抛出的是携带 `PERSISTENCE_VALIDATION_004` 的 `FrameworkException`。用缺省落点规则时，带 `[Tenant]` 或登记到命名连接的已登记实体在启动时就校验，不成立即中断启动；`shard` 只在带分片的调用里存在，不在启动时校验。带分片的调用交给回调的分片绑定，主值是租户连接串，属性含 `tenant`、`provider` 与透传属性。
- 读写分离（ADR-0118、ADR-0122，读写怎样路由与从库怎样配置见 [SqlSugar Provider](/persistence/sqlsugar#读写分离)）：提交之后同一作用域的读也在主库上。配了从库的连接每个作用域一份配置副本：SqlSugar 读从库时会改配置上的连接串，共用时别的作用域的写入会落到从库上。默认连接与命名连接的从库周期探测，健康项是 `SqlSugarPersistenceFeature.ReplicaHealthName`（`persistence.sqlsugar.replicas`）；分片连接的从库由回调按分片绑定给出，不探测。没配从库时没有任何额外开销。
- 差异日志（开启方式与注意事项见 [SqlSugar Provider](/persistence/sqlsugar#差异日志)）：开启的实体走 SqlSugar 的泛型构建器。回调里调用方的 AsyncLocal 可读，当前调用经 `SqlSugarDiffLogData.Services` 里的 `IInvocationAccessor` 取；缓冲时在回调里复制值。

### 写入与事务

- 行锁：更新与删除的读取带 `TranLock`（`FOR UPDATE` / `UPDLOCK`），读取与写入之间别人改不了这一行；SQLite 不支持行锁，由它的数据库级写锁保证；分表实体不加行锁（分表查询是多表 `UNION ALL`），靠并发戳，分表字段写入后不要再改。
- 游离实例更新：并发戳与当前行不同返回 `PERSISTENCE_CONFLICT_001`；通过后把调用方的实例整行写入，创建时间、租户与删除标记取当前行的值，调用方实例上的这几个值不生效，写入后调用方实例拿到它们。按主键更新影响 0 行（行在加锁前已被删除）也返回 `PERSISTENCE_CONFLICT_001`。
- 并发新增同一标识：检查之后别的事务提交了同一标识，插入撞上主键约束。PostgreSQL 系语句失败后整个事务进入中止状态，所以仓储另开一个不在事务里的连接核对这些标识：确已存在就返回 `PERSISTENCE_CONFLICT_002`，批量时这次写入的项统一报告；否则异常原样抛出。之后工作单元只能回滚：处理器忽略失败返回成功时，提交回滚并返回同一错误，不会对一个已中止的事务执行 `COMMIT`。
- 唯一键重复：新增、更新、软删除的写入失败又不是标识重复时，同样另开连接核对实体的唯一索引（这次写入里两项同键，或已提交的另一行同键；软删除实体的部分唯一索引只比较未删除的行；实体自己的唯一 `[SugarIndex]` 也核对，分表实体不核对），撞上返回 `PERSISTENCE_CONFLICT_003`（参数 `index` 是索引名），这次写入的项统一报告，工作单元随后只能回滚；核对不到的（与本工作单元先前未提交的行冲突、大小写不敏感排序规则下只差大小写、键含空值）异常原样抛出。
- 软删除：在取回的当前行上打标并整行写回，调用方实例随后拿到删除标记，以及当前行的修改时间与并发戳。
- 实体类型没有 `new()` 约束（开了差异日志的实体除外）。不提供按查询条件的批量更新 / 删除（`Updateable().SetColumns().Where()`）：它绕过审计、并发戳与软删除协议；需要时应用直接用客户端。
- 只读工作单元不开事务、拒绝写入（`PERSISTENCE_UNEXPECTED_001`，仓储写入的通用规则见 [Persistence.Abstractions](./persistence-abstractions)）。直接用客户端写入（`Insertable` / `Updateable` / `Deleteable`）之前先调 `EnsureTransactionAsync`，它在只读工作单元里抛出携带这个码的 `FrameworkException`（Transaction 阶段转为同码结果）；不经它直接执行的写入不受守卫，也不在事务里。
- 作用域结束时事务仍未提交（中间件之外直接使用），释放即回滚。
- 跨库提交是顺序提交，不是两阶段提交：一次调用写了多个连接时，后一个连接提交失败，前一个已经提交；需要严格原子性的写入放在同一个库。
- SQL Server 的库要打开 `READ_COMMITTED_SNAPSHOT`（`ALTER DATABASE [库名] SET READ_COMMITTED_SNAPSHOT ON`）：缺省的读已提交是加锁读，别的工作单元读未提交的行要等写入方结束（ADR-0234）。

### 健康项、运行时数据源与库结构读取

- 健康项 `persistence.sqlsugar.database`：启动时同步探一次，之后按间隔在后台重探并缓存结果；探测对默认连接走一次数据库往返（打开连接并执行一条语句，ADR-0123；`MongoDb`、`Custom`、`Odbc` 没有通用的语句，只打开连接），不开工作单元、不经管道，单次上限是这个间隔，驱动不理取消令牌时也不等它。连不上或超时为 Degraded（`KERNEL_UNAVAILABLE_005`，`reason` 写异常类型，完整异常进日志），应用仍在服务。
- 运行时数据源（ADR-0078，用法见[连接与运行时数据源](/persistence/connections)）：数据库类型是 SqlSugar `DbType` 的名字（不区分大小写，`Custom` 与数字不认）；登记时由驱动解析连接串，格式错即 `PERSISTENCE_VALIDATION_006`。客户端是每个数据源一个的 `SqlSugarScope`，不施加协议约定、不挂过滤器与 AOP、不进工作单元，PostgreSQL 上不自动转小写、Oracle 上不自动转大写；不加进作用域客户端，`default` / `shard` 与实体落点不受影响。清单的配置节因此写 `XiHan:Persistence`（读 `XiHan:Persistence:SqlSugar` 与 `XiHan:Persistence:DataSources`）。
- 库结构读取与实体目录（ADR-0078，规则见[连接与运行时数据源](/persistence/connections)）：缺省连接另开不在事务里的连接读，运行时数据源按定义登记后另建客户端；分表按已登记实体的模板折叠（模板里 `{year}` 四位，`{month}`、`{day}` 两位，不区分大小写）；驱动与 SqlSugar 的异常翻译为 `PERSISTENCE_UNAVAILABLE_006`，其余异常原样抛出。在 SQLite、PostgreSQL、MySQL、SQL Server 与 Oracle 上验证过（ADR-0084）：SQLite 没有注释，`INTEGER PRIMARY KEY` 报告可空（按主键改为不可空）；缺省值是数据库报告的原文（MySQL `x`、SQL Server `('x')`、Oracle `'x'`）；Oracle 读的是连接用户自己的数据字典，要 12c 及以上，11g 及更早的版本读列以 `PERSISTENCE_UNAVAILABLE_006` 失败。达梦、OceanBase Oracle 模式等 Oracle 兼容库走 SqlSugar 自己的读列（类型名可能是 CLR 类型名，identity、缺省值、大小写混合的表名可能读不对），没有验证，读到的结构不作保证（ADR-0087）。

### MySQL 上的已知限制

SqlSugar 上游缺陷（已报 [DotNetNext/SqlSugar#1460](https://github.com/DotNetNext/SqlSugar/issues/1460)，待上游修复；ADR-0100 修订）：两个及以上值的 `In(…)`、按实体删除（单个实体也是）与 `Deleteable<T>().In(…)` 把字符串内联成只转义单引号的字面量，集合 `Contains` 则把反斜杠写成两个。MySQL 缺省 sql_mode 下前一组对含反斜杠的值静默漏行（值里有 `\'` 时报语法错误），开了 `NO_BACKSLASH_ESCAPES` 时后一组静默漏行；逐个 `==`、只有一个值的 `In`、单行写入与多行插入用参数，两种模式都对。

仓储里受影响的是 `FindManyAsync`（两个及以上标识）、`AddRangeAsync` 的标识预查、`UpdateRangeAsync` / `RemoveRangeAsync` 的加锁取回、物理删除的 `RemoveAsync` / `RemoveRangeAsync`（缺省模式），以及规约里的集合 `Contains`（`NO_BACKSLASH_ESCAPES`）。框架自己的字符串主键（租户、调度、工作流定义）是不含反斜杠与引号的受限标识符，Guid 与整数主键也不受影响；框架不在仓储里绕开。应用的字符串主键首选不含反斜杠（写入前按 `^[A-Za-z0-9][A-Za-z0-9._-]*$` 这类规则校验）；做不到时改用参数化的写法：

```csharp
// db：本作用域的 ISqlSugarClient；documents：IRepository<Doc, string>；unitOfWork：本作用域的 SqlSugarUnitOfWork
// 多个标识的查找：缺省 sql_mode 下用集合 Contains（SqlSugar 对 MySQL 把反斜杠写成两个），不用 In / FindManyAsync
var docs = await db.Queryable<Doc>().Where(d => ids.Contains(d.Id)).ToListAsync();
var viaRepository = await documents.ListAsync(EntityQuery.All<Doc>().Where(new Specification<Doc>(d => ids.Contains(d.Id))), ct);
// 开了 NO_BACKSLASH_ESCAPES 的反过来：用 db.Queryable<Doc>().In(ids)，不用集合 Contains

// 两种模式都对的写法：逐个按 == 比较（参数）
var one = await documents.FindAsync(id, ct);
await unitOfWork.EnsureTransactionAsync(ct); // 在调用的工作单元里直接用客户端写入之前
await db.Deleteable<Doc>().Where(d => d.Id == id).ExecuteCommandAsync(); // 代替 RemoveAsync 的物理删除与 Deleteable(entity)
```

## 相关

- [持久化](/persistence/overview)
- [工作单元与事务](/persistence/unit-of-work)
- [多租户](/multi-tenancy/resolution)
- [审计](/common/auditing)
- [Persistence.Abstractions](./persistence-abstractions)
- [Persistence.EntityFrameworkCore](./persistence-entityframeworkcore)
- 规范：RFC-0005《领域协议与持久化》
- 决策：ADR-0013、ADR-0022、ADR-0023、ADR-0035、ADR-0036、ADR-0037、ADR-0040、ADR-0044、ADR-0078、ADR-0098、ADR-0100（含 MySQL 已知限制的修订）、ADR-0101
