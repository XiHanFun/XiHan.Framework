来源：https://framework.docs.xihanfun.com/persistence/sqlsugar

# SqlSugar Provider

这一页讲 `XiHan.Framework.Persistence.SqlSugar`：两种创建客户端的方式、`SqlSugarConventions.Apply` 施加了哪些约定、`InitTables` 建表、多库落点与命名连接、按时间分表、读写分离、差异日志、命令超时，以及可空值类型判空与 PostgreSQL 的两个注意事项。读完它，你能把应用切到 SqlSugar，并用上它独有的多库与分表能力。前置知识是[持久化与 Provider](/persistence/overview)与[工作单元与事务](/persistence/unit-of-work)。

## 为什么需要

已经用 SqlSugar 的项目不该为了接入框架推倒重来；有些场景 EF Core 也做不到：一次调用既写平台表又写租户分片库、日志类实体按月分表、写入时拿到前后值做差异日志、读走从库写走主库、运行时按记录连外部库。SqlSugar Provider 以作用域内的 `ISqlSugarClient` 实现仓储与事务型工作单元，实体不继承 SqlSugar 基类、不必标 `[SugarColumn]`，映射由协议约定推出；原生查询继续用自己的客户端。

## 概念

| 项 | 内容 |
| --- | --- |
| 包 | `XiHan.Framework.Persistence.SqlSugar`，依赖 `SqlSugarCore`（自带各数据库驱动）与 `Microsoft.Data.Sqlite`；应用改用 `SqlSugarCoreNoDrive` 加单个驱动时要整体替换 |
| 能力 | 提供 `persistence.relational@1`（`role: provider`）；依赖 `kernel.hosting@1`，可选依赖 `multitenancy.sharding@1` |
| 特性入口 | `SqlSugarPersistenceFeature`：无参构造用应用注册的客户端，带回调的构造 `Action<Maybe<BindingValue>, ConnectionConfig>` 由特性创建客户端；`DiffLog` 选出开启差异日志的实体 |
| 绑定与配置 | 绑定 `db`（`connection:relational`，可选）；配置节 `XiHan:Persistence:SqlSugar` |
| 健康项 | `persistence.sqlsugar.database`、`persistence.sqlsugar.replicas` |
| Native AOT | 不兼容：SqlSugar 的实体映射与表达式翻译基于运行时反射 |
| 主要类型 | `SqlSugarConventions`（`Apply`、`InitTables`）、`SqlSugarConnections`（`Default`、`Shard`）、`IEntityConnectionSelector`、`SqlSugarUnitOfWork`（`Client`、`ConnectionFor(Type)`、`EnsureTransactionAsync`）、`SqlSugarDiffLogData` |

## 最小示例

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

var app = builder.Build();
app.UseXiHan();
app.Run();
```

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

回调设置数据库类型与连接串，之后特性施加协议约定；默认连接的 `ConfigId` 由特性定为 `default`。运行后 `InitTables` 按已登记的实体建出 `Order` 表，`POST /orders` 的写入进入 PostgreSQL，操作代码与内存实现时完全相同。

方式二，应用自己注册带多个连接的客户端（替换上例里的装配），每个 `ConnectionConfig` 在创建客户端前先经 `SqlSugarConventions.Apply`，默认连接的 `ConfigId` 用 `SqlSugarConnections.Default`：

```csharp
builder.Services.AddScoped<ISqlSugarClient>(_ => new SqlSugarClient(
[
    SqlSugarConventions.Apply(new ConnectionConfig { ConfigId = SqlSugarConnections.Default, DbType = DbType.PostgreSQL, ConnectionString = appDb, IsAutoCloseConnection = true }),
    SqlSugarConventions.Apply(new ConnectionConfig { ConfigId = "audit", DbType = DbType.PostgreSQL, ConnectionString = auditDb, IsAutoCloseConnection = true }),
]));
builder.AddXiHan(x => x.UseStandardProfile().UseFeature(new SqlSugarPersistenceFeature()));
```

两种方式下仓储、工作单元与查询语义相同。方式二要自己保证每个作用域用一份配置副本（`SqlSugarScope` 自己会复制），`CommandTimeoutSeconds` 与 `Connections` 配置节对它不起作用，框架也不探测它的从库。

## 协议约定

`SqlSugarConventions.Apply` 在连接配置上登记的约定（保留配置里已有的实体服务与表服务回调，先执行它们）：

- 名为 `Id` 的属性是主键，缺省不自增；`ConcurrencyStamp` 长度 32；`[StringLength(n)]` 映射为长度 n，`[LongText]` 映射为长文本类型，实体自己的 `[SugarColumn]` 已给长度或类型时不动。
- `DateTimeOffset` 经转换器按 UTC 时刻往返；可空类型的列可空；没有公开 setter 的属性与集合属性不映射。
- `[EntityIndex]` 经表服务回调转成 `SugarIndexAttribute`；软删除实体的唯一索引由 `InitTables` 建表后补建成部分唯一索引。
- 字符串的 `Contains` / `StartsWith` / `EndsWith` 以同名外部函数接管翻译，按字面匹配，直接用施加了约定的客户端写的查询也一样；要通配用 `SqlFunc.Like`。
- PostgreSQL 连接串的启动参数追加 `standard_conforming_strings=on`，见下文。

软删除与租户过滤以表达式显式施加在每个查询上，不用 SqlSugar 的全局过滤器。列与索引的完整约定见[建表约定与索引](/persistence/schema)。

## 建表：InitTables

`XiHan:Persistence:SqlSugar:InitTables` 为 `true` 时，启动按已登记的实体建缺表、加缺列，经 `SqlSugarConventions.InitTables`，软删除实体的部分唯一索引随之建出。按落点分组：租户实体在默认连接建表（启动时没有分片），带 `[Tenant]` 或登记到命名连接的实体在各自的连接上建表；分表实体走 `CodeFirst.SplitTables()`。删列、改名、改类型不在范围内，生产库的结构演进走迁移脚本；索引按名字查不到才建，表里已有重复数据时唯一索引建不出来、启动失败。应用自己建表（租户独立库、迁移工具）也要调 `SqlSugarConventions.InitTables(client, entityTypes)`，直接调 `CodeFirst.InitTables` 建不出部分唯一索引。

## 多库落点

客户端可以带多个连接，每种实体落在哪个连接由 `IEntityConnectionSelector` 决定。默认实现 `SqlSugarConnectionSelector` 的规则按顺序：

1. 实体带 SqlSugar 的 `[Tenant(configId)]`：落到该 `ConfigId` 的连接；客户端里没有这个连接是装配错误（`PERSISTENCE_VALIDATION_004`），不回退到默认连接。
2. 实体以 `AddRepository<TEntity, TKey>(connection)` 登记到命名连接：落到同名 `ConfigId` 的连接，校验同上；与 `[Tenant]` 不一致同样是 `PERSISTENCE_VALIDATION_004`。
3. 实体实现 `IMultiTenant`，且客户端里有 `shard` 连接：落到分片连接。
4. 其余：根（默认）连接。

装配了 `multitenancy.sharding@1` 时，带分片的调用把分片绑定交给同一个回调，再建一个 `ConfigId = shard` 的连接：租户实体落分片库，租户目录、授予、发件箱这类平台表仍落默认库，同一调用里一起提交或回滚。方式二由应用自己提供名为 `shard` 的连接。见[租户分片与分库](/multi-tenancy/sharding)。

把审计类实体固定到 `audit` 库（配合上面方式二的客户端），实体类上标 SqlSugar 的 `[Tenant("audit")]` 即可，其余照常实现协议接口。替换规则时，在装配特性之前注册自己的实现：`builder.Services.AddSingleton<IEntityConnectionSelector, MySelector>()`。同一工作单元里对同一实体类型的选择结果必须稳定。工作单元在第一次写入时经 `AsTenant()` 在客户端的全部连接上开事务，提交逐连接提交，回滚逐连接回滚。

::: warning 跨库提交是顺序提交，不是两阶段提交
一次调用写了多个连接时，提交逐连接进行；后一个连接提交失败时，前一个已经提交。需要严格原子性的写入放在同一个库。
:::

### 命名连接

方式一在 `XiHan:Persistence:SqlSugar:Connections:<name>` 声明命名连接，回调对每个命名连接各调用一次，`config.ConfigId` 是连接名；方式二由应用把同名 `ConfigId` 的连接加进自己注册的客户端。每个作用域的客户端都带上命名连接，工作单元首次写入时只在默认连接与已经取用过的连接上开事务，此后首次取用的连接再补开：业务调用不碰命名连接，那个库就不被业务事务锁住。普通工作单元里，登记到命名连接的实体照常参与整体事务（与 EF Core 不同）；绑定连接的工作单元（`UnitOfWorkOptions { Connection = name }`）只用这一个连接，操作落在别处的实体抛 `PERSISTENCE_VALIDATION_004`。配置与代码示例见[连接与运行时数据源](/persistence/connections)。

## 分表

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

    public DateTimeOffset CreatedAt { get; set; }

    public string ConcurrencyStamp { get; set; } = string.Empty;
}
```

查询用 `EntityQuery.All<LoginLog>().Within(from, to)` 限定扫描的表（两端都含，只是扫描提示、不过滤行），没有提示时扫全部分表。写入按 `[SplitField]` 的值落表，缺少的分表由 SqlSugar 建出。分表查询是多表 `UNION ALL`，不能加行锁，分表实体的更新与删除靠并发戳防并发。分表实体不用 `[EntityIndex]`，直接用 `[SugarIndex]` 与 `{split_table}` 占位符。

## 读写分离

从库在配置回调里给，回调对每个连接都调用，所以要看 `ConfigId`，只给它所属的连接：

```csharp
new SqlSugarPersistenceFeature((binding, config) =>
{
    config.DbType = DbType.PostgreSQL;
    config.ConnectionString = binding.ValueOrThrow().Value;
    if (Equals(config.ConfigId, SqlSugarConnections.Default))
    {
        config.SlaveConnectionConfigs =
        [
            new SlaveConnectionConfig { ConnectionString = replicaA, HitRate = 10 },
            new SlaveConnectionConfig { ConnectionString = replicaB, HitRate = 10 },
        ];
    }
});
```

`HitRate` 是字段，配置绑定绑不上，从配置读时自己赋值；不大于 0 的权重按 `Replicas:DefaultHitRate`（缺省 10）归一化。

只读工作单元（动词 `get`、`list`，或 `UnitOfWorkOptions { IsReadOnly = true }`）的读走从库，设了 `ReadsPrimary` 的读主库；其余工作单元的读写都走主库，第一次写入之前的读也一样。工作单元之外直接用客户端时，事务内与写入走主库，事务外的读走从库。分片连接默认没有从库。跨调用读己之写不保证：写完马上发起的只读调用可能读到落后的从库，框架读授予和租户目录时因此读主库。

周期探测把不可读的从库摘除，全部不可读时读回到主库，恢复之后过了冷却窗口才放回读流量；健康项 `persistence.sqlsugar.replicas` 有从库不可读时是 Degraded（`PERSISTENCE_UNAVAILABLE_009`），应用仍然就绪，只探连通、不看复制延迟。主库不同的连接共用同一个从库是装配错误（`PERSISTENCE_VALIDATION_009`）：回调不看 `ConfigId` 时，租户的读会读到平台库。

## 差异日志

`DiffLog` 选出的实体，仓储的插入、更新（含软删除）、物理删除与批量方法都带 SqlSugar 的 `EnableDiffLogEvent`，应用在连接配置的 `AopEvents.OnDiffLogEvent` 上收到操作类型、表、主键与前后值，`BusinessData` 是 `SqlSugarDiffLogData`：

```csharp
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
```

`ChangeBuffer` 是应用自己的作用域服务；`feature` 照常 `UseFeature`。缺省一个实体都不开；开启的实体必须有公开的无参构造函数。回调在写入执行后同步触发、不等提交，事务回滚由应用按调用缓冲后再落库。分表实体同样支持，但回调里不要保存 `DiffLogModel` 的引用（分表插入的各组之间复用同一个）。审计子系统本身见[审计](/common/auditing)。

## 命令超时

`XiHan:Persistence:SqlSugar:CommandTimeoutSeconds`（缺省 300）设在特性建的客户端的每个连接上；不是正整数即启动失败，0 不接受（ADO.NET 的 0 是无限等待）。方式二要在自己的客户端上设 `Ado.CommandTimeOut`，配了这个键启动失败。个别长任务在调用点用 `Ado.CommandTimeOut` 临时调大。

## 可空值类型判空

规约在交给 SqlSugar 前先改写：可空值类型与 `null` 的 `==` 改写成 `SqlFunc.EqualsNull(x, null)`，`!=` 改写成 `HasValue`，避免译成 `= NULL` 后静默返回错误的结果；逻辑非下推到叶子（德摩根律展开、`!(a < b)` 换成 `a >= b`），因为 SqlSugar 在 `A && (!x.HasValue || B)` 这类位置会漏写 `OR` / `AND`。应用直接用客户端写查询时没有这层改写，判空要自己写 `SqlFunc.EqualsNull` 或 `HasValue`。

## PostgreSQL 注意事项

### standard_conforming_strings

SqlSugar 在多行插入、批量更新、按实体删除、`In`、集合 `Contains` 里内联的字符串字面量只在 `standard_conforming_strings = on` 时正确；库或角色设成 off 时含反斜杠的值会让语句报错或被静默改掉。`SqlSugarConventions.Apply` 在连接串（含从库）的启动参数 `Options` 追加 `-c standard_conforming_strings=on`，服务器在建连时施加它，优先于库与角色的缺省值；连接串已显式写成别的值时 `Apply` 抛 `ArgumentException`。经这个配置的全部连接（含应用的原生 SQL）都是 on，原生 SQL 里要转义用 `E'…'`。PgBouncer 要能透传启动参数的版本；运行时数据源不加这个参数。

### 时间戳的 Kind

SqlSugar 的 PostgreSQL 提供器自己打开 Npgsql 的 legacy 时间戳模式，`timestamptz` 列读出来是换算成本地时间、`Kind = Local` 的 `DateTime`。约定的 `DateTimeOffset` 转换器按 Kind 处理：`Local` 先换回 UTC，`Unspecified` 按写入时的 UTC 解释，最终都交出偏移为零的 `DateTimeOffset`。应用直接用客户端读 `DateTime` 列时没有这层转换，要自己看 Kind：把 `Local` 值直接当 UTC 会多出本地时区偏移，经实体回写后逐次累加。

## MySQL 的已知限制

MySQL 上 SqlSugar 对两个及以上值的 `In(…)` 与按实体删除把字符串内联成只转义单引号的字面量，缺省 sql_mode 下含反斜杠的字符串主键会静默漏行（开了 `NO_BACKSLASH_ESCAPES` 时集合 `Contains` 反过来漏行）。仓储里受影响的是 `FindManyAsync`、批量方法的标识预查与加锁取回、物理删除；Guid 与整数主键不受影响。应用的字符串主键首选不含反斜杠（写入前按 `^[A-Za-z0-9][A-Za-z0-9._-]*$` 这类规则校验），做不到时改用参数化写法（逐个 `==` 比较、`FindAsync`）。这是 SqlSugar 上游缺陷，待上游修复。

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Persistence:SqlSugar:InitTables` | 布尔 | `false` | 启动时建缺表、加缺列；只宜在开发与测试开启 |
| `XiHan:Persistence:SqlSugar:HealthProbeIntervalSeconds` | 正数 | `15` | 健康项的重探间隔，也是单次探测的超时 |
| `XiHan:Persistence:SqlSugar:Connections:<name>` | 字符串 | — | 方式一的命名连接；方式二写了这一节装配即失败 |
| `XiHan:Persistence:SqlSugar:CommandTimeoutSeconds` | 正整数 | `300` | 命令超时（秒） |
| `XiHan:Persistence:SqlSugar:Replicas:DefaultHitRate` | 正整数 | `10` | 权重不大于 0 的从库归一化成的权重 |
| `XiHan:Persistence:SqlSugar:Replicas:HealthCheck`、`HealthCheckIntervalSeconds`、`FailureCooldownSeconds` | 布尔、正数、非负数 | `true`、`30`、`120` | 是否周期探测从库、探测间隔、最后一次失败后恢复读流量前的冷却窗口 |
| `XiHan:Bindings:db` | 字符串 | — | 方式一的连接绑定主值 |

健康项 `persistence.sqlsugar.database` 对默认连接走一次数据库往返，连不上或超时为 Degraded（`KERNEL_UNAVAILABLE_005`），应用仍在服务。

## 常见问题

### 原有的 SqlSugar 实体要改成什么样

实现 `IEntity<TKey>` 并把主键属性命名为 `Id`，其余 `[SugarTable]`、`[SugarColumn]` 照旧。想让仓储维护创建时间、租户、软删除就实现对应的协议接口；不实现的字段仓储不碰。原生查询继续用自己的客户端。

### 更新与删除为什么要先读一遍

仓储在事务内加行锁取回当前行（`TranLock`），比对并发戳后把调用方的实例整行写入，其中创建时间、租户与删除标记先换成当前行的值，保证读取与写入之间别人改不了这一行、协议字段不被覆盖。SQLite 不支持行锁，由数据库级写锁保证；分表实体不加行锁。

### 从库有延迟，刚写的数据读不到

只读调用走从库，跨调用的读己之写不保证。需要立即读到刚写数据的只读路径，开工作单元时设 `ReadsPrimary = true`；或者把读写放进同一个可写调用。

## 相关包与下一步

- 相关包：[Persistence.SqlSugar](/packages/persistence-sqlsugar)、[Persistence.Abstractions](/packages/persistence-abstractions)、[Persistence.EntityFrameworkCore](/packages/persistence-entityframeworkcore)、[Persistence.Generators](/packages/persistence-generators)
- 规范：RFC-0005《领域协议与持久化》
- 下一页：[建表约定与索引](/persistence/schema)，讲列的约定、索引声明与生成规则
