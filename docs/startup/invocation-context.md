# 调用上下文

这一页讲贯穿一次调用的那个对象：`InvocationContext` 自带的成员、Baggage 与五个已登记的键、内核与安全子系统附加在它上面的信息（安全上下文、结果、调用来源、类型化条目）、各协议怎样从请求构造它、以及 Baggage 跨进程传播时落在哪些载体上。读完这页，你能在操作、中间件与调用链深处的服务里正确地读写上下文，也能为自己发起的调用建出一个合格的上下文。前置知识是[执行管道](/startup/pipeline)。

## 为什么需要

一次业务调用需要很多「随行信息」：还剩多少时间、属于哪条链路、来自哪个租户、要用哪种语言回错误、结果是什么。如果这些信息绑在 `HttpContext` 上，同一份操作到了 gRPC、消息或命令行就要另写一套；如果塞进线程静态变量，异步切换与嵌套调用会把它弄乱。框架用一个与协议无关的 `InvocationContext` 承载它们：协议适配器在 Ingress 阶段把请求翻译进来，中间件与处理器只读这个对象，Egress 阶段再翻译回协议。什么能跨进程传播、什么只在本进程可读，规范（RFC-0003）说得清清楚楚。

## InvocationContext 的成员

`InvocationContext` 在原语层，没有任何第三方依赖：

| 成员 | 类型 | 含义 |
| --- | --- | --- |
| `CancellationToken` | `CancellationToken` | 本次调用的取消令牌，宿主取消与截止时间都并进来；异步 API 一路传递它 |
| `Deadline` | `DateTimeOffset?` | 绝对截止时间，没有则为空 |
| `IsExpired(now)`、`RemainingTime(now)` | 方法 | 按给定的「现在」判断是否已过期、还剩多久；`now` 来自容器里的 `TimeProvider` |
| `TraceId`、`SpanId`、`TraceFlags` | `string`、`string`、`ActivityTraceFlags?` | W3C Trace Context：链路 ID、本次调用的跨度 ID、采样标志 |
| `Baggage` | `IReadOnlyDictionary<string, string>` | 跨进程传播的键值，只保留已登记的键 |
| `TryGetBaggage(key, out value)` | 方法 | 读一个 Baggage 值 |
| `WithBaggage(key, value)`、`WithCancellation(token)` | 方法 | 返回带新 Baggage 或新令牌的副本；上下文本身不可变 |
| `Items` | `IDictionary<object, object?>` | 本进程内的附加数据，中间件之间传值用；不传播 |
| `Services` | `IServiceProvider` | 本次调用的作用域服务提供器，见[依赖注入](/startup/dependency-injection) |

`TraceId`、`Deadline`、`Baggage` 可以传播到下游进程；`Items` 与 `Services` 只在本进程、本次调用内有意义。

## Baggage 与已登记的键

Baggage 是 W3C 格式的键值对，但框架只传播已登记的键，未登记的键在入站时丢弃。已登记的键在原语层的 `BaggageKeys` 里：

| 常量 | 键 | 含义 | 谁写入 |
| --- | --- | --- | --- |
| `BaggageKeys.Tenant` | `xihan.tenant` | 调用方声明的租户 | 命令行选项 `--tenant`、消息扩展 `xihantenant`；HTTP 入站不收，租户只来自认证与 Tenant 阶段 |
| `BaggageKeys.Principal` | `xihan.principal` | 发起者的主体 ID，只是标识不是凭据 | 消息扩展 `xihanprincipal`；HTTP 入站不收 |
| `BaggageKeys.Correlation` | `xihan.correlation` | 业务关联 ID，用来把一次业务操作引发的多次调用串起来 | HTTP `baggage` 头、命令行 `--correlation`、消息扩展 `xihancorrelation`、作业派发（运行 ID） |
| `BaggageKeys.IdempotencyKey` | `xihan.idempotency-key` | 幂等键，Idempotency 阶段据此去重 | HTTP `baggage` 头；消息派发不写它 |
| `BaggageKeys.Locale` | `xihan.locale` | 调用方区域，失败文案与 `key.L()` 据此翻译 | HTTP `baggage` 头或 `Accept-Language`、命令行 `--locale`、消息扩展 `xihanlocale` |

`BaggageKeys.Registered` 列出全部已登记的键，`IsRegistered(key)` 与 `IsWellFormed(key)` 供自定义载体校验。HTTP 入站不接受 `xihan.tenant` 与 `xihan.principal`，是因为任何客户端都能写请求头，租户与主体必须由凭据推出；消息派发接受它们，是因为消息的发送方是经过授权的服务，审计把 `xihan.principal` 记为发起者。

## 内核附加的信息

内核与安全子系统用扩展方法在上下文上读写结构化数据，不直接操作 `Items`：

| 扩展方法 | 命名空间 | 含义 |
| --- | --- | --- |
| `GetSecurity()` | `XiHan.Framework.Kernel.Security` | 安全上下文 `SecurityContext`：`Principal`（`Id`、`IsAuthenticated`、`AuthenticationScheme`、`Claims`、`DisplayName`）与 `Tenant`（`TenantContext`：`Id`、`DisplayName`，可空）。Authenticate 与 Tenant 阶段之前读到的是 `SecurityContext.Anonymous` |
| `SetSecurity(security)`、`SetTenant(tenant)` | 同上 | 由 Authenticate 与 Tenant 阶段写入，之后的阶段不应再改 |
| `SetOutcome(result)`、`SetOutcomeIfMissing(result)`、`FindOutcome()` | `XiHan.Framework.Kernel.Pipeline` | 本次调用的结果：中间件短路或处理器完成时写入，宿主在 Egress 读出翻译成协议；`FindOutcome()` 返回 `Maybe<Result>`，没写过时无值，`App.InvokeAsync` 把无值当成功 |
| `FindOrigin()`、`SetOrigin(origin)` | 同上 | 调用来源 `InvocationOrigin`，见下文；`FindOrigin()` 返回 `Maybe<InvocationOrigin>` |
| `Get<T>(key)`、`Set<T>(key, value)` | 同上 | 用 `ContextItemKey<T>` 在 `Items` 里存取类型化的值，`Get` 返回 `Maybe<T>` |

## 最小示例

一个操作读取追踪 ID、剩余时间、主体与关联 ID，并把它们回显；一个中间件用类型化条目把「请求进入时刻」交给下游。

```csharp
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace Shop.Diagnostics;

public static class ContextItems
{
    // 类型化条目的键：中间件写、处理器读，不出本进程
    public static readonly ContextItemKey<DateTimeOffset> EnteredAt = new("shop.diagnostics.entered_at");
}

public sealed class StampMiddleware(TimeProvider clock) : IPipelineMiddleware
{
    public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        context.Set(ContextItems.EnteredAt, clock.GetUtcNow());
        return nextStep(context);
    }
}

[Operation(Id, Stability = Stability.Preview, Expose = Expose.Http | Expose.CommandLine)]
public sealed class WhoAmI(TimeProvider clock) : IOperation<WhoAmIRequest, WhoAmIResponse>
{
    public const string Id = "diagnostics.whoami";

    public ValueTask<Result<WhoAmIResponse>> HandleAsync(WhoAmIRequest request, InvocationContext context)
    {
        var now = clock.GetUtcNow();
        var security = context.GetSecurity();
        var correlation = context.TryGetBaggage(BaggageKeys.Correlation, out var value) ? value : null;
        var enteredAt = context.Find(ContextItems.EnteredAt);

        return ValueTask.FromResult(Result.Success(new WhoAmIResponse(
            context.TraceId,
            security.Principal.Id,
            security.Principal.IsAuthenticated,
            security.Tenant?.Id,
            correlation,
            context.RemainingTime(now)?.TotalMilliseconds,
            enteredAt.HasValue ? (now - enteredAt.ValueOrThrow()).TotalMilliseconds : null)));
    }
}

public sealed record WhoAmIRequest();

public sealed record WhoAmIResponse(
    string TraceId,
    string? Principal,
    bool IsAuthenticated,
    string? Tenant,
    string? Correlation,
    double? RemainingMs,
    double? ElapsedMs);
```

特性里登记中间件与操作：`context.AddMiddleware<StampMiddleware>(PipelineStage.Observe)` 与 `context.AddGeneratedOperations()`；请求与响应类型登记进项目的 `JsonSerializerContext`。`whoami` 不是标准动词、请求也没有 `id`，HTTP 适配器把它映射成 `POST /diagnostics:whoami`，映射规则见 [HTTP 路由与载荷](/web/routing)。运行后：

```bash
curl -s -X POST localhost:5000/diagnostics:whoami \
  -H "Content-Type: application/json" \
  -H "baggage: xihan.correlation=order-42" \
  -d '{}'
```

```json
{"traceId":"4bf92f3577b34da6a3ce929d0e0e4736","principal":"anonymous","isAuthenticated":false,"tenant":null,"correlation":"order-42","remainingMs":null,"elapsedMs":0.4}
```

再加一个 `xihan-deadline: <RFC 3339 时刻>` 请求头，`remainingMs` 就是到该时刻的剩余毫秒数。同一份操作在命令行宿主上是 `app diagnostics whoami --correlation order-42 --timeout 30`，输出同样的字段，`remainingMs` 接近 30000，`traceId` 每次调用新生成。

## 各协议怎样构造上下文

Ingress 阶段由协议适配器或派发器完成，各自把载体上的信息翻译进上下文：

| 来源 | 入口 | 追踪与截止时间 | Baggage | 调用来源 |
| --- | --- | --- | --- | --- |
| HTTP | `HttpInvocation.CreateContext(httpContext)`，`UseXiHan()` 内部调用；端点里用 `httpContext.FindXiHanContext()` 取已建好的 | `traceparent` 头；`xihan-deadline`（RFC 3339）或 `grpc-timeout` | `baggage` 头，只收 `xihan.correlation`、`xihan.idempotency-key`、`xihan.locale`；`Accept-Language` 补 `xihan.locale` | 协议 `http`，目标是方法加路径，带远端地址、`User-Agent`、`Referer`、路由模板与端点名 |
| 命令行 | `CommandLineInvocation.Create(commandContext, operationId)`，`invoke` 命令内部调用；自定义命令发起多次调用时用 `Create(context, services, operationId)` 各建一个作用域 | 每次新生成 trace-id；`--timeout <秒>` | `--tenant`、`--correlation`、`--locale` | 协议 `cli`，目标是操作 ID，没有远端地址 |
| 消息 | `MessageDispatcher` 为每条消息开新作用域 | 信封的 `traceparent` 扩展 | `xihantenant`、`xihanprincipal`、`xihancorrelation`、`xihanlocale` 四个扩展进对应的键；不写幂等键 | 协议 `messaging`，目标是主题 |
| 作业 | `JobDispatcher` 为每次运行开新作用域 | 新链路 | `xihan.correlation` 是运行 ID | 协议 `job`，目标是作业名 |
| gRPC、MCP、工作流 | 对应的协议适配器与引擎 | 见各协议页 | 见各协议页 | 协议 `grpc`、`mcp`、`workflow` |
| 测试 | `TestHost.InvokeAsync(handler, deadline, baggage)`；不需要宿主时 `TestInvocationContext.Create(...)` | 按参数 | 按参数 | 无 |

自己发起的调用（后台服务、定时任务）用静态方法 `App.CreateContext` 建上下文，再交给 `App.InvokeAsync`，这样认证、租户、事务等中间件照常生效：

```csharp
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace Shop.Diagnostics;

public sealed class NightlyReconciler(App app, IServiceScopeFactory scopes)
{
    public async Task RunAsync(string runId, CancellationToken cancellationToken)
    {
        // 一次调用一个作用域：上下文的 Services 就是它
        await using var scope = scopes.CreateAsyncScope();
        var context = App.CreateContext(
            scope.ServiceProvider,
            new InvocationContextOptions
            {
                Deadline = DateTimeOffset.UtcNow.AddMinutes(10),
                Baggage = new Dictionary<string, string> { [BaggageKeys.Correlation] = runId },
            },
            cancellationToken);

        // 处理器作为 Handle 阶段执行；不写结果时视为成功
        var result = await app.InvokeAsync(context, static ctx =>
        {
            ctx.SetOutcome(Result.Success());
            return ValueTask.CompletedTask;
        });
    }
}
```

`CreateContext(services, cancellationToken)` 建一个不带载体的上下文；要带截止时间、Baggage 或上游的追踪时传 `InvocationContextOptions`（`TraceId`、`SpanId`、`TraceFlags`、`Deadline`、`Baggage`，都可省略），令牌总在最后。不给 `TraceId` 时并入当前的 Activity，也没有时新开一条链路。调度子系统的作业已经由 `JobDispatcher` 这样做了，优先用[作业与计划](/scheduling/jobs)而不是自己写循环。

## 调用来源

协议适配器与派发器在建上下文时写入一次调用来源 `InvocationOrigin`，之后只读、不跨进程传播：

| `InvocationOrigin` 成员 | 含义 |
| --- | --- |
| `Protocol` | `InvocationProtocols` 的常量：`Http`（`http`）、`Grpc`（`grpc`）、`Mcp`（`mcp`）、`CommandLine`（`cli`）、`Messaging`（`messaging`）、`Job`（`job`）、`Workflow`（`workflow`） |
| `Target` | HTTP 为方法与路径（不含查询串），gRPC 为方法全名，MCP 为工具名，命令行为操作 ID，消息为主题，作业为作业名，工作流为操作名 |
| `RemoteAddress` | 客户端 IP，不含端口；经反向代理时取 `UseForwardedHeaders()` 改写后的值，没有网络对端时为空 |
| `UserAgent`、`Referer` | 对应的请求头，没有时为空 |
| `Route`、`EndpointName` | 匹配到的端点的路由模板与 `WithName` 名字，没有匹配到端点或协议没有端点时为空 |

缓存回源、存储维护这类内部调用没有来源，`FindOrigin()` 返回无值。审计事件本身不带来源字段，需要远端地址或 User-Agent 时由应用写进细节，下面的中间件放在 Observe 阶段开头，被认证或授权拒绝的调用也能带上来源：

```csharp
using XiHan.Framework.Auditing.Abstractions.Pipeline;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace Shop.Diagnostics;

public sealed class AuditOriginMiddleware : IPipelineMiddleware
{
    public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        if (context.FindOrigin().TryGetValue(out var origin))
        {
            context.AddAuditDetail("protocol", origin.Protocol);
            context.AddAuditDetail("target", origin.Target);
            context.AddAuditDetail("remoteAddress", origin.RemoteAddress);
            context.AddAuditDetail("userAgent", origin.UserAgent);
        }

        return nextStep(context);
    }
}
```

`AddAuditDetail` 来自审计抽象包，见[审计](/common/auditing)。

## 调用链深处取上下文

操作与中间件从参数拿上下文；端点里用 `httpContext.FindXiHanContext()`（返回 `Maybe<InvocationContext>`，请求没有经过 `UseXiHan()` 时无值）。调用链深处拿不到任何参数的服务注入作用域服务 `IInvocationAccessor`，它的 `Current` 是 `Maybe<InvocationContext>`：

```csharp
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;

namespace Shop.Diagnostics;

public sealed class CurrentShopper(IInvocationAccessor accessor)
{
    public string? Id => accessor.Current.TryGetValue(out var context)
        ? context.GetSecurity().Principal.Id
        : null;
}
```

内核在调用进入管道前设置 `Current`，离开时恢复上一层；同一作用域内的嵌套调用各自看到自己的上下文。不在调用内（后台任务、作用域刚创建、调用已结束）时为无值，调用方要处理这种情况而不是 `ValueOrThrow()`。

## Baggage 的传播载体

| 载体 | 入站 | 出站 |
| --- | --- | --- |
| HTTP | `baggage` 头，W3C 格式，值按 URL 解码 | 调用下游 HTTP 服务时用 `HttpInvocation.FormatBaggage(context.Baggage)` 生成头值；`traceparent` 由 .NET 的 `HttpClient` 按当前 Activity 自动写 |
| CloudEvents 消息 | 扩展 `xihantenant`、`xihanprincipal`、`xihancorrelation`、`xihanlocale` | 在调用内发布时发布者自动补齐：租户与主体优先取安全上下文，其次取 Baggage；`traceparent` 取当前 Activity，见[发布与订阅](/messaging/pubsub) |
| 命令行 | `--tenant`、`--correlation`、`--locale` 选项 | 进程是链路起点，没有出站 |
| 作业 | 运行 ID 写进 `xihan.correlation` | 作业里发布的消息按消息载体传播 |

不传播的东西也要记住：`Items`、`Services`、调用来源、安全上下文都只在本进程有效。认证中间件不把主体回写到 Baggage，Baggage 是跨进程传播的载体，不是进程内状态的副本；消息派发之所以带 `xihan.principal`，是因为消费端没有凭据可以重新认证，只能记录发起者。

## 常见问题

### 为什么我在 HTTP 请求头里放了 `xihan.tenant`，操作里读不到

HTTP 入站只接受 `xihan.correlation`、`xihan.idempotency-key`、`xihan.locale` 三个键，其余 `xihan.` 键丢弃。租户来自认证后的主体与 Tenant 阶段的解析规则（请求头、域名、路径等由多租户子系统决定），见[租户解析与隔离](/multi-tenancy/resolution)。

### `Deadline` 与 `CancellationToken` 是什么关系

`Deadline` 是绝对时刻，`DeadlineMiddleware` 在 Resilience 阶段据此派生一个在截止时刻触发的令牌并并入 `CancellationToken`。处理器只需要传递 `CancellationToken`；要知道「还剩多久」才读 `Deadline` 或 `RemainingTime(now)`。超时得到 `KERNEL_TIMEOUT_001`，外部取消得到 `KERNEL_CANCELLED_001`。

### 我能在中间件里改 Baggage 吗

上下文不可变，`WithBaggage` 返回的是副本，而管道中的下一步收到的仍是原来的对象。Baggage 在 Ingress 阶段定型，中间件之间传值用 `Items`（配合 `ContextItemKey<T>`），要影响下游进程就在出站载体上显式写。

### `SetOutcome` 写了两次会怎样

后写的覆盖先写的，所以规则是「谁短路谁写，处理器之后不要改写别的阶段写的结果」。只想在没有结果时补一个时用 `SetOutcomeIfMissing`。

### 处理器既没有 `SetOutcome` 也没有抛异常，调用结果是什么

成功。`App.InvokeAsync` 把无值的结果当作 `Result.Success()`，操作契约的处理器由内核把返回的 `Result<TResponse>` 写进去，不需要手动调用。

## 相关包与下一步

- 包参考：[Primitives](/packages/primitives)（`InvocationContext`、`BaggageKeys`）、[Kernel](/packages/kernel)（扩展方法、`InvocationOrigin`、`IInvocationAccessor`）、[Hosting.AspNetCore](/packages/hosting-aspnetcore)、[Hosting.CommandLine](/packages/hosting-commandline)
- 下一章：[配置模型与选项](/configuration/options)，看 `XiHan:*` 配置节怎样绑定到选项
- 延伸：[审计](/common/auditing)、[日志与可观测性](/common/observability)、[全球化和本地化](/common/localization)
