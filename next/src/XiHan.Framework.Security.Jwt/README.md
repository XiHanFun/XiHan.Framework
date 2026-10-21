# XiHan.Framework.Security.Jwt

`security.authentication@1` 的 JWT Provider（Ring 3，`role: provider`）。注册后顶替内存实现的认证部分；授权仍由被选中的授权 Provider（默认 `Security.InMemory`）承担。

```csharp
builder.AddXiHan(x => x.UseFeature<InMemorySecurityFeature>().UseFeature<JwtSecurityFeature>());
```

```json
{ "XiHan": { "Security": { "Jwt": { "Secret": "至少 32 字节的对称密钥", "Issuer": "https://issuer", "Audience": "api", "ClockSkewSeconds": 60 } } } }
```

## 语义

- 凭据方案 `Bearer`；HS256 对称密钥（`Secret`，至少 32 字节）。签发者与受众配置了才校验；必须有过期时间；时钟偏移默认 60 秒。
- 主体 ID 取 `NameClaim`（默认 `sub`）；全部声明按原始类型名保留（不做 .NET 长声明类型映射），角色声明类型由 `XiHan:Security:RoleClaim` 决定。
- 校验失败返回 `SECURITY_DENIED_003`，不区分原因对外暴露。
- OpenID Connect（发现文档与 JWKS）顺延到 P3 随宿主适配器实现。

## 依赖方向

依赖 `Security.Abstractions` 与 `Microsoft.IdentityModel.JsonWebTokens`。

## 状态

P2：可用。
