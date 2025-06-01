# XiHan.Framework.Http

曦寒框架的企业级 HTTP 客户端库，基于 Polly 提供强大的弹性策略支持。

## 功能特性

### 🚀 核心功能

- **多种 HTTP 方法支持**：GET、POST、PUT、PATCH、DELETE、HEAD、OPTIONS
- **文件上传下载**：支持单文件和多文件上传，带进度回调的文件下载
- **批量请求**：支持并发控制的批量 HTTP 请求
- **灵活配置**：支持全局和单个请求级别的配置
- **流畅 API**：支持链式调用，让 HTTP 请求更加直观和简洁

### 🛡️ 弹性策略

- **重试机制**：基于 Polly 的智能重试策略
- **熔断器**：防止级联故障的熔断器模式
- **超时控制**：请求级别的超时设置
- **SSL 证书验证**：可配置的 SSL 证书验证策略

### 📊 监控与日志

- **详细日志记录**：请求/响应的完整日志
- **性能监控**：请求耗时统计
- **敏感数据保护**：可配置的敏感信息脱敏
- **请求追踪**：支持请求 ID 追踪

### ⚙️ 企业级特性

- **配置化管理**：支持 appsettings.json 配置
- **依赖注入**：完整的 DI 容器支持
- **多客户端管理**：支持多个预配置的 HTTP 客户端
- **中间件支持**：可扩展的 HTTP 中间件架构

## 快速开始

### 1. 安装包

```xml
<PackageReference Include="XiHan.Framework.Http" Version="1.0.0" />
```

### 2. 配置服务

```csharp
// Program.cs 或 Startup.cs
services.AddModule<XiHanHttpModule>();
```

### 3. 配置文件

在 `appsettings.json` 中添加配置：

```json
{
  "XiHan": {
    "Http": {
      "DefaultTimeoutSeconds": 30,
      "RetryCount": 3,
      "RetryDelaySeconds": [1, 5, 10],
      "CircuitBreakerFailureThreshold": 5,
      "CircuitBreakerSamplingDurationSeconds": 60,
      "CircuitBreakerMinimumThroughput": 10,
      "CircuitBreakerDurationOfBreakSeconds": 30,
      "EnableRequestLogging": true,
      "EnableResponseLogging": true,
      "LogSensitiveData": false,
      "MaxResponseContentLength": 4096,
      "ClientLifetimeMinutes": 5,
      "IgnoreSslErrors": false,
      "DefaultHeaders": {
        "Accept": "application/json",
        "User-Agent": "XiHan.Framework.Http/1.0"
      },
      "Clients": {
        "ApiClient": {
          "BaseAddress": "https://api.example.com",
          "TimeoutSeconds": 60,
          "Headers": {
            "X-API-Version": "v1"
          },
          "EnableRetry": true,
          "EnableCircuitBreaker": true,
          "IgnoreSslErrors": false
        }
      }
    }
  }
}
```

## 使用示例

### 🔗 链式调用 API(推荐)

#### 基础用法

```csharp
// 简单的GET请求
var user = await "https://api.example.com/users/123"
    .SetAuthorization("your-token")
    .GetAsync<User>();

// 带查询参数的GET请求
var users = await "https://api.example.com/users"
    .SetQuery("page", "1")
    .SetQuery("size", "20")
    .SetAuthorization("your-token")
    .GetAsync<List<User>>();

// POST请求
var newUser = await "https://api.example.com/users"
    .SetHeaders(new Dictionary<string, string>
    {
        ["Authorization"] = "Bearer your-token",
        ["Content-Type"] = "application/json"
    })
    .SetBody(new { Name = "张三", Email = "zhangsan@example.com" })
    .PostAsync<User>();
```

#### 高级链式调用

```csharp
// 完整的链式调用示例
var response = await "https://api.example.com/orders"
    .SetHeaders(new Dictionary<string, string>
    {
        ["Accept"] = "application/json",
        ["Accept-Language"] = "zh-CN"
    })
    .SetAuthorization("your-token")
    .SetQueries(new Dictionary<string, string>
    {
        ["status"] = "pending",
        ["date_from"] = "2024-01-01",
        ["date_to"] = "2024-12-31"
    })
    .SetTimeout(30)
    .UseClient("ApiClient")
    .WithVerboseLogging()
    .GetAsync<List<Order>>();

// 表单数据提交
var loginResult = await "https://api.example.com/auth/login"
    .SetFormData(new Dictionary<string, string>
    {
        ["username"] = "admin",
        ["password"] = "password123",
        ["grant_type"] = "password"
    })
    .PostAsync<LoginResponse>();

// 文件下载
var downloadResult = await "https://api.example.com/files/123/download"
    .SetAuthorization("your-token")
    .DownloadAsync("./downloads/file.pdf", new Progress<long>(bytes =>
    {
        Console.WriteLine($"已下载: {bytes} 字节");
    }));
```

#### 条件链式调用

```csharp
var token = "your-token";
var includeDetails = true;

var builder = "https://api.example.com/products"
    .AsHttp()
    .SetQuery("category", "electronics");

// 条件性添加参数
if (!string.IsNullOrEmpty(token))
{
    builder.SetAuthorization(token);
}

if (includeDetails)
{
    builder.SetQuery("include", "details,reviews");
}

var products = await builder.GetAsync<List<Product>>();
```

### 传统服务注入方式

```csharp
public class UserService
{
    private readonly IAdvancedHttpService _httpService;

    public UserService(IAdvancedHttpService httpService)
    {
        _httpService = httpService;
    }

    // GET 请求
    public async Task<User> GetUserAsync(int userId)
    {
        var result = await _httpService.GetAsync<User>($"https://api.example.com/users/{userId}");

        if (result.IsSuccess)
        {
            return result.Data;
        }

        throw new HttpRequestException(result.ErrorMessage);
    }

    // POST 请求
    public async Task<User> CreateUserAsync(CreateUserRequest request)
    {
        var options = new HttpRequestOptions()
            .AddHeader("Authorization", "Bearer token")
            .SetTimeout(TimeSpan.FromSeconds(30));

        var result = await _httpService.PostAsync<CreateUserRequest, User>(
            "https://api.example.com/users",
            request,
            options);

        return result.Data;
    }
}
```

### 文件操作

```csharp
// 上传文件(使用链式调用)
using var fileStream = File.OpenRead("document.pdf");
var uploadResult = await "https://api.example.com/upload"
    .SetAuthorization("your-token")
    .AsHttp()
    .UploadFileAsync<UploadResponse>(fileStream, "document.pdf");

// 批量上传
var files = new[]
{
    new FileUploadInfo { FileStream = stream1, FileName = "file1.pdf", FieldName = "file1" },
    new FileUploadInfo { FileStream = stream2, FileName = "file2.pdf", FieldName = "file2" }
};

var batchUploadResult = await "https://api.example.com/upload/batch"
    .SetAuthorization("your-token")
    .AsHttp()
    .UploadFilesAsync<BatchUploadResponse>(files);
```

### 批量请求

```csharp
// 使用链式调用进行批量请求
var userIds = new[] { 1, 2, 3, 4, 5 };
var tasks = userIds.Select(async id =>
{
    return await $"https://api.example.com/users/{id}"
        .SetAuthorization("your-token")
        .SetTimeout(5)
        .GetAsync<User>();
});

var results = await Task.WhenAll(tasks);
var successfulUsers = results
    .Where(r => r.IsSuccess)
    .Select(r => r.Data)
    .Where(u => u != null)
    .ToList();
```

## 错误处理

```csharp
// 使用扩展方法进行错误处理
try
{
    var user = await "https://api.example.com/users/123"
        .SetAuthorization("your-token")
        .GetAsync<User>()
        .ContinueWith(task => task.Result.GetDataOrThrow());
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"请求失败: {ex.Message}");
}

// 使用默认值
var userWithDefault = await "https://api.example.com/users/123"
    .SetAuthorization("your-token")
    .GetAsync<User>()
    .ContinueWith(task => task.Result.GetDataOrDefault(new User { Name = "默认用户" }));

// 检查状态码
var result = await "https://api.example.com/users/123"
    .SetAuthorization("your-token")
    .GetAsync<User>();

if (result.IsClientError())
{
    Console.WriteLine("客户端错误");
}
else if (result.IsServerError())
{
    Console.WriteLine("服务器错误");
}
```

## 链式调用 API 参考

### URL 扩展方法

```csharp
// 基础设置
"https://api.example.com/users"
    .SetHeader("name", "value")                    // 设置单个请求头
    .SetHeaders(headers)                           // 设置多个请求头
    .SetAuthorization("token", "Bearer")           // 设置授权头
    .SetBasicAuth("username", "password")          // 设置基本认证
    .SetQuery("name", "value")                     // 设置单个查询参数
    .SetQueries(parameters)                        // 设置多个查询参数
    .SetBody(object, "application/json")           // 设置请求体
    .SetJsonBody(object)                           // 设置JSON请求体
    .SetFormData(formData)                         // 设置表单数据
    .SetTimeout(30)                                // 设置超时时间(秒)
    .SetTimeout(TimeSpan.FromSeconds(30))          // 设置超时时间
    .UseClient("ApiClient")                        // 使用指定客户端
    .WithVerboseLogging()                          // 启用详细日志
    .WithoutLogging()                              // 禁用日志
    .WithoutRetry()                                // 禁用重试
    .WithoutCircuitBreaker()                       // 禁用熔断器
    .WithoutCache()                                // 禁用缓存
    .SetCorrelationId()                            // 设置关联ID
    .SetUserAgent("MyApp/1.0")                     // 设置用户代理
    .SetLanguage("zh-CN");                         // 设置语言
```

### HTTP 方法

```csharp
// GET请求
await url.GetAsync<T>();
await url.GetStringAsync();
await url.GetBytesAsync();

// POST请求
await url.PostAsync<T>();
await url.PostAsync<T>(body);
await url.PostStringAsync();

// PUT请求
await url.PutAsync<T>(body);

// PATCH请求
await url.PatchAsync<T>(body);

// DELETE请求
await url.DeleteAsync<T>();
await url.DeleteAsync();

// HEAD请求
await url.HeadAsync();

// OPTIONS请求
await url.OptionsAsync();

// 下载文件
await url.DownloadAsync("path", progress);
```

## 性能优化建议

### 1. 连接池管理

- 合理设置 `ClientLifetimeMinutes` 避免频繁创建连接
- 使用预配置的命名客户端减少配置开销

### 2. 重试策略

- 根据业务场景调整重试次数和间隔
- 对于幂等操作启用重试，非幂等操作谨慎使用

### 3. 熔断器配置

- 根据服务的 SLA 设置合适的失败阈值
- 监控熔断器状态，及时发现服务问题

### 4. 日志配置

- 生产环境关闭敏感数据日志
- 合理设置响应内容长度限制

## 扩展开发

### 自定义中间件

```csharp
public class CustomHeaderMiddleware : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // 添加自定义请求头
        request.Headers.Add("X-Custom-Header", "CustomValue");

        // 调用下一个处理器
        var response = await base.SendAsync(request, cancellationToken);

        // 处理响应
        return response;
    }
}

// 注册中间件
services.AddHttpClient("CustomClient")
    .AddHttpMessageHandler<CustomHeaderMiddleware>();
```

### 自定义扩展方法

```csharp
public static class CustomHttpExtensions
{
    public static HttpRequestBuilder WithApiKey(this HttpRequestBuilder builder, string apiKey)
    {
        return builder.SetHeader("X-API-Key", apiKey);
    }

    public static HttpRequestBuilder WithTenant(this string url, string tenantId)
    {
        return url.AsHttp().SetHeader("X-Tenant-ID", tenantId);
    }
}

// 使用自定义扩展
var result = await "https://api.example.com/data"
    .WithTenant("tenant-123")
    .WithApiKey("api-key-456")
    .GetAsync<Data>();
```

## 最佳实践

1. **优先使用链式调用 API**：代码更简洁、可读性更好
2. **合理使用客户端配置**：为不同的 API 服务配置专用客户端
3. **统一错误处理**：使用扩展方法进行统一的错误处理
4. **条件链式调用**：根据业务逻辑动态构建请求
5. **性能监控**：监控请求耗时和成功率
6. **安全考虑**：保护敏感信息，验证 SSL 证书

## 故障排除

### 常见问题

1. **HTTP 服务未初始化**

   ```
   错误：HTTP服务未初始化。请在应用启动时调用 StringHttpExtensions.SetHttpService() 方法。
   解决：确保已正确配置 XiHanHttpModule
   ```

2. **连接超时**

   - 检查网络连接
   - 调整超时设置：`.SetTimeout(60)`
   - 确认目标服务可用性

3. **SSL 证书错误**

   - 验证证书有效性
   - 开发环境可使用：`.UseClient("LocalClient")` 并配置忽略 SSL 错误

4. **序列化错误**

   - 检查数据模型定义
   - 确认 JSON 格式正确

## 版本历史

- **v1.0.0**: 初始版本，包含基础 HTTP 功能和 Polly 集成
- **v1.1.0**: 新增流畅 API 和链式调用支持
- 更多版本信息请查看 [CHANGELOG.md](CHANGELOG.md)

## 贡献指南

欢迎提交 Issue 和 Pull Request 来改进这个库。请确保：

1. 遵循现有的代码风格
2. 添加适当的单元测试
3. 更新相关文档

## 许可证

本项目采用 MIT 许可证。详情请查看 [LICENSE](LICENSE) 文件。
