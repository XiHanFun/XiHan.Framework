# XiHan.Framework.Security.OpenIdConnect

`security.authentication@1` 的 OpenID Connect Provider（Ring 3，`role: provider`）。注册后顶替内存实现的认证部分；授权仍由被选中的授权 Provider（默认 `Security.InMemory`）承担。与 `Security.Jwt` 同一时刻只装配其一。

```csharp
builder.AddXiHan(x => x.UseFeature<InMemorySecurityFeature>().UseFeature<OpenIdConnectSecurityFeature>());
```

```json
{ "XiHan": { "Security": { "OpenIdConnect": { "Authority": "https://login.example.com/realms/shop", "Audience": "api" } } } }
```

| 键 | 默认 | 说明 |
| --- | --- | --- |
| `Authority` | 必填 | 签发者地址（绝对 URI）；发现文档默认在 `<Authority>/.well-known/openid-configuration` |
| `MetadataAddress` | 由 `Authority` 推出 | 发现文档地址 |
| `Issuer` | 发现文档的 `issuer` | 期望的签发者 |
| `Audience` | 不校验 | 期望的受众 |
| `RequireHttpsMetadata` | `true` | 发现文档与 JWKS 必须经 HTTPS；只在本地开发时关闭 |
| `ClockSkewSeconds` | `60` | 有效期校验允许的时钟偏移 |
| `RefreshIntervalSeconds` | `43200` | 元数据自动刷新间隔 |
| `MinimumRefreshIntervalSeconds` | `300` | 两次按需刷新的最短间隔，也是刷新失败后沿用旧密钥的时长 |
| `BackchannelTimeoutSeconds` | `30` | 取发现文档与 JWKS 的 HTTP 超时 |
| `NameClaim` | `sub` | 作为主体 ID 的声明类型 |

## 语义

- 凭据方案 `Bearer`；签名密钥来自签发者 JWKS（RS256、ES256 等非对称算法）。签名、过期与有效期始终强制；签发者取 `Issuer` 或发现文档；受众配置了才校验。
- 元数据首次认证时按需取得并缓存（`OpenIdConnectMetadataCache`），按 `RefreshInterval` 自动刷新；并发认证只发一次请求。
- 令牌的 `kid` 不在 JWKS 里时按需刷新一次再校验，两次按需刷新至少间隔 `MinimumRefreshInterval`；伪造 `kid` 不能让签发者被打爆。
- 刷新失败沿用最后一份密钥并记日志；从未取到元数据时返回 `SECURITY_UNAVAILABLE_003`（5 秒退避后再试）。读不出的令牌不访问签发者。
- 主体 ID 取 `NameClaim`；全部声明按原始类型名保留（不做 .NET 长声明类型映射），角色声明类型由 `XiHan:Security:RoleClaim` 决定。校验失败返回 `SECURITY_DENIED_003`，不区分原因对外暴露。
- 测试或自带 HTTP 客户端时，先注册 `OpenIdConnectMetadataCache` 单例，特性以 `TryAdd` 让位。

## 依赖方向

依赖 `Security.Abstractions` 与 `Microsoft.IdentityModel.JsonWebTokens`；发现文档与 JWKS 用 `HttpClient` 直接取，不依赖 `Microsoft.IdentityModel.Protocols.*`。

## 状态

P3：可用。
