# 可观测性

本章讲内核发出的追踪与指标、框架与应用怎样写日志、怎样用 `Observability.OpenTelemetry` 导出到后端，以及用 `Observability.Host` 读取进程与主机的运行信息，适用于要接入监控与服务监控页的应用。完整 API 见 [Observability.OpenTelemetry 包](../packages/observability-opentelemetry)与 [Observability.Host 包](../packages/observability-host)。

## 内核发出什么

内核只依赖 `System.Diagnostics` 与 `Microsoft.Extensions.Logging.Abstractions`，不包装自己的追踪或指标接口。`FrameworkDiagnostics` 持有一个 `ActivitySource` 与一个 `Meter`，名字都是 `XiHan.Framework`（`FrameworkDiagnostics.SourceName`），版本是框架版本。Observe 阶段的内置中间件 `ObserveMiddleware` 为每次调用产生以下信号：

| 信号 | 名字 | 内容 |
| --- | --- | --- |
| Span | `xihan.invoke` | `ActivityKind.Internal`；父级取自调用上下文的 trace-id 与 span-id；标签 `xihan.outcome`；失败时状态为 Error，描述是错误码 |
| 计数器 | `xihan.invocations` | 单位 `{invocation}`，每次调用加一，标签 `xihan.outcome` |
| 直方图 | `xihan.invocation.duration` | 单位 `ms`，调用耗时，标签 `xihan.outcome` |

`xihan.outcome` 取 `success`、`failure` 或 `unset`；`unset` 表示管道走完也没有写入结果，用来发现漏写结果的处理器。成功、失败与取消都会记指标。调用期间的未处理异常由 `ObserveMiddleware` 以 Error 级记一条日志（`Unhandled exception during invocation '{TraceId}'.`），并转成 `KERNEL_UNEXPECTED_001` 结果；取消不在这里吞掉，由 Resilience 阶段区分超时与外部取消。

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

5.0 不包装 Serilog 或其他日志库，4.x 的「日志」一章并入本章：应用照常注入 `ILogger<T>`，需要第三方日志库时按该库的文档接到 `Microsoft.Extensions.Logging`。装配 `Observability.OpenTelemetry` 且 `Logging` 开启时，日志经 OpenTelemetry 的日志提供者与追踪、指标一起导出。

应用自己的日志也建议用 `[LoggerMessage]`：模板在编译期解析，不装箱，Native AOT 下可用。

## 导出到 OpenTelemetry

`Observability.OpenTelemetry` 提供 `observability.export@1`（`role: default`），把内核与 .NET、ASP.NET Core 内置的源与仪表接到 OpenTelemetry，再按配置装配导出器。`Profiles.Full` 已包含它；其他组合显式装配：

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Observability.OpenTelemetry;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<OpenTelemetryObservabilityFeature>());

var app = builder.Build();
app.UseXiHan();
app.Run();
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

导出器的启用规则：

1. `Otlp:Enabled` 显式为 `true` / `false` 时按它。
2. 没写 `Otlp:Enabled` 时，配置了 `Otlp:Endpoint` 或设置了环境变量 `OTEL_EXPORTER_OTLP_ENDPOINT` 就启用 OTLP 导出器，否则不导出，也不会向本机的空端口反复重试。
3. 有 `Otlp:Endpoint` 时按它与 `Otlp:Protocol`（`Grpc` 或 `HttpProtobuf`）建一个跨三类信号的导出器；只靠环境变量时，端点与协议都按 OpenTelemetry 的标准环境变量与默认端口（gRPC 4317、HTTP 4318）决定，`Otlp:Protocol` 不参与。
4. `Console` 为 `true` 时三类信号同时写到控制台导出器，只用于开发。

容器与 Kubernetes 里通常只给环境变量：

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 dotnet run
```

### 订阅的源与仪表

| 类别 | 缺省订阅 | 追加 |
| --- | --- | --- |
| Activity 源 | `XiHan.Framework`、`Microsoft.AspNetCore`、`System.Net.Http` | `Sources`，逗号分隔 |
| Meter | `XiHan.Framework`、`Microsoft.AspNetCore.Hosting`、`Microsoft.AspNetCore.Server.Kestrel`、`System.Net.Http`、`System.Runtime` | `Meters`，逗号分隔 |

ASP.NET Core 与 `HttpClient` 不装检测包，按名字订阅它们原生发出的 Activity 与 Meter。资源属性 `service.name` 缺省取入口程序集名（取不到时为 `xihan`），`service.version` 取入口程序集版本，`service.instance.id` 取机器名。

### 采样

采样器是 `ParentBasedSampler(TraceIdRatioBasedSampler(SamplingRatio))`：没有父级的根 Span 按 `SamplingRatio`（0 到 1，缺省 1）的比例采样，有父级的沿用父级的决定。`xihan.invoke` 以调用上下文的 trace-id 与 span-id 作父级，父级的采样标志恒为已采样，所以 5.0 预览版里内核 Span 总被采样，`SamplingRatio` 只影响 ASP.NET Core、`HttpClient` 这类自身作为根的 Span。

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

`Observability.Host` 提供 `observability.host@1`（`role: default`，RFC-0023，ADR-0056）：单例 `IHostMonitor` 读进程与所在主机的 CPU、内存、磁盘、网卡、GPU、主板与运行时长，供管理界面的服务监控使用。契约在 `Observability.Abstractions`，只依赖原语层。

| 方法 | 返回 |
| --- | --- |
| `ReadRuntime()` | `RuntimeInfo`：操作系统、.NET 与 RID、机器名与用户名、进程 ID / 名称 / 启动时刻 / 运行时长、系统启动时刻 / 运行时长、工作集、私有字节、托管堆、线程数、句柄数、三代 GC 次数、进程 CPU 使用率 |
| `ReadProcessor()` | `ProcessorInfo`：型号、厂商、架构、物理与逻辑核心、标称频率（MHz）、最后一级缓存、系统 CPU 使用率 |
| `ReadMemory()` | `MemoryInfo`：总量、可用、空闲、缓存，以及已用与使用率 |
| `ReadDisks()` | `DiskInfo` 列表：挂载点或盘符、设备、卷标、文件系统、类型、总量、空闲、当前用户可用，以及已用与使用率 |
| `ReadNetworkInterfaces()` | `NetworkInterfaceInfo` 列表：ID、名称、描述、类型、状态、速率、物理地址、IPv4 / IPv6 地址、网关、DNS、累计收发字节与包数、收发速率 |
| `ReadGpus()` | `GpuInfo` 列表：名称、厂商、PCI 厂商与设备 ID、驱动与版本、显存、温度 |
| `ReadBoard()` | `BoardInfo`：主板、整机与 BIOS 的厂商、型号、版本、序列号与日期 |

读取的约定：

- 不启动外部命令：只读 BCL、`/proc` 与 `/sys`、`pci.ids`、注册表与 kernel32 的 `GetSystemTimes`、`GetLogicalProcessorInformation`、`K32GetPerformanceInfo`，不用 WMI、PowerShell、`dmidecode`、`lspci`、`nvidia-smi`。读取不会挂起，也没有命令注入面。
- 读不到的项为 `null`，列表读不到为空列表，不以 0 或空串顶替，界面据此区分「没有」与「为零」。单项失败只让该项为空，读取不抛出。
- CPU 型号与核心、GPU、主板首次读取后缓存；内存、磁盘、网卡与运行时每次当场读。
- 使用率与速率是两次采样之差，由后台采样器负责：启动时同步采一次，之后每 `SampleIntervalSeconds`（缺省 5，取值 (0, 3600]）采一次；读取只返回最近算出的值，不睡眠、不等待，启动后第一个间隔内为 `null`。一次采样失败记一条警告，保留上一次的值。
- 不暴露命令行参数、环境变量与当前目录。

Linux 与 Windows 读全部项；macOS 等其他平台只有 BCL 读得到的部分，CPU 型号与使用率、内存、GPU、主板为 `null`。容器里的内存与 CPU 使用率是宿主机的数值，不是 cgroup 限额。与 4.x 的差异（NVIDIA 显存与温度、GPU 使用率、Windows 主板序列号等）列在 RFC-0023。

把运行信息暴露给服务监控页：

```csharp
using MyApp.Diagnostics;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Observability.Abstractions;
using XiHan.Framework.Observability.Host;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, MonitorJsonContext.Default));
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<HostObservabilityFeature>());

var app = builder.Build();
app.UseXiHan();

// 读取只返回后台采样的最近值，不阻塞请求线程；机器信息只给认证过的调用方
var monitor = app.MapGroup("/monitor").RequireXiHanAuthentication();
monitor.MapGet("/runtime", (IHostMonitor host) => host.ReadRuntime());
monitor.MapGet("/cpu", (IHostMonitor host) => host.ReadProcessor());
monitor.MapGet("/memory", (IHostMonitor host) => host.ReadMemory());
monitor.MapGet("/disks", (IHostMonitor host) => host.ReadDisks());

app.Run();
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

`Profiles.Full` 已装配 `HostObservabilityFeature`，后台采样循环默认每 5 秒读一次 CPU 时间与网卡计数。

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Observability:OpenTelemetry:ServiceName` | string | 入口程序集名 | 资源属性 `service.name` |
| `XiHan:Observability:OpenTelemetry:ServiceVersion` | string | 入口程序集版本 | 资源属性 `service.version` |
| `XiHan:Observability:OpenTelemetry:Tracing` | bool | `true` | 是否接入追踪 |
| `XiHan:Observability:OpenTelemetry:Metrics` | bool | `true` | 是否接入指标 |
| `XiHan:Observability:OpenTelemetry:Logging` | bool | `true` | 是否把日志经 OpenTelemetry 导出 |
| `XiHan:Observability:OpenTelemetry:SamplingRatio` | 0 到 1 | 1 | 根 Span 的采样比例 |
| `XiHan:Observability:OpenTelemetry:Otlp:Enabled` | bool | 自动 | 强制开关 OTLP 导出器 |
| `XiHan:Observability:OpenTelemetry:Otlp:Endpoint` | 绝对 URI | — | OTLP 端点 |
| `XiHan:Observability:OpenTelemetry:Otlp:Protocol` | `Grpc` / `HttpProtobuf` | `Grpc` | 配置了端点时的 OTLP 协议 |
| `XiHan:Observability:OpenTelemetry:Console` | bool | `false` | 同时写控制台导出器 |
| `XiHan:Observability:OpenTelemetry:Sources` | 逗号分隔 | — | 追加订阅的 Activity 源 |
| `XiHan:Observability:OpenTelemetry:Meters` | 逗号分隔 | — | 追加订阅的 Meter |
| `XiHan:Observability:Host:SampleIntervalSeconds` | 数值，(0, 3600] | 5 | 运行信息的采样间隔（秒） |

取值不合法时（比例越界、端点不是绝对 URI、布尔不是 `true` / `false`、采样间隔越界）装配阶段抛出 `ArgumentException`，应用启动失败。

## 与其他能力的关系

- 健康与探针：特性的健康条目、`/health/startup`、`/health/ready`、`/health/live` 三个探针在[生命周期与健康](./lifecycle)一章，本章不重复。
- 审计：日志、追踪与指标面向运维；谁在什么时候做了什么，是业务记录，见[审计](./auditing)。两者共享同一个 trace-id，可以从审计事件跳到链路。
- 执行管道：Observe 是管道的第二个阶段，见[执行管道](./pipeline)。

## 相关

- [Observability.OpenTelemetry 包](../packages/observability-opentelemetry)
- [Observability.Abstractions 包](../packages/observability-abstractions)
- [Observability.Host 包](../packages/observability-host)
- [Kernel 内核](../packages/kernel)
- 规范：RFC-0023《可观测性：导出与运行信息》
