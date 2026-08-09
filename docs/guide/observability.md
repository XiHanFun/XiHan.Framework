# 可观测性

线上出问题时要能回答三个问题：这次请求走过哪些环节、哪一步慢、进程现在什么状态。框架把这件事拆成链路追踪、指标、进程内性能统计与诊断、健康检查四块，其中链路与指标基于 OpenTelemetry，**默认全部关闭**。

完整 API 与配置全表见 [Observability 包](../packages/observability)。

## 四块能力，两种数据去向

| 能力 | 入口类型 | 受 `Enabled` 门控 | 数据去向 |
| --- | --- | --- | --- |
| 链路追踪 | `ActivitySource`（`XiHanActivitySources`） | 是 | OTLP / 控制台导出器 |
| 指标 | `IMetricsCollector` | 记录随时可调，**导出**受门控 | OTLP / 控制台导出器，进程内不留存 |
| 性能统计 | `IPerformanceMonitor` | 否 | 进程内内存，只能用 API 读 |
| 运行时诊断 | `IDiagnosticsService` | 否 | 即时读取，不留存 |
| 健康检查 | `Microsoft.Extensions.Diagnostics.HealthChecks` | 否 | 需要自己映射端点 |

::: warning 本包不含任何采集后端
它只负责按 OTLP 标准协议把 trace / metrics 发出去。采集端与可视化面板要自行部署，框架不内置、也不代管。
:::

## 启用

没有任何框架模块 `DependsOn` 可观测性模块——它必须由宿主模块自己声明：

```csharp
[DependsOn(typeof(XiHanObservabilityModule))]
public class MyWebHostModule : XiHanModule { }
```

模块的 `ConfigureServices` 只做一件事：调用 `services.AddXiHanObservability(configuration)`。这个扩展方法按顺序执行：

1. 把配置节 `XiHan:Observability` 绑定到 `XiHanObservabilityOptions`；
2. `services.AddHealthChecks()` 注册健康检查基础设施；
3. 单例注册 `IMetricsCollector` / `IPerformanceMonitor` / `IDiagnosticsService`；
4. **若 `Enabled` 为 `false`（默认值），到此 `return`**；
5. 否则 `services.AddOpenTelemetry()`，配置 Resource，再按 `EnableTracing` / `EnableMetrics` 分别装配。

::: danger 只挂模块不改配置 = 什么都不会发生
`Enabled` 默认 `false`，OTel SDK 根本不装配。此时 `IMetricsCollector` 的调用是「装配即孤儿」——数据既不落内存也不导出，唯一开销是方法调用本身。链路那侧同理，各处 span 代码会被 `HasListeners()` 短路。
:::

## 打开 OpenTelemetry

开发环境（全采样 + 控制台看效果）：

```json
{
  "XiHan": {
    "Observability": {
      "Enabled": true,
      "ServiceName": "XiHan.BasicApp",
      "ServiceVersion": "1.0.0",
      "EnableTracing": true,
      "EnableMetrics": true,
      "SamplingRatio": 1.0,
      "ConsoleExporter": true
    }
  }
}
```

生产环境（降采样 + 只发 OTLP）：

```json
{
  "XiHan": {
    "Observability": {
      "Enabled": true,
      "ServiceName": "XiHan.BasicApp",
      "EnableTracing": true,
      "EnableMetrics": true,
      "SamplingRatio": 0.1,
      "ConsoleExporter": false,
      "OtlpEndpoint": "http://otel-collector:4317",
      "AdditionalSources": ["MyApp.Custom"]
    }
  }
}
```

开关与实际装配的对应关系：

| 开关 | 装配了什么 |
| --- | --- |
| `EnableTracing`（默认 `true`） | `SetSampler(ParentBased(TraceIdRatioBased))` + `AddSource(XiHanActivitySources.All)` + `AddAspNetCoreInstrumentation()` + `AddHttpClientInstrumentation()`，再追加 `AdditionalSources` |
| `EnableMetrics`（默认 `false`） | `AddMeter(MetricsCollector.MeterName)`，即 `"XiHan.Metrics"` |
| `ConsoleExporter` | 给已启用的 Tracing / Metrics 各加一个控制台导出器 |
| `OtlpEndpoint` 非空 | 给已启用的 Tracing / Metrics 各加一个 OTLP 导出器，`Endpoint` 取该值 |
| `EnableLogging` | **占位**，扩展方法中没有对应的 `WithLogging(...)` 装配代码 |

::: warning 两个默认值方向相反
`EnableTracing` 默认 `true`、`EnableMetrics` 默认 `false`。只把 `Enabled` 打开，你会拿到链路但拿不到指标。
:::

::: danger 采了不等于发得出去
`ConsoleExporter` 与 `OtlpEndpoint` 都不配时，SDK 装配完成但没有任何出口，表现是「开关全开却一条数据都看不到」。二者至少配一个。
:::

## 采样率的实际语义

采样器是 `ParentBasedSampler(new TraceIdRatioBasedSampler(Math.Clamp(SamplingRatio, 0d, 1d)))`。两层含义：

- **`ParentBased`**：请求带了 `traceparent` 且上游已标记采样时，本服务跟随上游决定，不重新掷骰；
- **`TraceIdRatioBased`**：只有作为链路根（没有远端父级）时，才按比例决定整条链路采不采。

::: warning 采样是链路级的，不是 span 级的
`SamplingRatio: 0.1` 的意思是「本服务发起的新链路里保留一成」，被选中的链路整条完整保留，没选中的整条丢弃。它不会让你「每种 span 都看到 10%」。同理，把本服务调低而上游仍是 1.0，上游进来的请求依旧全量落盘。
:::

`SamplingRatio` 会被 `Math.Clamp` 到 `0~1`，配成 `5` 或 `-1` 不会报错，等价于 `1` 和 `0`。

## 框架内置的 Span 源

`ActivitySource` 的名字与实例集中在 `XiHan.Framework.Core.Tracing.XiHanActivitySources`（放在 Core 而非本包，是为了让 Data / EventBus / Cache 这些只依赖 Core 的层也能发 span 而不产生环依赖）。`All` 汇总全部源名，装配时一次性 `AddSource`。

| 源名 | 常量 / 实例 | 当前谁在发 span |
| --- | --- | --- |
| `XiHan.App` | `App` / `AppSource` | 框架自身不发，留给业务层打点 |
| `XiHan.Data` | `Data` / `DataSource` | SqlSugar 执行回调，span 名 `db.query`，`ActivityKind.Client`，带 `db.system` / `db.statement` 标签，异常时置 Error 状态 |
| `XiHan.EventBus` | `EventBus` / `EventBusSource` | Broker 分布式事件消费入口，span 名 `eventbus.consume {事件名}`，`ActivityKind.Consumer` |
| `XiHan.Cache` | `Cache` / `CacheSource` | Redis 缓存的批量 / 模式匹配 / Lua 异步操作，`ActivityKind.Client`，带 `db.system=redis` |
| `XiHan.Grpc` | `Grpc` / `GrpcSource` | 常量与实例已定义并纳入 `All`，但**框架内暂无代码在此源上创建 span** |
| `XiHan.AI` | `Ai`（只有名字常量，无静态实例） | 由 AI 会话管道的 `UseOpenTelemetry` 中间件产出，源名取自 `AiPipelineOptions.TelemetrySourceName` |

::: tip 未开启时零开销的实现方式
Data / Cache / EventBus 的 span 代码开头都是 `source.HasListeners()` 判断，没有监听者直接 return。这是「默认关闭不付代价」的落点，也意味着这些 span 的有无完全由 `EnableTracing` 决定。
:::

::: warning AI 的 span 要开两处开关
`XiHan.AI` 源已在 `All` 里被 `AddSource`，但真正产出 span 的是 AI 管道中间件，需要 `XiHan:AI:Pipeline:EnableTelemetry` 也为 `true`。默认它是关的。
:::

## 业务代码打点

用内置的应用源：

```csharp
using var activity = XiHanActivitySources.AppSource.StartActivity("PlaceOrder");
activity?.SetTag("order.id", orderId);
activity?.SetTag("order.channel", "web");
```

::: warning 必须用 `activity?.`
没有监听者时 `StartActivity` 返回 `null`。写成 `activity.SetTag(...)` 在 OTel 关闭的环境里会直接空引用崩溃。
:::

自定义源要额外声明才会被采集：

```csharp
public static class MyAppSources
{
    public const string Custom = "MyApp.Custom";
    public static readonly ActivitySource CustomSource = new(Custom);
}
```

```json
{ "XiHan": { "Observability": { "AdditionalSources": ["MyApp.Custom"] } } }
```

框架内置的六个源由 `XiHanActivitySources.All` 自动纳入，不要在 `AdditionalSources` 里重复写。

## TraceId 与 W3C Activity

框架的 `TraceId` 不是自己造的编号，而是**优先取 `Activity.Current.TraceId`**。理解这条链，才能解释「为什么开了 OTel 之后日志里的 TraceId 长得不一样了」。

产出侧，`XiHanTraceIdMiddleware` 位于 Web.Api 管线最前（仅次于 `UseForwardedHeaders`），按优先级取值：

| 顺序 | 来源 | 形态 |
| --- | --- | --- |
| 1 | `Activity.Current.TraceId.ToHexString()` | W3C 32 位十六进制 |
| 2 | 入站请求头 `X-Trace-Id` | 调用方给什么就是什么 |
| 3 | `HttpContext.TraceIdentifier` | Kestrel 的「连接 ID:请求序号」 |

取到后写入 `HttpContext.Items["__XiHanTraceId"]`，并回写响应头 `X-Trace-Id`。

消费侧，同一个值散落在这些地方：

| 消费方 | 取值途径 |
| --- | --- |
| 统一响应体 `ApiResponse.TraceId` | Web.Api 响应过滤器 |
| 审计五表（访问 / 接口 / 操作 / 异常 / 登录日志）的 `TraceId` 字段 | 各中间件与过滤器 |
| `ITraceIdProvider`（实现 `HttpTraceIdProvider`） | 给不依赖 ASP.NET Core 的层用；同样先看 `Activity.Current`，再回退 Items / `TraceIdentifier` |
| 实体的 `ITraceableEntity.TraceId` | 数据层写入拦截时经 `ITraceIdProvider` 自动填充 |
| `ICorrelationIdProvider` | 先看 `Activity.Current.TraceId`，无 Activity 时回退 `Change()` 显式设置的值 |
| 日志模板中的 `{TraceId}` / `{SpanId}` | Serilog 从 `Activity.Current` 自动填充，无链路时渲染为空 |

::: warning 开启 OTel 会改变 TraceId 的形态
未启用时 TraceId 通常是 `HttpContext.TraceIdentifier`（形如 `0HN7...:00000001`）；启用后 ASP.NET Core instrumentation 会为每个请求建 Activity，TraceId 变成 32 位十六进制，与追踪后端里的链路可直接 join。如果你的日志检索规则、告警正则按旧格式写死，要一并调整。
:::

跨进程的异步链路也串得上：分布式事件发布时把 `ICorrelationIdProvider.Get()`（即当前 TraceId）作为 `correlationId` 随消息带走；消费端拿它重建一个 `isRemote: true` 的 `ActivityContext` 作为父级，消费 span 因此归入同一条 trace。值不是合法 32-hex 时退化为新的根 span，不会报错。

::: tip 发布时没有 Activity 就串不起来
后台作业、定时任务里发布事件时，若没有环境 Activity，`correlationId` 取到的是 `Change()` 设置的值或 `null`，消费端会开新链路。需要串联时在作业入口自己 `StartActivity`。
:::

## 指标

`IMetricsCollector` 是单例，底层是一个 `Meter("XiHan.Metrics")`：

```csharp
metricsCollector.RecordCounter("orders.created");
metricsCollector.RecordCounter("orders.created", 5, new() { ["channel"] = "web" });
metricsCollector.RecordHistogram("order.amount", 199.9);

using (metricsCollector.BeginTimer("order.handle"))
{
    // 业务处理；Dispose 时记录直方图 `order.handle.duration`（毫秒）
}
```

::: danger `GetMetrics()` 恒返回空列表
`MetricsCollector` 直出 `Meter`，进程内不留存任何指标；`GetMetrics()` 返回空集合、`Clear()` 是空操作，两者仅为接口成员保留。想做一个「读 `GetMetrics()` 渲染指标面板」的接口，拿不到任何数据——指标只能通过导出器看。
:::

::: warning `RecordMeasurement` 实际记的是直方图
没有 pull 型 gauge 的回调上下文，`RecordMeasurement` 内部直接委托给 `RecordHistogram`，分布与百分位交给后端聚合。`MetricType` 枚举里的 `Gauge` / `Summary` 当前没有对应的记录路径。
:::

## 性能统计与运行时诊断

这两块与 OTel 开关无关，`Enabled=false` 时照样可用。

```csharp
using (var tracker = performanceMonitor.BeginOperation("PlaceOrder"))
{
    tracker.AddTag("channel", "web");
    tracker.Checkpoint("validated");
    // ... 处理 ...
    tracker.Checkpoint("persisted");
}

var stats = performanceMonitor.GetStatistics();        // 总数/成功/失败、平均/最小/最大、P50/P95/P99，并按操作名分组
var slow = performanceMonitor.GetSlowOperations(500);  // 耗时 ≥ 500ms，按耗时倒序
```

::: warning 记录存在无上限的 `ConcurrentBag` 里
`PerformanceMonitor` 的记录只增不减，`Clear()` 是唯一的释放途径，进程重启即全丢。它适合「排查期临时打开、看完就清」，不适合当常驻监控——常驻请走 OTel 指标与链路。
:::

`IDiagnosticsService` 读进程当下状态，全部基于 BCL，无副作用（除 GC 那个方法外）：

| 方法 | 内容 |
| --- | --- |
| `GetSystemInfo()` | 操作系统描述与版本、机器名、CPU 核数、系统启动时间、用户名 |
| `GetRuntimeInfo()` | .NET 版本、进程 ID、应用启动时间与运行秒数、是否 64 位 |
| `GetMemoryInfo()` | 已分配字节、工作集、私有内存，以及各代 GC 次数与暂停占比 |
| `GetThreadInfo()` | 线程池线程数、可用/最大工作线程与 IO 线程、待处理工作项数 |
| `GetDiagnosticsReport()` | 上述四项一次性汇总 |
| `ForceGarbageCollection()` | 两轮 `GC.Collect()` 中间夹一次 `WaitForPendingFinalizers()` |

::: danger 不要把 `ForceGarbageCollection` 挂到匿名端点
它是真的强制回收并等待终结器，会造成停顿。只在受权限保护的运维接口里暴露，且不要给任何自动化调用。
:::

## 健康检查

`AddXiHanObservability` 只调了 `AddHealthChecks()` 注册基础设施，**一个检查项都没注册**。包内自带的唯一实现是 `MemoryHealthCheck`：

```csharp
services.AddHealthChecks()
        .AddCheck("memory", new MemoryHealthCheck(thresholdMb: 512));
```

它读 `GC.GetTotalMemory(false)`，超过阈值返回 `Degraded`（不是 `Unhealthy`），两种结果都附带各代 GC 次数与内存负载阈值明细。默认阈值 1024MB。

其余检查项（数据库、Redis、向量库等）需要由应用实现 `IHealthCheck` 并注册。下面以应用自己的检查类型为例，再在模块初始化时映射端点：

```csharp
public override void ConfigureServices(ServiceConfigurationContext context)
{
    context.Services.AddHealthChecks()
        .AddCheck<MyDatabaseHealthCheck>("database")
        .AddCheck<MyRedisHealthCheck>("redis");
}

public override void OnApplicationInitialization(ApplicationInitializationContext context)
{
    var app = context.GetApplicationBuilder();
    var options = new HealthCheckOptions { ResponseWriter = WriteMinimalHealthResponseAsync };
    if (app is IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health", options).AllowAnonymous();
    }
}
```

::: danger 开了 FallbackPolicy 时 `/health` 不加 `AllowAnonymous()` 会 401
`XiHan:Web:Api:Auth:RequireAuthenticatedUser`（默认 `false`）设为 `true` 时，Web.Api 才装配鉴权 FallbackPolicy，此时未标记匿名的端点一律被授权中间件拦下。另外，开放接口签名中间件（`XiHan:Web:Api:OpenApiSecurity:IsEnabled`）的 `ProtectedPathPrefixes` 默认是 `["/api"]`、不覆盖 `/health`；若把它改成覆盖 `/health` 的前缀（或置空表示全路径），还要把 `/health` 加进 `IgnoredPathPrefixes`。
:::

::: warning 默认响应体会外泄细节
标准健康检查响应包含每项的 `description` 与异常信息，连接串很容易顺着异常消息暴露。自定义 `ResponseWriter`，只回总状态与各项名称/状态。
:::

## 关键配置

配置节 `XiHan:Observability`，最常调的几项：

| 键 | 默认值 | 说明 |
| --- | --- | --- |
| `Enabled` | `false` | OTel 总开关，关闭则整个 SDK 不装配 |
| `EnableTracing` | `true` | 链路追踪，仅在 `Enabled=true` 时生效 |
| `EnableMetrics` | `false` | 指标导出，仅在 `Enabled=true` 时生效 |
| `SamplingRatio` | `1.0` | 根链路采样比例，自动 Clamp 到 `0~1` |
| `OtlpEndpoint` | `null` | OTLP 端点，为空则不装 OTLP 导出器 |
| `ConsoleExporter` | `false` | 控制台导出器，生产关闭 |

`ServiceName`（默认 `"XiHan.App"`）、`ServiceVersion`、`EnableLogging`、`AdditionalSources` 见 [Observability 包](../packages/observability) 的完整配置表。

## 常见问题

| 现象 | 原因 | 处理 |
| --- | --- | --- |
| 一条 trace 都没有 | `Enabled` 默认 `false`，OTel 未装配 | 在 `XiHan:Observability` 显式打开 |
| `Enabled` 开了仍无数据 | 既没配 `OtlpEndpoint` 也没开 `ConsoleExporter`，没有出口 | 二者至少配一个 |
| 有链路但没有指标 | `EnableMetrics` 默认 `false` | 显式设为 `true` |
| `EnableLogging` 开了没有日志导出 | 扩展方法未接入 `WithLogging`，该项为占位 | 日志走 Logging 包的落地方式 |
| 看不到 `db.query` span | `EnableTracing=false` 时 `DataSource` 无监听者，代码被 `HasListeners()` 短路 | 开启 `EnableTracing` |
| gRPC 没有 span | `XiHan.Grpc` 源已注册但框架内无代码在其上发 span | 需要则自行用 `GrpcSource` 打点 |
| AI 调用没有 span | 还需 `XiHan:AI:Pipeline:EnableTelemetry=true` | 两处开关一起开 |
| `GetMetrics()` 返回空 | 设计如此，指标不在进程内留存 | 通过导出器查看 |
| 日志里 TraceId 格式变了 | 开启 OTel 后从 `TraceIdentifier` 变为 W3C 32-hex | 同步调整日志检索与告警规则 |
| 日志 TraceId 为空 | 当前上下文没有 Activity（如未走 HTTP 管线的后台线程） | 在入口自行 `StartActivity` |
| 消费端与发布端不在同一条 trace | 发布时没有环境 Activity，`correlationId` 非法或为空 | 在作业/任务入口建 Activity |
| 进程内存持续上涨 | `PerformanceMonitor` 的 `ConcurrentBag` 无上限累积 | 定期 `Clear()`，或改用 OTel 指标 |
| `/health` 返回 401 | `RequireAuthenticatedUser=true` 时装配的鉴权 FallbackPolicy 拦截 | `MapHealthChecks(...).AllowAnonymous()` |
| `/health` 200 但没有任何检查项 | 模块只注册基础设施，不含检查项 | 应用侧 `AddCheck` |
| `StartActivity` 处空引用 | OTel 关闭时返回 `null` | 一律用 `activity?.` |

## 下一步

- [日志](../packages/logging) — `{TraceId}` / `{SpanId}` 输出模板与落地方式
- [审计](../packages/auditing) — 五类审计记录中的 `TraceId` 字段
- [数据访问](./data) — `db.query` span 与 `ITraceableEntity` 的填充时机
- [缓存与分布式锁](./caching) — Redis 操作 span 的覆盖范围
- [Web 应用开发](./web) — 中间件管线顺序与统一响应体
- [Observability 包](../packages/observability) — 完整 API 清单与全部配置项
- [Core 包](../packages/core) — `XiHanActivitySources` 与 `ICorrelationIdProvider` 的定义位置
