# 日志与可观测性

这一章讲内核为每次调用发出的追踪与指标、框架与应用怎样写日志、怎样用 `Observability.OpenTelemetry` 把三类信号导出到后端并控制采样、怎样给业务打点，以及用 `Observability.Host` 读取进程与主机的运行信息做服务监控页。读完后，我们能把应用接进 OTLP 后端，并在管理界面上看到 CPU、内存与磁盘。前置知识是[执行管道](/startup/pipeline)与[生命周期与健康](/startup/lifecycle)。

## 为什么需要

线上一个请求慢了，要知道慢在哪个阶段、调了哪些下游、同一链路里的日志是哪几条；容量规划要知道每秒多少次调用、失败率多少、机器还剩多少内存。这些信号如果靠各处手写，格式不统一、trace-id 对不上、换监控系统要改代码。框架不包装自己的追踪或指标接口，直接用 .NET 的 `ActivitySource`、`Meter` 与 `ILogger`：内核在 Observe 阶段为每次调用产生 Span、计数与耗时，OpenTelemetry 包按配置把它们连同日志导出，运行信息包不启动外部命令就能读到主机状态。

## 内核发出什么

内核只依赖 `System.Diagnostics` 与 `Microsoft.Extensions.Logging.Abstractions`。`FrameworkDiagnostics` 持有一个 `ActivitySource` 与一个 `Meter`，名字都是 `XiHan.Framework`（`FrameworkDiagnostics.SourceName`），版本是框架版本。Observe 阶段的内置中间件 `ObserveMiddleware` 为每次调用产生前三个信号，插件宿主调用插件时另记一条 Span：

| 信号 | 名字 | 内容 |
| --- | --- | --- |
| Span | `xihan.invoke` | `ActivityKind.Internal`；父级见下文「采样」；标签 `xihan.outcome`，操作调用另带 `xihan.operation`（创建时给出，采样器看得到）；失败时状态为 Error，描述是错误码 |
| 计数器 | `xihan.invocations` | 单位 `{invocation}`，每次调用加一，标签 `xihan.outcome`，操作调用另带 `xihan.operation` |
| 直方图 | `xihan.invocation.duration` | 单位 `ms`，调用耗时，标签同上 |
| Span | `xihan.plugin.invoke` | `ActivityKind.Client`；宿主调用进程外或 Wasm 插件的操作、向插件投递事件各一条；管道里有 `xihan.invoke` 时是它的子 Span；标签 `xihan.plugin.id`（插件的清单 ID），操作调用另带 `xihan.operation`，投递不带；失败时状态为 Error，描述是错误码 |

`xihan.outcome` 取 `success`、`failure` 或 `unset`；`unset` 表示管道走完也没有写入结果，用来发现漏写结果的处理器。成功、失败与取消都会记指标。调用期间的未处理异常由 `ObserveMiddleware` 以 Error 级记一条日志（`Unhandled exception during invocation '{TraceId}'.`），并转成 `KERNEL_UNEXPECTED_001` 结果；取消不在这里吞掉，由 Resilience 阶段区分超时与外部取消。

`xihan.operation` 是操作 ID，只取登记表里操作描述的 ID：未知的操作 ID 不进管道，也就没有 Span 与指标，维度的取值数不超过登记的操作数。不是操作调用时（消息处理器、作业、直接交给 `App.InvokeAsync` 的处理器、没有匹配到操作端点的请求）不带这个标签，不写占位值，主题、作业名与请求路径也不进标签。两个指标的时间序列因此按操作分开：先聚合再比较的查询（`sum(...)`、`sum by (xihan_outcome)(...)`）结果不变，直接按 `{xihan_outcome="failure"}` 取单条序列的查询要加 `sum`。操作很多、后端按序列计费时，在导出一侧用视图去掉这个维度，见下文「去掉操作维度」。

`xihan.plugin.invoke` 覆盖从进入插件载体到它返回的时间，含等插件可用的时间；宿主在它之内组装下发给插件的元数据，写给插件的 `traceparent` 以它为父级，插件一侧的 Span 与插件带回来的回调都挂在它下面。

没有任何监听者时，`StartActivity` 返回 `null`，计数与直方图的记录也几乎没有开销。接收这些信号的是 OpenTelemetry 或任何订阅同名源与仪表的工具，内核不关心是谁。

## 日志

框架内部全部用 `Microsoft.Extensions.Logging`，日志点以 `[LoggerMessage]` 源生成，模板是英文；类别名是类型全名，按命名空间前缀调级别即可：

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "XiHan.Framework": "Warning",
      "XiHan.Framework.Auditing": "Information"
    }
  }
}
```

上例把框架日志调到 Warning，同时保留审计事件的 Information 日志。

框架不包装 Serilog 或其他日志库：应用照常注入 `ILogger<T>`，需要第三方日志库时按该库的文档接到 `Microsoft.Extensions.Logging`，框架日志照常进去。装配 `Observability.OpenTelemetry` 且 `Logging` 开启时，日志经 OpenTelemetry 的日志提供者与追踪、指标一起导出。

应用自己的日志也建议用 `[LoggerMessage]`：模板在编译期解析，不装箱，Native AOT 下可用。

## 最小示例

装配 OpenTelemetry 导出，把追踪、指标与日志发到本机的 OTLP 收集器：

```bash
dotnet add package XiHan.Framework.Observability.OpenTelemetry --prerelease
```

```csharp
// 接在第一个应用的 Program.cs 上，只多一行 UseFeature
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<OpenTelemetryObservabilityFeature>());   // using XiHan.Framework.Observability.OpenTelemetry;
```

```json
{
  "XiHan": {
    "Observability": {
      "OpenTelemetry": {
        "ServiceName": "orders",
        "SamplingRatio": 0.2,
        "Otlp": { "Endpoint": "http://localhost:4317", "Protocol": "Grpc" },
        "Console": false,
        "Sources": "MyApp.Orders",
        "Meters": "MyApp.Orders,MyApp.Auditing"
      }
    }
  }
}
```

`ServiceName` 编成 UTF-8 作为每个信号的资源属性 `service.name`，只有空白或有孤立的代理项时装配失败（`ArgumentException`，ADR-0334）。

运行后：每次经管道的调用在收集器里出现一个名为 `xihan.invoke` 的 Span（HTTP 请求下挂在 ASP.NET Core 的请求 Span 之下），指标里有 `xihan.invocations` 与 `xihan.invocation.duration`，日志以资源 `service.name = orders` 导出；把 `Console` 改为 `true` 可以先在控制台看到同样的内容。`Profiles.Full` 已包含这个特性，只需补配置。

## 导出到 OpenTelemetry

`Observability.OpenTelemetry` 提供 `observability.export@1`（`role: default`），把内核与 .NET、ASP.NET Core 内置的源与仪表接到 OpenTelemetry，再按配置装配导出器。导出器的启用规则：

1. `Otlp:Enabled` 显式为 `true` / `false` 时按它。
2. 没写 `Otlp:Enabled` 时，配置了 `Otlp:Endpoint` 或设置了环境变量 `OTEL_EXPORTER_OTLP_ENDPOINT` 就启用 OTLP 导出器，否则不导出，也不会向本机的空端口反复重试。
3. 有 `Otlp:Endpoint` 时按它与 `Otlp:Protocol`（`Grpc` 或 `HttpProtobuf`）建一个跨三类信号的导出器；只靠环境变量时，端点与协议都按 OpenTelemetry 的标准环境变量与默认端口（gRPC 4317、HTTP 4318）决定；写了 `Otlp:Protocol` 却没写 `Otlp:Endpoint` 时装配失败（`ArgumentException`），不会被静默忽略。
4. `Console` 为 `true` 时三类信号同时写到控制台导出器，只用于开发。

容器与 Kubernetes 里通常只给环境变量：

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 dotnet run
```

### 订阅的源与仪表

缺省订阅框架自己的 `XiHan.Framework`，以及 ASP.NET Core、`HttpClient` 与运行时的 Activity 源和 Meter（名单见 [Observability.OpenTelemetry](/packages/observability-opentelemetry#主要类型)）；应用自己的名字用 `Sources` 与 `Meters` 追加，逗号分隔。ASP.NET Core 与 `HttpClient` 不装检测包，按名字订阅它们原生发出的 Activity 与 Meter。资源属性 `service.name` 缺省取入口程序集名（取不到时为 `xihan`），`service.version` 取入口程序集版本，`service.instance.id` 取机器名。

### 采样

采样器是 `ParentBasedSampler(TraceIdRatioBasedSampler(SamplingRatio))`：没有父级的根 Span 按 `SamplingRatio`（0 到 1，缺省 1）的比例采样，有父级的沿用父级的决定。`xihan.invoke` 的父级与采样按规范 RFC-0003《调用上下文》：当前 Activity（ASP.NET Core 的请求 Span、外层调用）与调用上下文同一 trace 时是它的子 Span；否则调用上下文带着上游 `traceparent` 的标志时以上游为远端父级，上游「不采样」就不采样；没有上游时是链路的根 Span，trace-id 用调用上下文的（与 Problem Details、审计、日志一致），按 `SamplingRatio` 采样。没有 `traceparent` 的 HTTP 请求并入 ASP.NET Core 的请求 Span，两者同一 trace-id。消息沿用同一套规则：发布时信封的 `traceparent` 带发布方的采样标志（父级是发布时的 `xihan.invoke`），处理器的调用从它还原，上游「不采样」的链路在消费端也不采样。

### 去掉操作维度

`xihan.operation` 让两个指标的序列数从「结果数」变成「操作数 × 结果数」，直方图再乘桶数。不需要按操作看指标时，用 OpenTelemetry 的视图只留 `xihan.outcome`，Span 上的标签不受影响：

```csharp
using OpenTelemetry.Metrics;

builder.Services.ConfigureOpenTelemetryMeterProvider(static (_, metrics) => metrics
    .AddView("xihan.invocations", new MetricStreamConfiguration { TagKeys = ["xihan.outcome"] })
    .AddView("xihan.invocation.duration", new MetricStreamConfiguration { TagKeys = ["xihan.outcome"] }));
```

## 业务打点

应用的追踪与指标直接用 BCL 的 `ActivitySource` 与 `Meter`，把名字加进 `Sources` 与 `Meters` 才会被导出：

```csharp
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MyApp.Diagnostics;

public static class OrderDiagnostics
{
    // 名字要与 XiHan:Observability:OpenTelemetry 的 Sources / Meters 一致才会被导出
    public const string Name = "MyApp.Orders";

    public static ActivitySource ActivitySource { get; } = new(Name);

    public static Meter Meter { get; } = new(Name);

    public static Counter<long> Exported { get; } = Meter.CreateCounter<long>("myapp.orders.exported", "{order}", "Orders written to export files.");
}
```

```csharp
using Microsoft.Extensions.Logging;

namespace MyApp.Diagnostics;

public sealed partial class OrderExporter(ILogger<OrderExporter> logger)
{
    public async Task ExportAsync(string month, CancellationToken cancellationToken)
    {
        // 没有监听者时 StartActivity 返回 null，打点没有额外开销
        using var activity = OrderDiagnostics.ActivitySource.StartActivity("orders.export");
        activity?.SetTag("myapp.month", month);

        await Task.Delay(10, cancellationToken);
        var rows = 1200;

        OrderDiagnostics.Exported.Add(rows);
        LogExported(logger, month, rows);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Exported {Rows} orders for {Month}.")]
    private static partial void LogExported(ILogger logger, string month, int rows);
}
```

有监听者时，操作处理器里的 `Activity.Current` 就是 `xihan.invoke`，业务 Span 自动成为它的子 Span。

## 运行信息

`Observability.Host` 提供 `observability.host@1`（`role: default`，规范 RFC-0023《可观测性：导出与运行信息》）：单例 `IHostMonitor` 读进程与所在主机的 CPU、内存、磁盘、网卡、GPU、主板与运行时长，供管理界面的服务监控使用。契约在 `Observability.Abstractions`，只依赖原语层。

`IHostMonitor` 有七个读取方法：`ReadRuntimeAsync`（进程与运行时）、`ReadProcessorAsync`、`ReadMemoryAsync`、`ReadDisksAsync`、`ReadNetworkInterfacesAsync`、`ReadGpusAsync` 与 `ReadBoardAsync`，各返回一份不可变记录，字段见 [Observability.Abstractions](/packages/observability-abstractions#主要类型)。读取的约定：

- 不启动外部命令：只读 BCL、`/proc` 与 `/sys`、注册表与 kernel32，不用 WMI、PowerShell、`dmidecode`、`lspci`、`nvidia-smi`。读取不会挂起，也没有命令注入面。
- 读不到的项为 `null`，列表读不到为空列表，不以 0 或空串顶替，界面据此区分「没有」与「为零」。单项失败只让该项为空，读取不抛出。
- CPU 型号与核心、GPU、主板首次读取后缓存；内存、磁盘、网卡与运行时每次当场读。
- 使用率与速率是两次采样之差，由后台采样器负责：启动时同步采一次，之后每 `SampleIntervalSeconds`（缺省 5，取值 (0, 3600]）采一次；读取只返回最近算出的值，不睡眠、不等待，启动后第一个间隔内为 `null`。一次采样失败记一条警告，保留上一次的值。
- 不暴露命令行参数、环境变量与当前目录。

Linux 与 Windows 读全部项；macOS 等其他平台只有 BCL 读得到的部分，CPU 型号与使用率、内存、主板的各项为 `null`，GPU 是空列表。容器里的内存与 CPU 使用率是宿主机的数值，不是 cgroup 限额；限额用 .NET 自带的 `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes` 与 `Environment.ProcessorCount`（它们已按 cgroup 调整）。各平台的读取来源与读不到的项见 [Observability.Host](/packages/observability-host#行为与限制)。

把运行信息暴露给服务监控页：

```bash
dotnet add package XiHan.Framework.Observability.Host --prerelease
```

```csharp
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Observability.Abstractions;
using XiHan.Framework.Observability.Host;

// builder.Build() 之前：装配特性，并把下面的序列化上下文插进 HTTP JSON 选项
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, MonitorJsonContext.Default));
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<HostObservabilityFeature>());

// app.UseXiHan() 之后。读取只返回后台采样的最近值，不阻塞请求线程；机器信息只给认证过的调用方
var monitor = app.MapGroup("/monitor").RequireXiHanAuthentication();
monitor.MapGet("/runtime", (IHostMonitor host, CancellationToken ct) => host.ReadRuntimeAsync(ct));
monitor.MapGet("/cpu", (IHostMonitor host, CancellationToken ct) => host.ReadProcessorAsync(ct));
monitor.MapGet("/memory", (IHostMonitor host, CancellationToken ct) => host.ReadMemoryAsync(ct));
monitor.MapGet("/disks", (IHostMonitor host, CancellationToken ct) => host.ReadDisksAsync(ct));
```

```csharp
using System.Text.Json.Serialization;
using XiHan.Framework.Observability.Abstractions;

namespace MyApp.Diagnostics;

// 源生成的序列化上下文，端点返回运行信息时不走反射
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RuntimeInfo))]
[JsonSerializable(typeof(ProcessorInfo))]
[JsonSerializable(typeof(MemoryInfo))]
[JsonSerializable(typeof(IReadOnlyList<DiskInfo>))]
internal sealed partial class MonitorJsonContext : JsonSerializerContext;
```

`Profiles.Full` 已装配 `HostObservabilityFeature`，后台采样循环默认每 5 秒读一次 CPU 时间与网卡计数。`RequireXiHanAuthentication` 的用法见[端点里使用框架](/web/endpoints)。

## 配置

追踪、指标、日志的开关与导出器的键都在 `XiHan:Observability:OpenTelemetry` 下，见 [Observability.OpenTelemetry](/packages/observability-opentelemetry#配置)；运行信息只有采样间隔 `XiHan:Observability:Host:SampleIntervalSeconds`，见 [Observability.Host](/packages/observability-host#配置)。取值不合法时（比例越界、端点不是绝对 URI、布尔不是 `true` / `false`、采样间隔越界）装配阶段抛出 `ArgumentException`，应用启动失败。

## 与其他能力的关系

- 健康与探针：特性的健康条目、`/health/startup`、`/health/ready`、`/health/live` 三个探针在[生命周期与健康](/startup/lifecycle)，本章不重复。
- 审计：日志、追踪与指标面向运维；谁在什么时候做了什么，是业务记录，见[审计](/common/auditing)。两者共享同一个 trace-id，可以从审计事件跳到链路。
- 执行管道：Observe 是管道的第二个阶段，见[执行管道](/startup/pipeline)。
- 弹性：取消与超时的区分由 Resilience 阶段完成，见[弹性与限流](/common/resilience)。

## 相关包与下一步

- [Observability.Abstractions](/packages/observability-abstractions)：`IHostMonitor` 与运行信息类型
- [Observability.OpenTelemetry](/packages/observability-opentelemetry)：导出器、采样与配置
- [Observability.Host](/packages/observability-host)：运行信息的采样与平台支持
- [Kernel](/packages/kernel)：`FrameworkDiagnostics` 与 `ObserveMiddleware`
- 下一页：[弹性与限流](/common/resilience)
