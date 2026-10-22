# XiHan.Framework.Messaging.RabbitMQ

`messaging.queue@1` 的 RabbitMQ Provider（Ring 3，`role: provider`）。注册后顶替内存代理。

```csharp
builder.AddXiHan(x => x.UseFeature<RabbitMQMessagingFeature>());
```

连接来自 `XiHan:Messaging:RabbitMQ:Configuration`（AMQP URI），或绑定 `queue`（`queue`）的值；两者都没有时装配失败。

## 语义

- 主题映射为持久队列 `<QueuePrefix><topic>`（前缀默认 `xihan.`），默认交换机直接路由；消息持久化，`content_type` 为 `application/cloudevents+json`，`message_id` 为事件 ID。
- 每个订阅主题一个通道与一个消费者，预取 `PrefetchCount`（默认 16），手动确认，单线程派发保证主题内顺序。
- 处理失败：尝试次数（头 `x-xihan-attempt`）小于 `MaxAttempts` 时重新发布到队尾再确认原消息；否则发布到死信队列 `<queue><DeadLetterSuffix>` 并确认。无法解码的消息直接进死信。
- 连接与通道异常抛出携带 `MESSAGING_UNAVAILABLE_001` 的 `FrameworkException`。

## 测试

一致性套件需要真实 RabbitMQ：设置环境变量 `XIHAN_TEST_RABBITMQ`（如 `amqp://guest:guest@localhost:5672`）后运行；未设置时用例跳过。CI 用服务容器运行。

## 依赖方向

依赖 `Messaging.Abstractions` 与 `RabbitMQ.Client`。

## 状态

P2：可用，通过 `Messaging.Conformance` 套件。
