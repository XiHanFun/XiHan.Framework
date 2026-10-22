# XiHan.Framework.Security.Abstractions

安全契约（Ring 2）。能力 `security.authentication@1`（凭据 → 主体）与 `security.authorization@1`（策略判定）由 Provider 提供；主体、租户、授权请求与策略接口等原语在 `Kernel.Security`。

| 契约 | 用途 |
| --- | --- |
| `AuthenticationCredential`、`IAuthenticationHandler` | 宿主把协议凭据（如 `Authorization: Bearer …`）放进上下文；按方案匹配的处理器把它变成 `Principal` |
| `AuthenticationMiddleware` | Authenticate 阶段：无凭据则匿名，不支持的方案或无效凭据以结果拒绝，`RequireAuthentication` 时拒绝匿名 |
| `AuthorizationRequirement`、`PolicyCatalog`、`AuthenticatedPolicy`、`PermissionPolicy` | 调用要求的策略；按名查找，`permission:<code>` 动态映射为权限策略，未定义的权限码按策略不存在拒绝 |
| `AuthorizationMiddleware` | Authorize 阶段：有要求才判定，策略不存在或拒绝以结果返回 |
| `IPermissionGrantStore`、`PermissionGrant`、`GrantSubject`、`IPermissionChecker`、`PermissionChecker` | RBAC：用户与角色（来自 `role` 声明）的授予之并集，`*` 表示全部；租户专属授予只对该租户生效 |
| `SecurityOptions` | `RequireAuthentication`、`RoleClaim` |

```csharp
context.RequireAuthorization(AuthorizationRequirement.Permission("orders.read"));   // 宿主或操作契约在 Authorize 阶段之前设置
```

认证与授权失败都以 `Result` 返回，宿主翻译为 401 / 403；处理器内部的基础设施失败抛异常。

## 依赖方向

依赖 `XiHan.Framework.Kernel`。被 `Security.InMemory`、`Security.Jwt`、`Hosting.AspNetCore` 引用。

## 状态

P2：可用。
