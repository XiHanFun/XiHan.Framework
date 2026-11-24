# 认证

这一页讲调用凭据怎样变成主体：凭据与认证处理器、Authenticate 阶段的处理顺序、怎样在代码里读主体、三种认证 Provider（ApiKey、JWT、OpenID Connect）怎样选和配，以及认证失败时协议上怎样质询。读完你能让 SimpleApp 识别出调用方是谁，把开发用的 ApiKey 换成生产用的 JWT 或外部身份提供方的令牌，并看懂 401 响应里的内容。阅读前先了解[执行管道](/startup/pipeline)与[调用上下文](/startup/invocation-context)。

## 为什么需要

一个服务要知道「是谁在调用」，才谈得上「能不能调用」。如果让每个协议宿主各自解析令牌，HTTP、gRPC、MCP、命令行就有四套校验逻辑，换一种认证方式要改四处；如果让业务代码自己校验，每个处理器都得重复一遍。框架把认证放在内核管道的 Authenticate 阶段：宿主只负责把协议里的凭据原样放进调用上下文，`ICredentialHandler` 把凭据变成主体 `Principal`，之后的阶段只读安全上下文。认证是能力 `security.authentication@1`，换认证方式就是换 Provider，宿主与业务代码不动。

## 凭据、处理器与主体

认证分三步，分属三个角色：

1. 宿主在 Ingress 阶段把协议里的凭据原样放进调用上下文（`SetCredential`），自己不解释它。
2. Authenticate 阶段的 `AuthenticationMiddleware` 按方案名找到 `ICredentialHandler`，由它把凭据变成主体 `Principal`。
3. 主体写进安全上下文 `SecurityContext.Principal`，之后的阶段只读。

认证在内核管道里完成，不依赖 ASP.NET Core 的认证中间件；同一个处理器服务 HTTP、gRPC、MCP 与命令行。ASP.NET Core 的认证只在绕过管道的端点上用到，见下文。

凭据是 `AuthenticationCredential`：方案（`Scheme`，如 `Bearer`、`ApiKey`，比较时不区分大小写）与值（`Value`）。`AuthenticationCredential.TryParse` 解析 `<scheme> <value>` 形式，方案或值为空都不算凭据；`ToString()` 把值显示为 `***`，不会进日志。

| 宿主 | 凭据从哪来 |
| --- | --- |
| ASP.NET Core（HTTP、gRPC、MCP） | `Authorization` 头；没有时，请求路径落在登记的查询串令牌路径下才取查询串 `access_token`，作为 `Bearer` 凭据 |
| 命令行 | `--authorization '<scheme> <value>'`，或环境变量 `XIHAN_AUTHORIZATION` |

`Authorization` 头格式不对（如只有 `Bearer`）时视为没有凭据，调用按匿名继续。

`Principal` 不可变：

| 成员 | 说明 |
| --- | --- |
| `Id` | 主体 ID；匿名主体为 `null` |
| `AuthenticationScheme` | 认证方案；匿名主体为 `null` |
| `IsAuthenticated` | `Id` 与方案都有值；为真时编译器知道 `Id` 不为 `null`，`if (principal.IsAuthenticated)` 之后直接用 `principal.Id` |
| `DisplayName` | 显示名 |
| `Claims` | 声明列表（`KeyValuePair<string, string>`），同一类型可以有多个值 |
| `ClaimValues(type)` | 取某类型的全部声明值，类型按序数比较 |
| `HasClaim(type, value)` | 是否有这条声明 |

声明按原始类型名保留（`sub`、`role`、`tenant`），不映射成 .NET 的长类型 URI。匿名主体是 `Principal.Anonymous`，匿名无租户的安全上下文是 `SecurityContext.Anonymous`。

| 包 | 方案 | 清单角色 | 适用 |
| --- | --- | --- | --- |
| `Security.InMemory` | `ApiKey` | `default` | 开发、原型、测试；`Profiles.Standard` 已包含 |
| `Security.Jwt` | `Bearer` | `provider` | 应用自己签发的 HS256 令牌 |
| `Security.OpenIdConnect` | `Bearer` | `provider` | 外部身份提供方签发的令牌（RS256、ES256 等），签名密钥取自 JWKS |

## 最小示例

`Security.InMemory` 把主体与 API Key 写在配置里，凭据是 `Authorization: ApiKey <key>`。下面的主体来自 `framework/samples/SimpleApp`：

```json
{
  "XiHan": {
    "Security": {
      "InMemory": {
        "Principals": {
          "alice": { "ApiKey": "dev-alice-key", "DisplayName": "Alice", "Roles": "editor", "Tenant": "acme" },
          "bob": { "ApiKey": "dev-bob-key", "DisplayName": "Bob", "Roles": "viewer", "Tenant": "acme" },
          "relay": { "ApiKey": "dev-relay-key", "DisplayName": "Event Relay", "Roles": "event-relay" }
        }
      }
    }
  }
}
```

节名是主体 ID；`ApiKey` 必填且至少 8 个字符；`Roles` 逗号分隔；`Claims:*` 下的键值作为附加声明。认证成功的主体按顺序带上：每个角色一条 `RoleClaim` 类型的声明、一条租户声明（有 `Tenant` 时，类型取 `XiHan:MultiTenancy:TenantClaim`，缺省 `tenant`）、附加声明。

Standard 组合包已含 `Security.InMemory`（ApiKey 认证与内存授予），不用再装配特性。接在[第一个应用](/getting-started/first-app)换成 Standard 组合后的 `Program.cs` 上，在 `app.UseXiHan()` 之后多一个端点：

```csharp
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel.Security;

// 经 UseXiHan() 的端点在 Handle 阶段内执行，主体已由 Authenticate 阶段写进安全上下文
app.MapGet("/whoami", (HttpContext http) =>
{
    var security = http.FindXiHanContext().ValueOrThrow().GetSecurity();
    return security.Principal.IsAuthenticated
        ? $"{security.Principal.Id} ({security.Principal.AuthenticationScheme}) @ {security.Tenant?.Id ?? "-"}"
        : "anonymous";
});
```

```bash
curl -H "Authorization: ApiKey dev-alice-key" http://localhost:5000/whoami   # alice (ApiKey) @ acme
curl http://localhost:5000/whoami                                            # anonymous
curl -i -H "Authorization: ApiKey wrong-key" http://localhost:5000/whoami    # 401，WWW-Authenticate: ApiKey
```

操作契约的处理器从传进来的调用上下文读同样的东西：`context.GetSecurity().Principal`。

`ApiKeyCredentialHandler` 逐条做常量时间比较，不用字典查找，耗时不随匹配位置泄露信息；找不到返回 `SECURITY_DENIED_003`。主体数上限 `XiHan:Security:InMemory:MaxPrincipals`，缺省 1000。

::: warning 内存安全只用于开发与测试
API Key 明文写在配置里，主体与授予都在进程内存中，重启即恢复为配置内容。生产环境换成 JWT 或 OpenID Connect 认证，授予换成 `Security.Database`，见 [RBAC 授予与条件](/security/grants)。
:::

## Authenticate 阶段

`AuthenticationMiddleware` 的处理顺序：

1. 调用上下文里没有凭据：保持匿名，进入第 5 步。
2. 在容器里的 `ICredentialHandler` 中按方案名（不区分大小写）找第一个匹配者；找不到返回 `SECURITY_VALIDATION_001`。
3. 处理器返回 `Result<Principal>`。凭据无效、过期、签名不符一律返回 `SECURITY_DENIED_003`，不区分原因对外暴露；处理器内部的基础设施故障以异常抛出，由内核转成失败结果。
4. 认证成功：写入新的安全上下文，保留已有的租户。
5. `RequireAuthentication` 为真且主体仍是匿名：返回 `SECURITY_DENIED_001`。

`RequireAuthentication` 缺省为假：匿名调用放行，由各操作的授权要求决定是否需要登录；既没写授权要求、又没打开它的操作对匿名开放，见[授权与权限](/security/authorization)。它与承载角色的声明类型 `RoleClaim`（缺省 `role`）都在配置节 `XiHan:Security`，见 [Security.Abstractions](/packages/security-abstractions#配置)。

## 选择认证 Provider

`Security.InMemory` 同时提供认证与授权。装配 `Security.Jwt` 或 `Security.OpenIdConnect` 后，它让出认证、只保留授权（RBAC 授予存储）。`Security.Jwt` 与 `Security.OpenIdConnect` 都提供 `security.authentication@1`，同一时刻只装配其一。

JWT 与 OpenID Connect Provider 只校验令牌，不签发令牌；令牌由应用的登录服务或外部身份提供方签发。

### JWT

`Security.Jwt` 校验应用自己签发、HS256 对称密钥签名的 `Bearer` 令牌。同一份 `Program.cs`，`AddXiHan` 多一个特性：

```csharp
using XiHan.Framework.Security.Jwt;

// 显式 Provider 顶替内存实现的认证部分；授权仍由 Security.InMemory 承担
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<JwtSecurityFeature>());
```

```json
{
  "XiHan": {
    "Security": {
      "Jwt": {
        "Secret": "dev-only-signing-key-change-me-0123456789",
        "Issuer": "https://localhost:5001",
        "Audience": "orders-api",
        "ClockSkewSeconds": 60
      }
    }
  }
}
```

`Secret` 必填，UTF-8 编码后至少 32 字节；`Issuer` 与 `Audience` 配置了才校验；`ClockSkewSeconds` 是校验有效期时允许的时钟偏移。各键的类型与缺省值见 [Security.Jwt](/packages/security-jwt#配置)。

签名、`exp` 与有效期始终强制校验。主体 ID 取 `NameClaim`（缺省 `sub`）声明，令牌里没有这条声明同样返回 `SECURITY_DENIED_003`（消息键 `security.token.subject_missing`）；显示名取 `name` 声明；令牌的全部声明按原始类型名进入主体，所以角色声明的类型要与 `XiHan:Security:RoleClaim` 一致，租户声明的类型要与 `XiHan:MultiTenancy:TenantClaim` 一致。密钥放在用户机密、环境变量或远程配置里，不进仓库，见[远程配置与密钥](/configuration/remote)。

### OpenID Connect

`Security.OpenIdConnect` 校验外部身份提供方签发的 `Bearer` 令牌，签名密钥从签发者的发现文档与 JWKS 取得。装配方式与 JWT 相同，换成 `UseFeature<OpenIdConnectSecurityFeature>()`：

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

`Authority`（签发者地址）必填，`Audience`（期望的受众）在缺省校验受众时也必填，没有即启动失败；签发者始终校验。发现文档缺省必须经 HTTPS 取得，本地开发时用 `RequireHttpsMetadata: false` 关闭。发现文档地址、刷新间隔、超时等其余键见 [Security.OpenIdConnect](/packages/security-openidconnect#配置)。

元数据按需取得并缓存：首次认证时取发现文档与 JWKS 的签名密钥；令牌的 `kid` 不在 JWKS 里时（签发者轮换了密钥）按需刷新一次再校验，刷新有最短间隔，伪造的 `kid` 不会让签发者被反复请求。刷新失败时沿用最后一份密钥；从未取到元数据时返回 `SECURITY_UNAVAILABLE_003`（HTTP 503）。读不出的令牌直接返回 `SECURITY_DENIED_003`，不为它访问签发者。主体与声明的映射与 JWT Provider 相同。

## 凭据无效与 401 质询

认证失败与无权限在协议上分开：`SECURITY_DENIED_001`（未认证）与 `SECURITY_DENIED_003`（凭据无效）是认证失败，客户端据此刷新凭据或重新登录；其余 `DENIED` 是已认证但无权限。

| 错误码 | HTTP | `WWW-Authenticate` | gRPC |
| --- | --- | --- | --- |
| `SECURITY_DENIED_001` 未认证 | 401 | 按已装配的认证处理器逐个质询方案名（如 `Bearer`、`ApiKey`），去重，不带错误参数 | `Unauthenticated` |
| `SECURITY_DENIED_003` 凭据无效 | 401 | 只质询请求出示的方案：Bearer 为 `Bearer error="invalid_token"`，其他方案只写方案名 | `Unauthenticated` |
| `SECURITY_DENIED_002`、`SECURITY_DENIED_004` | 403 | 无 | `PermissionDenied` |
| `SECURITY_VALIDATION_001` 方案不支持 | 400 | 无 | `InvalidArgument` |

质询头在 `UseXiHan()` 写错误响应时加上（MCP 端点同样经过它），不带 `error_description`，与「不区分原因对外暴露」一致；具体原因只进日志。gRPC 的尾部元数据仍带 `xihan-error-code`。命令行宿主按错误码种类给退出码，`DENIED` 为 4。

## 绕过管道的端点

SignalR Hub、Server-Sent Events 这类长连接端点不经内核管道，Authenticate 阶段管不到它们。`Hosting.AspNetCore` 提供 ASP.NET Core 认证方案 `XiHan`，把这类端点的凭据交给内核已装配的同一个 `ICredentialHandler`，JWT、OpenID Connect、ApiKey 都适用，校验项只有一份：

- 只认带 `PipelineBypassMetadata` 的端点；普通请求仍由管道认证，不会被认证两次。
- 凭据按宿主的同一规则取：先 `Authorization` 头，没有时只在 `QueryTokenPaths` 登记的路径前缀下接受查询串 `access_token`。
- 认证成功后按登记顺序调用应用的 `IInvocationAuthenticationFilter`，任一返回失败即认证失败；会话门禁在这里由应用实现。
- 主体转成 `ClaimsPrincipal`，声明按原始类型名保留，另加一条 `ClaimTypes.NameIdentifier` 承载主体 ID。

装配写法、查询串令牌路径、会话过滤器与在 Hub 里经 `HttpInvocation.CreateContext` 发起内核调用的完整示例见[端点里使用框架](/web/endpoints)与[实时通信](/web/realtime)。

## 常见问题

### 怎样签发 JWT

框架只校验，不签发。登录服务用 .NET 的 `System.IdentityModel.Tokens.Jwt` 或 `Microsoft.IdentityModel.JsonWebTokens` 以同一个 `Secret` 做 HS256 签名，写入 `sub`（或 `NameClaim` 配置的类型）、`exp`，角色写成 `RoleClaim` 类型的声明，租户写成 `TenantClaim` 类型的声明，`Issuer` / `Audience` 与配置一致。更常见的做法是接入外部身份提供方，用 `Security.OpenIdConnect` 校验。

### 为什么令牌有效却被 401

依次检查：方案是不是 `Bearer`（`Authorization: Bearer <token>`）；`Secret` 是否一致且至少 32 字节；令牌里有没有 `NameClaim`（缺省 `sub`）声明；`Issuer` / `Audience` 配了就会校验；时钟偏移是否超过 `ClockSkewSeconds`。对外一律是 `SECURITY_DENIED_003`，具体原因只在日志里。

### 怎样用 Cookie 或 Session 认证

框架没有 Cookie 认证 Provider：内核管道服务 HTTP、gRPC、MCP、命令行四种宿主，凭据模型是 `<scheme> <value>` 的 `Authorization` 头。浏览器端应用用令牌（BFF 模式下由后端把 Cookie 换成令牌再调用），或者自己实现一个 `ICredentialHandler` 并在宿主的 Ingress 把 Cookie 翻译成凭据放进调用上下文。

### 能同时接受 ApiKey 和 Bearer 吗

`Security.InMemory` 装配后又装配 `Security.Jwt` 时，内存实现让出认证能力，只剩 Bearer。要同时支持两种方案，自己写一个提供 `security.authentication@1` 的特性，登记两个 `ICredentialHandler`（方案分别为 `ApiKey` 与 `Bearer`），中间件按方案名匹配。

## 相关包与下一步

- 包参考：[Security.Abstractions](/packages/security-abstractions)、[Security.InMemory](/packages/security-inmemory)、[Security.Jwt](/packages/security-jwt)、[Security.OpenIdConnect](/packages/security-openidconnect)、[Security.WebAuthn](/packages/security-webauthn)、[Hosting.AspNetCore](/packages/hosting-aspnetcore)
- 错误码：[Security.Abstractions](/packages/security-abstractions#错误码)
- 规范：RFC-0008《认证与授权》
- 相关章节：[通行密钥](/security/webauthn)、[租户解析与隔离](/multi-tenancy/resolution)、[端点里使用框架](/web/endpoints)、[命令行宿主](/protocols/command-line)
- 下一步：[授权与权限](/security/authorization)
