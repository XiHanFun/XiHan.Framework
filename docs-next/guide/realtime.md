# 实时通信

本页讲 5.0 下怎样做实时推送：直接使用 ASP.NET Core SignalR，Hub 的认证交给内核已装配的认证处理器，适用于要在 XiHan 应用里接 WebSocket、Server-Sent Events 一类长连接的项目。用到的 API 都在 [Hosting.AspNetCore 包](../packages/hosting-aspnetcore)。

## 5.0 的做法

5.0 不包装 SignalR，框架里没有 Hub 基类、连接登记表、推送抽象或背板。应用直接用 ASP.NET Core 共享框架自带的 SignalR（`AddSignalR`、`MapHub`、`IHubContext<THub>`），不需要额外的框架包。

框架只解决衔接上的两件事：

- 认证。Hub 是长连接，不经内核管道（不受截止时间约束，内核停止时也不被 503 打断），管道的 Authenticate 阶段管不到它。`Hosting.AspNetCore` 提供 ASP.NET Core 认证方案 `XiHan`，把凭据交给内核已装配的 `IAuthenticationHandler`，与管道里是同一个处理器，JWT、OpenID Connect、ApiKey 不用各接一遍（ADR-0051）。
- 在 Hub 里发起内核调用。`HttpInvocation.CreateContext(httpContext, services)` 以连接的 `HttpContext` 建调用上下文，凭据随之带上，调用经管道时照常重新认证、按契约授权。

## 接入 Hub

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

方案 `XiHan` 的规则：

| 项 | 行为 |
| --- | --- |
| 生效范围 | 只认带 `PipelineBypassMetadata` 的端点；普通请求由内核管道认证，方案对它们返回未认证，不重复认证 |
| 凭据 | 先取 `Authorization` 头；没有时，请求路径（相对 PathBase）按路径段、不区分大小写落在 `QueryTokenPaths` 的某个前缀下，且 `access_token` 只有一个非空值，才作为 `Bearer` 凭据；其他路径的查询串令牌一律忽略 |
| 路径前缀 | 不能为空也不能是 `/`，方案初始化时校验，违反时抛出 `ArgumentException` |
| 认证 | 交给方案名匹配的内核认证处理器；方案不支持、凭据无效都是 401，质询与管道一致（Bearer 带 `error="invalid_token"`） |
| 主体 | 转成认证类型为 `XiHan` 的 `ClaimsPrincipal`：声明保留原始类型名，另加 `ClaimTypes.NameIdentifier` 承载主体 ID 并作名称声明（SignalR 的 `UserIdentifier` 随之可用），角色声明类型取 `XiHan:Security:RoleClaim`（缺省 `role`） |
| 登记 | `AddXiHan` 可以多次调用，方案只登记一次、各次的配置依次生效；不设为缺省方案 |

## 会话门禁

令牌在有效期内、但对应的会话已经注销时，方案 `XiHan` 本身认不出来。应用实现 `IInvocationAuthenticationFilter` 做这类核对：内核处理器认证成功后按登记顺序调用，任一返回失败即 401。框架不判定会话，会话状态由应用提供。

```csharp
using XiHan.Framework.Hosting.AspNetCore.Authentication;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;

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

`ISessionStatus` 是应用自己的接口，示例只说明挂点的写法。

## 在 Hub 里调用操作

Hub 方法不在内核管道里，`HttpContext.GetXiHanContext()` 在这里是无值。要执行操作时以连接的 `HttpContext` 建调用上下文，再交给 `IOperationInvoker`：

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

`ListOrders`、`ListOrdersRequest`、`OrderList` 来自 `samples/SimpleApp`。上下文用 Hub 方法自己的服务作用域（构造函数注入的 `IServiceProvider`），不记入 `HttpContext.Items`。每次调用都重新认证，长连接上的令牌过期后，新的调用会被拒绝。

## 跨实例推送

`IHubContext<THub>` 只能推给连在本实例上的连接。多实例部署时：

- 推到其他实例上的连接要靠 SignalR 自己的横向扩展，如 Redis 背板或托管的 SignalR 服务；5.0 不提供背板。
- 业务事件借消息子系统跨进程送达。`messaging.queue@1` 是工作队列，每条消息只交给一个实例的处理器（至少一次），这个实例经 `IHubContext<THub>` 推送，背板再转给其他实例上的连接。事件随业务数据在同一事务里写出时，用发件箱。

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

消息处理器经完整管道执行，信封扩展属性里的租户在这里已经进入调用上下文，按租户分组推送时从 `context` 取。主题、处理器与发件箱见[消息与事件](./messaging)。

## 相关

- [Hosting.AspNetCore 包](../packages/hosting-aspnetcore)
- [宿主与 Web 应用](./web)：内核管道与绕过管道的端点
- [认证](./authentication)：内核认证处理器与 Provider
- [消息与事件](./messaging)
- 规范：RFC-0008《认证与授权》的「绕过管道的端点」一节；ADR-0051
