# XiHan.Framework.Profiles.Full

Full 组合包（元包）：Standard 加对象存储、调度、审计、工作流的默认实现，AI 工具目录，OpenTelemetry 导出与运行信息，以及 HTTP、gRPC、MCP、CloudEvents 四种协议适配器，`UseFullProfile()` 装配它们。适用于一份操作契约同时经多种协议服务的中后台与 SaaS 服务，零外部基础设施即可运行。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Profiles.Full` |
| 环层 | Ring 6（组合包） |
| 能力 | 自身不声明；装配的特性提供的能力见下文 |
| 特性入口 | `UseFullProfile()` |
| 配置节 | — |
| 依赖 | `XiHan.Framework.Profiles.Standard`；`ObjectStorage`、`Scheduling`、`Auditing`、`Workflow` 的 `.Abstractions` 与默认实现；`Configuration.Abstractions`、`AI.Abstractions`、`Observability.OpenTelemetry`、`Observability.Host`；`Protocols.Http` / `.Grpc` / `.Mcp` / `.CloudEvents` |
| AOT | 兼容 |
| 稳定性 | Stable |

## 概述

`UseFullProfile()` 先调用 `UseStandardProfile()`（Standard 装配的内容见 [Profiles.Standard](./profiles-standard)），再装配：

| 特性 | 包 | 提供的能力 |
| --- | --- | --- |
| `FileSystemObjectStorageFeature` | `ObjectStorage.FileSystem` | `objectstorage.blob@1`、`objectstorage.multipart@1`（`role: default`） |
| `InMemorySchedulingFeature` | `Scheduling.InMemory` | `scheduling.jobs@1`、`scheduling.leader@1`（`role: default`） |
| `InMemoryAuditingFeature` | `Auditing.InMemory` | `auditing.trail@1`（`role: default`） |
| `InMemoryWorkflowFeature` | `Workflow.InMemory` | `workflow.execution@1`（`role: default`） |
| `AIToolsFeature` | `AI.Abstractions` | `ai.tools@1`（`role: default`） |
| `OpenTelemetryObservabilityFeature` | `Observability.OpenTelemetry` | `observability.export@1`（`role: default`，没有配置导出端点就不导出） |
| `HostObservabilityFeature` | `Observability.Host` | `observability.host@1`（`role: default`） |
| `HttpProtocolFeature` | `Protocols.Http` | `protocols.http@1` |
| `GrpcProtocolFeature` | `Protocols.Grpc` | `protocols.grpc@1` |
| `McpProtocolFeature` | `Protocols.Mcp` | `protocols.mcp@1` |
| `CloudEventsProtocolFeature` | `Protocols.CloudEvents` | `protocols.cloudevents@1` |

默认实现都可被显式 Provider 顶替：再 `UseFeature` 一个声明同一能力的特性（如 `DatabaseLocalizationFeature`、S3 对象存储、`KubernetesSchedulingFeature`），能力全部被顶替的默认实现不装配、不启动；还有能力被选中的照常装配，例如加入 `KubernetesSchedulingFeature` 后 `Scheduling.InMemory` 只让出 `scheduling.leader`，仍提供 `scheduling.jobs`。`Configuration.Abstractions` 只随包带进来，没有默认 Provider：.NET 原生的文件与环境变量就是缺省，按需在 `IConfigurationBuilder` 上接远程配置与密钥（[配置与选项](../guide/configuration)）。管道在 Standard 之上多了 Observe 阶段末尾的审计中间件（缺省只记声明了权限的操作，[审计](../guide/auditing)）。

协议适配器只登记能力与服务，端点仍要在宿主上映射。

## 何时使用

- 一份操作契约要同时经 HTTP、gRPC 与 MCP 服务、接收 CloudEvents 事件，并需要对象存储、调度、审计或工作流：用 Full。
- 只需要其中几种协议或子系统：从 [Profiles.Standard](./profiles-standard) 开始逐个 `UseFeature`，引用更少的包。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Profiles.Full" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Full;
using XiHan.Framework.Protocols.CloudEvents.Inbound;
using XiHan.Framework.Protocols.Grpc.Services;
using XiHan.Framework.Protocols.Http.OpenApi;
using XiHan.Framework.Protocols.Http.Routing;
using XiHan.Framework.Protocols.Mcp.Transports;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseFullProfile());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanHealth();
app.MapXiHanOperations();   // HTTP
app.MapXiHanOpenApi();      // /openapi.json
app.MapXiHanGrpc();         // gRPC，需要 Kestrel 的 HTTP/2 端点
app.MapXiHanProto();        // /operations.proto
app.MapXiHanMcp();          // /mcp
app.MapXiHanCloudEvents();  // /events
app.Run();
```

`samples/KubernetesApp` 用 Full 组合演示探针、停机宽限、ConfigMap 热更新与 Lease 领导选举，见[部署与打包](../guide/deployment)。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `FullProfile` | 静态类，扩展方法 `UseFullProfile(this AppBuilder)` |

## 行为与限制

- gRPC 端点需要 HTTP/2：Kestrel 要有一个 `Protocols: Http2` 的端点（h2c），`xihan pack` 在清单写了 `ports.grpc` 时生成它。
- `FileSystemObjectStorageFeature` 写本地目录；容器里根文件系统只读时把它的根目录配到可写卷（KubernetesApp 配成 `/tmp` 下）。
- 内存默认实现进程重启即丢数据，多副本之间不共享；上线前按需换 Provider。

## 相关

- [宿主与 Web 应用](../guide/web)、[HTTP 与 OpenAPI](../guide/http)、[gRPC、MCP 与 CloudEvents](../guide/protocols)、[部署与打包](../guide/deployment)
- [Profiles.Standard](./profiles-standard)、[Profiles.Minimal](./profiles-minimal)
