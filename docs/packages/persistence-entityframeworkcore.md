# XiHan.Framework.Persistence.EntityFrameworkCore

`persistence.relational@1` 的 Entity Framework Core Provider：应用持有自己的 `DbContext`，框架在它之上提供仓储、事务型工作单元与协议约定。适合新项目与使用 EF Core 迁移管理表结构的应用。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Persistence.EntityFrameworkCore` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `persistence.relational@1`（`role: provider`）；依赖 `kernel.hosting@1`，可选依赖 `multitenancy.sharding@1`；绑定 `db`（`connection:relational`，可选） |
| 特性入口 | `EntityFrameworkCorePersistenceFeature<TContext>` |
| 配置节 | `XiHan:Persistence:EntityFrameworkCore` |
| 依赖 | `XiHan.Framework.Persistence.Abstractions`、`XiHan.Framework.MultiTenancy.Abstractions`、`Microsoft.EntityFrameworkCore`；数据库驱动包由应用引用 |
| AOT | 不兼容（EF Core 的运行时模型构建不满足 AOT 分析器） |
| 稳定性 | Preview |

## 概述

特性以 `AddDbContext<TContext>` 注册应用的 `DbContext`，每次构建选项时调用应用给的配置回调；同一作用域里的仓储、工作单元与应用自己注入的 `TContext` 是同一个实例。

工作单元在第一次写入时开启数据库事务，只读调用不开事务。仓储的每次写入立即 `SaveChanges`，所以同一事务里随后的查找与查询读得到它；提交即提交事务，回滚即回滚事务并清空变更跟踪器。批量方法用一次 `IN` 查询取回目标行、逐项校验后一次 `AddRange` / 跟踪修改，再一次 `SaveChanges`。

软删除与租户过滤由仓储用 `EF.Property` 显式施加在每个查询上，`FindAsync` 与 LINQ 查询行为一致。多租户实体另带名为 `xihan:tenant` 的全局查询过滤器（ADR-0224）：应用直接用工作单元里的 `DbContext` 查询时只看到工作单元租户的行，软删除条件要自己写。更新游离实体时先比对并发戳，跟踪中的实体由 EF Core 的并发令牌保护。创建时间、租户与删除标记只由仓储写入：更新与删除时它们回到跟踪实例的原值，调用方改了也不生效。

## 何时使用

- 新项目，或已经用 EF Core 的应用。
- 表结构用 EF Core 迁移管理，复杂查询直接写在 `DbContext` 上。

已有大量 SqlSugar 代码、需要一次调用写多个库或按时间分表时用 [Persistence.SqlSugar](./persistence-sqlsugar)；两个关系型 Provider 的 `IsAotCompatible` 都是 `false`，标注为 AOT 兼容的持久化实现只有 [Persistence.InMemory](./persistence-inmemory)，AOT 发布的整体要求见[序列化与 Native AOT](/common/serialization)。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Persistence.EntityFrameworkCore --prerelease
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[环境与安装](/getting-started/installation)。数据库驱动按所用数据库引用（Sqlite、Npgsql、SqlServer 等）。SQLite 用最新版的 `Microsoft.EntityFrameworkCore.Sqlite`：它带来的 `Microsoft.Data.Sqlite.Core` 修复了连接池激活的竞态（dotnet/efcore#39008：并发打开连接时两个连接可能共用一个原生连接），修复之前的版本带着它。本包不依赖任何 SQLite 包，版本由应用的驱动引用决定（ADR-0097）。

装配分三步，完整代码见 [EF Core Provider](/persistence/entity-framework-core#最小示例)：定义 `DbContext` 并在 `OnModelCreating` 末尾调用 `ApplyPersistenceConventions(Database)`（引用了 [Persistence.Generators](./persistence-generators) 时换成生成的 `ApplyGeneratedEntities(Database)`），用 `AddRepository<TEntity, TKey>()` 登记实体，装配 `EntityFrameworkCorePersistenceFeature<TContext>` 并在回调里用绑定 `db` 配置连接。

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Persistence:EntityFrameworkCore:EnsureCreated` | 布尔 | `false` | 启动时对默认连接调用 `EnsureCreated` 建表，登记了实体的每个命名连接也各执行一次；只用于开发与测试，生产用迁移 |
| `XiHan:Persistence:EntityFrameworkCore:HealthProbeIntervalSeconds` | 正数 | `15` | 健康项的重探间隔，也是单次探测的超时 |
| `XiHan:Persistence:EntityFrameworkCore:VerifiedShardCapacity` | 正整数 | `1024` | SQLite 上核对通过的租户分片库最多记多少个（见「已有 SQLite 库的时间列」）；记满淘汰最早记下的，被淘汰的分片下次绑定时重新核对一次 |
| `XiHan:Persistence:EntityFrameworkCore:Connections:<name>` | 字符串 | — | 命名连接，值是连接串；绑定它的工作单元所在作用域的 `DbContext` 建在这个连接上（见下文「命名连接」） |
| `XiHan:Bindings:db` | 字符串 | — | 连接绑定的主值，作为 `BindingValue.Value` 交给回调 |

回调签名是 `Action<Maybe<BindingValue>, DbContextOptionsBuilder>`。绑定缺失时 `Maybe` 为空，回调自己决定连接；`binding.ValueOrThrow()` 在缺失时抛出。配置 JSON 的例子见 [EF Core Provider](/persistence/entity-framework-core#最小示例)。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `EntityFrameworkCorePersistenceFeature<TContext>` | 特性入口；构造参数是配置回调；常量 `ConnectionBinding`（`db`）与 `HealthName`（`persistence.entityframeworkcore.database`） |
| `EntityFrameworkCorePersistenceOptions` | 选项：`EnsureCreated`、`HealthProbeInterval`、`VerifiedShardCapacity`、`Connections`（命名连接）；`FromConfiguration(configuration)` |
| `ModelBuilderExtensions` | `ApplyPersistenceConventions(Database)` 对模型里已有的每个实体施加协议约定：并发令牌、租户与软删除索引、租户查询过滤器（ADR-0224）、`[EntityIndex]`（ADR-0072、ADR-0081）、SQLite 上的时间列（ADR-0091），逐项见 [EF Core Provider](/persistence/entity-framework-core#应用自有-dbcontext-与约定)；常量 `TenantQueryFilter`（`xihan:tenant`）是租户查询过滤器的名字 |
| `EntityFrameworkCoreUnitOfWork` | 事务型工作单元；`Context` 是本作用域的 `DbContext`（开始时建出：绑定了命名连接就建在那个连接上）；由容器创建，构造不公开 |

## 使用示例

示例在教程页 [EF Core Provider](/persistence/entity-framework-core)，按场景找：

| 场景 | 示例 |
| --- | --- |
| 装配特性、定义 `DbContext`、配置 JSON | [最小示例](/persistence/entity-framework-core#最小示例) |
| 用生成的 `ApplyGeneratedEntities` 登记实体 | [应用自有 DbContext 与约定](/persistence/entity-framework-core#应用自有-dbcontext-与约定) |
| 仓储覆盖不了的查询与写入 | [直接使用 DbContext](/persistence/entity-framework-core#直接使用-dbcontext) |
| 命名连接的配置、绑定连接的工作单元 | [连接与运行时数据源](/persistence/connections) |
| 管道外自己开工作单元 | [工作单元与事务](/persistence/unit-of-work) |

## 行为与限制

- 读己之写：每次写入立即 `SaveChanges`，在本事务内可见，提交前对其他连接不可见。
- 延迟开启事务：只读过数据的调用不开事务。第一次 `EnsureTransactionAsync`（仓储每次写入前、提交时保存残余变更前都经它）开事务后 `HasTransaction` 为真，提交、回滚成功后立即释放事务对象、复位为假（ADR-0109）。
- 游离实例更新：先比对并发戳，不符返回 `PERSISTENCE_CONFLICT_001`；通过后把游离实例的值复制到跟踪实例上，再把创建时间、租户与删除标记恢复为原值（`OriginalValues`，即存储里的值）。保存成功后把修改时间、并发戳、创建时间与租户同步回游离实例。
- 读出后直接改了跟踪实例再更新：同样恢复这几个字段，写入路径也按原值判定可见性，所以读出后改了租户的实例照常更新，不会被当成不存在，也改不了租户。
- 提交时还有残余变更（绕过仓储直接改了跟踪实体）会先保存再提交，其中的创建时间与租户同样恢复为原值；并发令牌不匹配返回 `PERSISTENCE_CONFLICT_001` 并回滚。
- 并发新增同一标识：检查之后别的事务提交了同一标识，插入撞上主键约束。`SaveChanges` 在已有事务里会自动建保存点，失败时已回到保存点，事务仍可用。仓储把这次新增的实体移出跟踪，在同一事务里核对这些标识：确已存在就返回 `PERSISTENCE_CONFLICT_002`，批量时这次写入的项统一报告；否则异常原样抛出。之后工作单元只能回滚，处理器忽略失败返回成功时，提交回滚并返回同一错误。数据库是 MySQL 的可重复读这类快照隔离时，同一事务看不到对方刚提交的行，异常照常抛出。
- 只读工作单元拒绝写入（`PERSISTENCE_UNEXPECTED_001`，仓储写入的通用规则见 [Persistence.Abstractions](./persistence-abstractions)）：直接用 `DbContext` 写入之前先调 `EnsureTransactionAsync`，它在只读工作单元里抛出携带这个码的 `FrameworkException`（Transaction 阶段转为同码结果）；绕过仓储改了跟踪实体而未保存的，提交时以它回滚——只读操作里不要把读出的实体当草稿改。
- 唯一键重复：新增、更新、删除的 `SaveChanges` 失败又不是标识重复时，在保存点之后的同一事务里核对模型里的唯一索引（这次保存里两项同键，或库里另一行同键；软删除实体的部分唯一索引只比较未删除的行），撞上就把这些实体移出跟踪、返回 `PERSISTENCE_CONFLICT_003`（参数 `index` 是索引名），工作单元随后只能回滚；核对不到的异常原样抛出。
- 依赖 `Microsoft.EntityFrameworkCore.Relational`（索引名与表名是关系型元数据）。
- 软删除实体的唯一索引只约束未删除的行（ADR-0081）：`HasFilter` 的条件 Npgsql `"IsDeleted" = false`、SQLite `"IsDeleted" = 0`、SQL Server `[IsDeleted] = 0`（键含可空列另加 `IS NOT NULL`）；Pomelo MySQL、Oracle 等不支持部分唯一索引的 Provider 建模即失败（SqlSugar 在 MySQL 与 Oracle 上用函数索引代替，EF Core 的模型写不出函数索引，ADR-0249），要唯一约束就把 `IsDeleted` 显式写进列。显式的 `[EntityIndex(Name = …)]` 原样交给 `HasDatabaseName`。
- SQLite 上 `ApplyPersistenceConventions(Database)` 把模型里全部 `DateTimeOffset` 与可空 `DateTimeOffset` 属性（含从属类型与复杂类型里的，框架表与应用实体一样）存成 UTC ticks 整数（ADR-0091）：EF Core 的 SQLite Provider 把 `DateTimeOffset` 存成文本，翻译不了大小比较与排序（`SQLite does not support expressions of type 'DateTimeOffset' in ORDER BY clauses`）。写入 `UtcTicks`、读回偏移为零的同一时刻，100 纳秒精度与 SqlSugar 在 SQLite 上的 UTC 日期时间文本一致。应用已配了值转换或存储类型的属性（`HasConversion`、`ConfigureConventions` 的 `HaveConversion`，如 `DateTimeOffsetToBinaryConverter`）不动，库不用动、也不核对；其余 Provider 不动。库里这类列还是 EF Core 缺省的 `DateTimeOffset` 文本时，照整数读会被**静默读成错误的时刻**，所以特性启动时核对，不符即启动失败（`PERSISTENCE_VALIDATION_008`，ADR-0094），怎么修见下文[已有 SQLite 库的时间列](#已有-sqlite-库的时间列)。
- SQLite 上字符串的 `Contains` / `StartsWith` / `EndsWith` 一律译成 `LIKE … ESCAPE '\'`（ADR-0101）：只对 ASCII 字母不区分大小写、其余字母区分，`%`、`_`、反斜杠按字面，与 SqlSugar 在 SQLite 上同一套规则；字符实参与字符串一样，NULL 实参不匹配。EF Core 的 SQLite Provider 自己的翻译在同一个库上有三种规则：`Contains` 是区分大小写的 `instr`，`StartsWith` / `EndsWith` 的实参是值时是只对 ASCII 字母不区分大小写的 `LIKE`、是列或表达式时是区分的 `substr` 比较。范围是特性建出的 `DbContext` 上的全部 LINQ（仓储的规约、应用直接写的查询、投影、子查询、参数化集合），投影里落到客户端求值的部分按 .NET 原方法计算；应用自己的全局查询过滤器（`HasQueryFilter`）、不经特性建出的 `DbContext` 不经过改写，应用用 `UseInternalServiceProvider` 自给 EF Core 内部服务容器的不受支持。SQLite 的 `LIKE` 模式最长 50000 字节，`Contains` 的实参超过即报错。PostgreSQL、SQL Server 等其余数据库照 EF Core 数据库 Provider 的翻译（一套规则、按字面），不改写。
- 启动时在独立作用域里取一次模型，建模错误（索引声明不合法、Provider 不支持部分唯一索引）在启动时失败，不论是否开着 `EnsureCreated`；SQLite 上随后逐连接核对时间列的存储形式（见下文[已有 SQLite 库的时间列](#已有-sqlite-库的时间列)）。
- 作用域结束时事务仍未提交（中间件之外直接使用），释放即回滚。
- 租户分库：带分片的调用整个 `DbContext` 连到分片库，没有 SqlSugar Provider 那样按实体分库的落点规则，分片库的表结构由应用管理，见 [EF Core Provider](/persistence/entity-framework-core#租户分库)。
- 健康项 `persistence.entityframeworkcore.database`：启动时同步探一次，之后按间隔在后台重探并缓存结果；探测在独立作用域里对默认连接 `CanConnect`，不开工作单元、不经管道。连不上为 Degraded（`PERSISTENCE_UNAVAILABLE_003`），应用仍在服务。
- 整数主键按 EF Core 约定由数据库生成；应用自己生成整数标识时配 `ValueGeneratedNever()`，见[标识生成](/persistence/identifiers)。
- 索引：框架的数据库记录（授予、翻译、审计、调度、工作流、发件箱）在 `modelBuilder.Entity<T>()` 之后调用 `ApplyPersistenceConventions(Database)` 即得到索引，不用再手写 `HasIndex`；索引名取调用时的表名，`ToTable` 要在它之前配置，从属类型与共享类型实体跳过。
- 实体目录（ADR-0078）取 `DbContext` 模型（`GetTableName()`、`GetColumnName(表)`，影子属性不列）：没有映射到表时表名取类型名，从属类型不列，登记了却不在模型里的实体抛 `InvalidOperationException`。
- 运行时数据源与库结构读取不支持，也不计划支持（ADR-0078、ADR-0084）：数据库 Provider 由应用选、本包不引用，按数据库类型建不出连接；跨数据库读结构在 EF Core 里是设计期脚手架服务，`DbConnection.GetSchema` 各驱动不一致、SQLite 驱动不实现。`DataSourceRegistry` 照样登记，取用与测试连接、`ISchemaReader` 都返回 `PERSISTENCE_UNAVAILABLE_008`；需要代码生成的读结构、外部数据源或零代码运行时的应用用 [Persistence.SqlSugar](./persistence-sqlsugar)。

### 命名连接

在 `XiHan:Persistence:EntityFrameworkCore:Connections:<name>` 声明（ADR-0087），值是连接串。名字合 `^[a-z0-9][a-z0-9-]{0,63}$`，不能是 `default` 或 `shard`；值不能空白、不能写成子节，否则装配即 `ArgumentException`。

一个 `DbContext` 一个连接、建出后不换：`UnitOfWorkOptions { Connection = name }` 开启的工作单元先记下连接、再建本作用域的 `DbContext`，回调收到的绑定主值是那个连接串、属性沿用 `db` 绑定，事务只开在那个库上；命名连接与默认连接用同一个配置回调，是同一种数据库 Provider。所以登记到命名连接的实体（`AddRepository<TEntity, TKey>(connection)`）只能在绑定那个连接的工作单元里读写：

| 情况 | 结果 |
| --- | --- |
| 普通工作单元里取用登记到命名连接的实体的仓储 | 抛出携带 `PERSISTENCE_UNAVAILABLE_008` 的 `FrameworkException` |
| 绑定连接的工作单元里取用落在别处的实体 | `PERSISTENCE_VALIDATION_004` |
| 登记到没有声明的连接 | 装配即失败，`PERSISTENCE_VALIDATION_004` |
| 绑定没有声明的连接 | `BeginAsync` 抛出 `PERSISTENCE_VALIDATION_004` |
| 作用域里已经按别的连接建出 `DbContext`（先取用了它）再绑定 | `BeginAsync` 抛 `PERSISTENCE_VALIDATION_002`，在新作用域里绑定即可 |

EF Core 不做「每个连接一个 `DbContext`、跨上下文顺序提交」的多上下文工作单元，普通工作单元里取用命名连接上的实体维持明确报错（ADR-0091）；要在业务事务里一起读写命名连接上的实体，用 [Persistence.SqlSugar](./persistence-sqlsugar)。`EnsureCreated` 对登记了实体的每个命名连接各执行一次，按整个模型建表（那个库里多出的空表不用）。工作流的租约表就这样放到另一个库：SQLite 上 `XiHan:Workflow:Database:LockConnection` 指向这里声明的连接，调用方事务里取锁不用等自己的事务。

### 已有 SQLite 库的时间列

不经 UTC ticks 约定（ADR-0091）建出的 SQLite 库，没配转换的列是 `TEXT`，存 EF Core 的 `DateTimeOffset` 文本（如 `2026-11-01 08:00:00+08:00`）；按整数读时 SQLite 把文本开头的年份当整数，这些行会被静默读成 `0001-01-01T00:00:00.0002026` 这样的错误时刻，不报错。特性启动时因此核对库里的存储形式，不符即启动失败（ADR-0094）：

- 核对什么：默认连接一次，登记了实体的每个命名连接各一次；落在这个连接上的实体的表里、由约定映射成 UTC ticks 的列。列的声明类型不是 INTEGER（旧库的 `TEXT`，空表也算——TEXT 列会把之后写入的整数也存成文本）即不符；声明是 INTEGER 时抽样表里最早写入的 1000 行，查到非整数的值（重建表时没换算）即不符。
- 失败的样子：特性启动失败，原因是 `PERSISTENCE_VALIDATION_008`，消息列出连接与不符的列，如 `TimedRow.At (TEXT)`（声明类型）、`TimedRow.Due (INTEGER, text)`（声明类型与抽样查到的存储类）。核对只读，不改库。
- 不核对什么：SQLite 之外的数据库；应用自己配了转换的列；库里没有的表或列（迁移没跑到，读写时 EF Core 自己报错）；库文件还不存在（不打开连接，不会因此建出空库）。
- 租户分片库（ADR-0097）：启动时还不知道有哪些分片，改在首次绑定时核对——工作单元开始时 `DbContext` 建在租户分片上（Tenant 阶段解析出的分片，或 `TenantShardScope` 显式绑定的），而这个分片库还没有核对通过，就先按同样的范围与判定核对一次。不符时那次工作单元开不起来：抛出携带 `PERSISTENCE_VALIDATION_008` 的异常，经管道的调用是 `KERNEL_UNEXPECTED_001`（HTTP 500）、原因是它，消息带租户与不符的列（消息键 `persistence.schema.shard_timestamp_format_mismatch`，不带连接串）；这个分片上的调用都失败，别的租户照常。报 500 而不是 400（ADR-0099）：这是服务端库里的数据问题，调用方改不了，与工作单元开始时其余装配类错误（`PERSISTENCE_VALIDATION_002`、`004`）一样；原因码在 `Inner` 里，排查看日志里的内层错误。核对通过的分片按连接串记在进程内，之后每次开工作单元只看这条记录，同一分片的并发首绑排队、只查一次；记录至多 `VerifiedShardCapacity` 条（默认 1024），记满淘汰最早记下的，被淘汰的分片下次绑定时重新核对一次（并发再绑同样只查一次），SQLite 分片库很多的应用可以调大；不符、库文件还不存在、核对抛出异常（打不开库、调用取消）都不记下，按下面的办法修好或建出库之后，下一次绑定即重新核对，不用重启。不经工作单元、直接解析 `DbContext` 读写分片的不在范围内。
- 开销：每个连接一次读表结构，每张这类表一次至多 1000 行的抽样，与表的大小无关：1 张 100 万行的表约 1 毫秒，200 张表各 1 万行约 100 毫秒。租户分片是每个分片库首次绑定时一次，算在那次调用里。
- SqlSugar 不需要这项核对：它在 SQLite 上把 `DateTimeOffset` 存成 UTC 日期时间文本、读取时整段解析；库里是整数（如 EF Core 建的库）时读取抛出 `InvalidCastException`，不会静默读错。

怎么修：

- 数据可以丢的开发、测试库：删库重建（`EnsureCreated` 或重新跑迁移）。
- 要留数据：重建表，列建成 `INTEGER`，搬数据时把文本换算成 UTC ticks。SQLite 改不了列类型，EF Core 迁移改列类型本来就是重建表，但它搬数据是原样 `INSERT … SELECT`，文本搬进 INTEGER 列仍是文本，启动核对照样失败——要把迁移里搬数据的语句换成带换算式的（`migrationBuilder.Sql(...)`），或手工执行。只就地 `UPDATE` 不重建表时列仍声明为 `TEXT`，整数按文本存，启动核对同样失败。

换算式（`col` 换成列名；对 NULL 得 NULL），把 EF Core 的文本 `yyyy-MM-dd HH:mm:ss[.fffffff]±hh:mm` 换成 UTC ticks，实测不同偏移、带 7 位小数的值往返一致：

```sql
(unixepoch(substr(col, 1, 19))
 - (CASE substr(col, -6, 1) WHEN '-' THEN -1 ELSE 1 END) * (CAST(substr(col, -5, 2) AS INTEGER) * 3600 + CAST(substr(col, -2, 2) AS INTEGER) * 60)) * 10000000
+ CASE WHEN substr(col, 20, 1) = '.' THEN CAST(substr(substr(col, 21, length(col) - 26) || '0000000', 1, 7) AS INTEGER) ELSE 0 END
+ 621355968000000000
```

重建一张表（以 `TimedRow(Id, At NOT NULL, Due NULL)` 为例，`<At 的换算式>` 是上式把 `col` 换成 `"At"`）：

```sql
CREATE TABLE "ef_temp_TimedRow" ("Id" TEXT NOT NULL CONSTRAINT "PK_TimedRow" PRIMARY KEY, "At" INTEGER NOT NULL, "Due" INTEGER NULL);
INSERT INTO "ef_temp_TimedRow" ("Id", "At", "Due") SELECT "Id", <At 的换算式>, <Due 的换算式> FROM "TimedRow";
DROP TABLE "TimedRow";
ALTER TABLE "ef_temp_TimedRow" RENAME TO "TimedRow";
-- 再建回这张表原有的索引
```

## 相关

- [持久化](/persistence/overview)
- [工作单元与事务](/persistence/unit-of-work)
- [多租户](/multi-tenancy/resolution)
- [Persistence.Abstractions](./persistence-abstractions)
- [Persistence.SqlSugar](./persistence-sqlsugar)
- [Persistence.Generators](./persistence-generators)
- 规范：RFC-0005《领域协议与持久化》
- 决策：ADR-0005、ADR-0078、ADR-0101
