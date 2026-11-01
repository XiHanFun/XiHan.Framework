# XiHan.Framework.Messaging.Abstractions

消息契约（Ring 2）。能力 `messaging.queue@1`（至少一次投递的工作队列）与 `messaging.stream@1`（按分区键有序、按消费组保存位点、可按时刻重放的流）由 Provider 提供：内存与 RabbitMQ 实现提供队列，Kafka 实现同时提供队列与流。

| 契约 | 用途 |
| --- | --- |
| `MessageEnvelope`、`CloudEventsFormat`、`TopicName`、`EnvelopeExtensions` | CloudEvents 1.0 信封与结构化 JSON 编码；主题文法；租户、主体、关联、区域、追踪与分区键扩展属性，前四个与 Baggage 的对应 `BaggageMappings`（幂等键不进入消息） |
| `IMessageBroker`、`MessageDelivery` | 传输：发布（单条与同主题批量 `PublishManyAsync(topic, envelopes)`，逐项结果、原生批量）、按主题消费、重试与死信；Provider 实现它 |
| `IMessagePublisher`、`DirectPublisher` | 应用发布消息的入口；批量发布 `PublishManyAsync(topic, envelopes)` 逐项结果与入参一一对应，单条是它的特例；在调用内发布时从当前调用补齐信封缺失的 `traceparent`、租户、主体、关联与区域扩展（RFC-0009「信封」） |
| `IMessageReplayer` | 流能力 `messaging.stream@1`：`ReplayAsync(topic, since)` 按时刻重放，只在提供流的 Provider（Kafka）被选中时登记 |
| `IMessageHandler`、`AddMessageHandler<THandler>(topic)`、`MessageSubscriptions` | 按主题订阅处理器 |
| `MessageDispatcher` | 每条消息在新作用域内构造调用上下文（租户、主体、关联、区域按 `EnvelopeExtensions.BaggageMappings` 进入 Baggage，`traceparent` 给出追踪 ID、父跨度与采样标志）并经完整内核管道执行处理器 |
| `MessagingRuntime`、`MessagingRegistration` | Provider 共用的装配与启停 |
| `AddDomainEvent<TEvent>(topic, type, jsonTypeInfo, subject?)`、`DomainEventRegistry`、`DomainEventRegistration`、`DomainEventDispatcher` | 领域事件派发（RFC-0009，ADR-0025）：应用显式登记每种事件到主题与 CloudEvents `type`（序列化元数据源生成，不经反射）；Transaction 阶段提交前把工作单元里聚合的事件映射成信封（`id` UUID v7、`source` `/{topic}`、`time` 为发生时间、`xihantenant`），同一主题一次 `PublishManyAsync`（追踪、主体、关联与区域由发布者补齐），全部成功后清空缓冲；未登记的事件类型 → `MESSAGING_VALIDATION_003`；装配了发件箱时与业务写入同事务 |
| `OutboxFeature`、`OutboxMessage`、`OutboxPublisher`、`OutboxDispatcher`、`OutboxOptions` | 事务性发件箱：有活动工作单元时先写发件箱表（批量发布一次写入），提交后由后台循环取到期的消息按主题分组批量发布、批量更新状态；失败按指数退避重试，尝试达到 `MaxAttempts`（缺省 10）或载荷无法解码进死信（`DeadLetteredAt`，不再投递），退避中与死信的消息不占批次，代理不可达的一轮回滚、不计尝试；投递成功的行保留 `DispatchedRetention`（缺省 1 天）后删除（ADR-0064）；表上的索引 `IX_OutboxMessage_DiAt_DeLeAt_CrAt`（`(DispatchedAt, DeadLetteredAt, CreatedAt)`，实体上的 `[EntityIndex]`，ADR-0072）同时服务取到期消息与保留期清理 |

```csharp
builder.Services.AddMessageHandler<OrderPlacedHandler>("orders");
builder.AddXiHan(x => x.UseFeature<InMemoryMessagingFeature>().UseFeature<OutboxFeature>());
```

处理器返回失败或抛出异常都算投递失败：重试到 `XiHan:Messaging:MaxAttempts`（默认 3）后进入死信。消息派发不写幂等键：重试必须重新执行处理器，去重由处理器按消息 ID 自理。

## 依赖方向

依赖 `XiHan.Framework.Kernel`、`XiHan.Framework.Localization.Abstractions`（消息目录）与 `XiHan.Framework.Persistence.Abstractions`（发件箱）。被 `Messaging.InMemory`、`Messaging.RabbitMQ`、`Messaging.Kafka` 引用。

## 状态

P2：可用。
