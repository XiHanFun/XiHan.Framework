# XiHan.Framework.Messaging.Abstractions

消息契约（Ring 2）。能力 `messaging.queue@1`（至少一次投递的工作队列）由 Provider 提供；`messaging.stream@1` 预留。

| 契约 | 用途 |
| --- | --- |
| `MessageEnvelope`、`CloudEventsFormat`、`TopicName`、`EnvelopeExtensions` | CloudEvents 1.0 信封与结构化 JSON 编码；主题文法；租户、关联、追踪扩展属性 |
| `IMessageBroker`、`MessageDelivery` | 传输：发布（单条与同主题批量 `PublishManyAsync(topic, envelopes)`，逐项结果、原生批量）、按主题消费、重试与死信；Provider 实现它 |
| `IMessagePublisher`、`DirectPublisher` | 应用发布消息的入口；批量发布 `PublishManyAsync(topic, envelopes)` 逐项结果与入参一一对应，单条是它的特例 |
| `IMessageHandler`、`AddMessageHandler<THandler>(topic)`、`MessageSubscriptions` | 按主题订阅处理器 |
| `MessageDispatcher` | 每条消息在新作用域内构造调用上下文（租户、关联、追踪进入 Baggage）并经完整内核管道执行处理器 |
| `MessagingRuntime`、`MessagingRegistration` | Provider 共用的装配与启停 |
| `OutboxFeature`、`OutboxMessage`、`OutboxPublisher`、`OutboxDispatcher` | 事务性发件箱：有活动工作单元时先写发件箱表（批量发布一次写入），提交后由后台循环按主题分组批量发布、批量更新状态 |

```csharp
builder.Services.AddMessageHandler<OrderPlacedHandler>("orders");
builder.AddXiHan(x => x.UseFeature<InMemoryMessagingFeature>().UseFeature<OutboxFeature>());
```

处理器返回失败或抛出异常都算投递失败：重试到 `XiHan:Messaging:MaxAttempts`（默认 3）后进入死信。消息派发不写幂等键：重试必须重新执行处理器，去重由处理器按消息 ID 自理。

## 依赖方向

依赖 `XiHan.Framework.Kernel` 与 `XiHan.Framework.Persistence.Abstractions`（发件箱）。被 `Messaging.InMemory`、`Messaging.RabbitMQ` 引用。

## 状态

P2：可用。
