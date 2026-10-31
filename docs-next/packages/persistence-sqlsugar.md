# XiHan.Framework.Persistence.SqlSugar

`persistence.relational@1` 的 SqlSugar Provider：以作用域内的 `ISqlSugarClient` 实现仓储与事务型工作单元，实体不继承 SqlSugar 基类、不必标注 `[SugarColumn]`，映射由协议约定推出。面向已有 SqlSugar 代码的应用，并支持一次调用写多个库、按时间分表与差异日志。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Persistence.SqlSugar` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `persistence.relational@1`（`role: provider`）；依赖 `kernel.hosting@1`，可选依赖 `multitenancy.sharding@1`；绑定 `db`（`connection:relational`，可选） |
| 特性入口 | `SqlSugarPersistenceFeature` |
| 配置节 | `XiHan:Persistence:SqlSugar` |
| 依赖 | `XiHan.Framework.Persistence.Abstractions`、`XiHan.Framework.MultiTenancy.Abstractions`、`SqlSugarCore` 5.1.4.221（自带各数据库驱动） |
| AOT | 不兼容（SqlSugar 的实体映射与表达式翻译基于运行时反射） |
| 稳定性 | Preview |

## 概述

客户端有两种来源。方式一由特性按配置回调创建作用域内的 `SqlSugarClient`，自动施加 `SqlSugarConventions`；方式二由应用自己注册 `ISqlSugarClient`（`SqlSugarScope`、多库等），应用在创建客户端前对每个 `ConnectionConfig` 调用 `SqlSugarConventions.Apply`。两种方式下仓储、工作单元与查询语义相同。

SqlSugar 没有变更跟踪，仓储的每次写入立即执行。工作单元在第一次写入时经 `AsTenant()` 在客户端的全部连接上开事务，只读调用不开事务；提交逐连接提交，回滚逐连接回滚。更新与删除先在事务内加行锁取回当前行（`TranLock`），比对并发戳后把调用方的实例整行写入，读取与写入之间别人改不了这一行（SQLite 与分表实体不加行锁，见下文）。批量方法用一次主键 `IN` 查询取回目标行，逐项校验后一次多行插入、更新或删除。

软删除与租户过滤以表达式显式施加在每个查询上，不用 SqlSugar 的全局过滤器，不会与应用自己的过滤器互相覆盖。规约里可空值类型与 `null` 的 `==` / `!=` 在交给 SqlSugar 前改写成 `HasValue` 判定，避免译成 `= NULL` 后静默返回错误的结果（ADR-0035）。

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
| `SqlSugarPersistenceOptions` | 选项：`InitTables`、`HealthProbeInterval`、`DiffLog`；`FromConfiguration(configuration)` |
| `SqlSugarConventions` | `Apply(config)` 在连接配置上登记协议约定（保留配置里已有的实体服务回调，先执行它）；`Configure(property, column)` 对单个属性施加约定 |
| `SqlSugarConnections` | 连接的 `ConfigId` 约定：`Default`（`default`）、`Shard`（`shard`） |
| `IEntityConnectionSelector`、`SqlSugarConnectionSelector` | 实体落在哪个连接；默认规则见下文 |
| `SqlSugarUnitOfWork` | 事务型工作单元；`Client` 是根连接，`ConnectionFor(Type)` 返回某种实体所用的连接 |
| `SqlSugarRepository<TEntity, TKey>` | 仓储实现，由特性按登记注册 |
| `SqlSugarDiffLogData` | 差异日志的业务数据：`EntityType` 与仓储所在作用域的 `Services` |

## 使用示例

### 多库落点

客户端可以带多个连接，每种实体落在哪个连接由 `IEntityConnectionSelector` 决定。默认实现 `SqlSugarConnectionSelector` 的规则按顺序：

1. 实体带 SqlSugar 的 `[Tenant(configId)]`：落到该 `ConfigId` 的连接；客户端里没有这个连接是配置错误，抛出携带 `PERSISTENCE_UNAVAILABLE_003` 的 `FrameworkException`，不回退到默认连接。
2. 实体实现 `IMultiTenant`，且客户端里有 `shard` 连接：落到分片连接。
3. 其余：根（默认）连接。

方式一在装配了 `multitenancy.sharding@1` 时，带分片的调用把分片绑定（主值是租户连接串，属性含 `tenant`、`provider` 与透传属性）交给同一个回调，再建一个 `ConfigId = shard` 的连接，与默认连接组成一个客户端：租户实体落分片库，租户目录、授予、发件箱这类不带租户的平台表仍落默认库，同一调用里一起提交或回滚。方式二由应用自己组装连接集合，提供名为 `shard` 的连接即可让第 2 条规则生效。

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
| 作用范围 | 只管实体：声明在 `System.*` 类型上的属性原样交还 |

- `InitTables` 建缺表、加缺列，按落点分组：方式一启动时没有调用也就没有分片，全部实体在默认连接建表；方式二带 `[Tenant]` 的实体在各自的连接上建表。删列、改名、改类型不在范围内，生产库的结构演进走迁移脚本。给已有行的表加非可空列：PostgreSQL 上数值 / 枚举 / 布尔 / Guid 列由约定回填缺省值，代价是此后每次 `InitTables` 都对这些列重发一次 `SET DEFAULT`；SQLite 由 SqlSugar 加成可空列；非可空字符串、日期时间列与其他数据库不受支持，新列声明成可空，或重建开发库、走迁移脚本。
- 行锁：更新与删除的读取带 `TranLock`（`FOR UPDATE` / `UPDLOCK`）；SQLite 不支持行锁，由它的数据库级写锁保证；分表实体不加行锁。
- 游离实例更新：并发戳与当前行不同返回 `PERSISTENCE_CONFLICT_001`；通过后把调用方的实例整行写入（含 `CreatedAt`、`TenantId`）。按主键更新影响 0 行（行在加锁前已被删除）也返回 `PERSISTENCE_CONFLICT_001`。
- 软删除：在取回的当前行上打标并整行写回，调用方实例随后拿到删除标记，以及当前行的修改时间与并发戳。
- 实体类型没有 `new()` 约束，写入经 `InsertableByObject` / `UpdateableByObject` / `DeleteableByObject`，语义与泛型构建器相同。
- 不提供按查询条件的批量更新 / 删除（`Updateable().SetColumns().Where()`）：它绕过审计、并发戳与软删除协议；需要时应用直接用客户端。
- 只读工作单元：仓储写入时返回 `PERSISTENCE_VALIDATION_002`。
- 作用域结束时事务仍未提交（中间件之外直接使用），释放即回滚。
- 健康项 `persistence.sqlsugar.database`：启动时同步探一次，之后按间隔在后台重探并缓存结果；探测在独立作用域里对默认连接 `Ado.IsValidConnection()`，不开工作单元、不经管道。连不上为 Degraded（`PERSISTENCE_UNAVAILABLE_003`），应用仍在服务。

## 相关

- [持久化](../guide/persistence)
- [工作单元与事务](../guide/uow)
- [多租户](../guide/multi-tenancy)
- [审计](../guide/auditing)
- [Persistence.Abstractions](./persistence-abstractions)
- [Persistence.EntityFrameworkCore](./persistence-entityframeworkcore)
- 规范：RFC-0005《领域协议与持久化》
- 决策：ADR-0013、ADR-0022、ADR-0023、ADR-0035、ADR-0036、ADR-0037、ADR-0040、ADR-0044
