# XiHan.Framework.Messaging.Conformance

消息一致性套件。任何提供 `messaging.queue@1` 的 Provider 都必须继承 `MessageBrokerConformance` 并通过全部用例；提供 `messaging.stream@1` 的 Provider 继承 `MessageStreamConformance`（Preview，继承自 `MessageBrokerConformance`，同样实现 `CreateFeature()`），在队列用例之上再验证同一分区键按发布顺序投递与按时刻重放，不必再继承队列套件。

```csharp
public sealed class InMemoryMessageBrokerConformanceTests : MessageBrokerConformance
{
    protected override IFeature CreateFeature() => new InMemoryMessagingFeature();
}
```

套件覆盖：信封属性、扩展与数据完整到达处理器；同一主题按发布顺序投递；主题互不串扰；处理器失败或抛异常时重试到上限后停止且不阻塞后续消息；租户与关联扩展进入 Baggage；非法主题被发布者拒绝；发布到没人订阅的主题（单条与批量）成功；同一主题的竞争订阅与广播订阅各收到一次、各自重试；两个宿主连同一个代理时广播每个实例都收到、竞争每条只有一个实例收到（多实例用例要 `SupportsMultipleInstances`，默认为真；内存代理每个宿主一个进程内代理，覆写为假跳过）。广播在有的代理上异步接上，套件用 `WarmUpBroadcastAsync` 发预热消息等每个实例都收到后再断言；`StartWithSubscriptionsAsync(log, subscriptions)` 按给定订阅启动宿主。用例以 `Testing.TestHost` 走完整管道，等待投递有超时（`DeliveryTimeout`）。`Messaging.InMemory`、`Messaging.RabbitMQ` 通过队列套件，`Messaging.Kafka` 通过流套件。

发件箱的存储经仓储，持久化 Provider 继承 `OutboxConformance`（Preview），`CreateFeature()` 返回持久化特性（可覆写 `Configuration` 给连接与建表开关）。套件登记发件箱仓储与可推进的时钟，用脚本代理逐轮驱动 `OutboxDispatcher.RunOnceAsync`，不装配发件箱特性、不起后台循环。覆盖：代理对某条消息一直抛出时按尝试上限与退避进死信，同批与排在后面的照常发出；返回失败与抛出逐轮状态相同；调用方取消回滚本轮、不计尝试；不是调用方取消的 `OperationCanceledException` 计为失败（ADR-0108）。内存持久化、SqlSugar 的 SQLite 与 PostgreSQL、EF Core 的 SQLite 通过。EF Core 的上下文要把 `OutboxMessage` 放进模型。

```csharp
public sealed class InMemoryOutboxConformanceTests : OutboxConformance
{
    protected override IFeature CreateFeature() => new InMemoryPersistenceFeature();
}
```

## 依赖方向

依赖 `Messaging.Abstractions`、`Testing`、`Microsoft.Extensions.TimeProvider.Testing` 与 xUnit 核心包。工具链包。

## 状态

P2：可用。
