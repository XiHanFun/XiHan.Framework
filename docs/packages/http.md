# XiHan.Framework.Http

> 基于 `IHttpClientFactory` 与 Polly 的高级 HTTP 客户端：统一结果封装、韧性策略（重试/超时/熔断）、字符串链式请求、上传下载、批量并发与代理池。

- **NuGet**：`XiHan.Framework.Http`
- **模块类**：`XiHanHttpModule`
- **所在层**：基础设施层
- **关键依赖**：**Polly**（经 `Microsoft.Extensions.Http.Polly`，提供重试/超时/熔断策略）；框架内部依赖 Core / Serialization

## 概述

这个包在 `IHttpClientFactory` 之上封装了一套「开箱即用」的 HTTP 客户端。它替你把命名客户端的注册、超时、请求/响应日志、以及基于 **Polly** 的重试、超时、熔断策略都配置好，所有调用都返回统一的 `HttpResult<T>`（成功/失败、状态码、数据、耗时、异常一体）。

对外提供两种入口：一是注入高级服务 `IAdvancedHttpService` 直接发各类请求（全 HTTP 动词、JSON/表单/字节/流、文件上传下载带进度、批量并发）；二是字符串链式扩展 `"url".AsHttp()...`，无需注入即可流式构建并发送请求。此外内置可选的代理池管理与后台健康检查。

## 何时使用

- 需要调用外部 API，又不想手写 `HttpClient` 注册、超时、重试、熔断这些样板。
- 需要开箱即用的韧性：瞬时错误自动重试、失败过多自动熔断，且能按单次请求临时关闭。
- 需要统一的请求/响应日志、文件上传下载（带进度）、批量并发请求。
- 需要通过代理池发请求，并对代理做可用性校验与健康检查。
- 需要在没有构造函数注入的静态/工具代码里，用 `"url".AsHttp()` 快速发请求。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Http
```

```csharp
[DependsOn(typeof(XiHanHttpModule))]
public class MyModule : XiHanModule { }
```

模块启用后：

- `ConfigureServices` 调用 `AddXiHanHttpModule(configuration)`：绑定 `XiHanHttpClientOptions`（配置节 `XiHan:Http`）和 `XiHanProxyPoolOptions`（配置节 `XiHan:Http:ProxyPool`）；注册 `IProxyValidator`/`IProxyPoolManager`（Singleton）、`IAdvancedHttpService`（Scoped）；配置 `Remote`、`Local` 及配置里声明的自定义命名客户端；当代理池同时启用 `Enabled` 与 `EnableHealthCheck` 时注册后台服务 `ProxyPoolHealthCheckService`。
- `OnApplicationInitialization` 调用 `StringHttpExtensions.Initialize(context.ServiceProvider)`，让字符串链式扩展 `AsHttp()` 可用（内部缓存 `IAdvancedHttpService` 实例）。

> `XiHanHttpModule` 依赖 `XiHanSerializationModule`，会随之一并启用。

## 工作原理

- **命名客户端与分组**：模块为 `HttpGroupEnum.Remote`（默认，纯远程调用）和 `HttpGroupEnum.Local`（`BaseAddress = http://127.0.0.1`，本地服务间调用）各注册一个命名 `HttpClient`，再为 `XiHanHttpClientOptions.Clients` 里声明的每个自定义客户端注册一个。发请求时默认用 `Remote`；若请求选项里带了 `ClientName` 标签（由 `UseClient(name)` 设置），则改用该命名客户端。
- **韧性策略（Polly）**：每个客户端按顺序挂三段策略——
  - **重试**：`HttpPolicyExtensions.HandleTransientHttpError().Or<TimeoutRejectedException>().WaitAndRetryAsync(delays)`，延迟序列来自 `RetryDelaySeconds`（如 `[1,5,10]` 即重试 3 次，间隔 1/5/10 秒）。
  - **超时**：`Policy.TimeoutAsync<HttpResponseMessage>(2 * DefaultTimeoutSeconds)`。
  - **熔断**：`HandleTransientHttpError().CircuitBreakerAsync(CircuitBreakerFailureThreshold, TimeSpan.FromSeconds(CircuitBreakerDurationOfBreakSeconds), ...)`。
- **按请求关闭策略**：重试与熔断用「动态策略选择器」注册——它读取 `HttpRequestMessage.Options` 里的 `EnableRetry`/`EnableCircuitBreaker` 标志，若为 `false` 则退化成 `Policy.NoOpAsync`。因此 `WithoutRetry()` / `WithoutCircuitBreaker()` 能在不改全局配置的前提下，对单次请求关闭对应策略。
- **日志中间件**：每个客户端挂一个 `HttpLoggingMiddleware`（`DelegatingHandler`），按 `EnableRequestLogging`/`EnableResponseLogging` 记录请求/响应，响应体按 `MaxResponseContentLength` 截断；`Authorization`、`Cookie`、`X-API-Key` 等敏感头默认脱敏为 `***`，仅当 `LogSensitiveData=true` 时明文输出。请求级 `LogRequest`/`LogResponse` 可覆盖全局开关。
- **代理**：请求选项可直接指定 `Proxy`，或设 `UseProxyPool=true` 从 `IProxyPoolManager` 取下一个代理（需注入了代理池管理器）。

## 核心能力

- **高级 HTTP 服务** `IAdvancedHttpService`：GET/POST/PUT/PATCH/DELETE/HEAD/OPTIONS，JSON/表单/字节/流，文件上传（单/多，带附加字段）、下载（带进度回调）、批量并发请求，统一返回 `HttpResult<T>`。
- **字符串链式扩展** `AsHttp()` + `HttpRequestBuilder`：`"https://api...".AsHttp().SetHeader(...).SetJsonBody(...).PostAsync<T>()`，无需注入即可发请求。
- **请求选项流式扩展**（`HttpServiceExtensions`）：`WithAuthorization` / `WithBasicAuth` / `AsJson` / `AsXml` / `AsForm` / `WithUserAgent` / `WithCorrelationId` / `WithLanguage` / `WithoutRetry` / `WithoutCircuitBreaker` / `WithoutCache` 等。
- **结果扩展**：`GetDataOrThrow` / `GetDataOrDefault` / `IsSuccessStatusCode` / `IsClientError` / `IsServerError` / `GetHeader` / `GetContentType` / `GetContentLength`。
- **便捷方法**（`HttpServiceConvenienceExtensions`）：`QuickGetAsync` / `QuickPostAsync` / `QuickPutAsync` / `QuickDeleteAsync` / `GetPagedAsync`（可选传 `token` 自动加授权头）。
- **Polly 韧性策略**：重试 + 超时 + 熔断，可按请求上下文关闭。
- **命名客户端分组** `HttpGroupEnum`：`Remote` / `Local` 及自定义客户端，各自独立超时/请求头/策略。
- **代理池**：`IProxyPoolManager` / `IProxyValidator` + `ProxyPoolHealthCheckService` 后台健康检查，支持多种选择策略。

## 主要 API / 类型

### 高级服务 `IAdvancedHttpService`

所有方法均可传可选的 `XiHanHttpRequestOptions? options` 与 `CancellationToken`。

| 方法 | 说明 |
| --- | --- |
| `Task<HttpResult<T>> GetAsync<T>(string url, ...)` | GET，反序列化为 `T` |
| `Task<HttpResult<string>> GetStringAsync(string url, ...)` | GET，返回原始字符串 |
| `Task<HttpResult<byte[]>> GetBytesAsync(string url, ...)` | GET，返回字节数组 |
| `Task<HttpResult<Stream>> GetStreamAsync(string url, ...)` | GET，返回流 |
| `Task<HttpResult<TResponse>> PostAsync<TRequest, TResponse>(string url, TRequest request, ...)` | POST 对象（JSON 序列化） |
| `Task<HttpResult<T>> PostJsonAsync<T>(string url, string jsonContent, ...)` | POST 原始 JSON 字符串 |
| `Task<HttpResult<T>> PostFormAsync<T>(string url, Dictionary<string, string> formData, ...)` | POST 表单 |
| `Task<HttpResult<TResponse>> PutAsync<TRequest, TResponse>(string url, TRequest request, ...)` | PUT |
| `Task<HttpResult<TResponse>> PatchAsync<TRequest, TResponse>(string url, TRequest request, ...)` | PATCH |
| `Task<HttpResult<T>> DeleteAsync<T>(string url, ...)` / `Task<HttpResult> DeleteAsync(string url, ...)` | DELETE（有/无响应体） |
| `Task<HttpResult> HeadAsync(string url, ...)` / `Task<HttpResult> OptionsAsync(string url, ...)` | HEAD / OPTIONS |
| `Task<HttpResult<T>> UploadFileAsync<T>(string url, Stream fileStream, string fileName, string fieldName = "file", Dictionary<string, string>? additionalData = null, ...)` | 上传单文件 |
| `Task<HttpResult<T>> UploadFilesAsync<T>(string url, IEnumerable<FileUploadInfo> files, Dictionary<string, string>? additionalData = null, ...)` | 上传多文件 |
| `Task<HttpResult> DownloadFileAsync(string url, string destinationPath, IProgress<long>? progress = null, ...)` | 下载到文件，带进度回调 |
| `Task<IEnumerable<HttpResult<object>>> BatchRequestAsync(IEnumerable<BatchRequestInfo> requests, int maxConcurrency = 10, ...)` | 批量并发请求 |

### 结果封装 `HttpResult<T>` / `HttpResult`

| 成员 | 说明 |
| --- | --- |
| `bool IsSuccess` | 是否成功 |
| `HttpStatusCode StatusCode` | HTTP 状态码 |
| `T? Data` | 反序列化后的数据 |
| `string? RawDataString` | 原始响应字符串 |
| `string? ErrorMessage` / `Exception? Exception` | 错误消息 / 异常 |
| `Dictionary<string, IEnumerable<string>> Headers` | 响应头 |
| `long ElapsedMilliseconds` | 请求耗时（毫秒） |
| `string? RequestUrl` / `string? RequestMethod` | 请求 Url / 方法 |
| `static HttpResult<T> Success(T data, ...)` / `static HttpResult<T> Failure(string errorMessage, ...)` | 工厂方法 |

`HttpResult`（无泛型）继承 `HttpResult<object>`，用于无响应体的 HEAD/OPTIONS/DELETE 等。

### 字符串链式 `HttpRequestBuilder`（由 `AsHttp()` 创建）

- 头/查询：`SetHeader` / `SetHeaders` / `SetQuery` / `SetQueries`
- 认证：`SetAuthorization(token, scheme = "Bearer")` / `SetBasicAuth(username, password)`
- 请求体：`SetBody(object, contentType)` / `SetBodyContent(string, contentType)` / `SetJsonBody(object)` / `SetFormData(Dictionary<string, string>)`
- 控制：`SetTimeout(TimeSpan/int)` / `UseClient(clientName)` / `WithoutRetry()` / `WithoutCircuitBreaker()` / `WithoutCache()` / `WithVerboseLogging()` / `WithoutLogging()` / `ValidateSslCertificate(bool)` / `IgnoreSslErrors()`
- 元数据：`SetRequestId` / `SetCorrelationId` / `SetUserAgent` / `SetLanguage` / `AddTag`
- 发送：`GetAsync<T>` / `GetStringAsync` / `GetDynamicAsync` / `GetBytesAsync` / `PostAsync<T>` / `PostStringAsync` / `PostDynamicAsync` / `PutAsync<T>` / `PatchAsync<T>` / `DeleteAsync<T>` / `DeleteAsync` / `HeadAsync` / `OptionsAsync` / `DownloadAsync`

> `string` 上也有一批直达扩展（`StringHttpExtensions`），如 `"url".GetAsync<T>()`、`"url".PostAsync<T>(body)`、`"url".PutAsync<T>(body)`、`"url".DeleteAsync()`、`"url".DownloadAsync(path)`，以及返回 `HttpRequestBuilder` 的 `SetHeader`/`SetQuery`/`SetJsonBody`/`SetFormData`/`SetTimeout`/`UseClient` 等，用于继续链式。

### 代理池

| 类型 | 关键成员 |
| --- | --- |
| `IProxyPoolManager` | `GetNextProxy()`、`GetAvailableProxies()`、`Task<bool> AddProxyAsync(ProxyConfiguration)`、`RemoveProxy(string)`、`RecordProxyResult(string, bool, long)`、`MarkProxyUnavailable/Available(string)`、`GetProxyStatistics/GetAllStatistics`、`Task StartHealthCheckAsync(...)`、`StopHealthCheck()`、`GetPoolSize()`、`GetAvailableCount()` |
| `IProxyValidator` | `Task<ProxyValidationResult> ValidateAsync(ProxyConfiguration, string testUrl, int timeoutSeconds = 10, ...)`、`Task<IEnumerable<ProxyValidationResult>> ValidateBatchAsync(..., int maxConcurrency = 10, ...)` |
| `ProxyPoolHealthCheckService` | `BackgroundService`，按 `HealthCheckIntervalSeconds` 周期校验代理健康（仅在启用时注册） |
| `ProxyConfiguration` | 代理配置：`Host`/`Port`/`Type`/`Username`/`Password`/`Priority`/`Enabled` 等，`GetProxyAddress()`/`GetProxyUri()`/`Validate()` |
| `ProxySelectionStrategy` | `RoundRobin` / `Random` / `LeastUsed` / `FastestResponse` / `Priority` |
| `ProxyType` | `Http` / `Https` / `Socks4` / `Socks4A` / `Socks5` |

## 配置

主配置节 `XiHan:Http`（`XiHanHttpClientOptions`）：

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `DefaultTimeoutSeconds` | `int` | `60` | 默认超时（秒），范围 1–300；超时策略取其 2 倍 |
| `RetryCount` | `int` | `3` | 重试次数（范围 0–10） |
| `RetryDelaySeconds` | `int[]` | `[1, 5, 10]` | 每次重试的间隔序列（秒），决定实际重试次数 |
| `CircuitBreakerFailureThreshold` | `int` | `5` | 熔断失败阈值（范围 1–100） |
| `CircuitBreakerSamplingDurationSeconds` | `int` | `60` | 熔断采样窗口（秒） |
| `CircuitBreakerMinimumThroughput` | `int` | `10` | 熔断最小吞吐量 |
| `CircuitBreakerDurationOfBreakSeconds` | `int` | `10` | 熔断断开持续时间（秒） |
| `EnableRequestLogging` | `bool` | `true` | 是否记录请求日志 |
| `EnableResponseLogging` | `bool` | `true` | 是否记录响应日志 |
| `LogSensitiveData` | `bool` | `false` | 是否记录敏感数据（否则敏感头脱敏） |
| `MaxResponseContentLength` | `int` | `4096` | 日志中响应内容最大长度 |
| `ClientLifetimeMinutes` | `int` | `5` | 命名客户端处理器生存期（分钟） |
| `IgnoreSslErrors` | `bool` | `false` | 是否忽略 SSL 证书错误 |
| `DefaultHeaders` | `Dictionary<string, string>` | 空 | 所有客户端的默认请求头 |
| `Clients` | `Dictionary<string, HttpClientConfiguration>` | 空 | 自定义命名客户端集合 |

每个自定义客户端 `HttpClientConfiguration`：`BaseAddress`、`TimeoutSeconds?`、`Headers`、`EnableRetry`（默认 `true`）、`EnableCircuitBreaker`（默认 `true`）、`IgnoreSslErrors?`。

代理池配置节 `XiHan:Http:ProxyPool`（`XiHanProxyPoolOptions`）：`Enabled`(默认 false)、`SelectionStrategy`、`Proxies`、`EnableHealthCheck`(默认 true)、`HealthCheckIntervalSeconds`(60)、`HealthCheckTimeoutSeconds`(10)、`HealthCheckUrl`(默认 `https://www.google.com`)、`MaxRetryCount`(3)、`FailureThreshold`(5)、`RecoveryTimeSeconds`(300)、`ValidateOnStartup`(true)、`AutoRemoveFailedProxy`(false)、`StatisticsRetentionSeconds`(86400) 等。仅当 `Enabled && EnableHealthCheck` 时才注册后台健康检查服务。

### 示例 appsettings.json

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
      "IgnoreSslErrors": false,
      "DefaultHeaders": {
        "User-Agent": "XiHan/1.0"
      },
      "Clients": {
        "github": {
          "BaseAddress": "https://api.github.com",
          "TimeoutSeconds": 30,
          "Headers": { "Accept": "application/vnd.github+json" },
          "EnableRetry": true,
          "EnableCircuitBreaker": true
        }
      },
      "ProxyPool": {
        "Enabled": false,
        "SelectionStrategy": "RoundRobin",
        "EnableHealthCheck": true,
        "HealthCheckIntervalSeconds": 60,
        "Proxies": []
      }
    }
  }
}
```

## 使用示例

注入 `IAdvancedHttpService` 发请求，并对单次请求关闭重试：

```csharp
public class WeatherClient
{
    private readonly IAdvancedHttpService _http;

    public WeatherClient(IAdvancedHttpService http)
    {
        _http = http;
    }

    public async Task<WeatherDto?> GetAsync(string city)
    {
        var options = new XiHanHttpRequestOptions()
            .WithAuthorization("your-token")   // 加 Bearer 授权头
            .WithoutRetry();                   // 仅本次关闭重试

        var result = await _http.GetAsync<WeatherDto>(
            $"https://api.example.com/weather/{city}", options);

        return result.GetDataOrDefault();      // 失败返回 null
    }
}
```

字符串链式扩展（应用初始化后可用），指定自定义客户端并发 POST：

```csharp
var result = await "/repos/owner/name/issues"
    .AsHttp()
    .UseClient("github")                       // 用 appsettings 里声明的命名客户端
    .SetJsonBody(new { title = "bug", body = "..." })
    .PostAsync<IssueDto>();

if (result.IsSuccess)
{
    var issue = result.Data;
}
```

下载文件并上报进度：

```csharp
var progress = new Progress<long>(bytes => Console.WriteLine($"已下载 {bytes} 字节"));
await "https://example.com/large.zip"
    .AsHttp()
    .DownloadAsync(@"D:\downloads\large.zip", progress);
```

## 扩展点 / 自定义

- **替换默认实现**：`IProxyValidator` / `IProxyPoolManager` / `IAdvancedHttpService` 均以接口注册，可在模块中 `Replace` 为自定义实现。
- **新增命名客户端**：往 `XiHan:Http:Clients` 配置节增加条目即可（含独立 `BaseAddress`、超时、请求头、重试/熔断开关），调用侧用 `UseClient(name)` 选用；无需改代码。
- **按请求覆盖行为**：通过 `XiHanHttpRequestOptions` 及其扩展在单次请求上覆盖超时、日志、重试/熔断、认证、代理等，而不动全局配置。

## 注意事项与最佳实践

- **`AsHttp()` 需先初始化**：字符串链式扩展依赖 `StringHttpExtensions.Initialize(...)`（由模块 `OnApplicationInitialization` 自动完成）。在应用完成初始化前调用会抛 `InvalidOperationException`。因此单元测试或极早期启动代码里优先注入 `IAdvancedHttpService`。
- **超时是「策略超时」的 2 倍**：Polly 超时策略取 `2 * DefaultTimeoutSeconds`，而 `HttpClient.Timeout` 本身取 `DefaultTimeoutSeconds`；排查超时时两者都要看。
- **重试次数以 `RetryDelaySeconds` 长度为准**：`RetryCount` 字段存在但实际重试次数由 `RetryDelaySeconds` 数组长度决定。
- **敏感头默认脱敏**：`Authorization`、`Cookie`、`X-API-Key`、`Proxy-Authorization` 等在日志中显示为 `***`，仅 `LogSensitiveData=true` 才明文；请求体也仅在该开关开启时记录。
- **代理池默认关闭**：`UseProxyPool=true` 仅在容器中存在可用的 `IProxyPoolManager` 且代理池已配置代理时才生效；后台健康检查仅在 `Enabled && EnableHealthCheck` 时注册。
- **HTTPS 代理需 `Local`/`Remote` 之外的处理**：SOCKS 代理类型由 `ProxyType` 表达，实际连通性以 `IProxyValidator.ValidateAsync` 校验为准。

## 依赖模块

- [XiHan.Framework.Core](./core)
- [XiHan.Framework.Serialization](./serialization)（请求/响应 JSON 及动态对象序列化）
- 第三方核心：**Polly**（经 `Microsoft.Extensions.Http.Polly`）

## 相关模块

- [XiHan.Framework.Serialization](./serialization)
- [XiHan.Framework.Core](./core)
