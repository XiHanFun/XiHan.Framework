# XiHan.Framework.MultiTenancy.InMemory

内存租户存储。清单 `role: default`，提供 `multitenancy.context@1` 与 `multitenancy.sharding@1`。

```json
{
  "XiHan": {
    "MultiTenancy": {
      "RequireTenant": false,
      "Tenants": {
        "acme": { "DisplayName": "Acme", "IsEnabled": true, "Properties": { "ConnectionString": "Host=acme-db;Database=app", "DatabaseProvider": "postgresql" } },
        "beta": { "IsEnabled": false }
      }
    }
  }
}
```

## 语义

- 启动时从 `XiHan:MultiTenancy:Tenants` 装载；运行时可用 `InMemoryTenantStore.Set` / `Remove` 增删，上限 `XiHan:MultiTenancy:InMemory:MaxTenants`（默认 1000），超出返回 `MULTITENANCY_UNAVAILABLE_001`。
- 注册解析器顺序：`ClaimTenantResolver`（已认证主体的 `tenant` 声明）→ `BaggageTenantResolver`（`xihan.tenant`）。
- 在 Tenant 阶段加入 `TenantResolutionMiddleware`。
- 分片能力被选中时注册 `TenantStoreShardResolver`：租户属性 `ConnectionString` 有值即分库，`DatabaseProvider` 为提供者名，`Shard.<key>` 透传；结果按租户缓存（`XiHan:MultiTenancy:ShardCacheSize`，默认 10000），`Set` / `Remove` 改了租户连接后调用 `ITenantShardResolver.Invalidate`。
- 健康项 `multitenancy.inmemory.capacity`（存储自己实现，同步计算）：租户数达到 `MaxTenants` 即 Degraded（`MULTITENANCY_UNAVAILABLE_001`），否则 Ready。

## 依赖方向

依赖 `XiHan.Framework.MultiTenancy.Abstractions`。

## 状态

P2：可用，通过 `MultiTenancy.Conformance` 套件。
