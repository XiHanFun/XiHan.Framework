来源：https://framework.docs.xihanfun.com/protocols/grpc

# gRPC

这一章讲 HTTP 之外的协议载体。这一页先用一小节说清「一份契约，多个载体」的总体思路，再专讲 gRPC 适配器：怎样启用、服务名与方法名怎么推出、protobuf 线格式怎样由 JSON Schema 推出、`/operations.proto` 怎样发给客户端、调用上下文与错误怎样映射到 gRPC，最后用 grpcurl 调一次。读完你能把已有的操作发布成 gRPC 一元方法，并让任何语言的客户端按 `.proto` 生成桩代码。前置知识是[操作契约](/web/operations)与 [HTTP 路由与载荷](/web/routing)。

## 为什么需要

服务间调用常常要求二进制、强类型、带截止时间的协议，gRPC 是这类场景的事实标准。传统做法是另写一份 `.proto`、编译出服务基类、再写一层把 gRPC 请求翻译成业务调用的代码：同一个业务从此有两份契约、两份校验、两套授权，改一处漏一处。

框架的做法是只保留一份契约。操作契约（`[Operation]` 标注的 `IOperation<TRequest, TResponse>`）已经有由生成器产出的请求 / 响应 JSON Schema，gRPC 适配器直接从这份 Schema 推出 protobuf 消息与方法，服务端不需要编译 `.proto`，也不依赖 `Google.Protobuf`。每次 gRPC 调用以自己的调用上下文经过完整的内核管道，认证、按契约授权、校验、事务与 HTTP 上完全相同。

## 一份契约，多个载体

gRPC 与 MCP 暴露的都是操作契约，它们与 HTTP 适配器读同一个操作登记表、同一份 JSON Schema：HTTP 拿它生成 OpenAPI，gRPC 拿它推出 protobuf 线格式与 `.proto`，MCP 把它原样作为 tool 的 `inputSchema` / `outputSchema`。操作写在哪些协议上由 `[Operation]` 的 `Expose` 决定；`Stability.Internal` 的操作不在任何协议上发布。CloudEvents 不暴露操作，它是消息信封的 HTTP 绑定：入站事件按类型映射到主题交给消息处理器，出站把信封 POST 到别的服务。

| 适配器 | 暴露条件 | 映射 | 端点 |
| --- | --- | --- | --- |
| `Protocols.Http` | `Expose.Http` | 路由由操作 ID 推出 | `MapXiHanOperations()`、`MapXiHanOpenApi()` |
| `Protocols.Grpc` | `Expose.Grpc`，且有 Schema | 服务 `{Resource}`、方法 `{Verb}` | `MapXiHanGrpc()`、`MapXiHanProto()`（`GET /operations.proto`） |
| `Protocols.Mcp` | `Expose.Mcp` | tool `{resource}_{verb}` | `MapXiHanMcp()`（`POST /mcp`）；命令行宿主的 `mcp` 命令（stdio） |
| `Protocols.CloudEvents` | 事件类型能映射到主题 | 事件类型 → 主题 | `MapXiHanCloudEvents()`（`POST /events`）；`CloudEventsWebhookPublisher` |
| `Hosting.CommandLine` | `Expose.CommandLine` | 命令 `<resource> <verb>` | `invoke <操作 ID>` |

示例应用 SimpleApp 同时装配了四个适配器：`orders.create` 在 HTTP 上是 `POST /orders`，在 gRPC 上是 `Orders/Create`，在 MCP 上是 `orders_create`。本章后面三页分别讲 [MCP](/protocols/mcp)、[CloudEvents](/protocols/cloudevents) 与[命令行宿主](/protocols/command-line)。

## 服务名、方法名与配置

服务名是操作 ID 资源段的 PascalCase（连字符分段，`order-lines` → `OrderLines`），方法名是动词的 PascalCase：`orders.create` 就是 `Orders/Create`。想换名字或加包名，写在配置节 `XiHan:Protocols:Grpc`（`GrpcProtocolOptions`）：

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `Package` | string | 空 | protobuf 包名，小写标识符以点分隔（如 `acme.orders`）；写进 `.proto` 的 `package`，也是方法路径的前缀 `/{package}.{Service}/{Method}` |
| `Methods:<操作 ID>` | string | — | `Service/Method`，显式覆盖推导出的名字 |

服务名与方法名必须匹配 `^[A-Za-z_][A-Za-z0-9_]*$`。以下情形在映射时失败，应用启动不起来，这是有意的：契约问题要在部署前暴露，而不是在第一个客户端调用时。

| 情形 | 错误码 |
| --- | --- |
| 推导或配置的服务 / 方法名不合法 | `PROTOCOLS_VALIDATION_007` |
| 两个操作得到同一个服务 / 方法名 | `PROTOCOLS_CONFLICT_004` |
| 暴露给 gRPC 的操作没有 Schema，或 Schema 映射不成 protobuf（嵌套数组、映射里的映射） | `PROTOCOLS_VALIDATION_006` |
| 同名消息在不同操作里形状不同 | `PROTOCOLS_CONFLICT_003` |

## 最小示例

我们在已有 HTTP 宿主的应用上加 gRPC。三步：引用 `XiHan.Framework.Protocols.Grpc` 并装配 `GrpcProtocolFeature`；给 Kestrel 一个 HTTP/2 端点；`UseXiHan()` 之后映射 gRPC 方法与 `.proto` 端点。

```bash
dotnet add package XiHan.Framework.Protocols.Grpc --prerelease
```

契约只要在 `Expose` 里带上 `Grpc`，别的不改。下面是 SimpleApp 的下单操作：

```csharp
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;

namespace SimpleApp.Orders;

[Operation(Id, Stability = Stability.Experimental, Expose = Expose.Http | Expose.Grpc | Expose.Mcp, Permission = "orders.write")]
public sealed class CreateOrder(IRepository<Order, Guid> orders, IIdGenerator<Guid> ids, TimeProvider clock) : IOperation<PlaceOrderRequest, OrderResponse>
{
    public const string Id = "orders.create";

    public async ValueTask<Result<OrderResponse>> HandleAsync(PlaceOrderRequest request, InvocationContext context)
    {
        var order = new Order { Id = ids.NewId(), Name = request.Name, Amount = request.Amount };
        order.Place(clock.GetUtcNow());
        var added = await orders.AddAsync(order, context.CancellationToken);
        return added.IsFailure ? added.Error! : new OrderResponse(order.Id, order.Name, order.Amount, order.CreatedAt);
    }
}
```

不用 TLS 时，Kestrel 不能在同一个端口上同时协商 HTTP/1.1 与 HTTP/2，所以单开一个端口跑 h2c：

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

```csharp
using SimpleApp.Orders;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Protocols.Grpc;
using XiHan.Framework.Protocols.Grpc.Services;
using XiHan.Framework.Protocols.Http;
using XiHan.Framework.Protocols.Http.Routing;

var builder = WebApplication.CreateBuilder(args);

builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<HttpProtocolFeature>()
    .UseFeature<GrpcProtocolFeature>()
    .UseFeature<OrdersFeature>());

var app = builder.Build();

app.UseXiHan();
app.MapXiHanOperations();   // HTTP：POST /orders、GET /orders
app.MapXiHanGrpc();         // gRPC：Orders/Create、Orders/List
app.MapXiHanProto();        // GET /operations.proto

app.Run();
```

运行后，客户端先取 `.proto`，再用 grpcurl 调用。凭据放在 `authorization` 元数据里，与 HTTP 的 `Authorization` 头同一种写法：

```bash
curl -s localhost:5000/operations.proto -o operations.proto
grpcurl -plaintext -proto operations.proto -H "authorization: ApiKey dev-alice-key" localhost:5001 Orders/List
grpcurl -plaintext -proto operations.proto -H "authorization: ApiKey dev-alice-key" \
  -d '{"name":"widget","amount":3}' localhost:5001 Orders/Create
```

第一条返回订单列表，第二条返回新订单的 `id`、`name`、`amount`、`created_at`。方法由 `Grpc.AspNetCore.Server` 的 `IServiceMethodProvider` 动态绑定，消息以字节传递，适配器按 Schema 在 protobuf 与 JSON 之间转换。只支持一元方法，流式方法与 gRPC 反射服务未提供。

## 由 JSON Schema 推出线格式

请求与响应各对应一个消息，消息名取 Schema 的 `title`（封闭泛型带实参名，如 `PageResultOfUserResponse`），`$defs` 里的每个对象定义一个消息。字段号按 `properties` 的声明顺序从 1 起；`.proto` 里的字段名是属性名的 lower_snake_case，推导的 `json_name` 与属性名不同时显式标注。

| JSON Schema | protobuf |
| --- | --- |
| `string`；`contentEncoding: base64` | `string`；`bytes` |
| `boolean` | `bool` |
| `integer` + `format: int32` / `int64` | `int32` / `int64` |
| `integer` 无格式 | `string`，十进制文本 |
| `number` + `format: float` / `double` | `float` / `double` |
| `number` 其他（`decimal`、无格式） | `string`，十进制文本 |
| `array` | `repeated T`，元素只能是标量或消息 |
| `object` + `additionalProperties` | `map<string, T>`；键总是 `string`（JSON 属性名的文本），`Dictionary<int, T>` 也一样，`propertyNames` 不影响映射 |
| `$ref` 到对象定义 | 嵌套消息，允许递归 |
| `$ref` 到字符串枚举 | `string`，不建 proto `enum`，线格式不变 |
| 可空（`type` 或 `anyOf` 含 `null`） | `optional` |
| 无约束（`{}`） | `string`，JSON 文本原样；作数组元素或映射值时同样（`JsonArray` → `repeated string`，`JsonObject` → `map<string, string>`） |

解码时，线上缺席的布尔、数值、`repeated` 与 `map` 字段按 proto3 默认值补进 JSON（零值、`[]`、`{}`），契约 Schema 的 `required` 因此在 gRPC 上成立；字符串、字节与嵌套消息缺席即缺席。

## `/operations.proto`

`MapXiHanProto()` 把同一份描述生成的 `.proto` 发布在 `GET /operations.proto`（`pattern` 参数可改路径）。服务端在运行时从 Schema 推出同样的线格式，文件只是给客户端生成桩代码用。SimpleApp 的输出：

```protobuf
// Generated by XiHan Framework from operation contracts; the server derives it from the same schemas at runtime.
syntax = "proto3";

message ListOrdersRequest {
}

message OrderList {
  repeated OrderResponse items = 1;
}

message OrderResponse {
  string id = 1;
  string name = 2;
  int32 amount = 3;
  string created_at = 4;
}

message PlaceOrderRequest {
  string name = 1;
  int32 amount = 2;
}

service Orders {
  // orders.create
  rpc Create (PlaceOrderRequest) returns (OrderResponse);
  // orders.list
  rpc List (ListOrdersRequest) returns (OrderList);
}
```

## 上下文与截止时间

gRPC 元数据就是 HTTP/2 头，上下文映射与 HTTP 相同：`traceparent`、`baggage`、`authorization`、`accept-language`、`xihan-deadline`；没有 `xihan-deadline` 时接受 gRPC 标准的 `grpc-timeout`，客户端设置的 deadline 因此直接成为这次调用的截止时间。

gRPC 方法带 `PipelineBypassMetadata`，不经 `UseXiHan()` 的外层调用：每次调用在处理器里以请求头建自己的上下文、经完整管道执行，调用来源记协议 `grpc` 与方法全名（如 `/Orders/Create`），远端地址与 `User-Agent` 沿用 HTTP/2 请求。调用上下文的细节见[调用上下文](/startup/invocation-context)。

## 状态、错误映射与尾部元数据

操作返回的失败按错误 KIND 映射为 gRPC 状态：

| 错误 KIND | gRPC 状态 |
| --- | --- |
| `VALIDATION` | `InvalidArgument` |
| `NOTFOUND` | `NotFound` |
| `CONFLICT` | `Aborted` |
| `DENIED` | `PermissionDenied`；`SECURITY_DENIED_001` 与 `SECURITY_DENIED_003`（没有凭据、凭据无效）为 `Unauthenticated` |
| `UNAVAILABLE` | `Unavailable` |
| `TIMEOUT` | `DeadlineExceeded` |
| `CANCELLED` | `Cancelled` |
| 其他 | `Internal` |

`grpc-message` 是按 `accept-language` 翻译的文案；尾部元数据 `xihan-error-code` 是框架错误码，`xihan-trace-id` 是 trace-id，成功时也有。客户端排障时拿着 `xihan-trace-id` 去日志里查，拿着 `xihan-error-code` 对照[结果与错误处理](/web/errors)。

适配器自己的失败：

| 情形 | 状态 | 错误码 |
| --- | --- | --- |
| 请求不是合法 protobuf 或与 Schema 不符 | `InvalidArgument` | `PROTOCOLS_VALIDATION_005` |
| 响应编码不成 protobuf | `Internal` | `PROTOCOLS_UNEXPECTED_001` |
| 未登记的方法 | `Unimplemented` | gRPC 服务端默认 |
| 应用未就绪（启动中或正在停止） | `Unavailable` | `KERNEL_UNAVAILABLE_001`，不进管道，同样带两个尾部元数据 |

## 常见问题

### gRPC 能和 HTTP 共用一个端口吗

明文（h2c）不行：Kestrel 不能在没有 TLS 的端口上同时协商 HTTP/1.1 与 HTTP/2，所以示例单开 5001 端口。启用 TLS 后，ALPN 能在同一个端口上协商两种协议，这时只需一个端点，`Protocols` 设为 `Http1AndHttp2`。

### 支持服务端流或双向流吗

不支持，只有一元方法。操作契约是「一个请求、一个响应」的模型，流式语义不在契约里。需要推送用[实时通信](/web/realtime)，需要异步处理用[发布与订阅](/messaging/pubsub)。

### 客户端能用 gRPC 反射发现服务吗

反射服务未提供。客户端用 `GET /operations.proto` 取 `.proto`，这份文件与服务端运行时推出的线格式一致。grpcurl 用 `-proto` 参数即可，不用 `-use-reflection`。

### `decimal` 与无格式的整数为什么变成了 `string`

protobuf 没有十进制小数类型，`int64` 之外的整数范围也无法表达，所以这两类按十进制文本传输，客户端按文本解析，精度不丢。要在线上拿到数值类型，在契约里用 `int`、`long`、`float` 或 `double`。

### 启动时报 `PROTOCOLS_VALIDATION_006` 怎么办

某个暴露给 gRPC 的操作的 Schema 映射不成 protobuf：常见的是数组里套数组（`List<List<T>>`）或映射里套映射。把嵌套的那层包成一个对象（记录类型）即可；或者去掉这个操作的 `Expose.Grpc`。

## 相关包与下一步

- 包参考：[Protocols.Grpc](/packages/protocols-grpc)、[Protocols.Http](/packages/protocols-http)
- 契约与 Schema 的来源：[操作契约](/web/operations)、[规范化接口文档](/web/openapi)
- 错误码与 KIND：[结果与错误处理](/web/errors)
- 规范：RFC-0011《操作契约与 XGI》
- 下一步：[MCP](/protocols/mcp)，把同一份契约发布成大模型可调用的 tool
