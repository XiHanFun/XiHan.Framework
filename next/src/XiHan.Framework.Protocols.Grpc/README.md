# XiHan.Framework.Protocols.Grpc

gRPC 协议适配器（Ring 5）。提供 `protocols.grpc@1`，把登记表里标记为 `Expose.Grpc` 的操作发布为 gRPC 一元方法（RFC-0011、`spec/protocol/grpc.md`）。protobuf 线格式由操作的 JSON Schema（`Kernel.Generators` 产出）推出，服务端在 JSON 与 protobuf 之间转换，不需要 `.proto` 编译，也不依赖 `Google.Protobuf`；`.proto` 文件由同一份描述生成并经端点发布给客户端。

```csharp
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<GrpcProtocolFeature>().UseFeature<OrdersFeature>());
var app = builder.Build();
app.UseXiHan();
app.MapXiHanGrpc();    // /Orders/Create、/Orders/List …（有 Package 时 /{package}.Orders/Create）
app.MapXiHanProto();   // GET /operations.proto
```

```json
{ "Kestrel": { "Endpoints": { "Http": { "Url": "http://localhost:5000" }, "Grpc": { "Url": "http://localhost:5001", "Protocols": "Http2" } } } }
```

## 规则

- 方法名：服务 = 资源段的 PascalCase（`order-lines` → `OrderLines`），方法 = 动词的 PascalCase；显式覆盖 `XiHan:Protocols:Grpc:Methods:<操作 ID>` = `Service/Method`；包名 `XiHan:Protocols:Grpc:Package`（写进 `.proto` 的 `package`，也是路径前缀）。名字不合法或重复即启动失败。
- 发布范围：`Expose` 含 `Grpc` 且非 `Stability.Internal` 的操作；它们必须有 `OperationSchema`（用生成器登记，或手动传入），否则启动失败。
- 消息映射（`ProtoSchema`）：字段号按 Schema 属性顺序从 1 起；`string` → string（base64 → bytes）、`boolean` → bool、`integer` 按 `format` int32 / int64（无格式 → 十进制文本）、`number` 按 `format` float / double（decimal、无格式 → 十进制文本）、数组 → repeated、`additionalProperties` → map<string, T>、`$ref` → 嵌套消息、可空 → optional、无约束 → JSON 文本。嵌套数组与映射里的映射不可表示，启动失败。
- 调用：请求按 Schema 解码为 JSON 载荷，以请求头构造的上下文（`traceparent`、`baggage`、`authorization`、`accept-language`、`xihan-deadline` 或 `grpc-timeout`）经完整内核管道执行，响应按 Schema 编码。端点绕过 `UseXiHan()` 的回写，响应完全由 gRPC 处理器产出。
- 失败：VALIDATION → `InvalidArgument`，NOTFOUND → `NotFound`，CONFLICT → `Aborted`，DENIED → `PermissionDenied`（`SECURITY_DENIED_001` → `Unauthenticated`），UNAVAILABLE → `Unavailable`，TIMEOUT → `DeadlineExceeded`，CANCELLED → `Cancelled`，其他 → `Internal`；`grpc-message` 为按 `accept-language` 翻译的文案；尾部元数据 `xihan-error-code`、`xihan-trace-id`（成功时也有 trace-id）。请求不是合法 protobuf → `InvalidArgument` + `PROTOCOLS_VALIDATION_005`；应用未就绪 → `Unavailable`。

## 依赖方向

依赖 `XiHan.Framework.Kernel`、`XiHan.Framework.Hosting.AspNetCore`、`XiHan.Framework.Localization.Abstractions`、`Microsoft.AspNetCore.App` 与 `Grpc.AspNetCore.Server`（方法由 `IServiceMethodProvider` 动态绑定）。协议适配器在宿主之上（Ring 5）。

## 状态

P3：可用（一元方法、JSON Schema 驱动的 protobuf 编解码、`.proto` 发布）。流式方法与 gRPC 反射服务未提供。
