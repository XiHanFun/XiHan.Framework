# XiHan.Framework.MultiTenancy.Database

多租户的数据库 Provider：租户目录是数据库里的 `TenantRecord` 行，经 `persistence.relational@1` 的仓储读写，提供 `multitenancy.context@1` 与 `multitenancy.sharding@1`。适用于租户由管理后台在运行时增删、多个实例共享同一份目录的应用。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.MultiTenancy.Database` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `multitenancy.context@1`、`multitenancy.sharding@1`；依赖 `kernel.hosting@1`；`persistence.relational@1` 在启动时核对（见下文） |
| 特性入口 | `DatabaseMultiTenancyFeature`（清单 `xihan.multitenancy.database`，`role: provider`） |
| 配置节 | `XiHan:MultiTenancy`（含 `Database` 子节） |
| 依赖 | `XiHan.Framework.MultiTenancy.Abstractions`、`XiHan.Framework.Persistence.Abstractions`；不引用数据库驱动 |
| AOT | 兼容（属性 JSON 用源生成序列化） |
| 稳定性 | Preview |

## 概述

本包只换存储：解析顺序、`DefaultTenant` / `RequireTenant` / `ValidateAgainstStore` 与分片语义都在 `MultiTenancy.Abstractions`，与内存实现一致（RFC-0007，ADR-0026）。读写分成两个类型：

- `DatabaseTenantStore` 是单例 `ITenantStore`，Tenant 阶段查它。它在根提供者的新作用域里直接开只读工作单元读仓储，不经 `App.InvokeAsync`：目录是 Tenant 阶段自己要查的东西，嵌套调用会再过一遍 Tenant 与 Authorize 阶段，`RequireTenant` 为真时没有租户就失败，而且启动期读不了。查找结果按租户缓存（"不存在"也缓存），每条带存活时间。
- `TenantRecordStore` 是作用域服务，在调用的 Transaction 阶段内用调用方的工作单元批量写入，逐项返回结果，写入后立即失效目录与分片解析器的缓存。

清单不声明 `persistence.relational@1`：持久化 Provider 以可选依赖引用 `multitenancy.sharding@1` 来按分片选连接，内核把满足了的可选依赖也当作排序边，再声明持久化就成环。实际依赖改在 `StartAsync` 核对，能力图谱里没有持久化 Provider 时以 `KERNEL_CAPABILITY_001` 中断启动。

特性只为被选中的能力注册服务：另有显式 Provider 提供租户上下文时，本包不注册存储、解析器与中间件；另有分片 Provider 时不注册 `TenantStoreShardResolver`。`TenantShardScope` 总会注册。

## 何时使用

- 租户在运行时创建、停用、改连接，或多实例部署要共享目录：用本包。
- 租户随配置发布、数量少：用 [MultiTenancy.InMemory](./multitenancy-inmemory)，不需要数据库。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.MultiTenancy.Database" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。本包必须和一个持久化 Provider 一起装配，下面用 `Persistence.SqlSugar`，它按约定映射 `TenantRecord`，不需要登记：

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

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:MultiTenancy:Database:CacheSize` | `int` | 10000 | 目录缓存条目上限，"不存在"也占一条；满时整体清空 |
| `XiHan:MultiTenancy:Database:CacheTimeToLiveSeconds` | `double` | 30 | 每条缓存的存活时间；`0` 表示不过期，只靠 `Invalidate` 失效 |
| `XiHan:MultiTenancy:Database:MaxListedTenants` | `int` | 10000 | `DatabaseTenantStore.ListAsync` 最多返回的租户数 |

解析选项 `DefaultTenant`、`RequireTenant`、`ValidateAgainstStore`、`TenantClaim` 见 [MultiTenancy.Abstractions](./multitenancy-abstractions#配置)。持久化 Provider 的连接来自绑定，SqlSugar 的绑定名是 `db`。

```json
{
  "XiHan": {
    "Bindings": { "db": "Host=localhost;Database=app" },
    "MultiTenancy": {
      "RequireTenant": false,
      "Database": { "CacheSize": 10000, "CacheTimeToLiveSeconds": 30, "MaxListedTenants": 10000 }
    }
  }
}
```

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `DatabaseMultiTenancyFeature` | 特性入口：登记 `TenantRecord` 的仓储、目录、解析器与 Tenant 阶段中间件，启动时核对持久化 Provider |
| `DatabaseMultiTenancyOptions` | `CacheSize`、`CacheTimeToLive`（`TimeSpan?`，`null` 不过期）、`MaxListedTenants` |
| `Tenants.TenantRecord` | 存储行：`Id`（主键，租户 ID）、`DisplayName`、`IsEnabled`、`PropertiesJson`、`CreatedAt`、`ModifiedAt`、`ConcurrencyStamp`；平台级数据，不实现 `IMultiTenant`、不软删除 |
| `Tenants.DatabaseTenantStore` | 单例 `ITenantStore`：`FindAsync`（缓存）、`ListAsync`（按 ID 每页 1000 条读完，不走缓存）、`Invalidate(tenantId?)`、`CachedCount` |
| `Tenants.TenantRecordStore` | 作用域服务：`UpsertAsync`、`RemoveAsync` 批量逐项结果，`ListAsync(skip, take)` 按 ID 分页 |
| `Tenants.TenantRecordResult` | 一项结果：`TenantId`、`Outcome` |

## 使用示例

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
        return results[0].Outcome;
    }

    public async ValueTask<IReadOnlyList<TenantRecordResult>> DisableAsync(IReadOnlyList<TenantInfo> current, CancellationToken cancellationToken)
        => await tenants.UpsertAsync([.. current.Select(static t => t with { IsEnabled = false })], cancellationToken);
}
```

`UpsertAsync` 是整份覆盖：只想改启用标记时，先读出当前的 `TenantInfo` 再整体写回，否则显示名与属性会被清空。

## 行为与限制

- 写入：`UpsertAsync` 一次查询取回已有行，已有的更新、没有的新增，各用一次批量方法写入；同一批里同一租户后者整份覆盖前者，同一行只写一次，各项结果相同。`RemoveAsync` 对不存在的 ID 与同一批里重复出现的 ID 逐项返回 `MULTITENANCY_NOTFOUND_001`；不合法的租户 ID 是编程错误，整批在访问数据库前抛出 `ArgumentException`。空集合直接返回空结果。
- 可见性：写入在调用方事务提交后才对其他调用可见。`TenantRecordStore` 在写入时就失效缓存，失效与提交之间的窗口由 `CacheTimeToLive` 收敛；多实例部署时其他实例的缓存同样靠存活时间收敛。绕过 `TenantRecordStore` 直接改表时，自行调用 `DatabaseTenantStore.Invalidate` 与 `ITenantShardResolver.Invalidate`，或等存活时间到期。
- 故障：连接失败、提交失败这类基础设施异常按 `ITenantStore` 契约原样抛出，不伪装成租户不存在；Tenant 阶段据此让调用失败，分片解析器把它映射为 `MULTITENANCY_UNAVAILABLE_002`。
- 不合法的租户 ID 在 `FindAsync` 里直接返回无值，不查库，也不占缓存。
- 枚举有界：`ListAsync` 超过 `MaxListedTenants` 的部分不返回，并记一条警告。
- 表结构由应用管理，`InitTables` / `EnsureCreated` 只用于开发与测试。`PropertiesJson` 用长文本类型（`text` / `nvarchar(max)` / `longtext`）；租户 ID 列长度 128，显示名列长度 256。分片库的表结构同样由应用管理。
- 测试里可以与 `Persistence.InMemory` 一起装配；本包通过 `MultiTenancy.Conformance` 的 `TenantStoreConformance` 与 `TenantShardResolverConformance`，并在 SQLite（`Persistence.SqlSugar`）上验证了实体映射。

## 相关

- [多租户](../guide/multi-tenancy)
- [MultiTenancy.Abstractions](./multitenancy-abstractions)
- [MultiTenancy.InMemory](./multitenancy-inmemory)
- [Persistence.SqlSugar](./persistence-sqlsugar)
- [Persistence.EntityFrameworkCore](./persistence-entityframeworkcore)
- 规范：RFC-0007《多租户》；ADR-0018、ADR-0026、ADR-0057
