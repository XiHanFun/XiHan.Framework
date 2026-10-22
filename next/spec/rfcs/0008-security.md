# RFC-0008 认证与授权

> 状态：已采纳 | 日期：2026-10-21 | 对应 ADR：— | 参考实现：`XiHan.Framework.Security.Abstractions`、`XiHan.Framework.Security.InMemory`、`XiHan.Framework.Security.Jwt`、`XiHan.Framework.Security.OpenIdConnect` | 一致性：`XiHan.Framework.Security.Conformance`

## 摘要

认证把协议凭据变成主体，授权按策略判定；两者各自是一种能力，可独立换 Provider。内核只定义原语（主体、租户、授权请求、策略接口、权限登记），本 RFC 定义凭据、处理器、授权要求、策略目录与 RBAC 授予。

## 能力

| 能力 | 含义 | 默认实现 |
| --- | --- | --- |
| `security.authentication@1` | 凭据 → 主体 | `Security.InMemory`（`ApiKey` 方案，配置装载） |
| `security.authorization@1` | 策略判定与权限授予 | `Security.InMemory`（RBAC 授予存储） |

`Security.Jwt`（`Bearer`，HS256 对称密钥）与 `Security.OpenIdConnect`（`Bearer`，签发者 JWKS 的非对称密钥）是显式的认证 Provider，同一时刻只装配其一。

## 认证

1. 宿主在 Ingress 阶段把协议凭据（HTTP `Authorization: <scheme> <value>`）放入上下文，自己不解释它。
2. Authenticate 阶段：没有凭据保持匿名；有凭据交给方案匹配（不区分大小写）的 `IAuthenticationHandler`。不支持的方案返回 `SECURITY_VALIDATION_001`；凭据无效返回 `SECURITY_DENIED_003`，不区分原因对外暴露；成功写入 `SecurityContext.Principal`，保留已有租户。
3. `XiHan:Security:RequireAuthentication` 为真时匿名调用在此被拒绝（`SECURITY_DENIED_001`）；默认由策略决定。
4. 声明按原始类型名保留，不映射为 .NET 长类型；角色声明类型由 `XiHan:Security:RoleClaim`（默认 `role`）决定，租户声明类型由多租户选项决定。

## OpenID Connect

1. 配置 `XiHan:Security:OpenIdConnect:Authority`（必填，绝对 URI）。发现文档默认在 `<Authority>/.well-known/openid-configuration`，可用 `MetadataAddress` 覆盖；`RequireHttpsMetadata`（默认真）要求发现文档与 JWKS 都经 HTTPS，只在本地开发时关闭。
2. 元数据按需取得：首次认证时取发现文档的 `issuer` 与 `jwks_uri`，再取 JWKS 的签名密钥；`RefreshInterval`（默认 12 小时）后自动刷新。并发认证只发一次请求，其余等待同一结果。
3. 签名密钥未知（`kid` 不在 JWKS 里）时按需刷新一次再校验；两次按需刷新至少间隔 `MinimumRefreshInterval`（默认 5 分钟），伪造 `kid` 不能让签发者被打爆。
4. 刷新失败沿用最后一份密钥，`MinimumRefreshInterval` 后再试；从未取到元数据时返回 `SECURITY_UNAVAILABLE_003`，5 秒退避后再试。读不出的令牌不访问签发者，直接返回 `SECURITY_DENIED_003`。
5. 校验：签名、过期与有效期强制；签发者取 `Issuer` 覆盖或发现文档的 `issuer`；受众只在配置了 `Audience` 才校验；主体与声明的映射与 JWT Provider 相同。

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
