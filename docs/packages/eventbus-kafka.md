# XiHan.Framework.EventBus.Kafka

> 分布式事件总线的 Kafka 提供程序：所有事件写入同一主题，以事件名作为消息 Key，消费者组竞争消费、手动提交偏移。

- **NuGet**：`XiHan.Framework.EventBus.Kafka`
- **模块类**：`XiHanKafkaEventBusModule`
- **所在层**：基础设施层
- **关键依赖**：`Confluent.Kafka` 2.15.0 + [XiHan.Framework.EventBus](./eventbus)

## 概述

[XiHan.Framework.EventBus](./eventbus) 默认的 `IDistributedEventBus` 实现只在进程内分发。这个包提供基于 Kafka 的跨进程实现 `KafkaDistributedEventBus`：**所有事件写入同一个主题**，事件名作为消息 Key、序列化后的事件数据作为 Value、`messageId` / `correlationId` 放进消息 Header；消费端以**同一消费者组**竞争消费，保证一条分布式事件在集群中只被处理一次。

装上并 `[DependsOn]` 之后业务代码无需改动：照旧注入 `IDistributedEventBus` 发布、照旧实现 `IDistributedEventHandler<>` 消费。

## 何时使用

- 已有 Kafka 基础设施，希望事件与其它数据流统一在 Kafka 上治理
- 需要高吞吐、可回溯（按偏移重放）的事件管道
- 需要事件在集群中只被处理一次（消费者组竞争消费）

对吞吐要求不高、只想要一个轻量 MQ 时，[RabbitMQ](./eventbus-rabbitmq) 或 [Redis](./eventbus-redis) 提供程序部署成本更低。

## 安装与启用

```bash
dotnet add package XiHan.Framework.EventBus.Kafka
```

```csharp
[DependsOn(typeof(XiHanKafkaEventBusModule))]
public class MyModule : XiHanModule { }
```

`XiHanKafkaEventBusModule` 自身 `[DependsOn(typeof(XiHanEventBusModule))]`。它做两件事：

- `ConfigureServices`：绑定配置节 `XiHan:EventBus:Kafka` 到 `XiHanKafkaEventBusOptions`
- `OnApplicationInitializationAsync`：解析 `KafkaDistributedEventBus` 并调用 `InitializeAsync()`（确保主题、建生产者/消费者、启动消费循环）

### 它是怎么"替换"掉默认实现的

`KafkaDistributedEventBus` 标注 `[ExposeServices(typeof(IDistributedEventBus), typeof(KafkaDistributedEventBus))]` 且实现 `ISingletonDependency`，走**常规注册**（`services.Add`）；默认的 `LocalDistributedEventBus` 标注 `[Dependency(TryRegister = true)]`，走 `services.TryAdd`。模块按拓扑序加载，`EventBus` 先注册、本包随后追加，DI 解析单个服务取**最后一条**注册，因此 Kafka 实现胜出。

## 工作原理

### 初始化（`InitializeAsync`）

在应用初始化阶段执行，双检锁保证只跑一次：

1. 若 `EnsureTopicExists`（默认 `true`），用 `AdminClient` 建主题（分区数 `TopicPartitionCount`、副本数 `TopicReplicationFactor`）；主题已存在则忽略 `TopicAlreadyExists`，其它异常仅 `LogWarning` 不中断启动
2. 建生产者：`Acks = All` + `EnableIdempotence = true`（幂等生产者，避免重试导致的重复写入）
3. 建消费者：`GroupId` = 配置值，**`EnableAutoCommit = false`**（手动提交），`AutoOffsetReset` 由 `AutoOffsetReset` 配置解析（`latest` / `error` / 其余一律 `earliest`）
4. 以 `TaskCreationOptions.LongRunning` 起一个后台消费循环

### 发布路径

`PublishToBrokerAsync` → `producer.ProduceAsync(TopicName, new Message { Key = 事件名, Value = body, Headers = [...] })`：

- Header `messageId`：用于收件箱幂等去重
- Header `X-Correlation-Id`（`EventBusConsts.CorrelationIdHeaderName`）：链路关联标识
- 若返回的 `PersistenceStatus != Persisted`，记 `LogWarning`（**不抛异常**）

发布默认先落发件箱（`useOutbox: true`），由后台 `EventBoxOutboxSenderHostedService` 拉取后逐条投递。

### 消费路径

消费循环 `ConsumeLoopAsync`：`Subscribe(TopicName)` → 阻塞 `Consume(cancellationToken)` → 基类 `ProcessIncomingMessageAsync(messageId, message.Key, correlationId, message.Value)`：

1. 按事件名（消息 Key）查 `EventTypes`，**查不到直接返回**（本实例没订阅该事件）
2. 反序列化 JSON 为事件对象
3. 用 `correlationId`（若为合法 32 位十六进制）作为 W3C trace id 开一个 `eventbus.consume {事件名}` 的 Consumer Span
4. **若配置了收件箱**：写入收件箱（按 `messageId` 去重）后返回，由收件箱后台处理器异步触发处理器并重试
5. **未配置收件箱**：当前上下文直接触发处理器；失败则抛出

处理成功 → `Commit(consumeResult)`。处理抛异常 → 记 `LogError` 后**仍然 `Commit`**，避免毒消息卡死分区。

## 核心能力

- **跨进程分布式事件**：`KafkaDistributedEventBus`（`IDistributedEventBus` 单例），覆盖默认的进程内实现
- **竞争消费**：同一 `GroupId` 内一条事件只被一个实例处理
- **幂等生产者**：`Acks = All` + `EnableIdempotence = true`
- **手动提交偏移**：处理完才提交，配合收件箱可做到至少一次投递
- **自动建主题**：`EnsureTopicExists` 开关，生产环境常关闭（broker 侧禁用自动建主题时）
- **链路贯通**：`correlationId` 经消息 Header 传播，消费端归入同一 trace
- **优雅释放**：`IAsyncDisposable`，取消消费循环 → `Close`/`Dispose` 消费者 → `Flush(5s)` 生产者

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `XiHanKafkaEventBusModule` | 模块类；`[DependsOn(XiHanEventBusModule)]`，初始化阶段调用 `InitializeAsync()` |
| `KafkaDistributedEventBus` | 事件总线实现，继承 `BrokerDistributedEventBusBase`；`ISingletonDependency` + `IAsyncDisposable` |
| `XiHanKafkaEventBusOptions` | 配置选项；配置节 `XiHan:EventBus:Kafka` |
| `BrokerDistributedEventBusBase` | 三个 Broker 的公共基类（位于 `XiHan.Framework.EventBus`）：事件类型映射、订阅委派本地总线、发件箱投递、入站幂等/重试、Consumer Span |

## 配置

- **配置节名**：`XiHan:EventBus:Kafka`（来自 `XiHanKafkaEventBusOptions.SectionName`）

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `BootstrapServers` | `string` | `localhost:9092` | 集群地址（逗号分隔，如 `host1:9092,host2:9092`） |
| `TopicName` | `string` | `XiHan.EventBus` | 主题名称（所有事件写入同一主题） |
| `GroupId` | `string` | `XiHan.EventBus` | 消费者组 Id（同组内竞争消费） |
| `AutoOffsetReset` | `string` | `earliest` | 首次消费的偏移策略（`earliest` / `latest` / `error`） |
| `EnsureTopicExists` | `bool` | `true` | 初始化时是否自动建主题 |
| `TopicPartitionCount` | `int` | `1` | 自动建主题时的分区数 |
| `TopicReplicationFactor` | `short` | `1` | 自动建主题时的副本数 |

```json
{
  "XiHan": {
    "EventBus": {
      "Kafka": {
        "BootstrapServers": "localhost:9092",
        "TopicName": "MyApp.EventBus",
        "GroupId": "MyApp.EventBus",
        "AutoOffsetReset": "earliest",
        "EnsureTopicExists": false,
        "TopicPartitionCount": 3,
        "TopicReplicationFactor": 2
      }
    }
  }
}
```

## 使用示例

业务代码与单机时完全一致：

```csharp
[EventName("MyApp.Orders.OrderShipped")] // 事件名 = Kafka 消息 Key
public class OrderShippedEvent
{
    public long OrderId { get; set; }
}

public class OrderShippedHandler : IDistributedEventHandler<OrderShippedEvent>, ITransientDependency
{
    public Task HandleEventAsync(OrderShippedEvent eventData)
    {
        // 同一消费者组内只有一个实例会执行到这里
        return Task.CompletedTask;
    }
}

await _distributedEventBus.PublishAsync(new OrderShippedEvent { OrderId = 1001 });
```

## 扩展点 / 自定义

- **分区与顺序**：事件名作为消息 Key，同名事件落同一分区 → **同一事件类型的消息全局有序**，不同事件类型之间不保证顺序。需要按业务键（如订单号）保序时，须自行扩展 `PublishToBrokerAsync` 改写 Key
- **换发件箱为持久化实现**：见 [EventBus · 扩展点](./eventbus#扩展点-自定义)
- **启用收件箱**：配置 `XiHanDistributedEventBusOptions.Inboxes` 后，入站消息先落收件箱去重，再由后台处理器带重试地触发处理器

## 注意事项与最佳实践

- **不同应用必须用不同的 `GroupId`**。消费者组是竞争消费单元：两个不同的应用若共用同一 `GroupId`，会互相抢走对方的消息；抢到的实例因 `EventTypes` 里没有该事件名会**静默丢弃**（查不到类型直接 return，随后 Commit）。不报错、极难排查。共用同一主题但需各自全量消费时，务必分配不同 `GroupId`。
- **处理失败仍会提交偏移**。异常时记 `LogError` 后照样 `Commit`——刻意如此，避免毒消息永久阻塞分区。**可靠重试必须依赖收件箱（Inbox）**；未配置收件箱时，处理失败的事件就真的没了。
- **`EnsureTopicExists` 在生产环境建议关掉**。多数生产集群禁用了自动建主题；即便建主题失败，本包也只 `LogWarning` 不中断启动，随后的发布才会真正报错——看到"主题不存在"类错误时先回头看启动日志的这条警告。
- **单主题承载全部事件**。分区数决定并行度上限：`TopicPartitionCount = 1`（默认）时，同一消费者组内实质只有一个实例在消费。生产环境请按吞吐设置分区数。
- **发布是异步的**。`useOutbox` 默认 `true`，`PublishAsync` 返回时消息可能还在发件箱里。要同步直投传 `useOutbox: false`（会失去"事务提交后才发事件"的保证）。
- **默认发件箱是内存实现**，进程重启即丢。生产环境的"事件不丢"承诺需要把 `IEventOutbox` 换成持久化实现。
- **`PersistenceStatus != Persisted` 只记警告不抛异常**。极端情况下消息可能未真正落盘而调用方毫无察觉，关键链路请自行监控这条日志。
- **事件名就是消息 Key**，务必用 `[EventName("稳定名字")]` 显式指定；不加时回退 `Type.FullName`，重构改名会静默破坏生产者与消费者的契约。

## 依赖模块

- [XiHan.Framework.EventBus](./eventbus)（基类、发件箱/收件箱、本地事件总线）

## 相关模块

- [XiHan.Framework.EventBus.Abstractions](./eventbus-abstractions)
- [XiHan.Framework.EventBus.RabbitMQ](./eventbus-rabbitmq)
- [XiHan.Framework.EventBus.Redis](./eventbus-redis)
