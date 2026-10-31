# 认证

本章讲调用凭据怎样变成主体：凭据与认证处理器、Authenticate 阶段、ApiKey / JWT / OpenID Connect 三种认证 Provider、401 质询，以及绕过管道的端点怎样复用同一套认证，适用于需要识别调用方身份的应用。完整 API 见 [Security.Abstractions](../packages/security-abstractions) 与 [Security.InMemory](../packages/security-inmemory)、[Security.Jwt](../packages/security-jwt)、[Security.OpenIdConnect](../packages/security-openidconnect) 包页。

## 凭据、处理器与主体

认证分三步，分属三个角色。宿主在 Ingress 阶段把协议里的凭据原样放进调用上下文（`SetCredential`），自己不解释它；Authenticate 阶段的 `AuthenticationMiddleware` 按方案名找到 `IAuthenticationHandler`，由它把凭据变成主体 `Principal`；主体写进安全上下文 `SecurityContext.Principal`，之后的阶段只读。认证是能力 `security.authentication@1`，换认证方式就是换 Provider，宿主与业务代码不动。

5.0 的认证在内核管道里完成，不依赖 ASP.NET Core 的认证中间件；同一个处理器服务 HTTP、gRPC、MCP 与命令行。ASP.NET Core 的认证只在[绕过管道的端点](#绕过管道的端点)上用到。

凭据是 `AuthenticationCredential`：方案（`Scheme`，如 `Bearer`、`ApiKey`，比较时不区分大小写）与值（`Value`）。`AuthenticationCredential.TryParse` 解析 `<scheme> <value>` 形式，方案或值为空都不算凭据；`ToString()` 把值显示为 `***`。

| 宿主 | 凭据从哪来 |
| --- | --- |
| ASP.NET Core（HTTP、gRPC、MCP） | `Authorization` 头；没有时，请求路径落在登记的查询串令牌路径下才取查询串 `access_token`，作为 `Bearer` 凭据 |
| 命令行 | `--authorization '<scheme> <value>'`，或环境变量 `XIHAN_AUTHORIZATION` |

`Authorization` 头格式不对（如只有 `Bearer`）时视为没有凭据，调用按匿名继续。

## Authenticate 阶段

`AuthenticationMiddleware` 的处理顺序：

1. 调用上下文里没有凭据：保持匿名，进入第 5 步。
2. 在容器里的 `IAuthenticationHandler` 中按方案名（不区分大小写）找第一个匹配者；找不到返回 `SECURITY_VALIDATION_001`。
3. 处理器返回 `Result<Principal>`。凭据无效、过期、签名不符一律返回 `SECURITY_DENIED_003`，不区分原因对外暴露；处理器内部的基础设施故障以异常抛出，由内核转成失败结果。
4. 认证成功：写入新的安全上下文，保留已有的租户。
5. `RequireAuthentication` 为真且主体仍是匿名：返回 `SECURITY_DENIED_001`。

`RequireAuthentication` 缺省为假：匿名调用放行，由各操作的授权要求决定是否需要登录。

| 键（`XiHan:Security`） | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `RequireAuthentication` | `bool` | `false` | 为真时匿名调用在 Authenticate 阶段被拒绝 |
| `RoleClaim` | `string` | `role` | 承载角色的声明类型；RBAC 检查器与 ASP.NET Core 方案 `XiHan` 都读它 |

```json
{
  "XiHan": {
    "Security": {
      "RequireAuthentication": false,
      "RoleClaim": "role"
    }
  }
}
```

## 读取主体

`Principal` 不可变：

| 成员 | 说明 |
| --- | --- |
| `Id` | 主体 ID；匿名主体为空字符串 |
| `AuthenticationScheme` | 认证方案；匿名主体为 `null` |
| `IsAuthenticated` | 方案不为空且 `Id` 非空 |
| `DisplayName` | 显示名 |
| `Claims` | 声明列表（`KeyValuePair<string, string>`），同一类型可以有多个值 |
| `ClaimValues(type)` | 取某类型的全部声明值，类型按序数比较 |
| `HasClaim(type, value)` | 是否有这条声明 |

声明按原始类型名保留（`sub`、`role`、`tenant`），不映射成 .NET 的长类型 URI。匿名主体是 `Principal.Anonymous`，匿名无租户的安全上下文是 `SecurityContext.Anonymous`。

操作契约的处理器从调用上下文读：

```csharp
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

public static class CallerInfo
{
    public static string Describe(InvocationContext context)
    {
        var security = context.GetSecurity();
        return security.Principal.IsAuthenticated
            ? $"{security.Principal.Id} ({security.Principal.AuthenticationScheme}) @ {security.Tenant?.Id ?? "-"}"
            : "anonymous";
    }
}
```

经 `UseXiHan()` 的普通端点用 `http.GetXiHanContext()` 取调用上下文，SimpleApp 的 `/whoami` 端点就是这样返回主体与租户的。

## 选择认证 Provider

| 包 | 方案 | 清单角色 | 适用 |
| --- | --- | --- | --- |
| `Security.InMemory` | `ApiKey` | `default` | 开发、原型、测试；`Profiles.Standard` 已包含 |
| `Security.Jwt` | `Bearer` | `provider` | 应用自己签发的 HS256 令牌 |
| `Security.OpenIdConnect` | `Bearer` | `provider` | 外部身份提供方签发的令牌（RS256、ES256 等），签名密钥取自 JWKS |

`Security.InMemory` 同时提供认证与授权。装配 `Security.Jwt` 或 `Security.OpenIdConnect` 后，它让出认证、只保留授权（RBAC 授予存储）。`Security.Jwt` 与 `Security.OpenIdConnect` 都提供 `security.authentication@1`，同一时刻只装配其一。

5.0 的 JWT 与 OpenID Connect Provider 只校验令牌，不签发令牌；令牌由应用的登录服务或外部身份提供方签发。

## ApiKey（内存）

主体与 API Key 写在配置里，凭据是 `Authorization: ApiKey <key>`。下面的主体来自 `next/samples/SimpleApp`：

```json
{
  "XiHan": {
    "Security": {
      "InMemory": {
        "Principals": {
          "alice": { "ApiKey": "dev-alice-key", "DisplayName": "Alice", "Roles": "editor", "Tenant": "acme" },
          "bob": { "ApiKey": "dev-bob-key", "DisplayName": "Bob", "Roles": "viewer", "Tenant": "acme" }
        }
      }
    }
  }
}
```

节名是主体 ID；`ApiKey` 必填且至少 8 个字符；`Roles` 逗号分隔；`Claims:*` 下的键值作为附加声明。认证成功的主体按顺序带上：每个角色一条 `RoleClaim` 类型的声明、一条 `tenant` 声明（有 `Tenant` 时）、附加声明。

`ApiKeyAuthenticationHandler` 逐条做常量时间比较，不用字典查找，耗时不随匹配位置泄露信息；找不到返回 `SECURITY_DENIED_003`。主体数上限 `XiHan:Security:InMemory:MaxPrincipals`，缺省 1000。

```bash
curl -H "Authorization: ApiKey dev-alice-key" http://localhost:5000/whoami
```

::: warning 内存安全只用于开发与测试
API Key 明文写在配置里，主体与授予都在进程内存中，重启即恢复为配置内容。生产环境换成 JWT 或 OpenID Connect 认证，授予换成 `Security.Database`。
:::

## JWT

`Security.Jwt` 校验应用自己签发、HS256 对称密钥签名的 `Bearer` 令牌：

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Security.Jwt;

var builder = WebApplication.CreateBuilder(args);

// 显式 Provider 顶替内存实现的认证部分；授权仍由 Security.InMemory 承担
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<JwtSecurityFeature>());

var app = builder.Build();
app.UseXiHan();
app.Run();
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

| 键（`XiHan:Security:Jwt`） | 缺省 | 说明 |
| --- | --- | --- |
| `Secret` | 必填 | 对称密钥，UTF-8 编码后至少 32 字节（`JwtOptions.MinSecretBytes`） |
| `Issuer` | 不校验 | 配置了才校验签发者 |
| `Audience` | 不校验 | 配置了才校验受众 |
| `ClockSkewSeconds` | `60` | 校验有效期时允许的时钟偏移，不能为负 |
| `NameClaim` | `sub` | 作为主体 ID 的声明类型 |

签名、`exp` 与有效期始终强制校验。主体 ID 取 `NameClaim` 声明，令牌里没有这条声明同样返回 `SECURITY_DENIED_003`（消息键 `security.token.subject_missing`）；显示名取 `name` 声明；令牌的全部声明按原始类型名进入主体，所以角色声明的类型要与 `XiHan:Security:RoleClaim` 一致，租户声明的类型要与 `XiHan:MultiTenancy:TenantClaim` 一致。

## OpenID Connect

`Security.OpenIdConnect` 校验外部身份提供方签发的 `Bearer` 令牌，签名密钥从签发者的发现文档与 JWKS 取得：

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

| 键（`XiHan:Security:OpenIdConnect`） | 缺省 | 说明 |
| --- | --- | --- |
| `Authority` | 必填 | 签发者地址，绝对 http / https URI |
| `MetadataAddress` | `<Authority>/.well-known/openid-configuration` | 发现文档地址 |
| `Issuer` | 发现文档的 `issuer` | 期望的签发者，始终校验 |
| `Audience` | 不校验 | 配置了才校验受众 |
| `RequireHttpsMetadata` | `true` | 发现文档必须经 HTTPS 取得；只在本地开发时关闭 |
| `ClockSkewSeconds` | `60` | 有效期校验的时钟偏移 |
| `RefreshIntervalSeconds` | `43200`（12 小时） | 元数据自动刷新间隔 |
| `MinimumRefreshIntervalSeconds` | `300`（5 分钟） | 两次按需刷新的最短间隔，也是刷新失败后沿用旧密钥的时长 |
| `BackchannelTimeoutSeconds` | `30` | 取发现文档与 JWKS 的 HTTP 超时 |
| `NameClaim` | `sub` | 作为主体 ID 的声明类型 |

元数据按需取得：首次认证时取发现文档的 `issuer` 与 `jwks_uri`，再取 JWKS 的签名密钥并缓存，并发认证只发一次请求。令牌的 `kid` 不在 JWKS 里时（签发者轮换了密钥）按需刷新一次再校验，两次按需刷新至少间隔 `MinimumRefreshInterval`，伪造的 `kid` 不会让签发者被反复请求。

刷新失败时沿用最后一份密钥；从未取到元数据时返回 `SECURITY_UNAVAILABLE_003`（HTTP 503），5 秒（`OpenIdConnectMetadataCache.FailureRetryDelay`）后再试。读不出的令牌直接返回 `SECURITY_DENIED_003`，不为它访问签发者。`RequireHttpsMetadata` 为真而发现文档地址不是 HTTPS 时，启动读取配置就抛出 `InvalidOperationException`。主体与声明的映射与 JWT Provider 相同。

## 凭据无效与 401 质询

认证失败与无权限在协议上分开（ADR-0042）：`SECURITY_DENIED_001`（未认证）与 `SECURITY_DENIED_003`（凭据无效）是认证失败，客户端据此刷新凭据或重新登录；其余 `DENIED` 是已认证但无权限。

| 错误码 | HTTP | `WWW-Authenticate` | gRPC |
| --- | --- | --- | --- |
| `SECURITY_DENIED_001` 未认证 | 401 | 按已装配的认证处理器逐个质询方案名（如 `Bearer`、`ApiKey`），去重，不带错误参数 | `Unauthenticated` |
| `SECURITY_DENIED_003` 凭据无效 | 401 | 只质询请求出示的方案：Bearer 为 `Bearer error="invalid_token"`，其他方案只写方案名 | `Unauthenticated` |
| `SECURITY_DENIED_002`、`SECURITY_DENIED_004` | 403 | 无 | `PermissionDenied` |
| `SECURITY_VALIDATION_001` 方案不支持 | 400 | 无 | `InvalidArgument` |

质询头在 `UseXiHan()` 写错误响应时加上（MCP 端点同样经过它），不带 `error_description`，与"不区分原因对外暴露"一致；具体原因只进日志。gRPC 的尾部元数据仍带 `xihan-error-code`。命令行宿主按错误码种类给退出码，`DENIED` 为 4。

## 绕过管道的端点

SignalR Hub、Server-Sent Events 这类长连接端点不经内核管道：不受截止时间约束，内核停止时也不会被 503 打断，因此 Authenticate 阶段管不到它们。`Hosting.AspNetCore` 提供 ASP.NET Core 认证方案 `XiHan`（ADR-0051），把这类端点的凭据交给内核已装配的同一个 `IAuthenticationHandler`，JWT、OpenID Connect、ApiKey 都适用，校验项只有一份。

方案的规则：

1. 只认带 `PipelineBypassMetadata` 的端点。只登记了这一个方案时 ASP.NET Core 会把它当缺省方案，普通请求在这里按未认证处理，仍由管道认证，不会被认证两次。
2. 凭据按宿主的同一规则取：先 `Authorization` 头；没有时，请求路径（不含 PathBase）按路径段、不区分大小写落在 `QueryTokenPaths` 登记的前缀下，且 `access_token` 只有一个非空值，才作为 `Bearer` 凭据。没登记的路径带查询串令牌一律忽略，请求按匿名处理。
3. 没有凭据是未认证，不是失败；方案不支持、凭据无效都是认证失败（401），质询与管道一致，方案不支持时按已装配的方案质询。
4. 认证成功后按登记顺序调用应用的 `IInvocationAuthenticationFilter`，任一返回失败即认证失败。框架不判定会话，会话门禁在这里由应用实现。
5. 主体转成 `ClaimsPrincipal`：身份的认证类型为 `XiHan`，声明按原始类型名保留，另加一条 `ClaimTypes.NameIdentifier` 承载主体 ID 并作为名称声明（SignalR 的用户标识随之可用），角色声明类型取 `XiHan:Security:RoleClaim`。

```csharp
using Microsoft.AspNetCore.Authorization;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Hosting.AspNetCore.Authentication;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Security.Jwt;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<JwtSecurityFeature>());

// 浏览器的 WebSocket 与 EventSource 带不了请求头，只在 /hubs 下接受查询串令牌
builder.Services.AddAuthentication().AddXiHan(options => options.QueryTokenPaths.Add("/hubs"));
builder.Services.AddAuthorization();
builder.Services.AddSignalR();
// ISessionStatus 的实现由应用登记
builder.Services.AddSingleton<IInvocationAuthenticationFilter, ActiveSessionFilter>();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseXiHan();

var xihanScheme = new AuthorizeAttribute { AuthenticationSchemes = InvocationAuthenticationDefaults.AuthenticationScheme };
PipelineBypassMetadata.BypassXiHanPipeline(app.MapHub<OrdersHub>("/hubs/orders").RequireAuthorization(xihanScheme));

app.Run();
```

`AddXiHan` 可以多次调用：方案只登记一次，各次的配置依次生效，各模块可以各自添加查询串令牌路径。前缀不能为空也不能是 `/`，方案初始化时校验。查询串会进入访问日志与代理日志，只给带不了请求头的客户端用。

挂点核对令牌所属的会话：

```csharp
using XiHan.Framework.Hosting.AspNetCore.Authentication;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;

// 应用自己的会话表
public interface ISessionStatus
{
    ValueTask<bool> IsActiveAsync(string sessionId, CancellationToken cancellationToken);
}

public sealed class ActiveSessionFilter(ISessionStatus sessions) : IInvocationAuthenticationFilter
{
    public async ValueTask<Result> AuthenticatedAsync(Principal principal, InvocationContext context, CancellationToken cancellationToken)
    {
        // 会话注销后令牌在有效期内仍能通过签名校验，长连接的握手要在这里拒绝
        var sessionId = principal.ClaimValues("sid").FirstOrDefault();
        return sessionId is not null && await sessions.IsActiveAsync(sessionId, cancellationToken)
            ? Result.Success()
            : Result.Failure(Error.Denied(SecurityErrors.InvalidCredential, SecurityMessages.InvalidToken));
    }
}
```

在这类端点里发起内核调用，用 `HttpInvocation.CreateContext(httpContext, services)` 以请求建调用上下文，凭据按同一规则带上，调用经管道时照常重新认证与授权；长连接上的令牌过期后，新的调用会被拒绝。下面的 Hub 调用 SimpleApp 的 `orders.list` 操作：

```csharp
using Microsoft.AspNetCore.SignalR;
using SimpleApp.Orders;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel.Operations;

public sealed class OrdersHub(IOperationInvoker operations) : Hub
{
    public async Task<int> CountOrders()
    {
        var http = Context.GetHttpContext() ?? throw new InvalidOperationException("The hub connection has no HTTP context.");

        // 经完整管道调用：Authorize 阶段照常按契约的 orders.read 判定
        var context = HttpInvocation.CreateContext(http, http.RequestServices);
        var result = await operations.InvokeAsync<ListOrdersRequest, OrderList>(context, ListOrders.Id, new ListOrdersRequest());
        return result.IsSuccess ? result.ValueOrThrow().Items.Count : throw new HubException(result.Error?.Code);
    }
}
```

管道内的会话门禁仍由应用写成 Authenticate 阶段的中间件；挂点只作用于方案 `XiHan`。

## 错误码

| 错误码 | 含义 |
| --- | --- |
| `SECURITY_DENIED_001` | 未认证：`RequireAuthentication` 为真时的匿名调用，或要求认证的策略遇到匿名主体 |
| `SECURITY_DENIED_003` | 凭据无效、过期、签名不符，或令牌缺少主体声明 |
| `SECURITY_VALIDATION_001` | 认证方案不受支持 |
| `SECURITY_UNAVAILABLE_002` | 内存主体存储容量已满（`InMemoryPrincipalStore.Set` 与健康项 `security.inmemory.capacity`） |
| `SECURITY_UNAVAILABLE_003` | OpenID Connect 签发者的发现文档或 JWKS 不可达，且没有缓存 |

## 与其他能力的关系

- [多租户](./multi-tenancy)：Tenant 阶段在认证之后，从主体的租户声明解析租户。
- [授权](./authorization)：授权策略读认证写入的主体；匿名主体遇到权限策略得到 `SECURITY_DENIED_001`。
- [宿主与 Web 应用](./web)：`UseXiHan()` 把请求头翻译成调用上下文，并在 401 响应上写质询头。
- [执行管道](./pipeline)：Authenticate 阶段在 Resilience 之后、Tenant 之前。

## 相关

- [Security.Abstractions](../packages/security-abstractions)
- [Security.InMemory](../packages/security-inmemory)
- [Security.Jwt](../packages/security-jwt)
- [Security.OpenIdConnect](../packages/security-openidconnect)
- [Hosting.AspNetCore](../packages/hosting-aspnetcore)
- 规范：RFC-0008《认证与授权》；ADR-0042、ADR-0051
