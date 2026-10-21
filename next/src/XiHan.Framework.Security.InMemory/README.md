# XiHan.Framework.Security.InMemory

内存安全。清单 `role: default`，提供 `security.authentication@1`（`ApiKey` 方案）与 `security.authorization@1`（RBAC 授予存储）。只用于开发、原型与测试。

```json
{
  "XiHan": {
    "Security": {
      "InMemory": {
        "Principals": {
          "alice": { "ApiKey": "dev-alice", "DisplayName": "Alice", "Roles": "admin,editor", "Tenant": "acme", "Claims": { "email": "alice@example.com" } }
        },
        "Grants": {
          "role": { "admin": "*", "editor": "orders.read,orders.write" },
          "user": { "alice": "reports.read" }
        }
      }
    }
  }
}
```

## 语义

- `ApiKeyAuthenticationHandler`：凭据 `ApiKey <key>` 对应一个主体；键比较常量时间，不匹配返回 `SECURITY_DENIED_003`。
- `InMemoryPermissionGrantStore`：配置里的授予是全局的；运行时可用 `Grant` / `Revoke` 增加租户专属授予。上限 `XiHan:Security:InMemory:MaxPrincipals`（1000）与 `MaxGrants`（10000）。
- 当另一个显式 Provider（如 `Security.Jwt`）被选中提供 `security.authentication` 时，本包只提供授权部分。

## 依赖方向

依赖 `XiHan.Framework.Security.Abstractions`。

## 状态

P2：可用，通过 `Security.Conformance` 套件。
