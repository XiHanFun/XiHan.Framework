# XiHan.Framework.Security.Database

授权的数据库 Provider：权限授予是数据库里的 `PermissionGrantRecord` 行，经 `persistence.relational@1` 的仓储读写，授予存储带有界的 TTL 缓存。它只提供 `security.authorization@1`，适用于授予由管理后台维护、多实例共享的生产应用。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Security.Database` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `security.authorization@1`；依赖 `kernel.hosting@1`、`persistence.relational@1` |
| 特性入口 | `DatabaseSecurityFeature`（清单 `xihan.security.database`，`role: provider`） |
| 配置节 | `XiHan:Security`（含 `Database` 子节） |
| 依赖 | `XiHan.Framework.Security.Abstractions`、`XiHan.Framework.Persistence.Abstractions`；不引用数据库驱动 |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

本包只换授予存储（RFC-0008，ADR-0027）：检查器、策略目录、`authenticated` 策略与 Authorize 阶段中间件与内存实现是同一套，`IPermissionFilter` 与 `IPermissionGrantSource` 照常生效。读写分成两个类型：

- `DatabasePermissionGrantStore` 是单例 `IPermissionGrantStore`，Authorize 阶段经检查器查它。它按（主体，租户）一次查询全局行加该租户的行，在根提供者的新作用域里直接开只读工作单元读仓储，不经 `App.InvokeAsync`：授予在 Authorize 阶段被查询，嵌套调用会再过一遍 Tenant 与 Authorize 阶段，而且要求应用处于服务状态。结果进有界的 TTL 缓存，返回冻结集合，调用方改不了缓存。
- `PermissionGrantRecordStore` 是作用域服务，在调用的 Transaction 阶段内用调用方的工作单元批量授予、撤销，逐项返回结果，写入后失效涉及主体的缓存。

授予是平台级数据：`PermissionGrantRecord` 不实现 `IMultiTenant`，不受当前调用租户过滤，不软删除，租户在授予表自己的列里。全局授予以空字符串（`PermissionGrantRecord.GlobalTenantId`）落库，因为唯一索引对 `NULL` 的语义各数据库不一致；契约边界仍以 `PermissionGrant.TenantId == null` 表示全局。

本包不提供认证。与 `Security.InMemory` 同时装配时顶替它的授权部分，内存实现只剩 ApiKey 认证；认证也可以换成 `Security.Jwt` 或 `Security.OpenIdConnect`。被别的显式授权 Provider 顶替时，本包只保留自己的类型，不注册检查器与中间件。

## 何时使用

- 授予在运行时由管理后台增删，或多实例部署要共享授予：用本包。
- 开发与测试、授予随配置提交：用 [Security.InMemory](./security-inmemory)。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Security.Database" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。本包必须和一个持久化 Provider 一起装配：

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

`Persistence.EntityFrameworkCore` 的应用在自己的 `DbContext` 里登记实体，并给四列建联合唯一索引：

```csharp
using Microsoft.EntityFrameworkCore;
using XiHan.Framework.Security.Database.Grants;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // 全局授予的 TenantId 是空字符串而不是 NULL，唯一索引在各数据库上语义一致
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<PermissionGrantRecord>(static t => t.HasIndex(static x => new { x.SubjectKind, x.SubjectId, x.Permission, x.TenantId }).IsUnique());
}
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Security:Database:CacheSize` | `int` | 10000 | 缓存条目上限，每个（主体，租户）组合一条；满时整体清空 |
| `XiHan:Security:Database:CacheTimeToLiveSeconds` | `double` | 30 | 每条缓存的存活时间；`0` 表示不过期，只靠失效与容量清空 |

`XiHan:Security:RoleClaim` 决定检查器从哪类声明取角色，见 [Security.Abstractions](./security-abstractions#配置)。

```json
{
  "XiHan": {
    "Bindings": { "db": "Host=localhost;Database=app" },
    "Security": {
      "RoleClaim": "role",
      "Database": { "CacheSize": 10000, "CacheTimeToLiveSeconds": 30 }
    }
  }
}
```

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `DatabaseSecurityFeature` | 特性入口：登记授予实体的仓储、授予存储、`PermissionChecker`、`authenticated` 策略、`PolicyCatalog` 与 `AuthorizationMiddleware`；不声明健康项 |
| `DatabaseSecurityOptions` | `CacheSize`、`CacheTimeToLive`（`TimeSpan?`，`null` 不过期） |
| `Grants.PermissionGrantRecord` | 授予行：`Id`、`SubjectKind`、`SubjectId`、`Permission`、`TenantId`、`CreatedAt`；`ToGrant()` 换算成 `PermissionGrant`；`TenantColumnOf(tenantId)` 给出租户列的值 |
| `Grants.DatabasePermissionGrantStore` | 单例 `IPermissionGrantStore`：`GetGrantsAsync`、`Invalidate(subject?)`、`CachedEntryCount` |
| `Grants.PermissionGrantRecordStore` | 作用域服务：`GrantAsync`、`RevokeAsync` 批量逐项结果，`ListAsync(subject?, tenantId?, skip, take)` 分页 |
| `Grants.PermissionGrantResult` | 一项结果：`Grant`、`Outcome` |

## 使用示例

管理操作在处理器里经 `PermissionGrantRecordStore` 写入，Transaction 阶段已经开了工作单元：

```csharp
using XiHan.Framework.Security.Abstractions.Permissions;
using XiHan.Framework.Security.Database.Grants;

// 作用域服务，在操作的处理器里使用
public sealed class RoleAdministration(PermissionGrantRecordStore grants)
{
    // 一次授予多项：已存在的返回成功且不重复写
    public ValueTask<IReadOnlyList<PermissionGrantResult>> SetUpEditorAsync(string tenantId, CancellationToken cancellationToken)
        => grants.GrantAsync(
        [
            new PermissionGrant(GrantSubject.ForRole("editor"), "orders.read"),
            new PermissionGrant(GrantSubject.ForRole("editor"), "orders.write", tenantId),
        ], cancellationToken);

    // 不存在的授予逐项返回 SECURITY_NOTFOUND_002
    public ValueTask<IReadOnlyList<PermissionGrantResult>> RevokeWriteAsync(string tenantId, CancellationToken cancellationToken)
        => grants.RevokeAsync([new PermissionGrant(GrantSubject.ForRole("editor"), "orders.write", tenantId)], cancellationToken);

    public Task<IReadOnlyList<PermissionGrantRecord>> ListAsync(string userId, CancellationToken cancellationToken)
        => grants.ListAsync(GrantSubject.ForUser(userId), tenantId: null, skip: 0, take: 100, cancellationToken);
}
```

## 行为与限制

- 授予：一次查询取回已有行（主体种类、主体 ID、权限码、租户各取集合做 `IN`，再在内存里精确匹配），已存在的返回成功且不重复写，同一批里重复的项同样成功，没有的用一次 `AddRangeAsync` 写入。空集合直接返回空结果。
- 租户：全局授予的 `TenantId` 用 `null`；`GrantAsync` 与 `RevokeAsync` 遇到空白租户视为编程错误，在访问数据库前抛出 `ArgumentException`。
- 撤销：一次 `RemoveRangeAsync`；不存在的授予与同一批里第二次出现的同一授予返回 `SECURITY_NOTFOUND_002`（消息键 `security.grant.not_found`，参数 `subject` / `permission` / `tenant`，全局时 `tenant` 为 `*`）。
- 列表：按主体种类、主体 ID、权限码、租户排序；`subject` 为 `null` 不按主体过滤，`tenantId` 为 `null` 不按租户过滤，否则只列该租户专属的行；每页最多 1000 条。
- 缓存：条目按 `CacheTimeToLive` 过期，条目数达到 `CacheSize` 时整体清空，不逐条淘汰。`PermissionGrantRecordStore` 写入后对涉及的每个主体调用一次 `Invalidate`；写入在调用方事务提交后才对其他调用可见，失效与提交之间的窗口由存活时间收敛。多实例部署时其他实例的缓存同样靠存活时间收敛，需要更快生效时调小 `CacheTimeToLiveSeconds`。
- 故障：读取时的基础设施异常原样抛出，`PermissionChecker` 不捕获，由内核转成失败结果，宁可失败不可放行。
- 表结构由应用管理，`InitTables` 只用于开发与测试。`(SubjectKind, SubjectId, Permission, TenantId)` 建唯一索引，按主体的查询走它的 `(SubjectKind, SubjectId)` 前缀；列长度：`SubjectKind` 32、`SubjectId` 256、`Permission` 256、`TenantId` 128。
- 通过 `Security.Conformance` 的 `PermissionGrantStoreConformance`，并在 SQLite（`Persistence.SqlSugar`）上验证了实体映射。

## 相关

- [授权](../guide/authorization)
- [Security.Abstractions](./security-abstractions)
- [Security.InMemory](./security-inmemory)
- [Persistence.SqlSugar](./persistence-sqlsugar)
- [Persistence.EntityFrameworkCore](./persistence-entityframeworkcore)
- 规范：RFC-0008《认证与授权》；ADR-0027
