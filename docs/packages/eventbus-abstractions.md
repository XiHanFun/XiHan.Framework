# XiHan.Framework.EventBus.Abstractions

> 事件总线抽象包：只定义发布/订阅契约、本地与分布式处理器接口、可靠投递（Outbox/Inbox）契约，不含任何实现。

- **NuGet**：`XiHan.Framework.EventBus.Abstractions`
- **模块类**：`XiHanEventBusAbstractionsModule`（`[DependsOn(typeof(XiHanObjectMappingModule))]`）
- **所在层**：基础设施层
- **关键依赖**：仅 .NET 原生 + 框架内部依赖（Core / ObjectMapping / MultiTenancy.Abstractions / Timing）

## 概述

这个包只定义事件总线的接口契约，不含具体实现。它规定了"事件怎么发布、处理器怎么订阅、处理器长什么样"，把本地事件（进程内解耦）和分布式事件（跨进程/跨服务）的契约都摆出来；对分布式事件，还定义了 Outbox（发件箱）/ Inbox（收件箱）这套可靠投递契约。真正的实现由 [XiHan.Framework.EventBus](./eventbus) 提供。业务代码与实现层都面向本包接口编程，从而彼此解耦。

## 何时使用

- 你要在领域层/应用层声明事件与事件处理器，但不想耦合具体的事件总线实现
- 你要区分本地事件（同进程解耦）与分布式事件（跨服务通信）两种订阅契约
- 你要为分布式事件定义可靠投递的发件箱/收件箱（Outbox/Inbox）契约
- 一般不单独引用本包，而是引用实现包 `XiHan.Framework.EventBus`（它已依赖本包）

## 安装与启用

```bash
dotnet add package XiHan.Framework.EventBus.Abstractions
```

```csharp
[DependsOn(typeof(XiHanEventBusAbstractionsModule))]
public class MyModule : XiHanModule { }
```

`XiHanEventBusAbstractionsModule.ConfigureServices` 本身不注册任何服务（只取了 `IConfiguration`），它只承担"引入抽象契约与依赖模块（ObjectMapping）"的角色；真正的服务注册在实现包完成。

## 核心能力

- **统一事件总线契约** `IEventBus`：定义发布（`PublishAsync`）、订阅（多个 `Subscribe` 重载）、注销（多个 `Unsubscribe` / `UnsubscribeAll` 重载）
- **本地事件契约**：`ILocalEventBus`（进程内事件总线，继承 `IEventBus`）+ `ILocalEventHandler<TEvent>`（本地事件处理器）
- **分布式事件契约**：`IDistributedEventBus`（继承 `IEventBus`，发布额外带 `useOutbox` 参数）+ `IDistributedEventHandler<TEvent>`
- **处理器基础接口** `IEventHandler`：所有处理器的间接基接口，实际需实现 `ILocalEventHandler<>` 或 `IDistributedEventHandler<>`，不要直接实现它
- **可靠投递契约**：`IEventOutbox`（发件箱）、`IEventInbox`（收件箱），以及出/入站事件信息 `IOutgoingEventInfo` / `IIncomingEventInfo`（及其实现 `OutgoingEventInfo` / `IncomingEventInfo`）
- **事件盒配置**：`OutboxConfig` / `InboxConfig` 及其字典 `OutboxConfigDictionary` / `InboxConfigDictionary`，`ISupportsEventBoxes` 声明对象支持事件盒机制
- **处理器工厂与调用**：`IEventHandlerFactory`、`IEventHandlerInvoker`、`EventTypeWithEventHandlerFactories` 等抽象，供实现层构建处理管道
- **事件命名与租户**：`IEventNameProvider`（事件名提供器）、`IEventDataMayHaveTenantId`（事件可能携带租户）
- **泛型参数可继承**：`IEventDataWithInheritableGenericArgument`，让 `EventData<TEntity>` 这类泛型事件在 `TEntity` 有基类时，连带触发 `EventData<TEntity 的基类>`
- **分布式事件遥测钩子**：`DistributedEventSent` / `DistributedEventReceived`（配合 `DistributedEventSource` 来源枚举），实现包在直接发送/直接接收/走 Outbox/走 Inbox 时会把这两类"元事件"发布到本地事件总线，供观测代码订阅
- **顺序控制**：`LocalEventHandlerOrderAttribute` 标注本地处理器执行顺序

## 主要 API / 类型

### 根契约 `IEventBus`

| 方法 | 说明 |
| --- | --- |
| `Task PublishAsync<TEvent>(TEvent eventData, bool onUnitOfWorkComplete = true) where TEvent : class` | 触发事件；`onUnitOfWorkComplete = true` 时在当前工作单元完成后再发布（若存在 UoW） |
| `Task PublishAsync(Type eventType, object eventData, bool onUnitOfWorkComplete = true)` | 非泛型发布 |
| `IDisposable Subscribe<TEvent>(Func<TEvent, Task> action)` | 用委托订阅 |
| `IDisposable Subscribe<TEvent, THandler>() where THandler : IEventHandler, new()` | 用处理器类型订阅（每次新建实例） |
| `IDisposable Subscribe(Type eventType, IEventHandler handler)` | 用同一处理器实例订阅 |
| `IDisposable Subscribe<TEvent>(IEventHandlerFactory factory)` / `Subscribe(Type, IEventHandlerFactory)` | 用工厂订阅（管理处理器生命周期） |
| `void Unsubscribe<TEvent>(Func<TEvent, Task>)` / `Unsubscribe<TEvent>(ILocalEventHandler<TEvent>)` / `Unsubscribe(Type, IEventHandler)` / `Unsubscribe<TEvent>(IEventHandlerFactory)` / `Unsubscribe(Type, IEventHandlerFactory)` | 注销指定订阅 |
| `void UnsubscribeAll<TEvent>()` / `UnsubscribeAll(Type)` | 注销某事件类型的所有处理器 |

订阅方法都返回 `IDisposable`，`Dispose()` 即取消该次订阅。

### 本地 `ILocalEventBus : IEventBus`

| 方法 | 说明 |
| --- | --- |
| `IDisposable Subscribe<TEvent>(ILocalEventHandler<TEvent> handler)` | 用给定的本地处理器实例订阅（所有事件复用同一实例） |
| `List<EventTypeWithEventHandlerFactories> GetEventHandlerFactories(Type eventType)` | 取某事件类型对应的处理器工厂列表 |

### 本地处理器 `ILocalEventHandler<in TEvent> : IEventHandler`

```csharp
Task HandleEventAsync(TEvent eventData);
```

### 分布式 `IDistributedEventBus : IEventBus`

| 方法 | 说明 |
| --- | --- |
| `IDisposable Subscribe<TEvent>(IDistributedEventHandler<TEvent> handler)` | 用给定的分布式处理器实例订阅 |
| `Task PublishAsync<TEvent>(TEvent eventData, bool onUnitOfWorkComplete = true, bool useOutbox = true)` | 发布分布式事件；`useOutbox = true` 时先落发件箱再由后台可靠投递 |
| `Task PublishAsync(Type eventType, object eventData, bool onUnitOfWorkComplete = true, bool useOutbox = true)` | 非泛型分布式发布 |

分布式处理器 `IDistributedEventHandler<in TEvent> : IEventHandler` 同样定义 `Task HandleEventAsync(TEvent eventData)`。

### 处理器基础与工厂

| 类型 | 说明 |
| --- | --- |
| `IEventHandler` | 所有处理器的间接基接口（空接口，勿直接实现） |
| `IEventHandlerFactory` | 处理器工厂：`IEventHandlerDisposeWrapper GetHandler()` + `bool IsInFactories(List<IEventHandlerFactory>)` |
| `IEventHandlerDisposeWrapper` | 处理器释放包装（继承 `IDisposable`）：`IEventHandler EventHandler { get; }`，`Dispose()` 负责释放该次 `GetHandler()` 取得的处理器实例 |
| `IEventHandlerInvoker` | 处理器调用器：`Task InvokeAsync(IEventHandler eventHandler, object eventData, Type eventType)` |
| `EventTypeWithEventHandlerFactories` | 事件类型与其处理器工厂列表的配对（`EventType` + `EventHandlerFactories`） |
| `LocalEventHandlerOrderAttribute` | `[AttributeUsage(Class)]`，构造入参 `int order`，暴露 `Order` 属性；标注本地处理器执行顺序（小的先执行，默认 0） |

### 可靠投递（Outbox / Inbox）契约

| 类型 | 关键方法 |
| --- | --- |
| `IEventOutbox` | `Task EnqueueAsync(OutgoingEventInfo)`、`Task<List<OutgoingEventInfo>> GetWaitingEventsAsync(int maxCount, Expression<Func<IOutgoingEventInfo,bool>>? filter = null, CancellationToken)`、`Task DeleteAsync(Guid)`、`Task DeleteManyAsync(IEnumerable<Guid>)` |
| `IEventInbox` | `Task EnqueueAsync(IncomingEventInfo)`、`Task<List<IncomingEventInfo>> GetWaitingEventsAsync(...)`、`Task MarkAsProcessedAsync(Guid)`、`Task RetryLaterAsync(Guid id, int retryCount, DateTime? nextRetryTime)`、`Task MarkAsDiscardAsync(Guid)`、`Task<bool> ExistsByMessageIdAsync(string messageId)`、`Task DeleteOldEventsAsync()` |
| `IOutgoingEventInfo` | 出站事件信息：`Guid Id`、`string EventName`、`byte[] EventData`、`DateTime CreatedTime`（继承 `IHasExtraProperties`） |
| `IIncomingEventInfo` | 入站事件信息：在出站字段基础上多一个 `string MessageId`（用于去重） |
| `OutgoingEventInfo` / `IncomingEventInfo` | 上述接口的具体实现类；构造函数校验事件名非空且不超过 `MaxEventNameLength`（静态属性，默认 256）；均提供 `SetCorrelationId(string)` / `GetCorrelationId()`，内部借助 `ExtraProperties` 与 `EventBusConsts.CorrelationIdHeaderName` 读写关联标识 |
| `ISupportsEventBoxes` | 声明对象（通常是分布式事件总线）支持事件盒：`PublishFromOutboxAsync` / `PublishManyFromOutboxAsync` / `ProcessFromInboxAsync` |

### 事件盒配置

| 类型 | 说明 |
| --- | --- |
| `OutboxConfig` | 发件箱配置：`Name`（构造入参，非空校验）、`DatabaseName`、`ImplementationType`（实现类型）、`Func<Type,bool>? Selector`（事件筛选）、`bool IsSendingEnabled = true` |
| `InboxConfig` | 收件箱配置：`Name`、`DatabaseName`、`ImplementationType`、`Func<Type,bool>? EventSelector`、`Func<Type,bool>? HandlerSelector`、`bool IsProcessingEnabled = true` |
| `OutboxConfigDictionary` / `InboxConfigDictionary` | 以名称为键的配置字典（`Dictionary<string, XxxConfig>`），提供 `Configure(Action<XxxConfig>)`（默认名 `"Default"`）与 `Configure(string name, Action<XxxConfig>)` 便捷方法 |

### 分布式事件遥测（发送 / 接收）钩子

| 类型 | 说明 |
| --- | --- |
| `DistributedEventSent` | 分布式事件发送信息：`DistributedEventSource Source`、`string EventName`、`object EventData` |
| `DistributedEventReceived` | 分布式事件接收信息：字段与 `DistributedEventSent` 相同 |
| `DistributedEventSource` | 事件来源枚举：`Direct`（直接发送/接收）、`Inbox`（经收件箱）、`Outbox`（经发件箱） |

这三个类型本身只是数据载体，不含收发逻辑：`XiHan.Framework.EventBus` 实现包的 `DistributedEventBusBase` 在每次直接发送/直接接收、以及经由 Outbox/Inbox 处理事件时，会分别构造 `DistributedEventSent` / `DistributedEventReceived` 并调用 `TriggerDistributedEventSentAsync` / `TriggerDistributedEventReceivedAsync`——两者内部都是把这个"元事件"以 `onUnitOfWorkComplete: false` 发布到**本地事件总线**（并吞掉异常，不影响主流程）。因此只要在业务代码里实现 `ILocalEventHandler<DistributedEventSent>` / `ILocalEventHandler<DistributedEventReceived>`，即可零侵入地观测分布式事件的收发轨迹（写日志、打点、追踪来源是 Outbox 还是直接发送等），无需修改具体的分布式事件处理器。

### 泛型可继承事件数据

| 类型 | 说明 |
| --- | --- |
| `IEventDataWithInheritableGenericArgument` | `object[] GetConstructorArgs()`；用于形如 `EventData<TEntity>` 的单泛型参数事件类。若该泛型事件类实现此接口，当发布 `EventData<Student>`（`Student` 继承自 `Person`）时，实现包会额外用 `GetConstructorArgs()` 返回的构造参数构造并发布一份 `EventData<Person>`，从而让订阅基类事件的处理器也能收到 |

### 其它辅助契约

| 类型 | 说明 |
| --- | --- |
| `IEventNameProvider` | `string GetName(Type eventType)`；实现包的 `EventNameAttribute` 实现此接口，用于给分布式事件命名 |
| `IEventDataMayHaveTenantId` | `bool IsMultiTenant(out long? tenantId)`；事件数据可选携带租户上下文 |
| `EventBusConsts` | 常量：`CorrelationIdHeaderName = "X-Correlation-Id"`（关联标识请求头名） |

## 使用示例

定义事件与本地处理器（只面向抽象编程，具体实现由 EventBus 包提供）：

```csharp
// 事件（普通引用类型即可）
public class OrderCreatedEvent
{
    public long OrderId { get; set; }
}

// 本地事件处理器（框架注册管线会自动登记，无需手动订阅）
public class OrderCreatedHandler : ILocalEventHandler<OrderCreatedEvent>
{
    public Task HandleEventAsync(OrderCreatedEvent eventData)
    {
        // 处理逻辑
        return Task.CompletedTask;
    }
}
```

控制多个处理器的执行顺序：

```csharp
[LocalEventHandlerOrder(10)]
public class AuditHandler : ILocalEventHandler<OrderCreatedEvent> { /* ... */ }

[LocalEventHandlerOrder(20)]
public class NotifyHandler : ILocalEventHandler<OrderCreatedEvent> { /* ... */ }
// Order 越小越先执行；未标注默认 0
```

## 注意事项与最佳实践

- **不要直接实现 `IEventHandler`**：它只是间接基接口，实现 `ILocalEventHandler<TEvent>` 或 `IDistributedEventHandler<TEvent>`。
- **发布默认跟随工作单元**：`PublishAsync` 的 `onUnitOfWorkComplete` 默认 `true`，事件会在当前 UoW 提交后才真正发布，避免"事务回滚了但事件已发出"。无 UoW 时立即发布。
- **分布式发布默认走 Outbox**：`IDistributedEventBus.PublishAsync` 的 `useOutbox` 默认 `true`，先落发件箱、后台再投递。
- 本包只是契约，"处理器怎样被登记与订阅"由实现包决定，见 [XiHan.Framework.EventBus](./eventbus) 的注意事项。

## 依赖模块

- [XiHan.Framework.Core](./core)
- [XiHan.Framework.ObjectMapping](./objectmapping)（模块 `DependsOn` 依赖；`IHasExtraProperties` 来自此包）
- [XiHan.Framework.MultiTenancy.Abstractions](./multitenancy-abstractions)（事件可携带租户上下文）
- [XiHan.Framework.Timing](./timing)

## 相关模块

- [XiHan.Framework.EventBus](./eventbus)（本抽象的实现）
- [XiHan.Framework.Messaging](./messaging)
- [XiHan.Framework.Uow](./uow)
