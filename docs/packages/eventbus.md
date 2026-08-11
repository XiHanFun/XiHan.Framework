# XiHan.Framework.EventBus

> 事件总线实现：进程内本地事件总线 + 分布式事件总线（Outbox/Inbox 可靠投递），落地 EventBus.Abstractions 的全部契约。

- **NuGet**：`XiHan.Framework.EventBus`
- **模块类**：`XiHanEventBusModule`
- **所在层**：基础设施层
- **关键依赖**：仅 .NET 原生 + 框架内部依赖（Abstractions / Messaging / Uow / DistributedIds / Core）

## 概述

这个包是事件总线的具体实现，落地了 [XiHan.Framework.EventBus.Abstractions](./eventbus-abstractions) 里的接口。它提供进程内的本地事件总线 `LocalEventBus`，以及带可靠投递能力的分布式事件总线 `LocalDistributedEventBus`（发件箱 Outbox / 收件箱 Inbox 模式，默认内存实现）。你用它把业务解耦成"发布事件 → 一个或多个处理器响应"，并可让事件跟随工作单元（UoW）提交后再真正发布。核心亮点是**处理器自动登记**：只要你的处理器类实现了 `ILocalEventHandler<>` / `IDistributedEventHandler<>` 并走框架的模块注册管线，就会被自动订阅，无需手写 `Subscribe`。

## 何时使用

- 一处动作要触发多处副作用（如"下单成功"后同时发通知、扣库存、写日志），用事件解耦
- 希望事件在数据库事务/工作单元提交后再发布，避免"事务回滚了但事件已发出去"
- 需要分布式事件的可靠投递：先落发件箱、后台再异步发送；消费端用收件箱去重与失败重试
- 需要控制同一事件多个处理器的执行顺序（`LocalEventHandlerOrderAttribute`）

## 安装与启用

```bash
dotnet add package XiHan.Framework.EventBus
```

```csharp
[DependsOn(typeof(XiHanEventBusModule))]
public class MyModule : XiHanModule { }
```

`XiHanEventBusModule` 在 `PreConfigureServices` 里调用 `services.AddXiHanEventBus(config)` 完成注册，启用后你会得到：

- `ILocalEventBus` → `LocalEventBus`（单例，通过 `[ExposeServices]` 暴露）
- `IDistributedEventBus` → `LocalDistributedEventBus`（单例，`[Dependency(TryRegister = true)]`，可被替换）
- `IEventOutbox` / `IEventInbox` → 默认 `InMemoryEventOutbox` / `InMemoryEventInbox`（单例，`TryAddSingleton`）
- 两个后台托管服务：`EventBoxOutboxSenderHostedService`（发送发件箱）、`EventBoxInboxProcessorHostedService`（处理收件箱）
- `IUnitOfWorkEventPublisher` → `UnitOfWorkEventPublisher`（`[Dependency(ReplaceServices = true)]`，让事件在 UoW 完成后发布）
- 通过 `OnRegistered` 钩子把所有事件处理器类型自动收集进 `XiHanLocalEventBusOptions.Handlers` / `XiHanDistributedEventBusOptions.Handlers`

## 工作原理

### 处理器自动登记（关键机制）

`AddXiHanEventBus` 内部调用 `AddEventHandlers`，注册了 `services.OnRegistered(...)` 钩子：**每当有服务被注册进容器**，钩子检查其 `ImplementationType` 是否 `IsAssignableToGeneric(typeof(ILocalEventHandler<>))` 或 `IDistributedEventHandler<>`，命中就收集到本地/分布式处理器列表；随后通过 `services.Configure<XiHanLocalEventBusOptions>` / `Configure<XiHanDistributedEventBusOptions>` 把这些类型 `AddIfNotContains` 进各自的 `Handlers`（`ITypeList<IEventHandler>`）。

到运行期：`LocalEventBus` 构造时对 `Options.Handlers` 逐个 `SubscribeHandlers`；`LocalDistributedEventBus` 构造时对 `Options.Handlers` 逐个 `Subscribe`（按接口泛型参数取事件类型，用 `IocEventHandlerFactory` 从 DI 解析处理器实例）。**这条链路是订阅的唯一来源**——处理器必须进入 `Options.Handlers` 才会被订阅（详见「注意事项」）。

### 发布路径

- **本地**：`PublishAsync(eventData, onUnitOfWorkComplete)` → 若有当前 UoW 且 `onUnitOfWorkComplete = true`，事件记录进 UoW（`AddOrReplaceLocalEvent`），UoW 完成后由 `UnitOfWorkEventPublisher` 发布；否则立即 `TriggerHandlersAsync`，按 `LocalEventHandlerOrderAttribute.Order` 升序调用各处理器。
- **分布式**：`PublishAsync(eventData, onUnitOfWorkComplete, useOutbox)` → 有 UoW 则记录进 UoW（`AddOrReplaceDistributedEvent`）；否则若 `useOutbox` 则先 `AddToOutboxAsync` 落发件箱并返回，由后台服务异步投递；不走 Outbox 时直接投递并转交 `LocalEventBus`。

### 发布/接收观测钩子

分布式总线每次真正投递或收到事件时（直投、走发件箱、走收件箱），都会顺带把一条 `DistributedEventSent` / `DistributedEventReceived` 作为**本地事件**再发布一次（`TriggerDistributedEventSentAsync` / `TriggerDistributedEventReceivedAsync`，`onUnitOfWorkComplete: false`，异常被吞掉不影响主流程）。想做分布式事件的统一日志、审计或指标采集，实现 `ILocalEventHandler<DistributedEventSent>` / `ILocalEventHandler<DistributedEventReceived>` 订阅即可拿到 `Source`（`Direct` / `Outbox` / `Inbox`）、`EventName`、`EventData`，无需侵入每个业务处理器。

### 发件箱/收件箱后台循环

- `EventBoxOutboxSenderHostedService`：循环拉取每个已启用发件箱的等待事件（`GetWaitingEventsAsync`，批量 `OutboxBatchSize`），调用 `ISupportsEventBoxes.PublishManyFromOutboxAsync` 投递，成功后 `DeleteManyAsync` 清理。
- `EventBoxInboxProcessorHostedService`：循环拉取每个已启用收件箱的等待事件，逐个 `ProcessFromInboxAsync` 处理；成功 `MarkAsProcessedAsync`，失败进重试计数——达到 `MaxInboxRetryCount` 则 `MarkAsDiscardAsync` 丢弃，否则 `RetryLaterAsync` 延后（`InboxRetryDelaySeconds` 秒后）；每轮末尾 `DeleteOldEventsAsync` 清理过期（默认实现 `InMemoryEventInbox` 只清理非等待中、且最后修改时间超过 7 天的记录）。
- 两者轮询间隔 = `PollingIntervalMilliseconds`（下限 200ms）。

## 核心能力

- **本地事件总线** `LocalEventBus`（`ILocalEventBus`，单例）：进程内发布/订阅，接入工作单元与当前租户上下文；处理器按 `Order` 排序执行；支持事件继承（父类型处理器也会被基于子类型的发布触发）
- **分布式事件总线** `LocalDistributedEventBus`（`IDistributedEventBus`，单例，`TryRegister`）：在本地事件总线之上叠加 Outbox/Inbox 与事件名映射，可被真正的 MQ 实现替换；另有空实现 `NullDistributedEventBus`（`Instance` 单例）
- **Outbox / Inbox 模式**：默认内存实现 `InMemoryEventOutbox` / `InMemoryEventInbox`，可替换为持久化实现；发件箱保证事件不丢，收件箱靠 `MessageId` 去重、并做失败重试/丢弃
- **后台托管服务**：`EventBoxOutboxSenderHostedService`、`EventBoxInboxProcessorHostedService`（均为 `BackgroundService`，以 `TryAddEnumerable` 注册为 `IHostedService`）
- **处理器工厂体系**：`IocEventHandlerFactory`（从 DI 解析，支持依赖注入）、`TransientEventHandlerFactory` / `TransientEventHandlerFactory<THandler>`（每次新建瞬时实例）、`SingleInstanceHandlerFactory`（复用同一实例）、`ActionEventHandler<TEvent>`（把委托包装成处理器）
- **工作单元集成** `UnitOfWorkEventPublisher`：实现 `IUnitOfWorkEventPublisher`，让本地/分布式事件跟随 UoW 完成后发布
- **事件命名**：`EventNameAttribute`（实现 `IEventNameProvider`）/ `GenericEventNameAttribute` 为分布式事件指定名称，未标注时回退到 `Type.FullName`

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `LocalEventBus` | 本地事件总线实现（单例）；注入 `ILocalEventBus` 使用；`Subscribe/Unsubscribe/UnsubscribeAll` + `GetEventHandlerFactories` |
| `LocalDistributedEventBus` | 分布式事件总线实现（单例，可替换）；在 `LocalEventBus` 之上做 Outbox/Inbox 与事件名→类型映射 |
| `BrokerDistributedEventBusBase` | 面向真实消息中间件的分布式事件总线基类；抽取三个 Broker Provider 的公共逻辑（事件类型映射、订阅委派本地总线、发件箱投递、入站幂等/重试、Consumer Span）。Provider 只需实现 `InitializeAsync` 与 `PublishToBrokerAsync` |
| `NullDistributedEventBus` | 分布式事件总线空实现（`Instance` 单例），所有发布/订阅均为空操作 |
| `NullLocalEventBus` | 本地事件总线空实现（`Instance` 单例） |
| `XiHanLocalEventBusOptions` | 本地事件总线选项：`ITypeList<IEventHandler> Handlers`（处理器类型列表） |
| `XiHanDistributedEventBusOptions` | 分布式事件总线选项：`Handlers`、`OutboxConfigDictionary Outboxes`、`InboxConfigDictionary Inboxes` |
| `EventBoxProcessingOptions` | 发件箱/收件箱后台处理选项；配置节 `XiHan:EventBus:EventBoxes` |
| `InMemoryEventOutbox` / `InMemoryEventInbox` | 默认内存事件盒实现（`ConcurrentDictionary` 存储） |
| `EventNameAttribute` | 为分布式事件类型指定事件名；静态 `GetNameOrDefault<TEvent>()` / `GetNameOrDefault(Type)` 取名或回退 `FullName` |
| `GenericEventNameAttribute` | 为泛型事件类型（如 `EntityCreatedEventData<TEntity>`）按其唯一泛型参数动态生成事件名，可配 `Prefix` / `Postfix`；泛型参数不唯一时抛 `XiHanException` |
| `DistributedEventSent` / `DistributedEventReceived` | 分布式事件发送/接收的观测型本地事件（`Source` 取 `Direct`/`Outbox`/`Inbox`），发布/接收路径上自动触发，用于旁路日志、审计、指标采集 |
| `IocEventHandlerFactory` / `TransientEventHandlerFactory` / `SingleInstanceHandlerFactory` | 三种处理器工厂 |
| `UnitOfWorkEventPublisher` | 工作单元事件发布者 |

## 配置

### 事件盒后台处理 `EventBoxProcessingOptions`

- **配置节名**：`XiHan:EventBus:EventBoxes`（来自 `EventBoxProcessingOptions.SectionName`）

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `PollingIntervalMilliseconds` | `int` | `2000` | 后台轮询间隔（毫秒，实际下限 200ms） |
| `OutboxBatchSize` | `int` | `100` | 发件箱单批处理数量 |
| `InboxBatchSize` | `int` | `100` | 收件箱单批处理数量 |
| `MaxInboxRetryCount` | `int` | `5` | 收件箱最大重试次数，超过则丢弃 |
| `InboxRetryDelaySeconds` | `int` | `10` | 收件箱重试延迟秒数 |

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

### 事件盒实现与选择器 `XiHanDistributedEventBusOptions`

发件箱/收件箱默认名 `"Default"`、默认 `DatabaseName = "Default"`、默认实现 `InMemoryEventOutbox` / `InMemoryEventInbox`（`AddXiHanEventBus` 里兜底填充）。要替换实现或做事件筛选，用 `Configure`：

```csharp
Configure<XiHanDistributedEventBusOptions>(options =>
{
    options.Outboxes.Configure(config =>
    {
        config.ImplementationType = typeof(MyDbEventOutbox); // 替换持久化实现
        config.Selector = type => type.Namespace!.StartsWith("MyApp.Orders"); // 只有这些事件走此发件箱
    });
    options.Inboxes.Configure(config =>
    {
        config.ImplementationType = typeof(MyDbEventInbox);
    });
});
```

## 使用示例

### 1. 定义事件 + 处理器 + 发布（完整链路）

```csharp
// 1) 事件
public class OrderCreatedEvent
{
    public long OrderId { get; set; }
}

// 2) 处理器：实现 ILocalEventHandler<> 即被框架自动登记与订阅，无需手动 Subscribe
public class OrderCreatedNotifier : ILocalEventHandler<OrderCreatedEvent>, ITransientDependency
{
    private readonly INotifier _notifier;
    public OrderCreatedNotifier(INotifier notifier) => _notifier = notifier;

    public async Task HandleEventAsync(OrderCreatedEvent eventData)
    {
        await _notifier.SendAsync($"订单 {eventData.OrderId} 已创建");
    }
}

// 3) 发布（注入 ILocalEventBus）
public class OrderAppService
{
    private readonly ILocalEventBus _localEventBus;
    public OrderAppService(ILocalEventBus localEventBus) => _localEventBus = localEventBus;

    public async Task CreateAsync()
    {
        // onUnitOfWorkComplete 默认 true：在当前工作单元完成后再发布
        await _localEventBus.PublishAsync(new OrderCreatedEvent { OrderId = 1001 });
    }
}
```

### 2. 分布式事件（走 Outbox 可靠投递）

```csharp
[EventName("MyApp.Orders.OrderShipped")] // 指定稳定的事件名（跨服务）
public class OrderShippedEvent
{
    public long OrderId { get; set; }
}

public class OrderShippedHandler : IDistributedEventHandler<OrderShippedEvent>, ITransientDependency
{
    public Task HandleEventAsync(OrderShippedEvent eventData) => Task.CompletedTask;
}

// 发布：useOutbox 默认 true —— 先落发件箱，后台服务异步投递
await _distributedEventBus.PublishAsync(new OrderShippedEvent { OrderId = 1001 });
```

## 扩展点 / 自定义

- **替换分布式事件总线为真正的 MQ**：框架已提供三个开箱即用的 Broker 提供程序——[EventBus.RabbitMQ](./eventbus-rabbitmq)、[EventBus.Kafka](./eventbus-kafka)、[EventBus.Redis](./eventbus-redis)，装上并 `[DependsOn]` 对应模块即可，业务代码无需改动。原理：默认 `LocalDistributedEventBus` 用 `[Dependency(TryRegister = true)]` 注册，Provider 走常规注册并在其后追加，DI 解析取最后一条注册，故 Provider 胜出。自己实现 `IDistributedEventBus` 同理。
- **替换发件箱/收件箱为持久化实现**：默认内存实现用 `TryAddSingleton` 注册；把你的 `IEventOutbox` / `IEventInbox`（如落 SqlSugar/DB）注册进容器，并在 `XiHanDistributedEventBusOptions.Outboxes/Inboxes` 的 `ImplementationType` 指向它即可。
- **事件命名**：跨服务事件建议加 `[EventName("稳定名字")]`，避免依赖 `Type.FullName`（重构改名会破坏契约）。
- **事件筛选/多事件盒**：用 `OutboxConfigDictionary.Configure("名字", ...)` 配多个命名事件盒，配 `Selector` 做事件类型路由。

## 注意事项与最佳实践

- **本地/分布式处理器必须进入 `Options.Handlers` 才会被订阅（最重要）**。运行期 `LocalEventBus` 构造时对 `Options.Handlers` 逐个 `SubscribeHandlers`、`LocalDistributedEventBus` 逐个 `Subscribe`；而 `Handlers` 的填充来自 `AddXiHanEventBus` 里 `services.OnRegistered(...)` 钩子——它在服务注册阶段识别实现了 `ILocalEventHandler<>` / `IDistributedEventHandler<>` 的类型并 `AddIfNotContains` 进 `Handlers`。这意味着处理器要**走框架的模块注册管线**才会被登记（例如实现 `ITransientDependency`/被约定扫描注册，或以能触发 `OnRegistered` 的方式加入 `IServiceCollection`）。**若绕过这条链路——例如手动裸 `AddTransient(具体处理器类型)` 而未被 `OnRegistered` 捕获，或在钩子注册之后才加入——它不会进入 `Handlers`，也就不会被订阅，事件会静默无人处理、且不报错。** 排查"处理器没触发"时，第一步就是确认它有没有进 `Handlers`。
- **发布默认跟随工作单元**：有当前 UoW 时，事件先记录进 UoW，直到 UoW 成功提交才由 `UnitOfWorkEventPublisher` 真正发布；UoW 回滚则事件不发。要"立即发布、不等 UoW"，显式传 `onUnitOfWorkComplete: false`。
- **分布式默认走 Outbox**：`useOutbox` 默认 `true`，发布是异步的（先入发件箱，后台再投递），不要期望调用返回后处理器已执行完。要同步直投可传 `useOutbox: false`。
- **默认实现是内存版**：`InMemoryEventOutbox` / `InMemoryEventInbox` 进程重启即丢，仅适合单机/开发；生产的"不丢/去重"承诺需替换为持久化实现。
- **处理器执行顺序**：同一事件的多个本地处理器按 `LocalEventHandlerOrderAttribute.Order` 升序执行，未标注默认 0；顺序相同则不保证稳定顺序。
- **事件继承会被触发**：基于子类型发布时，订阅了其父类型/接口的处理器也会被触发（`ShouldTriggerEventForHandler` 用 `IsAssignableFrom` 判断）。

## 依赖模块

- [XiHan.Framework.EventBus.Abstractions](./eventbus-abstractions)（接口契约）
- [XiHan.Framework.Messaging](./messaging)
- [XiHan.Framework.Uow](./uow)（工作单元集成）
- [XiHan.Framework.DistributedIds](./distributed-ids)（事件/关联 Id 生成）
- [XiHan.Framework.Core](./core)

## 相关模块

- [XiHan.Framework.EventBus.Abstractions](./eventbus-abstractions)
- [XiHan.Framework.EventBus.RabbitMQ](./eventbus-rabbitmq)（分布式事件走 RabbitMQ）
- [XiHan.Framework.EventBus.Kafka](./eventbus-kafka)（分布式事件走 Kafka）
- [XiHan.Framework.EventBus.Redis](./eventbus-redis)（分布式事件走 Redis Streams）
- [XiHan.Framework.Messaging](./messaging)
- [XiHan.Framework.Uow](./uow)
