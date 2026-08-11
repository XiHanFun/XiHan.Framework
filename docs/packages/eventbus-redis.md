# XiHan.Framework.EventBus.Redis

> 分布式事件总线的 Redis Streams 提供程序：`XADD` 写入同一 Stream，消费者组 `XREADGROUP` 竞争消费、处理后 `XACK`。

- **NuGet**：`XiHan.Framework.EventBus.Redis`
- **模块类**：`XiHanRedisEventBusModule`
- **所在层**：基础设施层
- **关键依赖**：`StackExchange.Redis` 3.0.11 + [XiHan.Framework.EventBus](./eventbus)

## 概述

[XiHan.Framework.EventBus](./eventbus) 默认的 `IDistributedEventBus` 实现只在进程内分发。这个包基于 **Redis Streams** 提供跨进程实现 `RedisDistributedEventBus`：发布时用 `XADD` 把事件写入同一个 Stream（字段含事件名 / `messageId` / `correlationId` / 数据）；消费时通过**消费者组** `XREADGROUP` 竞争消费，处理完 `XACK`，保证一条分布式事件在集群中只被处理一次。

它是三个 Broker 提供程序里**部署成本最低**的一个——多数项目本来就有 Redis。总线使用**独立连接**（与缓存各连各的），并带近似长度裁剪防止 Stream 无限增长。

## 何时使用

- 已有 Redis，不想为事件总线再引入 RabbitMQ / Kafka
- 事件量中等、可接受轮询延迟（默认无消息时 1 秒轮询一次）
- 需要跨进程竞争消费，但不需要 Kafka 那样的回溯与高吞吐

对可靠性要求极高（事件绝不能丢）时，优先考虑 [RabbitMQ](./eventbus-rabbitmq) 或 [Kafka](./eventbus-kafka)，并务必配置收件箱与持久化发件箱。

## 安装与启用

```bash
dotnet add package XiHan.Framework.EventBus.Redis
```

```csharp
[DependsOn(typeof(XiHanRedisEventBusModule))]
public class MyModule : XiHanModule { }
```

`XiHanRedisEventBusModule` 自身 `[DependsOn(typeof(XiHanEventBusModule))]`。它做两件事：

- `ConfigureServices`：绑定配置节 `XiHan:EventBus:Redis` 到 `XiHanRedisEventBusOptions`
- `OnApplicationInitializationAsync`：解析 `RedisDistributedEventBus` 并调用 `InitializeAsync()`（建连接、创建消费者组、启动消费循环）

### 它是怎么"替换"掉默认实现的

`RedisDistributedEventBus` 标注 `[ExposeServices(typeof(IDistributedEventBus), typeof(RedisDistributedEventBus))]` 且实现 `ISingletonDependency`，走**常规注册**（`services.Add`）；默认的 `LocalDistributedEventBus` 标注 `[Dependency(TryRegister = true)]`，走 `services.TryAdd`。模块按拓扑序加载，`EventBus` 先注册、本包随后追加，DI 解析单个服务取**最后一条**注册，因此 Redis 实现胜出。

## 工作原理

### 初始化（`InitializeAsync`）

在应用初始化阶段执行，双检锁保证只跑一次：

1. `ConnectionMultiplexer.ConnectAsync(Configuration)` 建**独立连接**（不复用缓存的连接）
2. `EnsureConsumerGroupAsync()`：`XGROUP CREATE`（`StreamPosition.NewMessages`、`createStream: true`）；已存在则捕获 `BUSYGROUP` 忽略
3. 以 `TaskCreationOptions.LongRunning` 起一个后台消费循环

消费者名固定为 `{机器名}:{每进程一个新 Guid}`。

### 发布路径

`PublishToBrokerAsync` → `XADD`，写入四个字段：

| 字段 | 内容 |
| --- | --- |
| `event` | 事件名 |
| `mid` | `messageId`（收件箱幂等去重用） |
| `cid` | `correlationId`（链路关联标识） |
| `data` | UTF-8 JSON 序列化的事件数据 |

`MaxStreamLength > 0`（默认 `100000`）时按**近似最大长度**裁剪（`useApproximateMaxLength: true`）；`<= 0` 则不裁剪。

发布默认先落发件箱（`useOutbox: true`），由后台 `EventBoxOutboxSenderHostedService` 拉取后逐条投递。

### 消费路径

消费循环 `ConsumeLoopAsync`：

1. `XREADGROUP`（`StreamPosition.NewMessages`，批量 `ReadBatchSize`）
2. 读取失败 → `LogError` → 等 `PollIntervalMilliseconds` 后重试；无消息 → 等 `PollIntervalMilliseconds` 后再读
3. 逐条取出字段，调基类 `ProcessIncomingMessageAsync(mid, event, cid, data)`：
   - 按事件名查 `EventTypes`，**查不到直接返回**（本实例没订阅该事件）
   - 反序列化 JSON 为事件对象
   - 用 `correlationId`（若为合法 32 位十六进制）作为 W3C trace id 开一个 `eventbus.consume {事件名}` 的 Consumer Span
   - **若配置了收件箱**：写入收件箱（按 `messageId` 去重）后返回，由收件箱后台处理器异步触发处理器并重试
   - **未配置收件箱**：当前上下文直接触发处理器
4. 处理抛异常 → `LogError`；**无论成功与否，`finally` 里都执行 `XACK`**（避免毒消息滞留 PEL）

## 核心能力

- **跨进程分布式事件**：`RedisDistributedEventBus`（`IDistributedEventBus` 单例），覆盖默认的进程内实现
- **竞争消费**：同一 `ConsumerGroup` 内一条事件只被一个实例处理
- **独立连接**：与缓存的 Redis 连接分离，互不影响
- **自动裁剪**：`MaxStreamLength` 近似裁剪，防止 Stream 无限增长撑爆内存
- **链路贯通**：`correlationId` 经 Stream 字段传播，消费端归入同一 trace
- **优雅释放**：`IAsyncDisposable`，取消消费循环 → 关闭并释放连接

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `XiHanRedisEventBusModule` | 模块类；`[DependsOn(XiHanEventBusModule)]`，初始化阶段调用 `InitializeAsync()` |
| `RedisDistributedEventBus` | 事件总线实现，继承 `BrokerDistributedEventBusBase`；`ISingletonDependency` + `IAsyncDisposable` |
| `XiHanRedisEventBusOptions` | 配置选项；配置节 `XiHan:EventBus:Redis` |
| `BrokerDistributedEventBusBase` | 三个 Broker 的公共基类（位于 `XiHan.Framework.EventBus`）：事件类型映射、订阅委派本地总线、发件箱投递、入站幂等/重试、Consumer Span |

## 配置

- **配置节名**：`XiHan:EventBus:Redis`（来自 `XiHanRedisEventBusOptions.SectionName`）

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `Configuration` | `string` | `localhost:6379` | StackExchange.Redis 连接串（独立于缓存连接） |
| `StreamKey` | `string` | `XiHan:EventBus:Stream` | 事件流键（所有事件写入同一 Stream） |
| `ConsumerGroup` | `string` | `XiHan.EventBus` | 消费者组名称（同组内竞争消费） |
| `ReadBatchSize` | `int` | `10` | 单次读取批量大小 |
| `PollIntervalMilliseconds` | `int` | `1000` | 无消息时的轮询间隔（毫秒） |
| `MaxStreamLength` | `int` | `100000` | Stream 近似最大长度，自动裁剪；`<= 0` 表示不裁剪 |

```json
{
  "XiHan": {
    "EventBus": {
      "Redis": {
        "Configuration": "localhost:6379",
        "StreamKey": "MyApp:EventBus:Stream",
        "ConsumerGroup": "MyApp.EventBus",
        "ReadBatchSize": 10,
        "PollIntervalMilliseconds": 1000,
        "MaxStreamLength": 100000
      }
    }
  }
}
```

## 使用示例

业务代码与单机时完全一致：

```csharp
[EventName("MyApp.Orders.OrderShipped")] // 事件名写入 Stream 的 event 字段
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

- **换发件箱为持久化实现**：见 [EventBus · 扩展点](./eventbus#扩展点-自定义)
- **启用收件箱**：配置 `XiHanDistributedEventBusOptions.Inboxes` 后，入站消息先落收件箱去重，再由后台处理器带重试地触发处理器
- **降低延迟**：调小 `PollIntervalMilliseconds`（代价是空转时的 Redis 请求变多）

## 注意事项与最佳实践

- **不同应用必须用不同的 `ConsumerGroup`（或不同的 `StreamKey`）**。消费者组是竞争消费单元：两个不同的应用若共用同一组名，会互相抢走对方的消息；抢到的实例因 `EventTypes` 里没有该事件名会**静默丢弃**（查不到类型直接 return，随后 `XACK`）。不报错、极难排查。
- **处理失败仍会 `XACK`**。异常时记 `LogError` 后照样在 `finally` 里确认——刻意如此，避免毒消息滞留 PEL。**可靠重试必须依赖收件箱（Inbox）**；未配置收件箱时，处理失败的事件就真的没了。
- **崩溃残留的消息不会被自动认领**。消费循环只读 `StreamPosition.NewMessages`（即 `>`），没有 `XAUTOCLAIM` / `XPENDING` 回收逻辑；进程若在 `XREADGROUP` 之后、`XACK` 之前崩溃，那批消息会永久留在 PEL 里无人处理（消费者名每进程都是新 Guid，重启后也不会认领旧条目）。需要这层保障时请启用收件箱，或自行补一个认领任务。
- **消费者组从"新消息"开始**。`XGROUP CREATE` 用的是 `StreamPosition.NewMessages`，因此**组创建之前已写入 Stream 的事件永远不会被消费**。首次部署时先启动消费端、再开始发布。
- **近似裁剪会丢未消费的事件**。`MaxStreamLength` 默认 `100000` 且按近似长度裁剪：写入速度远超消费速度时，老消息会被裁掉——哪怕还没被消费。事件积压时优先扩容消费端，别指望 Stream 兜底。
- **发布是异步的**。`useOutbox` 默认 `true`，`PublishAsync` 返回时消息可能还在发件箱里。要同步直投传 `useOutbox: false`（会失去"事务提交后才发事件"的保证）。
- **默认发件箱是内存实现**，进程重启即丢。生产环境的"事件不丢"承诺需要把 `IEventOutbox` 换成持久化实现。
- **空闲时有固定轮询开销**。无消息时每 `PollIntervalMilliseconds` 发一次 `XREADGROUP`；实例多时注意 Redis 的 QPS 基线。
- **事件名务必用 `[EventName("稳定名字")]` 显式指定**；不加时回退 `Type.FullName`，重构改名会静默破坏生产者与消费者的契约。

## 依赖模块

- [XiHan.Framework.EventBus](./eventbus)（基类、发件箱/收件箱、本地事件总线）

## 相关模块

- [XiHan.Framework.EventBus.Abstractions](./eventbus-abstractions)
- [XiHan.Framework.EventBus.RabbitMQ](./eventbus-rabbitmq)
- [XiHan.Framework.EventBus.Kafka](./eventbus-kafka)
- [XiHan.Framework.Caching](./caching)（Redis 缓存，与本包各用各的连接）
