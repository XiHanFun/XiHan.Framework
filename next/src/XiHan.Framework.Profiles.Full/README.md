# XiHan.Framework.Profiles.Full

Full 组合包（Ring 6）：Standard + `ObjectStorage` / `Scheduling` / `Auditing` 的抽象与默认实现 + `Configuration` 抽象 + `Observability.OpenTelemetry` + `Protocols.Http` / `.Grpc` / `.Mcp` / `.CloudEvents`。中后台与 SaaS 服务从这里开始：一份操作契约同时在 HTTP、gRPC、MCP 上服务，零外部基础设施即可运行。

```csharp
builder.AddXiHan(xihan => xihan
    .UseFullProfile()
    .UseFeature<OrdersFeature>());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanHealth();
app.MapXiHanOperations();   // HTTP
app.MapXiHanOpenApi();      // /openapi.json
app.MapXiHanGrpc();         // gRPC（需要 Kestrel 的 HTTP/2 端点）
app.MapXiHanProto();        // /operations.proto
app.MapXiHanMcp();          // /mcp
app.MapXiHanCloudEvents();  // /events
```

`UseFullProfile()` 在 Standard 之上装配：`FileSystemObjectStorageFeature`、`InMemorySchedulingFeature`、`InMemoryAuditingFeature`、`OpenTelemetryObservabilityFeature`（没有配置端点就不导出）、`HttpProtocolFeature`、`GrpcProtocolFeature`、`McpProtocolFeature`、`CloudEventsProtocolFeature`。默认实现都是 `role: default`：再 `UseFeature` 一个显式 Provider（如 `DatabaseLocalizationFeature`、S3 对象存储、Kubernetes Lease 领导选举）即顶替对应能力。远程配置与密钥（`Configuration.Abstractions`）没有默认 Provider：.NET 原生的文件与环境变量就是默认，按需在 `IConfigurationBuilder` 上 `AddXiHanRemoteConfiguration` / `AddXiHanSecrets`。

管道在 Standard 之上多了 Observe 末尾的 `AuditMiddleware`（默认只记声明了权限的操作）。

## 依赖方向

组合包可引用任何环；被应用引用。

## 状态

P3：可用。
