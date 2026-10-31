# 宿主与 Web 应用

本页讲 ASP.NET Core 宿主怎样把 HTTP 请求接进内核管道：装配、请求到调用上下文的翻译、探针、区域协商、Problem Details、端点上的授权要求与 Native AOT，适用于用 `WebApplication` 承载 XiHan 应用的项目。完整 API 见 [Hosting.AspNetCore 包](../packages/hosting-aspnetcore)。

## 宿主做什么

`Hosting.AspNetCore` 是 Ring 4 的宿主适配，宿主本身不是特性（包里另有一个可选的预签名地址特性，见[对象存储](./storage)）。它负责三件事：在 `WebApplicationBuilder` 上装配 XiHan 应用（`AddXiHan`）；把每个 HTTP 请求翻译成调用上下文，让其余 ASP.NET Core 管道在内核管道的 Handle 阶段内执行（`UseXiHan`）；把失败结果写成 RFC 9457 Problem Details。认证、租户解析与授权判定不在宿主里：宿主只把凭据和授权要求放进上下文，由安全与多租户子系统在 Authenticate、Tenant、Authorize 阶段处理。

一个请求经过 `UseXiHan()` 时按下面的顺序处理：

1. 匹配到的端点带 `PipelineBypassMetadata`（探针、OpenAPI 文档、`.proto`、gRPC 方法，以及应用自己标注的端点）时直接放行，不建调用上下文。
2. 应用不在服务状态（`Ready` 或 `Degraded`）时直接回 503，错误码 `KERNEL_UNAVAILABLE_001`。
3. 从请求头建调用上下文。端点元数据上的授权要求（`RequireXiHan*`）与操作描述（`MapXiHanOperations` 映射的端点）进入上下文；操作端点先把请求绑定成载荷，绑定失败直接写 Problem Details，不进管道。
4. 经内核管道 `Ingress → Observe → Resilience → Authenticate → Tenant → Authorize → Validate → Idempotency → Transaction → Handle → Egress` 执行，ASP.NET Core 剩下的中间件与端点委托就是 Handle 阶段。端点抛出的异常由内核翻译成 `KERNEL_UNEXPECTED_001`。
5. 调用结果是失败、且响应还没有开始写出时，宿主写 Problem Details；端点已经写出正文时保留端点的内容。

成功的调用宿主不改动响应，端点返回什么就写出什么。管道阶段的含义见[执行管道](./pipeline)。

## 启用

1. 引用 `XiHan.Framework.Hosting.AspNetCore`。`Profiles.Minimal`、`Profiles.Standard`、`Profiles.Full` 都已包含它。
2. 在 `WebApplicationBuilder` 上调用 `AddXiHan(...)` 装配特性。
3. 调用 `app.UseXiHan()`。它必须在路由之后、端点之前：`WebApplication` 会在管道开头自动加上路由，按下例的位置调用即可。
4. 需要探针时调用 `app.MapXiHanHealth()`。

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

// Standard 组合：内核、宿主与五个子系统的内存默认实现
builder.AddXiHan(xihan => xihan.UseStandardProfile());

var app = builder.Build();

app.UseXiHan();          // 之后映射的端点都在内核管道的 Handle 阶段内执行
app.MapXiHanHealth();    // /health/startup、/health/ready、/health/live，不经内核管道

app.MapGet("/", () => "ok");

app.Run();
```

`WebApplicationBuilder` 上的 `AddXiHan` 先装载环境变量 `XIHAN_CONFIG_PATH` 指向的目录（Kubernetes 里是 ConfigMap 的挂载点）：`appsettings.json` 最先，其余顶层 `*.json` 按文件名序数随后；这些文件晚于内置配置装载因而优先，并以 `reloadOnChange` 装载，`IOptionsMonitor` 随文件变化。然后交给内核的 `AddXiHan`：XiHan 应用与宿主共用服务集合和配置，随宿主启动与停止。配置的写法见[配置与选项](./configuration)，挂载目录的部署方式见[部署与打包](./deployment)。

和其他 ASP.NET Core 中间件一起用时：

- 经反向代理部署时，在 `UseXiHan()` 之前调用 `UseForwardedHeaders()`，调用来源里的远端地址才是客户端地址。框架不自己解析 `X-Forwarded-For`，信任哪些代理由 `ForwardedHeadersOptions` 决定。
- 给 SignalR Hub 这类绕过内核管道的端点做认证时，`UseAuthentication()`、`UseAuthorization()` 放在 `UseXiHan()` 之前，见[实时通信](./realtime)。

## 请求如何变成调用上下文

`UseXiHan()` 用 `HttpInvocation.CreateContext(httpContext)` 建上下文，映射规则来自 HTTP 载体规范：

| 请求 | 调用上下文 | 规则 |
| --- | --- | --- |
| `traceparent` | `TraceId`、`SpanId` | W3C 版本 `00`；缺失或格式不对时生成新的 trace-id |
| `baggage` | `Baggage` | W3C 格式；只保留格式合法的 `xihan.` 键（如 `xihan.tenant`），值按 URL 解码，其他键丢弃 |
| `xihan-deadline` | `Deadline` | RFC 3339，按 UTC 解析；解析失败视为没有截止时间 |
| `grpc-timeout` | `Deadline` | 只在没有 `xihan-deadline` 时生效；数值最多 8 位，单位 `H`、`M`、`S`、`m`、`u`、`n`，按宿主时钟换算成绝对时间 |
| `Accept-Language` | Baggage `xihan.locale` | 只在 Baggage 里没有 `xihan.locale` 时写入，见[区域协商与本地化](#区域协商与本地化) |
| `Authorization` | 凭据 | 只拆成方案与值原样放入，由 Authenticate 阶段解释；宿主不验证凭据 |
| 方法、路径、连接 | 调用来源 `InvocationOrigin` | 协议 `http`；目标是方法加路径（含 PathBase，不含查询串）；远端地址、`User-Agent`、`Referer`；匹配到的端点的路由模板与 `WithName` 名字（ADR-0043、ADR-0048） |

截止时间在进入管道时已过的请求得到 504 `KERNEL_TIMEOUT_001`。调用来源只在本进程内可读，不随下游调用传播；访问日志与审计据此取客户端信息，与协议无关。

`Authorization` 头缺失时，只有登记在认证方案 `XiHan` 里的路径前缀下，查询串 `access_token` 才作为 `Bearer` 凭据（ADR-0051），其他路径的查询串令牌一律忽略。这条规则只为浏览器的 WebSocket 与 EventSource 准备，见[实时通信](./realtime)。

## 探针

`MapXiHanHealth(prefix)` 映射三个探针端点，`prefix` 缺省为 `/health`。探针直接读应用状态机，带 `PipelineBypassMetadata`，应用未就绪时也能访问；通过时返回 200、`status` 为 `pass`，否则 503、`status` 为 `fail`。

| 端点 | 通过条件 |
| --- | --- |
| `/health/startup` | 全部特性已启动 |
| `/health/ready` | 应用处于服务状态（`Ready` 或 `Degraded`） |
| `/health/live` | 应用状态不是 `Failed` |

```bash
curl -s localhost:5000/health/ready
```

下面的响应只截取了 `features` 里的一条：

```json
{
  "status": "pass",
  "state": "Ready",
  "features": [
    { "id": "xihan.samples.orders", "state": "Ready" }
  ],
  "contributions": []
}
```

响应体是 `HealthResponse`：`state` 是应用的聚合状态；`features` 每个特性一条（`id`、`state`，失败时有 `error` 错误码）；`contributions` 是 `IHealthContributor` 上报的条目（如每个进程外插件），另带 `description`、`checkedAt`、`durationMs` 与 `data`，有才写。某个条目 `Degraded` 或 `Failed` 时应用聚合为 `Degraded`，仍在服务，`/health/ready` 仍通过。状态机与健康贡献见[生命周期与健康](./lifecycle)，Kubernetes 探针的配置见[部署与打包](./deployment)。

## 在端点里读调用上下文

经过 `UseXiHan()` 的请求，端点里用 `HttpContext.GetXiHanContext()` 取当前调用上下文，返回 `Maybe<InvocationContext>`；请求没有经过 `UseXiHan()`（绕过管道的端点、或者没有调用 `UseXiHan()`）时为无值。端点在 Handle 阶段执行，此时 Authenticate 与 Tenant 阶段已经写好安全上下文，`GetSecurity()` 取主体与租户；`SetOutcome(...)` 把失败结果写回上下文，由宿主写成 Problem Details。

下例接上一节的 `Program.cs`，写法来自 `samples/SimpleApp`：

```csharp
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

app.MapGet("/whoami", (HttpContext http) =>
{
    var security = http.GetXiHanContext().ValueOrThrow().GetSecurity();
    return new WhoAmI(security.Principal.Id, security.Principal.IsAuthenticated, security.Tenant?.Id);
});

app.MapGet("/missing", (HttpContext http) =>
{
    // 不抛异常：失败结果留在上下文里，管道结束后宿主写成 Problem Details
    http.GetXiHanContext().ValueOrThrow().SetOutcome(Error.NotFound("SAMPLES_NOTFOUND_001", SampleMessages.ResourceMissing));
    return Results.Empty;
});

public sealed record WhoAmI(string Principal, bool IsAuthenticated, string? Tenant);

public static class SampleMessages
{
    public const string ResourceMissing = "samples.resource.missing";
}
```

调用链深处拿不到 `HttpContext` 的服务（领域服务、仓储装饰器）不要依赖 HTTP，改用作用域服务 `IInvocationAccessor.Current` 取当前调用的上下文，见[执行管道](./pipeline)。

## 端点上的授权要求

普通的最小 API 端点用三个约定扩展挂授权要求，`UseXiHan()` 把它放进上下文，Authorize 阶段判定：

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

`OrderEventCounter` 与 `OrderStatistics` 是 `samples/SimpleApp` 里的类型。判定由安全子系统完成：没有装配 `security.authorization@1` 时要求只进入上下文，不被判定。权限码必须由某个特性用 `FeatureContext.AddPermission` 登记，未登记的权限码与不存在的策略都以 `SECURITY_NOTFOUND_001` 拒绝。端点上的要求优先于操作契约上的权限或策略。认证与授权的配置见[认证](./authentication)与[授权](./authorization)。

## Problem Details

失败结果按 RFC 9457 写成 `application/problem+json`，标准成员之外有四个扩展成员（`code`、`traceId`、`errors`、`details`）：

| 成员 | 内容 |
| --- | --- |
| `type` | `urn:xihan:error:<错误码>` |
| `title` | 错误码的 KIND 段，如 `NOTFOUND` |
| `status` | HTTP 状态码，按 KIND 映射，对照表见[状态码与错误映射](./http#状态码与错误映射) |
| `detail` | 按调用方区域翻译的文案；没有装配本地化时是 `Error.Message` |
| `instance` | 请求路径 |
| `code` | 错误码，客户端只依赖它 |
| `traceId` | 本次调用的 trace-id；应用未就绪的 503 还没有上下文，为空串 |
| `errors` | 只在 VALIDATION 种类的错误上出现：字段键 → 消息数组 |
| `details` | `Error.Details` 里的标量（字符串、布尔、数值、`Guid`、时间等，如 `id`、`retryAfter`）；集合与对象不输出 |

```bash
curl -s localhost:5000/missing -H "Accept-Language: zh-CN"
```

```json
{
  "type": "urn:xihan:error:SAMPLES_NOTFOUND_001",
  "title": "NOTFOUND",
  "status": 404,
  "detail": "示例资源不存在。",
  "instance": "/missing",
  "code": "SAMPLES_NOTFOUND_001",
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736"
}
```

::: v-pre
`errors` 的键是字段键：请求属性的序列化名，嵌套用点分路径与下标（`lines[0].sku`），根级为空串。值有两种来源：内核校验器按 Schema 产生的违规带 `kernel.validation.*` 消息键，与 `detail` 走同一条翻译通道；应用自校验（`IValidatableObject`）返回的文案原样输出。`samples/SimpleApp` 的下单请求名称为空、金额为 0 时，响应里的这一段是 `{"errors":{"name":["长度必须 >= 1"],"amount":["值必须 >= 1"]}}`。校验的写法见[数据校验](./validation)。
:::

401 响应另带 `WWW-Authenticate` 质询，规则见[状态码与错误映射](./http#状态码与错误映射)。

`ProblemResponse` 公开了这套规则：`FromError` 构造响应体，`StatusCodeFor` 给出状态码，`WriteTo` 写出 JSON。绕过管道的端点要自己写错误、或访问日志要记状态码时用它们，结果与宿主写出的一致。

## 区域协商与本地化

`Accept-Language` 的候选按权重降序排列（同权重保持原顺序），跳过 `*`、权重为 0 与格式不合法的项。调用方在 Baggage 里显式给了 `xihan.locale` 时以它为准，不看 `Accept-Language`。

装配了 `localization.resources@1`（`Profiles.Minimal` 起已包含 `Localization.InMemory`）时，候选与启用的区域协商：先找精确命中，再找同语言的第一个启用区域，都没有时取默认区域 `en-US`。没有装配本地化时取第一个合法候选，规范化后写进 Baggage，下游服务仍能拿到调用方语言。

Problem Details 的 `detail` 用消息目录翻译成协商出的区域，`Content-Language` 标出文案实际命中的区域：目录里没有这种语言时沿回退链取到默认区域，`Content-Language` 就是 `en-US`。`code` 不随区域变化。端点里直接调用 `key.L()` 也得到当前请求语言的文案。

应用自己的文案按消息键放进每种语言一个文件的消息目录，登记方式与框架包相同：

```csharp
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;

builder.Services.AddMessageCatalog(MessageCatalog.FromEmbeddedResources("samples", typeof(SampleMessages)));
```

```xml
<ItemGroup>
  <EmbeddedResource Include="Localization/*.json" LogicalName="Localization/%(Filename)%(Extension)" />
</ItemGroup>
```

消息键、区域配置与回退链见[国际化](./localization)。

## Native AOT

`Hosting.AspNetCore` 的响应体都不经反射序列化：探针用源生成的序列化上下文，Problem Details 由 `ProblemResponse.WriteTo` 手写。应用要 Native AOT 发布时，自己的端点也照这两点做：

1. 在项目里打开最小 API 的请求委托生成器，端点委托在编译期生成，不走反射绑定。
2. 端点用到的请求与响应类型登记进源生成的 `JsonSerializerContext`，插到 HTTP JSON 选项解析链的最前面。

```xml
<PropertyGroup>
  <EnableRequestDelegateGenerator>true</EnableRequestDelegateGenerator>
</PropertyGroup>
```

```csharp
using System.Text.Json.Serialization;

// 接上例：放在 builder.Build() 之前
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));

// camelCase 与不区分大小写：HTTP 适配器把路由值、查询串与请求体合并成 JSON 对象，按 JSON 约定匹配属性
// 允许从字符串读数字：路由值与查询串都是字符串
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(WhoAmI))]
internal sealed partial class AppJsonContext : JsonSerializerContext;
```

操作契约的请求与响应类型也登记在同一个上下文里，操作的 JSON Schema 由 `Kernel.Generators` 在编译期生成。仓库 CI 以 `dotnet publish next/samples/SimpleApp/SimpleApp.csproj` 做 Native AOT 冒烟（`PublishAot` 在项目文件里声明），示例装配了 HTTP、gRPC、MCP 与 CloudEvents 四个协议适配器。序列化与裁剪的完整要求见[序列化与 Native AOT](./aot)。

## 行为与错误码

| 情形 | 状态码 | 错误码 |
| --- | --- | --- |
| 应用未就绪或正在停止 | 503 | `KERNEL_UNAVAILABLE_001` |
| 进入管道时截止时间已过 | 504 | `KERNEL_TIMEOUT_001` |
| 端点抛出未处理异常 | 500 | `KERNEL_UNEXPECTED_001` |
| 操作端点的请求体不是 JSON 对象 | 400 | `KERNEL_VALIDATION_001` |
| 未认证 / 凭据无效、过期或签名不符 | 401 | `SECURITY_DENIED_001` / `SECURITY_DENIED_003` |
| 策略拒绝 / 缺少权限 | 403 | `SECURITY_DENIED_002` / `SECURITY_DENIED_004` |
| 策略不存在或权限码未登记 | 404 | `SECURITY_NOTFOUND_001` |

## 相关

- [Hosting.AspNetCore 包](../packages/hosting-aspnetcore)
- [HTTP 与 OpenAPI](./http)：把操作契约映射成端点
- [执行管道](./pipeline)、[生命周期与健康](./lifecycle)
- [认证](./authentication)、[授权](./authorization)、[国际化](./localization)
- [实时通信](./realtime)：绕过管道的端点与认证方案 `XiHan`
- 规范：`spec/protocol/http.md`《HTTP 载体》、RFC-0003《调用上下文与传播》、RFC-0010《本地化》
