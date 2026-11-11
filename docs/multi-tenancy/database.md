# 数据库租户目录

这一页讲 `MultiTenancy.Database`：把租户目录从配置搬进数据库，租户作为实体经仓储读写，管理操作在运行时增删租户，多个实例共享同一份目录。读完你能装配数据库目录、写一个创建与停用租户的管理操作，并知道缓存怎样失效、启动时为什么会核对持久化 Provider。阅读前先读[租户解析与隔离](/multi-tenancy/resolution)与[仓储读写](/persistence/repositories)。

## 为什么需要

内存目录从配置装载租户，适合租户随配置发布、数量少的应用；但 SaaS 的租户是运营动作的产物：后台开通一个、停用一个、改一条连接串，不能每次都改配置重新部署，多实例部署时各实例还得看到同一份目录。`MultiTenancy.Database` 只换存储：解析顺序、`DefaultTenant` / `RequireTenant` / `ValidateAgainstStore` 与分片语义都在抽象包里，与内存实现相同。租户是一行 `TenantRecord`，经 `persistence.relational@1` 的仓储读写，所以要和一个持久化 Provider 一起装配。

## 两个类型

读写分成两个类型，各自解决一个问题：

| 类型 | 生命周期 | 作用 |
| --- | --- | --- |
| `DatabaseTenantStore` | 单例 `ITenantStore` | Tenant 阶段查它。它在根提供者的新作用域里直接开只读工作单元读仓储（配了从库也读主库），不经 `App.InvokeAsync`：目录是 Tenant 阶段自己要查的东西，嵌套调用会再过一遍 Tenant 与 Authorize 阶段，`RequireTenant` 为真时会自我拒绝，启动期也读不了。查找结果按租户缓存，「不存在」也缓存，每条带存活时间 |
| `TenantRecordStore` | 作用域服务 | 管理操作在 Transaction 阶段内用调用方的工作单元批量新增、覆盖、删除租户，逐项返回结果；写入后立即失效目录与分片解析器的缓存 |

存储行 `TenantRecord` 的列：

| 列 | 说明 |
| --- | --- |
| `Id` | 主键，就是租户 ID，长度 128 |
| `DisplayName` | 显示名，长度 256 |
| `IsEnabled` | 启用标记 |
| `PropertiesJson` | 整份租户属性的 JSON 对象，长文本类型 |
| `CreatedAt`、`ModifiedAt` | 创建与修改时间 |
| `ConcurrencyStamp` | 并发戳 |

`TenantRecord` 是平台级数据：不实现 `IMultiTenant`，不受当前租户过滤，不软删除。

| 其他类型 | 说明 |
| --- | --- |
| `DatabaseMultiTenancyFeature` | 特性入口（清单 `xihan.multitenancy.database`，`role: provider`）：登记 `TenantRecord` 的仓储、目录、解析器与 Tenant 阶段中间件，启动时核对持久化 Provider |
| `DatabaseMultiTenancyOptions` | `CacheSize`、`CacheTimeToLive`（`TimeSpan?`，`null` 不过期） |

## 最小示例

```bash
dotnet add package XiHan.Framework.MultiTenancy.Database --prerelease
```

本包必须和一个持久化 Provider 一起装配。下面用 `Persistence.SqlSugar`，它按约定映射 `TenantRecord`，不需要登记：

```csharp
using SqlSugar;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.MultiTenancy.Database;
using XiHan.Framework.Persistence.SqlSugar;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

// 显式 Provider 顶替 Standard 组合包里的内存持久化与内存多租户
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature(new SqlSugarPersistenceFeature(static (binding, config) =>
    {
        config.DbType = DbType.PostgreSQL;
        config.ConnectionString = binding.ValueOrThrow().Value;
    }))
    .UseFeature<DatabaseMultiTenancyFeature>());

var app = builder.Build();
app.UseXiHan();
app.Run();
```

```json
{
  "XiHan": {
    "Bindings": { "db": "Host=localhost;Database=app" },
    "MultiTenancy": {
      "RequireTenant": false,
      "Database": { "CacheSize": 10000, "CacheTimeToLiveSeconds": 30 }
    }
  }
}
```

启动后 Tenant 阶段改为查数据库：主体带租户声明 `acme` 时，`TenantRecord` 表里要有 `Id = acme` 且 `IsEnabled` 为真的行，否则调用以 `MULTITENANCY_NOTFOUND_001` 或 `MULTITENANCY_DENIED_001` 结束。配置里的 `XiHan:MultiTenancy:Tenants` 不再被读取。

`Persistence.EntityFrameworkCore` 的应用在自己的 `DbContext` 里登记实体，属性列用长文本类型：

```csharp
using Microsoft.EntityFrameworkCore;
using XiHan.Framework.MultiTenancy.Database.Tenants;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // PropertiesJson 是整份租户属性的 JSON 对象
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<TenantRecord>(static t => t.Property(static x => x.PropertiesJson).HasColumnType("text"));
}
```

## 配置

缓存与枚举的配置在 `XiHan:MultiTenancy:Database`：

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `CacheSize` | `int` | 10000 | 目录缓存条目上限，「不存在」也占一条；满时整体清空 |
| `CacheTimeToLiveSeconds` | `double` | 30 | 每条缓存的存活时间；`0` 表示不过期，只靠 `Invalidate` 失效 |

解析选项 `DefaultTenant`、`RequireTenant`、`ValidateAgainstStore`、`TenantClaim` 仍在 `XiHan:MultiTenancy`，见[租户解析与隔离](/multi-tenancy/resolution)。持久化 Provider 的连接来自绑定，SqlSugar 的绑定名是 `db`，见[资源绑定](/configuration/bindings)。

## 管理租户

管理操作在处理器里经 `TenantRecordStore` 写入；Transaction 阶段已经开了工作单元，写入随调用一起提交：

```csharp
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.MultiTenancy.Database.Tenants;
using XiHan.Framework.Primitives;

// 作用域服务，在操作的处理器里使用
public sealed class TenantOnboarding(TenantRecordStore tenants)
{
    public async ValueTask<Result> CreateAsync(string tenantId, string displayName, string? connectionString, CancellationToken cancellationToken)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        if (connectionString is not null)
        {
            // 有连接串即分库；写入后目录与分片解析器的缓存随即失效
            properties[TenantShardKeys.ConnectionString] = connectionString;
        }

        var results = await tenants.UpsertAsync([new TenantInfo { Id = tenantId, DisplayName = displayName, Properties = properties }], cancellationToken);
        return results[0];
    }

    // 整份覆盖：先读出当前记录再改启用标记写回，否则显示名与属性会被清空
    public async ValueTask<IReadOnlyList<Result>> DisableAsync(IReadOnlyList<TenantInfo> current, CancellationToken cancellationToken)
        => await tenants.UpsertAsync([.. current.Select(static t => t with { IsEnabled = false })], cancellationToken);

    // 不存在的 ID 逐项返回 MULTITENANCY_NOTFOUND_001
    public ValueTask<IReadOnlyList<Result>> RemoveAsync(IReadOnlyList<string> tenantIds, CancellationToken cancellationToken)
        => tenants.RemoveAsync(tenantIds, cancellationToken);

    // 按 ID 分页列出
    public Task<IReadOnlyList<TenantInfo>> PageAsync(int skip, int take, CancellationToken cancellationToken)
        => tenants.ListAsync(skip, take, cancellationToken);
}
```

三个批量方法的语义：

| 方法 | 行为 |
| --- | --- |
| `UpsertAsync(tenants)` | 一次查询取回已有行，已有的更新、没有的新增，各用一次批量方法写入；同一批里同一租户后者整份覆盖前者，同一行只写一次，各项结果相同。整份覆盖：只想改启用标记时，先读出当前的 `TenantInfo` 再整体写回 |
| `RemoveAsync(tenantIds)` | 一次批量删除；不存在的 ID 与同一批里重复出现的 ID 逐项返回 `MULTITENANCY_NOTFOUND_001` |
| `ListAsync(skip, take)` | 按 ID 分页 |

不合法的租户 ID 是编程错误，整批在访问数据库前抛出 `ArgumentException`；空集合直接返回空结果。这些方法由管理操作调用，操作契约要写上权限码（如 `tenants.manage`），由[授权与权限](/security/authorization)挡住无权的调用方。

## 缓存与失效

- `DatabaseTenantStore.FindAsync` 的结果按租户缓存，「不存在」也缓存；条目按 `CacheTimeToLive` 过期，条目数达到 `CacheSize` 时整体清空，不逐条淘汰。不合法的租户 ID 直接返回无值，不查库，也不占缓存。
- `TenantRecordStore` 在写入时就失效目录与分片解析器（`ITenantShardResolver.Invalidate`）的缓存。写入在调用方事务提交后才对其他调用可见，失效与提交之间的窗口由存活时间收敛。
- 多实例部署时其他实例的缓存同样靠存活时间收敛，需要更快生效时调小 `CacheTimeToLiveSeconds`。
- 绕过 `TenantRecordStore` 直接改表时，自行调用 `DatabaseTenantStore.Invalidate(tenantId)` 与 `ITenantShardResolver.Invalidate(tenantId)`，或等存活时间到期。`Invalidate()` 不带参数时清空全部。
- `ListAsync` 不走缓存，按 ID 排序、以偏移为续页令牌一页一页读，调用方读到令牌为空为止，不再有截断。

## 启动期核对持久化 Provider

本包的清单不声明 `persistence.relational@1`：持久化 Provider 以可选依赖引用 `multitenancy.sharding@1` 来按分片选连接，内核把满足了的可选依赖也当作排序边，再声明持久化就成环。实际依赖改在 `StartAsync` 核对，能力图谱里没有持久化 Provider 时以 `KERNEL_CAPABILITY_001` 中断启动，不会带着一个查不了库的目录运行。

特性只为被选中的能力注册服务：另有显式 Provider 提供租户上下文时，本包不注册存储、解析器与中间件；另有分片 Provider 时不注册 `TenantStoreShardResolver`。`TenantShardScope` 总会注册。

## 行为与限制

- 故障：连接失败、提交失败这类基础设施故障返回 `MULTITENANCY_UNAVAILABLE_003`，不伪装成租户不存在，也不进缓存；Tenant 阶段据此以 503 结束调用，分片解析器把它包进 `MULTITENANCY_UNAVAILABLE_002`。
- 表结构由应用管理，`InitTables` / `EnsureCreated` 只用于开发与测试。`PropertiesJson` 用长文本类型（`text` / `nvarchar(max)` / `longtext`）。分片库的表结构同样由应用管理。
- 测试里可以与 `Persistence.InMemory` 一起装配；本包通过 `MultiTenancy.Conformance` 的 `TenantStoreConformance` 与 `TenantShardResolverConformance`，并在 SQLite（`Persistence.SqlSugar`）上验证了实体映射。

| 错误码 | 含义 | HTTP |
| --- | --- | --- |
| `MULTITENANCY_NOTFOUND_001` | 租户不存在；`RemoveAsync` 删除不存在的租户也逐项返回它 | 404 |
| `MULTITENANCY_DENIED_001` | 租户已禁用 | 403 |
| `MULTITENANCY_UNAVAILABLE_002` | 分片解析时租户存储不可达 | 503 |
| `KERNEL_CAPABILITY_001` | 装配了 `MultiTenancy.Database` 却没有持久化 Provider，启动中断 | — |

## 常见问题

### 刚创建的租户马上调用为什么还是 404

`TenantRecordStore` 写入时已经失效本实例的缓存，但写入要等调用方的事务提交后才可见；如果创建与首次调用几乎同时发生，或首次调用落在另一个实例上，其他实例的「不存在」缓存要等 `CacheTimeToLiveSeconds` 到期。要更快生效就调小存活时间，或在创建流程里等待提交完成再通知客户端。

### 能用 `TenantRecordStore` 在后台任务里批量导入租户吗

能，但后台任务没有 Transaction 阶段代劳，要自己建作用域、开工作单元、提交，写法见[工作单元与事务](/persistence/unit-of-work)。`TenantRecordStore` 是作用域服务，与工作单元在同一作用域内解析。

### 为什么 `UpsertAsync` 把我的属性清空了

它是整份覆盖，不是局部更新。改一个字段时先经 `ITenantStore.FindAsync` 或 `TenantRecordStore.ListAsync` 读出当前 `TenantInfo`，用 `with` 改字段后整体写回。

### 配了读写分离，目录会读到从库的旧数据吗

不会。`DatabaseTenantStore` 读目录时固定走主库，租户刚创建就能被核对到，不受从库复制延迟影响。

### 能同时保留配置里的租户和数据库里的租户吗

不能。装配 `DatabaseMultiTenancyFeature` 后它是 `multitenancy.context@1` 的唯一 Provider，`XiHan:MultiTenancy:Tenants` 不再读取。要迁移时先把配置里的租户用 `UpsertAsync` 导入表里。

## 相关包与下一步

- 包参考：[MultiTenancy.Database](/packages/multitenancy-database)、[MultiTenancy.Abstractions](/packages/multitenancy-abstractions)、[MultiTenancy.InMemory](/packages/multitenancy-inmemory)、[Persistence.SqlSugar](/packages/persistence-sqlsugar)、[Persistence.EntityFrameworkCore](/packages/persistence-entityframeworkcore)
- 规范：RFC-0007《多租户》
- 相关章节：[租户分片与分库](/multi-tenancy/sharding)、[仓储读写](/persistence/repositories)、[生命周期与健康](/startup/lifecycle)
- 下一步：[认证](/security/authentication)
