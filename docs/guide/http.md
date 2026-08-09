# HTTP 远程请求

调用外部 API 时，你不想每次都手写 `HttpClient` 注册、超时、重试、日志、异常兜底这一堆样板。`XiHan.Framework.Http` 在 `IHttpClientFactory` 之上把这些固化下来：一个注入即用的服务、一套统一的返回值 `HttpResult<T>`、一组按名字挑选的客户端，以及基于 Polly 的重试 / 超时 / 熔断。

## 先分清：这一章和动态 API 是两件事

两者经常被放在一起提，但方向相反。

| | 动态 API | HTTP 远程请求 |
| --- | --- | --- |
| 方向 | **入站**：把你的应用服务暴露成 HTTP 端点 | **出站**：你的进程去调别人的 HTTP 接口 |
| 所在包 | `XiHan.Framework.Web.Api` | `XiHan.Framework.Http` |
| 关键类型 | `DynamicApiControllerFactory`；`[DynamicApi]` 特性在 `XiHan.Framework.Application` | `IAdvancedHttpService`、`HttpRequestBuilder` |
| 你写的东西 | 应用服务方法 | 请求 URL + 选项 |

::: warning 框架不提供客户端代理生成
`Web.Api` 的 `DynamicApi` 目录里只有控制器工厂、约定、参数分析这些**服务端** MVC 侧的东西，没有「按服务接口自动生成 HTTP 调用代理」的能力。要调用另一个服务的动态 API 端点，仍然是本章的做法：拿到 URL，用 `IAdvancedHttpService` 发请求，自己声明返回 DTO。
:::

## 安装与启用

```bash
dotnet add package XiHan.Framework.Http
```

```csharp
[DependsOn(typeof(XiHanHttpModule))]
public class MyModule : XiHanModule { }
```

`XiHanHttpModule` 依赖 `XiHanSerializationModule`，会一并启用。模块启动时做两件事：

| 阶段 | 行为 |
| --- | --- |
| `ConfigureServices` | 绑定 `XiHanHttpClientOptions`（`XiHan:Http`）与 `XiHanProxyPoolOptions`（`XiHan:Http:ProxyPool`）；注册 `IAdvancedHttpService`（Scoped）、`IProxyValidator` / `IProxyPoolManager`（Singleton）；注册 `Remote`、`Local` 及配置里声明的自定义命名客户端 |
| `OnApplicationInitialization` | 调用 `StringHttpExtensions.Initialize(context.ServiceProvider)`，激活字符串链式扩展 |

## 两个入口

### 入口一：注入 `IAdvancedHttpService`

推荐的默认写法，构造函数注入，测试时可替换。

```csharp
using XiHan.Framework.Http.Extensions;
using XiHan.Framework.Http.Options;
using XiHan.Framework.Http.Services;

public class WeatherClient
{
    private readonly IAdvancedHttpService _http;

    public WeatherClient(IAdvancedHttpService http)
    {
        _http = http;
    }

    public async Task<WeatherDto?> GetAsync(string city, CancellationToken ct = default)
    {
        var options = new XiHanHttpRequestOptions()
            .WithAuthorization("your-token")     // Authorization: Bearer your-token
            .AddQueryParameter("unit", "metric")
            .SetTimeout(TimeSpan.FromSeconds(10));

        var result = await _http.GetAsync<WeatherDto>(
            $"https://api.example.com/weather/{city}", options, ct);

        return result.GetDataOrDefault();        // 失败返回 default
    }
}
```

`XiHanHttpRequestOptions` 本身带链式方法（`AddHeader` / `AddQueryParameter` / `AddTag` / `SetTimeout` / `SetRequestId` / `SetProxy` / `EnableProxyPool`），`HttpServiceExtensions` 再补一层语义化的（`WithAuthorization` / `WithBasicAuth` / `AsJson` / `AsForm` / `WithUserAgent` / `WithCorrelationId` / `WithLanguage` / `WithoutRetry` / `WithoutCircuitBreaker` / `WithoutCache` / `UseClient` / `WithVerboseLogging` / `WithoutLogging`）。

### 入口二：字符串链式 `AsHttp()`

适合没有构造函数可注入的静态工具代码。

```csharp
using XiHan.Framework.Http.Extensions;

var result = await "/repos/owner/name/issues"
    .AsHttp()
    .UseClient("github")                          // 用配置里声明的命名客户端
    .SetJsonBody(new { title = "bug", body = "..." })
    .PostAsync<IssueDto>();

if (result.IsSuccess)
{
    var issue = result.Data;
}
```

`string` 上还有一批直达扩展，跳过 `AsHttp()`：`"url".GetAsync<T>()`、`"url".GetStringAsync()`、`"url".PostAsync<T>(body)`、`"url".PutAsync<T>(body)`、`"url".DeleteAsync()`、`"url".DownloadAsync(path)`。

::: warning 链式扩展依赖应用初始化
`AsHttp()` 从静态字段拿 `IAdvancedHttpService`，该字段由 `OnApplicationInitialization` 填充。在应用初始化完成之前（含单元测试直接调用）会抛 `InvalidOperationException`，提示「HTTP 服务扩展未初始化」。这类场景请注入 `IAdvancedHttpService`。
:::

## 统一返回值 `HttpResult<T>`

所有方法都返回 `HttpResult<T>`，**不靠抛异常表达失败**——网络异常、超时、非 2xx 状态码都被收进同一个对象里。

```csharp
var result = await _http.GetAsync<OrderDto>(url);

result.IsSuccess;            // 是否 2xx
result.StatusCode;           // HttpStatusCode
result.Data;                 // 反序列化后的 T
result.RawDataString;        // 原始响应字符串
result.ErrorMessage;         // 失败时的消息，含被截断的响应体
result.Exception;            // 失败时的异常
result.ElapsedMilliseconds;  // 耗时
```

取值有三种姿势，按调用点的容错要求选：

```csharp
var a = result.GetDataOrThrow();      // 失败抛 HttpRequestException
var b = result.GetDataOrDefault();    // 失败返回 default
var c = result.GetDataOrDefault(new OrderDto());   // 失败返回指定兜底值
```

无响应体的动词（`DeleteAsync(url)` / `HeadAsync` / `OptionsAsync`）返回非泛型的 `HttpResult`，它继承自 `HttpResult<object>`。

## 命名客户端

框架注册三类命名客户端，请求时按名字挑：

| 名称 | 来源 | BaseAddress | 用途 |
| --- | --- | --- | --- |
| `Remote` | `HttpGroupEnum.Remote`，**默认** | 无 | 调用外部服务，URL 写全 |
| `Local` | `HttpGroupEnum.Local` | `http://127.0.0.1` | 本机服务间调用，URL 只写路径 |
| 自定义 | `XiHan:Http:Clients` 的每个键 | 由配置给出 | 每个上游一套独立的地址 / 超时 / 请求头 |

选择靠 `UseClient(name)`，它把名字写进 `XiHanHttpRequestOptions.Tags["ClientName"]`；`AdvancedHttpService` 读这个标签调 `IHttpClientFactory.CreateClient(name)`，没有标签就用 `Remote`。

新增一个上游只改配置、不改代码：

```json
{
  "XiHan": {
    "Http": {
      "Clients": {
        "github": {
          "BaseAddress": "https://api.github.com",
          "TimeoutSeconds": 30,
          "Headers": { "Accept": "application/vnd.github+json" },
          "EnableRetry": true,
          "EnableCircuitBreaker": true
        }
      }
    }
  }
}
```

::: warning 自定义客户端的两个边界
- `EnableRetry` 与 `EnableCircuitBreaker` **同时为 `false`** 时，整段 Polly 配置会被跳过——连超时策略也不会挂上，这个客户端只剩 `HttpClient.Timeout` 一道保护。
- `Local` 客户端没有配置主消息处理器，所以全局的 `IgnoreSslErrors` 对它不生效；`Remote` 与自定义客户端生效。
:::

## 韧性策略：管道长什么样

每个命名客户端的处理器链，从外到内是固定顺序：

```text
IAdvancedHttpService
        │  CancellationTokenSource(请求级超时或 DefaultTimeoutSeconds)
        ▼
┌──────────────────────────┐
│ HttpLoggingMiddleware    │  记录请求/响应，敏感头脱敏
├──────────────────────────┤
│ 重试策略                  │  HandleTransientHttpError + TimeoutRejectedException
├──────────────────────────┤
│ 超时策略                  │  2 × DefaultTimeoutSeconds
├──────────────────────────┤
│ 熔断策略                  │  HandleTransientHttpError
├──────────────────────────┤
│ HttpClientHandler        │
└──────────────────────────┘
```

有两个由顺序直接决定的结论：

- **日志在重试之外**。一次逻辑请求只打一对请求/响应日志，`Elapsed` 包含全部重试耗时，看不到中间的失败尝试。
- **超时策略在重试之内**。每次重试各自享有一份 Polly 超时预算，但整体仍受最外层 `CancellationTokenSource` 约束。

### 重试

重试条件是 Polly 的 `HandleTransientHttpError()`（网络层 `HttpRequestException`、5xx、408）再加上 `TimeoutRejectedException`。重试节奏来自 `RetryDelaySeconds` 数组：

```json
{ "XiHan": { "Http": { "RetryDelaySeconds": [1, 5, 10] } } }
```

上面这份配置 = 重试 3 次，间隔 1 秒、5 秒、10 秒。

::: tip 重试次数只看数组长度
`RetryCount` 这个配置项存在且会被绑定，但策略构建过程完全不读它。要改重试次数，改 `RetryDelaySeconds` 的**元素个数**。
:::

### 超时：三层，取最紧的那层

| 层 | 值 | 作用范围 |
| --- | --- | --- |
| `CancellationTokenSource` | `XiHanHttpRequestOptions.Timeout` ?? `DefaultTimeoutSeconds` | 整次调用，**含全部重试** |
| `HttpClient.Timeout` | 客户端的 `TimeoutSeconds` ?? `DefaultTimeoutSeconds`；请求级 `Timeout` 会覆写 | 单次底层发送 |
| Polly 超时策略 | `2 × DefaultTimeoutSeconds` | 单次尝试 |

::: danger 请求级超时是总预算
`SetTimeout(TimeSpan.FromSeconds(5))` 之后，5 秒是「首次尝试 + 所有重试 + 所有退避间隔」的总和。配了 `[1, 5, 10]` 的重试却只给 5 秒超时，等于重试根本没机会跑完。排查超时时三层都要看。

另外，Polly 超时策略固定按全局 `DefaultTimeoutSeconds` 算，自定义客户端的 `TimeoutSeconds` 不参与。
:::

### 熔断

熔断用 `HandleTransientHttpError().CircuitBreakerAsync(...)`，阈值取 `CircuitBreakerFailureThreshold`，断开时长取 `CircuitBreakerDurationOfBreakSeconds`。断路器打开时 `AdvancedHttpService` 捕获 `BrokenCircuitException`，返回状态码 `503` 的失败结果，消息里带上还要等多久。

::: danger 当前实现下熔断器不会真正打开
熔断和重试都注册成 Polly 的**动态策略选择器**（`AddPolicyHandler(request => ...)`），选择器在每次请求时被调用。重试策略是无状态的，每次新建没有影响；但熔断器是有状态的，每次请求新建一份就意味着失败计数从零开始，无法跨请求累积，因此不会进入 Open 状态。

需要真正的跨请求出站熔断，请自行为该命名客户端追加一个**静态**的熔断策略实例，或把熔断放到网关层处理。
:::

配置项里的 `CircuitBreakerSamplingDurationSeconds` 与 `CircuitBreakerMinimumThroughput` 同样只被绑定、不被策略构建使用——用的是连续失败计数式熔断，不是采样窗口式。

### 按单次请求关闭策略

策略开关通过 `HttpRequestMessage.Options` 传给动态选择器，选择器读到 `false` 就退化成 `Policy.NoOpAsync`。所以不用改全局配置也能对某一次请求让路：

```csharp
// 非幂等的写操作，重试会造成重复提交
var options = new XiHanHttpRequestOptions()
    .WithoutRetry()
    .WithoutCircuitBreaker();

await _http.PostAsync<CreateOrderDto, OrderDto>(url, input, options);
```

链式写法同理：`"url".AsHttp().WithoutRetry().PostAsync<T>()`。

::: tip 默认给 POST/PUT/PATCH 关掉重试
`HandleTransientHttpError()` 会重试 5xx，而 5xx 不代表上游没收到、没处理。除非接口明确幂等，写操作建议显式 `WithoutRetry()`。
:::

## 日志与脱敏

`HttpLoggingMiddleware` 挂在每个命名客户端上。行为要点：

| 项 | 行为 |
| --- | --- |
| 开关 | 全局 `EnableRequestLogging` / `EnableResponseLogging`；请求级 `WithVerboseLogging()` / `WithoutLogging()` 覆盖全局 |
| 敏感请求头 | `Authorization`、`Cookie`、`Set-Cookie`、`X-API-Key`、`X-Auth-Token`、`Proxy-Authorization` 记为 `***` |
| 请求体 | **仅当 `LogSensitiveData = true` 时**才读取并记录 |
| 响应体 | 开了响应日志就完整记录，**不做截断**（`MaxResponseContentLength` 不作用于日志） |
| 日志级别 | 响应 2xx 用 `Information`，否则 `Warning`；异常用 `Error` |
| 关联 | 每次请求自动带 `X-Request-Id` 头，日志用同一个值串起请求与响应 |

::: warning `LogSensitiveData` 是生产禁区
打开它会把 `Authorization` 头和完整请求体明文写进日志。它默认为 `false`，只在本地排障时临时开。
:::

跨服务追链路用 `WithCorrelationId()`，它写的是 `X-Correlation-Id` 头（不传参时自动生成 GUID）。

## 上传、下载与批量

```csharp
// 单文件上传，附带表单字段
await using var fs = File.OpenRead(@"D:\report.xlsx");
var upload = await _http.UploadFileAsync<UploadResultDto>(
    "https://api.example.com/files",
    fs, "report.xlsx", fieldName: "file",
    additionalData: new Dictionary<string, string> { ["category"] = "monthly" });

// 下载到磁盘，带进度
var progress = new Progress<long>(bytes => Console.WriteLine($"已下载 {bytes} 字节"));
await _http.DownloadFileAsync(
    "https://example.com/large.zip", @"D:\downloads\large.zip", progress);

// 批量并发，默认并发 10
var results = await _http.BatchRequestAsync(new[]
{
    new BatchRequestInfo { Method = HttpMethod.Get, Url = "https://api.example.com/a" },
    new BatchRequestInfo { Method = HttpMethod.Get, Url = "https://api.example.com/b" },
}, maxConcurrency: 4);
```

多文件用 `UploadFilesAsync<T>(url, IEnumerable<FileUploadInfo> files, ...)`。批量请求的返回类型固定是 `HttpResult<object>`，其中 `Data` 是反序列化出的动态对象，不是强类型 DTO；需要强类型就自己 `Task.WhenAll` 拼多个 `GetAsync<T>`。

::: warning 大响应体走 `DownloadFileAsync`，不要用 `GetStreamAsync`
`SendRequestAsync` 会先把响应完整读成字符串填进 `RawDataString`，再反序列化成 `T`，方法返回前响应对象已经释放。这意味着：

- 任何请求的响应体都会整份进内存，几十 MB 的接口要留意；
- `GetStreamAsync` 拿到的流依附于已释放的响应，不适合当作持续读取的下载流。

需要流式落盘时用 `DownloadFileAsync`——它走 `HttpCompletionOption.ResponseHeadersRead` + 8 KB 缓冲逐段写文件，并逐段回调 `IProgress<long>`。
:::

## 代理（可选）

两种用法：请求级直接指定，或从代理池取。

```csharp
// 指定代理
var direct = new XiHanHttpRequestOptions()
    .SetProxy(new ProxyConfiguration { Host = "127.0.0.1", Port = 7890, Type = ProxyType.Http });

// 从代理池取下一个（需 XiHan:Http:ProxyPool:Enabled = true 且池中有代理）
var pooled = new XiHanHttpRequestOptions().EnableProxyPool();
```

代理池由 `IProxyPoolManager` 管理，选择策略 `ProxySelectionStrategy` 支持 `RoundRobin` / `Random` / `LeastUsed` / `FastestResponse` / `Priority`；`IProxyValidator` 负责可用性校验；`Enabled` 与 `EnableHealthCheck` 同时为真时才注册后台服务 `ProxyPoolHealthCheckService`。每次请求结束会把成败与耗时回写给代理池（`RecordProxyResult`）。

::: danger 走代理 = 绕过命名客户端
一旦请求指定了 `Proxy`、启用了 `UseProxyPool` 并真的取到代理，或者 `ValidateSslCertificate` 的结果与全局 `IgnoreSslErrors` 不一致，`AdvancedHttpService` 会临时 `new HttpClient(handler)` 发送这一次请求。这条路径**不经过 `IHttpClientFactory`**，因此：

- 重试、超时、熔断策略全部不生效；
- 日志中间件不生效；
- `DefaultHeaders` 与命名客户端的 `BaseAddress`、`Headers` 全部不生效（URL 必须写全）。

此外 `DownloadFileAsync` 始终走命名客户端，不处理 `Proxy` / `UseProxyPool`。
:::

## 配置速览

主配置节 `XiHan:Http`，常调的几项：

| 键 | 默认值 | 说明 |
| --- | --- | --- |
| `DefaultTimeoutSeconds` | `60` | 客户端超时；Polly 超时策略取其 2 倍 |
| `RetryDelaySeconds` | `[1, 5, 10]` | 重试间隔序列，长度即重试次数 |
| `CircuitBreakerFailureThreshold` | `5` | 熔断失败阈值 |
| `CircuitBreakerDurationOfBreakSeconds` | `10` | 熔断断开时长 |
| `EnableRequestLogging` / `EnableResponseLogging` | `true` | 请求 / 响应日志开关 |
| `LogSensitiveData` | `false` | 明文记录敏感头与请求体 |
| `MaxResponseContentLength` | `4096` | 失败响应体拼进 `ErrorMessage` 时的截断长度 |
| `ClientLifetimeMinutes` | `5` | 命名客户端处理器生存期 |
| `IgnoreSslErrors` | `false` | 忽略证书错误（不作用于 `Local`） |
| `DefaultHeaders` | 空 | 所有命名客户端的默认请求头 |
| `Clients` | 空 | 自定义命名客户端 |

```json
{
  "XiHan": {
    "Http": {
      "DefaultTimeoutSeconds": 60,
      "RetryDelaySeconds": [1, 5, 10],
      "CircuitBreakerFailureThreshold": 5,
      "CircuitBreakerDurationOfBreakSeconds": 10,
      "EnableRequestLogging": true,
      "LogSensitiveData": false,
      "DefaultHeaders": { "User-Agent": "XiHan/1.0" },
      "Clients": {},
      "ProxyPool": { "Enabled": false }
    }
  }
}
```

完整配置项（含代理池全部字段）见 [Http 包文档](../packages/http)。

## 常见问题

| 现象 | 原因 | 处理 |
| --- | --- | --- |
| 调 `AsHttp()` 抛 `InvalidOperationException` | 应用尚未走完 `OnApplicationInitialization` | 改注入 `IAdvancedHttpService` |
| 改了 `RetryCount` 没有任何变化 | 该项不被策略构建读取 | 改 `RetryDelaySeconds` 的元素个数 |
| 上游持续挂掉，熔断器却从不打开 | 熔断策略每次请求新建，计数不跨请求累积 | 为该客户端追加静态熔断策略，或在网关层熔断 |
| 配了重试但只跑了一次就返回超时 | 请求级 `Timeout` 是含重试的总预算 | 放大请求级超时，或缩短 `RetryDelaySeconds` |
| 自定义客户端完全没有超时保护 | `EnableRetry` 与 `EnableCircuitBreaker` 同为 `false`，整段 Polly 被跳过 | 至少保留一项为 `true`，或依赖 `TimeoutSeconds` |
| 走代理后重试 / 日志全没了 | 代理路径使用临时 `HttpClient`，不经工厂 | 已知边界；代理场景请自行判断失败并重发 |
| `UseClient("x")` 之后 `BaseAddress` 没拼上 | 该次请求命中了代理 / SSL 临时客户端分支 | URL 写全，或去掉代理与 `ValidateSslCertificate` |
| 下载大文件内存飙升 | 用了 `GetStreamAsync` / `GetBytesAsync`，响应会整份进内存 | 换 `DownloadFileAsync` |
| `Local` 客户端仍校验证书 | `Local` 未配置主消息处理器 | 用 `Remote` 或自定义客户端，并配 `IgnoreSslErrors` |
| 日志里看不到请求体 | `LogSensitiveData` 为 `false` | 仅本地排障时临时打开 |

## 下一步

- [Http 包文档](../packages/http)：完整 API 清单与全部配置项
- [动态 API](./dynamic-api)：把自己的应用服务暴露成 HTTP 端点
- [Web 应用开发](./web)：入站请求管道与中间件顺序
- [配置与选项](./configuration)：Options 绑定与配置节约定
- [Serialization 包文档](../packages/serialization)：请求 / 响应的 JSON 与动态对象处理
- [Web.Gateway 包文档](../packages/web-gateway)：网关侧的流量治理与转发
