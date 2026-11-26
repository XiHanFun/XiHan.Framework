# 队列与流

这一页讲消息子系统的两个能力 `messaging.queue` 与 `messaging.stream` 到底差在哪里，以及三个生产级 Provider 选型时要知道的行为：RabbitMQ 的订阅方声明队列、队尾重试、广播队列与死信队列；Kafka 的消费组位点、分区键有序、按时刻重放、预建主题与死信主题；Redis 的流与消费组、原位重试、超时回收、死信流与长度上限。最后说明实例标识的配置，以及消息与 CloudEvents 协议绑定的关系。读完你能按一致性与顺序要求选 Provider，写出带分区键的发布与按时刻重放的代码。前置知识是[发布与订阅](/messaging/pubsub)。

## 为什么需要

内存代理只在单进程内有效，进程一退出消息就没了，多实例部署下每个实例各有一份队列、彼此看不见。真正的工作队列要把消息放在独立的代理上，由多个实例竞争消费、失败后重试、重试不成进死信。工作队列之上还有一类需求是「顺序」与「回放」：同一个订单的事件必须按发生顺序处理，修复了处理器的缺陷后要把过去一小时的事件重新跑一遍。这些是流的语义，队列不承诺。框架把两类保证拆成两个能力：RabbitMQ 与 Redis 提供队列，Kafka 同时提供队列与流；业务代码只面对 `IMessagePublisher` 与 `IMessageHandler`，需要流的特性在清单里多声明一个能力。

## 队列与流的差别

`messaging.queue@1` 只承诺至少一次与按主题消费；`messaging.stream@1` 在此之上多三条保证：

| 保证 | `messaging.queue` | `messaging.stream`（Kafka） |
| --- | --- | --- |
| 投递 | 至少一次，按主题消费，重试到上限进死信 | 同样 |
| 顺序 | 不承诺；重试排到队尾，后面的消息先于重试被处理 | 信封扩展 `xihankey` 决定分区，同一主题内同键的消息按发布顺序投递；重试在原位 |
| 位点 | 代理按队列管理，消费即出队 | 按消费组（`ConsumerGroup`，缺省 `xihan`）保存，投递完成后才提交；重启续接 |
| 重放 | 没有 | `IMessageReplayer.ReplayAsync(topic, since, ct)` 把该时刻起的消息重新投递给当前的竞争订阅 |

| Provider | 提供的能力 | 特性入口 | 清单 ID | 配置节 | 可选绑定 |
| --- | --- | --- | --- | --- | --- |
| `Messaging.InMemory` | `messaging.queue@1` | `InMemoryMessagingFeature`（随 Standard 装配） | `xihan.messaging.inmemory` | `XiHan:Messaging:InMemory` | — |
| `Messaging.RabbitMQ` | `messaging.queue@1` | `RabbitMQMessagingFeature` | `xihan.messaging.rabbitmq` | `XiHan:Messaging:RabbitMQ` | `queue` |
| `Messaging.Kafka` | `messaging.queue@1`、`messaging.stream@1` | `KafkaMessagingFeature` | `xihan.messaging.kafka` | `XiHan:Messaging:Kafka` | `stream` |
| `Messaging.Redis` | `messaging.queue@1` | `RedisMessagingFeature` | `xihan.messaging.redis` | `XiHan:Messaging:Redis` | `queue`（类型 `cache:distributed`） |

三个远程 Provider 的底层客户端都未声明 Native AOT 兼容，AOT 发布的应用只能用内存代理或自己的 Provider。三个 Provider 的重试之间都不等待，次数都由 `XiHan:Messaging:MaxAttempts` 决定；发件箱有自己独立的重试与死信规则，见[领域事件与发件箱](/messaging/domain-events)。

Redis 的流本身是有序日志，`XRANGE` 也能按时刻读，但 `Messaging.Redis` 不提供 `messaging.stream`：竞争组把条目轮流分给组内的消费者，同一分区键的消息可能同时在两个实例上处理，做不到同键有序（ADR-0130）。

## 最小示例

装配 Kafka，再写一个把同一订单的事件放进同一分区、并能按时刻重放的服务。所在特性的清单声明 `messaging.stream`（`MessagingCapabilities.Stream`），因为 `IMessageReplayer` 只在 Kafka 被选中提供流能力时登记。`Program.cs` 接在[第一个应用](/getting-started/first-app)换成 Standard 组合后的那份上，`AddXiHan` 里只多一行：

```csharp
using XiHan.Framework.Messaging.Kafka;

// Kafka 顶替内存代理提供 messaging.queue，并提供 messaging.stream
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<KafkaMessagingFeature>());
```

```json
{
  "XiHan": {
    "Messaging": {
      "Kafka": {
        "BootstrapServers": "localhost:9092",
        "ConsumerGroup": "orders"
      }
    }
  }
}
```

```csharp
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;

public sealed class OrderStream(IMessagePublisher publisher, IMessageReplayer replayer)
{
    // 同一订单的事件用订单 ID 作分区键，消费端按发布顺序收到
    public ValueTask<Result> PublishAsync(Guid orderId, MessageEnvelope envelope, CancellationToken cancellationToken)
        => publisher.PublishAsync("orders", envelope.WithExtension(EnvelopeExtensions.PartitionKey, orderId.ToString("N")), cancellationToken);

    // 修复处理器的缺陷后，把最近一小时的消息重新跑一遍；处理器要能去重
    public ValueTask<Result> ReplayLastHourAsync(CancellationToken cancellationToken)
        => replayer.ReplayAsync("orders", DateTimeOffset.UtcNow.AddHours(-1), cancellationToken);
}
```

本地可以用单节点容器起一个 Kafka：`docker run -d -p 9092:9092 apache/kafka`。启动应用后，对同一个订单连续发布三条事件，处理器按发布顺序收到它们，别的订单的事件可能交错在中间；调用 `ReplayLastHourAsync` 后处理器再次收到这一小时内的全部消息，正常消费的位点不受影响。主题不存在或该时刻之后没有消息时重放成功返回；代理不可达返回 `MESSAGING_UNAVAILABLE_001`。

## RabbitMQ

装配方式与最小示例相同，只把特性换成 `RabbitMQMessagingFeature`（命名空间 `XiHan.Framework.Messaging.RabbitMQ`）。它顶替内存代理，处理器、发布者与发件箱的代码不变。

```json
{ "XiHan": { "Messaging": { "RabbitMQ": { "Configuration": "amqp://guest:guest@localhost:5672" } } } }
```

必须给的只有 `Configuration`（AMQP URI）：有 `queue` 绑定时被绑定覆盖，两者都没有时装配失败。队列前缀、交换机与预取数等见 [Messaging.RabbitMQ](/packages/messaging-rabbitmq#配置)。

选型时要知道的行为：

- 消息发到一个 direct 交换机，路由键 `<QueuePrefix><topic>`（前缀缺省 `xihan.`；前缀与交换机名有孤立的代理项时启动失败，它们经客户端宽松编码，两套配置会落到同一个队列上，ADR-0321）。持久队列只由订阅方在启动时声明并绑定，发布方不声明队列。
- 发布开启了发布确认，代理确认后才返回成功；消费手动确认。
- 处理失败的消息重新发布到队尾，后面的消息先于重试被处理；到 `MaxAttempts` 进死信队列 `<QueuePrefix><topic><DeadLetterSuffix>`。
- 广播订阅是每个实例一条独占、随连接删除的队列 `<QueuePrefix><topic>@<InstanceId>`，重试到上限时确认掉并记 Warning，不进死信。
- 滚动升级时先升级订阅方，或让一个新版本的订阅实例先启动一次建好绑定，否则新主题的消息在订阅方上线前按「没有订阅者」丢弃。

通道与预取、重新发布的确认顺序、无法解码的消息与停止时的处理见 [Messaging.RabbitMQ](/packages/messaging-rabbitmq)。

## Kafka

`KafkaMessageBroker` 同时实现 `IMessageBroker` 与 `IMessageReplayer`，特性只为被选中的能力绑定接口。最小示例只配了 `BootstrapServers`（引导地址，有 `stream` 绑定时被绑定覆盖，两者都没有时装配失败）与 `ConsumerGroup`（竞争订阅的消费组，缺省 `xihan`）。主题前缀、重投等待与透传给 librdkafka 的 `Properties:<key>`（SASL、TLS）见 [Messaging.Kafka](/packages/messaging-kafka#配置)；凭据从密钥来源注入，不要写在仓库里的配置文件中，见[远程配置与密钥](/configuration/remote)。

差别表之外，选型时要知道的行为：

- 主题映射为 `<TopicPrefix><topic>`（前缀缺省 `xihan.`；前缀与 `ConsumerGroup` 有孤立的代理项时启动失败，ADR-0321）。没有分区键的消息由 Kafka 分配分区，只在主题只有一个分区时全局有序。
- 竞争订阅没有已提交位点时从最早位点开始，订阅前发布的消息也会收到。
- 处理失败在原位重试到 `MaxAttempts`，竞争订阅仍失败就发布到死信主题 `<TopicPrefix><topic><DeadLetterSuffix>`。原位重试是分区内有序的代价：一条消息重试期间，同分区后面的消息等待。处理器对暂时性故障应快速失败，对永久性故障尽早返回失败让它进死信。
- 广播订阅用本实例独占的消费组 `<ConsumerGroup>@<InstanceId>`，从最新位点开始、不提交位点，重启不回放历史；重试耗尽时丢弃并记 Warning，不发布死信。
- 重放只投给竞争订阅：用一个新的独立消费组从该时刻读到调用时的高水位，经同样的派发、重试与死信规则再投递一遍，不提交位点、不影响正常消费；处理器要能按 `Id` + `Source` 去重。

生产者的确认方式、位点提交与死信发布失败后的回退重投见 [Messaging.Kafka](/packages/messaging-kafka)。

::: warning Kafka 关了自动建主题时要预建主题
Kafka 缺省开启 `auto.create.topics.enable`，主题由 broker 建出来。生产集群常把它关掉，这时主题不存在是部署错误，框架不替你建，也不把它当作「没有订阅者」丢弃：发布约 30 秒后失败（`MESSAGING_UNAVAILABLE_001`），订阅一直等到主题建好，死信主题不存在时消息回退重投、分区停在原地。要预先建好应用发布与订阅的主题、被竞争订阅主题的死信主题，以及框架自己发布的调度与工作流事件主题（没人订阅的也要建）；收不到消息时先检查主题名是否带了 `TopicPrefix`。清单与建主题的命令见 [Messaging.Kafka](/packages/messaging-kafka#主题)。
:::

## Redis

`RedisMessageBroker` 用 Redis Streams 与消费组实现 `IMessageBroker`（需要 Redis 6.2 或更高）。装配方式同样只换特性：`RedisMessagingFeature`（命名空间 `XiHan.Framework.Messaging.Redis`）。它顶替内存代理，处理器、发布者与发件箱的代码不变；它不会因为应用装了 `Caching.Redis` 就自动启用。

```json
{
  "XiHan": {
    "Messaging": {
      "Redis": {
        "Configuration": "localhost:6379",
        "ConsumerGroup": "orders-api"
      }
    }
  }
}
```

示例用到的键：`Configuration` 是 StackExchange.Redis 连接字符串（有 `queue` 绑定时被绑定覆盖，两者都没有时装配失败），`ConsumerGroup` 是竞争订阅的消费组名（缺省 `xihan`）。取数批量、轮询间隔、回收阈值与流的长度上限见 [Messaging.Redis](/packages/messaging-redis#配置)。

选型时要知道的行为：

- 每个主题一条流 `<KeyPrefix><topic>`（前缀缺省 `xihan:messaging:`，有孤立的代理项时启动失败，ADR-0321），只由订阅方在启动时连同消费组一起建立，发布不建流。
- 竞争订阅在消费组 `ConsumerGroup` 里分摊条目，广播订阅每个实例一个组 `<ConsumerGroup>@<InstanceId>`。消费是轮询：客户端的多路复用连接不支持阻塞读，没取到就等 `PollIntervalSeconds`（缺省 0.2）再取。
- 处理失败在原位立即重试到 `MaxAttempts`；竞争订阅仍失败就写进死信流 `<KeyPrefix><topic><DeadLetterSuffix>`，广播订阅确认掉并记 Warning，不进死信。
- 实例崩溃时没确认的条目，空闲超过 `ClaimIdleTimeSeconds`（缺省 60）后由别的实例认领、接着计数投递。它必须大于一批条目的最长处理时间，否则还在处理的条目会被别的实例认领、重复处理。
- 实例正常停止时不再读新条目，没读走的留在流里由别的实例直接读到；已经拿到却没处理完的条目退回投递次数，空闲时间设到 `ClaimIdleTimeSeconds`，别的实例下一次维护就接手，不用等满这段时间。
- 每条流以 `MaxLength`（缺省 100000）近似封顶，消费者落后超过它时最旧的条目在投递前被裁掉。

发布的事务、重试时的认领、回收与清理的细则见 [Messaging.Redis](/packages/messaging-redis)。

::: warning 消息用的 Redis 不能淘汰流
`maxmemory-policy` 要是 `noeviction` 或 `volatile-*`：`allkeys-*` 在内存满时会把流连同未投递的消息一起删掉。缓存用的 Redis 常配 `allkeys-lru`，所以消息 Provider 不读缓存的 `cache` 绑定，配置节也与缓存分开；要共用一台 Redis 时按消息的要求配淘汰策略。发布成功只表示主节点收下了条目，持久性取决于 Redis 的 AOF / RDB 与复制，见 [Messaging.Redis](/packages/messaging-redis)。
:::

## 实例标识

实例标识是 `MessagingInstance.Id`（消息装配登记的单例，每个宿主一个），广播订阅的队列名与消费组名用它。缺省每次启动生成（机器名、进程 ID 与 8 位随机十六进制）；要在 RabbitMQ 管理界面与 Kafka 消费组列表里认出实例，可以用 `XiHan:Messaging:InstanceId` 配一个固定值：

```json
{ "XiHan": { "Messaging": { "InstanceId": "orders-api-0" } } }
```

- 文法：1 到 64 个字母、数字、`.`、`_`、`-`，字母或数字开头（不含 `@`）。写了但不合文法（含空字符串）在装配时抛 `ArgumentException`；不想固定就不写这个键。
- 必须每个同时在线的实例都不同。Kafka 与 Redis 上两个实例同一标识就是同一个广播消费组，组内分摊消息，广播静默退化成竞争；RabbitMQ 上后启动的实例声明同名独占队列被代理拒绝，启动失败（`MESSAGING_UNAVAILABLE_001`）。
- 多副本部署配成部署平台保证唯一的值，例如 Kubernetes 用下行 API 把 Pod 名设进环境变量，见 [Kubernetes](/deployment/kubernetes)；多个副本共用的配置文件里不要写死。
- 固定标识不改变广播语义：重启后仍不回放历史。代价是旧连接没有及时断开时（节点失联、进程被冻结），用同一标识重启的实例要等代理发现：RabbitMQ 在旧连接心跳超时前启动失败，Kafka 在旧成员会话超时前收不到广播。缺省的生成值每次都不同，没有这个问题。

## 与 CloudEvents 协议的关系

消息子系统管的是进程与代理之间的传输；`Protocols.CloudEvents` 是同一份信封在 HTTP 上的协议绑定：入站端点 `MapXiHanCloudEvents()` 接收 binary / structured / batch 三种内容模式，按 `XiHan:Protocols:CloudEvents:Topics:<事件类型>` 映射到主题，缺省在请求内经同一个 `MessageDispatcher` 派发给处理器（也可以只发布到代理）；出站的 `CloudEventsWebhookPublisher` 把信封推送到配置的 Webhook。`framework/samples/SimpleApp` 的 `POST /events` 就是把外部的 `orders.placed` 事件交给 `OrderPlacedHandler`。所以同一个 `IMessageHandler` 既能处理代理投递的消息，也能处理 HTTP 推来的 CloudEvent，细节见 [CloudEvents](/protocols/cloudevents)。

## 健康项

| 健康项 | 来源 | Degraded 的条件 |
| --- | --- | --- |
| `messaging.rabbitmq.connection` | `Messaging.RabbitMQ` | 后台按 `HealthProbeIntervalSeconds` 检查连接与发布通道，断开时 Degraded（`MESSAGING_UNAVAILABLE_001`） |
| `messaging.kafka.connection` | `Messaging.Kafka` | 后台按 `HealthProbeIntervalSeconds` 描述集群，不可达或超时时 Degraded（`KERNEL_UNAVAILABLE_005`） |
| `messaging.redis.connection` | `Messaging.Redis` | 后台按 `HealthProbeIntervalSeconds` 发 `PING`，连不上或超时时 Degraded |

三个 Provider 启动时都先连接代理再开始消费，连不上即启动失败（`MESSAGING_UNAVAILABLE_001`），不退回内存代理。

## 常见问题

### RabbitMQ、Kafka 和 Redis 怎么选

只要工作队列、不在意同一主题内的顺序：RabbitMQ 更轻，队列按订阅方声明、死信就是一个队列。同一业务对象的事件必须按顺序处理、或需要在修复后重放历史：Kafka，给信封设分区键，用 `IMessageReplayer`。部署里只有 Redis、不想再运维一个代理：Redis，语义与 RabbitMQ 的工作队列相同，代价是消费靠轮询、积压受 `MaxLength` 限制、持久性取决于 Redis 的持久化配置。单进程、开发与测试用内存代理。

### 订阅方还没启动过，这期间发布的消息去哪了

RabbitMQ 上没有队列可进，按「没有订阅者」丢弃；订阅方第一次启动声明队列之后，它离线期间的消息才会留在队列里。要在订阅方上线之前就留存，由运维预先声明持久队列并绑定。Redis 同理：没有订阅方建过消费组就没有流，消息丢弃；要预先留存，由运维建好流与组（`XGROUP CREATE <KeyPrefix><topic> <ConsumerGroup> $ MKSTREAM`）。Kafka 上消息进主题、留到保留期结束，竞争订阅第一次启动时从最早位点开始，能收到。

## 相关包与下一步

- 包参考：[Messaging.RabbitMQ](/packages/messaging-rabbitmq)、[Messaging.Kafka](/packages/messaging-kafka)、[Messaging.Redis](/packages/messaging-redis)、[Messaging.InMemory](/packages/messaging-inmemory)、[Messaging.Abstractions](/packages/messaging-abstractions)
- 绑定 `queue` / `stream` 的来源见[资源绑定](/configuration/bindings)；流 Provider 要通过的一致性套件见[一致性套件与门禁](/testing/conformance)
- 下一步：[gRPC](/protocols/grpc)，把同一份操作契约暴露成 gRPC 服务
