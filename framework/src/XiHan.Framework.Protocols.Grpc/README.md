# XiHan.Framework.Protocols.Grpc

## 关于

`XiHan.Framework.Protocols.Grpc` gRPC 协议适配器：把标记为 `Expose.Grpc` 的操作发布为 gRPC 一元方法。protobuf 线格式由操作的 JSON Schema 推出，不需要编译 `.proto`，`.proto` 文件由同一份描述生成并经端点发布给客户端。

## 主要特性

* 一份操作契约同时在 gRPC 上服务，经完整内核管道
* 错误按 KIND 映射为 gRPC 状态，错误码与 trace-id 放在尾部元数据
* `GET /operations.proto` 发布 `.proto` 文件

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Protocols.Grpc --prerelease
```

### 使用

注册特性并映射端点；gRPC 需要 Kestrel 的 HTTP/2 端点。

```csharp
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<GrpcProtocolFeature>());
var app = builder.Build();
app.UseXiHan();
app.MapXiHanGrpc();    // /Orders/Create、/Orders/List …
app.MapXiHanProto();   // GET /operations.proto
```

```json
{ "Kestrel": { "Endpoints": { "Grpc": { "Url": "http://localhost:5001", "Protocols": "Http2" } } } }
```

## 主要类型

此库提供的主要类型：

* `GrpcProtocolFeature`：特性入口
* `GrpcProtocolOptions`：`Package`、`Methods`（覆盖推导出的服务与方法名）
* `OperationGrpcEndpointRouteBuilderExtensions`：`MapXiHanGrpc`、`MapXiHanProto`

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/protocols-grpc)

## 相关包

* HTTP 协议适配器：[XiHan.Framework.Protocols.Http](https://www.nuget.org/packages/XiHan.Framework.Protocols.Http)
* MCP 协议适配器：[XiHan.Framework.Protocols.Mcp](https://www.nuget.org/packages/XiHan.Framework.Protocols.Mcp)
* CloudEvents 协议适配器：[XiHan.Framework.Protocols.CloudEvents](https://www.nuget.org/packages/XiHan.Framework.Protocols.CloudEvents)

## 反馈与贡献

`XiHan.Framework.Protocols.Grpc` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
