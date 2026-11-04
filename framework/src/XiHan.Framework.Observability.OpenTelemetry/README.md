# XiHan.Framework.Observability.OpenTelemetry

## 关于

`XiHan.Framework.Observability.OpenTelemetry` 把框架与 .NET、ASP.NET Core 内置的追踪、指标和日志接到 OpenTelemetry，并按配置启用 OTLP 或控制台导出器、采样与资源属性。要把数据送进 Collector、Jaeger 等后端时使用。

## 主要特性

* 追踪、指标、日志三类信号各有开关，缺省全开
* 配置了 OTLP 端点或标准环境变量才导出，没有配置就不导出
* 支持按比例采样与追加订阅自己的 Activity 源和 Meter

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Observability.OpenTelemetry --prerelease
```

### 使用

注册特性，再在配置里写 OTLP 端点。

```csharp
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<OpenTelemetryObservabilityFeature>());
```

```json
{
  "XiHan": { "Observability": { "OpenTelemetry": {
    "ServiceName": "orders",
    "SamplingRatio": 0.2,
    "Otlp": { "Endpoint": "http://localhost:4317", "Protocol": "Grpc" }
  } } }
}
```

## 主要类型

此库提供的主要类型：

* `OpenTelemetryObservabilityFeature`：能力包入口
* `OpenTelemetryOptions`：服务名、采样比例、OTLP、控制台导出、追加订阅的源与仪表
* `OtlpProtocol`：`Grpc` 或 `HttpProtobuf`

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/observability-opentelemetry)

## 相关包

* 运行信息：[XiHan.Framework.Observability.Host](https://www.nuget.org/packages/XiHan.Framework.Observability.Host)
* 可观测性抽象：[XiHan.Framework.Observability.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Observability.Abstractions)

## 反馈与贡献

`XiHan.Framework.Observability.OpenTelemetry` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
