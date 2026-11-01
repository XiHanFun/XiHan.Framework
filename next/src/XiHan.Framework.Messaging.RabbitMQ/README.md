# XiHan.Framework.Messaging.RabbitMQ

`messaging.queue@1` 的 RabbitMQ Provider（Ring 3，`role: provider`）。注册后顶替内存代理。

```csharp
builder.AddXiHan(x => x.UseFeature<RabbitMQMessagingFeature>());
```

连接来自绑定 `queue`（`queue`）的主值，没有绑定时取 `XiHan:Messaging:RabbitMQ:Configuration`（AMQP URI）：两者都给时绑定优先，其余键照读配置节（RFC-0016「资源绑定与配置节」）；两者都没有时装配失败。

## 语义

- 发布方只声明交换机（ADR-0079）：消息发到持久的 direct 交换机 `Exchange`（默认 `xihan`），路由键 `<QueuePrefix><topic>`（前缀默认 `xihan.`），发布方不声明队列；消息持久化，`content_type` 为 `application/cloudevents+json`，`message_id` 为事件 ID。
- 队列只由订阅方声明：订阅方启动时声明持久队列 `<QueuePrefix><topic>` 并以同名路由键绑定到交换机；订阅方第一次启动之后，它离线期间的消息留在队列里。每个订阅主题一个通道与一个消费者，预取 `PrefetchCount`（默认 16），手动确认，单线程派发保证主题内顺序。
- 广播订阅（RFC-0009「订阅方式」）：每个实例声明独占、非持久、随连接删除的队列 `<QueuePrefix><topic>@<InstanceId>`（`MessagingInstance.Id`），以主题的路由键绑定到交换机，交换机给竞争队列与每个实例的广播队列各一份。实例与代理断开时队列随连接删除（客户端自动恢复连接时重新声明），断开期间的消息收不到；重试定向发回本实例的广播队列，到上限或无法解码时确认掉、记 Warning 日志，不进死信。
- 发布确认：发布通道开启发布确认并由客户端跟踪，发布以 `mandatory` 发出，等到代理确认才返回成功；代理拒绝（nack）返回 `MESSAGING_UNAVAILABLE_001`（批量发布逐项）。批量发布连续写出整批后一次等待全部确认。
- 没有订阅者：发到交换机的消息被退回（路由键上没有队列绑定）时丢弃，发布照常成功（批量逐项成功），记一条 Debug 日志——与内存代理一致，发件箱不为它重试、不记死信。调度、工作流等框架事件发到应用没有订阅的主题，不再建出没人消费的队列。
- 处理失败：尝试次数（头 `x-xihan-attempt`）小于 `MaxAttempts` 时经默认交换机重新发布到所消费队列的队尾，否则发布到死信队列 `<queue><DeadLetterSuffix>`（由死信的一方声明为持久队列并以同名路由键绑定，即主题加后缀的竞争队列）；重新发布得到确认后才确认原消息，未被确认、被退回（队列已不存在）或失败时把原消息 nack 并重新入队，由 RabbitMQ 再投递。无法解码的消息直接进死信（同样等确认）。
- 连接与通道异常抛出携带 `MESSAGING_UNAVAILABLE_001` 的 `FrameworkException`。
- 健康项 `messaging.rabbitmq.connection`：启动后按 `XiHan:Messaging:RabbitMQ:HealthProbeIntervalSeconds`（默认 15）在后台检查连接与发布通道是否打开并缓存结果；断开时条目 Degraded（`MESSAGING_UNAVAILABLE_001`，`reason` 是代理给出的关闭原因），应用仍在服务。

## 升级说明

- 先前版本由发布方建出的队列：没有人订阅的主题（如 `xihan.scheduling.job.started` 等 `scheduling.job.*`）的队列不再收到新消息，框架也不删除它们，已经堆积的消息留在原处。确认没有消费者后由运维删除：`rabbitmqctl list_queues name messages consumers` 找出 `consumers` 为 0 的框架主题队列，再 `rabbitmqctl delete_queue <队列>`（或在管理界面删除）。
- 订阅方的队列名与参数不变：升级后第一次启动即补上到交换机的绑定，积压照常消费。
- 滚动升级：发布方已是新版本、订阅方仍是旧版本时，新版本发出的消息没有绑定可走，会按「没有订阅者」丢弃。先升级订阅方，或让一个新版本的订阅实例先启动一次建好绑定；同一个应用的实例既发布又订阅，第一个新版本实例启动即建好绑定。
- 希望订阅方上线之前就留存消息的部署，由运维预先声明持久队列 `<QueuePrefix><topic>` 并以同名路由键绑定到交换机 `Exchange`。
- 发布方与订阅方的 `QueuePrefix`、`Exchange` 要一致，否则消息到不了订阅方的队列。

## 测试

发布确认、拓扑、广播队列与重新发布的确认用打桩的连接与通道覆盖（`RabbitMQPublisherConfirmTests`、`RabbitMQBroadcastTests`），不需要 RabbitMQ。一致性套件（含两个实例的广播与竞争用例）与拓扑用例（`RabbitMQTopologyTests`：没人订阅的主题没有队列、订阅方的队列在它离线期间留存消息、广播队列只属于本实例且随实例下线删除）需要真实 RabbitMQ：设置环境变量 `XIHAN_TEST_RABBITMQ`（如 `amqp://guest:guest@localhost:5672`）后运行；未设置时用例跳过。CI 用服务容器运行。

## 依赖方向

依赖 `Messaging.Abstractions` 与 `RabbitMQ.Client`。

## 状态

P2：可用，通过 `Messaging.Conformance` 套件。
