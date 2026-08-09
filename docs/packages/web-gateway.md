# XiHan.Framework.Web.Gateway

> API 网关薄接入层：在流量入口做灰度路由决策、请求追踪与网关级异常处理；限流/熔断为配置开关。灰度规则引擎的真源在 Traffic 模块。

- **NuGet**：`XiHan.Framework.Web.Gateway`
- **模块类**：`XiHanWebGatewayModule`
- **所在层**：Web 层
- **关键依赖**：无第三方库；仅 .NET 原生 + 框架内部依赖（[Traffic](./traffic) 提供灰度规则引擎）

## 概述

XiHan.Framework.Web.Gateway 是放在流量入口的一层薄治理组件，职责是「路由决策 + 策略执行」——为进入的请求构建灰度上下文、调用灰度规则引擎做决策，把结果注入 `HttpContext.Items` 供后续中间件按决策转发；同时提供网关级异常处理与请求追踪。真正的灰度规则引擎（`IGrayRuleEngine`）、匹配器、规则仓储等能力沉淀在 [Traffic](./traffic) 模块，本包只是它在 Web 请求管道中的接入层。本包不负责业务逻辑，也不管理规则本身，只在管道里执行决策。

## 何时使用

- 需要在入口做**灰度发布 / 灰度路由**：按用户、租户、百分比、Header 等规则把请求分流到不同版本或分组。
- 需要网关级的统一异常响应（`GatewayErrorResponse`）与请求追踪（`X-Trace-Id`）。
- 需要在流量入口集中控制超时、CORS 来源、全局 Header 等入口策略（经 `XiHanGatewayOptions`）。
- 不该用它的场景：灰度**规则/匹配器**的定义、注册与生命周期管理属于 [Traffic](./traffic)，不在本包。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Web.Gateway
```

```csharp
[DependsOn(typeof(XiHanWebGatewayModule))]
public class MyModule : XiHanModule { }
```

模块 `ConfigureServices` 只做一件事：调用 `AddGrayRouting()`（**由 Traffic 模块提供**）注册灰度规则引擎与相关服务。网关中间件需在应用侧显式经 `UseGateway()` 挂入管道（模块本身不自动接管道）。

## 工作原理

`UseGateway()` 按固定顺序挂三段中间件（异常处理在最外层）：

```text
请求 → GatewayExceptionMiddleware（try/catch 兜底）
     → RequestTracingMiddleware（注入 X-Trace-Id、记录耗时）
     → GrayRoutingMiddleware（构建灰度上下文 → IGrayRuleEngine.DecideAsync → 决策入 HttpContext.Items）
     → 后续中间件 / 端点（按决策自行转发，本包不转发）
```

- **灰度上下文构建**：`GrayRoutingMiddleware` 从请求提取 `RequestPath` / `RequestMethod` / `ClientIpAddress`、用户 ID（`sub` 或 `userId` Claim，或 `X-User-Id` 头）、租户 ID（`ICurrentTenant`）以及全部请求 Header，组成 `GrayContext`。
- **决策注入**：`IGrayRuleEngine.DecideAsync(grayContext, ...)` 的结果写入 `context.Items[GatewayConstants.GrayDecisionKey]`；命中灰度时记一条 `Information` 日志。后续中间件/控制器经 `GatewayContextHelper` 读取决策。
- **追踪**：`RequestTracingMiddleware` 获取 TraceId 优先级为「W3C `Activity.Current.TraceId`（与 Web.Api 同源）→ `X-Trace-Id` 请求头 → `TraceIdentifier`」，回写到响应头与 `Items`，并记录请求开始/结束与耗时。
- **异常兜底**：`GatewayExceptionMiddleware` 捕获未处理异常，按类型映射状态码（`UnauthorizedAccessException`→401、`ArgumentException`→400、其余→500），以 CamelCase JSON 返回 `GatewayErrorResponse`。

## 核心能力

- **灰度路由决策**：`GrayRoutingMiddleware` 构建灰度上下文、经 `IGrayRuleEngine` 决策并注入 `HttpContext`（不负责实际转发）。
- **请求追踪**：`RequestTracingMiddleware` 注入/透传 `X-Trace-Id`，记录请求耗时。
- **网关异常处理**：`GatewayExceptionMiddleware` 置于最外层，统一网关级错误响应。
- **上下文读取扩展**：`GatewayContextHelper` 提供 `HttpContext` 扩展方法读取追踪与决策。
- **入口策略配置**：`XiHanGatewayOptions`（配置节 `XiHan:Web:Gateway`）开关灰度/追踪/限流/熔断，配置超时、CORS 来源与全局 Header。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `XiHanWebGatewayModule` | 模块类，`ConfigureServices` 调用 `AddGrayRouting()`（Traffic 提供） |
| `UseGateway()` | 应用构建器扩展，按序挂载异常/追踪/灰度三段中间件 |
| `UseGrayRouting()` / `UseRequestTracing()` | 单独挂载灰度路由 / 请求追踪中间件 |
| `AddGateway(Action<XiHanGatewayOptions>?)` | 服务集合扩展，仅 `Configure` 选项（不注册引擎，引擎来自 `AddGrayRouting()`） |
| `GrayRoutingMiddleware` | 灰度路由决策中间件；依赖 `IGrayRuleEngine`、可选 `ICurrentTenant` |
| `RequestTracingMiddleware` | 请求追踪中间件 |
| `GatewayExceptionMiddleware` | 网关级异常处理中间件 |
| `GatewayContextHelper` | `HttpContext` 扩展：`GetTraceId()` / `GetGrayDecision()` / `IsGrayRequest()` / `GetTargetVersion()` |
| `GatewayErrorResponse` | 错误响应模型：`TraceId` / `ErrorCode` / `ErrorMessage` / `Path` / `Timestamp` / `Details` |
| `XiHanGatewayOptions` | 网关配置（见下） |
| `GatewayConstants` | `Items` 键与 Header 名常量 |

`GatewayConstants` 关键常量：`Items` 键 `TraceIdKey` / `GrayDecisionKey` / `RateLimitKey` / `CircuitBreakerKey` / `RequestContextKey`；Header 名（`GatewayConstants.Headers`）`X-Trace-Id` / `X-Gray-Version` / `X-User-Id` / `X-Tenant-Id`。

## 配置

配置节 `XiHan:Web:Gateway`（`XiHanGatewayOptions.SectionName`）：

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `EnableGrayRouting` | `bool` | `true` | 是否启用灰度路由 |
| `EnableRequestTracing` | `bool` | `true` | 是否启用请求追踪 |
| `EnableRateLimiting` | `bool` | `false` | 是否启用限流（开关位，见注意事项） |
| `EnableCircuitBreaker` | `bool` | `false` | 是否启用熔断（开关位，见注意事项） |
| `RequestTimeoutSeconds` | `int` | `30` | 请求超时时间（秒） |
| `AllowedOrigins` | `List<string>` | `[]` | 允许的 CORS 来源 |
| `GlobalHeaders` | `Dictionary<string,string>` | `{}` | 全局 Header |

```json
{
  "XiHan": {
    "Web": {
      "Gateway": {
        "EnableGrayRouting": true,
        "EnableRequestTracing": true,
        "EnableRateLimiting": false,
        "EnableCircuitBreaker": false,
        "RequestTimeoutSeconds": 30,
        "AllowedOrigins": [ "https://app.example.com" ],
        "GlobalHeaders": { "X-Gateway": "xihan" }
      }
    }
  }
}
```

## 使用示例

服务注册（可选配置选项）+ 管道挂载：

```csharp
builder.Services.AddGateway(options =>
{
    options.EnableGrayRouting = true;
    options.RequestTimeoutSeconds = 30;
    options.AllowedOrigins = ["https://app.example.com"];
});

var app = builder.Build();
app.UseGateway();           // 异常 + 追踪 + 灰度
app.MapControllers();
```

在控制器里读取灰度决策：

```csharp
var decision = HttpContext.GetGrayDecision();
if (HttpContext.IsGrayRequest())
{
    // 命中灰度：走新版本逻辑，可读 decision.TargetVersion / decision.MatchedRuleId
}
```

> 灰度**规则**的定义、注册与仓储（如 `GrayRule` / `IGrayRuleRepository` / 自定义 `IGrayMatcher`）属于 [Traffic](./traffic) 模块，本包只消费引擎决策。

## 注意事项与最佳实践

- **`AddGrayRouting()` 的来源在 Traffic**：本包只做接入；`IGrayRuleEngine`、匹配器、规则仓储等真正实现都在 [Traffic](./traffic)，写自定义规则/匹配器时去 Traffic 模块。
- **限流/熔断当前是配置开关位**：`EnableRateLimiting` / `EnableCircuitBreaker` 及对应常量键已定义，但网关中间件链（`UseGateway()`）本身未内置限流/熔断中间件；应用侧的限流/熔断能力见 [Web.Api](./web-api)。
- **中间件顺序不可乱**：`UseGateway()` 固定「异常 → 追踪 → 灰度」，异常处理必须在最外层才能兜住后续中间件抛出的异常。
- **决策不自动转发**：网关只把决策写入 `HttpContext.Items`，实际的版本分流/转发由你的后续中间件或业务逻辑按 `TargetVersion` 决定。

## 依赖模块

- 内部依赖：[XiHan.Framework.Web.Core](./web-core)、[XiHan.Framework.Traffic](./traffic)（灰度规则引擎真源）、[XiHan.Framework.MultiTenancy](./multitenancy)、[XiHan.Framework.Logging](./logging)、[XiHan.Framework.Serialization](./serialization)。
- 第三方核心：无（仅 .NET 原生 + 框架内部依赖）。

## 相关模块

- [XiHan.Framework.Traffic](./traffic) — 灰度路由规则引擎与流量治理能力的实现层。
- [XiHan.Framework.Web.Api](./web-api) — 应用侧的限流与熔断能力（与网关可各自启用）。
