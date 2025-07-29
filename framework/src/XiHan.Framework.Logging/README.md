# XiHan.Framework.Logging

曦寒框架统一日志记录与管理库，基于 .NET 9 日志框架，支持结构化日志、多目标输出，兼容 Serilog 等扩展库。

## 功能特性

- **统一日志接口**：提供 `IXiHanLogger` 统一日志记录接口
- **结构化日志**：支持结构化数据记录和查询
- **性能日志**：专门的性能监控和计时功能
- **多目标输出**：支持控制台、文件等多种输出目标
- **异步日志**：支持异步日志写入，提升性能
- **日志上下文**：支持请求级别的日志上下文管理
- **Serilog 集成**：无缝集成 Serilog 生态系统
- **灵活配置**：丰富的配置选项，满足各种场景需求

## 快速开始

### 1. 添加包引用

```xml
<PackageReference Include="XiHan.Framework.Logging" Version="1.0.0" />
```

### 2. 注册服务

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // 基础日志服务
    services.AddXiHanLogging(options =>
    {
        options.MinimumLevel = LogLevel.Information;
        options.EnableStructuredLogging = true;
        options.EnablePerformanceCounters = true;
    });

    // 或者使用 Serilog
    services.AddXiHanSerilog(config =>
    {
        config
            .WriteTo.Console()
            .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day);
    });
}
```

### 3. 使用日志器

```csharp
public class UserService
{
    private readonly IXiHanLogger<UserService> _logger;
    private readonly IStructuredLogger _structuredLogger;
    private readonly IPerformanceLogger _performanceLogger;

    public UserService(
        IXiHanLogger<UserService> logger,
        IStructuredLogger structuredLogger,
        IPerformanceLogger performanceLogger)
    {
        _logger = logger;
        _structuredLogger = structuredLogger;
        _performanceLogger = performanceLogger;
    }

    public async Task<User> GetUserAsync(int userId)
    {
        _logger.LogInformation("Getting user with ID: {UserId}", userId);

        using var timer = _performanceLogger.StartTimer("GetUser");
        timer.AdditionalData = new { UserId = userId };

        try
        {
            var user = await GetUserFromDatabaseAsync(userId);

            _structuredLogger.LogInformation("User retrieved successfully", new
            {
                UserId = userId,
                UserName = user.Name,
                RetrievedAt = DateTime.UtcNow
            });

            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user with ID: {UserId}", userId);
            throw;
        }
    }
}
```

## 核心组件

### 1. IXiHanLogger

统一的日志记录接口，提供标准的日志记录方法：

```csharp
public interface IXiHanLogger
{
    void LogTrace(string message, params object[] args);
    void LogDebug(string message, params object[] args);
    void LogInformation(string message, params object[] args);
    void LogWarning(string message, params object[] args);
    void LogError(string message, params object[] args);
    void LogError(Exception exception, string message, params object[] args);
    void LogCritical(string message, params object[] args);
    void LogCritical(Exception exception, string message, params object[] args);

    // 结构化日志
    void LogStructured(LogLevel level, string message, object properties);

    // 性能日志
    void LogPerformance(string operationName, TimeSpan duration, object? properties = null);

    bool IsEnabled(LogLevel logLevel);
    IDisposable BeginScope<TState>(TState state) where TState : notnull;
}
```

### 2. IStructuredLogger

专门的结构化日志记录器：

```csharp
// 记录结构化数据
_structuredLogger.LogInformation("User login", new
{
    UserId = user.Id,
    UserName = user.Name,
    LoginTime = DateTime.UtcNow,
    IpAddress = context.Request.HttpContext.Connection.RemoteIpAddress?.ToString()
});

// 记录业务事件
_structuredLogger.LogBusiness("OrderCreated", new
{
    OrderId = order.Id,
    CustomerId = order.CustomerId,
    TotalAmount = order.TotalAmount,
    CreatedAt = order.CreatedAt
});
```

### 3. IPerformanceLogger

性能监控和计时功能：

```csharp
// 手动计时
var stopwatch = Stopwatch.StartNew();
await DoSomethingAsync();
stopwatch.Stop();
_performanceLogger.LogOperation("DoSomething", stopwatch.Elapsed, new { ItemCount = 100 });

// 自动计时
using var timer = _performanceLogger.StartTimer("DatabaseQuery");
timer.AdditionalData = new { QueryType = "SELECT", TableName = "Users" };
var users = await GetUsersAsync();
// timer 会在 using 块结束时自动记录性能数据

// API 调用性能
_performanceLogger.LogApiCall("/api/users", TimeSpan.FromMilliseconds(150), 200, new
{
    RequestSize = 1024,
    ResponseSize = 2048
});
```

### 4. ILogContext

请求级别的日志上下文管理：

```csharp
public class LoggingMiddleware
{
    public async Task InvokeAsync(HttpContext context, ILogContext logContext)
    {
        logContext.RequestId = context.TraceIdentifier;
        logContext.IpAddress = context.Connection.RemoteIpAddress?.ToString();
        logContext.UserAgent = context.Request.Headers["User-Agent"];

        using var scope = logContext.CreateScope("RequestPath", context.Request.Path);

        await _next(context);
    }
}
```

## 配置选项

### XiHanLoggingOptions

```csharp
services.AddXiHanLogging(options =>
{
    // 基础配置
    options.IsEnabled = true;
    options.MinimumLevel = LogLevel.Information;

    // 结构化日志
    options.EnableStructuredLogging = true;

    // 异步日志
    options.EnableAsyncLogging = true;
    options.AsyncBufferSize = 10000;

    // 性能计数器
    options.EnablePerformanceCounters = true;

    // 请求日志
    options.EnableRequestLogging = true;
    options.RequestLoggingExcludePaths = new[] { "/health", "/metrics" };

    // 输出模板
    options.ConsoleOutputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";
    options.FileOutputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    // 文件配置
    options.FileOutputPath = "logs/xihan-.log";
    options.RollingInterval = RollingInterval.Day;
    options.RetainedFileCountLimit = 31;
    options.FileSizeLimitBytes = 100 * 1024 * 1024; // 100MB

    // 日志过滤器
    options.Filters.Add("Microsoft", LogLevel.Warning);
    options.Filters.Add("System", LogLevel.Warning);
});
```

## 最佳实践

### 1. 结构化日志

使用结构化日志记录关键业务事件：

```csharp
// ✅ 好的做法 - 结构化数据
_structuredLogger.LogInformation("Order processed", new
{
    OrderId = order.Id,
    CustomerId = order.CustomerId,
    Amount = order.TotalAmount,
    PaymentMethod = order.PaymentMethod,
    ProcessingTime = processingTime.TotalMilliseconds
});

// ❌ 不推荐 - 字符串插值
_logger.LogInformation($"Order {order.Id} processed for customer {order.CustomerId} with amount {order.TotalAmount}");
```

### 2. 性能监控

对关键操作进行性能监控：

```csharp
// 数据库查询
using var dbTimer = _performanceLogger.StartTimer("DatabaseQuery");
dbTimer.AdditionalData = new { Query = "GetUsersByRole", Role = "Admin" };
var users = await _userRepository.GetUsersByRoleAsync("Admin");

// API 调用
using var apiTimer = _performanceLogger.StartTimer("ExternalApiCall");
apiTimer.AdditionalData = new { Endpoint = "/api/external/data", Method = "GET" };
var response = await _httpClient.GetAsync("/api/external/data");
```

### 3. 异常处理

正确记录异常信息：

```csharp
try
{
    await ProcessOrderAsync(order);
}
catch (ValidationException ex)
{
    _logger.LogWarning(ex, "Order validation failed: {OrderId}", order.Id);
    // 业务异常，记录为警告
}
catch (Exception ex)
{
    _structuredLogger.LogError("Order processing failed", new
    {
        OrderId = order.Id,
        CustomerId = order.CustomerId,
        ErrorType = ex.GetType().Name,
        ErrorMessage = ex.Message,
        StackTrace = ex.StackTrace
    });
    throw;
}
```

### 4. 日志上下文

利用日志上下文传递请求相关信息：

```csharp
using var userScope = _logContext.CreateScope("UserId", currentUser.Id);
using var operationScope = _logContext.CreateScope("Operation", "CreateOrder");

// 在这个作用域内的所有日志都会包含 UserId 和 Operation 信息
await CreateOrderAsync(orderRequest);
```

## 依赖关系

- XiHan.Framework.Core
- Serilog.AspNetCore
- Serilog.Sinks.Async

## 版本兼容性

- .NET 9.0+
- XiHan.Framework.Core 1.0.0+

## 许可证

MIT License - 详见 LICENSE 文件
