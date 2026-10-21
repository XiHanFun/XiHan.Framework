# XiHan.Framework.MultiTenancy.Abstractions

多租户契约（Ring 2）。能力 `multitenancy.context@1` 由 Provider 提供；`multitenancy.sharding@1` 预留。

| 契约 | 用途 |
| --- | --- |
| `TenantInfo`、`TenantIdentifier` | 租户信息（ID、显示名、是否启用、属性）与 ID 文法 |
| `ITenantStore` | 按 ID 查找与枚举租户；Provider 实现它 |
| `ITenantResolver`、`ClaimTenantResolver`、`BaggageTenantResolver` | 从调用上下文解析租户 ID 候选；按注册顺序，先解析出的生效 |
| `MultiTenancyOptions` | 默认租户、是否必须有租户、是否核对存储、租户声明类型 |
| `TenantResolutionMiddleware` | Tenant 阶段：解析 → 校验格式 → 核对存储（存在且启用）→ 写入安全上下文 |

解析顺序由 Provider 注册决定，默认先主体声明后 Baggage：已认证主体所属的租户优先于客户端自报的租户。当前租户经 `context.GetSecurity().Tenant` 读取，没有环境态访问器。

## 依赖方向

依赖 `XiHan.Framework.Kernel`。被 `MultiTenancy.InMemory` 等实现包引用。

## 状态

P2：可用。
