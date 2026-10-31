# XiHan.Framework.Observability.OpenTelemetry

可观测性导出的默认实现：把内核与 .NET、ASP.NET Core 内置的 Activity、Meter 与日志接到 OpenTelemetry，按配置启用 OTLP 与控制台导出器、采样与资源属性。提供 `observability.export@1`。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Observability.OpenTelemetry` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `observability.export@1`（`role: default`）；依赖 `kernel.hosting@1` |
| 特性入口 | `OpenTelemetryObservabilityFeature`（清单 `xihan.observability.opentelemetry`） |
| 配置节 | `XiHan:Observability:OpenTelemetry` |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Observability.Abstractions`；`OpenTelemetry.Extensions.Hosting`、`OpenTelemetry.Exporter.OpenTelemetryProtocol`、`OpenTelemetry.Exporter.Console`，均为 1.20.1 |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

内核只依赖 `System.Diagnostics`：`FrameworkDiagnostics.ActivitySource` 发出 `xihan.invoke` Span，`FrameworkDiagnostics.Meter` 发出 `xihan.invocations` 计数与 `xihan.invocation.duration` 直方图，名字都是 `XiHan.Framework`。本包在被选中时调用 `AddOpenTelemetry()`：

- 资源属性：`service.name`、`service.version` 取配置或入口程序集，`service.instance.id` 取机器名。
- 追踪：订阅缺省源与 `Sources`，采样器 `ParentBasedSampler(TraceIdRatioBasedSampler(SamplingRatio))`。
- 指标：订阅缺省 Meter 与 `Meters`。
- 日志：把 `Microsoft.Extensions.Logging` 的日志接到 OpenTelemetry 的日志提供者。
- 导出：配置了端点或标准环境变量时启用跨三类信号的 OTLP 导出器；`Console` 为真时三类信号再各接一个控制台导出器。

ASP.NET Core 与 `HttpClient` 不装检测包，按名字订阅它们原生发出的源与仪表。没有配置端点就不导出，也不会向本机的空端口反复重试。

## 何时使用

要把链路、指标与日志送进 OpenTelemetry Collector、Jaeger、Prometheus（经 Collector）等后端时。`Profiles.Full` 已包含本包；不装配时内核照样发出 Activity 与 Meter，只是没有导出。只要进程与主机的运行信息时用 [Observability.Host](./observability-host)。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Observability.OpenTelemetry" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。

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

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `ServiceName` | string | 入口程序集名，取不到时 `xihan` | 资源属性 `service.name` |
| `ServiceVersion` | string | 入口程序集版本 | 资源属性 `service.version` |
| `Tracing` | bool | `true` | 是否注册追踪提供者 |
| `Metrics` | bool | `true` | 是否注册指标提供者 |
| `Logging` | bool | `true` | 是否把日志经 OpenTelemetry 导出 |
| `SamplingRatio` | 0 到 1 | 1 | 根 Span 的采样比例 |
| `Otlp:Enabled` | bool | 自动 | 不写时：有 `Otlp:Endpoint` 或环境变量 `OTEL_EXPORTER_OTLP_ENDPOINT` 即启用 |
| `Otlp:Endpoint` | 绝对 URI | — | OTLP 端点 |
| `Otlp:Protocol` | `Grpc` / `HttpProtobuf` | `Grpc` | 只在配置了 `Otlp:Endpoint` 时生效 |
| `Console` | bool | `false` | 三类信号同时写控制台导出器 |
| `Sources` | 逗号分隔 | — | 追加订阅的 Activity 源 |
| `Meters` | 逗号分隔 | — | 追加订阅的 Meter |

键都在 `XiHan:Observability:OpenTelemetry` 下：

```json
{
  "XiHan": {
    "Observability": {
      "OpenTelemetry": {
        "ServiceName": "orders",
        "SamplingRatio": 0.2,
        "Otlp": { "Endpoint": "http://localhost:4318", "Protocol": "HttpProtobuf" },
        "Console": false,
        "Sources": "MyApp.Orders",
        "Meters": "MyApp.Orders"
      }
    }
  }
}
```

比例越界、端点不是绝对 URI、协议名或布尔值不合法时，`OpenTelemetryOptions.FromConfiguration` 抛出 `ArgumentException`，应用在装配阶段失败。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `OpenTelemetryObservabilityFeature` | 能力包入口 |
| `OpenTelemetryOptions` | 上表各项；`ExportsOtlp` 是按规则算出的「是否启用 OTLP」；`ConfigurationSection` 为 `XiHan:Observability:OpenTelemetry` |
| `OtlpProtocol` | `Grpc`（默认端口 4317）、`HttpProtobuf`（默认端口 4318） |
| `OpenTelemetryDefaults` | 缺省订阅的 `Sources` 与 `Meters`，以及 `OtlpEndpointVariable`（`OTEL_EXPORTER_OTLP_ENDPOINT`） |

缺省订阅：

| 类别 | 名字 |
| --- | --- |
| Activity 源 | `XiHan.Framework`、`Microsoft.AspNetCore`、`System.Net.Http` |
| Meter | `XiHan.Framework`、`Microsoft.AspNetCore.Hosting`、`Microsoft.AspNetCore.Server.Kestrel`、`System.Net.Http`、`System.Runtime` |

## 使用示例

本包注册的是标准的 OpenTelemetry 提供者，应用可以在其上追加处理器或导出器，例如在测试里收集内核 Span：

```csharp
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using XiHan.Framework.Observability.OpenTelemetry;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

public sealed class TracingTests
{
    [Fact]
    public async Task Invocation_ProducesKernelSpan()
    {
        var activities = new List<Activity>();
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<OpenTelemetryObservabilityFeature>(),
            services: services => services.ConfigureOpenTelemetryTracerProvider((_, tracing) => tracing.AddInMemoryExporter(activities)));

        // 提供者是惰性单例，先解析让监听器挂上
        var tracer = host.Services.GetRequiredService<TracerProvider>();
        await host.InvokeAsync(static context =>
        {
            context.SetOutcome(Result.Success());
            return ValueTask.CompletedTask;
        });

        tracer.ForceFlush();
        var span = Assert.Single(activities, static a => a.OperationName == "xihan.invoke");
        Assert.Equal("success", span.GetTagItem("xihan.outcome"));
    }
}
```

`AddInMemoryExporter` 来自 `OpenTelemetry.Exporter.InMemory` 包，只在测试项目里引用。

## 行为与限制

- 能力没被选中时（另有显式 Provider 提供 `observability.export@1`）只登记选项，不注册任何提供者。
- `Tracing`、`Metrics`、`Logging` 为假时对应的提供者不注册，`TracerProvider` 等服务不存在。
- `xihan.invoke` 以调用上下文的 trace-id 与 span-id 作父级，父级的采样标志恒为已采样，5.0 预览版里内核 Span 总被采样，`SamplingRatio` 只影响自身作为根的 Span。
- 只靠环境变量启用 OTLP 时，端点与协议都按 OpenTelemetry 的标准环境变量与默认端口决定，`Otlp:Protocol` 不参与。
- 本包没有健康条目；导出失败由 OpenTelemetry SDK 自身处理，不影响调用。

## 相关

- [可观测性](../guide/observability#导出到-opentelemetry)
- [Observability.Abstractions](./observability-abstractions)
- [Observability.Host](./observability-host)
- [Kernel 内核](./kernel)
- 规范：RFC-0023《可观测性：导出与运行信息》
