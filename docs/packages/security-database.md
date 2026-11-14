# XiHan.Framework.Security.Database

授权的数据库 Provider：权限授予是数据库里的 `PermissionGrantRecord` 行，经 `persistence.relational@1` 的仓储读写，授予存储经 `ICache` 缓存，写入后所有共用缓存的实例立即失效。它只提供 `security.authorization@1`，适用于授予由管理后台维护、多实例共享的生产应用。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Security.Database` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `security.authorization@1`；依赖 `kernel.hosting@1`、`persistence.relational@1`、`caching.local@1` |
| 特性入口 | `DatabaseSecurityFeature`（清单 `xihan.security.database`，`role: provider`） |
| 配置节 | `XiHan:Security`（含 `Database` 子节） |
| 依赖 | `XiHan.Framework.Security.Abstractions`、`XiHan.Framework.Persistence.Abstractions`、`XiHan.Framework.Caching.Abstractions`；不引用数据库驱动 |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

本包只换授予存储（RFC-0008，ADR-0027）：检查器、策略目录、`authenticated` 策略与 Authorize 阶段中间件与内存实现是同一套，`IPermissionFilter` 与 `IPermissionGrantSource` 照常生效。读写分成两个类型：

- `DatabasePermissionGrantStore` 是单例 `IPermissionGrantStore`，Authorize 阶段经检查器查它。它按（主体，租户）一次查询全局行加该租户的行，在根提供者的新作用域里直接开只读工作单元读仓储（配了从库也读主库，ADR-0122），不经 `App.InvokeAsync`：授予在 Authorize 阶段被查询，嵌套调用会再过一遍 Tenant 与 Authorize 阶段，而且要求应用处于服务状态。结果经 `ICache` 缓存（`GenerationCache`，区 `security:grants`，范围是主体 `<种类>:<ID>`、条目是租户，平台为空字符串），装了两级缓存时命中在本机一级缓存，判定不出进程；返回冻结集合，同一个缓存对象只建一次，调用方改不了缓存。
- `PermissionGrantRecordStore` 是作用域服务，在调用的 Transaction 阶段内用调用方的工作单元批量授予、撤销，逐项返回结果，写入后失效涉及主体的缓存（所有共用缓存的实例，ADR-0229）。

本包依赖 `caching.local@1`：Standard / Full 组合包已带 `InMemoryCachingFeature`，自己挑特性的宿主要加上它。多实例部署装 `Caching.Redis` 与 `Caching.Hybrid`，授予与撤销才会立即传到每个实例。

授予是平台级数据：`PermissionGrantRecord` 不实现 `IMultiTenant`，不受当前调用租户过滤，不软删除，租户在授予表自己的列里。平台授予以空字符串（`PermissionGrantRecord.PlatformTenantId`）落库，因为唯一索引对 `NULL` 的语义各数据库不一致；契约边界仍以 `PermissionGrant.TenantId == null` 表示全局。

本包不提供认证。与 `Security.InMemory` 同时装配时顶替它的授权部分，内存实现只剩 ApiKey 认证；认证也可以换成 `Security.Jwt` 或 `Security.OpenIdConnect`。被别的显式授权 Provider 顶替时，本包只保留自己的类型，不注册检查器与中间件，也不核对策略引用。

## 何时使用

- 授予在运行时由管理后台增删，或多实例部署要共享授予：用本包。
- 开发与测试、授予随配置提交：用 [Security.InMemory](./security-inmemory)。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Security.Database --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[快速上手](/getting-started/installation)。本包必须和一个持久化 Provider 一起装配：

```csharp
using SqlSugar;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Persistence.SqlSugar;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Security.Database;
using XiHan.Framework.Security.Jwt;

var builder = WebApplication.CreateBuilder(args);

// JWT 认证 + 数据库授予；Standard 组合包里的内存持久化与内存安全都被顶替
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature(new SqlSugarPersistenceFeature(static (binding, config) =>
    {
        config.DbType = DbType.PostgreSQL;
        config.ConnectionString = binding.ValueOrThrow().Value;
    }))
    .UseFeature<JwtSecurityFeature>()
    .UseFeature<DatabaseSecurityFeature>());

var app = builder.Build();
app.UseXiHan();
app.Run();
```

`Persistence.EntityFrameworkCore` 的应用在自己的 `DbContext` 里登记实体并应用协议约定，四列的联合唯一索引由实体上的 `[EntityIndex]` 建出：

```csharp
using Microsoft.EntityFrameworkCore;
using XiHan.Framework.Security.Database.Grants;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // 平台授予的 TenantId 是空字符串而不是 NULL，唯一索引在各数据库上语义一致；每个租户的授予是 *
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PermissionGrantRecord>();
        modelBuilder.ApplyPersistenceConventions(Database);
    }
}
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Security:Database:CacheTimeToLiveSeconds` | `double` | 30 | 每条缓存的存活时间，必须为正，`0` 与负数让装配失败；授予与撤销经失效立即生效，存活时间只是兜底 |

缓存容量由提供 `caching.local` 的 Provider 管（`XiHan:Caching:InMemory:MaxEntries` 或 `XiHan:Caching:Hybrid:LocalCapacity`）；以前的 `XiHan:Security:Database:CacheSize` 已删除，配置里留着也被忽略。`XiHan:Security:RoleClaim` 决定检查器从哪类声明取角色，见 [Security.Abstractions](./security-abstractions#配置)。

```json
{
  "XiHan": {
    "Bindings": { "db": "Host=localhost;Database=app" },
    "Security": {
      "RoleClaim": "role",
      "Database": { "CacheTimeToLiveSeconds": 30 }
    }
  }
}
```

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `DatabaseSecurityFeature` | 特性入口：登记授予实体的仓储、授予存储、`PermissionChecker`、`authenticated` 策略、`PolicyCatalog` 与 `AuthorizationMiddleware`，经 `CacheSerializerRegistration.AddTypeInfo` 登记授予缓存的序列化类型；不声明健康项 |
| `DatabaseSecurityOptions` | `CacheTimeToLive`（`TimeSpan`，必须为正，缺省 30 秒） |
| `Grants.PermissionGrantRecord` | 授予行：`Id`、`SubjectKind`、`SubjectId`、`Permission`、`TenantId`、`CreatedAt`；`ToGrant()` 换算成 `PermissionGrant`；`TenantColumnOf(tenantId)` 给出租户列的值 |
| `Grants.DatabasePermissionGrantStore` | 单例 `IPermissionGrantStore`，构造参数 `(IServiceProvider, DatabaseSecurityOptions, ICache, ILoggerFactory)`：`GetManyAsync`（缓存未命中的主体一次查库，`Result`，数据库故障为 `SECURITY_UNAVAILABLE_001`）、`InvalidateAsync(subjects, ct)`、`InvalidateAllAsync(ct)`（作用于所有共用缓存的实例，缓存后端故障时抛出）；常量 `CacheRegion` = `security:grants` |
| `Grants.PermissionGrantRecordStore` | 作用域服务，实现 `IPermissionGrantAdministration` 与 `IAsyncDisposable`：`GrantAsync`、`RevokeAsync` 批量逐项结果，`ListAsync(PermissionGrantQuery)` 分页并计数，作用域释放时再失效一次；构造参数多了 `ILoggerFactory` |

## 使用示例

管理操作在处理器里经 `IPermissionGrantAdministration`（作用域服务，就是 `PermissionGrantRecordStore`）写入，Transaction 阶段已经开了工作单元：

```csharp
using XiHan.Framework.Security.Abstractions.Permissions;

// 作用域服务，在操作的处理器里使用
public sealed class RoleAdministration(IPermissionGrantAdministration grants)
{
    // 一次授予多项：已存在的返回成功且不重复写
    public ValueTask<IReadOnlyList<Result>> SetUpEditorAsync(string tenantId, CancellationToken cancellationToken)
        => grants.GrantAsync(
        [
            new PermissionGrant(GrantSubject.ForRole("editor"), "orders.read"),
            new PermissionGrant(GrantSubject.ForRole("editor"), "orders.write", tenantId),
        ], cancellationToken);

    // 不存在的授予逐项返回 SECURITY_NOTFOUND_002
    public ValueTask<IReadOnlyList<Result>> RevokeWriteAsync(string tenantId, CancellationToken cancellationToken)
        => grants.RevokeAsync([new PermissionGrant(GrantSubject.ForRole("editor"), "orders.write", tenantId)], cancellationToken);

    public Task<IReadOnlyList<PermissionGrantRecord>> ListAsync(string userId, CancellationToken cancellationToken)
        => grants.ListAsync(GrantSubject.ForUser(userId), tenantId: null, skip: 0, take: 100, cancellationToken);
}
```

## 行为与限制

- 被选为授权能力时，特性在 `Configure` 里登记启动核对 `PolicyCatalog.EnforceOn`，核对操作契约按名引用的策略：内核在容器建好之后、任何特性启动之前执行它，不存在（含 `permission:<code>` 的权限码未登记）即抛出 `SECURITY_VALIDATION_004`、宿主启动失败，不论失败策略（`IsolateFeature` 也一样，不包成 `KERNEL_HOSTING_002`）；之后登记的操作（插件握手之后）在登记时核对（ADR-0080、ADR-0085）。特性本身不实现 `IHostedFeature`。
- 授予：一次查询取回已有行（主体种类、主体 ID、权限码、租户各取集合做 `IN`，再在内存里精确匹配），已存在的返回成功且不重复写，同一批里重复的项同样成功，没有的用一次 `AddRangeAsync` 写入。空集合直接返回空结果。
- 范围：平台授予的 `TenantId` 用 `null`，每个租户的授予用 `PermissionGrant.AnyTenant`（ADR-0197）；`GrantAsync` 与 `RevokeAsync` 遇到空白租户视为编程错误，在访问数据库前抛出 `ArgumentException`。
- 撤销：一次 `RemoveRangeAsync`；不存在的授予与同一批里第二次出现的同一授予返回 `SECURITY_NOTFOUND_002`（消息键 `security.grant.not_found`，参数 `subject` / `permission` / `tenant`，全局时 `tenant` 为 `*`）。
- 列表：按主体种类、主体 ID、权限码、租户排序；`subject` 为 `null` 不按主体过滤，`tenantId` 为 `null` 不按租户过滤，否则只列该租户专属的行；每页最多 1000 条。
- 缓存：条目按 `CacheTimeToLive` 过期，容量与淘汰由 `caching.local` 的 Provider 决定。`PermissionGrantRecordStore` 写入后对涉及的主体调用一次 `InvalidateAsync`，作用域释放时（调用已提交或回滚）再调用一次；失效按主体换代数，主体在所有租户下的条目一起作废，失效之前开始的读取写回的旧条目不会被当作命中，授予与撤销在提交之后立即生效（ADR-0194、ADR-0229）。失效作用于所有共用缓存的实例：装了 `Caching.Redis` 与 `Caching.Hybrid` 时所有实例一起生效；只装 Redis 时 `ICache` 仍是进程内的，其他实例靠存活时间收敛。绕过 `PermissionGrantRecordStore` 直接改表时，自行 `await InvalidateAsync(subjects, ct)` 或等存活时间。
- 故障：开启、读取、提交只读工作单元时的数据库故障（连接、命令超时、事务失败）返回 `SECURITY_UNAVAILABLE_001`（消息键 `security.grant_store.unavailable`，不带参数），检查器原样返回，调用以 503 结束——不会落到 403。底层原因只记警告日志，不进响应。失败不进缓存，数据库恢复后下一次判定立即重新查库。调用方取消照常抛出；容器里缺工作单元或仓储是装配错误，照常抛出（ADR-0071）。
- 缓存后端故障：判定直接查库、不写缓存，结果仍然正确，警告每 10 秒最多一条。写入时的失效遇到故障就抛出（`CACHING_UNAVAILABLE_001`），调用失败（内核转成 `KERNEL_UNEXPECTED_001`）、事务回滚：撤销传不到其他实例时不报成功。作用域释放时的失效遇到故障只记错误日志，已提交的写入在存活时间内对所有实例生效。`PermissionGrantRecordStore` 的同步释放会阻塞到失效写完，宿主应异步释放作用域。
- 跨进程缓存里的值类型（`GenerationCacheEntry<GrantSetEntry>`、`string`）由本包经 `AddTypeInfo` 登记；应用自己登记 `ICacheSerializer` 时要并进 `CacheTypeInfoSource`，见[缓存读写 · 序列化器](/caching/basics#序列化器)。共用一个 Redis 的多个应用要用不同的 `KeyPrefix`。
- 直接构造 `DatabasePermissionGrantStore`、`PermissionGrantRecordStore` 的代码按新签名传参，原来的 `Invalidate` / `InvalidateAll` 改为 `await InvalidateAsync(…, ct)` / `InvalidateAllAsync(ct)`，`CachedEntryCount` 已删除；经容器构造的不受影响。
- 已有的库按唯一索引的名字补建索引；表里有重复授予时先去重。
- 表结构由应用管理，`InitTables` 只用于开发与测试。唯一索引 `UX_PermissionGrantRecord_SuKi_SuId_Pe_TeId`（`(SubjectKind, SubjectId, Permission, TenantId)`，实体上的 `[EntityIndex]`，SqlSugar 的 `InitTables` 与 EF Core 的 `ApplyPersistenceConventions(Database)` 都按它建，ADR-0072），按主体的查询走它的 `(SubjectKind, SubjectId)` 前缀；两个调用同时授予同一项时后插入的那一批返回 `PERSISTENCE_CONFLICT_003`、调用回滚，重试即成功（已存在）；列长度：`SubjectKind` 32、`SubjectId` 256、`Permission` 256、`TenantId` 128。
- 通过 `Security.Conformance` 的 `PermissionGrantStoreConformance`（含存储不可达用例：连接指向不存在的 SQLite 文件）与 `PermissionGrantAdministrationConformance`（含另一个实例的用例，两个宿主共用一个 SQLite 文件与一个 `ICache`），并在 SQLite（`Persistence.SqlSugar`）上验证了实体映射。缓存故障用 [Testing](./testing) 的 `FaultyCache` 复现。

## 相关

- [授权](/security/authorization)
- [Security.Abstractions](./security-abstractions)
- [Security.InMemory](./security-inmemory)
- [Persistence.SqlSugar](./persistence-sqlsugar)
- [Persistence.EntityFrameworkCore](./persistence-entityframeworkcore)
- [两级缓存与键管理](/caching/hybrid)
- 规范：RFC-0008《认证与授权》；ADR-0027、ADR-0229
