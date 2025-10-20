# XiHan.Framework.Http

曦寒框架网络请求库，提供强大的 HTTP 客户端功能，包括代理支持、重试机制、熔断器等企业级特性。

## 功能特性

### 核心功能

- ✅ 完整的 HTTP/HTTPS 请求支持（GET、POST、PUT、PATCH、DELETE、HEAD、OPTIONS）
- ✅ 文件上传和下载
- ✅ 流式响应处理
- ✅ 批量请求
- ✅ Fluent API 构建器模式

### 代理功能

- ✅ HTTP/HTTPS 代理支持
- ✅ SOCKS4/SOCKS4A/SOCKS5 代理支持（.NET 6+ 原生支持）
- ✅ 代理认证（用户名/密码）
- ✅ 代理池管理
- ✅ 代理健康检查
- ✅ 多种代理选择策略（轮询、随机、最少使用、最快响应、优先级）
- ✅ 代理统计信息

### 企业级特性

- ✅ Polly 重试策略
- ✅ 熔断器模式
- ✅ 超时控制
- ✅ 请求/响应日志记录
- ✅ SSL 证书验证控制
- ✅ 自定义请求头

## 快速开始

### 1. 安装

```bash
dotnet add package XiHan.Framework.Http
```

### 2. 配置

在 `appsettings.json` 中添加配置：

```json
{
  "XiHan": {
    "Http": {
      "DefaultTimeoutSeconds": 30,
      "RetryCount": 3,
      "RetryDelaySeconds": [1, 5, 10],
      "CircuitBreakerFailureThreshold": 5,
      "CircuitBreakerDurationOfBreakSeconds": 30,
      "EnableRequestLogging": true,
      "EnableResponseLogging": true,
      "IgnoreSslErrors": false,
      "DefaultHeaders": {
        "Accept": "application/json",
        "User-Agent": "XiHan.Framework.Http/1.4.1"
      }
    },
    "Http:ProxyPool": {
      "Enabled": true,
      "SelectionStrategy": "RoundRobin",
      "EnableHealthCheck": true,
      "HealthCheckIntervalSeconds": 60,
      "HealthCheckTimeoutSeconds": 10,
      "HealthCheckUrl": "https://www.google.com",
      "FailureThreshold": 5,
      "RecoveryTimeSeconds": 300,
      "Proxies": [
        {
          "Name": "Proxy1",
          "Host": "127.0.0.1",
          "Port": 7890,
          "Type": "Http",
          "Enabled": true,
          "Priority": 0,
          "Username": "",
          "Password": ""
        }
      ]
    }
  }
}
```

### 3. 注册服务

在 `Program.cs` 或模块中注册：

```csharp
services.AddXiHanHttpModule(configuration);
```

### 4. 使用

#### 基本使用

```csharp
public class MyService
{
    private readonly IAdvancedHttpService _httpService;

    public MyService(IAdvancedHttpService httpService)
    {
        _httpService = httpService;
    }

    public async Task<MyData> GetDataAsync()
    {
        var result = await _httpService.GetAsync<MyData>("https://api.example.com/data");

        if (result.IsSuccess)
        {
            return result.Data;
        }

        throw new Exception(result.ErrorMessage);
    }
}
```

#### 使用代理池

```csharp
// 自动从代理池获取可用代理
var options = new XiHanHttpRequestOptions()
    .EnableProxyPool();

var result = await _httpService.GetAsync<MyData>(
    "https://api.example.com/data",
    options);
```

#### 使用指定代理

```csharp
var proxy = new ProxyConfiguration
{
    Host = "127.0.0.1",
    Port = 7890,
    Type = ProxyType.Http,
    Username = "user",
    Password = "pass"
};

var options = new XiHanHttpRequestOptions()
    .SetProxy(proxy);

var result = await _httpService.GetAsync<MyData>(
    "https://api.example.com/data",
    options);
```

#### POST 请求

```csharp
var requestData = new { Name = "Test", Age = 25 };

var result = await _httpService.PostAsync<MyRequest, MyResponse>(
    "https://api.example.com/data",
    requestData);
```

#### 文件上传

```csharp
using var fileStream = File.OpenRead("file.txt");

var result = await _httpService.UploadFileAsync<UploadResponse>(
    "https://api.example.com/upload",
    fileStream,
    "file.txt");
```

#### 文件下载

```csharp
var progress = new Progress<long>(bytes =>
{
    Console.WriteLine($"已下载: {bytes} 字节");
});

var result = await _httpService.DownloadFileAsync(
    "https://api.example.com/file.zip",
    "local-file.zip",
    progress);
```

#### 批量请求

```csharp
var requests = new[]
{
    new BatchRequestInfo
    {
        Url = "https://api.example.com/data1",
        Method = HttpMethod.Get
    },
    new BatchRequestInfo
    {
        Url = "https://api.example.com/data2",
        Method = HttpMethod.Get
    }
};

var results = await _httpService.BatchRequestAsync(requests, maxConcurrency: 5);
```

## 代理池管理

### 代理选择策略

- **RoundRobin**: 轮询选择代理
- **Random**: 随机选择代理
- **LeastUsed**: 选择使用次数最少的代理
- **FastestResponse**: 选择响应最快的代理
- **Priority**: 按优先级选择代理

### 代理统计

```csharp
public class ProxyMonitorService
{
    private readonly IProxyPoolManager _proxyPoolManager;

    public ProxyMonitorService(IProxyPoolManager proxyPoolManager)
    {
        _proxyPoolManager = proxyPoolManager;
    }

    public void ShowStatistics()
    {
        var stats = _proxyPoolManager.GetAllStatistics();

        foreach (var stat in stats)
        {
            Console.WriteLine($"代理: {stat.ProxyAddress}");
            Console.WriteLine($"总请求: {stat.TotalRequests}");
            Console.WriteLine($"成功率: {stat.SuccessRate:P2}");
            Console.WriteLine($"平均响应时间: {stat.AverageResponseTime}ms");
            Console.WriteLine($"可用: {stat.IsAvailable}");
            Console.WriteLine("---");
        }
    }
}
```

### 动态管理代理

```csharp
// 添加代理
var newProxy = new ProxyConfiguration
{
    Host = "192.168.1.100",
    Port = 8080,
    Type = ProxyType.Http
};

await _proxyPoolManager.AddProxyAsync(newProxy);

// 移除代理
_proxyPoolManager.RemoveProxy("http://192.168.1.100:8080");

// 标记代理不可用
_proxyPoolManager.MarkProxyUnavailable("http://127.0.0.1:7890");

// 标记代理可用
_proxyPoolManager.MarkProxyAvailable("http://127.0.0.1:7890");
```

## 高级功能

### 自定义请求头

```csharp
var options = new XiHanHttpRequestOptions()
    .AddHeader("Authorization", "Bearer token123")
    .AddHeader("X-Custom-Header", "value");

var result = await _httpService.GetAsync<MyData>(url, options);
```

### 查询参数

```csharp
var options = new XiHanHttpRequestOptions()
    .AddQueryParameter("page", "1")
    .AddQueryParameter("size", "20");

var result = await _httpService.GetAsync<MyData>(url, options);
```

### 超时控制

```csharp
var options = new XiHanHttpRequestOptions()
    .SetTimeout(TimeSpan.FromSeconds(10));

var result = await _httpService.GetAsync<MyData>(url, options);
```

### SSL 证书验证

```csharp
var options = new XiHanHttpRequestOptions
{
    ValidateSslCertificate = false  // 忽略 SSL 证书错误
};

var result = await _httpService.GetAsync<MyData>(url, options);
```

## SOCKS 代理支持

从 .NET 6 开始，`SocketsHttpHandler` 和 `HttpClientHandler` 原生支持 SOCKS4、SOCKS4A 和 SOCKS5 代理！

你可以直接使用：

```csharp
// SOCKS5 代理
var proxy = new ProxyConfiguration
{
    Host = "127.0.0.1",
    Port = 9050,
    Type = ProxyType.Socks5
};

var options = new XiHanHttpRequestOptions().SetProxy(proxy);
var result = await httpService.GetAsync<Data>(url, options);
```

**说明**:

- ✅ .NET 6+ 版本原生支持所有 SOCKS 代理类型
- ✅ 支持 SOCKS 代理认证
- ✅ 性能优秀，无需第三方依赖
- 参考: [HttpToSocks5Proxy GitHub - 已废弃，推荐使用 .NET 6+ 原生支持](https://github.com/MihaZupan/HttpToSocks5Proxy)

## 注意事项

1. 代理池健康检查会定期验证代理可用性，可能会产生额外的网络流量
2. 使用代理池时，失败的代理会被临时标记为不可用，在恢复时间后会重新尝试
3. 代理认证信息会被记录在日志中，建议在生产环境关闭敏感数据日志
4. SSL 证书验证应该仅在开发环境中关闭，生产环境请使用有效证书

## 许可证

MIT License - 查看 [LICENSE](../../../LICENSE) 文件了解详情
