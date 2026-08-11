# XiHan.Framework.Logging

> 结构化日志：Serilog 集成、控制台/异步文件双路输出、按天/按大小滚动、结构化与性能日志、多租户日志上下文。

- **NuGet**：`XiHan.Framework.Logging`
- **模块类**：`XiHanLoggingModule`
- **所在层**：基础设施层
- **关键依赖**：`Serilog.AspNetCore`（含 Serilog 核心与 Console/File Sink 传递依赖）、`Serilog.Sinks.Async`（异步文件写入）

## 概述

XiHan.Framework.Logging 在 Serilog 之上封装了一套开箱即用的日志基础设施。启用后从配置节 `XiHan:Logging` 绑定选项，配置控制台与异步文件双路输出：文件借助 `Serilog.Sinks.Async` 异步写入、按天滚动并可按大小滚动、限制保留文件数量。除通用日志外，它还额外提供三类专用能力：结构化日志 `IStructuredLogger`（事件/业务动作）、性能监控日志 `IPerformanceLogger`（API/数据库/内存/CPU + `using` 计时器），以及多租户日志上下文 `ILogContext`（UserId/TenantId/TraceId 等，支持作用域）。

## 何时使用

- 想一行 `[DependsOn]` 就获得控制台 + 文件的完整 Serilog 配置，无需手写引导代码。
- 需要日志文件异步写入、按天滚动、限制单文件大小与保留数量。
- 需要记录结构化数据、业务事件，或对 API / 数据库调用做性能计时。
- 需要在日志中自动携带 UserId、TenantId、TraceId 等请求上下文。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Logging
```

```csharp
[DependsOn(typeof(XiHanLoggingModule))]
public class MyModule : XiHanModule { }
```

模块启用后：

- `PreConfigureServices` 先给 `XiHanLoggingOptions` 设默认值（`IsEnabled = true`、`MinimumLevel = Information`）。
- `ConfigureServices` 调用 `AddXiHanLogging(config)`，从配置节 `XiHan:Logging` 绑定 `XiHanLoggingOptions`，并完成以下注册：
  - `IXiHanLoggerFactory` → `XiHanLoggerFactory`（Singleton）
  - `IXiHanLogger` → `XiHanLogger`、`IXiHanLogger<>` → `XiHanLogger<>`（Transient）
  - `IStructuredLogger` → `StructuredLogger`、`IPerformanceLogger` → `PerformanceLogger`（Singleton）
  - `ILogContext` → `LogContext`（Scoped）
  - 通过 `AddSerilog(...)` 接入 Serilog：按 `XiHanLoggingOptions` 中的最小级别、输出模板、滚动策略等配置 Console Sink 与异步 File Sink，并启用 `Enrich.FromLogContext()`。

> 注意：日志写出走标准 `Serilog` 管道（`AddSerilog`），因此注入原生的 `Microsoft.Extensions.Logging.ILogger<T>` 也会经由本模块配置的 Serilog 输出；`IXiHanLogger` 等接口是在其上提供的更贴合业务的封装。

## 工作原理

- **运行期 Serilog 装配**：DI 扩展的私有方法 `AddXiHanSerilog` 在 `AddSerilog` 回调里读取 `XiHanLoggingOptions`，把 `MinimumLevel`（经 `LogLevel` → `LogEventLevel` 映射）、`ConsoleOutputTemplate`、`FileOutputPath/Template`、`RollingInterval`、`RetainedFileCountLimit`、`FileSizeLimitBytes`、`RollOnFileSizeLimit` 应用到 Console Sink 与 `WriteTo.Async(a => a.File(...))` 异步文件 Sink，并统一 `Enrich.FromLogContext()` 与固定属性 `Application = XiHanFramework`。
- **独立配置构建器**：`XiHanLoggerBuilder` / `XiHanLoggerConfigurationBuilder` 是一套**独立的 Fluent 构建器**，用于手动构造 `Serilog.Core.Logger`（例如宿主启动早期的引导日志），不参与模块 DI 装配。其 `BuildDefault()` 提供一套分级、多目录的默认策略（见下）。

## 核心能力

- **Serilog 集成**：从配置节绑定选项后统一配置最小级别、上下文增强、控制台与异步文件输出。
- **异步文件输出**：借助 `Serilog.Sinks.Async` 异步写入，按天滚动 + 可按大小滚动，限制保留文件数与单文件大小。
- **控制台输出**：使用可配置的输出模板。
- **结构化日志**：`IStructuredLogger` 支持信息/警告/错误、事件（`LogEvent`）与业务动作（`LogBusiness`）的结构化记录。
- **性能监控**：`IPerformanceLogger` 记录操作 / API 调用 / 数据库查询 / 内存 / CPU；`IPerformanceTimer` 支持 `using` 自动计时。
- **多租户上下文**：`ILogContext`（Scoped）携带 UserId、UserName、TenantId、RequestId、TraceId、SessionId、IpAddress、UserAgent 及自定义属性，支持属性作用域（`CreateScope`）隔离与还原。

## 主要 API / 类型

### 日志器与工厂

| 类型 | 说明 |
| --- | --- |
| `IXiHanLogger` / `IXiHanLogger<out T>` | 通用日志接口与泛型版本。方法：`LogTrace/LogDebug/LogInfo/LogWarn/LogError/LogCritical(string, params object[])`、`LogError(Exception, string, ...)`、`LogStructured(LogLevel, string, object)`、`LogPerformance(string, TimeSpan, object?)`、`IsEnabled(LogLevel)`、`BeginScope<TState>(TState)` |
| `IXiHanLoggerFactory` | 工厂：`IXiHanLogger CreateLogger(string categoryName)`、`IXiHanLogger<T> CreateLogger<T>()`、`IStructuredLogger CreateStructuredLogger(string)`、`IPerformanceLogger CreatePerformanceLogger(string)` |

### 结构化日志

| 类型 | 说明 |
| --- | --- |
| `IStructuredLogger` | `LogInformation/LogWarning/LogError(string message, object data)`、`LogError(Exception, string, object)`、`Log(LogLevel, string, object)`、`LogEvent(string eventName, object eventData)`、`LogBusiness(string businessAction, object businessData)` |

### 性能日志

| 类型 | 说明 |
| --- | --- |
| `IPerformanceLogger` | `LogOperation(string, TimeSpan, object?)`、`LogApiCall(string, TimeSpan, int statusCode, object?)`、`LogDatabaseQuery(string, TimeSpan, int recordCount, object?)`、`LogMemoryUsage(string, long, long)`、`LogCpuUsage(string, double, TimeSpan)`、`IPerformanceTimer StartTimer(string operationName)` |
| `IPerformanceTimer : IDisposable` | 属性 `OperationName`、`Stopwatch`、`AdditionalData`；方法 `Stop()`。释放时自动记录耗时 |

### 日志上下文

| 类型 | 说明 |
| --- | --- |
| `ILogContext`（Scoped） | 上下文属性 `UserId/UserName/TenantId/RequestId/TraceId/SessionId/IpAddress/UserAgent`、`Properties`；方法 `SetProperty(string, object)`、`GetProperty<T>(string)`、`RemoveProperty(string)`、`Clear()`、`CreateScope(Dictionary<string, object>)`、`CreateScope(string, object)` |

### 提供器与构建器

| 类型 | 说明 |
| --- | --- |
| `XiHanFileLoggerProvider` / `XiHanConsoleLoggerProvider` | 实现 `Microsoft.Extensions.Logging.ILoggerProvider` 的文件 / 控制台提供器，可通过 `AddXiHanFileLogger()` / `AddXiHanConsoleLogger()` 挂到 `ILoggingBuilder` |
| `XiHanLoggerBuilder` | 独立日志器构建入口：`Logger CreateLogger(IConfiguration)`、`Logger CreateLogger()`、`Logger CreateLoggerDefault()` |
| `XiHanLoggerConfigurationBuilder` | Serilog `LoggerConfiguration` 的 Fluent 构建器：`MinimumLevel(...)`、`Override(source, level)`、`EnrichWithProperty(...)`、`EnrichFromLogContext()`、`WriteToConsole(level, template)`、`WriteToFile(level, path, template)`，以及对应的 `*Default()` 与 `Build()/BuildDefault()` |

### DI 扩展方法（`XiHanLoggingServiceCollectionExtensions`）

| 方法 | 说明 |
| --- | --- |
| `AddXiHanLogging(IConfiguration)` | 从配置节 `XiHan:Logging` 绑定选项并装配全部日志服务 + Serilog（模块内部调用） |
| `AddXiHanLogging()` / `AddXiHanLogging(Action<XiHanLoggingOptions>)` | 以代码方式配置选项并装配 |
| `AddXiHanFileLogger(Action<XiHanFileLoggerOptions>?)` | 向 `ILoggingBuilder` 追加自定义文件日志提供器 |
| `AddXiHanConsoleLogger(Action<XiHanConsoleLoggerOptions>?)` | 向 `ILoggingBuilder` 追加自定义控制台日志提供器 |

## 配置

主配置节 `XiHan:Logging`（`XiHanLoggingOptions.SectionName`）。核心字段：

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `IsEnabled` | `bool` | `true` | 是否启用日志 |
| `MinimumLevel` | `LogLevel` | `Information` | 最小日志级别（映射为 Serilog `LogEventLevel`） |
| `ConsoleOutputTemplate` | `string` | `[{Timestamp:HH:mm:ss} {Level:u3}] [{TraceId}] {SourceContext}: {Message:lj}{NewLine}{Exception}` | 控制台输出模板（链路 ID 紧跟级别之后） |
| `FileOutputPath` | `string` | `logs/xihan-.log` | 文件输出路径（Serilog 会在文件名中插入日期后缀） |
| `FileOutputTemplate` | `string` | `[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [{TraceId} {SpanId}] {SourceContext}: {Message:lj}{NewLine}{Exception}` | 文件输出模板（链路 ID 紧跟级别之后） |
| `RollingInterval` | `RollingInterval` | `Day` | 文件滚动间隔 |
| `RetainedFileCountLimit` | `int?` | `31` | 保留文件数（`null` 表示永久保留） |
| `FileSizeLimitBytes` | `long?` | `104857600`（100MB） | 单文件大小上限 |
| `RollOnFileSizeLimit` | `bool` | `true` | 达到大小上限时是否滚动新文件 |
| `EnableStructuredLogging` | `bool` | `true` | 是否启用结构化日志 |
| `EnableAsyncLogging` | `bool` | `true` | 是否启用异步日志 |
| `AsyncBufferSize` | `int` | `10000` | 异步日志缓冲区大小 |
| `BlockWhenFull` | `bool` | `false` | 缓冲区满时是否阻塞 |
| `ContextProperties` | `Dictionary<string, object>` | `[]` | 附加日志上下文属性 |
| `EnablePerformanceCounters` | `bool` | `false` | 是否启用性能计数器 |
| `EnableRequestLogging` | `bool` | `true` | 是否启用请求日志 |
| `RequestLoggingExcludePaths` | `string[]` | `/health`、`/metrics`、`/favicon.ico`、`/swagger` | 请求日志排除路径 |
| `Filters` | `Dictionary<string, LogLevel>` | `[]` | 分类日志级别过滤 |

自定义提供器的独立配置节（仅在使用 `AddXiHanFileLogger` / `AddXiHanConsoleLogger` 时相关）：

- `XiHan:Logging:File`（`XiHanFileLoggerOptions`）：`FilePath`、`FileSizeLimit`（默认 10MB）、`RetainedFileCountLimit`（默认 31）、`BufferSize`、`FlushPeriod`、`MinLevel`、`IncludeScopes`、`LogFormat`、`EnableAsyncWrite`、`Encoding`。
- `XiHan:Logging:Console`（`XiHanConsoleLoggerOptions`）：`MinLevel`、`IncludeScopes`、`EnableColors`、`EnableRainbow`、`LogFormat`、`TimestampFormat`、`ShowCategoryName/Timestamp/LogLevel`、`LogLevelColors`、`SingleLine`、`UseStdErrorForErrors`。

示例 `appsettings.json`：

```json
{
  "XiHan": {
    "Logging": {
      "IsEnabled": true,
      "MinimumLevel": "Information",
      "FileOutputPath": "logs/xihan-.log",
      "RollingInterval": "Day",
      "RetainedFileCountLimit": 31,
      "FileSizeLimitBytes": 104857600,
      "RollOnFileSizeLimit": true,
      "RequestLoggingExcludePaths": [ "/health", "/metrics", "/favicon.ico", "/swagger" ]
    }
  }
}
```

## 使用示例

### 结构化日志与业务事件

```csharp
public class OrderService
{
    private readonly IStructuredLogger _logger;

    public OrderService(IStructuredLogger logger) => _logger = logger;

    public void CreateOrder(Guid orderId, decimal amount)
    {
        _logger.LogBusiness("OrderCreated", new { OrderId = orderId, Amount = amount });
        _logger.LogEvent("PaymentRequested", new { OrderId = orderId });
    }
}
```

### 性能计时（`using` 自动记录）

```csharp
public class ReportService
{
    private readonly IPerformanceLogger _perf;

    public ReportService(IPerformanceLogger perf) => _perf = perf;

    public void GenerateReport()
    {
        using var timer = _perf.StartTimer("GenerateReport");
        // ... 耗时操作，timer 释放时自动写出耗时
    }
}
```

### 日志上下文（携带 UserId / TenantId）

```csharp
public class AuditMiddleware
{
    private readonly ILogContext _logContext; // Scoped

    public AuditMiddleware(ILogContext logContext) => _logContext = logContext;

    public void SetContext(string userId, string tenantId)
    {
        _logContext.UserId = userId;
        _logContext.TenantId = tenantId;
        using var scope = _logContext.CreateScope("Feature", "Billing");
        // 作用域内的日志将携带 Feature=Billing，退出后自动移除
    }
}
```

## 扩展点 / 自定义

- **替换日志实现**：`IXiHanLogger`、`IStructuredLogger`、`IPerformanceLogger`、`ILogContext` 均以 `TryAdd*` 注册，可在应用侧提前注册自定义实现覆盖默认行为。
- **追加 M.E.Logging 提供器**：在配置 `ILoggingBuilder` 时调用 `AddXiHanFileLogger()` / `AddXiHanConsoleLogger()` 挂上本包提供的 `ILoggerProvider`，各自读取 `XiHan:Logging:File` / `XiHan:Logging:Console` 配置节。
- **手动构造引导日志**：宿主启动早期可用 `new XiHanLoggerBuilder().CreateLoggerDefault()` 得到一个分级、多目录（Debug/Info/Waring/Error/Fatal 各自子目录）的 Serilog `Logger`，用于 DI 就绪前的引导日志。

## 注意事项与最佳实践

- `ILogContext` 是 **Scoped**，仅在有请求作用域（如 Web 请求）时才是每请求隔离；后台任务中若无作用域请自行创建 DI Scope 再解析。
- `IStructuredLogger` / `IPerformanceLogger` 是 **Singleton**，`IXiHanLogger` 是 **Transient**。
- 文件路径中的日期后缀由 Serilog 依据 `RollingInterval` 自动插入（如 `xihan-20260705.log`）；`FileOutputPath` 只需给出基名。
- 模块 DI 装配走 `XiHanLoggingOptions`；`XiHanLoggerConfigurationBuilder` 的 `*Default()` 模板与目录约定是**独立**的另一套默认策略，二者不要混淆。

## 依赖模块

- 内部依赖：仅 [XiHan.Framework.Core](./core)。
- 第三方核心：`Serilog.AspNetCore`、`Serilog.Sinks.Async`。

## 相关模块

- [XiHan.Framework.Observability](./observability) — 可观测性（追踪 / 指标）与日志互补。
- [XiHan.Framework.Timing](./timing) — 时钟与时区，日志时间戳的规范化基础。
