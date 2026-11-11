# 实时通信

这页讲怎样在 XiHan 应用里做实时推送：直接使用 ASP.NET Core SignalR，Hub 的认证交给内核已装配的认证处理器，Hub 方法里照常调用操作，多实例部署时用消息子系统把事件送到持有连接的实例。读完这页，我们能接入一个带认证的 Hub，并知道框架在这件事上做了什么、没做什么。

前置知识：[端点里使用框架](/web/endpoints)的「绕过管道的端点」、[认证](/security/authentication)与[发布与订阅](/messaging/pubsub)。

## 为什么需要

WebSocket、Server-Sent Events 这类长连接和一次请求一次响应的调用不一样：连接可能持续几小时，不能受截止时间约束，应用正在停止时也不该被 503 打断，所以它们不能进内核管道。可一旦不进管道，Authenticate 阶段就管不到它们，而 Hub 又确实需要知道连接方是谁，否则推送会发给错误的人。

框架不包装 SignalR，只解决衔接上的两件事：让绕过管道的端点能用内核的认证处理器认证（ASP.NET Core 认证方案 `XiHan`），以及让 Hub 方法能以连接的凭据建调用上下文、经完整管道调用操作。推送本身、连接登记、横向扩展都用 ASP.NET Core 与消息子系统现成的能力。

## 框架做什么、不做什么

| 事项 | 由谁提供 |
| --- | --- |
| Hub、`MapHub`、`IHubContext<THub>`、分组、客户端库 | ASP.NET Core 共享框架自带的 SignalR，不需要额外的框架包 |
| Hub 的认证 | `Hosting.AspNetCore` 的认证方案 `XiHan`，复用内核已装配的 `IAuthenticationHandler`（JWT、OpenID Connect、ApiKey） |
| 会话是否仍有效 | 应用实现 `IInvocationAuthenticationFilter` |
| 在 Hub 里调用操作 | `HttpInvocation.CreateContext(httpContext, services)` 加 `IOperationInvoker` |
| 跨实例推送 | SignalR 自己的背板（如 Redis）或托管服务；业务事件经消息子系统送达 |
| Hub 基类、连接登记表、推送抽象 | 框架不提供 |

## 最小示例

1. `AddSignalR()`，并用 `AddAuthentication().AddXiHan(...)` 登记方案 `XiHan`；浏览器的 WebSocket 与 EventSource 带不了请求头，把 Hub 的路径前缀登记进 `QueryTokenPaths`。
2. `UseAuthentication()`、`UseAuthorization()` 放在 `UseXiHan()` 之前。
3. 映射 Hub 时用 `PipelineBypassMetadata.BypassXiHanPipeline(...)` 标注绕过内核管道，授权策略指定方案 `XiHan`。

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
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

PipelineBypassMetadata.BypassXiHanPipeline(app.MapHub<NotificationHub>("/hubs/notifications"))
    .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = InvocationAuthenticationDefaults.AuthenticationScheme });

app.Run();

public sealed class NotificationHub : Hub;
```

运行后，能带请求头的客户端（.NET、移动端）以 `Authorization` 头连接，凭据交给内核已装配的认证处理器，Standard 组合缺省是内存 ApiKey 处理器（`Authorization: ApiKey dev-alice-key`）。浏览器客户端以 `/hubs/notifications?access_token=<令牌>` 连接，查询串令牌一律按 `Bearer` 方案处理，所以要装配以 `Bearer` 工作的 Provider（`Security.Jwt` 或 `Security.OpenIdConnect`），只有 ApiKey 处理器时浏览器拿不到可用的凭据。令牌无效时握手得到 401，质询与管道里一致。`Context.UserIdentifier` 在 Hub 里可用，值是内核主体的 ID。

## 方案 `XiHan` 的规则

| 项 | 行为 |
| --- | --- |
| 生效范围 | 只认带 `PipelineBypassMetadata` 的端点；普通请求由内核管道认证，方案对它们返回未认证，不重复认证 |
| 凭据 | 先取 `Authorization` 头；没有时，请求路径（相对 PathBase）按路径段、不区分大小写落在 `QueryTokenPaths` 的某个前缀下，且 `access_token` 只有一个非空值，才作为 `Bearer` 凭据；其他路径的查询串令牌一律忽略 |
| 路径前缀 | 不能为空也不能是 `/`，方案初始化时校验，违反时抛出 `ArgumentException` |
| 认证 | 交给方案名匹配的内核认证处理器；方案不支持、凭据无效都是 401，质询与管道一致（Bearer 带 `error="invalid_token"`） |
| 主体 | 转成认证类型为 `XiHan` 的 `ClaimsPrincipal`：声明保留原始类型名，另加 `ClaimTypes.NameIdentifier` 承载主体 ID 并作名称声明（SignalR 的 `UserIdentifier` 随之可用），角色声明类型取 `XiHan:Security:RoleClaim`（缺省 `role`） |
| 登记 | `AddXiHan` 可以多次调用，方案只登记一次、各次的配置依次生效；不设为缺省方案 |

查询串令牌只给带不了请求头的客户端用：查询串会进入访问日志与代理日志，所以要把 `QueryTokenPaths` 限定到 Hub 的前缀，而不是整个站点。

## 会话门禁

令牌在有效期内、但对应的会话已经注销时，方案 `XiHan` 本身认不出来。应用实现 `IInvocationAuthenticationFilter` 做这类核对：内核处理器认证成功后按登记顺序调用，任一返回失败即 401。框架不判定会话，会话状态由应用提供。

```csharp
using XiHan.Framework.Hosting.AspNetCore.Authentication;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;

// 应用自己的会话表
public interface ISessionStatus
{
    bool IsActive(string sessionId);
}

public sealed class SessionGate(ISessionStatus sessions) : IInvocationAuthenticationFilter
{
    public ValueTask<Result> AuthenticatedAsync(Principal principal, InvocationContext context, CancellationToken cancellationToken)
    {
        // 会话已注销按凭据无效拒绝：客户端据 401 重新登录
        var active = principal.ClaimValues("sid").All(sessions.IsActive);
        return ValueTask.FromResult(active ? Result.Success() : Result.Failure(Error.Denied(SecurityErrors.InvalidCredential, SecurityMessages.InvalidToken)));
    }
}
```

```csharp
builder.Services.AddSingleton<IInvocationAuthenticationFilter, SessionGate>();
```

`ISessionStatus` 是应用自己的接口，示例只说明挂点的写法。挂点只作用于方案 `XiHan`；管道内的会话门禁仍由应用写成 Authenticate 阶段的中间件，见[执行管道](/startup/pipeline)。

## 在 Hub 里调用操作

Hub 方法不在内核管道里，`HttpContext.FindXiHanContext()` 在这里是无值。要执行操作时以连接的 `HttpContext` 建调用上下文，再交给 `IOperationInvoker`：

```csharp
using Microsoft.AspNetCore.SignalR;
using SimpleApp.Orders;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel.Operations;

public sealed class OrdersHub(IOperationInvoker operations, IServiceProvider services) : Hub
{
    public async Task<OrderList> GetOrders()
    {
        var http = Context.GetHttpContext() ?? throw new HubException("The connection has no HTTP context.");
        // 凭据按方案 XiHan 的同一规则带上；调用经完整管道：重新认证、按契约授权、记审计
        var call = HttpInvocation.CreateContext(http, services);
        var result = await operations.InvokeAsync<ListOrdersRequest, OrderList>(call, ListOrders.Id, new ListOrdersRequest());
        return result.TryGetValue(out var orders) ? orders : throw new HubException(result.Error?.Code);
    }
}
```

`ListOrders`、`ListOrdersRequest`、`OrderList` 来自示例应用。上下文用 Hub 方法自己的服务作用域（构造函数注入的 `IServiceProvider`），不记入 `HttpContext.Items`。每次调用都重新认证，长连接上的令牌过期后，新的调用会被拒绝；失败结果是 `Result`，Hub 方法自己决定怎样告知客户端，上例把错误码放进 `HubException`。操作的调用器与错误码见[操作契约](/web/operations)。

## 跨实例推送

`IHubContext<THub>` 只能推给连在本实例上的连接。多实例部署时：

- 推到其他实例上的连接要靠 SignalR 自己的横向扩展，如 Redis 背板或托管的 SignalR 服务；框架不提供背板。
- 业务事件借消息子系统跨进程送达。缺省的竞争订阅是工作队列，每条消息只交给一个实例的处理器（至少一次），这个实例经 `IHubContext<THub>` 推送，背板再转给其他实例上的连接。事件随业务数据在同一事务里写出时，用发件箱。

```csharp
using Microsoft.AspNetCore.SignalR;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Primitives;

public sealed class OrderPushHandler(IHubContext<NotificationHub> hub) : IMessageHandler
{
    public async ValueTask<Result> HandleAsync(MessageDelivery delivery, InvocationContext context, CancellationToken cancellationToken)
    {
        await hub.Clients.All.SendAsync("orderPlaced", delivery.Envelope.Subject, cancellationToken);
        return Result.Success();
    }
}
```

```csharp
using XiHan.Framework.Messaging.Abstractions;

builder.Services.AddMessageHandler<OrderPushHandler>("orders");
```

消息处理器经完整管道执行，信封扩展属性里的租户在这里已经进入调用上下文，按租户分组推送时从 `context.GetSecurity().Tenant` 取。主题、处理器与发件箱见[发布与订阅](/messaging/pubsub)与[领域事件与发件箱](/messaging/domain-events)。

有些事要在每个实例上各做一次，例如吊销会话后断开连在各个实例上的连接、清掉各实例的本地缓存：背板只转发推送，断不了别的实例上的连接。用广播订阅（`SubscriptionMode.Broadcast`）：发起的实例发布一条消息，每个在线实例的处理器各收到一份，各自处理连在自己身上的连接。

```csharp
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Primitives;

// 应用自己的连接登记：断开本实例上属于这些会话的连接
public interface ILocalConnections
{
    ValueTask DisconnectAsync(IReadOnlyCollection<string> sessionIds, CancellationToken cancellationToken);
}

public sealed class SessionsRevokedHandler(ILocalConnections connections) : IMessageHandler
{
    public async ValueTask<Result> HandleAsync(MessageDelivery delivery, InvocationContext context, CancellationToken cancellationToken)
    {
        // 数据是被吊销的会话 ID；每个实例只断自己的连接，没有连接时什么也不做
        var sessionIds = System.Text.Json.JsonSerializer.Deserialize(delivery.Envelope.Data.Span, RealtimeJsonContext.Default.StringArray) ?? [];
        await connections.DisconnectAsync(sessionIds, cancellationToken);
        return Result.Success();
    }
}
```

```csharp
builder.Services.AddMessageHandler<SessionsRevokedHandler>("sessions.revoked", SubscriptionMode.Broadcast);
```

`RealtimeJsonContext` 是应用的源生成序列化上下文，登记了 `string[]`。广播是尽力而为：发布时不在线的实例收不到（它上线时本来也没有这些连接），失败到上限丢弃不进死信；只装了内存代理的单实例部署里，广播就是本进程收到一次。

## 常见问题

### 框架有没有 Hub 基类或者「给某个用户推消息」的服务

没有。SignalR 自带的 `IHubContext<THub>`、`Clients.User(userId)`、`Clients.Group(name)` 已经够用，`UserIdentifier` 就是内核主体的 ID。连接与用户、租户的对应关系由应用自己在 `OnConnectedAsync` 里登记（例如按租户加入分组），框架不替应用维护这张表。

### 为什么浏览器连 Hub 时 401，而 curl 带 `Authorization` 头却正常

浏览器的 WebSocket 与 EventSource 带不了自定义请求头，令牌只能放在查询串 `access_token` 里，而方案 `XiHan` 只在 `QueryTokenPaths` 登记的前缀下接受查询串令牌，并且一律按 `Bearer` 方案交给内核处理器。依次核对：Hub 的路径是否落在登记的前缀下（按路径段匹配，`/hubs` 匹配 `/hubs/notifications`，不匹配 `/hubsx`）；`access_token` 是否只出现一次；是否装配了以 `Bearer` 工作的认证 Provider（只有内存 ApiKey 处理器时，查询串令牌的方案不受支持，得到 401）。

### 令牌过期了，已建立的连接会断吗

不会自动断。方案 `XiHan` 只在握手时认证，连接建立后不再核对；Hub 方法里经 `HttpInvocation.CreateContext` 发起的内核调用会重新认证，令牌过期后这些调用被拒，Hub 可以据此断开连接。要主动吊销，用上文的广播订阅让每个实例断开自己身上的连接。

### Hub 方法里能不能直接注入仓储读数据，不经过操作

技术上可以，但会绕过认证、租户、授权与事务：Hub 方法没有调用上下文，租户过滤与审计都不会发生。读写业务数据一律经 `IOperationInvoker` 调用操作，Hub 只做连接与推送。

### 要不要用 Server-Sent Events 代替 SignalR

框架对两者一视同仁：都是绕过管道的长连接端点，认证都走方案 `XiHan`，查询串令牌规则相同。SSE 只有服务端到客户端一个方向、用 ASP.NET Core 自带的能力即可实现；需要双向、分组与自动重连时用 SignalR。

## 相关包与下一步

- 相关包：[Hosting.AspNetCore](/packages/hosting-aspnetcore)、[Messaging.Abstractions](/packages/messaging-abstractions)、[Security.Jwt](/packages/security-jwt)
- 下一页：[规范化接口文档](/web/openapi)，从同一份操作契约发布 OpenAPI 文档
- 相关章节：[端点里使用框架](/web/endpoints)、[认证](/security/authentication)、[发布与订阅](/messaging/pubsub)、[领域事件与发件箱](/messaging/domain-events)
- 规范：RFC-0008《认证与授权》的「绕过管道的端点」一节
