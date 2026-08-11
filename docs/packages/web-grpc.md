# XiHan.Framework.Web.Grpc

> gRPC 服务端轻量接入：把 ASP.NET Core gRPC 纳入框架的模块化生命周期；契约与服务映射由应用侧完成。

- **NuGet**：`XiHan.Framework.Web.Grpc`
- **模块类**：`XiHanWebGrpcModule`
- **所在层**：Web 层
- **关键依赖**：`Grpc.AspNetCore`（gRPC 服务端运行时）

## 概述

XiHan.Framework.Web.Grpc 让框架应用具备 gRPC 服务端能力。模块在 `ConfigureServices` 中调用 `AddXiHanWebGrpc()`——内部即标准的 `AddGrpc()`——把 gRPC 服务注册进 DI 容器。它是一层极薄的接入：把 `Grpc.AspNetCore` 纳入框架的 `[DependsOn]` 模块体系，随框架生命周期统一初始化。gRPC 服务实现、`.proto` 契约以及端点映射（`MapGrpcService<T>()`）都由你的应用侧定义与完成——模块的 `OnApplicationInitialization` 未做任何额外的映射或中间件挂载。

## 何时使用

- 需要在框架应用中对外提供 gRPC 服务（服务间高性能 RPC 通信）。
- 希望 gRPC 的注册随框架模块化生命周期统一管理，而不是散落在启动代码里。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Web.Grpc
```

```csharp
[DependsOn(typeof(XiHanWebGrpcModule))]
public class MyModule : XiHanModule { }
```

模块 `ConfigureServices` 调用 `AddXiHanWebGrpc()`（等价于 `AddGrpc()`）注册 gRPC 服务端。`OnApplicationInitialization` 取到 `IApplicationBuilder` 但**未做额外映射**——具体的服务端点映射由应用侧负责。

## 核心能力

- **gRPC 服务注册**：`AddXiHanWebGrpc()` 封装 `AddGrpc()`，将 gRPC 服务端能力接入框架 DI。
- **模块化接入**：以 `XiHanWebGrpcModule` 参与 `[DependsOn]` 组合，与其它 Web 模块共存于同一应用。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `XiHanWebGrpcModule` | 模块类，`ConfigureServices` 中 `AddXiHanWebGrpc()`；`OnApplicationInitialization` 无额外映射 |
| `AddXiHanWebGrpc()` | 服务集合扩展，等价于 `AddGrpc()` 的接入封装 |

## 使用示例

启用模块后，在应用侧实现 gRPC 服务并映射端点（映射不在本模块内）：

```csharp
// 应用侧的端点映射（例如在 WebHost 的端点配置中）
app.MapGrpcService<MyGreeterService>();
```

`MyGreeterService` 继承自由 `.proto` 生成的服务基类，契约与代码生成同标准 ASP.NET Core gRPC 一致。

## 注意事项与最佳实践

- **端点映射由应用侧完成**：本模块只注册服务，不调用 `MapGrpcService<T>()`；忘记映射端点会导致 gRPC 服务不可达。
- **托管环境要求**：gRPC over HTTP/2 需要相应的 Kestrel/协议配置，遵循 `Grpc.AspNetCore` 的标准托管要求。

## 依赖模块

- 内部依赖：[XiHan.Framework.Web.Core](./web-core)、[XiHan.Framework.Serialization](./serialization)。
- 第三方核心：`Grpc.AspNetCore`（gRPC 服务端运行时）。

## 相关模块

- [XiHan.Framework.Web.Api](./web-api) — 若同时对外提供 REST 与 gRPC，可与本包并存。
