# RFC-0009 消息、CloudEvents 信封与发件箱

> 状态：已采纳 | 日期：2026-10-21 | 对应 ADR：— | 参考实现：`XiHan.Framework.Messaging.Abstractions`、`XiHan.Framework.Messaging.InMemory`、`XiHan.Framework.Messaging.RabbitMQ` | 一致性：`XiHan.Framework.Messaging.Conformance`

## 摘要

消息是跨进程的调用：信封用 CloudEvents 1.0，投递经过与请求相同的内核管道，传输只负责至少一次投递、重试与死信。事务性发件箱把"写业务数据"与"发消息"放进同一个工作单元。

## 能力

| 能力 | 含义 | 默认实现 |
| --- | --- | --- |
| `messaging.queue@1` | 工作队列：发布、按主题消费、至少一次、重试、死信 | `Messaging.InMemory`（单进程） |
| `messaging.stream@1` | 流（预留） | — |

## 信封

CloudEvents 1.0：`id`、`source`、`type` 必填；`time`、`subject`、`datacontenttype`（默认 `application/json`）、`data`。线上编码为结构化 JSON（`application/cloudevents+json`）：JSON 数据内联在 `data`，其他数据放 `data_base64`，扩展属性是顶层成员。

框架扩展属性：`xihantenant`（→ Baggage `xihan.tenant`）、`xihancorrelation`（→ `xihan.correlation`）、`traceparent`（→ 追踪 ID 与父跨度）。扩展属性名 `^[a-z0-9]{1,20}$`。

主题文法 `^[a-z0-9][a-z0-9._-]{0,127}$`；死信主题为主题加 `DeadLetterSuffix`（默认 `.dlq`）。

## 投递

1. 应用用 `AddMessageHandler<THandler>(topic)` 订阅；处理器是作用域服务。
2. 代理为每个订阅主题消费，把投递交给派发器；派发器为每条消息开新作用域、构造调用上下文（Baggage 与追踪来自扩展属性）、经完整管道依次执行该主题的处理器，第一个失败即调用结果。
3. 处理器返回失败或抛异常都算投递失败。代理重试到 `MaxAttempts`（默认 3，含首次），之后进入死信。重试的顺序由代理决定（内存实现排到队尾）。
4. 派发不写幂等键：重试必须重新执行处理器；去重由处理器按 `id` + `source` 自理，或在 P3 由操作契约提供消息级去重。
5. 代理在应用进入服务状态之前不派发。

## 发件箱

`OutboxFeature` 需要 `messaging.queue@1` 与 `persistence.relational@1`：

- 把 `IMessagePublisher` 换成 `OutboxPublisher`：有活动工作单元时写入 `OutboxMessage` 表（随业务事务提交或回滚），没有时直接发布。
- 后台循环按 `PollInterval`（默认 1 秒）每轮一个工作单元：取未投递的消息（按创建时间，最多 `BatchSize`），发布，成功记投递时间，失败记次数与原因。投递是至少一次：循环提交前崩溃会重复投递。

## 一致性

Provider 必须继承 `MessageBrokerConformance` 并通过全部用例。

## 错误码

见 `conventions/error-codes.md` 中 `MESSAGING_*`。
