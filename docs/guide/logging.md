# 日志

框架把 Serilog 接成唯一的日志出口：控制台 + 异步滚动文件，每行自带 W3C TraceId。这一章讲怎么写日志才可聚合、TraceId 是怎么串起来的、以及哪些配置项写了也不会生效。

完整 API 与全部配置项见 [Logging 包](../packages/logging)。

## 先分清五件事

| 你要做的 | 用什么 | 落到哪 |
| --- | --- | --- |
| 普通业务日志 | `ILogger<T>`（原生 `Microsoft.Extensions.Logging`） | Serilog 管道 → 控制台 + 文件 |
| 带一坨对象的结构化日志 | `IStructuredLogger` | 同上（属性进事件，见下方模板陷阱） |
| 耗时计量 | `IPerformanceLogger` | 同上 |
| 登录/操作/接口/异常留痕 | Auditing 包的各 Pipeline | 数据库，见 [Auditing 包](../packages/auditing) |
| 请求级字段暂存 | `ILogContext` | 内存数据袋，**不自动进日志** |

::: tip 日常写日志就用 `ILogger<T>`
本包不要求你换接口。`AddSerilog` 把 Serilog 设为日志提供程序后，注入原生 `ILogger<T>` 写出的日志就已经走本模块配置好的管道了。
:::

## 安装与启用

```bash
dotnet add package XiHan.Framework.Logging
```

```csharp
[DependsOn(typeof(XiHanLoggingModule))]
public class MyModule : XiHanModule { }
```

`XiHanApplicationModule` 已经 `[DependsOn(typeof(XiHanLoggingModule))]`，应用层模块通常不必再声明一次。

模块做两件事：

- `PreConfigureServices` 先写入默认值 `IsEnabled = true`、`MinimumLevel = Information`；
- `ConfigureServices` 调 `AddXiHanLogging(config)`，绑定配置节 `XiHan:Logging`（后绑定的配置值覆盖上面的默认值），注册日志服务，并装配 Serilog。

注册结果：

| 服务 | 实现 | 生命周期 |
| --- | --- | --- |
| `IXiHanLoggerFactory` | `XiHanLoggerFactory` | Singleton |
| `IXiHanLogger` / `IXiHanLogger<>` | `XiHanLogger` / `XiHanLogger<>` | Transient |
| `IStructuredLogger` | `StructuredLogger` | Singleton |
| `IPerformanceLogger` | `PerformanceLogger` | Singleton |
| `ILogContext` | `LogContext` | Scoped |

全部走 `TryAdd*`，应用侧提前注册自己的实现即可覆盖。

## 结构化日志约定

Serilog 存的是「消息模板 + 具名属性」，不是拼好的字符串。写法决定了这条日志能不能被检索和聚合。

```csharp
public class OrderAppService(ILogger<OrderAppService> logger)
{
    public async Task PayAsync(long orderId, decimal amount)
    {
        // 好：模板固定，orderId/amount 作为独立属性存下来
        logger.LogInformation("订单支付完成 {OrderId}，金额 {Amount}", orderId, amount);

        // 好：整个对象解构成结构化属性
        logger.LogInformation("支付回调 {@Callback}", callback);

        // 差：字符串插值，每条日志都是不同模板，属性全丢
        logger.LogInformation($"订单支付完成 {orderId}，金额 {amount}");
    }
}
```

约定：

| 约定 | 说明 |
| --- | --- |
| 用具名占位符，不用字符串插值 | 插值后模板不再固定，无法按模板聚合，属性也不复存在 |
| 占位符用 PascalCase | 与框架内置日志（`{TraceId}`、`{StatusCode}`、`{Elapsed}`）保持一致 |
| 对象前缀 `@` 解构 | `{@Order}` 展开成结构化属性；不加 `@` 只调 `ToString()` |
| 异常放第一个参数 | `logger.LogError(ex, "…")`，不要把 `ex.Message` 拼进消息 |
| 高基数值（订单号、用户 ID）作为属性 | 不要拼进消息文本，否则检索不到 |

::: warning `IXiHanLogger` 不绑定调用方的模板
`XiHanLogger` 内部固定使用模板 `"{Message}{Args}"`：你传进去的 `message` 整体成为一个 `Message` 属性，`args` 整体成为一个 `Args` 属性。

也就是说 `LogInfo("用户 {UserId} 登录", userId)` 里的 `{UserId}` **不会被替换**，输出的是字面量加上参数数组。

需要占位符绑定就用 `ILogger<T>`。`IXiHanLogger` 适合消息本身已经拼好、只求一行简短 API 的场景。
:::

### `IStructuredLogger`：挂一坨数据

```csharp
public class OrderService(IStructuredLogger logger)
{
    public void CreateOrder(long orderId, decimal amount)
    {
        logger.LogBusiness("OrderCreated", new { OrderId = orderId, Amount = amount });
        logger.LogEvent("PaymentRequested", new { OrderId = orderId });
        logger.LogInformation("库存已锁定", new { OrderId = orderId, Sku = "A-001" });
    }
}
```

实现是把数据 `PushProperty` 进 Serilog 的 `LogContext` 后再写一条日志，附加的属性名固定为：

| 方法 | 附加属性 | 消息模板 |
| --- | --- | --- |
| `LogInformation/LogWarning/LogError/Log` | `StructuredData` | 你传入的 message 原样作为模板 |
| `LogEvent` | `EventName`、`EventData` | `Event: {EventName}` |
| `LogBusiness` | `BusinessAction`、`BusinessData` | `Business Action: {BusinessAction}` |

::: warning 默认输出模板不渲染这些属性
默认的控制台/文件模板里只有 `{Timestamp}`、`{Level}`、`{TraceId}`（文件模板还有 `{SpanId}`）、`{SourceContext}`、`{Message}`、`{Exception}`，**没有 `{Properties}`**。`StructuredData`、`BusinessData` 这些属性确实写进了日志事件，但在默认文本输出里看不见。

要看到它们：把 `{Properties}` 加进 `ConsoleOutputTemplate` / `FileOutputTemplate`，或者在应用侧自行接一个 JSON 格式的 Sink。
:::

### `IPerformanceLogger`：耗时计量

```csharp
public class ReportService(IPerformanceLogger perf)
{
    public void Generate()
    {
        using var timer = perf.StartTimer("GenerateReport");
        timer.AdditionalData = new { Rows = 12000 };
        // 释放时自动写出 Operation {OperationName} completed in {Duration}ms
    }
}
```

还有 `LogOperation`、`LogApiCall`、`LogDatabaseQuery`、`LogMemoryUsage`、`LogCpuUsage`，全部以 `Information` 级别写出。其中 `LogOperation` / `LogApiCall` / `LogDatabaseQuery` 带可选 `additionalData` 参数，模板末尾渲染 `{@AdditionalData}`；`LogMemoryUsage` / `LogCpuUsage` 没有这个参数，模板里也没有 `{@AdditionalData}`。

::: tip 两条性能日志路径的门控不一样
`IPerformanceLogger` 无条件写出；而 `IXiHanLogger.LogPerformance(...)` 受 `EnablePerformanceCounters` 门控，该项默认 `false`，也就是默认什么都不写。
:::

### `ILogContext` 是数据袋，不是 enricher

`ILogContext`（Scoped）带 `UserId`、`UserName`、`TenantId`、`RequestId`、`TraceId`、`SessionId`、`IpAddress`、`UserAgent` 和自定义 `Properties`，`CreateScope` 进出作用域会自动还原原值。

::: warning 它不会让日志自动带上这些字段
`LogContext` 的实现只是一个进程内字典，框架没有把它接进 Serilog 的属性增强链。设了 `UserId` 不等于日志行里就有 `UserId`。

要让一段代码内的所有日志都带上某属性，用其中之一：

```csharp
// 方式一：Serilog 的上下文（AddXiHanSerilog 已启用 Enrich.FromLogContext）
using (Serilog.Context.LogContext.PushProperty("TenantId", tenantId))
{
    // 这里写的日志都带 TenantId 属性
}

// 方式二：M.E.L 的作用域
using (logger.BeginScope(new Dictionary<string, object> { ["TenantId"] = tenantId }))
{
    // 同上
}
```

属性写进事件之后，同样要模板里出现 `{TenantId}` 或用 JSON Sink 才看得见。
:::

## TraceId 如何贯穿

日志行首那对方括号里的链路 ID 不是框架填的，是 Serilog 从当前 `Activity` 上取的。

| 环节 | 机制 | 源 |
| --- | --- | --- |
| 日志行 | Serilog 的 `LogEvent` 自带 `TraceId` / `SpanId`，取自事件创建时的环境 `Activity`；默认模板已把 `{TraceId}` 放在级别之后 | Serilog |
| 响应头与 `HttpContext` | `XiHanTraceIdMiddleware` 把 W3C TraceId 镜像进 `HttpContext.Items["__XiHanTraceId"]` 和响应头 `X-Trace-Id` | Web.Api |
| 业务取值 | `ITraceIdProvider`（`HttpTraceIdProvider`）与 `ICorrelationIdProvider`（`DefaultCorrelationIdProvider`）都优先返回 `Activity.Current.TraceId` 的 32 位十六进制串 | Web.Api / Core |
| 审计留痕 | 访问日志/接口日志/异常日志记录都带 `TraceId` 字段 | Auditing |
| 跨进程 | 事件总线发布时把当前 trace id 作为 `correlationId` 随消息发出；消费端据它建 Consumer Span，从而落在同一条 trace 上 | EventBus |

中间件的回退链（`XiHanTraceIdMiddleware`）：

1. `Activity.Current.TraceId`（W3C，32 位十六进制）；
2. 入站请求头 `X-Trace-Id`；
3. `HttpContext.TraceIdentifier`（Kestrel 的连接内序号）。

::: warning 两个 TraceId 口径可能不一致
日志模板里的 `{TraceId}` **只**来自 `Activity`；而审计表、响应头里的 TraceId 走的是上面那条带回退的链。没有 `Activity` 时前者渲染为空、后者是 `TraceIdentifier`，两边对不上。

`XiHanTraceIdMiddleware` 注册在管线最前（仅次于 `UseForwardedHeaders`），HTTP 请求内两者一致。
:::

::: warning 跨进程串联依赖可观测性开关
消费端的 Consumer Span 只在对应 `ActivitySource` 有监听者时才创建，也就是要开 `XiHan:Observability`（`Enabled` + `EnableTracing`）。没开的话生产者与消费者是两条独立的 trace，日志的 TraceId 串不起来。

后台作业、定时任务同理：没有 `Activity` 就没有 TraceId，日志里那对方括号是空的。
:::

配置与导出见 [Observability 包](../packages/observability)。当前 OTLP 只导出 trace 与 metrics，日志本身不经 OTLP 导出。

## 日志级别

只有一个总开关 `XiHan:Logging:MinimumLevel`，映射到 Serilog 级别：

| `LogLevel` | Serilog `LogEventLevel` |
| --- | --- |
| `Trace` | `Verbose` |
| `Debug` | `Debug` |
| `Information` | `Information` |
| `Warning` | `Warning` |
| `Error` | `Error` |
| `Critical` | `Fatal` |
| `None` | `Fatal` |

::: danger `None` 不是「关掉日志」
`LogLevel.None` 被映射成 `Fatal`。配 `"MinimumLevel": "None"` 的效果是**只保留致命日志**，不是静默。要静默请把输出目标指向别处，或在应用侧自行装配 Serilog。
:::

::: warning 没有按分类的级别过滤
`AddXiHanSerilog` 没有配置任何 `MinimumLevel.Override`，`XiHanLoggingOptions.Filters` 字段也没有被读取。也就是说框架装配路径下压不掉 `Microsoft.*` / `System.*` 的噪声，只能整体调高 `MinimumLevel`。需要按分类分级请在应用侧自行装配 Serilog。
:::

`IsEnabled` 是另一件事：它只在 `XiHanLogger` 的方法里做前置判断，`IsEnabled: false` 时 `IXiHanLogger` 系列不写日志，但 `ILogger<T>`、`IStructuredLogger`、`IPerformanceLogger` 照写不误。

## 输出目标

装配是固定的两路，都在 `AddXiHanSerilog` 里写死：

| 目标 | 说明 |
| --- | --- |
| 控制台 | 模板取 `ConsoleOutputTemplate` |
| 文件 | 经 `WriteTo.Async` 异步写入，模板取 `FileOutputTemplate`，滚动/保留/大小按配置 |

统一附加：`Enrich.FromLogContext()` 与固定属性 `Application = "XiHanFramework"`（不可配置）。

文件相关配置项：

| 键 | 默认值 | 说明 |
| --- | --- | --- |
| `FileOutputPath` | `logs/xihan-.log` | 基名，Serilog 按滚动间隔在扩展名前插日期，如 `logs/xihan-20260805.log` |
| `RollingInterval` | `Day` | 滚动间隔 |
| `RetainedFileCountLimit` | `31` | 保留文件数，`null` 表示永久保留 |
| `FileSizeLimitBytes` | `104857600`（100 MB） | 单文件上限 |
| `RollOnFileSizeLimit` | `true` | 达到上限时切新文件 |

::: warning 两路 Sink 都关不掉，也没有追加钩子
没有开关能单独禁用控制台或文件；`AddXiHanSerilog` 也没有对外暴露「再挂一个 Sink」的入口。要写 Seq / Elasticsearch / OTLP，就在应用侧自行装配 Serilog 管道，不要指望通过 `XiHan:Logging` 配出来。
:::

### 声明了但当前不生效的选项

这些字段存在于 `XiHanLoggingOptions`，但当前实现中没有任何代码读取它们。配了不会报错，也不会有效果：

| 字段 | 现状 | 替代做法 |
| --- | --- | --- |
| `EnableAsyncLogging` | 文件 Sink 恒定走 `WriteTo.Async`，不受此开关影响 | 无需配置 |
| `AsyncBufferSize` / `BlockWhenFull` | 未传给 `WriteTo.Async` | 应用侧自行装配 Serilog |
| `ContextProperties` | 未加入 enricher | `Serilog.Context.LogContext.PushProperty` |
| `Filters` | 未转成 `MinimumLevel.Override` | 应用侧自行装配 Serilog |
| `EnableRequestLogging` / `RequestLoggingExcludePaths` | 无消费方 | 请求日志由 Web.Api 的中间件负责，排除路径见 [Auditing 包](../packages/auditing) 的 `IgnoredPathPrefixes` |

同样地，`XiHanFileLoggerOptions` 的 `BufferSize`、`FlushPeriod`、`EnableAsyncWrite` 与 `XiHanConsoleLoggerOptions` 的 `UseStdErrorForErrors` 也没有被对应的 Provider 读取。

## 配置

只列当前真正生效的键：

```json
{
  "XiHan": {
    "Logging": {
      "IsEnabled": true,
      "MinimumLevel": "Information",
      "ConsoleOutputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] [{TraceId}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
      "FileOutputPath": "logs/xihan-.log",
      "FileOutputTemplate": "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [{TraceId} {SpanId}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
      "RollingInterval": "Day",
      "RetainedFileCountLimit": 31,
      "FileSizeLimitBytes": 104857600,
      "RollOnFileSizeLimit": true,
      "EnableStructuredLogging": true,
      "EnablePerformanceCounters": false
    }
  }
}
```

配置节常量为 `XiHanLoggingOptions.SectionName`。全部字段清单见 [Logging 包](../packages/logging)。

## 自建 ILoggerProvider 的适用场景

本包另外提供两个原生 `ILoggerProvider`：

```csharp
builder.Logging.AddXiHanConsoleLogger(o =>
{
    o.MinLevel = LogLevel.Debug;
    o.SingleLine = true;
    o.LogFormat = "[{Timestamp:HH:mm:ss}] [{Level:u3}] {Category}: {Message}{Exception}";
});

builder.Logging.AddXiHanFileLogger(o => o.FilePath = "logs/app-.log");
```

::: danger 和本模块的 Serilog 管道互斥
`AddSerilog` 默认 `writeToProviders: false`——Serilog 不会把事件转发给通过 `Microsoft.Extensions.Logging` 注册的 `ILoggerProvider`。所以在启用了 `XiHanLoggingModule` 的应用里挂这两个 Provider，**它们收不到任何日志**。

它们的用途是在不走本模块 Serilog 管道的宿主（工具、独立进程）里当轻量输出。
:::

::: warning 两个 Options 必须用 lambda 配
`XiHanFileLoggerOptions.SectionName` / `XiHanConsoleLoggerOptions.SectionName` 只是常量，`AddXiHanFileLogger` / `AddXiHanConsoleLogger` 内部并没有绑定这两个配置节。写进 `appsettings.json` 的 `XiHan:Logging:File` / `XiHan:Logging:Console` 不会被读取。
:::

## 引导日志

`XiHanLoggerBuilder` 是一套与 DI 装配完全独立的 Fluent 构建器，用于 DI 就绪前的引导日志：

```csharp
var bootstrapLogger = new XiHanLoggerBuilder().CreateLoggerDefault();
```

`CreateLoggerDefault()` 给出的是另一套默认策略：按级别分目录写文件（`Logs/Debug/`、`Logs/Info/`、`Logs/Waring/`、`Logs/Error/`、`Logs/Fatal/`，路径相对 `AppContext.BaseDirectory` 且强制转小写），单文件 10 MB、保留 60 个，并对 `Microsoft`、`System` 两个源做 `Warning` 覆盖。

::: warning 别把两套默认值混为一谈
`XiHanLoggerConfigurationBuilder` 的模板、目录、保留策略与 `XiHanLoggingOptions` 毫无关系，改 `XiHan:Logging` 不会影响引导日志。
:::

## 别和 Utils 里的静态日志混用

`XiHan.Framework.Utils.Logging` 下的 `LogHelper` / `LogFileHelper` 是一套独立的静态工具，自带配置与文件写入，**不经过 Serilog 管道**，输出也不带 TraceId。应用代码请统一走 `ILogger<T>`。

## 常见问题

| 现象 | 原因 |
| --- | --- |
| 日志行里 `[]` 是空的，没有 TraceId | 当前执行流没有 `Activity`。HTTP 请求内正常有值；后台作业、定时任务里没有 |
| 事件总线消费端的 TraceId 和生产端对不上 | 没开 `XiHan:Observability`，消费端不建 Consumer Span |
| `IStructuredLogger` 写的数据在日志里看不到 | 默认模板没有 `{Properties}`，属性进了事件但不渲染 |
| `IXiHanLogger.LogInfo("… {UserId} …", id)` 没替换占位符 | 该实现固定用 `"{Message}{Args}"` 模板，不绑定调用方模板 |
| `MinimumLevel: "None"` 之后只剩致命日志 | `None` 被映射为 `Fatal` |
| `IsEnabled: false` 但日志照写 | 该开关只作用于 `IXiHanLogger` 系列 |
| `LogPerformance` 什么都不写 | `EnablePerformanceCounters` 默认 `false`（`IPerformanceLogger` 不受此限） |
| `AddXiHanConsoleLogger()` / `AddXiHanFileLogger()` 没有输出 | Serilog 作为日志提供程序时默认不向 M.E.L 的 Provider 转发事件 |
| `Filters` / `RequestLoggingExcludePaths` 配了没反应 | 当前实现未读取这些字段 |
| `ILogContext` 设了 `UserId` 但日志里没有 | 它是内存数据袋，没接进 Serilog 的属性增强链 |
| 后台任务里 `ILogContext` 每次都是新实例 | Scoped，无 DI 作用域时需自建 `IServiceScope` |
| 日志文件名带日期后缀 | Serilog 按 `RollingInterval` 自动插入，`FileOutputPath` 只给基名 |

## 下一步

- [配置与选项](./configuration)：`XiHan:Logging` 的绑定与覆盖顺序
- [Web 应用开发](./web)：TraceId 中间件在请求管线中的位置
- [Auditing 包](../packages/auditing)：登录/操作/接口/异常日志落库
- [Observability 包](../packages/observability)：链路追踪与指标，TraceId 的来源
- [Logging 包](../packages/logging)：完整 API 清单与全部配置项
