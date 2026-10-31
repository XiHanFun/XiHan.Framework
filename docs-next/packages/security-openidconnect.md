# XiHan.Framework.Security.OpenIdConnect

认证的 OpenID Connect Provider：从签发者的发现文档与 JWKS 取签名密钥，校验 `Bearer` 令牌（非对称签名、签发者、受众、有效期），密钥轮换时按需刷新，提供 `security.authentication@1`。适用于令牌由外部身份提供方签发的应用。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Security.OpenIdConnect` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `security.authentication@1`；依赖 `kernel.hosting@1` |
| 特性入口 | `OpenIdConnectSecurityFeature`（清单 `xihan.security.openidconnect`，`role: provider`） |
| 配置节 | `XiHan:Security:OpenIdConnect` |
| 依赖 | `XiHan.Framework.Security.Abstractions`、`Microsoft.IdentityModel.JsonWebTokens` 8.23.0；发现文档与 JWKS 用 `HttpClient` 直接取，不依赖 `Microsoft.IdentityModel.Protocols.*` |
| AOT | 不兼容（`IsAotCompatible` 为 `false`：Microsoft.IdentityModel 未声明 AOT 兼容） |
| 稳定性 | Preview |

## 概述

`OpenIdConnectSecurityFeature` 注册 `OpenIdConnectOptions`、元数据缓存 `OpenIdConnectMetadataCache`、`Bearer` 方案的 `OpenIdConnectAuthenticationHandler` 与 Authenticate 阶段的 `AuthenticationMiddleware`。签名密钥来自签发者 JWKS，支持 RS256、ES256 等非对称算法。

元数据按需取得：首次认证时取发现文档的 `issuer` 与 `jwks_uri`，再取 JWKS 的签名密钥并缓存，之后每隔 `RefreshInterval` 自动刷新；并发认证只发一次请求，其余等待同一结果。令牌的 `kid` 不在当前 JWKS 里时（签发者轮换了密钥）按需刷新一次再校验，两次按需刷新至少间隔 `MinimumRefreshInterval`，伪造的 `kid` 不会让签发者被反复请求。

本包只提供认证，装配后顶替 `Security.InMemory` 的认证部分，授权仍由被选中的授权 Provider 承担。本包与 `Security.Jwt` 同一时刻只装配其一。

## 何时使用

- 令牌由 Keycloak、Entra ID 等支持 OpenID Connect 发现的身份提供方签发：用本包。
- 应用自己签发 HS256 令牌：用 [Security.Jwt](./security-jwt)。
- 发布为 Native AOT 的应用不能用本包。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Security.OpenIdConnect" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Security.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

// Security.InMemory 让出认证，只保留授权
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<OpenIdConnectSecurityFeature>());

var app = builder.Build();
app.UseXiHan();
app.Run();
```

## 配置

| 键（`XiHan:Security:OpenIdConnect`） | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `Authority` | `string` | 必填 | 签发者地址，绝对 http / https URI |
| `MetadataAddress` | `string` | `<Authority>/.well-known/openid-configuration` | 发现文档地址 |
| `Issuer` | `string` | 发现文档的 `issuer` | 期望的签发者；签发者始终校验 |
| `Audience` | `string` | 不校验 | 配置了才校验受众 |
| `RequireHttpsMetadata` | `bool` | `true` | 发现文档与 JWKS 必须经 HTTPS 取得；只在本地开发时关闭 |
| `ClockSkewSeconds` | `double` | 60 | 有效期校验允许的时钟偏移 |
| `RefreshIntervalSeconds` | `double` | 43200（12 小时） | 元数据自动刷新间隔，必须大于 0 |
| `MinimumRefreshIntervalSeconds` | `double` | 300（5 分钟） | 两次按需刷新的最短间隔，也是刷新失败后沿用旧密钥的时长，必须大于 0 |
| `BackchannelTimeoutSeconds` | `double` | 30 | 取发现文档与 JWKS 的 HTTP 超时，必须大于 0 |
| `NameClaim` | `string` | `sub` | 作为主体 ID 的声明类型 |

```json
{
  "XiHan": {
    "Security": {
      "OpenIdConnect": {
        "Authority": "https://login.example.com/realms/shop",
        "Audience": "orders-api"
      }
    }
  }
}
```

配置在装配时读取：缺少 `Authority`、时长不是非负数在装配时抛出 `ArgumentException`；`RequireHttpsMetadata` 为真而发现文档地址不是 HTTPS 时抛出 `InvalidOperationException`，不等到第一次认证。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `OpenIdConnectSecurityFeature` | 特性入口 |
| `OpenIdConnectOptions` | 上表各项（时长为 `TimeSpan`）；`DiscoveryPath`；`ResolveMetadataAddress()` 给出发现文档的最终地址 |
| `OpenIdConnectMetadataCache` | 元数据缓存：`GetAsync`、`RefreshAsync`（受最短间隔限制）、`Current`；`FailureRetryDelay` 为 5 秒 |
| `OpenIdConnectMetadata` | 一份元数据：`Issuer`、`JwksUri`、`SigningKeys`、`RetrievedAt` |
| `OpenIdConnectAuthenticationHandler` | `Bearer` 方案的处理器；`SchemeName` = `Bearer` |

## 使用示例

元数据缓存以 `TryAddSingleton` 注册。需要自带 HTTP 客户端（经代理出网、测试替身）时，在 `AddXiHan` 之前登记自己的实例：

```csharp
using System.Net;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Security.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

var options = OpenIdConnectOptions.FromConfiguration(builder.Configuration);
var http = new HttpClient(new SocketsHttpHandler { Proxy = new WebProxy("http://proxy.local:8080") }) { Timeout = options.BackchannelTimeout };

// 先登记：特性里的 TryAddSingleton 看到已有实例就让位
builder.Services.AddSingleton(sp => new OpenIdConnectMetadataCache(options, http, TimeProvider.System, sp.GetRequiredService<ILoggerFactory>()));
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<OpenIdConnectSecurityFeature>());

var app = builder.Build();
app.UseXiHan();
app.Run();
```

令牌的声明按原始类型名进入主体：角色声明的类型要与 `XiHan:Security:RoleClaim` 一致，租户声明的类型要与 `XiHan:MultiTenancy:TenantClaim` 一致；签发者把角色放在其他类型的声明里时，调整 `RoleClaim` 或在签发者侧加声明映射。

## 行为与限制

- 签名、过期与有效期始终强制；签发者取 `Issuer` 或发现文档的 `issuer` 并始终校验；受众只在配置了 `Audience` 时校验。主体 ID 取 `NameClaim`，显示名取 `name` 声明。
- 校验失败返回 `SECURITY_DENIED_003`，不区分原因对外暴露；读不出的令牌直接拒绝，不为它访问签发者；令牌没有 `NameClaim` 声明同样是 `SECURITY_DENIED_003`。
- 刷新失败时沿用最后一份密钥并记日志，`MinimumRefreshInterval` 后再试；从未取到元数据时返回 `SECURITY_UNAVAILABLE_003`（HTTP 503），5 秒退避期内不再请求签发者。
- 发现文档缺 `issuer` 或 `jwks_uri`、JWKS 没有签名密钥、`RequireHttpsMetadata` 为真而 `jwks_uri` 不是 HTTPS，都按取数失败处理。
- 缺省 HTTP 客户端的超时取 `BackchannelTimeout`，连接池中的连接每 5 分钟轮换一次。
- 经 ASP.NET Core 宿主时，401 响应带 `WWW-Authenticate` 质询（ADR-0042）；绕过管道的端点经方案 `XiHan` 复用本处理器（ADR-0051）。

## 相关

- [认证](../guide/authentication)
- [Security.Abstractions](./security-abstractions)
- [Security.Jwt](./security-jwt)
- [Security.InMemory](./security-inmemory)
- 规范：RFC-0008《认证与授权》；ADR-0042、ADR-0051
