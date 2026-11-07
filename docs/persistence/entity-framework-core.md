# EF Core Provider

这一页讲 `XiHan.Framework.Persistence.EntityFrameworkCore`：怎样让应用自己的 `DbContext` 接入框架的仓储与工作单元、`ApplyPersistenceConventions` 与生成的 `ApplyGeneratedEntities` 各做什么、事务为什么在首次写入时才开、命名连接怎样声明、SQLite 上有哪些要注意的地方。读完它，你能把前几页在内存实现上跑通的应用切到 EF Core，并知道直接用 `DbContext` 时要自己补上什么。前置知识是[持久化与 Provider](/persistence/overview)与[工作单元与事务](/persistence/unit-of-work)。

## 为什么需要

EF Core 是 .NET 上最常用的 ORM，有迁移、变更跟踪与完整的 LINQ 翻译。框架不重新发明这些，而是在应用自己的 `DbContext` 之上提供仓储、事务型工作单元与协议约定：仓储按实体协议维护公共字段并施加软删除与租户过滤，工作单元在第一次写入时开事务、处理器成功后提交；应用仍然持有 `DbContext`，复杂查询直接写 LINQ，表结构用 EF Core 迁移管理。同一作用域里仓储、工作单元与应用注入的 `DbContext` 是同一个实例、同一个事务。

## 概念

| 项 | 内容 |
| --- | --- |
| 包 | `XiHan.Framework.Persistence.EntityFrameworkCore` |
| 能力 | 提供 `persistence.relational@1`（`role: provider`）；依赖 `kernel.hosting@1`，可选依赖 `multitenancy.sharding@1` |
| 特性入口 | `EntityFrameworkCorePersistenceFeature<TContext>`，构造参数是配置回调 `Action<Maybe<BindingValue>, DbContextOptionsBuilder>` |
| 绑定 | `db`（`connection:relational`，可选），配置键 `XiHan:Bindings:db` |
| 配置节 | `XiHan:Persistence:EntityFrameworkCore` |
| 健康项 | `persistence.entityframeworkcore.database` |
| Native AOT | 不兼容：EF Core 的运行时模型构建不满足 AOT 分析器 |
| 主要类型 | `EntityFrameworkCorePersistenceFeature<TContext>`（以 `AddDbContext<TContext>` 注册应用的 `DbContext`）、`ModelBuilderExtensions.ApplyPersistenceConventions(Database)`、`EntityFrameworkCoreUnitOfWork`（`Context`、`EnsureTransactionAsync`）、`EntityFrameworkCoreRepository<TEntity, TKey>` |

## 最小示例

安装本包与一个数据库驱动（本包不依赖任何 SQLite 包，版本由应用的驱动引用决定；SQLite 用最新版的 `Microsoft.EntityFrameworkCore.Sqlite`，它修复了并发打开连接时的连接池竞态）：

```bash
dotnet add package XiHan.Framework.Persistence.EntityFrameworkCore --prerelease
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
```

1. 定义 `DbContext`：登记实体后调用 `ApplyPersistenceConventions(Database)`。
2. 登记实体：`AddRepository<TEntity, TKey>()`。
3. 装配特性，在回调里用绑定 `db` 配置连接。

```csharp
using Microsoft.EntityFrameworkCore;
using SimpleApp.Orders;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.EntityFrameworkCore;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRepository<Order, Guid>();
builder.AddXiHan(x => x
    .UseStandardProfile()
    // 显式 Provider 顶替 Standard 里的内存默认实现
    .UseFeature(new EntityFrameworkCorePersistenceFeature<AppDbContext>(
        static (binding, options) => options.UseSqlite(binding.ValueOrThrow().Value))));

var app = builder.Build();
app.UseXiHan();
app.Run();

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>();
        // 在登记全部实体、配置完表名之后调用：并发戳设为并发令牌，租户与软删除标记建索引，[EntityIndex] 映射为 HasIndex
        modelBuilder.ApplyPersistenceConventions(Database);
    }
}
```

```json
{
  "XiHan": {
    "Bindings": {
      "db": "Data Source=app.db"
    },
    "Persistence": {
      "EntityFrameworkCore": {
        "EnsureCreated": true
      }
    }
  }
}
```

运行后应用在 `app.db` 里建出 `Order` 表（`EnsureCreated` 只用于开发与测试，生产用迁移），`POST /orders` 的写入进入 SQLite，重启后数据仍在。操作代码与内存实现时完全相同。回调的第一个参数是 `Maybe<BindingValue>`：绑定缺失时为空，回调自己决定连接；上例的 `ValueOrThrow()` 在缺失时抛出。

## 应用自有 DbContext 与约定

特性以 `AddDbContext<TContext>` 注册应用的 `DbContext`，所以模型完全由应用定义：表名、列类型、关系、值对象的 `OwnsOne`、迁移，都按 EF Core 的常规做法。框架只要求在 `OnModelCreating` 末尾调用一次 `ApplyPersistenceConventions(Database)`，它对模型里已有的每个实体：

| 约定 | 内容 |
| --- | --- |
| `ConcurrencyStamp` | 设为并发令牌，最大长度 32 |
| `TenantId`、`IsDeleted` | 建索引 |
| `[EntityIndex]` | 映射为 `HasIndex(列).IsUnique(…).HasDatabaseName(名字)`，名字与 SqlSugar 建出的相同；软删除实体的唯一索引加 `HasFilter`，条件按 `Database.ProviderName` 写方言（Npgsql、SQLite、SQL Server），其余 Provider 建模时抛出 `PERSISTENCE_UNAVAILABLE_008` |
| `DateTimeOffset`（SQLite） | 与可空 `DateTimeOffset` 属性存成 UTC ticks 整数，应用已配转换的不动 |
| 字符串匹配（SQLite） | `Contains` / `StartsWith` / `EndsWith` 一律译成 `LIKE … ESCAPE '\'`，按字面匹配、只对 ASCII 字母不区分大小写 |

索引名取调用时的 `GetTableName()`，所以 `ToTable` 要在它之前配置；从属类型与共享类型实体跳过。索引的声明方式见[建表约定与索引](/persistence/schema)。

引用了 `Persistence.Generators` 时，`OnModelCreating` 里换成生成的 `ApplyGeneratedEntities(Database)`，它登记本程序集的全部实体并接着调用 `ApplyPersistenceConventions(Database)`：

```csharp
using Microsoft.EntityFrameworkCore;

namespace SimpleApp;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // 登记本程序集全部实体，并应用持久化约定
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyGeneratedEntities(Database);
}
```

需要额外配置（值对象、关系、表名）时，先写自己的配置，最后再调 `ApplyGeneratedEntities(Database)`。

整数主键按 EF Core 约定由数据库生成；应用自己用 `IIdGenerator<long>` 生成整数标识时，在模型里配 `ValueGeneratedNever()`，见[标识生成](/persistence/identifiers)。

## 事务与仓储的行为

- 延迟开启事务：只读过数据的调用不开事务，多个只读调用与一个写入调用能在 SQLite 这类单写者数据库上并存。第一次 `EnsureTransactionAsync`（仓储每次写入前、提交时保存残余变更前都经它）开事务后 `HasTransaction` 为真，提交、回滚后复位为假。
- 读己之写：每次写入立即 `SaveChanges`，在本事务内可见。批量方法用一次 `IN` 查询取回目标行、逐项校验后一次 `SaveChanges`。
- 软删除与租户过滤由仓储用 `EF.Property` 显式施加在每个查询上，不用 EF Core 的全局查询过滤器；应用直接用 `DbContext` 查询时不带这两项过滤。
- 游离实例更新先比对并发戳，通过后把值复制到跟踪实例上，再把创建时间、租户与删除标记恢复为原值；提交时还有残余变更（绕过仓储直接改了跟踪实体）会先保存再提交，并发令牌不匹配返回 `PERSISTENCE_CONFLICT_001` 并回滚。
- 并发新增同一标识撞上主键约束时，`SaveChanges` 自动建的保存点让事务仍可用，仓储在同一事务里核对标识，确已存在就返回 `PERSISTENCE_CONFLICT_002`；不是标识重复则核对模型里的唯一索引，撞上返回 `PERSISTENCE_CONFLICT_003`。之后工作单元只能回滚。
- 只读工作单元不开事务：仓储写入返回 `PERSISTENCE_UNEXPECTED_001`；直接用 `DbContext` 写入前调用的 `EnsureTransactionAsync` 抛出同码异常；绕过仓储改了跟踪实体而未保存的，提交时以它回滚。作用域结束时事务仍未提交，释放即回滚。

## 直接使用 DbContext

仓储覆盖不了的查询直接注入应用的 `DbContext`。它与仓储共享同一个实例和同一个事务，但不带软删除与租户过滤，要自己加（或用生成的 `<实体>Filters` 规约）：

```csharp
using Microsoft.EntityFrameworkCore;

namespace SimpleApp.Orders;

public sealed class OrderReports(AppDbContext db)
{
    // 直接查询绕过仓储的过滤：软删除与租户条件要自己写
    public Task<int> TotalAmountAsync(string tenantId, CancellationToken cancellationToken)
        => db.Set<Order>()
            .Where(o => !o.IsDeleted && o.TenantId == tenantId)
            .SumAsync(static o => o.Amount, cancellationToken);
}
```

用 `builder.Services.AddScoped<OrderReports>()` 注册后在操作里注入。直接用 `DbContext` 写入之前，先调用本作用域 `EntityFrameworkCoreUnitOfWork` 的 `EnsureTransactionAsync`，写入才在事务里、只读工作单元里才会被拒绝。在管道外使用时同样要先开工作单元，见[工作单元与事务](/persistence/unit-of-work)。

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Persistence:EntityFrameworkCore:EnsureCreated` | 布尔 | `false` | 启动时对默认连接调用 `EnsureCreated` 建表；登记了实体的每个命名连接也各执行一次；只用于开发与测试，生产用迁移 |
| `XiHan:Persistence:EntityFrameworkCore:HealthProbeIntervalSeconds` | 正数 | `15` | 健康项的重探间隔，也是单次探测的超时 |
| `XiHan:Persistence:EntityFrameworkCore:VerifiedShardCapacity` | 正整数 | `1024` | SQLite 上核对通过的租户分片库最多记多少个；记满淘汰最早记下的，被淘汰的分片下次绑定时重新核对 |
| `XiHan:Persistence:EntityFrameworkCore:Connections:<name>` | 字符串 | — | 命名连接，值是连接串 |
| `XiHan:Bindings:db` | 字符串 | — | 连接绑定的主值，作为 `BindingValue.Value` 交给回调 |

健康项 `persistence.entityframeworkcore.database` 启动时同步探一次，之后按间隔在后台重探并缓存结果；探测在独立作用域里对默认连接 `CanConnect`，不开工作单元、不经管道。连不上为 Degraded（`PERSISTENCE_UNAVAILABLE_003`），应用仍在服务。

启动时在独立作用域里取一次模型，建模错误（索引声明不合法、Provider 不支持部分唯一索引）在启动时失败，不论是否开着 `EnsureCreated`。

## 命名连接

在 `XiHan:Persistence:EntityFrameworkCore:Connections:<name>` 声明命名连接，值是连接串。名字合 `^[a-z0-9][a-z0-9-]{0,63}$`，不能是 `default` 或 `shard`；值不能空白、不能写成子节，否则装配即 `ArgumentException`。

EF Core 的规则是一个 `DbContext` 一个连接、建出后不换：`UnitOfWorkOptions(Connection: name)` 开启的工作单元先记下连接、再建本作用域的 `DbContext`，回调收到的绑定主值是那个连接串、属性沿用 `db` 绑定，事务只开在那个库上。所以登记到命名连接的实体（`AddRepository<TEntity, TKey>(connection)`）只能在绑定那个连接的工作单元里读写：

| 情况 | 结果 |
| --- | --- |
| 普通工作单元里取用登记到命名连接的实体的仓储 | 抛出携带 `PERSISTENCE_UNAVAILABLE_008` 的 `FrameworkException` |
| 绑定连接的工作单元里取用落在别处的实体 | `PERSISTENCE_VALIDATION_004` |
| 登记到没有声明的连接 | 装配即失败，`PERSISTENCE_VALIDATION_004` |
| 绑定没有声明的连接 | `BeginAsync` 抛出 `PERSISTENCE_VALIDATION_004` |
| 作用域里已经按别的连接建出 `DbContext` 再绑定 | `BeginAsync` 抛 `PERSISTENCE_VALIDATION_002`，在新作用域里绑定即可 |

EF Core 不做「每个连接一个 `DbContext`、跨上下文顺序提交」的多上下文工作单元，要在业务事务里一起读写命名连接上的实体，用 [SqlSugar Provider](/persistence/sqlsugar)。命名连接与默认连接用同一个配置回调，是同一种数据库 Provider。典型用途是 SQLite 上把工作流的租约表放到另一个库文件，见 [Workflow.Database](/packages/workflow-database)；连接的整体说明见[连接与运行时数据源](/persistence/connections)。

## 租户分库

装配了 `multitenancy.sharding@1` 时，`DbContext` 选项按作用域构建；带分片的调用把分片绑定（主值是租户连接串，属性含 `tenant`、`provider` 与透传属性）交给同一个回调，整个 `DbContext` 连到分片库，其余调用用默认连接。分片库的表结构由应用管理，`EnsureCreated` 只作用于默认连接。EF Core 没有 SqlSugar Provider 那样按实体分库的落点规则：一次调用要么全在默认库，要么全在分片库。细节见[租户分片与分库](/multi-tenancy/sharding)。

## SQLite 注意事项

### 时间列存成 UTC ticks

EF Core 的 SQLite Provider 把 `DateTimeOffset` 存成文本，翻译不了大小比较与排序，而到期书签、按时间过滤与分页都要用到。`ApplyPersistenceConventions(Database)` 在 SQLite 上把模型里全部 `DateTimeOffset` 与可空 `DateTimeOffset` 属性存成 UTC ticks 整数：写入 `UtcTicks`、读回偏移为零的同一时刻，整数的比较与排序就是时刻的比较与排序。应用已配了值转换或存储类型的属性不动；其余数据库不动。

**已有的 SQLite 库**要注意：原来没配转换的列存的是文本，照整数读会被静默读成错误的时刻。特性启动时因此核对默认连接与每个命名连接上这类列的存储形式（声明类型不是 INTEGER，或抽样最早的 1000 行查到非整数的值，即不符），不符即启动失败（`PERSISTENCE_VALIDATION_008`，消息列出连接与不符的列）；应用自己配了转换的列、库里没有的表或列、还不存在的库文件不核对。租户分片库在首次绑定时核对，不符时那次工作单元开不起来，经管道的调用是 `KERNEL_UNEXPECTED_001`（HTTP 500），别的租户照常；核对通过的分片按连接串记在进程内，至多 `VerifiedShardCapacity` 条。

怎么修：数据可以丢的开发、测试库删库重建；要留数据就重建表，列建成 `INTEGER`，搬数据时把文本换算成 UTC ticks。EF Core 迁移改列类型本来就是重建表，但它搬数据是原样 `INSERT … SELECT`，要把搬数据的语句换成带换算式的 `migrationBuilder.Sql(...)`。换算式与重建表的完整 SQL 见 [Persistence.EntityFrameworkCore 包](/packages/persistence-entityframeworkcore)。

### 字符串匹配

EF Core 的 SQLite Provider 原来把 `Contains` 译成区分大小写的 `instr`，`StartsWith` / `EndsWith` 按实参是值还是列分别译成 `LIKE` 或 `substr` 比较，同一个库上三种规则。特性在 `DbContext` 选项上加了查询表达式拦截器与方法翻译插件，只在 SQLite 上把这三个方法一律译成 `LIKE … ESCAPE '\'`：`%`、`_`、反斜杠按字面，只对 ASCII 字母不区分大小写，与 SqlSugar 在 SQLite 上同一套规则。范围是特性建出的 `DbContext` 上的全部 LINQ；应用自己的 `HasQueryFilter`、不经特性建出的 `DbContext`、用 `UseInternalServiceProvider` 自给内部服务容器的不在范围内。SQLite 的 `LIKE` 模式最长 50000 字节。

### 单写者

SQLite 只有一个写者：业务事务写过之后，同一个库文件上的另一个事务要等它结束。延迟开事务让只读调用不受影响；需要在业务事务进行中另行提交的数据（工作流租约）放到命名连接指向的另一个库文件。

## Native AOT 与不支持的能力

本包的 `IsAotCompatible` 是 `false`：EF Core 的运行时模型构建不满足 AOT 分析器。标注为 AOT 兼容的持久化实现只有内存实现；AOT 发布的整体要求见 [JSON 序列化与 Native AOT](/common/serialization)。

运行时数据源与库结构读取在 EF Core 上不支持，也不计划支持：数据库 Provider 由应用选、本包不引用，按数据库类型建不出连接；跨数据库读结构在 EF Core 里是设计期脚手架服务，各驱动不一致。`DataSourceRegistry` 的取用与测试连接、`ISchemaReader` 都返回 `PERSISTENCE_UNAVAILABLE_008`；实体目录 `IEntityCatalog` 取 `DbContext` 模型，可用。需要这两项的应用用 [SqlSugar Provider](/persistence/sqlsugar)。

## 常见问题

### 我已经有一个 `DbContext` 了，要改多少

两处：`OnModelCreating` 末尾加一行 `modelBuilder.ApplyPersistenceConventions(Database)`；`Program.cs` 把原来的 `AddDbContext` 换成装配 `EntityFrameworkCorePersistenceFeature<AppDbContext>`，连接配置搬进回调。原来直接用 `DbContext` 的代码继续工作，只是写入前要经 `EnsureTransactionAsync`，才能与仓储在同一事务里。

### 为什么我直接 `db.Set<Order>()` 查到了别的租户的数据

仓储的软删除与租户过滤是显式施加在仓储查询上的，不是全局查询过滤器，`DbContext` 上的 LINQ 不带它们。直接查询时自己写条件，或用生成的 `OrderFilters.ActiveFor(tenantId).Criteria`。

### 启动报 `PERSISTENCE_VALIDATION_008`

这是 SQLite 库里的 `DateTimeOffset` 列还是旧的文本形式。按上文「时间列存成 UTC ticks」处理：开发库删库重建，要留数据的重建表并换算。SqlSugar 不需要这项核对，它在 SQLite 上一直存 UTC 日期时间文本。

## 相关包与下一步

- 相关包：[Persistence.EntityFrameworkCore](/packages/persistence-entityframeworkcore)、[Persistence.Abstractions](/packages/persistence-abstractions)、[Persistence.Generators](/packages/persistence-generators)、[Persistence.SqlSugar](/packages/persistence-sqlsugar)
- 规范：RFC-0005《领域协议与持久化》
- 下一页：[SqlSugar Provider](/persistence/sqlsugar)，讲多库、分表、读写分离与差异日志
