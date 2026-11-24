# ASP.NET Core 宿主

这页讲 `Hosting.AspNetCore` 怎样把一个 ASP.NET Core 应用变成 XiHan 应用：在 `WebApplicationBuilder` 上装配特性，把每个 HTTP 请求翻译成调用上下文并送进内核管道，映射探针，把失败写成 Problem Details，按 `Accept-Language` 协商区域，以及 Native AOT 发布要做的两件事。读完这页，我们能用 `WebApplication` 承载一个 XiHan 应用，并且说得出一个请求从进入 Kestrel 到写出响应中间经过了什么。

前置知识：[第一个应用](/getting-started/first-app)、[装配入口](/startup/bootstrap)与[执行管道](/startup/pipeline)。在端点里读调用上下文、给端点挂授权要求这两件事放在[端点里使用框架](/web/endpoints)，这页只做提及。

## 为什么需要

ASP.NET Core 自己就能承载 Web 应用，但它不知道什么是调用上下文、特性与管道阶段：请求头里的 `traceparent`、`baggage`、截止时间与凭据需要有人翻译成内核能理解的上下文，失败结果需要有人统一写成客户端能解析的错误信封，Kubernetes 的探针需要有人把应用状态机暴露出去。没有宿主适配，每个应用都得自己写这一层，而且写法互不相同，客户端也无法用同一套规则解析错误。

`Hosting.AspNetCore` 把这层翻译收敛成两个调用：`AddXiHan` 负责装配，`UseXiHan` 负责把请求接进管道。宿主只做翻译，不做判定：认证、租户解析与授权都在管道的 Authenticate、Tenant、Authorize 阶段由对应子系统完成，宿主只把凭据和授权要求放进上下文。

## 宿主做什么

`Hosting.AspNetCore` 是宿主适配，不是特性，所以它没有清单，也不提供能力 ID（包里另有一个可选的预签名地址特性，见[分片、预签名与命名实例](/object-storage/advanced)）。它负责三件事：

| 调用 | 职责 |
| --- | --- |
| `builder.AddXiHan(...)` | 在 `WebApplicationBuilder` 上装配 XiHan 应用：先装载挂载的配置目录，再把特性交给内核的 `AddXiHan`，应用与宿主共用服务集合与配置，随宿主一起启动与停止 |
| `app.UseXiHan()` | 把每个 HTTP 请求翻译成调用上下文，让它之后的 ASP.NET Core 中间件与端点在内核管道的 Handle 阶段内执行；调用失败时写 RFC 9457 Problem Details |
| `app.MapXiHanHealth()` | 映射 startup / ready / live 三个探针，直接读应用状态机，不经管道 |

一个请求经过 `UseXiHan()` 时按下面的顺序处理：

1. 匹配到的端点带 `PipelineBypassMetadata`（探针、OpenAPI 文档、`.proto`、gRPC 方法，以及应用自己标注的端点）时直接放行，不建调用上下文。
2. 应用不在服务状态（`Ready` 或 `Degraded`）时直接回 503，错误码 `KERNEL_UNAVAILABLE_001`。
3. 从请求头建调用上下文。端点元数据上的授权要求（`RequireXiHan*`）与操作描述（`MapXiHanOperations` 映射的端点）进入上下文；操作端点的载荷不在这里读，只把绑定委托作为来源放进上下文，Validate 阶段（Authorize 之后）才读请求体并绑定，绑定失败与其他失败一样写成 Problem Details。
4. 经内核管道 `Ingress → Observe → Resilience → Authenticate → Tenant → Authorize → Validate → Idempotency → Transaction → Handle → Egress` 执行，ASP.NET Core 剩下的中间件与端点委托就是 Handle 阶段。端点抛出的异常由内核翻译成 `KERNEL_UNEXPECTED_001`。
5. 调用结果是失败、且响应还没有开始写出时，宿主写 Problem Details；端点已经写出正文时保留端点的内容。

成功的调用宿主不改动响应，端点返回什么就写出什么。管道阶段的含义见[执行管道](/startup/pipeline)。

## 最小示例

1. 引用 `XiHan.Framework.Hosting.AspNetCore`。`Profiles.Minimal`、`Profiles.Standard`、`Profiles.Full` 都已包含它，选组合包的办法见[组合包与选包](/getting-started/profiles)。
2. 在 `WebApplicationBuilder` 上调用 `AddXiHan(...)` 装配特性。
3. 调用 `app.UseXiHan()`。它必须在路由之后、端点之前：`WebApplication` 会在管道开头自动加上路由，按下例的位置调用即可。
4. 需要探针时调用 `app.MapXiHanHealth()`。

```bash
dotnet add package XiHan.Framework.Profiles.Standard --prerelease
```

宿主自己的部分只有下面几行，完整的 `Program.cs` 见[第一个应用](/getting-started/first-app)：

```csharp
// Standard 组合：内核、宿主与五个子系统的内存默认实现
builder.AddXiHan(xihan => xihan.UseStandardProfile());

var app = builder.Build();

app.UseXiHan();          // 之后映射的端点都在内核管道的 Handle 阶段内执行
app.MapXiHanHealth();    // /health/startup、/health/ready、/health/live，不经内核管道

app.MapGet("/", () => "ok");
```

下面的命令假设应用监听 `http://localhost:5000`（`dotnet run -- --urls http://localhost:5000`，或者在 `appsettings.json` 的 `Kestrel:Endpoints` 里配置）：

```bash
curl -s localhost:5000/
curl -s localhost:5000/health/ready
```

第一条得到 `ok`，第二条得到一段 JSON，`status` 是 `pass`，`state` 是 `Ready`。这个请求经过了完整的内核管道，只是没有任何阶段拒绝它；`/health/ready` 则不经管道，应用还没启动完也能访问。

## 装配时发生了什么

`WebApplicationBuilder` 上的 `AddXiHan` 先把环境变量 `XIHAN_CONFIG_PATH` 指向的目录（Kubernetes 里是 ConfigMap 的挂载点）装载为可重载的配置源，然后交给内核的 `AddXiHan`，应用与宿主共用服务集合和配置。文件的装载顺序与配置源的优先级见[配置目录与热更新](/configuration/config-directory)，挂载目录的部署方式见 [Kubernetes](/deployment/kubernetes)。

和其他 ASP.NET Core 中间件一起用时有两条顺序约定：

- 经反向代理部署时，在 `UseXiHan()` 之前调用 `UseForwardedHeaders()`，调用来源里的远端地址才是客户端地址。框架不自己解析 `X-Forwarded-For`，信任哪些代理由 `ForwardedHeadersOptions` 决定。
- 给 SignalR Hub 这类绕过内核管道的端点做认证时，`UseAuthentication()`、`UseAuthorization()` 放在 `UseXiHan()` 之前，写法见[端点里使用框架](/web/endpoints)与[实时通信](/web/realtime)。

## 请求如何变成调用上下文

`UseXiHan()` 用 `HttpInvocation.CreateContext(httpContext)` 建上下文，映射规则来自 HTTP 载体规范：

| 请求 | 调用上下文 | 规则 |
| --- | --- | --- |
| `traceparent` | `TraceId`、`SpanId`、`TraceFlags` | W3C 版本 `00`，采样标志原样保留（上游 `-00` 时内核 Span 不采样）；缺失或格式不对时并入 ASP.NET Core 当前的请求 Activity，也没有时生成新的 trace-id |
| `baggage` | `Baggage` | W3C 格式；只保留已登记的键（`xihan.correlation`、`xihan.idempotency-key`、`xihan.locale`；`xihan.tenant` 与 `xihan.principal` 不收，调用方的租户与主体只来自认证），值按 URL 解码，未登记的 `xihan.` 键与其他键丢弃 |
| `xihan-deadline` | `Deadline` | 绝对截止时间，只认一种写法（RFC-0003 规则 8）：`2026-11-18T03:25:00Z` 或带偏移的 `2026-11-18T11:25:00.5+08:00`，必须有 `T`、秒与偏移（`Z` 或 `±HH:MM`），小数一到九位；单值头，按整串匹配，不裁剪空白。读不出（不合文法、越界、空值、出现多次）当作没有这个头，不报错 |
| `grpc-timeout` | `Deadline` | 没有 `xihan-deadline` 或它读不出时生效；数值 1 到 8 位，单位 `H`、`M`、`S`、`m`、`u`、`n`，按宿主时钟换算成绝对时间；自己读不出或出现多次同样当作没有 |
| `Accept-Language` | Baggage `xihan.locale` | 只在 Baggage 里没有 `xihan.locale` 时写入，见下文「区域协商与本地化」 |
| `Authorization` | 凭据 | 只拆成方案与值原样放入，由 Authenticate 阶段解释；宿主不验证凭据 |
| 方法、路径、连接 | 调用来源 `InvocationOrigin` | 协议 `http`；目标是方法加路径（含 PathBase，不含查询串）；远端地址、`User-Agent`、`Referer`；匹配到的端点的路由模板与 `WithName` 名字 |

截止时间在进入管道时已过的请求得到 504 `KERNEL_TIMEOUT_001`。`xihan-deadline` 读不出时请求照常处理，只是没有截止时间，宿主不为此报错：空格代替 `T`、没有偏移、没有秒、只有日期、`+0800` 这类偏移、首尾带空白、HTTP 日期都读不出。值不为空而读不出时，宿主记一条 Warning（类别 `XiHan.Framework.Kernel.Diagnostics.UnreadableDeadlineLog`），带头名、原值的字符数与请求匹配到的操作 ID，不带原值；请求头这一类 1 分钟最多一条，下一条带上这期间被压掉的条数，一个请求只算一次。没有给 `xihan-deadline` 而 `grpc-timeout` 读不出时同样记，头名是 `grpc-timeout`；值为空不记。调用方写这个头，.NET 用 `DeadlineText.Format(deadline)`，JavaScript 的 `Date.prototype.toISOString()`、Go 的 `time.RFC3339Nano`、Java 的 `Instant.toString()` 写出的也合文法；不带时区的时间（`Kind` 为 `Unspecified` 的 `DateTime`、Python 的朴素 `datetime.isoformat()`）与秒为零时省略秒的写法（Java 的 `OffsetDateTime.toString()`）读不出。调用来源只在本进程内可读，不随下游调用传播；访问日志与审计据此取客户端信息，与协议无关。上下文的字段含义与传播规则见[调用上下文](/startup/invocation-context)。

`Authorization` 头缺失时，只有登记在认证方案 `XiHan` 里的路径前缀下，查询串 `access_token` 才作为 `Bearer` 凭据，其他路径的查询串令牌一律忽略。这条规则只为浏览器的 WebSocket 与 EventSource 准备，见[实时通信](/web/realtime)。

## 探针

`MapXiHanHealth(prefix)` 映射三个探针端点，`prefix` 缺省为 `/health`。探针直接读应用状态机，带 `PipelineBypassMetadata`，应用未就绪时也能访问；通过时返回 200、`status` 为 `pass`，否则 503、`status` 为 `fail`；`/health/startup`、`/health/ready`、`/health/live` 各自的通过条件与对应的 Kubernetes 探针见[生命周期与健康](/startup/lifecycle#探针端点)。

探针谁都能访问，只给状态（ADR-0209）：

```json
{ "status": "pass", "state": "Ready" }
```

各特性与健康条目的详情由 `MapXiHanHealthDetails(pattern)`（缺省 `/health/details`）给出。它经内核管道，返回的约定构建器能加访问要求，详情会告诉访问者装配了哪些特性与 Provider，不要匿名开放：

```csharp
// health.read 是应用自己的权限码，要先在某个特性的 Configure 里登记：
// context.AddPermission(new PermissionDefinition("health.read", "Read health details"));
app.MapXiHanHealthDetails().RequireXiHanPermission("health.read");
```

端点引用的权限码没有登记时，宿主建请求管道时启动失败（`SECURITY_VALIDATION_004`）。

下面的详情只截取了 `features` 里的一条：

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

响应体是 `HealthResponse`：`state` 是应用的聚合状态；详情端点里 `features` 每个特性一条（`id`、`state`，失败时有 `error` 错误码）；`contributions` 是 `IHealthContributor` 上报的条目（如每个进程外插件），另带 `description`、`checkedAt`、`durationMs` 与 `data`，有才写。某个条目 `Degraded` 或 `Failed` 时应用聚合为 `Degraded`，仍在服务，`/health/ready` 仍通过。状态机与健康贡献见[生命周期与健康](/startup/lifecycle)，Kubernetes 探针的配置见 [Kubernetes](/deployment/kubernetes)。

## Problem Details

失败结果按 RFC 9457 写成 `application/problem+json`，标准成员之外有四个扩展成员（`code`、`traceId`、`errors`、`details`）：

| 成员 | 内容 |
| --- | --- |
| `type` | `urn:xihan:error:<错误码>` |
| `title` | 错误码的 KIND 段，如 `NOTFOUND` |
| `status` | HTTP 状态码，按 KIND 映射，对照表见[结果与错误处理](/web/errors#错误码) |
| `detail` | 按调用方区域翻译的文案；没有装配本地化时是 `Error.Message` |
| `instance` | 请求路径 |
| `code` | 错误码，客户端只依赖它 |
| `traceId` | 本次调用的 trace-id；应用未就绪的 503 还没有上下文，为空串 |
| `errors` | 只在 VALIDATION 种类的错误上出现：字段键 → 消息数组 |
| `details` | `Error.Details` 里的标量（字符串、布尔、数值、`Guid`、时间等，如 `id`、`retryAfter`）；集合与对象不输出 |

以示例应用的 `/missing` 端点为例（它把一个 `NotFound` 错误写进上下文，写法见[端点里使用框架](/web/endpoints)）：

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
`errors` 的键是字段键：请求属性的序列化名，嵌套用点分路径与下标（`lines[0].sku`），根级为空串。值有两种来源：内核校验器按 Schema 产生的违规带 `kernel.validation.*` 消息键，与 `detail` 走同一条翻译通道；应用自校验（`IValidatableObject`）返回的文案原样输出。示例应用的下单请求名称为空、金额为 0 时，响应里的这一段是 `{"errors":{"name":["长度必须 >= 1"],"amount":["值必须 >= 1"]}}`。校验的写法见[数据校验](/web/validation)。
:::

401 响应另带 `WWW-Authenticate` 质询，规则见 [HTTP 路由与载荷](/web/routing)。

`ProblemResponse` 公开了这套规则：`FromError` 构造响应体，`StatusCodeFor` 给出状态码，`WriteTo` 写出 JSON。绕过管道的端点要自己写错误、或访问日志要记状态码时用它们，结果与宿主写出的一致。`Result` 与 `Error` 本身的用法见[结果与错误处理](/web/errors)；宿主自己写出的状态与错误码一览见 [Hosting.AspNetCore](/packages/hosting-aspnetcore#行为与限制)。

## 区域协商与本地化

调用方在 Baggage 里显式给了 `xihan.locale` 时以它为准；否则宿主把 `Accept-Language` 的候选与启用的区域协商，结果写进 Baggage，下游服务也能拿到调用方语言。候选的排序、两轮协商与没有装配本地化时的取法见[全球化和本地化](/common/localization#区域协商)。

Problem Details 的 `detail` 用消息目录翻译成协商出的区域，`Content-Language` 标出文案实际命中的区域：目录里没有这种语言时沿回退链取到默认区域，`Content-Language` 就是 `en-US`。`code` 不随区域变化。端点里直接调用 `key.L()` 也得到当前请求语言的文案。

应用自己的文案按消息键放进每种语言一个文件的消息目录，用 `AddMessageCatalog` 登记，方式与框架包相同；消息键、语言文件、区域配置与回退链见[全球化和本地化](/common/localization)。

## Native AOT

`Hosting.AspNetCore` 的响应体都不经反射序列化：探针用源生成的序列化上下文，Problem Details 由 `ProblemResponse.WriteTo` 手写。应用要 Native AOT 发布时，自己的端点也照这两点做：在项目里打开最小 API 的请求委托生成器（`EnableRequestDelegateGenerator`），端点委托在编译期生成；端点用到的请求与响应类型登记进源生成的 `JsonSerializerContext`，插到 HTTP JSON 选项解析链的最前面。操作契约的请求与响应类型也登记在同一个上下文里。项目文件、上下文的写法与发布步骤见 [JSON 序列化与 Native AOT](/common/serialization)。

## 相关包与下一步

- 相关包：[Hosting.AspNetCore](/packages/hosting-aspnetcore)、[Protocols.Http](/packages/protocols-http)、[Kernel](/packages/kernel)、[Profiles.Standard](/packages/profiles-standard)
- 下一步：[操作契约](/web/operations)，把业务写成与协议无关的操作，再由宿主与适配器发布到 HTTP
- 相关章节：[执行管道](/startup/pipeline)、[生命周期与健康](/startup/lifecycle)、[认证](/security/authentication)、[授权与权限](/security/authorization)
- 规范：HTTP 载体规范、RFC-0003《调用上下文与传播》、RFC-0010《本地化》
