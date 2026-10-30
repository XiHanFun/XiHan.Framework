# XiHan.Framework.Observability.OpenTelemetry

可观测性导出（Ring 3，`role: default`）。提供 `observability.export@1`。内核只依赖 `System.Diagnostics`（`FrameworkDiagnostics.ActivitySource` / `Meter`：`xihan.invoke` Span、`xihan.invocations` 与 `xihan.invocation.duration`）与 `Microsoft.Extensions.Logging`；本包把它们和 .NET、ASP.NET Core 内置的源与仪表接到 OpenTelemetry，并按配置装配导出器。

```csharp
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<OpenTelemetryObservabilityFeature>());
```

```json
{
  "XiHan": { "Observability": { "OpenTelemetry": {
    "ServiceName": "orders", "SamplingRatio": 0.2,
    "Otlp": { "Endpoint": "http://otel-collector:4317", "Protocol": "Grpc" },
    "Console": false, "Sources": "MyCompany.Orders", "Meters": "MyCompany.Orders"
  } } }
}
```

## 规则

- 追踪、指标、日志三类信号各有开关（默认全开）；默认订阅 `XiHan.Framework`、`Microsoft.AspNetCore`、`System.Net.Http` 源与 `XiHan.Framework`、`Microsoft.AspNetCore.Hosting`、`Microsoft.AspNetCore.Server.Kestrel`、`System.Net.Http`、`System.Runtime` 仪表，`Sources` / `Meters` 追加。
- OTLP 导出器在配置了 `Otlp:Endpoint` 或环境变量 `OTEL_EXPORTER_OTLP_ENDPOINT` 时自动启用，`Otlp:Enabled` 可强制开关；没有端点时按标准环境变量与默认端口（gRPC 4317 / HTTP 4318）决定。没有配置就不导出，不会向本机空端口重试。
- `SamplingRatio` 只作用于根 Span，有上游 trace 的沿用上游决定；资源属性 `service.name` / `service.version` 默认取入口程序集，`service.instance.id` 取机器名。
- 没有 ASP.NET Core 或 HttpClient 检测包：.NET 8 起它们原生发出 Activity 与 Meter，按名字订阅即可。

## 依赖方向

依赖 `XiHan.Framework.Kernel`、`XiHan.Framework.Observability.Abstractions`（能力 ID）、`OpenTelemetry.Extensions.Hosting`、`OpenTelemetry.Exporter.OpenTelemetryProtocol` 与 `OpenTelemetry.Exporter.Console`。

## 状态

P3：可用。
