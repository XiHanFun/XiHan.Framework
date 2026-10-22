# CloudEvents 载体

> 规范性。参考实现：`XiHan.Framework.Protocols.CloudEvents`。消息子系统信封（RFC-0009）的 CloudEvents 1.0 HTTP 协议绑定。

## 信封映射（RFC-0009）

信封就是 CloudEvents 1.0 事件：`id`、`source`、`type` 必填，`time`、`subject`、`datacontenttype`、`data` 可选，扩展属性名 `^[a-z0-9]{1,20}$`。框架扩展 `xihantenant`、`xihancorrelation`、`traceparent` 在派发时进入调用上下文（租户、关联 ID、追踪）。

## 入站（RFC-0003 上下文：承载请求的头部按 `protocol/http.md` 映射；事件自身的上下文来自扩展属性）

| 模式 | 识别 | 事件 |
| --- | --- | --- |
| structured | `Content-Type: application/cloudevents+json` | 请求体为结构化 JSON（`CloudEventsFormat`） |
| batch | `Content-Type: application/cloudevents-batch+json` | 请求体为结构化 JSON 的数组，按顺序处理 |
| binary | 其他内容类型且有 `ce-specversion` 头 | `ce-id`、`ce-source`、`ce-type` 必填，`ce-time`（RFC 3339）、`ce-subject` 可选，其余 `ce-*` 为扩展属性；`Content-Type` 为数据内容类型（缺省 `application/json`），请求体为数据；头值按百分号解码 |

- 都不满足 → 400 `PROTOCOLS_VALIDATION_008`；信封不合法（规范版本、必填属性、时间、扩展名）→ 400 `MESSAGING_VALIDATION_002`。
- 主题 = `XiHan:Protocols:CloudEvents:Topics:<type>`，否则 `type` 本身（须满足主题文法），否则 400 `PROTOCOLS_VALIDATION_009`。
- `Inbound = Dispatch`（默认）：`MessageDispatcher` 在请求内以新作用域、新上下文经完整内核管道执行主题的处理器（`MessageDelivery.Attempt = 1`）；成功 202 无正文；处理器失败按错误 KIND 映射 HTTP 状态（RFC 9457 Problem Details），发送方据此重试；主题没有处理器 → 404 `PROTOCOLS_NOTFOUND_003`。
- `Inbound = Publish`：`IMessagePublisher.PublishAsync(topic, envelope)` 成功即 202；至少一次投递、重试与死信由代理负责（含发件箱）。
- batch：逐个处理，第一个失败即停止并作为响应；已处理的不回滚（至少一次语义，处理器按 `id` + `source` 去重）。

## 出站

- `CloudEventsWebhookPublisher.PublishAsync(topic, envelope)`：POST 到 `XiHan:Protocols:CloudEvents:Webhooks:<topic>`；`Outbound = Binary`（默认，`ce-*` 头 + 数据体，头值按规范百分号编码非可打印 ASCII）或 `Structured`（`application/cloudevents+json` 体）。
- 没有配置 → `PROTOCOLS_NOTFOUND_004`；响应非 2xx → `PROTOCOLS_UNAVAILABLE_001`（`status` 在详情里）；连接失败 → `PROTOCOLS_UNAVAILABLE_002`。重试由调用方决定（经发件箱投递即获得至少一次语义）。
- 使用命名 HttpClient `xihan.protocols.cloudevents`。
