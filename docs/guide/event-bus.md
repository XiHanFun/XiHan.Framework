# 事件总线

用事件把「一件事发生了」和「谁来响应」拆开：发布方不认识订阅方，订阅方随时可增可减。这一章讲清楚两件最容易出错的事——处理器怎样才算真的订阅上了，以及事件到底在哪个时刻发出去。

完整 API 清单与全部配置项见 [EventBus 包](../packages/eventbus)。

## 本地事件还是分布式事件

框架提供两条独立的总线，接口不同、发布时机不同，选错会直接影响事务正确性。

| | 本地事件 | 分布式事件 |
| --- | --- | --- |
| 发布接口 | `ILocalEventBus` | `IDistributedEventBus` |
| 处理器接口 | `ILocalEventHandler<TEvent>` | `IDistributedEventHandler<TEvent>` |
| 传播范围 | 当前进程 | 取决于 Broker，默认仍在当前进程 |
| 在工作单元中的发布时机 | **事务提交之前** | **事务提交之后** |
| 默认投递路径 | 直接触发处理器 | 先落发件箱，后台异步投递 |
| 处理器抛异常的后果 | 事务回滚 | 处理器在收件箱后台跑，异常回不到调用方：重试到 `MaxInboxRetryCount` 后丢弃 |
| 典型用途 | 同进程解耦，处理器还要继续写库 | 通知另一个服务，可接受最终一致 |

选择很简单：**消费方在同一个进程里就用本地事件**，只有跨进程/跨服务才用分布式事件。分布式事件是异步的，调用返回时处理器多半还没跑。

::: tip 不装消息中间件也能用分布式事件
不引入任何 Broker 包时，`IDistributedEventBus` 的实现是 `LocalDistributedEventBus`：它照样走发件箱、收件箱和事件名映射，但最终把事件交回本地事件总线，事件不出进程。这让你可以先按分布式语义写代码，以后再接中间件。
:::

## 安装与启用

```bash
dotnet add package XiHan.Framework.EventBus
```

```csharp
[DependsOn(typeof(XiHanEventBusModule))]
public class MyModule : XiHanModule;
```

`XiHanEventBusModule` 在 `PreConfigureServices` 里调用 `services.AddXiHanEventBus(configuration)`，启用后容器里有：

| 服务 | 默认实现 | 说明 |
| --- | --- | --- |
| `ILocalEventBus` | `LocalEventBus` | 单例 |
| `IDistributedEventBus` | `LocalDistributedEventBus` | 单例，`TryRegister`，可被 Broker 实现替换 |
| `IEventOutbox` / `IEventInbox` | `InMemoryEventOutbox` / `InMemoryEventInbox` | 单例，`TryAddSingleton` |
| `IUnitOfWorkEventPublisher` | `UnitOfWorkEventPublisher` | `ReplaceServices`，负责在工作单元完成后发事件 |
| `IHostedService` | `EventBoxOutboxSenderHostedService`、`EventBoxInboxProcessorHostedService` | 发件箱发送与收件箱处理的轮询循环 |

同时会创建一个名为 `Default` 的发件箱配置和一个名为 `Default` 的收件箱配置，`DatabaseName` 均为 `"Default"`，默认都处于启用状态。

## 定义事件与处理器

事件就是一个普通类，只放数据：

```csharp
public class OrderCreatedEvent
{
    public long OrderId { get; set; }

    public long TenantId { get; set; }
}
```

处理器实现对应接口，方法名固定为 `HandleEventAsync`：

```csharp
public class OrderCreatedNotifier : ILocalEventHandler<OrderCreatedEvent>, ITransientDependency
{
    private readonly ILogger<OrderCreatedNotifier> _logger;

    public OrderCreatedNotifier(ILogger<OrderCreatedNotifier> logger)
    {
        _logger = logger;
    }

    public Task HandleEventAsync(OrderCreatedEvent eventData)
    {
        _logger.LogInformation("订单 {OrderId} 已创建", eventData.OrderId);
        return Task.CompletedTask;
    }
}
```

发布方注入 `ILocalEventBus`：

```csharp
public class OrderAppService : ApplicationServiceBase
{
    private readonly ILocalEventBus _localEventBus;
    private readonly IRepositoryBase<Order, long> _orders;

    public OrderAppService(ILocalEventBus localEventBus, IRepositoryBase<Order, long> orders)
    {
        _localEventBus = localEventBus;
        _orders = orders;
    }

    [UnitOfWork(true)]
    public async Task CreateAsync(OrderCreateDto input)
    {
        var order = await _orders.AddAsync(input.ToEntity());

        // onUnitOfWorkComplete 默认 true：先记进工作单元，提交时才真正发布
        await _localEventBus.PublishAsync(new OrderCreatedEvent { OrderId = order.Id });
    }
}
```

## 处理器必须登记进 Handlers

::: danger 这是本章最重要的一条
运行期的订阅**只有一个来源**：`LocalEventBus` 构造时对 `XiHanLocalEventBusOptions.Handlers` 逐个订阅，`LocalDistributedEventBus`（以及各 Broker 实现的基类 `BrokerDistributedEventBusBase`）构造时对 `XiHanDistributedEventBusOptions.Handlers` 逐个订阅。

**没进 `Handlers` 的处理器不会被订阅，事件发出去没人接，并且不会报任何错。**
:::

`AddXiHanEventBus` 里确实注册了一个自动收集钩子（`services.OnRegistered`），但它的覆盖面很窄：

- 这个钩子只在应用引入了 `XiHanCastleModule` 时才会被执行（动态代理扫描阶段）；
- 扫描时**跳过服务类型不是接口的注册**，而约定注册默认只把「名字是类名后缀的接口」暴露成服务类型——`ILocalEventHandler<TEvent>` 只有在处理器类名以 `LocalEventHandler` 结尾时才会被暴露。

所以别指望自动发现，**显式登记**：

```csharp
public static IServiceCollection AddLocalEventHandler<THandler>(this IServiceCollection services)
    where THandler : class, IEventHandler
{
    services.AddTransient<THandler>();
    services.Configure<XiHanLocalEventBusOptions>(options => options.Handlers.TryAdd<THandler>());
    return services;
}

// 分布式处理器同理，换成 XiHanDistributedEventBusOptions
public static IServiceCollection AddDistributedEventHandler<THandler>(this IServiceCollection services)
    where THandler : class, IEventHandler
{
    services.AddTransient<THandler>();
    services.Configure<XiHanDistributedEventBusOptions>(options => options.Handlers.TryAdd<THandler>());
    return services;
}
```

模块里成批登记：

```csharp
public override void ConfigureServices(ServiceConfigurationContext context)
{
    context.Services.AddLocalEventHandler<OrderCreatedNotifier>();
    context.Services.AddLocalEventHandler<OrderCreatedStockReserver>();
    context.Services.AddDistributedEventHandler<OrderShippedHandler>();
}
```

::: warning 处理器类本身必须能从容器解析
订阅时用的是 `IocEventHandlerFactory`：每次触发都新建一个服务作用域，按**具体处理器类型**从容器解析实例。类型没注册就抛 `InvalidOperationException`，提示无法从 IoC 容器解析事件处理器。用 `Transient` 生命周期，避免并发复用同一实例。
:::

## 发布时机与事件顺序

`PublishAsync` 并不总是立刻触发处理器：

| 场景 | 行为 |
| --- | --- |
| 有当前工作单元 + `onUnitOfWorkComplete: true`（默认） | 事件连同一个 `EventOrder` 序号记进工作单元，等工作单元完成时发布；工作单元回滚则不发 |
| 有当前工作单元 + `onUnitOfWorkComplete: false` | 立即走发布路径 |
| 没有当前工作单元 | 立即走发布路径，`onUnitOfWorkComplete` 不起作用 |

工作单元完成时的动作是有先后的：

```text
1. SaveChanges
2. 发布本地事件        ← 提交之前（处理器的写入落在同一事务里）
3. 提交事务
4. 发布分布式事件      ← 提交之后（避免事务回滚了事件照发）
```

**EventOrder** 是记进工作单元时由 `EventOrderGenerator.GetNext()` 取的进程内自增序号。工作单元完成时，本地事件与分布式事件**各自**按 `EventOrder` 升序发布：

- 同一批里的相对顺序 = 你调用 `PublishAsync` 的顺序；
- 本地事件和分布式事件是两批，分别排序，不会互相交错；
- 本地事件的处理器如果又发布了新事件，会在同一个循环里继续发布并再次 `SaveChanges`，直到没有新事件为止。

::: warning 两类事件的失败后果完全不同
本地事件在提交前发布，处理器抛异常会让工作单元回滚并把异常上抛。分布式事件在提交后发布，此时事务已经落库，投递失败只会把异常抛给调用方——数据不会跟着回滚。
:::

## 处理器执行顺序

同一个事件的多个本地处理器，按 `LocalEventHandlerOrderAttribute` 的 `Order` 升序执行：

```csharp
[LocalEventHandlerOrder(1)]
public class OrderCreatedStockReserver : ILocalEventHandler<OrderCreatedEvent> { /* … */ }

[LocalEventHandlerOrder(2)]
public class OrderCreatedNotifier : ILocalEventHandler<OrderCreatedEvent> { /* … */ }
```

- 未标注视为 `0`；
- `Order` 相同的处理器之间不保证稳定顺序；
- 每个处理器的异常都会被单独捕获收集，**所有处理器都会被执行完**，然后统一抛出：一个异常按原样重抛，多个则包成 `AggregateException`。

别把「顺序」当成事务：需要严格串行且失败即止的步骤，写在一个方法里，不要拆成多个处理器。

## 事件继承与租户上下文

**继承会被触发**：发布子类型时，订阅了其父类型或接口的处理器同样会被调用（判定用 `IsAssignableFrom`）。可以借此订阅一个事件基类统一处理，也要小心别意外命中。

**租户上下文按事件数据决定**：触发处理器前会切换到事件数据携带的租户——事件实现 `IMultiTenant`、实现 `IEventDataMayHaveTenantId`，或者干脆有一个 `long` / `string` 类型的 `TenantId` 属性，都能被识别；三者都没有时沿用当前租户。

::: warning 异步路径上没有请求上下文
发件箱、收件箱和 Broker 消费都在后台线程里跑，没有 HTTP 请求带来的租户信息。跨租户场景**必须把 TenantId 放进事件数据本身**，否则处理器拿到的是后台线程的默认租户。
:::

## 分布式事件的投递链路

`IDistributedEventBus.PublishAsync` 比本地多一个 `useOutbox` 参数，默认 `true`。默认实现（`LocalDistributedEventBus`）的完整链路：

```text
PublishAsync(useOutbox: true)
  → 有工作单元？ 记进工作单元，提交后再继续
  → 写入发件箱（IEventOutbox.EnqueueAsync）
  → EventBoxOutboxSenderHostedService 轮询取出，投递
  → 写入收件箱（按 messageId 去重）
  → EventBoxInboxProcessorHostedService 轮询取出，触发处理器
```

::: warning 发件箱只在有工作单元时生效
写发件箱要从当前工作单元的服务提供器里取实现，所以**没有当前工作单元时，`useOutbox: true` 会静默退化为直接投递**。想让事件走可靠投递，发布点必须处在工作单元内。
:::

收件箱负责幂等与重试：

| 环节 | 行为 |
| --- | --- |
| 去重 | 入队前按 `messageId` 查 `ExistsByMessageIdAsync`，重复消息直接视为已处理 |
| 成功 | `MarkAsProcessedAsync` |
| 失败 | `RetryLaterAsync`，延迟 `InboxRetryDelaySeconds` 秒后再来 |
| 连续失败达到 `MaxInboxRetryCount` | `MarkAsDiscardAsync` 丢弃并记警告日志 |
| 清理 | 每轮末尾 `DeleteOldEventsAsync`；内存实现只清理非等待中、且最后修改时间超过 7 天的记录 |

::: danger 默认事件盒在内存里
`InMemoryEventOutbox` / `InMemoryEventInbox` 用 `ConcurrentDictionary` 存储，进程一停全部丢失，重试计数也只在当前进程内累计。开发和单机够用，要真正的「不丢」必须换成持久化实现：把自己的 `IEventOutbox` / `IEventInbox` 注册进容器，并把 `ImplementationType` 指过去。
:::

```csharp
Configure<XiHanDistributedEventBusOptions>(options =>
{
    options.Outboxes.Configure(config =>
    {
        config.ImplementationType = typeof(MyDbEventOutbox);
        config.Selector = type => type.Namespace!.StartsWith("MyApp.Orders");
    });

    options.Inboxes.Configure(config =>
    {
        config.ImplementationType = typeof(MyDbEventInbox);
    });
});
```

无参的 `Configure` 配的是名为 `Default` 的那一个；用 `Configure("名字", ...)` 可以配多个命名事件盒，再用 `Selector` / `EventSelector` 把不同事件路由到不同事件盒。

## 事件命名

分布式事件靠**事件名**跨进程传递，接收端按事件名反查类型再反序列化。

```csharp
[EventName("MyApp.Orders.OrderShipped")]
public class OrderShippedEvent
{
    public long OrderId { get; set; }
}
```

- 不标注时事件名回退为 `Type.FullName`——重命名类或挪动命名空间就会破坏跨服务契约，所以**跨服务的事件一律显式标注**；
- 泛型事件用 `GenericEventNameAttribute`，按其唯一泛型参数生成名字，可配 `Prefix` / `Postfix`；泛型参数不止一个时抛 `XiHanException`；
- 事件名到类型的映射来自订阅登记和发件箱写入，**本实例没有订阅者的事件名，收到消息也会被直接忽略**。

## 三个 Broker 怎么选

装上对应包并 `[DependsOn]` 对应模块，`IDistributedEventBus` 的实现就被替换掉，业务代码一行不改。

| | RabbitMQ | Kafka | Redis Streams |
| --- | --- | --- | --- |
| 模块类 | `XiHanRabbitMQEventBusModule` | `XiHanKafkaEventBusModule` | `XiHanRedisEventBusModule` |
| 配置节 | `XiHan:EventBus:RabbitMQ` | `XiHan:EventBus:Kafka` | `XiHan:EventBus:Redis` |
| 消息模型 | direct 交换机 + 事件名做路由键，持久化消息 | 单主题 + 事件名做消息 Key | 单 Stream + 消费者组 |
| 竞争消费单位 | 队列 `QueueName` | 消费者组 `GroupId` | 消费者组 `ConsumerGroup` |
| 位点/确认 | 手动 Ack | `EnableAutoCommit = false`，手动提交 | 处理成功才 `XACK` |
| 失败处理 | Nack 且不重回队列，重试交给收件箱 | 不提交位点 | 滞留消息按 `ClaimMinIdleMilliseconds` 被其他消费者接管，投递次数超过 `MaxDeliveryCount` 转入死信 Stream |
| 额外依赖 | RabbitMQ | Kafka 集群 | 已有 Redis 即可 |

选型建议：

- **单进程 / 开发环境**：什么都不装，`LocalDistributedEventBus` 就够；
- **已经有 Redis、事件量不大**：选 Redis Streams，不引入新中间件，且自带死信和滞留接管；
- **需要成熟的路由、死信队列和运维台**：选 RabbitMQ；
- **高吞吐、要保留事件历史并支持重放**：选 Kafka。

::: warning 处理器要在初始化前登记
Broker 模块在 `OnApplicationInitializationAsync` 阶段调用 `InitializeAsync` 建连接、起消费者。RabbitMQ 在这一步按**当时已登记的事件名**绑定队列路由键——处理器没在服务配置阶段进 `Handlers`，这个事件的消息连收都收不到。这是「必须显式登记」的第二个后果。
:::

## 观测分布式事件

每次真正投递或收到分布式事件时，总线会额外发一条**本地事件**：`DistributedEventSent` / `DistributedEventReceived`，带 `Source`（`Direct` / `Outbox` / `Inbox`）、`EventName`、`EventData`。做统一日志、审计或指标时订阅它们即可，不必改每个业务处理器：

```csharp
public class DistributedEventLogger : ILocalEventHandler<DistributedEventSent>, ITransientDependency
{
    public Task HandleEventAsync(DistributedEventSent eventData)
    {
        // eventData.Source / eventData.EventName
        return Task.CompletedTask;
    }
}
```

这两条事件以 `onUnitOfWorkComplete: false` 发布，且触发过程中的异常会被吞掉，不影响主链路——反过来说，这里的处理器出错不会有任何提示。

## 配置

事件盒后台处理走配置节 `XiHan:EventBus:EventBoxes`：

| 配置项 | 默认值 | 含义 |
| --- | --- | --- |
| `PollingIntervalMilliseconds` | `2000` | 两个后台循环的轮询间隔，实际下限 200ms |
| `OutboxBatchSize` | `100` | 发件箱单批取数 |
| `InboxBatchSize` | `100` | 收件箱单批取数 |
| `MaxInboxRetryCount` | `5` | 收件箱最大重试次数，达到即丢弃 |
| `InboxRetryDelaySeconds` | `10` | 收件箱重试延迟秒数 |

```json
{
  "XiHan": {
    "EventBus": {
      "EventBoxes": {
        "PollingIntervalMilliseconds": 2000,
        "OutboxBatchSize": 100,
        "InboxBatchSize": 100,
        "MaxInboxRetryCount": 5,
        "InboxRetryDelaySeconds": 10
      }
    }
  }
}
```

事件盒实现、事件路由选择器、处理器选择器不走配置文件，用代码 `Configure<XiHanDistributedEventBusOptions>` 设置。各 Broker 的配置项见对应包文档。

## 常见问题

| 现象 | 原因 |
| --- | --- |
| 事件发出去没人处理，也不报错 | 处理器没进 `Handlers`；显式 `Configure` 登记 |
| 解析处理器时抛「无法从 IoC 容器解析事件处理器」 | 处理器进了 `Handlers` 但具体类型没注册进容器 |
| 工作单元回滚了事件却发了 | 发布时传了 `onUnitOfWorkComplete: false`，或者当时根本没有工作单元 |
| 分布式事件迟迟不到 | 走发件箱本就是异步，间隔为 `PollingIntervalMilliseconds`；默认配置还要再过一次收件箱轮询 |
| `useOutbox: true` 但发件箱里查不到记录 | 发布点没有当前工作单元，静默退化成了直接投递 |
| 进程重启后未投递的事件消失 | 默认事件盒是内存实现，换持久化实现 |
| 换了 Broker 后某个事件收不到 | 处理器没在初始化前登记（RabbitMQ 按已登记事件名绑队列），或两端事件名不一致 |
| 处理器里租户不对 | 事件数据没带 `TenantId`，后台线程拿不到请求上下文 |
| 收件箱事件被丢弃 | 连续失败达到 `MaxInboxRetryCount`，已 `MarkAsDiscardAsync`，看警告日志 |
| 一个处理器出错，整个发布调用抛异常 | 设计如此：所有处理器都会执行完，异常统一抛出（多个时为 `AggregateException`） |

## 下一步

- [工作单元与事务](./uow)：本地/分布式事件的发布时序就定在这里
- [多租户](./multi-tenancy)：处理器里的租户上下文从哪来
- [定时任务与后台作业](../packages/tasks)：不需要「发生了什么」语义时的另一种异步
- [EventBus 包](../packages/eventbus)：完整 API 与全部类型清单
- [EventBus.RabbitMQ](../packages/eventbus-rabbitmq)、[EventBus.Kafka](../packages/eventbus-kafka)、[EventBus.Redis](../packages/eventbus-redis)：各 Broker 的完整配置项
