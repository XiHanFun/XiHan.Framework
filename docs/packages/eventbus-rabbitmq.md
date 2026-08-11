# XiHan.Framework.EventBus.RabbitMQ

> 分布式事件总线的 RabbitMQ 提供程序：以事件名作为路由键投递到 direct 交换机，同一队列竞争消费。

- **NuGet**：`XiHan.Framework.EventBus.RabbitMQ`
- **模块类**：`XiHanRabbitMQEventBusModule`
- **所在层**：基础设施层
- **关键依赖**：`RabbitMQ.Client` 7.2.1 + [XiHan.Framework.EventBus](./eventbus)

## 概述

[XiHan.Framework.EventBus](./eventbus) 默认的 `IDistributedEventBus` 实现是 `LocalDistributedEventBus`——它只在进程内分发，"分布式"只是接口语义。这个包提供真正跨进程的实现 `RabbitMQDistributedEventBus`：发布时把事件序列化为 UTF-8 JSON、以**事件名作为路由键**投递到 direct 交换机；消费时同一应用的多个实例**共享同一队列**形成竞争消费，保证一条分布式事件在集群中只被处理一次。

装上并 `[DependsOn]` 之后，你的业务代码**一行都不用改**——照旧注入 `IDistributedEventBus` 发布、照旧实现 `IDistributedEventHandler<>` 消费，事件自动跨进程流转。

## 何时使用

- 应用需要**多实例部署**，且分布式事件必须只被其中一个实例处理
- 需要跨服务（不同应用、不同语言）传递事件，且已有 RabbitMQ 基础设施
- 需要交换机级别的路由能力（按事件名绑定队列，实例只收自己订阅的事件）

不需要跨进程时不必装：默认的 `LocalDistributedEventBus` 已能满足单机场景。

## 安装与启用

```bash
dotnet add package XiHan.Framework.EventBus.RabbitMQ
```

```csharp
[DependsOn(typeof(XiHanRabbitMQEventBusModule))]
public class MyModule : XiHanModule { }
```

`XiHanRabbitMQEventBusModule` 自身 `[DependsOn(typeof(XiHanEventBusModule))]`，所以你不用再单独依赖 `EventBus`。它做两件事：

- `ConfigureServices`：绑定配置节 `XiHan:EventBus:RabbitMQ` 到 `XiHanRabbitMQEventBusOptions`
- `OnApplicationInitializationAsync`：解析 `RabbitMQDistributedEventBus` 并调用 `InitializeAsync()`（建连接、声明交换机/队列、绑定路由键、启动消费者）

### 它是怎么"替换"掉默认实现的

`RabbitMQDistributedEventBus` 标注 `[ExposeServices(typeof(IDistributedEventBus), typeof(RabbitMQDistributedEventBus))]` 且实现 `ISingletonDependency`，走**常规注册**（`services.Add`）；而默认的 `LocalDistributedEventBus` 标注 `[Dependency(TryRegister = true)]`，走 `services.TryAdd`。模块按拓扑序加载，`EventBus` 先于本包注册，因此默认实现先落位、本包随后**追加**一条 `IDistributedEventBus` 注册。DI 解析单个服务时取**最后一条**注册，所以 `RabbitMQDistributedEventBus` 胜出。

## 工作原理

### 初始化（`InitializeAsync`）

在应用初始化阶段（此时所有事件处理器已注册完毕）执行，双检锁保证只跑一次：

1. 用 `ConnectionFactory` 建连接（`AutomaticRecoveryEnabled = true`；若配了 `Uri` 则覆盖 Host/Port/凭据等单项配置），连接名取 `ClientProvidedName`
2. 开两个 channel：一个专用于发布、一个专用于消费
3. `ExchangeDeclare(ExchangeName, ExchangeType, durable: true)`
4. `QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: false)`
5. **遍历 `EventTypes.Keys` 逐个 `QueueBind`**——`EventTypes` 是基类 `BrokerDistributedEventBusBase` 在构造时根据 `XiHanDistributedEventBusOptions.Handlers` 填充的"事件名 → 事件类型"映射
6. `BasicQos(prefetchCount: PrefetchCount)`，挂 `AsyncEventingBasicConsumer`，`BasicConsume(autoAck: false)`

### 发布路径

`PublishToBrokerAsync` 把事件推入交换机：`DeliveryMode = Persistent`（消息持久化），`messageId` 写入 `BasicProperties.MessageId`、`correlationId` 写入 `BasicProperties.CorrelationId`，`routingKey` = 事件名。发布用信号量串行化（`_publishLock`），因为 channel 非线程安全。

注意发布**默认先落发件箱**（`useOutbox: true`）：`PublishAsync` → `AddToOutboxAsync` → 后台 `EventBoxOutboxSenderHostedService` 拉取 → `PublishManyFromOutboxAsync` → 逐条 `PublishToBrokerAsync`。

### 消费路径

消费者回调 `HandleReceivedAsync` → 基类 `ProcessIncomingMessageAsync(messageId, routingKey, correlationId, body)`：

1. 按事件名查 `EventTypes`，**查不到直接返回**（本实例没订阅该事件）
2. 反序列化 JSON 为事件对象
3. 用 `correlationId`（若为合法 32 位十六进制）作为 W3C trace id 开一个 `eventbus.consume {事件名}` 的 Consumer Span，让消费端日志/审计与上游同 trace
4. **若配置了收件箱**：写入收件箱（按 `messageId` 幂等去重）后返回，由收件箱后台处理器异步触发处理器并负责重试
5. **未配置收件箱**：当前上下文直接触发处理器；失败则抛出

回调成功 → `BasicAck`；抛异常 → 记 `LogError` 并 `BasicNack(requeue: false)`。

## 核心能力

- **跨进程分布式事件**：`RabbitMQDistributedEventBus`（`IDistributedEventBus` 单例），覆盖默认的进程内实现
- **竞争消费**：同一 `QueueName` 的多个实例共享队列，一条事件只被一个实例处理
- **按事件名路由**：direct 交换机 + 事件名路由键，实例只收自己 `[DependsOn]` 链上注册了处理器的事件
- **消息持久化**：交换机/队列 `durable: true`，消息 `DeliveryModes.Persistent`
- **链路贯通**：`correlationId` 经 `BasicProperties.CorrelationId` 传播，消费端归入同一 trace
- **幂等与重试**：复用框架收件箱（Inbox）机制，按 `messageId` 去重
- **优雅释放**：`IAsyncDisposable`，依次关闭消费 channel、发布 channel、连接

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `XiHanRabbitMQEventBusModule` | 模块类；`[DependsOn(XiHanEventBusModule)]`，初始化阶段调用 `InitializeAsync()` |
| `RabbitMQDistributedEventBus` | 事件总线实现，继承 `BrokerDistributedEventBusBase`；`ISingletonDependency` + `IAsyncDisposable` |
| `XiHanRabbitMQEventBusOptions` | 配置选项；配置节 `XiHan:EventBus:RabbitMQ` |
| `BrokerDistributedEventBusBase` | 三个 Broker 的公共基类（位于 `XiHan.Framework.EventBus`）：事件类型映射、订阅委派本地总线、发件箱投递、入站幂等/重试、Consumer Span |

## 配置

- **配置节名**：`XiHan:EventBus:RabbitMQ`（来自 `XiHanRabbitMQEventBusOptions.SectionName`）

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `Uri` | `string?` | `null` | 完整连接串（`amqp://user:pass@host:5672/vhost`）；**设置后优先于下方单项配置** |
| `HostName` | `string` | `localhost` | 主机名 |
| `Port` | `int` | `5672` | 端口 |
| `UserName` | `string` | `guest` | 用户名 |
| `Password` | `string` | `guest` | 密码 |
| `VirtualHost` | `string` | `/` | 虚拟主机 |
| `ExchangeName` | `string` | `XiHan` | 交换机名称 |
| `ExchangeType` | `string` | `direct` | 交换机类型（`direct` / `topic` / `fanout`），空值回退 `direct` |
| `QueueName` | `string` | `XiHan.EventBus` | 队列名称（同一应用的多实例共享 → 竞争消费） |
| `PrefetchCount` | `ushort` | `50` | 消费者预取数量（QoS） |
| `ClientProvidedName` | `string` | `XiHan.EventBus` | 客户端连接名（便于在管理台识别） |

```json
{
  "XiHan": {
    "EventBus": {
      "RabbitMQ": {
        "HostName": "localhost",
        "Port": 5672,
        "UserName": "guest",
        "Password": "guest",
        "VirtualHost": "/",
        "ExchangeName": "XiHan",
        "ExchangeType": "direct",
        "QueueName": "MyApp.EventBus",
        "PrefetchCount": 50,
        "ClientProvidedName": "MyApp"
      }
    }
  }
}
```

## 使用示例

业务代码与单机时**完全一致**——这正是这个包的价值：

```csharp
// 1) 事件：跨服务务必用 [EventName] 固定事件名（它就是路由键）
[EventName("MyApp.Orders.OrderShipped")]
public class OrderShippedEvent
{
    public long OrderId { get; set; }
}

// 2) 处理器：实现 IDistributedEventHandler<> 即被自动登记
public class OrderShippedHandler : IDistributedEventHandler<OrderShippedEvent>, ITransientDependency
{
    public Task HandleEventAsync(OrderShippedEvent eventData)
    {
        // 集群中只有一个实例会执行到这里
        return Task.CompletedTask;
    }
}

// 3) 发布：默认先落发件箱，后台异步投递到 RabbitMQ
await _distributedEventBus.PublishAsync(new OrderShippedEvent { OrderId = 1001 });
```

## 扩展点 / 自定义

- **换交换机类型**：`ExchangeType` 改 `topic` 可做通配路由；但队列绑定仍按事件名精确绑定，通配需自行扩展 `InitializeAsync`
- **换发件箱为持久化实现**：见 [EventBus · 扩展点](./eventbus#扩展点-自定义)，默认内存发件箱进程重启即丢
- **启用收件箱**：配置 `XiHanDistributedEventBusOptions.Inboxes` 后，入站消息先落收件箱去重，再由后台处理器带重试地触发处理器

## 注意事项与最佳实践

- **队列绑定只在 `InitializeAsync` 时做一次**。绑定的路由键来自 `EventTypes.Keys`，而 `EventTypes` 在总线构造时由 `XiHanDistributedEventBusOptions.Handlers` 填充。**应用初始化之后才动态 `Subscribe` 的事件，队列上没有对应绑定，消息永远不会投递到本实例。** 事件处理器请走常规的模块注册管线（实现 `IDistributedEventHandler<>` + 被约定扫描注册）。
- **不同应用必须用不同的 `QueueName`**。队列是竞争消费单元：两个不同的应用若共用同一队列名，会互相抢走对方的消息；而抢到的实例因 `EventTypes` 里没有该事件名，会**静默丢弃**（`ProcessIncomingMessageAsync` 查不到类型直接 return，随后 Ack）。这类问题不报错、极难排查。
- **失败即丢弃，没有死信队列**。消费抛异常时 `BasicNack(requeue: false)`——刻意不重投，避免毒消息无限循环。**可靠重试必须依赖收件箱（Inbox）**；未配置收件箱时，处理失败的事件就真的没了。生产环境请配置收件箱，或自行为队列挂 DLX。
- **发布是异步的**。`useOutbox` 默认 `true`，`PublishAsync` 返回时消息可能还躺在发件箱里。要同步直投传 `useOutbox: false`（但会失去"事务提交后才发事件"的保证）。
- **默认发件箱是内存实现**，进程重启即丢。生产环境的"事件不丢"承诺需要把 `IEventOutbox` 换成持久化实现。
- **事件名就是路由键**，务必用 `[EventName("稳定名字")]` 显式指定。不加时回退到 `Type.FullName`，一次命名空间重构就会让生产者与消费者对不上，且不会有任何编译期错误。
- **`Uri` 会覆盖单项配置**。同时配了 `Uri` 和 `HostName` 时，只有 `Uri` 生效——排查"连错环境"时先看这条。

## 依赖模块

- [XiHan.Framework.EventBus](./eventbus)（基类、发件箱/收件箱、本地事件总线）

## 相关模块

- [XiHan.Framework.EventBus.Abstractions](./eventbus-abstractions)
- [XiHan.Framework.EventBus.Kafka](./eventbus-kafka)
- [XiHan.Framework.EventBus.Redis](./eventbus-redis)
