# XiHan.Framework.Protocols.CloudEvents

CloudEvents 协议适配器（Ring 5）。提供 `protocols.cloudevents@1`，是消息子系统信封（RFC-0009，`MessageEnvelope`）的 CloudEvents 1.0 HTTP 协议绑定（`spec/protocol/cloudevents.md`）：入站端点把 HTTP 上的事件变成信封交给消息子系统，出站 Webhook 发布者把信封推送到别的服务。需要 `messaging.queue@1`。

```csharp
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<CloudEventsProtocolFeature>().UseFeature<OrdersFeature>());
var app = builder.Build();
app.UseXiHan();
app.MapXiHanCloudEvents();   // POST /events：binary / structured / batch 事件 → 按类型映射主题 → 派发给处理器
```

```json
{ "XiHan": { "Protocols": { "CloudEvents": { "Topics": { "orders.placed": "orders" }, "Webhooks": { "orders": "https://hooks.example.com/orders" } } } } }
```

## 规则

- 入站内容模式：`Content-Type: application/cloudevents+json`（structured）、`application/cloudevents-batch+json`（batch，事件数组）、否则有 `ce-specversion` 头即 binary（属性在 `ce-*` 头，`Content-Type` 是数据内容类型，请求体是数据，头值按规范百分号解码）；都不是 → 400 `PROTOCOLS_VALIDATION_008`。信封不合法 → 400 `MESSAGING_VALIDATION_002`。
- 主题：`XiHan:Protocols:CloudEvents:Topics:<事件类型>` 显式映射，否则事件类型本身须是合法主题（`orders.placed` 可以直接当主题），都不是 → 400 `PROTOCOLS_VALIDATION_009`。
- `Inbound = Dispatch`（默认）：在请求内经内核管道派发给主题的处理器（`MessageDispatcher`：新作用域，租户 / 关联 / 追踪从扩展属性进入上下文），处理器的结果就是响应，失败按错误 KIND 写 Problem Details，发送方据此重试；主题没有处理器 → 404 `PROTOCOLS_NOTFOUND_003`。`Inbound = Publish`：发布到消息代理后立即 202，重试与死信由代理负责。
- batch：先把每个事件的类型解析成主题，任一事件没有映射整批 400、不处理任何事件；派发模式按顺序处理、第一个失败即停止并作为响应，发布模式按主题分组一次批量发布、第一个失败的结果作为响应；已处理的事件不回滚，发送方重试整批时处理器按 `id` + `source` 去重。
- 出站：`CloudEventsWebhookPublisher`（实现 `IMessagePublisher`，按具体类型注入，不替换代理的发布者）把信封 POST 到 `Webhooks:<主题>`，`Outbound` 为 `Binary`（默认）或 `Structured`；批量发布多条时用 batch 内容模式（`application/cloudevents-batch+json`）一次 POST，接收方须支持该模式，一个响应对整批生效；没有配置 → NOTFOUND，非 2xx → `PROTOCOLS_UNAVAILABLE_001`，连不上 → `PROTOCOLS_UNAVAILABLE_002`。命名 HttpClient `xihan.protocols.cloudevents` 可配置超时与重试。
- 入站端点经过 `UseXiHan()`：请求凭据在外层认证，可用端点约定（如 `RequireXiHanAuthentication()`）限制调用方。

## 依赖方向

依赖 `XiHan.Framework.Kernel`、`XiHan.Framework.Hosting.AspNetCore`、`XiHan.Framework.Messaging.Abstractions`、`XiHan.Framework.Localization.Abstractions` 与 `Microsoft.AspNetCore.App`。协议适配器在宿主之上（Ring 5）。

## 状态

P3：可用（HTTP 绑定三种入站模式、两种出站模式）。
