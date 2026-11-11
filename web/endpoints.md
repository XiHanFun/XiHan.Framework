来源：https://framework.docs.xihanfun.com/web/endpoints

# 端点里使用框架

这页讲不是操作契约的普通最小 API 端点怎样使用框架：它们在 `UseXiHan()` 之后处于管道的 Handle 阶段内，可以读调用上下文里的主体与租户、把失败结果交给宿主写成 Problem Details、挂上由 Authorize 阶段判定的授权要求，也可以选择绕过管道并改用 ASP.NET Core 认证方案 `XiHan`。读完这页，我们能判断一个端点该写成操作还是普通端点，并在普通端点里正确地拿到框架的东西。

前置知识：[ASP.NET Core 宿主](/web/hosting)、[执行管道](/startup/pipeline)与[调用上下文](/startup/invocation-context)。

## 为什么需要

不是所有 HTTP 入口都适合写成操作：文件下载、重定向、健康面板、临时的诊断端点、SignalR Hub 都有自己的 HTTP 形态，硬套 `IOperation<TRequest, TResponse>` 反而别扭。但这些端点同样需要知道调用方是谁、属于哪个租户，失败时也应该和操作一样写出 Problem Details，而不是另起一套错误格式。

宿主把普通端点放进管道的 Handle 阶段：请求到达端点时 Authenticate、Tenant、Authorize 阶段已经执行完，端点只要从 `HttpContext` 取出调用上下文就能读到结果；把失败写回上下文，宿主在管道结束后统一翻译。真正不能进管道的长连接端点，再用 `PipelineBypassMetadata` 标出来，认证交给方案 `XiHan`。

## 端点在管道里的位置

`UseXiHan()` 之后映射的端点，每个请求都先经过内核管道的前十个阶段，端点委托本身就是 Handle 阶段，端点返回后再走 Egress。于是在端点里：

| 要做的事 | 入口 | 说明 |
| --- | --- | --- |
| 取当前调用上下文 | `http.FindXiHanContext()` | 返回 `Maybe<InvocationContext>`，没经过 `UseXiHan()` 时为无值 |
| 读主体与租户 | `context.GetSecurity()` | `SecurityContext`：`Principal`（`Id`、`IsAuthenticated`、声明）与 `Tenant`（可空） |
| 返回框架错误 | `context.SetOutcome(Error)` | 失败结果留在上下文，宿主写 Problem Details |
| 取调用方语言的文案 | `key.L()` | 按上下文里协商出的区域翻译 |
| 要求已认证 / 权限 / 策略 | `RequireXiHanAuthentication()` 等 | 写在端点元数据上，Authorize 阶段判定 |
| 读截止时间与取消 | `context.Deadline`、`context.CancellationToken` | 与操作里相同 |

端点委托里抛出的异常由内核翻译成 `KERNEL_UNEXPECTED_001`、HTTP 500；可恢复的失败不要抛，用 `SetOutcome`。

## 最小示例

下面三个端点来自示例应用的 `Program.cs`，接 [ASP.NET Core 宿主](/web/hosting)里的装配代码：

```csharp
using SimpleApp;
using SimpleApp.Orders;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, SampleJsonContext.Default));
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<OrdersFeature>());

var app = builder.Build();
app.UseXiHan();

// 读安全上下文：Authenticate 与 Tenant 阶段已经写好主体与租户
app.MapGet("/whoami", (HttpContext http) =>
{
    var security = http.FindXiHanContext().ValueOrThrow().GetSecurity();
    return new WhoAmI(security.Principal.Id, security.Principal.IsAuthenticated, security.Tenant?.Id);
}).ExcludeFromDescription();

// 返回框架错误：失败结果留在上下文里，管道结束后宿主写成 Problem Details
app.MapGet("/missing", (HttpContext http) =>
{
    http.FindXiHanContext().ValueOrThrow().SetOutcome(Error.NotFound("SAMPLES_NOTFOUND_001", SampleMessages.ResourceMissing));
    return Results.Empty;
});

// 授权要求：没有凭据的请求在 Authorize 阶段被拒，端点委托不执行
app.MapGet("/orders/stats", (OrderEventCounter counter) => new OrderStatistics(counter.Handled))
    .RequireXiHanAuthentication()
    .ExcludeFromDescription();

app.Run();
```

`OrdersFeature`、`OrderEventCounter`、`OrderStatistics`、`SampleJsonContext` 与 `WhoAmI`（记录：`Principal`、`IsAuthenticated`、`Tenant`）都来自示例应用（见[示例说明](/appendix/samples)）；`SampleMessages.ResourceMissing` 是消息键常量 `samples.resource.missing`，各语言文案在 `Localization/<区域>.json` 里。`WhoAmI` 与 `OrderStatistics` 登记进源生成的序列化上下文。运行后：

```bash
curl -s localhost:5000/whoami
curl -s localhost:5000/whoami -H "Authorization: ApiKey dev-alice-key"
curl -s localhost:5000/missing -H "Accept-Language: zh-CN"
curl -s -i localhost:5000/orders/stats
```

第一条返回匿名主体：`{"principal":null,"isAuthenticated":false,"tenant":null}`（匿名主体没有 ID）；第二条返回 `alice` 与她的租户 `acme`；第三条返回 404 的 Problem Details，`code` 是 `SAMPLES_NOTFOUND_001`，`detail` 是中文文案；第四条返回 401，`code` 是 `SECURITY_DENIED_001`，响应头带 `WWW-Authenticate: ApiKey`。

## 读调用上下文

`HttpContext.FindXiHanContext()` 返回 `Maybe<InvocationContext>`：请求经过了 `UseXiHan()` 时有值；绕过管道的端点、或者根本没调用 `UseXiHan()` 时为无值。端点确定自己在管道内时用 `ValueOrThrow()`，不确定时用 `TryGetValue(out var context)` 分支处理。

上下文里的内容按阶段写入，端点执行时都已就位：`GetSecurity()` 取 Authenticate 与 Tenant 阶段写好的 `SecurityContext`；`TraceId`、`Baggage`、`Deadline` 来自请求头；`FindOperation()` 在普通端点上为无值，因为它不是操作。读主体声明的写法见[认证](/security/authentication)。

调用链深处拿不到 `HttpContext` 的服务（领域服务、仓储装饰器、消息处理器里的辅助类）不要依赖 HTTP，改用作用域服务 `IInvocationAccessor.Current` 取当前调用的上下文，它同样返回 `Maybe<InvocationContext>`，对 HTTP、gRPC、命令行与消息投递都成立，见[调用上下文](/startup/invocation-context)。

## 返回错误

端点把失败写进调用上下文，由宿主统一翻译：

```csharp
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

app.MapGet("/reports/{id}", (string id, HttpContext http) =>
{
    // 结果写进上下文，Egress 由宿主翻译为 Problem Details；消息参数随错误一起带走，文案在 Egress 翻译
    http.FindXiHanContext().ValueOrThrow().SetOutcome(
        Error.NotFound("SHOP_NOTFOUND_002", "shop.report.not_found", MessageArguments.Of(("id", id))));
    return Results.Empty;
});
```

几条规则：

- `SetOutcome` 接受 `Result`，`Error` 隐式转换成失败结果；调用后端点返回 `Results.Empty` 即可，宿主在响应还没开始写出时写 Problem Details。端点已经写出正文后再 `SetOutcome`，宿主保留端点的内容，只有结果记入日志与审计。
- 状态码由错误码的 KIND 段映射（`NOTFOUND` 是 404），对照表见 [HTTP 路由与载荷](/web/routing)；`detail` 按调用方区域翻译，`details` 带标量参数。
- `Error` 只携带消息键与参数，不带文案；键与各语言文件的约定见[结果与错误处理](/web/errors)与[全球化和本地化](/common/localization)。
- 操作契约不需要这样做：`HandleAsync` 返回的 `Result` 就是调用结果。

绕过管道的端点没有上下文，要自己写错误时用 `ProblemResponse`：`FromError(error, instance, traceId)` 构造响应体，`StatusCodeFor(error)` 给出状态码，`WriteTo(writer)` 写出 JSON，结果与宿主写出的一致。

## 端点上的授权要求

普通端点用三个约定扩展挂授权要求，`UseXiHan()` 把它放进上下文，Authorize 阶段判定：

| 扩展 | 要求 |
| --- | --- |
| `RequireXiHanAuthentication()` | 已认证 |
| `RequireXiHanPermission(permissionCode, resource)` | 持有权限码；`resource` 可选，限定到某个资源 |
| `RequireXiHanPolicy(policyName, resource)` | 命名策略允许；`resource` 可选 |

```csharp
using XiHan.Framework.Hosting.AspNetCore.Endpoints;

app.MapGet("/orders/stats", (OrderEventCounter counter) => new OrderStatistics(counter.Handled))
    .RequireXiHanAuthentication();

app.MapGet("/orders/export", () => Results.Text("id,name"))
    .RequireXiHanPermission("orders.read");
```

判定由安全子系统完成：没有装配 `security.authorization@1` 时要求只进入上下文，不被判定。权限码必须由某个特性用 `FeatureContext.AddPermission` 登记；装配了授权能力时，`UseXiHan()` 在宿主建请求管道时核对全部端点的要求，未登记的权限码与不存在的策略让宿主启动失败（`SECURITY_VALIDATION_004`，一次列出全部）。端点上的要求优先于操作契约上的权限或策略。认证与授权的配置见[认证](/security/authentication)与[授权与权限](/security/authorization)。

这三个扩展与 ASP.NET Core 自己的 `RequireAuthorization()` 不是一回事：前者由内核管道的 Authorize 阶段判定，对管道内的端点用；后者由 ASP.NET Core 授权中间件判定，只给下文绕过管道的端点用。

## 端点与 OpenAPI 文档

`MapXiHanOpenApi()` 生成的文档也会收录普通端点，条件是端点有 HTTP 方法元数据、带 `Accepts` 或 `Produces` 描述元数据之一，并且没有 `ExcludeFromDescription()`。最小 API 为返回类型自动加上 `Produces` 元数据，所以返回 JSON 对象的端点缺省会进文档，而文档对 JSON 类型只引用已登记操作的 Schema，不另做推导：引用不到时整份文档生成失败（`PROTOCOLS_VALIDATION_010`）。

因此示例里 `/whoami`、`/orders/stats` 这类返回自定义 JSON 的端点都加了 `ExcludeFromDescription()`；`/missing` 返回 `Results.Empty`，没有响应类型，不进文档。响应正文确实与某个操作的请求或响应同形时，用 `WithXiHanOperationSchema(request:, response:)` 声明引用，端点就能进文档并共用组件。规则与写法见[规范化接口文档](/web/openapi)。

## 绕过管道的端点

SignalR Hub、Server-Sent Events 这类长连接端点不经内核管道：不受截止时间约束，内核停止时也不会被 503 打断，因此 Authenticate 阶段管不到它们。给端点加 `PipelineBypassMetadata`，`UseXiHan()` 看到它就直接放行，不建调用上下文；探针、OpenAPI 文档、`.proto` 与 gRPC 方法的端点都带着它。

这类端点需要认证时，`Hosting.AspNetCore` 提供 ASP.NET Core 认证方案 `XiHan`，把凭据交给内核已装配的同一个 `IAuthenticationHandler`，JWT、OpenID Connect、ApiKey 不用各接一遍：

1. `AddAuthentication().AddXiHan(...)` 登记方案；浏览器的 WebSocket 与 EventSource 带不了请求头，把允许查询串令牌的路径前缀加进 `QueryTokenPaths`。
2. `UseAuthentication()`、`UseAuthorization()` 放在 `UseXiHan()` 之前。
3. 映射端点时用 `PipelineBypassMetadata.BypassXiHanPipeline(...)` 标注绕过，`RequireAuthorization` 的策略指定方案 `XiHan`。

```csharp
using Microsoft.AspNetCore.Authorization;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Hosting.AspNetCore.Authentication;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseStandardProfile());

builder.Services.AddSignalR();
// 查询串 access_token 只在 /hubs 下接受：查询串会进访问日志与代理日志
builder.Services.AddAuthentication().AddXiHan(options => options.QueryTokenPaths.Add("/hubs"));
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseXiHan();

var xihanScheme = new AuthorizeAttribute { AuthenticationSchemes = InvocationAuthenticationDefaults.AuthenticationScheme };
PipelineBypassMetadata.BypassXiHanPipeline(app.MapHub<NotificationHub>("/hubs/notifications")).RequireAuthorization(xihanScheme);

app.Run();
```

方案 `XiHan` 的规则：

| 项 | 行为 |
| --- | --- |
| 生效范围 | 只认带 `PipelineBypassMetadata` 的端点；普通请求由内核管道认证，方案对它们返回未认证，不重复认证 |
| 凭据 | 先取 `Authorization` 头；没有时，请求路径（相对 PathBase）按路径段、不区分大小写落在 `QueryTokenPaths` 的某个前缀下，且 `access_token` 只有一个非空值，才作为 `Bearer` 凭据；其他路径的查询串令牌一律忽略 |
| 路径前缀 | 不能为空也不能是 `/`，方案初始化时校验，违反时抛出 `ArgumentException` |
| 认证 | 交给方案名匹配的内核认证处理器；没有凭据是未认证，方案不支持、凭据无效都是 401，质询与管道一致 |
| 会话门禁 | 认证成功后按登记顺序调用应用的 `IInvocationAuthenticationFilter`，任一返回失败即 401 |
| 主体 | 转成认证类型为 `XiHan` 的 `ClaimsPrincipal`：声明保留原始类型名，另加 `ClaimTypes.NameIdentifier` 承载主体 ID 并作名称声明，角色声明类型取 `XiHan:Security:RoleClaim`（缺省 `role`） |
| 登记 | `AddXiHan` 可以多次调用，方案只登记一次、各次的配置依次生效；不设为缺省方案 |

绕过管道的端点里 `FindXiHanContext()` 为无值。要在里面发起内核调用（如 Hub 方法调用操作），用 `HttpInvocation.CreateContext(httpContext, services)` 以请求建调用上下文，凭据按同一规则带上，调用经 `IOperationInvoker` 走完整管道时照常重新认证与授权。Hub 的完整写法、会话门禁与跨实例推送见[实时通信](/web/realtime)。

## 常见问题

### 这个入口该写成操作还是普通端点

输入输出是 JSON 对象、要暴露给多种协议或进 OpenAPI 文档、需要按 Schema 校验的，写成操作（[操作契约](/web/operations)）。返回文件、文本、重定向，或者形态只在 HTTP 上有意义的（下载、SSE、Hub），写普通端点。普通端点拿不到 Validate 阶段的 Schema 校验与 Transaction 阶段按契约的只读判断，其余阶段一样。

### `FindXiHanContext().ValueOrThrow()` 抛了异常

说明请求没有经过 `UseXiHan()`：要么端点映射在 `UseXiHan()` 之前，要么端点带了 `PipelineBypassMetadata`，要么根本没调用 `UseXiHan()`。核对中间件顺序；绕过管道的端点要用 `HttpInvocation.CreateContext(httpContext, services)` 自己建上下文。

### 加了 `RequireXiHanPermission` 之后应用启动失败

权限码没有被任何特性用 `AddPermission` 登记，或者 `RequireXiHanPolicy` 引用的策略不存在。宿主在建请求管道时核对全部端点的要求，一次列出全部不合格项（`SECURITY_VALIDATION_004`）。在拥有这个权限的特性里登记权限码，或者改用已登记的码。

### 端点返回了 JSON，为什么 `/openapi.json` 整个 500 了

文档对 JSON 类型只引用已登记操作的 Schema，端点返回的类型不是任何操作的请求或响应类型时报 `PROTOCOLS_VALIDATION_010`。给端点加 `ExcludeFromDescription()`，或者用 `WithXiHanOperationSchema` 声明它与哪个操作同形，或者把它改写成操作。

### `UseAuthentication()` 为什么要放在 `UseXiHan()` 之前

方案 `XiHan` 只服务绕过管道的端点，这类端点在 `UseXiHan()` 里被直接放行，认证与授权中间件放在前面，它们的握手在进入 `UseXiHan()` 之前就已经完成判定。放在后面时，管道内的普通请求也会在 Handle 阶段再经过一次 ASP.NET Core 认证中间件，而它们已经由内核管道认证过了。按约定放在 `UseXiHan()` 之前，顺序只有这一种。

## 相关包与下一步

- 相关包：[Hosting.AspNetCore](/packages/hosting-aspnetcore)、[Protocols.Http](/packages/protocols-http)、[Kernel](/packages/kernel)
- 下一步：[实时通信](/web/realtime)，用 SignalR 做推送，Hub 的认证交给方案 `XiHan`
- 相关章节：[调用上下文](/startup/invocation-context)、[结果与错误处理](/web/errors)、[认证](/security/authentication)、[授权与权限](/security/authorization)、[规范化接口文档](/web/openapi)
- 规范：RFC-0008《认证与授权》的「绕过管道的端点」一节
