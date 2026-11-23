# 数据库租户目录

这一页讲 `MultiTenancy.Database`：把租户目录从配置搬进数据库，租户作为实体经仓储读写，管理操作在运行时增删租户，多个实例共享同一份目录。读完你能装配数据库目录、写一个创建与停用租户的管理操作，并知道缓存怎样失效、启动时为什么会核对持久化 Provider。阅读前先读[租户解析与隔离](/multi-tenancy/resolution)与[仓储读写](/persistence/repositories)。

## 为什么需要

内存目录从配置装载租户，适合租户随配置发布、数量少的应用；但 SaaS 的租户是运营动作的产物：后台开通一个、停用一个、改一条连接串，不能每次都改配置重新部署，多实例部署时各实例还得看到同一份目录。`MultiTenancy.Database` 只换存储：解析顺序、`DefaultTenant` / `RequireTenant` / `ValidateAgainstStore` 与分片语义都在抽象包里，与内存实现相同。租户是一行 `TenantRecord`，经 `persistence.relational@1` 的仓储读写，所以要和一个持久化 Provider 一起装配。

## 两个类型

读写分成两个类型，各自解决一个问题：

| 类型 | 生命周期 | 作用 |
| --- | --- | --- |
| `DatabaseTenantStore` | 单例 `ITenantStore` | Tenant 阶段查它。它在根提供者的新作用域里直接开只读工作单元读仓储（配了从库也读主库），不经 `App.InvokeAsync`：目录是 Tenant 阶段自己要查的东西，嵌套调用会再过一遍 Tenant 与 Authorize 阶段，`RequireTenant` 为真时会自我拒绝，启动期也读不了。查找结果经 `ICache` 按租户缓存（区 `multitenancy:tenants`），「不存在」也缓存；装了两级缓存时命中在本机一级缓存，租户解析不出进程 |
| `TenantRecordStore` | 作用域服务 | 管理操作在 Transaction 阶段内用调用方的工作单元批量新增、覆盖、删除租户，逐项返回结果；写入后立即失效目录缓存（所有共用缓存的实例），并通知分片解析器 |

存储行 `TenantRecord` 的列：

| 列 | 说明 |
| --- | --- |
| `Id` | 主键，就是租户 ID，长度 128 |
| `DisplayName` | 显示名，长度 256 |
| `IsEnabled` | 启用标记 |
| `PropertiesJson` | 整份租户属性的 JSON 对象，长文本类型 |
| `CreatedAt`、`ModifiedAt` | 创建与修改时间 |
| `ConcurrencyStamp` | 并发戳 |
| `IsDeleted`、`DeletedAt` | 软删除标记与时刻：删除的行是墓碑（ADR-0225） |

`TenantRecord` 是平台级数据：不实现 `IMultiTenant`，不受当前租户过滤。删除是软删除，行留作墓碑：读不到这个租户，也不能再用这个 ID 新建（`MULTITENANCY_CONFLICT_001`）——租户的数据按 ID 归属、不随删除清理，复用 ID 的新租户会接手它们。确实要释放一个 ID，先清理它的数据，再由运维直接删掉那一行。已有的表要加上这两列（SqlSugar 的 `InitTables` 会补列，EF Core 用迁移）。

## 最小示例

```bash
dotnet add package XiHan.Framework.MultiTenancy.Database --prerelease
```

本包必须和一个持久化 Provider 一起装配。下面用 `Persistence.SqlSugar`，它按约定映射 `TenantRecord`，不需要登记。接在[第一个应用](/getting-started/first-app)换成 Standard 组合后的 `Program.cs` 上，`AddXiHan` 多两个特性：

```csharp
using SqlSugar;
using XiHan.Framework.MultiTenancy.Database;
using XiHan.Framework.Persistence.SqlSugar;

// 显式 Provider 顶替 Standard 组合包里的内存持久化与内存多租户
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature(new SqlSugarPersistenceFeature(static (binding, config) =>
    {
        config.DbType = DbType.PostgreSQL;
        config.ConnectionString = binding.ValueOrThrow().Value;
    }))
    .UseFeature<DatabaseMultiTenancyFeature>());
```

```json
{
  "XiHan": {
    "Bindings": { "db": "Host=localhost;Database=app" },
    "MultiTenancy": {
      "RequireTenant": false,
      "Database": { "CacheTimeToLiveSeconds": 30 }
    }
  }
}
```

启动后 Tenant 阶段改为查数据库：主体带租户声明 `acme` 时，`TenantRecord` 表里要有 `Id = acme` 且 `IsEnabled` 为真的行，否则调用以 `MULTITENANCY_NOTFOUND_001` 或 `MULTITENANCY_DENIED_001` 结束。本包是 `multitenancy.context@1` 的唯一 Provider，配置里的 `XiHan:MultiTenancy:Tenants` 不被读取；要沿用的租户先用 `UpsertAsync` 导入表里。

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

目录缓存只有一个键 `XiHan:MultiTenancy:Database:CacheTimeToLiveSeconds`（上例的 30 秒就是缺省值）：写入经失效立即生效，存活时间只是兜底。缓存的容量由提供 `caching.local` 的 Provider 管，取值范围与对应的键见 [MultiTenancy.Database](/packages/multitenancy-database#配置)。

解析选项 `DefaultTenant`、`RequireTenant`、`ValidateAgainstStore`、`TenantClaim` 仍在 `XiHan:MultiTenancy`，见[租户解析与隔离](/multi-tenancy/resolution)。持久化 Provider 的连接来自绑定，SqlSugar 的绑定名是 `db`，见[资源绑定](/configuration/bindings)。

## 管理租户

管理操作在处理器里经租户管理 `ITenantAdministration`（作用域服务，就是 `TenantRecordStore`）写入；Transaction 阶段已经开了工作单元，写入随调用一起提交。列出租户用 `ITenantStore.ListAsync(TenantQuery)`：

```csharp
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;

// 作用域服务，在操作的处理器里使用
public sealed class TenantOnboarding(ITenantAdministration tenants)
{
    public async ValueTask<Result> CreateAsync(string tenantId, string displayName, string? connectionString, CancellationToken cancellationToken)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        if (connectionString is not null)
        {
            // 有连接串即分库；写入后目录缓存随即失效，分片解析器下一次解析就用上新连接
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
}
```

`UpsertAsync` 是整份覆盖，`RemoveAsync` 对不存在的 ID 逐项返回 `MULTITENANCY_NOTFOUND_001`；同一批里的重复项、空集合与不合法 ID 的处理见 [MultiTenancy.Database](/packages/multitenancy-database#行为与限制)。这些方法由管理操作调用，操作契约要写上权限码（如 `tenants.manage`），由[授权与权限](/security/authorization)挡住无权的调用方。

## 缓存与失效

- `DatabaseTenantStore.FindAsync` 的结果经 `ICache` 按租户缓存（区 `multitenancy:tenants`），「不存在」也缓存，条目按 `CacheTimeToLive` 过期。
- 失效按租户换代数（[按范围失效的缓存区](/caching/basics#按范围失效的缓存区)），作用于所有共用这个缓存的实例。`TenantRecordStore` 在写入时与调用结束（提交之后）各失效一次：新增、停用、改连接串、删除在提交之后立即生效（ADR-0194、ADR-0229）。
- 多实例部署要装 `Caching.Redis` 与 `Caching.Hybrid`，这时命中在本机一级缓存，一个实例写入后别的实例经 Redis 的变更通知立即失效。只装 Redis 时 `ICache` 仍是进程内的，别的实例只能等存活时间。
- 绕过 `TenantRecordStore` 直接改表时，自行 `await DatabaseTenantStore.InvalidateAsync([tenantId], ct)`（清全部用 `InvalidateAllAsync(ct)`），或等存活时间到期。

数据库或缓存后端故障时的行为、`ListAsync` 的分页、表结构与测试方面的细则见 [MultiTenancy.Database](/packages/multitenancy-database#行为与限制)。

## 启动期核对持久化 Provider

本包靠持久化 Provider 读写 `TenantRecord`，清单里却不声明 `persistence.relational@1`：声明了会与持久化 Provider 对分片能力的可选依赖成环。实际依赖改在启动时核对，能力图谱里没有持久化 Provider 时以 `KERNEL_CAPABILITY_001`（见 [Kernel](/packages/kernel)）中断启动，不会带着一个查不了库的目录运行。清单声明的 `caching.local@1` 由 Standard / Full 组合包自带的内存缓存满足，自己挑特性的宿主要加 `InMemoryCachingFeature`。

## 常见问题

### 刚创建的租户马上调用为什么还是 404

写入要等创建的调用提交后才可见，调用返回之前到达的请求读到的是提交之前的表。调用返回之后仍然 404 时，检查多实例部署是否装了 `Caching.Redis` 与 `Caching.Hybrid`，再看日志里有没有提交之后失效失败的错误：这两种情况下其他实例的「不存在」缓存都要等 `CacheTimeToLiveSeconds` 到期。

### 能用 `TenantRecordStore` 在后台任务里批量导入租户吗

能，但后台任务没有 Transaction 阶段代劳，要自己建作用域、开工作单元、提交，写法见[工作单元与事务](/persistence/unit-of-work)。`TenantRecordStore` 是作用域服务，与工作单元在同一作用域内解析。

## 相关包与下一步

- 包参考：[MultiTenancy.Database](/packages/multitenancy-database)、[MultiTenancy.Abstractions](/packages/multitenancy-abstractions)、[MultiTenancy.InMemory](/packages/multitenancy-inmemory)、[Persistence.SqlSugar](/packages/persistence-sqlsugar)、[Persistence.EntityFrameworkCore](/packages/persistence-entityframeworkcore)
- 错误码：[MultiTenancy.Abstractions](/packages/multitenancy-abstractions#错误码)
- 规范：RFC-0007《多租户》
- 相关章节：[租户分片与分库](/multi-tenancy/sharding)、[仓储读写](/persistence/repositories)、[生命周期与健康](/startup/lifecycle)
- 下一步：[认证](/security/authentication)
