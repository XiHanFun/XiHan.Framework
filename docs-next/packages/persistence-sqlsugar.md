# XiHan.Framework.Persistence.SqlSugar

`persistence.relational@1` 的 SqlSugar Provider：以作用域内的 `ISqlSugarClient` 实现仓储与事务型工作单元，实体不继承 SqlSugar 基类、不必标注 `[SugarColumn]`，映射由协议约定推出。面向已有 SqlSugar 代码的应用，并支持一次调用写多个库、按时间分表与差异日志。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Persistence.SqlSugar` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `persistence.relational@1`（`role: provider`）；依赖 `kernel.hosting@1`，可选依赖 `multitenancy.sharding@1`；绑定 `db`（`connection:relational`，可选） |
| 特性入口 | `SqlSugarPersistenceFeature` |
| 配置节 | `XiHan:Persistence:SqlSugar` |
| 依赖 | `XiHan.Framework.Persistence.Abstractions`、`XiHan.Framework.MultiTenancy.Abstractions`、`SqlSugarCore` 5.1.4.221（自带各数据库驱动）、`Microsoft.Data.Sqlite` 10.0.13 |
| AOT | 不兼容（SqlSugar 的实体映射与表达式翻译基于运行时反射） |
| 稳定性 | Preview |

## 概述

客户端有两种来源。方式一由特性按配置回调创建作用域内的 `SqlSugarClient`，自动施加 `SqlSugarConventions`；方式二由应用自己注册 `ISqlSugarClient`（`SqlSugarScope`、多库等），应用在创建客户端前对每个 `ConnectionConfig` 调用 `SqlSugarConventions.Apply`。两种方式下仓储、工作单元与查询语义相同。

SqlSugar 没有变更跟踪，仓储的每次写入立即执行。工作单元在第一次写入时经 `AsTenant()` 在客户端的全部连接上开事务，只读调用不开事务；提交逐连接提交，回滚逐连接回滚。更新与删除先在事务内加行锁取回当前行（`TranLock`），比对并发戳后把调用方的实例整行写入，其中创建时间、租户与删除标记先换成当前行的值；读取与写入之间别人改不了这一行（SQLite 与分表实体不加行锁，见下文）。批量方法用一次主键 `IN` 查询取回目标行，逐项校验后一次多行插入、更新或删除。

软删除与租户过滤以表达式显式施加在每个查询上，不用 SqlSugar 的全局过滤器，不会与应用自己的过滤器互相覆盖。规约在交给 SqlSugar 前先改写（ADR-0035 与其修订）：

- 可空值类型与 `null` 的 `==` 改写成 `SqlFunc.EqualsNull(x, null)`，`!=` 改写成 `HasValue`，避免译成 `= NULL` 后静默返回错误的结果。
- 逻辑非下推到叶子（双重否定消去、`!(p && q)` 按德摩根律展开、`!(a < b)` 换成 `a >= b`、`!x.HasValue` 换成判空）。SqlSugar 在 `A && (!x.HasValue || B)`、`A && B && (x == null || …)` 这类位置会漏写 `OR` / `AND`，生成语法错误的 SQL，改写后照常翻译，结果在 SQL 三值逻辑下不变。

字符串的 `Contains`、`StartsWith`、`EndsWith` 按字面匹配（ADR-0098）。SqlSugar 原来把它们译成不转义的 `LIKE`：`t_2` 命中 `budgetx2026`，`0%` 命中 `500 off`，SQL Server 上 `[a]` 是字符类，PostgreSQL 与 MySQL 上反斜杠被当成转义符。`SqlSugarConventions.Apply` 以同名的外部函数（`SqlFuncServices`）接管这三个方法的翻译，所以不只是仓储的规约，直接用施加了约定的客户端写的查询、删除条件、投影与子查询也一样；集合的 `Contains`（`IN`）不受影响。见下文「行为与限制」的「字符串匹配」一行。

## 何时使用

- 已有 SqlSugar 代码的应用：实体不推倒重来，原生查询继续用自己的客户端，逐步收进仓储。
- 一次调用既写平台表又写租户分片库，或需要 `[Tenant]` 把实体固定到某个库。
- 按月 / 按日分表的日志类实体。
- 需要 SqlSugar 差异日志取写入前后值。

新项目、用 EF Core 迁移管理表结构的应用见 [Persistence.EntityFrameworkCore](./persistence-entityframeworkcore)。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Persistence.SqlSugar" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。本包依赖 `SqlSugarCore`；应用改用 `SqlSugarCoreNoDrive` 加单个驱动时要整体替换，两者不能并存。

本包还显式依赖 `Microsoft.Data.Sqlite` 10.0.13（ADR-0097）。`SqlSugarCore` 5.1.4.221 只声明 `>= 10.0.11`，NuGet 按下限解析到的 10.0.11 带着连接池激活的竞态（dotnet/efcore#39008，10.0.13 修复）：SQLite 上并发打开连接时两个连接可能共用同一个原生连接，报 "cannot start a transaction within a transaction"，清池时正在打开的连接被释放、报 `ObjectDisposedException`。有了这条依赖，引用本包的应用解析到 10.0.13（连带 `Microsoft.Data.Sqlite.Core` 10.0.13 与 `SQLitePCLRaw` 2.1.13），不用自己钉版本；项目里直接引用了更低版本的，NuGet 报降级 `NU1605`，删掉那条引用或抬到 10.0.13。

方式一，特性按回调创建客户端，连接串来自绑定 `db`：

```csharp
using SimpleApp.Orders;
using SqlSugar;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.SqlSugar;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRepository<Order, Guid>();
builder.AddXiHan(x => x
    .UseStandardProfile()
    .UseFeature(new SqlSugarPersistenceFeature(static (binding, config) =>
    {
        config.DbType = DbType.PostgreSQL;
        config.ConnectionString = binding.ValueOrThrow().Value;
    })));
```

回调设置数据库类型与连接串，之后特性施加协议约定；默认连接的 `ConfigId` 由特性定为 `default`，回调里改了也以特性为准。

方式二，应用自己注册带多个连接的客户端（替换上例 `Program.cs` 里的装配）：

```csharp
using SqlSugar;
using XiHan.Framework.Persistence.SqlSugar;

builder.Services.AddScoped<ISqlSugarClient>(_ => new SqlSugarClient(
[
    // 每个连接都要先施加约定；默认连接用 SqlSugarConnections.Default 命名
    SqlSugarConventions.Apply(new ConnectionConfig { ConfigId = SqlSugarConnections.Default, DbType = DbType.PostgreSQL, ConnectionString = "Host=localhost;Database=app;Username=app", IsAutoCloseConnection = true }),
    SqlSugarConventions.Apply(new ConnectionConfig { ConfigId = "audit", DbType = DbType.PostgreSQL, ConnectionString = "Host=localhost;Database=audit;Username=app", IsAutoCloseConnection = true }),
]));
builder.AddXiHan(x => x.UseStandardProfile().UseFeature(new SqlSugarPersistenceFeature()));
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Persistence:SqlSugar:InitTables` | 布尔 | `false` | 启动时按已登记的实体建缺表、加缺列；只宜在开发与测试开启 |
| `XiHan:Persistence:SqlSugar:HealthProbeIntervalSeconds` | 正数 | `15` | 健康项的重探间隔，也是单次探测的超时 |
| `XiHan:Bindings:db` | 字符串 | — | 方式一的连接绑定主值 |
| `XiHan:Persistence:SqlSugar:Connections:<name>` | 字符串 | — | 方式一的命名连接，值是连接串（见下文「命名连接」）；方式二写了这一节装配即失败 |

```json
{
  "XiHan": {
    "Bindings": {
      "db": "Host=localhost;Database=app;Username=app"
    },
    "Persistence": {
      "SqlSugar": {
        "InitTables": true
      }
    }
  }
}
```

差异日志开关 `DiffLog` 不从配置读取，在特性上设置，见下文。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `SqlSugarPersistenceFeature` | 特性入口；无参构造用应用注册的客户端，带回调的构造由特性创建客户端；`DiffLog` 选出开启差异日志的实体；常量 `ConnectionBinding`（`db`）与 `HealthName`（`persistence.sqlsugar.database`） |
| `SqlSugarPersistenceOptions` | 选项：`InitTables`、`HealthProbeInterval`、`Connections`（方式一的命名连接）、`DiffLog`；`FromConfiguration(configuration)` |
| `SqlSugarConventions` | `Apply(config)` 在连接配置上登记协议约定（保留配置里已有的实体服务与表服务回调，先执行它们）；`Configure(property, column)` 对单个属性施加约定；`ConfigureIndexes(type, entityInfo)` 把实体上的 `[EntityIndex]` 登记为 SqlSugar 索引（软删除实体的部分唯一索引除外）；`InitTables(client, entityTypes)` 按约定建表并补建软删除实体的部分唯一索引，应用自己建表（租户独立库）用它 |
| `SqlSugarConnections` | 连接的 `ConfigId` 约定：`Default`（`default`）、`Shard`（`shard`） |
| `IEntityConnectionSelector`、`SqlSugarConnectionSelector` | 实体落在哪个连接；默认规则见下文 |
| `SqlSugarUnitOfWork` | 事务型工作单元；`Client` 是根连接，`ConnectionFor(Type)` 返回某种实体所用的连接；以 `UnitOfWorkOptions.Connection` 开启时只用那个命名连接 |
| `SqlSugarRepository<TEntity, TKey>` | 仓储实现，由特性按登记注册 |
| `SqlSugarDiffLogData` | 差异日志的业务数据：`EntityType` 与仓储所在作用域的 `Services` |
| `SqlSugarDataSource`、`SqlSugarDataSourceExtensions` | 运行时数据源：`Client` 是每个数据源一个的 `SqlSugarScope`；`source.GetSqlSugarClient()` 从中立的 `IDataSource` 取它 |

## 使用示例

### 多库落点

客户端可以带多个连接，每种实体落在哪个连接由 `IEntityConnectionSelector` 决定。默认实现 `SqlSugarConnectionSelector` 的规则按顺序：

1. 实体带 SqlSugar 的 `[Tenant(configId)]`：落到该 `ConfigId` 的连接；客户端里没有这个连接是装配错误，抛出携带 `PERSISTENCE_VALIDATION_004` 的 `FrameworkException`，不回退到默认连接。用缺省落点规则时，带 `[Tenant]` 的已登记实体在启动时就校验，连接不存在即中断启动；`shard` 只在带分片的调用里存在，不在启动时校验。
2. 实体以 `AddRepository<TEntity, TKey>(connection)` 登记到命名连接：落到同名 `ConfigId` 的连接，校验同上；与 `[Tenant]` 指向不同的连接同样是 `PERSISTENCE_VALIDATION_004`（见下文「命名连接」）。
3. 实体实现 `IMultiTenant`，且客户端里有 `shard` 连接：落到分片连接。
4. 其余：根（默认）连接。

方式一在装配了 `multitenancy.sharding@1` 时，带分片的调用把分片绑定（主值是租户连接串，属性含 `tenant`、`provider` 与透传属性）交给同一个回调，再建一个 `ConfigId = shard` 的连接，与默认连接组成一个客户端：租户实体落分片库，租户目录、授予、发件箱这类不带租户的平台表仍落默认库，同一调用里一起提交或回滚。方式二由应用自己组装连接集合，提供名为 `shard` 的连接即可让第 3 条规则生效。

把审计类实体固定到 `audit` 库（配合上面方式二的客户端）：

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

替换规则时，在装配特性之前注册自己的实现：`builder.Services.AddSingleton<IEntityConnectionSelector, MySelector>()`。同一工作单元里对同一实体类型的选择结果必须稳定。

### 命名连接

一部分平台数据要放在另一个库里、并在业务事务进行中另行提交时（RFC-0005「命名连接」，ADR-0084），用两样东西：实体登记到命名连接，管道外的写入开绑定那个连接的工作单元。典型场景是 SQLite 上的工作流租约表——SQLite 只有一个写者，业务事务写过之后，同一个库文件上的另一个事务要等它结束（见 [Workflow.Database](./workflow-database#sqlite-上的租约表)）。

方式一在配置里声明命名连接，回调对每个命名连接各调用一次：绑定的主值换成该连接串、属性沿用 `db` 绑定（`provider` 等），`config.ConfigId` 是连接名，回调据此区分：

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
// 应用自己的实体也可以登记到命名连接；框架的工作流租约表由 LockConnection 登记
builder.Services.AddRepository<SyncCursor, string>(connection: "leases");

// 管道外与业务事务互不等待的写入：工作单元绑定这个连接，事务只开在它上面
await using var scope = services.CreateAsyncScope();
var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
await unitOfWork.BeginAsync(new UnitOfWorkOptions(Connection: "leases"), ct);
// …经 IRepository<SyncCursor, string> 读写，然后 CommitAsync
```

- 名字合 `^[a-z0-9][a-z0-9-]{0,63}$`，不能是 `default` 或 `shard`；值不能空白。不合法时装配即抛 `ArgumentException`（消息只带名字，不带连接串）。
- 每个作用域的客户端都带上命名连接，排在默认连接之后，根连接仍是默认连接。工作单元首次写入时只在默认连接与已经取用过的连接上开事务，此后首次取用的连接再补开：业务调用不碰命名连接，那个库就不被业务事务锁住。
- 普通工作单元里，登记到命名连接的实体照常参与整体事务（同一调用触及的连接顺序提交）；`InitTables` 把它建在那个连接上。
- 绑定连接的工作单元（`UnitOfWorkOptions(Connection: name)`）只用这一个连接：操作落在别处的实体抛 `PERSISTENCE_VALIDATION_004`；连接不在客户端里，`BeginAsync` 抛出同码异常。
- 方式二由应用把同名 `ConfigId` 的连接加进自己注册的客户端，配置里的 `Connections` 节对它不适用，写了即装配失败。

::: warning 跨库提交是顺序提交，不是两阶段提交
一次调用写了多个连接时，提交逐连接进行；后一个连接提交失败时，前一个已经提交。需要严格原子性的写入放在同一个库。
:::

### 分表

带 SqlSugar `[SplitTable]` 的实体自动走分表入口：表名含 `{year}{month}{day}` 占位符，`[SplitField]` 是按 UTC 写入的 `DateTime` 列（SqlSugar 按 `DateTime` 推表名，不能用 `DateTimeOffset`）。

```csharp
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using XiHan.Framework.Domain.Entities;

namespace SimpleApp.Auditing;

[SugarTable("login_logs_{year}{month}{day}")]
[SplitTable(SplitType.Month)]
public sealed class LoginLog : IEntity<Guid>, IHasCreationTime, IHasConcurrencyStamp
{
    public Guid Id { get; init; }

    // 决定落在哪张分表；写入后不要再改，更新与删除按它定位表
    [SplitField]
    public DateTime LoggedAt { get; set; }

    [StringLength(256)]
    public string UserName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public string ConcurrencyStamp { get; set; } = string.Empty;
}
```

查询用 `Within` 限定扫描的表，没有提示时扫全部分表：

```csharp
var october = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
// 两端都含：只扫 2026 年 10 月那张表；范围是扫描提示，不是过滤条件
var query = EntityQuery.All<LoginLog>().Within(october, october.AddMonths(1).AddTicks(-1));
var logs = await loginLogs.ListAsync(query, cancellationToken);
```

`loginLogs` 是注入的 `IRepository<LoginLog, Guid>`。写入按 `[SplitField]` 的值落表，缺少的分表由 SqlSugar 建出；`InitTables` 对分表实体走 `CodeFirst.SplitTables()`。分表查询是多表 `UNION ALL`，不能加行锁，分表实体的更新与删除靠并发戳防并发。

### 差异日志

`DiffLog` 选出的实体，仓储的插入、更新（含软删除）、物理删除与批量方法都带 SqlSugar 的 `EnableDiffLogEvent`，应用在连接配置的 `AopEvents.OnDiffLogEvent` 上收到操作类型、表、主键与前后值，`BusinessData` 是 `SqlSugarDiffLogData`：

```csharp
using Microsoft.Extensions.DependencyInjection;
using SimpleApp.Orders;
using SqlSugar;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Persistence.SqlSugar;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

var feature = new SqlSugarPersistenceFeature(static (binding, config) =>
{
    config.DbType = DbType.PostgreSQL;
    config.ConnectionString = binding.ValueOrThrow().Value;
    config.AopEvents = new AopEvents
    {
        OnDiffLogEvent = static diff =>
        {
            if (diff.BusinessData is SqlSugarDiffLogData data)
            {
                // 回调在写入后同步触发、不等提交：只复制需要的值进本调用的缓冲，提交成功后再落库
                data.Services.GetRequiredService<ChangeBuffer>().Add($"{data.EntityType.Name}:{diff.DiffType}");
            }
        },
    };
})
{
    // 只给需要前后值的实体开：每次写入多一次按主键的读取
    DiffLog = static type => type == typeof(Order),
};

builder.Services.AddScoped<ChangeBuffer>();
builder.AddXiHan(x => x.UseStandardProfile().UseFeature(feature));

public sealed class ChangeBuffer
{
    private readonly List<string> _entries = [];

    public IReadOnlyList<string> Entries => _entries;

    public void Add(string entry) => _entries.Add(entry);
}
```

缺省一个实体都不开。开启的实体走 SqlSugar 的泛型构建器，必须有公开的无参构造函数。分表实体同样支持：分表删除由框架按 `[SplitField]` 分组到表逐表删除，因为 SqlSugar 自己的分表删除会丢掉差异日志开关。SqlSugar 在分表插入的各组之间复用同一个 `DiffLogModel`，回调里不要保存它的引用。

## 行为与限制

协议约定（`SqlSugarConventions`）：

| 约定 | 内容 |
| --- | --- |
| 主键 | 名为 `Id` 的属性是主键；是否自增沿用实体自己的 `[SugarColumn(IsIdentity)]`，缺省不自增 |
| 并发戳 | `ConcurrencyStamp` 长度 32 |
| 字符串长度 | `[StringLength(n)]` 或 `[MaxLength(n)]` → 长度 n；`[LongText]` 或无参 `[MaxLength]` → 长文本类型；实体自己的 `[SugarColumn]` 已给长度或类型时不动；没有注解的沿用 SqlSugar 缺省（PostgreSQL 为 `varchar(255)`） |
| `DateTimeOffset` | 经转换器按 UTC 时刻往返；读取按驱动交出的 `DateTime.Kind` 还原（ADR-0037） |
| 可空 | 可空值类型与可空引用类型的列可空 |
| 忽略 | 没有公开 setter 的属性与集合属性（字符串、字节数组除外）不映射 |
| PostgreSQL 缺省值 | 非可空的数值、枚举、布尔与 `Guid` 列配 CLR 缺省值（`0`、`false`、全零 Guid），`InitTables` 才能给已有行的表加这类列（ADR-0040） |
| 索引 | 实体上的 `[EntityIndex]` 经表服务回调（`EntityNameService`）转成 `SugarIndexAttribute`，名字按映射出的表名推出（ADR-0072）；`InitTables` 按名字查不到才建，已有的不改不删；分表实体声明它抛 `NotSupportedException` |
| 部分唯一索引 | 软删除实体的唯一索引只约束未删除的行（ADR-0081）：不交给 SqlSugar（它写不出条件），由 `SqlSugarConventions.InitTables` 建表后补建——PostgreSQL / SQLite `CREATE UNIQUE INDEX IF NOT EXISTS … WHERE IsDeleted = false / 0`，SQL Server 筛选索引（键含可空列另加 `IS NOT NULL`）；只支持这三种数据库，其余（MySQL、Oracle、达梦、源自 PostgreSQL 而未验证的国产库等）上启动即失败（`PERSISTENCE_UNAVAILABLE_008`），列里显式写 `IsDeleted` 的普通唯一索引不受限制 |
| 字符串匹配 | `string.Contains` / `StartsWith` / `EndsWith` 在 SQLite、PostgreSQL、MySQL、SQL Server、Oracle 上译成 `LIKE … ESCAPE '!'`（ADR-0098）：关键字是值时参数值先转义再拼通配符，SQL 里只有参数名；是列或表达式时在 SQL 里用 `REPLACE` 转义。转义 `%`、`_`、`!`，SQL Server 另转义 `[`。转义符不用反斜杠，结果与 MySQL 的 `NO_BACKSLASH_ESCAPES`、PostgreSQL 的 `standard_conforming_strings` 无关（MySQL 开了 `NO_BACKSLASH_ESCAPES` 时连接串要写 `NoBackslashEscapes=true`，这是 MySqlConnector 对所有参数的要求）。大小写照各库：SQLite 只对 ASCII 字母不区分，PostgreSQL 与 Oracle 区分，MySQL 与 SQL Server 按排序规则；`MoreSettings.EnableILike` 照原样用 `ilike`。要通配用 `SqlFunc.Like`。其余数据库（TiDB、OceanBase、openGauss、人大金仓、达梦等兼容库）没有在真实库上验证，维持 SqlSugar 原来的翻译、按通配匹配，验证之后再加进名单；运行时数据源不施加约定，同样按通配匹配（这两条已定，ADR-0101）；应用在 `Apply` 之前自己登记了同名外部函数的以应用的为准 |
| PostgreSQL 会话 | 连接串（含从库）的启动参数 `Options` 追加 `-c standard_conforming_strings=on`（ADR-0100）：SqlSugar 在多行插入、批量更新、按实体删除、`In`、集合 `Contains` 里内联的字符串字面量只在 on 时正确，库或角色设成 off 时含反斜杠的值会让语句报错或被静默改掉、按主键的更新删除查找静默漏行。服务器在建连时施加它，优先于库与角色的缺省值；连接串已显式写成 on 以外的值时 `Apply` 抛 `ArgumentException`。经这个配置的全部连接（含应用的原生 SQL）都是 on；会话里自己 `SET … = off` 的不支持；PgBouncer 要 1.20 及以上；PostgreSQL 系其他 `DbType` 与运行时数据源不加 |
| 作用范围 | 只管实体：声明在 `System.*` 类型上的属性原样交还 |

- `InitTables` 建缺表、加缺列，按落点分组：启动时没有调用也就没有分片，租户实体在默认连接建表；带 `[Tenant]` 或登记到命名连接的实体在各自的连接上建表。删列、改名、改类型不在范围内，生产库的结构演进走迁移脚本。给已有行的表加非可空列：PostgreSQL 上数值 / 枚举 / 布尔 / Guid 列由约定回填缺省值，代价是此后每次 `InitTables` 都对这些列重发一次 `SET DEFAULT`；SQLite 由 SqlSugar 加成可空列；非可空字符串、日期时间列与其他数据库不受支持，新列声明成可空，或重建开发库、走迁移脚本。
- 行锁：更新与删除的读取带 `TranLock`（`FOR UPDATE` / `UPDLOCK`）；SQLite 不支持行锁，由它的数据库级写锁保证；分表实体不加行锁。
- 游离实例更新：并发戳与当前行不同返回 `PERSISTENCE_CONFLICT_001`；通过后把调用方的实例整行写入，创建时间、租户与删除标记取当前行的值，调用方实例上的这几个值不生效，写入后调用方实例拿到它们。按主键更新影响 0 行（行在加锁前已被删除）也返回 `PERSISTENCE_CONFLICT_001`。
- 并发新增同一标识：检查之后别的事务提交了同一标识，PostgreSQL 在插入时等对方提交后报主键冲突。PostgreSQL 系语句失败后整个事务进入中止状态，仓储另开一个不在事务里的连接（`CopyNew`，同一份连接配置）核对这些标识：确已存在就返回 `PERSISTENCE_CONFLICT_002`，批量时这次写入的项统一报告；否则异常原样抛出。之后工作单元只能回滚：处理器忽略失败返回成功时，提交回滚并返回同一错误，不会对一个已中止的事务执行 `COMMIT`（PostgreSQL 会静默回滚，调用方看到的成功是假的）。
- 唯一键重复：新增、更新、软删除的写入失败又不是标识重复时，同样另开连接核对实体的唯一索引（这次写入里两项同键，或已提交的另一行同键；软删除实体的部分唯一索引只比较未删除的行），撞上返回 `PERSISTENCE_CONFLICT_003`（参数 `index` 是索引名），这次写入的项统一报告，工作单元随后只能回滚；核对不到的（与本工作单元先前未提交的行冲突、大小写不敏感排序规则下只差大小写、键含空值）异常原样抛出。表里已有重复数据时 `InitTables` 建不出唯一索引、启动失败，先去重。
- 软删除：在取回的当前行上打标并整行写回，调用方实例随后拿到删除标记，以及当前行的修改时间与并发戳。
- 实体类型没有 `new()` 约束，写入经 `InsertableByObject` / `UpdateableByObject` / `DeleteableByObject`，语义与泛型构建器相同。
- 不提供按查询条件的批量更新 / 删除（`Updateable().SetColumns().Where()`）：它绕过审计、并发戳与软删除协议；需要时应用直接用客户端。
- 只读工作单元（只读操作或 `UnitOfWorkOptions(IsReadOnly: true)`）不开事务：仓储写入返回 `PERSISTENCE_UNEXPECTED_001` 并把工作单元记为失败，处理器忽略这次失败时提交仍返回它；直接用客户端写入（`Insertable` / `Updateable` / `Deleteable`）之前调用的 `EnsureTransactionAsync` 抛出携带它的 `FrameworkException`（Transaction 阶段转为同码结果）。不经 `EnsureTransactionAsync` 直接执行的写入不受守卫，也不在事务里。
- 作用域结束时事务仍未提交（中间件之外直接使用），释放即回滚。
- 运行时数据源（ADR-0078）：特性登记 SqlSugar 的数据源 Provider，数据库类型是 `DbType` 的名字（不区分大小写，`Custom` 与数字不认）。登记时由驱动解析连接串，格式错即 `PERSISTENCE_VALIDATION_006`；客户端是每个数据源一个的 `SqlSugarScope`（`IsAutoCloseConnection`），不施加协议约定、不挂过滤器与 AOP、不进工作单元，PostgreSQL 上不自动转小写；测试连接与健康探测另建客户端、打开原生 `DbConnection` 再关闭。运行时数据源不加进作用域客户端，`default` / `shard` 与实体落点不受影响。清单的配置节写 `XiHan:Persistence`（读 `XiHan:Persistence:SqlSugar` 与 `XiHan:Persistence:DataSources`）。
- 库结构读取与实体目录（ADR-0078）：读表与读列经 `DbMaintenance`（同步 API）；缺省连接 `CopyNew` 另开不在事务里的连接，运行时数据源按定义登记后另建客户端；分表按已登记实体的模板折叠；驱动与 SqlSugar 的异常翻译为 `PERSISTENCE_UNAVAILABLE_006`。实体目录取 `EntityMaintenance.GetEntityInfo`。SQLite 没有注释，`INTEGER PRIMARY KEY` 报告可空（按主键改为不可空）；PostgreSQL 的注释、identity、serial 缺省值与加引号的大小写混合表名都能读到。MySQL、SQL Server、Oracle 在真实库上实测过（ADR-0084）：注释、自增、主键、缺省值原文、大小写混合的表名、分表折叠与分页总数都对；长度只给字符与二进制类型（`tinyint(1)`、`datetime(6)`、SQL Server `int` 的 10 这类显示宽度或精度不算长度）。Oracle 的读列不走 SqlSugar（它给 CLR 类型名、读不到 identity、缺省值错位、大小写混合的表名查不到），改查连接用户自己的数据字典，要 Oracle 12c 及以上；运行时数据源在 Oracle 上不自动转大写。不支持（已定，ADR-0087）：Oracle 11g 及更早的版本读列以 `PERSISTENCE_UNAVAILABLE_006` 失败（数据字典没有 `IDENTITY_COLUMN`），不做 11g 的分支；达梦、OceanBase Oracle 模式等 Oracle 兼容库走 SqlSugar 原来的读列（类型名可能是 CLR 类型名，identity、缺省值、大小写混合的表名可能读不对），没有验证，读到的结构不作保证。
- MySQL 上的已知限制（SqlSugar 上游缺陷，已报上游，待上游修复；ADR-0100 修订）：两个及以上值的 `In(…)`、按实体删除（单个实体也是）与 `Deleteable<T>().In(…)` 把字符串内联成只转义单引号的字面量，集合 `Contains` 则把反斜杠写成两个。MySQL 缺省 sql_mode 下前一组对含反斜杠的值静默漏行（值里有 `\'` 时报语法错误），开了 `NO_BACKSLASH_ESCAPES` 时后一组静默漏行；逐个 `==`、只有一个值的 `In`、单行写入与多行插入用参数，两种模式都对。仓储里受影响的是 `FindManyAsync`（两个及以上标识）、`AddRangeAsync` 的标识预查、`UpdateRangeAsync` / `RemoveRangeAsync` 的加锁取回、物理删除的 `RemoveAsync` / `RemoveRangeAsync`（缺省模式），以及规约里的集合 `Contains`（`NO_BACKSLASH_ESCAPES`）。框架自己的字符串主键（租户、调度、工作流定义）是不含反斜杠与引号的受限标识符，Guid 与整数主键也不受影响；框架不在仓储里绕开。应用的字符串主键首选不含反斜杠（写入前按 `^[A-Za-z0-9][A-Za-z0-9._-]*$` 这类规则校验）；做不到时改用参数化的写法：

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

- 健康项 `persistence.sqlsugar.database`：启动时同步探一次，之后按间隔在后台重探并缓存结果；探测在独立作用域里对默认连接 `Ado.IsValidConnection()`，不开工作单元、不经管道。连不上为 Degraded（`PERSISTENCE_UNAVAILABLE_003`），应用仍在服务。

## 相关

- [持久化](../guide/persistence)
- [工作单元与事务](../guide/uow)
- [多租户](../guide/multi-tenancy)
- [审计](../guide/auditing)
- [Persistence.Abstractions](./persistence-abstractions)
- [Persistence.EntityFrameworkCore](./persistence-entityframeworkcore)
- 规范：RFC-0005《领域协议与持久化》
- 决策：ADR-0013、ADR-0022、ADR-0023、ADR-0035、ADR-0036、ADR-0037、ADR-0040、ADR-0044、ADR-0078、ADR-0098、ADR-0100（含 MySQL 已知限制的修订）、ADR-0101
