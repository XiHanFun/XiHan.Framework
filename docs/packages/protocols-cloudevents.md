# XiHan.Framework.Protocols.CloudEvents

CloudEvents 协议适配器，提供 `protocols.cloudevents@1`：消息子系统信封的 CloudEvents 1.0 HTTP 协议绑定。入站端点接收 binary、structured、batch 三种内容模式的事件，按类型映射到主题后派发给处理器或发布到消息代理；出站 Webhook 发布者把信封推送到配置的 HTTP 端点。

适用于与外部系统用 CloudEvents 交换事件的应用。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Protocols.CloudEvents` |
| 环层 | Ring 5（协议与插件） |
| 能力 | 提供 `protocols.cloudevents@1`；依赖 `kernel.hosting@1`、`messaging.queue@1` |
| 特性入口 | `CloudEventsProtocolFeature` |
| 配置节 | `XiHan:Protocols:CloudEvents` |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Hosting.AspNetCore`、`XiHan.Framework.Messaging.Abstractions`、`XiHan.Framework.Localization.Abstractions`；共享框架 `Microsoft.AspNetCore.App` |
| AOT | 兼容（`IsAotCompatible`） |
| 稳定性 | Preview |

## 概述

`CloudEventsProtocolFeature` 登记选项、本包的消息目录、命名 HttpClient `xihan.protocols.cloudevents`（过[出站地址策略](../guide/outbound)）与 `CloudEventsWebhookPublisher`。

入站：`MapXiHanCloudEvents()` 映射 `POST /events`，按 `Content-Type` 与 `ce-specversion` 头识别内容模式，把事件读成 `MessageEnvelope`，事件类型经 `Topics` 映射到主题（没有映射时类型本身须是合法主题）。`Inbound = Dispatch`（缺省）时在请求内经 `MessageDispatcher` 派发给本实例里主题的处理器（竞争订阅与广播订阅各一次投递，先竞争后广播），处理器的结果就是响应，要让广播订阅到达每个实例用 `Publish`；`Inbound = Publish` 时发布到消息代理后立即 202。扩展属性 `xihantenant`、`xihanprincipal`、`xihancorrelation`、`xihanlocale` 与 `traceparent` 在派发时进入调用上下文。入站端点经过 `UseXiHan()`，请求凭据在外层认证。框架不给端点加任何要求，权限要由宿主自己加（`MapXiHanCloudEvents().RequireXiHanPermission(...)`）：扩展属性里的租户与主体是发送方给的，能调端点的人就能选租户（ADR-0121）。

出站：`CloudEventsWebhookPublisher` 实现 `IMessagePublisher`，把信封 POST 到 `Webhooks:<主题>`，模式为 `Binary`（缺省）或 `Structured`，批量时用 batch 内容模式一次 POST。它只按具体类型登记，不替换消息代理的发布者。信封按原样发出：它不像 `DirectPublisher`、`OutboxPublisher` 那样从当前调用补齐 `traceparent`、租户、主体、关联与区域扩展，租户 ID、主体 ID 等内部标识不会外推给接收方。这是定下的规则（ADR-0074）：接收方是第三方，内部标识推出去会暴露内部结构与身份、让第三方依赖内部 ID，追踪链是否跨出组织边界也由调用方决定；要让接收方拿到的扩展由调用方显式写进信封。HTTP 层同理（ADR-0083）：启用追踪时 .NET 的 HttpClient 本会按当前 Activity 写 W3C 的 `traceparent`、`tracestate` 与 `baggage` 头（与信封扩展无关），Webhook 的命名客户端 `xihan.protocols.cloudevents` 缺省不写这些头；要把追踪链接到接收方，设 `XiHan:Protocols:CloudEvents:PropagateTraceContext` 为 `true`，按 .NET 的全局传播器写。这只作用于 Webhook 的命名客户端（主处理器 `SocketsHttpHandler` 的 `ActivityHeadersPropagator`），不改进程全局的传播，出站请求的客户端跨度照常记录。调连接参数用 `UseSocketsHttpHandler` 配置同一个处理器，缺省保留；整个替换主处理器（`ConfigurePrimaryHttpMessageHandler`）则由替换后的处理器决定是否传播。

## 何时使用

外部系统（SaaS 的 Webhook、事件网关、其他语言的服务）以 CloudEvents 推事件进来，或应用要把消息以 CloudEvents 推给外部 HTTP 端点时用它。服务内部的消息传递直接用消息子系统，见[消息与事件](../guide/messaging)。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Protocols.CloudEvents --prerelease
```

框架尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。`Profiles.Full` 已包含本包。

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Protocols.CloudEvents;
using XiHan.Framework.Protocols.CloudEvents.Inbound;

var builder = WebApplication.CreateBuilder(args);
// Standard 组合里的内存消息实现提供 messaging.queue@1
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<CloudEventsProtocolFeature>());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanCloudEvents();   // POST /events
app.Run();
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Protocols:CloudEvents:Inbound` | `Dispatch` / `Publish` | `Dispatch` | 入站事件派发给处理器，或发布到消息代理；不区分大小写 |
| `XiHan:Protocols:CloudEvents:Outbound` | `Binary` / `Structured` | `Binary` | 出站单条事件的内容模式；不接受 `Batch` |
| `XiHan:Protocols:CloudEvents:PropagateTraceContext` | bool | `false` | 出站 Webhook 是否在 HTTP 头里写追踪上下文（`traceparent`、`tracestate`、`baggage`）；只作用于 Webhook 的命名客户端 |
| `XiHan:Protocols:CloudEvents:Topics:<事件类型>` | string | — | 事件类型到主题的映射，值须满足主题文法 |
| `XiHan:Protocols:CloudEvents:Webhooks:<主题>` | string | — | 出站 Webhook 地址，绝对的 `http` / `https` 地址 |

```json
{
  "XiHan": {
    "Protocols": {
      "CloudEvents": {
        "Inbound": "Dispatch",
        "Outbound": "Binary",
        "Topics": { "orders.placed": "orders" },
        "Webhooks": { "orders": "https://hooks.example.com/orders" }
      }
    }
  }
}
```

值不合法时读取配置就抛出 `ArgumentException`。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `CloudEventsProtocolFeature` | 特性入口 |
| `CloudEventsProtocolOptions` | `Inbound`、`Outbound`、`Topics`、`Webhooks`；`TopicFor(type)`；`FromConfiguration` |
| `Inbound.CloudEventsEndpointRouteBuilderExtensions` | `MapXiHanCloudEvents(pattern)`，缺省 `/events` |
| `Inbound.CloudEventsHttpBinding` | HTTP 绑定：`ModeOf`、`ReadAsync`、`Write`、`WriteBatch`、`EncodeHeaderValue` / `DecodeHeaderValue`；常量 `StructuredContentType`、`BatchContentType`、`HeaderPrefix` = `ce-` |
| `Inbound.CloudEventsContentMode` | `Binary`、`Structured`、`Batch` |
| `Inbound.CloudEventsInboundMode` | `Dispatch`、`Publish` |
| `Outbound.CloudEventsWebhookPublisher` | 出站发布者：`PublishAsync`、`PublishManyAsync`；常量 `HttpClientName` = `xihan.protocols.cloudevents` |
| `CloudEventsErrors` | 本包的错误码 |

## 使用示例

外部系统以 binary 模式推一个事件（`samples/SimpleApp`，主题 `orders` 上有 `OrderPlacedHandler`，端点要求 `events.deliver`，只有服务账号 `relay` 持有）：

```bash
curl -s -o /dev/null -w "%{http_code}\n" -X POST localhost:5000/events \
  -H "Authorization: ApiKey dev-relay-key" \
  -H "ce-specversion: 1.0" -H "ce-id: evt-1" -H "ce-source: /external" -H "ce-type: orders.placed" \
  -H "ce-xihantenant: acme" -H "Content-Type: application/json" -d '{"id":"evt-1"}'
```

出站：在消息处理器里把主题上的消息转发给 Webhook，失败时消息按消息子系统的规则重试：

```csharp
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Primitives;
using XiHan.Framework.Protocols.CloudEvents.Outbound;

public sealed class OrderWebhookForwarder(CloudEventsWebhookPublisher webhooks) : IMessageHandler
{
    public ValueTask<Result> HandleAsync(MessageDelivery delivery, InvocationContext context, CancellationToken cancellationToken)
        => webhooks.PublishAsync(delivery.Topic, delivery.Envelope, cancellationToken);
}
```

内容模式、batch 语义与派发模式的完整说明见 [gRPC、MCP 与 CloudEvents](../guide/protocols#cloudevents)。

## 行为与限制

- 派发模式下成功是 202、无正文，处理器失败按错误 KIND 写 Problem Details，发送方据此重试。
- batch 先解析全部事件的主题，任一没有映射整批 400、一个都不处理；已处理的事件不回滚，处理器按 `id` + `source` 去重。
- 出站多条事件用一个 batch 请求，接收方须支持 `application/cloudevents-batch+json`，一个响应对整批生效。
- 发布者自己不重试；超时与重试经命名 HttpClient `xihan.protocols.cloudevents` 配置，或放在消息处理器 / 发件箱里获得至少一次语义。

| 错误码 | 情形 |
| --- | --- |
| `PROTOCOLS_VALIDATION_008` | 请求不是 CloudEvent（400） |
| `MESSAGING_VALIDATION_002` | 信封不合法（400） |
| `PROTOCOLS_VALIDATION_009` | 事件类型没有映射到主题，类型本身也不是合法主题（400） |
| `PROTOCOLS_NOTFOUND_003` | 派发模式下主题在本实例两种订阅方式都没有处理器（404） |
| `PROTOCOLS_NOTFOUND_004` | 主题没有配置出站 Webhook |
| `PROTOCOLS_UNAVAILABLE_001` | Webhook 返回非 2xx，`status` 在错误细节里 |
| `PROTOCOLS_UNAVAILABLE_002` | Webhook 无法连接 |

## 相关

- [gRPC、MCP 与 CloudEvents](../guide/protocols)、[消息与事件](../guide/messaging)
- [Messaging.Abstractions](./messaging-abstractions)、[Hosting.AspNetCore](./hosting-aspnetcore)
- 规范：`spec/protocol/cloudevents.md`《CloudEvents 载体》、RFC-0009《消息、CloudEvents 信封与发件箱》
