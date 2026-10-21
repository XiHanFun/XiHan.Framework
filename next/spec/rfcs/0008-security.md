# RFC-0008 认证与授权

> 状态：已采纳 | 日期：2026-10-21 | 对应 ADR：— | 参考实现：`XiHan.Framework.Security.Abstractions`、`XiHan.Framework.Security.InMemory`、`XiHan.Framework.Security.Jwt` | 一致性：`XiHan.Framework.Security.Conformance`

## 摘要

认证把协议凭据变成主体，授权按策略判定；两者各自是一种能力，可独立换 Provider。内核只定义原语（主体、租户、授权请求、策略接口、权限登记），本 RFC 定义凭据、处理器、授权要求、策略目录与 RBAC 授予。

## 能力

| 能力 | 含义 | 默认实现 |
| --- | --- | --- |
| `security.authentication@1` | 凭据 → 主体 | `Security.InMemory`（`ApiKey` 方案，配置装载） |
| `security.authorization@1` | 策略判定与权限授予 | `Security.InMemory`（RBAC 授予存储） |

`Security.Jwt`（`Bearer`，HS256）是显式的认证 Provider；OpenID Connect 顺延到 P3 随宿主适配器实现。

## 认证

1. 宿主在 Ingress 阶段把协议凭据（HTTP `Authorization: <scheme> <value>`）放入上下文，自己不解释它。
2. Authenticate 阶段：没有凭据保持匿名；有凭据交给方案匹配（不区分大小写）的 `IAuthenticationHandler`。不支持的方案返回 `SECURITY_VALIDATION_001`；凭据无效返回 `SECURITY_DENIED_003`，不区分原因对外暴露；成功写入 `SecurityContext.Principal`，保留已有租户。
3. `XiHan:Security:RequireAuthentication` 为真时匿名调用在此被拒绝（`SECURITY_DENIED_001`）；默认由策略决定。
4. 声明按原始类型名保留，不映射为 .NET 长类型；角色声明类型由 `XiHan:Security:RoleClaim`（默认 `role`）决定，租户声明类型由多租户选项决定。

## 授权

1. 调用的授权要求 `AuthorizationRequirement(policy, resource?)` 由宿主端点元数据或操作契约（P3）在 Authorize 阶段之前写入上下文；没有要求则放行。
2. 策略按名在 `PolicyCatalog` 查找：注册的 `IPolicy`；`permission:<code>` 在权限码已登记于内核 `PermissionRegistry` 时动态映射为权限策略，未登记的权限码按策略不存在处理。
3. 策略不存在返回 `SECURITY_NOTFOUND_001`（宁可拒绝，不可放行）；拒绝返回策略给出的原因，未给出时为 `SECURITY_DENIED_002`。
4. 内置策略：`authenticated`（已认证）；`permission:<code>`（匿名先按 `SECURITY_DENIED_001` 拒绝，缺权限返回 `SECURITY_DENIED_004`）。

## RBAC 授予

- 主体是用户（`user:<id>`）或角色（`role:<name>`）；授予是（主体，权限码，租户?）。租户为空的授予对任何租户生效，租户专属授予只对该租户生效。
- 检查器取用户本身的授予与其角色授予之并集；`*` 匹配任何权限。
- `IPermissionGrantStore` 由 Provider 实现；内存实现从 `XiHan:Security:InMemory:Grants` 装载全局授予，有界。

## 一致性

授权 Provider 必须让其 `IPermissionGrantStore` 通过 `PermissionGrantStoreConformance`。

## 错误码

见 `conventions/error-codes.md` 中 `SECURITY_*`。
