# XiHan.Framework.Messaging.RabbitMQ

`messaging.queue@1` 的 RabbitMQ Provider（Ring 3，`role: provider`）。注册后顶替内存代理。

```csharp
builder.AddXiHan(x => x.UseFeature<RabbitMQMessagingFeature>());
```

连接来自绑定 `queue`（`queue`）的主值，没有绑定时取 `XiHan:Messaging:RabbitMQ:Configuration`（AMQP URI）：两者都给时绑定优先，其余键照读配置节（RFC-0016「资源绑定与配置节」）；两者都没有时装配失败。

## 语义

- 主题映射为持久队列 `<QueuePrefix><topic>`（前缀默认 `xihan.`），默认交换机直接路由；消息持久化，`content_type` 为 `application/cloudevents+json`，`message_id` 为事件 ID。
- 每个订阅主题一个通道与一个消费者，预取 `PrefetchCount`（默认 16），手动确认，单线程派发保证主题内顺序。
- 发布确认：发布通道开启发布确认并由客户端跟踪，发布以 `mandatory` 发出，等到代理确认才返回成功；代理拒绝（nack）或无法路由（退回）按失败返回 `MESSAGING_UNAVAILABLE_001`（批量发布逐项），退回时清掉该队列的声明缓存、下一次发布重新声明。批量发布连续写出整批后一次等待全部确认。
- 处理失败：尝试次数（头 `x-xihan-attempt`）小于 `MaxAttempts` 时重新发布到队尾，否则发布到死信队列 `<queue><DeadLetterSuffix>`；重新发布得到确认后才确认原消息，未被确认或失败时把原消息 nack 并重新入队，由 RabbitMQ 再投递。无法解码的消息直接进死信（同样等确认）。
- 连接与通道异常抛出携带 `MESSAGING_UNAVAILABLE_001` 的 `FrameworkException`。
- 健康项 `messaging.rabbitmq.connection`：启动后按 `XiHan:Messaging:RabbitMQ:HealthProbeIntervalSeconds`（默认 15）在后台检查连接与发布通道是否打开并缓存结果；断开时条目 Degraded（`MESSAGING_UNAVAILABLE_001`，`reason` 是代理给出的关闭原因），应用仍在服务。

## 测试

发布确认与重新发布的确认用打桩的连接与通道覆盖（`RabbitMQPublisherConfirmTests`），不需要 RabbitMQ。一致性套件需要真实 RabbitMQ：设置环境变量 `XIHAN_TEST_RABBITMQ`（如 `amqp://guest:guest@localhost:5672`）后运行；未设置时用例跳过。CI 用服务容器运行。

## 依赖方向

依赖 `Messaging.Abstractions` 与 `RabbitMQ.Client`。

## 状态

P2：可用，通过 `Messaging.Conformance` 套件。
