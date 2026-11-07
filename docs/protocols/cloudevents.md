# 14.3 CloudEvents

这一页讲 CloudEvents 适配器：入站端点怎样把 HTTP 上的事件（structured、batch、binary 三种内容模式）变成消息信封交给消息子系统，事件类型怎样映射到主题，Dispatch 与 Publish 两种模式的区别，为什么入站端点必须由宿主自己加权限要求；再讲出站 Webhook 发布者怎样把信封推给别的服务。读完你能接收第三方系统推来的事件，也能把自己的消息按 CloudEvents 格式推出去。前置知识是[发布与订阅](/messaging/pubsub)与 [gRPC](/protocols/grpc) 开头的「一份契约，多个载体」。

## 为什么需要

系统之间传事件，最常见的载体还是 HTTP：对方给你一个 Webhook 地址，或者让你开一个端点接收回调。各家的事件格式五花八门，每接一个来源就要写一遍解析、一遍去重、一遍把事件转成内部消息的代码。

CloudEvents 1.0 是 CNCF 定义的事件信封标准，框架的消息子系统本来就用它作为信封格式（RFC-0009）。于是 HTTP 上的 CloudEvents 不需要「转换」：入站端点按规范识别内容模式、校验信封、把事件类型映射到主题，然后像任何一条消息那样经完整管道交给 `IMessageHandler`；出站发布者反过来把信封按规范写成 HTTP 请求。你写的处理器不知道消息是从消息代理来的还是从 HTTP 来的。

## 入站：内容模式与主题

入站端点按请求识别三种内容模式：

| 模式 | 识别 | 事件 |
| --- | --- | --- |
| structured | `Content-Type: application/cloudevents+json` | 请求体是一个结构化 JSON 事件 |
| batch | `Content-Type: application/cloudevents-batch+json` | 请求体是结构化事件的数组，按顺序处理 |
| binary | 其他内容类型且有 `ce-specversion` 头 | `ce-id`、`ce-source`、`ce-type` 必填，`ce-time`（RFC 3339）、`ce-subject` 可选，其余 `ce-*` 头是扩展属性，头值按百分号解码；`Content-Type` 是数据内容类型，请求体是数据 |

事件类型先查配置 `XiHan:Protocols:CloudEvents:Topics:<事件类型>`，没有映射时类型本身须满足主题文法（`orders.placed` 可以直接当主题）。扩展属性 `xihantenant`、`xihanprincipal`、`xihancorrelation`、`xihanlocale` 与 `traceparent` 在派发时进入调用上下文，成为租户、主体、关联 ID、区域与追踪。

配置 `Inbound` 决定事件的去向：

| 模式 | 行为 | 响应 |
| --- | --- | --- |
| `Dispatch`（缺省） | 在请求内经消息派发器交给主题的处理器，以新的作用域与上下文经完整管道执行 | 处理器成功 202、无正文；失败按错误 KIND 写 Problem Details，发送方据此重试 |
| `Publish` | 经 `IMessagePublisher` 发布到消息代理，至少一次投递、重试与死信由代理负责（含发件箱） | 发布成功即 202 |

选 `Dispatch` 时发送方能立刻知道处理结果，适合对方要求同步确认的回调；选 `Publish` 时端点只负责收下，适合高吞吐或处理很慢的事件。

## 最小示例

我们让 SimpleApp 接收外部系统推来的 `orders.placed` 事件。三步：引用 `XiHan.Framework.Protocols.CloudEvents` 并装配 `CloudEventsProtocolFeature`（它依赖消息队列能力 `messaging.queue@1`，`UseStandardProfile()` 的内存消息实现即可满足）；订阅主题；`UseXiHan()` 之后映射端点并加上权限要求。

```bash
dotnet add package XiHan.Framework.Protocols.CloudEvents --prerelease
```

处理器就是普通的 `IMessageHandler`，在特性的 `Configure` 里订阅，权限码也在那里登记：

```csharp
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Primitives;

namespace SimpleApp.Orders;

public sealed class OrderPlacedHandler(OrderEventCounter counter) : IMessageHandler
{
    public ValueTask<Result> HandleAsync(MessageDelivery delivery, InvocationContext context, CancellationToken cancellationToken)
    {
        // 这里能看到租户等上下文；返回失败会让消息进入重试与死信流程
        counter.Increment();
        return ValueTask.FromResult(Result.Success());
    }
}
```

```csharp
// OrdersFeature.Configure 里；using XiHan.Framework.Kernel.Security 与 XiHan.Framework.Messaging.Abstractions
context.Services.AddMessageHandler<OrderPlacedHandler>("orders");
context.AddPermission(new PermissionDefinition("events.deliver", "Deliver events"));
```

```json
{
  "XiHan": {
    "Protocols": {
      "CloudEvents": {
        "Inbound": "Dispatch",
        "Topics": { "orders.placed": "orders" }
      }
    }
  }
}
```

```csharp
using SimpleApp.Orders;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Protocols.CloudEvents;
using XiHan.Framework.Protocols.CloudEvents.Inbound;

var builder = WebApplication.CreateBuilder(args);

builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<CloudEventsProtocolFeature>()
    .UseFeature<OrdersFeature>());

var app = builder.Build();

app.UseXiHan();
// POST /events：框架不给这个端点加任何要求，权限由宿主自己加
app.MapXiHanCloudEvents().RequireXiHanPermission(OrdersFeature.DeliverEventsPermission);

app.Run();
```

运行后以 binary 模式推一个事件。SimpleApp 里只有服务账号 `relay`（`dev-relay-key`）持有 `events.deliver`：

```bash
curl -s -o /dev/null -w "%{http_code}\n" -X POST localhost:5000/events \
  -H "Authorization: ApiKey dev-relay-key" \
  -H "ce-specversion: 1.0" -H "ce-id: evt-1" -H "ce-source: /external" -H "ce-type: orders.placed" \
  -H "ce-xihantenant: acme" -H "Content-Type: application/json" -d '{"id":"evt-1"}'
```

输出 `202`，事件已交给 `OrderPlacedHandler`，`GET /orders/stats` 的计数加一。匿名请求得到 401，`alice`、`bob` 得到 403。

## 为什么权限要宿主自己加

入站端点经过 `UseXiHan()`，请求凭据在外层认证，但框架不给它加任何授权要求。原因在扩展属性：事件里的 `xihantenant`、`xihanprincipal` 是发送方给的，派发时不和请求的身份核对，能调这个端点的人就能选租户、写审计主体。这与 HTTP 的 `baggage` 头不同，是这个协议的设计，框架不收紧，而是要求宿主明确决定谁能投递：

```csharp
using XiHan.Framework.Hosting.AspNetCore.Endpoints;

// 权限码由某个特性用 FeatureContext.AddPermission 登记
app.MapXiHanCloudEvents().RequireXiHanPermission("events.deliver");
```

权限不按租户收窄，只授给平台的服务账号或服务角色，不要授给租户里的角色。没有装配授权能力 `security.authorization@1` 时，这个要求只进入上下文、不被判定，端点等于敞开。授权的写法见[授权与权限](/security/authorization)。

## 批量、去重与错误

batch 先把每个事件的类型解析成主题，任一事件没有映射时整批 400、一个都不处理；派发模式逐个处理，第一个失败即停止并作为响应；发布模式按主题分组各一次 `PublishManyAsync`，第一个失败的结果作为响应。已处理的事件不回滚，发送方重试整批时，处理器按 `id` + `source` 去重，这是 CloudEvents 规范定义的事件唯一标识。

| 情形 | 状态码 | 错误码 |
| --- | --- | --- |
| 不是 CloudEvent（内容类型既不是 structured / batch，也没有 `ce-specversion`） | 400 | `PROTOCOLS_VALIDATION_008` |
| 信封不合法（规范版本、必填属性、时间、扩展名） | 400 | `MESSAGING_VALIDATION_002` |
| 事件类型没有映射，类型本身也不是合法主题 | 400 | `PROTOCOLS_VALIDATION_009` |
| 派发模式下主题没有处理器 | 404 | `PROTOCOLS_NOTFOUND_003` |
| 处理器或发布失败 | 按错误 KIND | 处理器或代理返回的错误码 |

## 出站 Webhook

`CloudEventsWebhookPublisher` 实现 `IMessagePublisher`，把信封 POST 到配置 `Webhooks:<主题>` 的地址。它只按具体类型登记，不替换消息代理的发布者，要推送的地方显式注入它。下例把 `orders` 主题上的每条消息转发给外部服务，Webhook 失败时处理器返回失败，消息按消息子系统的规则重试，用尽后进死信：

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

```csharp
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Protocols.CloudEvents.Outbound;

builder.Services.AddMessageHandler<OrderWebhookForwarder>("orders");
// 命名 HttpClient：超时与重试按 IHttpClientFactory 的方式配置
builder.Services.AddHttpClient(CloudEventsWebhookPublisher.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(10));
```

```json
{
  "XiHan": {
    "Protocols": {
      "CloudEvents": {
        "Outbound": "Binary",
        "Webhooks": { "orders": "https://hooks.example.com/orders" }
      }
    }
  }
}
```

`Outbound` 为 `Binary`（缺省，`ce-*` 头加数据体，头值按规范百分号编码）或 `Structured`（`application/cloudevents+json` 请求体），不接受 `Batch`。`PublishManyAsync` 一条时沿用这个模式，多条时用 batch 内容模式一次 POST，接收方须支持 `application/cloudevents-batch+json`，一个响应对整批生效。Webhook 地址必须是绝对的 `http` / `https` 地址，`Inbound`、`Outbound` 的值不区分大小写，不合法时读取配置就抛出 `ArgumentException`。

信封按原样发出，不从当前调用补齐扩展：`xihantenant`、`xihanprincipal`、`xihancorrelation`、`xihanlocale` 与 `traceparent` 都不由框架加上。接收方是第三方，租户、主体、关联是本系统的内部标识，推出去会暴露内部结构与身份，并让第三方依赖本该可变的内部 ID；追踪链要不要跨出组织边界也由调用方决定。要带给接收方的扩展显式写进信封。HTTP 层同理：启用追踪时 .NET 的 `HttpClient` 本会按当前 Activity 在请求头里写 W3C 的 `traceparent`、`tracestate` 与 `baggage`，Webhook 的命名客户端 `xihan.protocols.cloudevents` 缺省不写；确实要把追踪链接到接收方，设 `XiHan:Protocols:CloudEvents:PropagateTraceContext` 为 `true`，它只作用于这个客户端，出站请求的客户端跨度照常记录。

| 情形 | 错误码 |
| --- | --- |
| 主题没有配置 Webhook | `PROTOCOLS_NOTFOUND_004` |
| Webhook 返回非 2xx（`details.status` 是状态码） | `PROTOCOLS_UNAVAILABLE_001` |
| 连接失败 | `PROTOCOLS_UNAVAILABLE_002`，`reason` 只写异常类型的全名，完整异常进日志 |
| 地址被[出站地址策略](/security/outbound)拒绝 | `PROTOCOLS_UNAVAILABLE_002`，`reason` 是 `OutboundAddressDeniedException` 的类型全名，不发起连接 |

Webhook 的接收方是第三方，它的 3xx 能把请求引到内网，所以这个命名客户端的连接与重定向都过出站地址策略。接收方在内网时，把它写进 `XiHan:Security:Outbound:AllowedHosts`。发布者自己不重试，重试由调用方决定：像上例那样放在消息处理器里，或经发件箱投递，得到至少一次语义。

## 与消息子系统的关系

CloudEvents 适配器不新造概念，它只是消息子系统在 HTTP 上的两个接口：

| 消息子系统 | CloudEvents 适配器 |
| --- | --- |
| `MessageEnvelope`（CloudEvents 1.0 信封） | 入站解析成它，出站把它写成请求 |
| 主题与 `IMessageHandler` | 入站事件类型映射到主题，处理器不区分来源 |
| `IMessagePublisher` | `Publish` 模式用它发到代理；`CloudEventsWebhookPublisher` 是它的另一个实现 |
| 领域事件与发件箱 | 聚合记录的领域事件经发件箱发到主题，再由转发处理器推成 Webhook |

主题、处理器、重试与死信的规则见[发布与订阅](/messaging/pubsub)，领域事件与发件箱见[领域事件与发件箱](/messaging/domain-events)。

## 常见问题

### 发送方不按 CloudEvents 格式推，只给一个普通 JSON

端点只认 CloudEvents。两条路：让发送方在请求头里补 `ce-specversion`、`ce-id`、`ce-source`、`ce-type` 四个头，就是合法的 binary 模式，请求体不用改；做不到就自己写一个最小端点把它包成 `MessageEnvelope` 后发布，写法见[端点里使用框架](/web/endpoints)。

### 同一个事件收到两次怎么办

HTTP 投递是至少一次，重试必然带来重复。处理器按 `id` + `source` 去重（这是规范定义的事件唯一标识），或者把处理写成幂等的。batch 重试时已处理的事件不回滚，所以去重要落在处理器而不是端点。

### 入站事件的租户为什么不按凭据核对

事件来自别的系统，它的租户是事件的属性而不是调用者的属性，中继服务一次可能替多个租户投递。框架因此把判断权交给宿主：端点只开给平台服务账号，服务账号的持有者对事件里的租户负责。不要把投递权限授给租户内的角色。

### 为什么 Webhook 不带 `traceparent`

接收方是第三方，追踪链是否跨出组织由你决定，缺省不带。需要时设 `PropagateTraceContext` 为 `true`；信封里的 `traceparent` 扩展则要显式写进信封。

### Webhook 失败后谁来重试

发布者不重试。放在消息处理器里时由消息子系统按重试策略重投，用尽后进死信；经发件箱投递时由发件箱重试。两种都得到至少一次语义，接收方要能去重。

## 相关包与下一步

- 包参考：[Protocols.CloudEvents](/packages/protocols-cloudevents)、[Messaging.Abstractions](/packages/messaging-abstractions)
- 主题、处理器、发件箱：[发布与订阅](/messaging/pubsub)、[领域事件与发件箱](/messaging/domain-events)
- 端点的权限：[授权与权限](/security/authorization)；Webhook 的目标地址：[出站地址策略](/security/outbound)
- 规范：RFC-0009《消息、CloudEvents 信封与发件箱》
- 下一步：[命令行宿主](/protocols/command-line)，同一份契约在终端里当命令
