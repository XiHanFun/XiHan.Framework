# XiHan.Framework.Security.Database

`security.authorization@1` 的数据库 Provider（Ring 3，`role: provider`，RFC-0008，ADR-0027）。权限授予以 `PermissionGrantRecord`（主体种类、主体 ID、权限码、租户）经 `persistence.relational@1` 的仓储读写；`IPermissionGrantStore` 的实现按主体与租户一次查询并进有界的 TTL 缓存。只提供授权：认证仍由 `Security.InMemory`（ApiKey）、`Security.Jwt` 或 `Security.OpenIdConnect` 提供，与 `Security.InMemory` 同时装配时本包顶替其授权部分、内存实现只剩认证。

```csharp
builder.AddXiHan(x => x
    .UseFeature(new EntityFrameworkCorePersistenceFeature<AppDbContext>(static (binding, o) => o.UseNpgsql(binding.ValueOrThrow().Value)))
    .UseFeature<JwtSecurityFeature>()
    .UseFeature<DatabaseSecurityFeature>());

// AppDbContext.OnModelCreating：四列联合唯一；全局授予的 TenantId 是空字符串而不是 NULL，唯一索引在各数据库上语义一致
modelBuilder.Entity<PermissionGrantRecord>(static t => t.HasIndex(static x => new { x.SubjectKind, x.SubjectId, x.Permission, x.TenantId }).IsUnique());

// 管理操作里（Transaction 阶段内）批量授予 / 撤销，逐项结果；写入后涉及主体的缓存立即失效
var granted = await store.GrantAsync([new PermissionGrant(GrantSubject.ForRole("editor"), "orders.write"), new PermissionGrant(GrantSubject.ForUser("alice"), "reports.export", "acme")], ct);
var revoked = await store.RevokeAsync([new PermissionGrant(GrantSubject.ForRole("editor"), "orders.write")], ct);   // 不存在 → SECURITY_NOTFOUND_002
var rows = await store.ListAsync(GrantSubject.ForUser("alice"), tenantId: null, skip: 0, take: 100, ct);
```

```json
{ "XiHan": { "Security": { "RoleClaim": "role", "Database": { "CacheSize": 10000, "CacheTimeToLiveSeconds": 30 } } } }
```

| 类型 | 用途 |
| --- | --- |
| `DatabaseSecurityFeature` | 登记授予实体的仓储，装配授予存储、`PermissionChecker`、`authenticated` 策略、`PolicyCatalog` 与 Authorize 阶段的 `AuthorizationMiddleware`；被别的显式 Provider 顶替时只留本包自己的类型。不声明健康项 |
| `DatabaseSecurityOptions` | `XiHan:Security:Database`：`CacheSize`（默认 10000）、`CacheTimeToLiveSeconds`（默认 30，0 不过期） |
| `Grants.PermissionGrantRecord` | 授予行：`SubjectKind` / `SubjectId` / `Permission` / `TenantId` / `CreatedAt`；全局授予的 `TenantId` 是 `GlobalTenantId`（空字符串）；`ToGrant()` 换算成契约的 `PermissionGrant`（空字符串 → `null`） |
| `Grants.DatabasePermissionGrantStore` | `IPermissionGrantStore`（单例）：`GetGrantsAsync(subject, tenantId)` 一次查询全局行加该租户的行；缓存键（主体种类、主体 ID、租户），`Invalidate(subject?)` 按主体或全部失效 |
| `Grants.PermissionGrantRecordStore` | 作用域，须在调用的 Transaction 阶段内使用：`GrantAsync` / `RevokeAsync` 批量逐项结果、`ListAsync` 分页 |
| `Grants.PermissionGrantResult` | 一项的授予与结果 |

## 规则

- 授予是平台级数据：实体不实现 `IMultiTenant`，不受当前调用租户过滤，不软删除；租户在授予表自己的列里。全局授予以空字符串落库（唯一索引对 NULL 的语义各数据库不一致），契约边界仍以 `PermissionGrant.TenantId == null` 表示全局；`GrantAsync` / `RevokeAsync` 拒绝空白的租户（编程错误，抛 `ArgumentException`）。
- `DatabasePermissionGrantStore` 读数据库不经 `App.InvokeAsync`（授予在 Authorize 阶段被查询，嵌套调用会再次经过 Tenant / Authorize 阶段，而且要求应用处于服务状态），而是在根提供者的新作用域里直接开只读工作单元读仓储；基础设施异常原样抛出，`PermissionChecker` 不捕获，由内核转成 Unexpected 结果（宁可失败，不可放行）。
- 缓存：每个（主体、租户）一条，`CacheTimeToLive` 后过期；条目数达到 `CacheSize` 时整体清空（不逐条淘汰，容量只防无界增长）；返回的集合是冻结集合，调用方改不了缓存。`PermissionGrantRecordStore` 写入后对涉及的主体调用 `Invalidate`；写入在调用方的事务提交后才对别的调用可见，失效与提交之间的窗口由 TTL 收敛。多实例部署时别的实例靠 TTL 收敛，需要立即生效的改小 `CacheTimeToLiveSeconds`。
- `GrantAsync`：一次查询取回已有行（主体种类 / ID / 权限码 / 租户各取集合做 `IN`，再内存精确匹配），已存在的返回成功且不重复写，同一批里重复的项同样成功，没有的用 `AddRangeAsync` 一次写入；`RevokeAsync` 同样一次 `RemoveRangeAsync`，不存在（含同一批第二次）→ `SECURITY_NOTFOUND_002`（`security.grant.not_found`，参数 `subject` / `permission` / `tenant`，全局时 `tenant` 为 `*`）；`ListAsync` 按主体种类、主体 ID、权限码、租户排序，`tenantId` 为 `null` 不按租户过滤、否则只列该租户专属的行。
- 表结构由应用管理（`InitTables` 只用于开发与测试）：`(SubjectKind, SubjectId, Permission, TenantId)` 建唯一索引，按主体的查询走它的 `(SubjectKind, SubjectId)` 前缀。

## 依赖方向

依赖 `XiHan.Framework.Security.Abstractions` 与 `XiHan.Framework.Persistence.Abstractions`；不引用任何数据库驱动，具体数据库由装配的持久化 Provider 决定。

## 状态

P5：可用，通过 `Security.Conformance`；对 SQLite（`Persistence.SqlSugar`）验证实体映射。
