# XiHan.Framework.MultiTenancy.Abstractions

多租户契约（Ring 2）。能力 `multitenancy.context@1`（租户解析、校验与存储）与 `multitenancy.sharding@1`（按租户选择数据连接）由 Provider 提供。

| 契约 | 用途 |
| --- | --- |
| `TenantInfo`、`TenantIdentifier` | 租户信息（ID、显示名、是否启用、属性）与 ID 文法 |
| `ITenantStore` | 按 ID 查找与枚举租户；Provider 实现它 |
| `ITenantResolver`、`ClaimTenantResolver`、`BaggageTenantResolver` | 从调用上下文解析租户 ID 候选；按注册顺序，先解析出的生效 |
| `MultiTenancyOptions` | 默认租户、是否必须有租户、是否核对存储、租户声明类型、分片缓存容量 |
| `TenantResolutionMiddleware` | Tenant 阶段：解析 → 校验格式 → 核对存储（存在且启用）→ 写入安全上下文 → 解析分片写入调用上下文（装配了解析器时；失败即调用失败） |
| `ITenantShardResolver`、`TenantShard`、`TenantShardKeys`、`TenantStoreShardResolver` | 租户分片：解析器契约（`ResolveAsync` → `Result<Maybe<TenantShard>>`、`Invalidate`）、分片（连接串、小写提供者名、透传属性）、租户属性键（`ConnectionString`、`DatabaseProvider`、`Shard.*`）与从 `ITenantStore` 推出分片的默认解析器（按租户有界缓存） |
| `TenantShardInvocationExtensions`、`TenantShardBinding` | 调用上下文里的分片（`GetTenantShard` / `SetTenantShard`）与持久化 Provider 在作用域内用的分片连接绑定（主值是分片连接串，属性并入分片属性与 `tenant` / `provider`） |

解析顺序由 Provider 注册决定，默认先主体声明后 Baggage：已认证主体所属的租户优先于客户端自报的租户。当前租户经 `context.GetSecurity().Tenant` 读取，没有环境态访问器。

分库：租户属性 `ConnectionString` 有值即分库，Tenant 阶段解析一次写进调用上下文，持久化 Provider（SqlSugar、EF Core）在作用域内按分片选连接；解析失败返回 `MULTITENANCY_UNAVAILABLE_002`，不降级为共库。租户连接变更后调用 `ITenantShardResolver.Invalidate`。分片库的表结构由应用管理。

## 依赖方向

依赖 `XiHan.Framework.Kernel`。被 `MultiTenancy.InMemory` 等实现包引用。

## 状态

P2：可用；P5 加入分片。
