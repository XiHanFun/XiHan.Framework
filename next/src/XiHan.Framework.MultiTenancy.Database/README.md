# XiHan.Framework.MultiTenancy.Database

`multitenancy.context@1` 与 `multitenancy.sharding@1` 的数据库 Provider（Ring 3，`role: provider`，RFC-0007，ADR-0026）。租户目录以 `TenantRecord`（ID、显示名、启用标记、属性 JSON）经 `persistence.relational@1` 的仓储读写（需要装配一个持久化 Provider）；单例目录 `DatabaseTenantStore` 直接开工作单元读取并按租户有界缓存，分片仍由抽象包的 `TenantStoreShardResolver` 从租户属性推出。装配后顶替默认的 `MultiTenancy.InMemory`。

```csharp
builder.AddXiHan(x => x
    .UseFeature(new EntityFrameworkCorePersistenceFeature<AppDbContext>(static (binding, o) => o.UseNpgsql(binding.ValueOrThrow().Value)))
    .UseFeature<DatabaseMultiTenancyFeature>());

// AppDbContext.OnModelCreating：属性列用长文本类型；SqlSugar 按约定映射，不需要登记
modelBuilder.Entity<TenantRecord>(static t => t.Property(static x => x.PropertiesJson).HasColumnType("text"));

// 管理操作里（Transaction 阶段内）批量写入；写完即失效目录与分片解析器的缓存
var results = await store.UpsertAsync(
[
    new TenantInfo { Id = "acme", DisplayName = "Acme", Properties = new Dictionary<string, string>(StringComparer.Ordinal) { [TenantShardKeys.ConnectionString] = "Host=acme-db;Database=app" } },
    new TenantInfo { Id = "beta", IsEnabled = false },
], ct);
var removed = await store.RemoveAsync(["beta"], ct);
```

```json
{ "XiHan": { "MultiTenancy": { "RequireTenant": false, "Database": { "CacheSize": 10000, "CacheTimeToLiveSeconds": 30, "MaxListedTenants": 10000 } } } }
```

| 类型 | 用途 |
| --- | --- |
| `DatabaseMultiTenancyFeature` | 登记 `TenantRecord` 的仓储、目录、解析器（`ClaimTenantResolver` → `BaggageTenantResolver`）与 Tenant 阶段的 `TenantResolutionMiddleware`；分片能力被选中时注册 `TenantStoreShardResolver`；只为被选中的能力注册。清单不声明 `persistence.relational@1`（持久化 Provider 可选依赖 `multitenancy.sharding@1`，再声明会成环），启动时核对图里有持久化 Provider，没有即 `KERNEL_CAPABILITY_001` 中断启动 |
| `Tenants.TenantRecord` | 存储行：主键是租户 ID，`DisplayName`、`IsEnabled`、`PropertiesJson`（键区分大小写的字符串字典的 JSON 对象，源生成序列化），创建 / 修改时间与并发戳；平台级数据，不实现 `IMultiTenant`、不软删除 |
| `Tenants.DatabaseTenantStore` | 单例 `ITenantStore`：`FindAsync` 按租户缓存 `Maybe<TenantInfo>`（不存在也缓存），容量 `CacheSize`（满时整体清空）、每条存活 `CacheTimeToLive`；`ListAsync` 按 ID 分页读完（每页 1000，最多 `MaxListedTenants` 条，超出记警告）、不走缓存；`Invalidate(tenantId?)` |
| `Tenants.TenantRecordStore` | 作用域、用调用方的工作单元：`UpsertAsync` / `RemoveAsync` 批量逐项结果（不存在 → `MULTITENANCY_NOTFOUND_001`），`ListAsync` 分页；写入后失效目录与分片解析器（可选依赖，没有时跳过）的缓存 |
| `DatabaseMultiTenancyOptions` | `XiHan:MultiTenancy:Database`：`CacheSize`（10000）、`CacheTimeToLiveSeconds`（30，0 不过期）、`MaxListedTenants`（10000） |

## 规则

- 目录是 Tenant 阶段自己要查的东西，所以读取不经 `App.InvokeAsync` 再走一遍管道（嵌套调用会再过 Tenant / Authorize 阶段，`RequireTenant` 时无租户即失败，而且启动期读不了），而是在根提供者的新作用域里直接开只读工作单元取仓储；租户是平台级数据，工作单元不带租户。
- 基础设施异常（连接不上、提交失败）按 `ITenantStore` 契约原样抛出，不伪装成不存在；Tenant 阶段据此让调用失败，分片解析器把它映射为 `MULTITENANCY_UNAVAILABLE_002`。
- `TenantRecordStore` 的写入在调用方事务提交后才可见；它在写入时就失效缓存，失效与提交之间的窗口由 `CacheTimeToLive` 收敛。绕过 `TenantRecordStore` 直接改表时自行调用 `DatabaseTenantStore.Invalidate` 与 `ITenantShardResolver.Invalidate`，或等存活时间到期。
- 同一批里同一租户后者整份覆盖前者（同一行只写一次）；删除对不存在与重复出现的 ID 逐项返回不存在；不合法的租户 ID 是编程错误，整批在触库前抛出。
- 表结构由应用管理（`InitTables` / `EnsureCreated` 只用于开发与测试）：`PropertiesJson` 列用长文本类型（`text` / `nvarchar(max)` / `longtext`）；分片库的表结构同样由应用管理。
- 解析顺序、`DefaultTenant` / `RequireTenant` / `ValidateAgainstStore` 与分片语义都在 `MultiTenancy.Abstractions`，本包只换存储。

## 依赖方向

依赖 `XiHan.Framework.MultiTenancy.Abstractions` 与 `XiHan.Framework.Persistence.Abstractions`；不引用任何数据库驱动，具体数据库由装配的持久化 Provider 决定。

## 状态

P5：可用，通过 `MultiTenancy.Conformance`（`TenantStoreConformance` 与 `TenantShardResolverConformance`），对 SQLite（`Persistence.SqlSugar`）验证实体映射。
