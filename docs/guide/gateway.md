# 网关与流量治理

灰度发布要按人群、租户、百分比把一部分流量导到新逻辑上，同时希望每条请求可追踪、出错有统一响应格式。这章讲这套东西怎么装、决策怎么算出来、以及哪些开关目前只是声明位。

## 两个包，各管一段

| 包 | 职责 | 有没有 HTTP 依赖 |
| --- | --- | --- |
| `XiHan.Framework.Traffic` | 灰度规则模型、匹配器、规则引擎、规则仓储；限流/熔断的策略接口 | 无，纯决策 |
| `XiHan.Framework.Web.Gateway` | 把决策接进 ASP.NET Core 请求管道：构建上下文、写决策、追踪、异常兜底 | 有 |

分工是「Traffic 算，Gateway 接」。灰度规则怎么写、怎么匹配全在 Traffic；Gateway 只负责从 `HttpContext` 里凑出 `GrayContext`、调一次引擎、把结果塞回 `HttpContext.Items`。

::: warning 它不是反向代理
`GrayRoutingMiddleware` **不转发请求**，也不做服务发现和负载均衡。它只回答「这条请求该走哪个版本」，写进 `HttpContext.Items`，之后走哪套代码由你自己的中间件或控制器决定。需要边缘代理仍然用独立的网关组件。
:::

## 安装与启用

```bash
dotnet add package XiHan.Framework.Web.Gateway
```

```csharp
[DependsOn(typeof(XiHanWebGatewayModule))]
public class MyAppModule : XiHanModule { }
```

`XiHanWebGatewayModule.ConfigureServices` 只做一件事：调用 Traffic 提供的 `AddGrayRouting()`，注册引擎、内存仓储和五个内置匹配器。**中间件不会自动挂载**，要自己挂：

```csharp
public override void OnPreApplicationInitialization(ApplicationInitializationContext context)
{
    context.GetApplicationBuilder().UseGateway();
}
```

`UseGateway()` 按固定顺序挂三段：

```text
① GatewayExceptionMiddleware   try/catch 兜底（必须最外层）
② RequestTracingMiddleware     TraceId 注入 + 请求起止日志
③ GrayRoutingMiddleware        构建上下文 → 引擎决策 → 写 HttpContext.Items
```

也可以只挂需要的那段：`UseGrayRouting()` 或 `UseRequestTracing()`。

::: tip 已经用了 Web.Api 就别挂满三段
`XiHanWebApiModule` 的管道里已经有 `XiHanTraceIdMiddleware`（同样从 W3C `Activity` 取 TraceId、同样写 `X-Trace-Id` 响应头）和全局异常处理，这时通常只需要 `app.UseGrayRouting()` 一段。

两个追踪中间件写的是**不同的 `Items` 键**——网关写 `GatewayConstants.TraceIdKey`（`"XiHan.Gateway.TraceId"`），Web.Api 写 `"__XiHanTraceId"`。`HttpContext.GetTraceId()` 读的是前者，没挂 `RequestTracingMiddleware` 就会返回 `null`。
:::

::: warning 挂载位置决定客户端 IP 准不准
框架的模块生命周期是「所有模块的 `OnPreApplicationInitialization` → 所有模块的 `OnApplicationInitialization`」。Web.Api 的整条管道（含最前面的 `UseForwardedHeaders()` 和最后的 `UseEndpoints()`）建在 `OnApplicationInitialization`，所以：

- 挂在 `OnPreApplicationInitialization` → 网关中间件排在 `UseForwardedHeaders()` **之前**，`GrayContext.ClientIpAddress` 取到的是反向代理的 IP，`IpAddress` 类型的规则会失准。
- 挂在你自己模块的 `OnApplicationInitialization` → 排在 `UseEndpoints()` 之后，永远不会执行。

反向代理后面又要按 IP 灰度，就别用 `UseGateway()` 统一挂，改成在 Web.Api 管道内部自己 `app.UseMiddleware<GrayRoutingMiddleware>()`，或在匹配器里改从转发头取 IP。
:::

`AddGateway(configure)` 只是把你传入的委托 `Configure` 进 `XiHanGatewayOptions`，不注册任何服务——引擎来自模块里的 `AddGrayRouting()`，不调 `AddGateway` 也能跑。

## 定义灰度规则

一条规则就是一个 `GrayRule`：

```csharp
var rule = new GrayRule
{
    RuleId = "gray-order-v2-whitelist",
    RuleName = "订单 v2 内测白名单",
    RuleType = GrayRuleType.UserId,
    IsEnabled = true,
    Priority = 10,                       // 数字越小越先匹配
    TargetVersion = "v2",
    TargetServiceId = "order-service",
    Configuration = """{"userIds":[1001,1002]}""",
    EffectiveTime = DateTime.UtcNow,
    ExpiryTime = DateTime.UtcNow.AddDays(7),
    CreatedTime = DateTime.UtcNow
};
```

`Configuration` 是 JSON 字符串，结构随 `RuleType` 变：

| `GrayRuleType` | `Configuration` | 命中条件 | 需要的上下文 |
| --- | --- | --- | --- |
| `Percentage = 1` | `{"percentage":20}` | `Random.Shared.Next(1, 101)` 落在 `<= percentage` | 无 |
| `UserId = 2` | `{"userIds":[1001,1002]}` | 上下文 `UserId` 在列表里 | `GrayContext.UserId` |
| `TenantId = 3` | `{"tenantIds":[1,2]}` | 上下文 `TenantId` 在列表里 | `GrayContext.TenantId` |
| `Header = 4` | `{"headerName":"X-Gray","headerValue":"true"}` | 请求头存在且值相等（忽略大小写）；省略 `headerValue` 则只要头存在就命中 | `GrayContext.Headers` |
| `IpAddress = 5` | `{"ipAddresses":["10.0.0.7","192.168.1.0/24"]}` | 精确 IP 相等，或落在 CIDR 网段内 | `GrayContext.ClientIpAddress` |
| `Custom = 99` | 自定 | 由你实现的 `IGrayMatcher` 决定 | 自定 |

::: danger userIds / tenantIds 必须写数字
两个匹配器把它们反序列化成 `List<long>`。写成 `["admin","tester"]` 会抛 `JsonException`，被匹配器内部的 `catch` 吞掉后直接返回「不命中」——**不报错、不打日志、永远不生效**。上下文里的 `UserId` 同理是 `long?`，由 `long.TryParse` 解析而来，非数字用户标识拿不到。
:::

上下文由 `GrayRoutingMiddleware` 从请求里凑：

| `GrayContext` 字段 | 来源 |
| --- | --- |
| `RequestPath` / `RequestMethod` | `HttpContext.Request` |
| `ClientIpAddress` | `HttpContext.Connection.RemoteIpAddress` |
| `UserId` | Claim `sub` → Claim `userId` → 请求头 `X-User-Id`，逐个回退后 `long.TryParse` |
| `TenantId` | `ICurrentTenant.Id` |
| `Headers` | 全部请求头，键不区分大小写 |

## 优先级与决策顺序

`DefaultGrayRuleEngine.DecideAsync` 的流程：

```text
取 IGrayRuleRepository.GetEnabledRulesAsync()   ← 只取 IsEnabled = true 的
  ↓ 一条都没有
返回 NotGray("没有启用的灰度规则")
  ↓ 有规则
按 Priority 升序排序（数字小的先看）
  ↓ 逐条
校验有效期：EffectiveTime / ExpiryTime 对比 DateTime.UtcNow，失效跳过
  ↓
按 rule.RuleType 找匹配器，找不到打 Warning 跳过
  ↓
IsMatchAsync 命中 → 立即短路返回 Gray(TargetVersion ?? "gray", RuleId, Reason)
  ↓ 全不命中
返回 NotGray("未命中任何灰度规则")
```

四条要记住的语义：

- **`Priority` 越小越优先，命中即短路**。把「必须稳定进新版」的规则（内部白名单、指定租户）排在百分比规则前面。
- **有效期比的是 UTC**。`EffectiveTime` / `ExpiryTime` 存本地时间会整体偏移。
- **一个 `RuleType` 只有一个匹配器生效**。引擎用 `FirstOrDefault(m => m.RuleType == rule.RuleType)` 找匹配器，同类型注册多个时只有最先注册的那个被调用——想按多个自定义维度分流，需要各自实现后在一个 `Custom` 匹配器里分派，或者复用 `Header` 类型。
- **决策异常一律降级为「未命中」**。引擎最外层的 `catch` 把异常写日志后返回 `NotGray($"决策异常: {ex.Message}")`，规则坏了不会阻断主流程，但也不会向上报错，得看日志。

典型的放量节奏就是几条规则的优先级排布：

| 阶段 | 规则类型 | `Priority` | `Configuration` |
| --- | --- | --- | --- |
| 内测 | `UserId` | 10 | `{"userIds":[1001,1002]}` |
| 内网验证 | `IpAddress` | 20 | `{"ipAddresses":["10.0.0.0/8"]}` |
| 小流量 | `Percentage` | 100 | `{"percentage":5}` |
| 放量 | `Percentage` | 100 | `{"percentage":20}` |

白名单规则的 `Priority` 小于百分比规则，测试账号才能每次都稳定落到新版本。

## 消费决策

决策写在 `HttpContext.Items[GatewayConstants.GrayDecisionKey]`，用 `GatewayContextHelper` 的扩展方法读：

```csharp
[HttpGet]
public IActionResult GetOrders()
{
    if (HttpContext.IsGrayRequest())
    {
        var version = HttpContext.GetTargetVersion();   // "v2"
        return Ok(_v2Service.Query());
    }

    return Ok(_v1Service.Query());
}
```

要拿完整决策（命中的规则 Id、原因）：

```csharp
var decision = HttpContext.GetGrayDecision();           // IGrayDecision?
if (decision?.IsGray == true)
{
    _logger.LogInformation("灰度命中 {RuleId}: {Reason}", decision.MatchedRuleId, decision.Reason);
}
```

`IGrayDecision.TargetServiceId` 恒为 `null`：`GrayDecision.Gray()` 只写 `TargetVersion` / `MatchedRuleId` / `Reason`，规则上配的 `TargetServiceId` 不会被带进决策。要按目标服务分流，拿 `MatchedRuleId` 去 `IGrayRuleRepository.GetRuleByIdAsync()` 回查规则。

想让下游或前端也看见版本，自己挂一段中间件把它写进响应头：

```csharp
public class GrayVersionHeaderMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var decision = context.GetGrayDecision();
        if (decision?.IsGray == true)
        {
            context.Response.Headers[GatewayConstants.Headers.GrayVersion] = decision.TargetVersion ?? "gray";
        }

        await next(context);
    }
}
```

`GatewayConstants.Headers` 里定义了 `X-Trace-Id` / `X-Gray-Version` / `X-User-Id` / `X-Tenant-Id` 四个头名，其中只有 `X-Trace-Id` 由框架主动写入，另外三个是约定给你用的常量。

## 生产化：换掉内存规则仓储

默认的 `InMemoryGrayRuleRepository` 是单例 `ConcurrentDictionary`，源码注释写明「仅用于演示和测试」：规则重启即丢、多实例各存各的、没有任何管理入口（`AddRule` / `RemoveRule` / `Clear` 不在 `IGrayRuleRepository` 接口上）。

生产要实现三个只读方法后替换：

```csharp
public class DbGrayRuleRepository : IGrayRuleRepository
{
    public Task<List<IGrayRule>> GetEnabledRulesAsync(CancellationToken cancellationToken = default) { … }

    public Task<IGrayRule?> GetRuleByIdAsync(string ruleId, CancellationToken cancellationToken = default) { … }

    public Task RefreshAsync(CancellationToken cancellationToken = default) { … }
}
```

```csharp
public override void ConfigureServices(ServiceConfigurationContext context)
{
    context.Services.ReplaceGrayRuleRepository<DbGrayRuleRepository>();
}
```

::: warning GetEnabledRulesAsync 是每请求调用一次的
`GrayRoutingMiddleware` 每条请求都会走一次引擎，引擎每次都问仓储要全部启用规则。数据库实现**必须自己带缓存**，否则每个请求一次查库。`RefreshAsync` 是留给你在规则变更后主动刷缓存的钩子——框架自身不会调它，得由你的规则管理入口（写完规则后）触发。

仓储用 `TryAddSingleton` 注册，替换实现也是单例；里面若要用 Scoped 的仓储/DbContext，从 `IServiceScopeFactory` 开作用域。
:::

新增匹配维度就实现 `IGrayMatcher`：

```csharp
public class DeviceTypeGrayMatcher : IGrayMatcher
{
    public GrayRuleType RuleType => GrayRuleType.Custom;

    public bool IsMatch(GrayContext context, IGrayRule rule)
    {
        return context.Headers is not null
            && context.Headers.TryGetValue("X-Device-Type", out var deviceType)
            && deviceType.Equals("mobile", StringComparison.OrdinalIgnoreCase);
    }

    public Task<bool> IsMatchAsync(GrayContext context, IGrayRule rule, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(IsMatch(context, rule));
    }
}
```

```csharp
context.Services.AddGrayMatcher<DeviceTypeGrayMatcher>();
```

匹配器是单例，别在里面存请求级状态。

## 请求追踪

`RequestTracingMiddleware` 做三件事：定 TraceId、写出去、记两条日志。

TraceId 的取值优先级：

| 顺序 | 来源 | 说明 |
| --- | --- | --- |
| 1 | `Activity.Current.TraceId`（W3C，32 位十六进制） | 与 Web.Api、可观测性同源；上游传了 `traceparent` 会自动延续 |
| 2 | 请求头 `X-Trace-Id` | 没有 Activity 时才轮到 |
| 3 | `HttpContext.TraceIdentifier` | 兜底 |

定好后同时写进 `HttpContext.Items[GatewayConstants.TraceIdKey]` 和响应头 `X-Trace-Id`，业务代码用 `HttpContext.GetTraceId()` 读。

::: tip 每条请求两条 Information 日志
「请求开始」和「请求结束」（含状态码与耗时毫秒）都是 `Information` 级，且结束那条写在 `finally` 里、异常也会记。QPS 高的服务要么按类别 `XiHan.Framework.Web.Gateway.Middlewares.RequestTracingMiddleware` 调日志级别过滤，要么别挂这一段、直接用 Web.Api 的请求日志。
:::

## 网关异常响应

`GatewayExceptionMiddleware` 捕获后续管道的未处理异常，按类型映射状态码，以 camelCase JSON 返回 `GatewayErrorResponse`：

| 异常类型 | 状态码 |
| --- | --- |
| `UnauthorizedAccessException` | 401 |
| `ArgumentException`（含派生类） | 400 |
| 其余 | 500 |

```json
{
  "traceId": "8a3f…",
  "errorCode": "GATEWAY_ERROR",
  "errorMessage": "……",
  "path": "/api/orders",
  "timestamp": "2026-08-05T02:11:34.000Z"
}
```

`errorCode` 恒为 `GATEWAY_ERROR`，`details` 字段模型里有但中间件不填。

::: danger 异常消息原样返回给调用方
`errorMessage` 直接取 `exception.Message`，可能带连接串、SQL 片段、内部路径。对公网暴露前要么在它外面再包一层自己的异常中间件改写响应，要么干脆不挂这一段，用 Web.Api 的统一异常处理。

另外它不检查 `Response.HasStarted`——响应已经开始写之后再抛异常，这里设置状态码会二次抛出。流式接口（下载、SSE）挂在它后面时要注意。
:::

## 限流与熔断当前在哪

::: warning Traffic 的限流/熔断只有接口
`IRateLimitPolicy`（`PolicyName` / `IsAllowedAsync(key, ct)`）和 `ICircuitBreakerPolicy`（`PolicyName` / `IsOpen(key)` / `RecordSuccess(key)` / `RecordFailure(key)`）在 Traffic 包里是**纯接口，没有任何内置实现，模块也不注册它们**。`UseGateway()` 挂的三段中间件里同样没有限流和熔断。

真正能用的入站限流与熔断在 `XiHan.Framework.Web.Api` 的管道里（路由后、鉴权前），配置开关分别是 `XiHan:Web:RateLimiting:IsEnabled` 与 `XiHan:Web:CircuitBreaking:IsEnabled`，默认都关。见 [Web 应用开发](./web)。
:::

## 配置

::: danger XiHan:Web:Gateway 配置节当前不生效
`XiHanGatewayOptions.SectionName` 定义了 `"XiHan:Web:Gateway"`，但**没有任何代码把这个配置节绑定到选项**——`AddGateway(configure)` 只应用你传进去的委托。更关键的是，三段中间件都不注入 `IOptions<XiHanGatewayOptions>`，所以下表所有字段目前**没有消费方**，改了不会有任何效果。
:::

| 字段 | 默认值 | 想达到同样效果，实际该用 |
| --- | --- | --- |
| `EnableGrayRouting` | `true` | 挂或不挂 `UseGrayRouting()` |
| `EnableRequestTracing` | `true` | 挂或不挂 `UseRequestTracing()` |
| `EnableRateLimiting` | `false` | `XiHan:Web:RateLimiting:IsEnabled`（Web.Api） |
| `EnableCircuitBreaker` | `false` | `XiHan:Web:CircuitBreaking:IsEnabled`（Web.Api） |
| `RequestTimeoutSeconds` | `30` | 宿主/代理层的超时设置 |
| `AllowedOrigins` | `[]` | `XiHan:Web:Api:Cors`（Web.Api 的 `XiHanCorsOptions`） |
| `GlobalHeaders` | `{}` | 自己写一段中间件往 `Response.Headers` 里塞 |

换句话说，当前这一层的「开关」就是**挂不挂对应的中间件**，粒度靠 `UseGrayRouting()` / `UseRequestTracing()` 分别控制。

## 常见问题

| 现象 | 原因 |
| --- | --- |
| 决策永远是「没有启用的灰度规则」 | 用的还是默认内存仓储，里面一条规则都没有 |
| 规则配好了但从不命中，也不报错 | `Configuration` 的 JSON 结构或类型不对（`userIds` / `tenantIds` 必须是数字），匹配器把异常吞了 |
| 日志里出现「找不到规则类型 X 的匹配器」 | 用了 `Custom` 但没 `AddGrayMatcher<T>()` 注册对应匹配器 |
| 同一个用户一会儿新版一会儿旧版 | 百分比匹配器每次请求独立取随机数，本来就不粘；要稳定分流自己写按用户哈希的匹配器 |
| 多实例之间灰度比例/名单对不上 | 内存仓储各进程独立，换成共享的数据库仓储 |
| 规则到点没失效 | `EffectiveTime` / `ExpiryTime` 与 `DateTime.UtcNow` 比较，存了本地时间会偏 |
| `HttpContext.GetTraceId()` 返回 `null` | 没挂 `RequestTracingMiddleware`；Web.Api 的 TraceId 在另一个 `Items` 键上 |
| 改了 `appsettings` 里的 `XiHan:Web:Gateway` 完全没反应 | 该配置节没有绑定代码，见上一节 |
| 按 IP 的规则在生产上全不命中 | 网关中间件排在 `UseForwardedHeaders()` 之前，取到的是代理 IP |
| 决策出来了但请求没被分流 | 网关不转发，只写决策，分流逻辑要你自己写 |
| 自定义匹配器没被调用 | 同一 `RuleType` 已有匹配器先注册，引擎只取第一个 |

## 下一步

- [Web 应用开发](./web)：Web.Api 的完整管道、限流与熔断的真实位置
- [多租户](./multi-tenancy)：`ICurrentTenant` 从哪来，按租户灰度的前提
- [Traffic 包](../packages/traffic)：规则模型、匹配器与引擎的完整 API
- [Web.Gateway 包](../packages/web-gateway)：中间件、常量与选项的完整清单
- [可观测性](../packages/observability)：把灰度命中与 TraceId 接进指标和链路
