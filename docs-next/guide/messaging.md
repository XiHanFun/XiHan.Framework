# 消息与事件

本章讲消息子系统：CloudEvents 信封、发布与订阅、重试与死信、领域事件、事务性发件箱，以及队列与流两个能力，适用于在特性之间或服务之间传递事件的开发者。完整 API 见 [Messaging.Abstractions 包](../packages/messaging-abstractions)。

## 能力与 Provider

消息是跨进程的调用：信封用 CloudEvents 1.0，投递经过与请求相同的内核管道，传输只负责至少一次投递、重试与死信（RFC-0009《消息、CloudEvents 信封与发件箱》）。

| 能力 | 含义 | 默认实现 | 显式 Provider |
| --- | --- | --- | --- |
| `messaging.queue@1` | 工作队列：发布、按主题消费、至少一次、重试、死信 | `Messaging.InMemory`（单进程） | `Messaging.RabbitMQ`、`Messaging.Kafka` |
| `messaging.stream@1` | 有序分区流：在队列之上按分区键有序、按消费组保存位点、按时刻重放 | — | `Messaging.Kafka` |

`Profiles.Standard` 装配了 `Messaging.InMemory`，不加 Provider 时消息只在本进程内流转、进程退出即丢失。再 `UseFeature` 一个显式 Provider 就顶替它：`RabbitMQMessagingFeature` 只提供队列，`KafkaMessagingFeature` 同时提供队列与流。两个显式 Provider 同时提供 `messaging.queue` 时能力求解报 `KERNEL_CAPABILITY_004`，用 `XiHan:Providers:messaging.queue` 写清单 ID（`xihan.messaging.rabbitmq`、`xihan.messaging.kafka`）指定一个，规则见[能力包与清单](./modularity)。

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Messaging.RabbitMQ;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

// RabbitMQ 顶替内存代理；订阅与发布的代码不变
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<RabbitMQMessagingFeature>());
```

```json
{ "XiHan": { "Messaging": { "RabbitMQ": { "Configuration": "amqp://guest:guest@localhost:5672" } } } }
```

连接配置缺失时装配失败，启动时连不上代理时启动失败（`MESSAGING_UNAVAILABLE_001`），不退回内存代理。Kafka 的配置见 [Messaging.Kafka](../packages/messaging-kafka)。

## 信封

`MessageEnvelope` 是 CloudEvents 1.0 信封，数据是字节：

| 属性 | 说明 |
| --- | --- |
| `Id` | 必填，事件 ID，与 `Source` 一起唯一 |
| `Source` | 必填，来源 URI 引用（如 `/orders`） |
| `Type` | 必填，事件类型（如 `orders.placed`） |
| `Time` | 可选，发生时间 |
| `Subject` | 可选，事件所涉的对象（如 `order/42`） |
| `DataContentType` | 数据内容类型，缺省 `application/json` |
| `Data` | 数据字节 |
| `Extensions` | 扩展属性，名称只含小写字母与数字、1 到 20 个字符；用 `WithExtension(name, value)` 派生、`Extension(name)` 读取 |

框架约定的扩展属性（`EnvelopeExtensions`）：

| 扩展 | 常量 | 投递时的作用 |
| --- | --- | --- |
| `xihantenant` | `Tenant` | 进入 Baggage `xihan.tenant`；装配了多租户 Provider 时 Tenant 阶段据此解析租户 |
| `xihancorrelation` | `Correlation` | 进入 Baggage `xihan.correlation` |
| `traceparent` | `TraceParent` | W3C Trace Context，成为处理器调用的追踪 ID 与父跨度 |
| `xihankey` | `PartitionKey` | 流能力的分区键，见[队列与流](#队列与流) |

主题名满足 `^[a-z0-9][a-z0-9._-]{0,127}# 消息与事件

本章讲消息子系统：CloudEvents 信封、发布与订阅、重试与死信、领域事件、事务性发件箱，以及队列与流两个能力，适用于在特性之间或服务之间传递事件的开发者。完整 API 见 [Messaging.Abstractions 包](../packages/messaging-abstractions)。

## 能力与 Provider

消息是跨进程的调用：信封用 CloudEvents 1.0，投递经过与请求相同的内核管道，传输只负责至少一次投递、重试与死信（RFC-0009《消息、CloudEvents 信封与发件箱》）。

| 能力 | 含义 | 默认实现 | 显式 Provider |
| --- | --- | --- | --- |
| `messaging.queue@1` | 工作队列：发布、按主题消费、至少一次、重试、死信 | `Messaging.InMemory`（单进程） | `Messaging.RabbitMQ`、`Messaging.Kafka` |
| `messaging.stream@1` | 有序分区流：在队列之上按分区键有序、按消费组保存位点、按时刻重放 | — | `Messaging.Kafka` |

`Profiles.Standard` 装配了 `Messaging.InMemory`，不加 Provider 时消息只在本进程内流转、进程退出即丢失。再 `UseFeature` 一个显式 Provider 就顶替它：`RabbitMQMessagingFeature` 只提供队列，`KafkaMessagingFeature` 同时提供队列与流。两个显式 Provider 同时提供 `messaging.queue` 时能力求解报 `KERNEL_CAPABILITY_004`，用 `XiHan:Providers:messaging.queue` 写清单 ID（`xihan.messaging.rabbitmq`、`xihan.messaging.kafka`）指定一个，规则见[能力包与清单](./modularity)。

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Messaging.RabbitMQ;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

// RabbitMQ 顶替内存代理；订阅与发布的代码不变
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<RabbitMQMessagingFeature>());
```

```json
{ "XiHan": { "Messaging": { "RabbitMQ": { "Configuration": "amqp://guest:guest@localhost:5672" } } } }
```

连接配置缺失时装配失败，启动时连不上代理时启动失败（`MESSAGING_UNAVAILABLE_001`），不退回内存代理。Kafka 的配置见 [Messaging.Kafka](../packages/messaging-kafka)。

## 信封

`MessageEnvelope` 是 CloudEvents 1.0 信封，数据是字节：

| 属性 | 说明 |
| --- | --- |
| `Id` | 必填，事件 ID，与 `Source` 一起唯一 |
| `Source` | 必填，来源 URI 引用（如 `/orders`） |
| `Type` | 必填，事件类型（如 `orders.placed`） |
| `Time` | 可选，发生时间 |
| `Subject` | 可选，事件所涉的对象（如 `order/42`） |
| `DataContentType` | 数据内容类型，缺省 `application/json` |
| `Data` | 数据字节 |
| `Extensions` | 扩展属性，名称只含小写字母与数字、1 到 20 个字符；用 `WithExtension(name, value)` 派生、`Extension(name)` 读取 |

框架约定的扩展属性（`EnvelopeExtensions`）：

| 扩展 | 常量 | 投递时的作用 |
| --- | --- | --- |
| `xihantenant` | `Tenant` | 进入 Baggage `xihan.tenant`；装配了多租户 Provider 时 Tenant 阶段据此解析租户 |
| `xihancorrelation` | `Correlation` | 进入 Baggage `xihan.correlation` |
| `traceparent` | `TraceParent` | W3C Trace Context，成为处理器调用的追踪 ID 与父跨度 |
| `xihankey` | `PartitionKey` | 流能力的分区键，见[队列与流](#队列与流) |

（`TopicName.IsValid` / `Validate`）；RabbitMQ 与 Kafka 的死信目标是映射后的名字加 `DeadLetterSuffix`（缺省 `.dlq`）。线上编码是结构化 JSON（`CloudEventsFormat`，`application/cloudevents+json`）：JSON 数据内联在 `data`，其他数据放 `data_base64`，扩展属性是顶层成员。

## 发布消息

应用经 `IMessagePublisher` 发布，它是作用域服务（缺省 `DirectPublisher` 直接交给代理；装配了发件箱时是 `OutboxPublisher`）。`IMessageBroker` 是 Provider 实现的传输，应用不直接调用。

| 方法 | 结果 |
| --- | --- |
| `PublishAsync(topic, envelope, ct)` | `Result` |
| `PublishManyAsync(topic, envelopes, ct)` | 与入参等长、顺序一致的 `IReadOnlyList<Result>`；单条是它的特例 |

批量发布遵循批量优先约定（ADR-0012）：同一主题内保持入参顺序，空集合不触代理，重复的 `Id` 照发（去重是消费端的事）；主题不合法时每一项都是 `MESSAGING_VALIDATION_001`；内存代理容量不足时整批返回 `MESSAGING_UNAVAILABLE_002`，不写入半批；代理不可达时整批抛出携带 `MESSAGING_UNAVAILABLE_001` 的 `FrameworkException`。Provider 用代理的原生批量：RabbitMQ 在同一通道上连续发布，Kafka 交给客户端合并成批次后一次等待全部投递报告，内存代理一次入队。

发布者不会从当前调用自动补扩展属性，处理器那一端要看到租户、关联 ID 与追踪，发布方自己写进信封：

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;

public sealed record OrderShipped(Guid OrderId, string Carrier);

[JsonSerializable(typeof(OrderShipped))]
internal sealed partial class ShippingJsonContext : JsonSerializerContext;

public sealed class ShipmentNotifier(IMessagePublisher publisher, TimeProvider clock)
{
    public const string Topic = "shipments";

    public async ValueTask<Result> NotifyAsync(IReadOnlyList<OrderShipped> shipments, InvocationContext context)
    {
        var envelopes = shipments.Select(shipped => WithContext(new MessageEnvelope
        {
            Id = Guid.CreateVersion7().ToString("N"),
            Source = "/shipping",
            Type = "shipping.order-shipped",
            Time = clock.GetUtcNow(),
            Subject = $"order/{shipped.OrderId:N}",
            Data = JsonSerializer.SerializeToUtf8Bytes(shipped, ShippingJsonContext.Default.OrderShipped),
        }, context)).ToList();

        // 一次批量发布；逐项结果与入参一一对应，返回第一个失败
        foreach (var result in await publisher.PublishManyAsync(Topic, envelopes, context.CancellationToken))
        {
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success();
    }

    // 租户、关联 ID 与追踪随信封走，处理器的调用上下文从这里还原
    private static MessageEnvelope WithContext(MessageEnvelope envelope, InvocationContext context)
    {
        if (context.GetSecurity().Tenant is { } tenant)
        {
            envelope = envelope.WithExtension(EnvelopeExtensions.Tenant, tenant.Id);
        }

        if (context.TryGetBaggage(BaggageKeys.Correlation, out var correlation))
        {
            envelope = envelope.WithExtension(EnvelopeExtensions.Correlation, correlation);
        }

        return envelope.WithExtension(EnvelopeExtensions.TraceParent, $"00-{context.TraceId}-{context.SpanId}-01");
    }
}
```

`ShipmentNotifier` 依赖作用域服务 `IMessagePublisher`，登记成作用域（`AddScoped`），在操作或消息处理器里调用。在单例的后台服务里发布时先 `CreateAsyncScope()` 再取发布者。领域事件不需要手写信封，见[领域事件](#领域事件)。

## 订阅与处理

处理器实现 `IMessageHandler`，用 `AddMessageHandler<THandler>(topic)` 订阅；处理器注册为作用域服务，每条消息在新作用域里解析。下面的处理器仿照 `next/samples/SimpleApp` 的 `OrderPlacedHandler`，加上了解码数据与读取租户：

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Domain.Events;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Primitives;

public sealed record OrderPlaced(Guid OrderId, DateTimeOffset OccurredAt) : IDomainEvent;

[JsonSerializable(typeof(OrderPlaced))]
internal sealed partial class OrdersJsonContext : JsonSerializerContext;

public sealed class OrderPlacedHandler(ILogger<OrderPlacedHandler> logger) : IMessageHandler
{
    public ValueTask<Result> HandleAsync(MessageDelivery delivery, InvocationContext context, CancellationToken cancellationToken)
    {
        if (JsonSerializer.Deserialize(delivery.Envelope.Data.Span, OrdersJsonContext.Default.OrderPlaced) is not { } placed)
        {
            // 抛出异常与返回失败一样算投递失败：代理重试到上限后进死信
            throw new InvalidOperationException("The orders.placed event has no data.");
        }

        // 装配了多租户 Provider 时，信封的 xihantenant 已在 Tenant 阶段解析成当前租户，这次调用里的仓储按它过滤
        logger.LogInformation("Order {OrderId} placed for tenant {TenantId} (attempt {Attempt}).", placed.OrderId, context.GetSecurity().Tenant?.Id, delivery.Attempt);
        return ValueTask.FromResult(Result.Success());
    }
}
```

```csharp
// 特性的 Configure 里订阅；同一主题可以订阅多个处理器
context.Services.AddMessageHandler<OrderPlacedHandler>("orders");
```

`MessageDelivery` 带主题（`Topic`）、信封（`Envelope`）与第几次尝试（`Attempt`，从 1 起）。派发器 `MessageDispatcher` 为每条消息：

1. 开新的作用域，用信封的扩展属性构造调用上下文：`xihantenant`、`xihancorrelation` 进 Baggage，`traceparent` 给出追踪 ID 与父跨度，调用来源（`InvocationOrigin`）的协议是 `InvocationProtocols.Messaging`、目标是主题。
2. 经完整管道执行（`Ingress → Observe → Resilience → Authenticate → Tenant → Authorize → Validate → Idempotency → Transaction → Handle → Egress`），按订阅顺序依次执行该主题的处理器，第一个失败即为调用结果。

所以处理器里能用的东西与操作处理器一样：装配了多租户的 Provider 时 `context.GetSecurity().Tenant` 是信封带来的租户，仓储自动按租户过滤；Transaction 阶段开着工作单元，处理器里的写入随它提交或回滚；装配了发件箱时处理器发布的消息也先写进发件箱，随它一起提交。中间件里可以用 `MessageDispatcher.GetDelivery(context)` 取当前投递。代理在应用进入服务状态之前不派发。

消息没有凭据，Authenticate 阶段得到的是匿名主体；派发不写幂等键，Idempotency 阶段直接放行。

## 重试与死信

处理器返回失败或抛出异常都算投递失败。代理重试到 `XiHan:Messaging:MaxAttempts`（缺省 3，含首次），仍失败就进死信；重试之间不等待。

| Provider | 重试 | 死信 |
| --- | --- | --- |
| `Messaging.InMemory` | 以下一次尝试号重新入队到队尾；入队时队列已满则直接死信 | 该主题的有界死信列表 `DeadLetters(topic)`，满时丢弃最旧 |
| `Messaging.RabbitMQ` | 尝试次数放在消息头 `x-xihan-attempt`，重新发布到队尾后确认原消息 | 队列 `<QueuePrefix><topic><DeadLetterSuffix>`；无法解码的消息直接进死信 |
| `Messaging.Kafka` | 在原位重试，不重新入队，分区内顺序不变 | 主题 `<TopicPrefix><topic><DeadLetterSuffix>`；无法解码的消息直接进死信 |

::: warning 处理器必须能承受重复投递
投递是至少一次：重试、重放、代理重投与发件箱的崩溃恢复都会让同一条消息再到达处理器。派发不经过幂等键，去重由处理器按信封的 `Id` + `Source` 自理。
:::

内存代理与 RabbitMQ 的重试排到队尾，同一主题后面的消息先于重试被处理；需要严格有序时用 Kafka 并给消息设分区键。

## 领域事件

聚合在工作单元里记录的领域事件由消息子系统派发（ADR-0025）。应用显式登记每种事件：主题、CloudEvents `type`、源生成的序列化元数据与可选的 `subject` 选择器。下面两行取自 `next/samples/SimpleApp` 的 `OrdersFeature`：

```csharp
// 聚合记录的 OrderPlaced 在提交前发到 orders 主题，类型 orders.placed，subject 是订单
context.Services.AddDomainEvent<OrderPlaced>(Topic, "orders.placed", SampleJsonContext.Default.OrderPlaced, static placed => $"order/{placed.OrderId:N}");
context.Services.AddMessageHandler<OrderPlacedHandler>(Topic);
```

派发由 Transaction 阶段决定时机：处理器成功后、提交前，`UnitOfWorkMiddleware` 把工作单元登记的、带有未派发事件的聚合交给 `IDomainEventDispatcher`（首次 `AddDomainEvent` 时注册为 `DomainEventDispatcher`）。事件映射成信封：

| 信封属性 | 取值 |
| --- | --- |
| `Id` | 新的 UUID v7（`N` 格式） |
| `Source` | `/<topic>` |
| `Type` | 登记的类型 |
| `Subject` | 选择器的结果，没有选择器时省略 |
| `Time` | `IDomainEvent.OccurredAt` |
| `Data` | 用登记的 `JsonTypeInfo` 序列化的事件 |
| `xihantenant` | 工作单元有租户时带上 |

同一主题的事件一次 `PublishManyAsync`，主题按首次出现的顺序；任一项失败即派发失败，工作单元回滚，聚合的缓冲保留；全部成功后清空每个聚合的缓冲再提交。

| 情况 | 结果 |
| --- | --- |
| 事件类型没有登记 | 调用以 `MESSAGING_VALIDATION_003` 结束并回滚，一条也不发 |
| 同一事件类型登记两次，或登记表满（10000 种） | 装配时抛 `FrameworkException`（`MESSAGING_VALIDATION_004`） |
| 有事件但没有任何 `AddDomainEvent`（没有派发器） | 调用以 `PERSISTENCE_VALIDATION_003` 结束并回滚 |

没有发件箱时，事件在提交前直接发到代理：提交失败时消息已经发出。要让事件与业务数据一起提交或回滚，装配[事务性发件箱](#事务性发件箱)，事件会随业务写入落进发件箱表。聚合与 `DomainEventBuffer` 的写法见[领域模型](./domain)，工作单元见[工作单元与事务](./uow)。

## 事务性发件箱

`OutboxFeature` 把「写业务数据」与「发消息」放进同一个工作单元。它在 `Messaging.Abstractions` 里，需要 `messaging.queue@1` 与 `persistence.relational@1`，不在任何组合包里，按需装配（`next/samples/SimpleApp` 的写法）：

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Messaging.Abstractions.Outbox;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<OutboxFeature>());
```

```json
{ "XiHan": { "Messaging": { "Outbox": { "PollIntervalSeconds": 0.5, "BatchSize": 100 } } } }
```

装配后 `IMessagePublisher` 被替换成 `OutboxPublisher`：

1. 有活动工作单元时（操作与消息处理器的 Transaction 阶段都开着），发布写成 `OutboxMessage` 行（主题、CloudEvents 结构化 JSON、创建时间），批量发布用仓储的 `AddRangeAsync` 一次写入，随业务事务提交或回滚。没有活动工作单元时直接发布到代理。
2. 后台的 `OutboxDispatcher` 每隔 `PollInterval` 跑一轮，每轮一个工作单元：按创建时间取最多 `BatchSize` 条未投递的消息，按主题分组各一次 `PublishManyAsync`，成功的记 `DispatchedAt`，失败的累加 `Attempts` 并记 `LastError`，最后一次批量更新。
3. 单轮异常记日志后继续下一轮，只有停止才退出循环。

投递是至少一次：一轮发布之后、提交之前崩溃，下一轮会重发。发件箱没有尝试上限，失败的消息每一轮都会再试；投递成功的行只记投递时间、不删除，清理由应用按 `DispatchedAt` 自己做。用 EF Core 时 `OutboxMessage` 要像其他实体一样进应用 `DbContext` 的模型（`modelBuilder.Entity<OutboxMessage>()`），见[持久化](./persistence)。

## 队列与流

`messaging.queue@1` 只承诺至少一次与按主题消费；`messaging.stream@1`（Kafka）在此之上多三条保证：

1. 分区键：信封扩展 `xihankey`（`EnvelopeExtensions.PartitionKey`）决定分区，同一主题内同键的消息按发布顺序投递；没有键的消息由 Kafka 分配分区，只在主题只有一个分区时全局有序。
2. 消费位点：按消费组（`GroupId`，缺省 `xihan`）保存，投递完成（成功、重试耗尽进死信、无法解码进死信）后才提交；重启续接，仍是至少一次。
3. 重放：`IMessageReplayer.ReplayAsync(topic, since, ct)` 用独立的消费位点把该时刻起的消息重新投递给当前订阅的处理器，经完整管道、按同样的重试与死信规则，不影响正常消费的位点。

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

`IMessageReplayer` 只在 Kafka 被选中提供 `messaging.stream` 时登记；依赖它的特性在清单里声明 `messaging.stream@1`。主题不存在或该时刻之后没有消息时重放成功返回；代理不可达返回 `MESSAGING_UNAVAILABLE_001`。

## 与 CloudEvents 协议的关系

`Protocols.CloudEvents` 是信封在 HTTP 上的 CloudEvents 协议绑定：入站端点 `MapXiHanCloudEvents()` 接收 binary / structured / batch 三种内容模式，按 `XiHan:Protocols:CloudEvents:Topics:<事件类型>` 映射到主题，缺省在请求内经同一个 `MessageDispatcher` 派发给处理器（也可以只发布到代理）；出站的 `CloudEventsWebhookPublisher` 把信封推送到配置的 Webhook。`next/samples/SimpleApp` 的 `POST /events` 就是把外部的 `orders.placed` 事件交给 `OrderPlacedHandler`。细节见 [gRPC、MCP 与 CloudEvents](./protocols) 与 [Protocols.CloudEvents](../packages/protocols-cloudevents)。

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Messaging:MaxAttempts` | 正整数 | 3 | 一条消息最多投递次数（含首次） |
| `XiHan:Messaging:DeadLetterSuffix` | 字符串 | `.dlq` | 死信主题 / 队列后缀 |
| `XiHan:Messaging:Outbox:PollIntervalSeconds` | 正数 | 1 | 发件箱投递循环的轮询间隔 |
| `XiHan:Messaging:Outbox:BatchSize` | 正整数 | 100 | 发件箱每轮最多投递条数 |
| `XiHan:Messaging:InMemory:MaxQueuedMessages` | 正整数 | 10000 | 内存代理每个主题（及其死信列表）的条目上限 |
| `XiHan:Messaging:RabbitMQ:*` | — | — | 连接、队列前缀、预取数，见 [Messaging.RabbitMQ](../packages/messaging-rabbitmq#配置) |
| `XiHan:Messaging:Kafka:*` | — | — | 引导地址、消费组、主题前缀、透传配置，见 [Messaging.Kafka](../packages/messaging-kafka#配置) |

非法值在装配时抛 `ArgumentException`。

## 健康项与错误码

| 健康项 | 来源 | Degraded 的条件 |
| --- | --- | --- |
| `messaging.inmemory.capacity` | `Messaging.InMemory` | 任一主题的队列排满（`MESSAGING_UNAVAILABLE_002`） |
| `messaging.rabbitmq.connection` | `Messaging.RabbitMQ` | 连接或发布通道关闭（`MESSAGING_UNAVAILABLE_001`） |
| `messaging.kafka.connection` | `Messaging.Kafka` | 集群不可达或超时（`KERNEL_UNAVAILABLE_005`） |

| 错误码 | 含义 |
| --- | --- |
| `MESSAGING_VALIDATION_001` | 主题不合法，发布者逐项返回 |
| `MESSAGING_VALIDATION_002` | 信封不合法或无法解码 |
| `MESSAGING_VALIDATION_003` | 领域事件类型没有用 `AddDomainEvent` 登记 |
| `MESSAGING_VALIDATION_004` | 领域事件类型重复登记或登记表已满（装配时抛出） |
| `MESSAGING_UNAVAILABLE_001` | 代理不可达或操作失败；发布时以 `FrameworkException` 抛出，重放时作为失败结果返回 |
| `MESSAGING_UNAVAILABLE_002` | 内存代理的主题队列已满 |
| `MESSAGING_UNEXPECTED_001` | 处理器抛出未处理异常，代理记为一次失败的投递 |

## 相关

- [Messaging.Abstractions](../packages/messaging-abstractions)、[Messaging.InMemory](../packages/messaging-inmemory)、[Messaging.RabbitMQ](../packages/messaging-rabbitmq)、[Messaging.Kafka](../packages/messaging-kafka)
- [执行管道](./pipeline)、[多租户](./multi-tenancy)、[工作单元与事务](./uow)、[领域模型](./domain)
- [gRPC、MCP 与 CloudEvents](./protocols)
- [测试与一致性套件](./testing)：Provider 必须通过 `MessageBrokerConformance`，流 Provider 还要通过 `MessageStreamConformance`
- 规范：RFC-0009《消息、CloudEvents 信封与发件箱》；ADR-0012、ADR-0019、ADR-0025
