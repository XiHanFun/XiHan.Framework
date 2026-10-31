# XiHan.Framework.Protocols.Grpc

gRPC 协议适配器，提供 `protocols.grpc@1`：把 `Expose.Grpc` 的操作契约发布成 gRPC 一元方法（服务 `{Resource}`、方法 `{Verb}`），以操作的 JSON Schema 作为 protobuf 描述在两种线格式之间转换，并发布由同一份描述生成的 `.proto`。适用于服务之间要用 gRPC 调用操作的场景。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Protocols.Grpc` |
| 环层 | Ring 5（协议与插件） |
| 能力 | 提供 `protocols.grpc@1`；依赖 `kernel.hosting@1` |
| 特性入口 | `GrpcProtocolFeature` |
| 配置节 | `XiHan:Protocols:Grpc` |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Hosting.AspNetCore`、`XiHan.Framework.Localization.Abstractions`；`Grpc.AspNetCore.Server` 2.84.0；共享框架 `Microsoft.AspNetCore.App` |
| AOT | 兼容（`IsAotCompatible`） |
| 稳定性 | Preview |

## 概述

`GrpcProtocolFeature` 调用 `AddGrpc()`，登记选项与一个 `IServiceMethodProvider`：方法在运行时按操作表动态绑定，消息以字节数组传递，不需要编译 `.proto`，也不依赖 `Google.Protobuf`。操作表在第一次解析时建成，这时全部特性已装配、登记表已定型。

每个方法调用都是一次独立调用：处理器以请求的 HTTP/2 头建调用上下文（`traceparent`、`baggage`、`authorization`、`accept-language`、`xihan-deadline` 或 `grpc-timeout`），把请求按 Schema 从 protobuf 解码为 JSON 载荷，经完整内核管道执行，再把响应编码回 protobuf。gRPC 方法带 `PipelineBypassMetadata`，不经 `UseXiHan()` 的外层调用。失败映射为 gRPC 状态，错误码与 trace-id 放在尾部元数据。

## 何时使用

调用方是 gRPC 客户端（其他语言的服务、需要 HTTP/2 与二进制线格式的场景）时用它。浏览器与通用 HTTP 客户端用 [Protocols.Http](./protocols-http)。只支持一元方法，流式方法与 gRPC 反射服务未提供。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Protocols.Grpc" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。`Profiles.Full` 已包含本包。

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Protocols.Grpc;
using XiHan.Framework.Protocols.Grpc.Services;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<GrpcProtocolFeature>());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanGrpc();    // /{package}.{Service}/{Method}
app.MapXiHanProto();   // GET /operations.proto
app.Run();
```

不用 TLS 时给 gRPC 单开一个 HTTP/2 端点（h2c）：

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": { "Url": "http://localhost:5000" },
      "Grpc": { "Url": "http://localhost:5001", "Protocols": "Http2" }
    }
  }
}
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Protocols:Grpc:Package` | string | 空 | protobuf 包名（小写标识符以点分隔），写进 `.proto` 的 `package`，也是方法路径前缀 |
| `XiHan:Protocols:Grpc:Methods:<操作 ID>` | string | — | `Service/Method`，覆盖推导的名字 |

```json
{
  "XiHan": {
    "Protocols": {
      "Grpc": {
        "Package": "acme.orders",
        "Methods": { "orders.list": "Orders/ListAll" }
      }
    }
  }
}
```

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `GrpcProtocolFeature` | 特性入口 |
| `GrpcProtocolOptions` | `Package`、`Methods`；`MethodFor(contract)`；`FromConfiguration` |
| `Services.OperationGrpcEndpointRouteBuilderExtensions` | `MapXiHanGrpc()`、`MapXiHanProto(pattern)`（缺省 `/operations.proto`，`text/plain`） |
| `Methods.GrpcMethodConvention` | `Derive(contract)`：资源段与动词的 PascalCase |
| `Methods.GrpcMethodName` | 服务名与方法名；`PathFor(package)`、`FullServiceName(package)`、`Parse("Orders/List")` |
| `Methods.GrpcOperationTable` | 暴露给 gRPC 的操作、各自的请求与响应消息、生成的 `.proto` 文本 |
| `Proto.ProtoSchema` | `FromJsonSchema(schema)`：由 JSON Schema 建 protobuf 消息描述 |
| `Proto.ProtoDecoder`、`Proto.ProtoEncoder` | protobuf 与 JSON 之间的转换 |
| `Proto.ProtoFileBuilder` | 由消息描述生成 `.proto` |
| `Proto.ProtoMessage`、`ProtoField`、`ProtoValueType`、`ProtoKind`、`ProtoCardinality` | 消息描述模型 |
| `GrpcErrors` | 本包的错误码 |

## 使用示例

客户端用发布出的 `.proto` 调用（`samples/SimpleApp`）：

```bash
curl -s localhost:5000/operations.proto -o operations.proto
grpcurl -plaintext -proto operations.proto -H "authorization: ApiKey dev-alice-key" \
  -d '{"name":"widget","amount":3}' localhost:5001 Orders/Create
```

JSON Schema 到 protobuf 的映射表与生成的 `.proto` 见 [gRPC、MCP 与 CloudEvents](../guide/protocols#grpc)。

## 行为与限制

- 暴露给 gRPC 的操作必须有 Schema；名字、Schema 与消息形状的问题都在映射时失败，应用启动不起来。
- 线上缺席的布尔、数值、`repeated`、`map` 字段按 proto3 默认值补进 JSON；字符串、字节、嵌套消息缺席即缺席。
- 字符串枚举映射为 `string`，不建 proto `enum`；`integer` 无格式与 `decimal` 映射为十进制文本。
- 应用未就绪时直接以 `Unavailable` 拒绝，此时还没有调用上下文。

| KIND | gRPC 状态 |
| --- | --- |
| `VALIDATION` | `InvalidArgument` |
| `NOTFOUND` | `NotFound` |
| `CONFLICT` | `Aborted` |
| `DENIED` | `PermissionDenied`；`SECURITY_DENIED_001`、`SECURITY_DENIED_003` 为 `Unauthenticated` |
| `UNAVAILABLE` | `Unavailable` |
| `TIMEOUT` | `DeadlineExceeded` |
| `CANCELLED` | `Cancelled` |
| 其他 | `Internal` |

| 错误码 | 情形 |
| --- | --- |
| `PROTOCOLS_VALIDATION_005` | 请求不是合法的 protobuf 或与 Schema 不符（`InvalidArgument`） |
| `PROTOCOLS_VALIDATION_006` | 暴露给 gRPC 的操作没有 Schema，或 Schema 无法映射为 protobuf（启动失败） |
| `PROTOCOLS_VALIDATION_007` | 推导或配置的服务 / 方法名不合法（启动失败） |
| `PROTOCOLS_CONFLICT_002` | 两个操作得到同一个服务 / 方法名（启动失败） |
| `PROTOCOLS_CONFLICT_003` | 同名 protobuf 消息在不同操作里形状不同（启动失败） |
| `PROTOCOLS_UNEXPECTED_001` | 响应无法按 Schema 编码为 protobuf（`Internal`） |

尾部元数据 `xihan-error-code` 是错误码，`xihan-trace-id` 是 trace-id（成功时也有）；`grpc-message` 按 `accept-language` 翻译。

## 相关

- [gRPC、MCP 与 CloudEvents](../guide/protocols)、[操作契约](../guide/operations)
- [Hosting.AspNetCore](./hosting-aspnetcore)、[Protocols.Http](./protocols-http)、[Kernel.Generators](./kernel-generators)
- 规范：`spec/protocol/grpc.md`《gRPC 载体》、RFC-0011《操作契约与 XGI》
