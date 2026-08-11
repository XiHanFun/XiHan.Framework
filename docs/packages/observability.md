# XiHan.Framework.Observability

> 可观测性基础库：健康检查、指标采集、性能监控、运行时诊断，并内置可选的 OpenTelemetry 装配（链路追踪 / 指标导出），默认零运行时开销、按需开启。

- **NuGet**：`XiHan.Framework.Observability`
- **模块类**：`XiHanObservabilityModule`
- **所在层**：基础设施层
- **关键依赖**：`Microsoft.Extensions.Diagnostics.HealthChecks`、`System.Diagnostics.PerformanceCounter`、`OpenTelemetry.Extensions.Hosting` 系列包（Tracing/Metrics/OTLP/Console 导出器）；框架内部仅依赖 `XiHan.Framework.Core`

## 概述

XiHan.Framework.Observability 把「应用当前健康吗、跑得快不快、进程内部状态如何」这类运行时观察能力封装成一组可注入服务，覆盖四个方向：健康检查、指标采集、性能监控、运行时诊断。性能监控与运行时诊断全部基于 BCL（`GC`、`Process`、`ThreadPool`、`Stopwatch` 等）与 `Microsoft.Extensions.Diagnostics.HealthChecks` 实现，采集结果保存在进程内内存中，供上层应用自行读取、展示或转发。

包内同时内置了 OpenTelemetry SDK 的**可选**装配：由 `XiHanObservabilityOptions.Enabled` 总开关控制（默认 `false`），关闭时保持「装配即孤儿、零运行时行为」；开启后可选择性启用链路追踪（Tracing，基于 `XiHan.Framework.Core.Tracing.XiHanActivitySources` 汇集的框架内置 `ActivitySource`）与指标导出（Metrics，`IMetricsCollector` 底层 `Meter` 直出），支持 OTLP 与控制台导出器。**不内置** Prometheus/Jaeger/Grafana 等具体后端，只负责按标准协议把数据导出去。

## 何时使用

- 想在代码里记录计数器、测量值（Gauge）、直方图，或对一段操作计时——底层直接对接 `System.Diagnostics.Metrics.Meter`，可选导出到 OTel 后端。
- 需要监控关键操作耗时、汇总 P50/P95/P99 统计、筛出慢操作（进程内存，不依赖 OTel）。
- 想读取进程的系统、运行时、内存、线程等诊断信息，或按需触发 GC。
- 需要一个基于内存阈值的健康检查（`MemoryHealthCheck`）接入 ASP.NET Core 健康检查端点。
- 需要把框架内置的链路 Span（`XiHan.Data`/`XiHan.EventBus`/`XiHan.Grpc`/`XiHan.Cache`/`XiHan.AI`/`XiHan.App`）与自定义指标导出到 OTLP 兼容后端（如 Jaeger、Tempo、Prometheus 网关）。
- 不适用：需要具体 APM 产品的深度集成、指标长期持久化或自带可视化面板时，本包只负责标准化采集/导出，不提供，需要另行接入相应生态。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Observability
```

```csharp
[DependsOn(typeof(XiHanObservabilityModule))]
public class MyModule : XiHanModule { }
```

模块在 `ConfigureServices` 中调用扩展方法 `AddXiHanObservability(configuration)`，它依次做：

1. 绑定配置节 `XiHan:Observability` 到 `XiHanObservabilityOptions`（同时 `services.Configure<XiHanObservabilityOptions>`）；
2. 调用 `services.AddHealthChecks()` 注册健康检查基础设施（`HealthCheckService`）；
3. 以**单例**注册 `IMetricsCollector` → `MetricsCollector`；
4. 以**单例**注册 `IPerformanceMonitor` → `PerformanceMonitor` 与 `IDiagnosticsService` → `DiagnosticsService`；
5. 若 `options.Enabled` 为 `false`（默认值），到此为止直接返回，**不装配** OpenTelemetry SDK；
6. 若 `options.Enabled` 为 `true`，调用 `services.AddOpenTelemetry()`，配置 `Resource`（`service.name`/`service.version`），并按 `EnableTracing`/`EnableMetrics` 分别装配 `WithTracing(...)` / `WithMetrics(...)`（见「配置项」一节）。

> 注意：`AddXiHanObservability` **不会**注册任何具体的 `IHealthCheck`（包括 `MemoryHealthCheck`）。健康检查项需要你在应用侧显式 `AddCheck` 接入（见「使用示例」）。OpenTelemetry 的 Logging（OTLP 日志导出）目前仅有 `EnableLogging` 配置项占位，扩展方法尚未接入实际的 `WithLogging(...)` 装配（源码注释标注「后续增量」），暂不要依赖它导出日志。

## 配置项（`XiHanObservabilityOptions`）

绑定配置节 `XiHan:Observability`：

| 属性 | 默认值 | 说明 |
| --- | --- | --- |
| `Enabled` | `false` | OpenTelemetry 总开关；关闭则不装配 SDK，仅保留自研诊断/指标/性能与健康检查 |
| `ServiceName` | `"XiHan.App"` | 写入 OTel `Resource` 的 `service.name` |
| `ServiceVersion` | `null` | 写入 OTel `Resource` 的 `service.version` |
| `EnableTracing` | `true` | 是否启用链路追踪（`WithTracing`） |
| `EnableMetrics` | `false` | 是否启用指标导出（`WithMetrics`） |
| `EnableLogging` | `false` | 是否启用日志导出（OTLP Logs）；**当前仅占位，扩展方法未接入实际装配** |
| `SamplingRatio` | `1.0` | 采样率（0~1），`ParentBased(TraceIdRatioBased)` |
| `OtlpEndpoint` | `null` | OTLP 导出端点（如 `http://localhost:4317`）；为空则不启用 OTLP 导出器 |
| `ConsoleExporter` | `false` | 是否额外输出到控制台导出器（dev 友好，prod 建议关闭） |
| `AdditionalSources` | `[]` | 应用自定义要监听的 `ActivitySource` 名列表；框架内置源由 `XiHanActivitySources.All` 自动纳入，无需在此重复声明 |

`Enabled=true` 且 `EnableTracing=true` 时，追踪装配会固定加上 `AddSource(XiHanActivitySources.All)`、`AddAspNetCoreInstrumentation()`、`AddHttpClientInstrumentation()`；`EnableMetrics=true` 时指标装配固定 `AddMeter(MetricsCollector.MeterName)`（即 `"XiHan.Metrics"`）。`ConsoleExporter`/`OtlpEndpoint` 对 Tracing 与 Metrics 是否导出各自独立生效。

## 核心能力

- **指标采集（`IMetricsCollector` / `MetricsCollector`）**：记录计数器、测量值、直方图，`BeginTimer` 返回可释放计时器（`Dispose` 时以 `{name}.duration` 记录耗时直方图）。底层基于 `System.Diagnostics.Metrics.Meter`（`MeterName = "XiHan.Metrics"`）直出 OTel 指标管道——`RecordMeasurement` 内部委托给 `RecordHistogram`（无 pull 型 Gauge 回调上下文）；**不再内存留存**，`GetMetrics()` 恒返回空列表、`Clear()` 为空操作（仅为接口向后兼容保留）。是否真正被导出取决于「配置项」一节的 `Enabled`/`EnableMetrics` 是否开启。
- **性能监控（`IPerformanceMonitor` / `PerformanceMonitor`）**：`BeginOperation` 返回 `IPerformanceTracker`，支持打标签与记录检查点；记录保存在进程内 `ConcurrentBag<PerformanceRecord>`，汇总为 `PerformanceStatistics`（含成功/失败计数、平均/最小/最大耗时与 P50/P95/P99），并可按阈值筛出慢操作。与 OTel 装配无关，不受 `Enabled` 影响。
- **运行时诊断（`IDiagnosticsService` / `DiagnosticsService`）**：读取系统、运行时、内存、线程信息，强制触发 GC，或一次性生成 `DiagnosticsReport` 完整报告。
- **健康检查（`IHealthCheck` 实现）**：`MemoryHealthCheck` 为真实实现——按内存阈值判定 `Healthy`/`Degraded` 并附带 GC 明细。
- **链路追踪源（`XiHan.Framework.Core.Tracing.XiHanActivitySources`）**：框架共享的 `ActivitySource` 名与实例，定义在 `Core` 而非本包（避免 Data/EventBus/Http/Web 等既依赖 Core 又要发 Span 却不能反向依赖 Observability）；内置 `App`/`Data`/`EventBus`/`Grpc`/`Cache`/`Ai` 六个源，`All` 汇总供 OTel `AddSource` 一次性注册。

## 主要 API / 类型

### 指标采集

| 类型 | 说明 |
| --- | --- |
| `IMetricsCollector` | `void RecordCounter(string name, long value = 1, Dictionary<string,string>? tags = null)`、`void RecordMeasurement(string, double, ...)`、`void RecordHistogram(string, double, ...)`、`IDisposable BeginTimer(string, ...)`、`IReadOnlyList<MetricData> GetMetrics()`（恒空）、`void Clear()`（空操作） |
| `MetricsCollector.MeterName` | `const string` = `"XiHan.Metrics"`；`WithMetrics().AddMeter(MetricsCollector.MeterName)` 时使用 |
| `MetricData` | 单条指标模型：`Name` / `Type`（`MetricType`）/ `Value` / `Tags` / `Timestamp` / `Unit`（当前仅作为 `GetMetrics()` 的返回元素类型保留，不再被实际填充） |
| `MetricType` | 枚举：`Counter`、`Gauge`、`Histogram`、`Summary` |

### 性能监控

| 类型 | 说明 |
| --- | --- |
| `IPerformanceMonitor` | `IPerformanceTracker BeginOperation(string operationName)`、`PerformanceStatistics GetStatistics()`、`IReadOnlyList<PerformanceRecord> GetSlowOperations(double thresholdMs = 1000)`、`void Clear()` |
| `IPerformanceTracker` | `IDisposable`；`string OperationName`、`void AddTag(string,string)`、`void Checkpoint(string)` |
| `PerformanceRecord` | 单次操作记录：起止时间、`DurationMs`、`Tags`、`Checkpoints`、`Success`、`Exception` |
| `PerformanceStatistics` | 汇总统计：总数/成功/失败、平均/最小/最大耗时、P50/P95/P99、按操作名分组的 `OperationStatistics` |

### 运行时诊断

| 类型 | 说明 |
| --- | --- |
| `IDiagnosticsService` | `SystemInfo GetSystemInfo()`、`RuntimeInfo GetRuntimeInfo()`、`MemoryInfo GetMemoryInfo()`、`ThreadInfo GetThreadInfo()`、`void ForceGarbageCollection()`、`DiagnosticsReport GetDiagnosticsReport()` |
| `DiagnosticsReport` | 汇总 `SystemInfo` / `RuntimeInfo` / `MemoryInfo`（含 `GCInfo`）/ `ThreadInfo` 与生成时间 |

### 健康检查

| 类型 | 说明 |
| --- | --- |
| `MemoryHealthCheck` | **真实实现**。构造参数 `thresholdMb`（默认 1024MB）；超阈值返回 `Degraded`，否则 `Healthy`，均附带 GC 明细数据 |

数据库、Redis 与向量库检查不属于本包。应用应针对自己实际使用的客户端实现 `IHealthCheck`，避免通用占位检查给出错误的健康状态。

### 链路追踪源（`XiHan.Framework.Core.Tracing`）

| 类型 | 说明 |
| --- | --- |
| `XiHanActivitySources` | 静态类，位于 `XiHan.Framework.Core`。常量 `App`/`Data`/`EventBus`/`Grpc`/`Cache`/`Ai` 为源名字符串；对应 `AppSource`/`DataSource`/`EventBusSource`/`GrpcSource`/`CacheSource` 为可直接 `StartActivity` 的 `ActivitySource` 实例；`All`（`string[]`）汇总六个源名，供 `WithTracing().AddSource(XiHanActivitySources.All)` 一次性注册 |

## 使用示例

指标与计时：

```csharp
// 计数与耗时直方图（BeginTimer 在 Dispose 时记录 {name}.duration）
metricsCollector.RecordCounter("orders.created");
using (metricsCollector.BeginTimer("order.handle"))
{
    // 业务处理
}
```

性能监控与慢操作：

```csharp
using (var tracker = performanceMonitor.BeginOperation("PlaceOrder"))
{
    tracker.AddTag("channel", "web");
    tracker.Checkpoint("validated");
    // ... 处理 ...
}

var stats = performanceMonitor.GetStatistics();        // P50/P95/P99 等
var slow = performanceMonitor.GetSlowOperations(500);  // 超过 500ms 的操作
```

接入健康检查端点（需在应用侧显式注册检查项）：

```csharp
// 模块 AddXiHanObservability 已调用 AddHealthChecks()，此处补充具体检查项
services.AddHealthChecks()
        .AddCheck("memory", new MemoryHealthCheck(thresholdMb: 512));

// Program.cs
app.MapHealthChecks("/health");
```

启用 OpenTelemetry 装配（appsettings.json，开发环境示例——链路追踪 + 控制台导出）：

```json
{
  "XiHan": {
    "Observability": {
      "Enabled": true,
      "ServiceName": "XiHan.BasicApp",
      "EnableTracing": true,
      "EnableMetrics": true,
      "SamplingRatio": 1.0,
      "ConsoleExporter": true,
      "OtlpEndpoint": "http://localhost:4317",
      "AdditionalSources": ["MyApp.Custom"]
    }
  }
}
```

框架内置源自动纳入追踪，业务代码可直接用 `XiHanActivitySources` 打点，或通过 `AdditionalSources` 声明自定义源：

```csharp
using var activity = XiHanActivitySources.AppSource.StartActivity("PlaceOrder");
activity?.SetTag("order.id", orderId);
```

## 扩展点 / 自定义

- 三个核心服务均以 `AddSingleton`（非 `TryAdd`）注册；如需替换 `IMetricsCollector` / `IPerformanceMonitor` / `IDiagnosticsService` 实现，在本模块之后再次注册你的实现覆盖即可。
- 健康检查采用 `Microsoft.Extensions.Diagnostics.HealthChecks` 标准机制，可自定义 `IHealthCheck` 后通过 `AddCheck` / `AddCheck<T>` 接入。
- 应用层可通过 `XiHanObservabilityOptions.AdditionalSources` 声明自定义 `ActivitySource` 名，随框架内置源一并被 `WithTracing().AddSource(...)` 采集，无需自行装配 OTel `TracerProvider`。

## 注意事项与最佳实践

- **OpenTelemetry 默认关闭**：`XiHanObservabilityOptions.Enabled` 默认 `false`，`AddXiHanObservability` 不装配 OTel SDK，`IMetricsCollector` 的 `Meter` 调用是「装配即孤儿」——数据既不落内存也不导出，唯一开销是 API 调用本身；需要链路追踪/指标导出时在 `XiHan:Observability` 节显式开启。
- **指标不再内存留存**：`MetricsCollector` 改为直接对接 `System.Diagnostics.Metrics.Meter`，`GetMetrics()` 恒返回空列表、`Clear()` 为空操作——若需要查看指标数据，必须开启 OTel 装配并接入导出器（控制台/OTLP），不能再通过 `GetMetrics()` 在进程内读取。
- **性能/诊断仍是内存存储、非持久**：`PerformanceMonitor` 的记录保存在进程内 `ConcurrentBag<PerformanceRecord>`，进程重启即丢失，且会持续累积，长时间运行需自行择机调用 `Clear()`；此行为与 OTel 开关无关。
- **依赖检查由应用实现**：本包不提供数据库或 Redis 检查；应使用应用实际注册的数据库、缓存与向量库客户端做真实探测。
- **健康检查需手动注册**：模块只注册了基础设施，不含任何检查项——不 `AddCheck` 则 `/health` 端点不会体现内存/依赖状态。
- **OTLP Logs 尚未接入**：`EnableLogging` 配置项存在，但扩展方法当前没有对应的 `WithLogging(...)` 装配代码，开启该项目前不会产生任何日志导出效果。

## 依赖模块

- [XiHan.Framework.Core](./core) — 唯一的框架内部 `ProjectReference`（模块化、DI 基础，以及 `XiHanActivitySources` 共享追踪源）。
- 第三方：`Microsoft.Extensions.Diagnostics.HealthChecks`、`System.Diagnostics.PerformanceCounter`；`OpenTelemetry.Extensions.Hosting`、`OpenTelemetry.Instrumentation.AspNetCore`、`OpenTelemetry.Instrumentation.Http`、`OpenTelemetry.Exporter.OpenTelemetryProtocol`、`OpenTelemetry.Exporter.Console`（均按 `Enabled` 开关条件装配，未开启时不产生运行时行为）。**不内置** Prometheus/Jaeger 等具体后端组件，只做标准协议导出。

## 相关模块

- [XiHan.Framework.Logging](./logging) — 日志能力，与可观测性同属运行时观察范畴。
- [XiHan.Framework.Timing](./timing) — 时间与计时基础，可配合性能监控使用。
