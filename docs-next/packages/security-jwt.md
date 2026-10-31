# XiHan.Framework.Security.Jwt

认证的 JWT Provider：校验 HS256 对称密钥签名的 `Bearer` 令牌（签名、有效期、签发者、受众），把令牌映射为主体，提供 `security.authentication@1`。适用于令牌由应用自己的登录服务签发的场景；外部身份提供方签发的非对称令牌用 [Security.OpenIdConnect](./security-openidconnect)。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Security.Jwt` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `security.authentication@1`；依赖 `kernel.hosting@1` |
| 特性入口 | `JwtSecurityFeature`（清单 `xihan.security.jwt`，`role: provider`） |
| 配置节 | `XiHan:Security:Jwt` |
| 依赖 | `XiHan.Framework.Security.Abstractions`、`Microsoft.IdentityModel.JsonWebTokens` 8.23.0 |
| AOT | 不兼容（`IsAotCompatible` 为 `false`：Microsoft.IdentityModel 未声明 AOT 兼容） |
| 稳定性 | Preview |

## 概述

`JwtSecurityFeature` 注册 `JwtOptions`、`Bearer` 方案的 `JwtAuthenticationHandler` 与 Authenticate 阶段的 `AuthenticationMiddleware`。处理器用 `JsonWebTokenHandler` 校验令牌，关闭入站声明映射（`MapInboundClaims = false`），声明按 JWT 原始类型名（`sub`、`role`、`tenant`）进入主体，与 ApiKey 处理器的声明形态一致。

本包只提供认证。装配后顶替 `Security.InMemory` 的认证部分，授权仍由被选中的授权 Provider 承担（缺省 `Security.InMemory`，或 `Security.Database`）。本包与 `Security.OpenIdConnect` 都提供 `security.authentication@1`，同一时刻只装配其一。

本包只校验令牌，不签发令牌；签发由应用的登录服务完成，签名密钥、签发者与受众与这里的配置保持一致。

## 何时使用

- 应用自己签发令牌、用共享的对称密钥签名：用本包。
- 令牌来自 Keycloak、Entra ID 这类外部身份提供方，签名密钥要从 JWKS 取：用 [Security.OpenIdConnect](./security-openidconnect)。
- 发布为 Native AOT 的应用不能用本包。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Security.Jwt" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Security.Jwt;

var builder = WebApplication.CreateBuilder(args);

// Security.InMemory 让出认证，只保留授权
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<JwtSecurityFeature>());

var app = builder.Build();
app.UseXiHan();
app.Run();
```

## 配置

| 键（`XiHan:Security:Jwt`） | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `Secret` | `string` | 必填 | 对称密钥，UTF-8 编码后至少 32 字节（`JwtOptions.MinSecretBytes`）；缺失或过短在装配时抛出 `ArgumentException` |
| `Issuer` | `string` | 不校验 | 配置了才校验签发者 |
| `Audience` | `string` | 不校验 | 配置了才校验受众 |
| `ClockSkewSeconds` | `double` | 60 | 有效期校验允许的时钟偏移，不能为负 |
| `NameClaim` | `string` | `sub` | 作为主体 ID 的声明类型 |

```json
{
  "XiHan": {
    "Security": {
      "Jwt": {
        "Secret": "dev-only-signing-key-change-me-0123456789",
        "Issuer": "https://localhost:5001",
        "Audience": "orders-api",
        "ClockSkewSeconds": 60,
        "NameClaim": "sub"
      }
    }
  }
}
```

密钥不要写进提交到仓库的配置文件，生产环境从环境变量或密钥库注入同名配置键。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `JwtSecurityFeature` | 特性入口 |
| `JwtOptions` | `Secret`、`Issuer`、`Audience`、`ClockSkew`、`NameClaim`；`FromConfiguration` 读取上表 |
| `JwtAuthenticationHandler` | `Bearer` 方案的处理器；`SchemeName` = `Bearer` |

## 使用示例

调用方带上令牌：

```bash
curl -H "Authorization: Bearer <token>" http://localhost:5000/whoami
```

令牌至少要有 `sub`（或 `NameClaim` 配置的类型）与 `exp`。要参与 RBAC 判定的角色写成类型等于 `XiHan:Security:RoleClaim`（缺省 `role`）的声明；要让 Tenant 阶段识别所属租户，写一条类型等于 `XiHan:MultiTenancy:TenantClaim`（缺省 `tenant`）的声明。显示名取 `name` 声明。

测试里不经宿主直接校验，调用上下文取自测试基座 `XiHan.Framework.Testing`：

```csharp
using XiHan.Framework.Security.Abstractions.Authentication;
using XiHan.Framework.Security.Jwt;
using XiHan.Framework.Testing;

public static class TokenCheck
{
    public static async Task<string?> SubjectOfAsync(string token, CancellationToken cancellationToken)
    {
        var handler = new JwtAuthenticationHandler(new JwtOptions { Secret = "dev-only-signing-key-change-me-0123456789", Audience = "orders-api" });
        var result = await handler.AuthenticateAsync(new AuthenticationCredential(JwtAuthenticationHandler.SchemeName, token), TestInvocationContext.Create(), cancellationToken);
        return result.IsSuccess ? result.ValueOrThrow().Id : null;
    }
}
```

## 行为与限制

- 签名、`exp` 与有效期始终强制校验；签发者与受众只在配置了才校验。
- 任何校验失败都返回 `SECURITY_DENIED_003`（消息键 `security.token.invalid`），不区分原因对外暴露；令牌没有 `NameClaim` 声明同样返回 `SECURITY_DENIED_003`（消息键 `security.token.subject_missing`）。
- 经 ASP.NET Core 宿主时，无效令牌是 401 并带 `WWW-Authenticate: Bearer error="invalid_token"`，没有令牌时是 401 并带 `WWW-Authenticate: Bearer`（ADR-0042）。
- 容器里有 `Bearer` 方案的处理器时，`Protocols.Http` 发布的 OpenAPI 文档带 `components.securitySchemes.bearer` 与全局 `security`。
- 绕过管道的端点（SignalR Hub 等）经 ASP.NET Core 方案 `XiHan` 复用本处理器，见[认证 · 绕过管道的端点](../guide/authentication#绕过管道的端点)。

## 相关

- [认证](../guide/authentication)
- [Security.Abstractions](./security-abstractions)
- [Security.OpenIdConnect](./security-openidconnect)
- [Security.InMemory](./security-inmemory)
- [Hosting.AspNetCore](./hosting-aspnetcore)
- 规范：RFC-0008《认证与授权》；ADR-0042、ADR-0051
