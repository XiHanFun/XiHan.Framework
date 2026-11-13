来源：https://framework.docs.xihanfun.com/multi-tenancy/sharding

# 租户分片与分库

这一页讲怎样把每个租户的数据落到租户自己的数据库：分片契约 `ITenantShardResolver` 与 `TenantShard`，缺省实现怎样从租户属性推出分片，持久化 Provider 怎样按分片选连接，以及不经管道的代码怎样用 `TenantShardScope` 显式绑定分片。读完你能给某个租户配一条独立连接串，让它的多租户实体读写都落到独立库，而平台表仍在默认库。阅读前先读[租户解析与隔离](/multi-tenancy/resolution)与[持久化与 Provider](/persistence/overview)。

## 为什么需要

行级隔离（每张表一列 `TenantId`）对大多数租户够用，但大客户往往要求数据物理分开：独立备份、独立容量、独立合规审计，甚至放在客户自己的数据库实例里。如果让业务代码自己按租户挑连接，每个仓储、每个作业都要重复这段逻辑，漏掉一处就会把租户数据写进平台库。框架把「这个租户用哪条连接」做成能力 `multitenancy.sharding@1`：Tenant 阶段解析一次分片写进调用上下文，持久化 Provider 创建客户端时读它；解析失败就让调用失败，不降级为共库。框架只做库级隔离，行级隔离就是上一页的租户过滤，Schema 级隔离由应用经透传属性自行处理。

## 分片契约

| 类型 | 说明 |
| --- | --- |
| `ITenantShardResolver` | `ResolveAsync(tenantId)` 返回 `Result<Maybe<TenantShard>>`：共库租户与未知租户是成功且无值；租户存储不可达返回 `MULTITENANCY_UNAVAILABLE_002`。`Invalidate(tenantId?)` 丢弃缓存，参数为 `null` 时清空全部 |
| `TenantShard` | 一个分片：`TenantId`、`ConnectionString`、可选的 `Provider`（提供者名，转成小写，如 `postgresql`）与透传属性 `Properties` |
| `TenantStoreShardResolver` | 缺省解析器，从 `ITenantStore` 里租户的 `Properties` 推出分片，按租户缓存结果 |
| `TenantShardKeys` | 租户属性键的常量 |
| `TenantShardScope` | 作用域服务，给不经管道的作用域显式绑定分片 |
| `context.FindTenantShard()` | 读调用上下文里的分片，`Maybe<TenantShard>` |

两个多租户 Provider（`MultiTenancy.InMemory`、`MultiTenancy.Database`）在分片能力被选中时都注册 `TenantStoreShardResolver`，它按下面的租户属性推出分片：

| 租户属性键 | 常量 | 含义 |
| --- | --- | --- |
| `ConnectionString` | `TenantShardKeys.ConnectionString` | 有值即该租户分库；没有或为空白是共库租户 |
| `DatabaseProvider` | `TenantShardKeys.DatabaseProvider` | 数据库提供者名，可选；空表示与应用默认连接同一种 |
| `Shard.<key>` | `TenantShardKeys.PropertyPrefix` | 去掉前缀后作为 `<key>` 透传给持久化 Provider；用点不用冒号，因为属性来自配置时冒号是节分隔符 |

## 最小示例

给内存租户目录里的 `acme` 配一条独立连接，`beta` 不配就是共库租户：

```json
{
  "XiHan": {
    "Bindings": { "db": "Host=localhost;Database=app" },
    "MultiTenancy": {
      "Tenants": {
        "acme": {
          "DisplayName": "Acme Corp",
          "Properties": {
            "ConnectionString": "Host=localhost;Database=acme",
            "DatabaseProvider": "postgresql"
          }
        },
        "beta": { "DisplayName": "Beta" }
      }
    }
  }
}
```

装配一个关系型持久化 Provider，下面用 `Persistence.SqlSugar`，默认连接来自绑定 `db`：

```csharp
using SqlSugar;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.Persistence.SqlSugar;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

// Standard 组合包已含内存多租户；SqlSugar 顶替其中的内存持久化，并以可选依赖接上分片能力
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature(new SqlSugarPersistenceFeature(static (binding, config) =>
    {
        config.DbType = DbType.PostgreSQL;
        config.ConnectionString = binding.ValueOrThrow().Value;
    })));

var app = builder.Build();
app.UseXiHan();

// 分片在 Tenant 阶段解析一次，写进调用上下文；共库租户与无租户的调用没有分片
app.MapGet("/shard", (HttpContext http) =>
    http.FindXiHanContext().ValueOrThrow().FindTenantShard().TryGetValue(out var shard)
        ? $"{shard.TenantId} -> {shard.Provider ?? "default provider"}"
        : "shared");

app.Run();
```

用租户为 `acme` 的主体调用 `/shard`，响应 `acme -> postgresql`；租户为 `beta` 的主体响应 `shared`。此后在 `acme` 的调用里，实现 `IMultiTenant` 的实体经仓储读写都落到 `Database=acme`，不实现它的平台表仍落 `Database=app`。

## 解析与缓存

分片在 Tenant 阶段解析一次，写进调用上下文；解析失败即调用失败（`MULTITENANCY_UNAVAILABLE_002`），不降级为共库，因为降级会把租户数据写进平台库。无租户的调用没有分片。

`TenantStoreShardResolver` 的结果按租户缓存，「无分片」也缓存；缓存条目数达到 `XiHan:MultiTenancy:ShardCacheSize`（缺省 10000）时整体清空，条目过了 `ShardCacheTimeToLiveSeconds`（缺省 30 秒）即重新读租户存储。租户连接变更后调用 `ITenantShardResolver.Invalidate(tenantId)`，失效只作用于本实例，多实例部署里别的实例在存活时间之后才用上新连接（ADR-0193）；经数据库目录的 `TenantRecordStore` 写入时会自动失效，见[数据库租户目录](/multi-tenancy/database)。

想自己决定分片规则（按租户哈希选库、从独立的分片表读连接）时，实现 `ITenantShardResolver` 并在特性里登记为单例；中间件与持久化 Provider 只认契约。自带分片 Provider 的应用同样要登记 `TenantShardScope`。

## 持久化 Provider 按分片选连接

持久化 Provider（`Persistence.SqlSugar`、`Persistence.EntityFrameworkCore`）以可选依赖声明 `multitenancy.sharding@1`，在作用域内创建客户端时经 `TenantShardBinding.Resolve` 读分片，不访问存储、不阻塞。分片是实体级的落点，不是整个作用域的切换：带分片的调用里，实现 `IMultiTenant` 的实体走分片连接，平台表仍走默认连接。

| Provider | 带分片的调用 |
| --- | --- |
| `Persistence.SqlSugar` | 客户端同时持有默认连接与分片连接，工作单元在两个连接上开事务、依次提交或回滚（不是两阶段提交），见 [SqlSugar Provider](/persistence/sqlsugar) |
| `Persistence.EntityFrameworkCore` | 一个 `DbContext` 一个连接，整个上下文切到分片连接；平台表要另起 `DbContext`，由应用处理，见 [EF Core Provider](/persistence/entity-framework-core) |

`TenantShard.Provider` 为空时沿用默认连接的数据库种类；有值时 Provider 据此选驱动，所以同一应用可以让默认库是 SQL Server、某个租户库是 PostgreSQL。

::: warning 分片库的表结构由应用管理
框架的建表开关（SqlSugar 的 `InitTables`、EF Core 的 `EnsureCreated`）只作用于默认连接。租户库的建表与升级由应用自己执行，下一节的 `TenantShardScope` 用于在管道外连到租户库。EF Core 在 SQLite 上的时间列核对对分片库在首次绑定时执行：分片库还是旧的文本形式时，这个租户的工作单元开不起来，经管道的调用是 `KERNEL_UNEXPECTED_001`（HTTP 500，原因 `PERSISTENCE_VALIDATION_008`），修好库之后下一次绑定即恢复，见[建表约定与索引](/persistence/schema)。
:::

## 管道外绑定分片

后台写入、调度作业、租户库初始化这类代码自己建服务作用域，不经 `App.InvokeAsync`，作用域里没有调用上下文，也就没有分片。作用域服务 `TenantShardScope` 给这类作用域显式绑定分片：

| 成员 | 说明 |
| --- | --- |
| `BindTenantAsync(tenantId, cancellationToken)` | 经 `ITenantShardResolver` 解析并绑定，返回解析结果；不看租户是否启用，也不受应用是否在服务状态（启动、排空）影响；共库租户、未知租户与没有装配分片解析器时成功且不绑定；解析失败返回错误、不绑定 |
| `Bind(shard)` | 直接绑定一个分片 |
| `Shard` | 已绑定的分片，没有绑定为无值 |
| `IsConnected` | 作用域的持久化客户端是否已经取过连接 |

绑定必须发生在作用域内首次创建持久化客户端之前。客户端建好之后再绑定，或改绑到另一个分片，抛出 `InvalidOperationException`；同一个分片重复绑定没有副作用。显式绑定优先于调用上下文里的分片。

```csharp
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Primitives;

// Invoice 见上一页，仓储已经登记
public sealed class InvoiceCounter(IServiceScopeFactory scopes)
{
    public async Task<Result<long>> CountAsync(string tenantId, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;

        // 先绑定再解析任何持久化服务：客户端在作用域里只建一次，建好后改不了连接
        var bound = await services.GetRequiredService<TenantShardScope>().BindTenantAsync(tenantId, cancellationToken);
        if (bound.IsFailure)
        {
            return bound.Error!;
        }

        // 没有 Transaction 阶段代劳，工作单元自己开；租户由调用方给出，与绑定的租户一致
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        await unitOfWork.BeginAsync(new UnitOfWorkOptions { TenantId = tenantId, IsReadOnly = true }, cancellationToken);
        try
        {
            var invoices = services.GetRequiredService<IRepository<Invoice, Guid>>();
            var count = await invoices.CountAsync(EntityQuery.All<Invoice>(), cancellationToken);
            var committed = await unitOfWork.CommitAsync(cancellationToken);
            return committed.IsFailure ? committed.Error! : count;
        }
        catch
        {
            if (unitOfWork.State == UnitOfWorkState.Active)
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
            }

            throw;
        }
    }
}
```

工作单元的租户不会从绑定推导出来，两者都由调用方给出，必须一致：客户端建在分片上时，开始工作单元即核对，不一致（含不带租户）抛出 `PERSISTENCE_VALIDATION_010`，工作单元保持未开始（ADR-0223）；管道外开工作单元、派发领域事件的完整写法见[工作单元与事务](/persistence/unit-of-work)。两个多租户 Provider 都登记 `TenantShardScope`，与是否选中分片能力无关。

## 常见问题

### 租户配了连接串但数据还是写进了默认库

先确认装配的持久化 Provider 是 `Persistence.SqlSugar` 或 `Persistence.EntityFrameworkCore`：`Persistence.InMemory` 不接分片能力。再确认实体实现了 `IMultiTenant`：分片只作用于多租户实体，平台表始终走默认连接。最后看 `/shard` 这样的端点能不能读到分片；读不到说明调用里没有租户，或者分片解析器的缓存还是旧值，调用 `Invalidate(tenantId)`。

### 租户库连不上时调用会降级到共库吗

不会。分片解析失败（租户存储不可达）返回 `MULTITENANCY_UNAVAILABLE_002`，调用以 503 结束；分片解析成功但租户库连不上，工作单元开不起来，调用同样失败。降级会把租户数据写进平台库，所以框架不做。

### 怎样给新租户建表

租户库的表结构由应用管理。在建租户的流程里用 `TenantShardScope.BindTenantAsync` 绑到新库，再用 Provider 自己的建表能力（SqlSugar 的 `CodeFirst`、EF Core 的迁移）执行；或者在数据库运维流程里完成，再把连接串写进租户属性。不要依赖框架的 `InitTables` / `EnsureCreated`，它们只作用于默认连接。

### 能按租户切换 Schema 而不是整库吗

框架只做库级分片。Schema 级隔离把 Schema 名写进 `Shard.<key>` 属性透传给持久化 Provider，由应用在 Provider 的配置回调里按 `TenantShard.Properties` 处理。

### 带分片的调用发的消息怎样投递

经发件箱发布时，发件箱行随调用的事务写入，EF Core Provider 写进分片库，SqlSugar Provider 写进默认库。发件箱投递循环每轮先投递默认库，再逐个投递分片库，所以两种 Provider 下消息都会发出；EF Core 下分片库要建发件箱表。见[领域事件与发件箱](/messaging/domain-events)。

### 同一调用里能同时读平台表和租户表吗

能。SqlSugar Provider 的客户端同时持有默认连接与分片连接，按实体是否实现 `IMultiTenant` 落点；两个连接上的事务依次提交，不是两阶段提交，跨库一致性由应用的补偿或发件箱保证。EF Core Provider 一个 `DbContext` 一个连接，平台表要另起 `DbContext`。

## 相关包与下一步

- 包参考：[MultiTenancy.Abstractions](/packages/multitenancy-abstractions)、[MultiTenancy.InMemory](/packages/multitenancy-inmemory)、[MultiTenancy.Database](/packages/multitenancy-database)、[Persistence.SqlSugar](/packages/persistence-sqlsugar)、[Persistence.EntityFrameworkCore](/packages/persistence-entityframeworkcore)
- 规范：RFC-0007《多租户》
- 相关章节：[连接与运行时数据源](/persistence/connections)、[工作单元与事务](/persistence/unit-of-work)、[作业与计划](/scheduling/jobs)
- 下一步：[数据库租户目录](/multi-tenancy/database)
