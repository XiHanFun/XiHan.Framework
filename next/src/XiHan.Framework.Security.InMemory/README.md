# XiHan.Framework.Security.InMemory

内存安全。清单 `role: default`，提供 `security.authentication@1`（`ApiKey` 方案）与 `security.authorization@1`（RBAC 授予存储）。只用于开发、原型与测试。被选为授权能力时，特性启动时调用 `PolicyCatalog.EnforceOn` 核对操作契约按名引用的策略：不存在（含 `permission:<code>` 的权限码未登记）即 `SECURITY_VALIDATION_004`，本特性启动失败（默认失败策略下应用启动失败）；之后登记的操作（插件握手之后）在登记时核对（ADR-0080）。

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

- `ApiKeyAuthenticationHandler`：凭据 `ApiKey <key>` 对应一个主体；键比较常量时间，不匹配返回 `SECURITY_DENIED_003`。主体记录的租户写成声明，类型取 `XiHan:MultiTenancy:TenantClaim`（缺省 `tenant`），与多租户的声明解析器读同一个键（RFC-0008 认证第 4 条）。
- `InMemoryPermissionGrantStore`：配置里的授予是全局的；运行时可用 `Grant` / `Revoke` 增加租户专属授予。上限 `XiHan:Security:InMemory:MaxPrincipals`（1000）与 `MaxGrants`（10000）。被容量拒绝的授予不留下条目，撤销主体在某租户下的最后一条授予时连同这一组移除。
- 当另一个显式 Provider（如 `Security.Jwt`）被选中提供 `security.authentication` 时，本包只提供授权部分。
- 健康项 `security.inmemory.capacity`（`InMemorySecurityHealthContributor`，同步计算）：主体存储或授予存储任一达到上限即 Degraded（`SECURITY_UNAVAILABLE_002`，先报主体存储），否则 Ready；被顶替的能力对应的存储不参与判定。

## 依赖方向

依赖 `XiHan.Framework.Security.Abstractions`。

## 状态

P2：可用，通过 `Security.Conformance` 套件。
