# 发布与订阅

这一页讲消息子系统的基本用法：它提供哪些能力与 Provider、消息的 CloudEvents 信封长什么样、怎样用 `IMessagePublisher` 发布、怎样写 `IMessageHandler` 并订阅主题、消息怎样经管道派发、竞争与广播两种订阅方式有什么区别，以及重试、死信、配置、健康项与错误码。读完你能让两个特性或两个服务之间通过消息解耦，并在单进程与 RabbitMQ、Kafka 之间切换而不改业务代码。前置知识是[特性与清单](/startup/features)与[执行管道](/startup/pipeline)。

## 为什么需要

订单创建后要通知库存、发邮件、更新报表，如果都写在下单操作里，任何一个下游变慢或出错都会拖垮下单本身，新增下游还得改下单代码。消息把「发生了什么」发到主题，关心的人各自订阅，发布方不知道也不等待订阅方。但消息系统自己有一堆细节：信封格式、租户与追踪怎样跟着消息走、失败了重试几次、多实例时谁收。框架把这些定成规范（RFC-0009《消息、CloudEvents 信封与发件箱》）：信封用 CloudEvents，投递经过与请求相同的内核管道，传输只负责至少一次投递、重试与死信。内存代理随 Standard 组合包装配，零配置可用；多实例时装配 RabbitMQ 或 Kafka 顶替。

## 能力与 Provider

| 能力 | 含义 | 默认实现 | 显式 Provider |
| --- | --- | --- | --- |
| `messaging.queue@1` | 工作队列：发布、按主题消费、至少一次、重试、死信 | `Messaging.InMemory`（单进程） | `Messaging.RabbitMQ`、`Messaging.Kafka` |
| `messaging.stream@1` | 有序分区流：在队列之上按分区键有序、按消费组保存位点、按时刻重放 | — | `Messaging.Kafka` |

`Profiles.Standard` 装配了 `Messaging.InMemory`，不加 Provider 时消息只在本进程内流转、进程退出即丢失；内存代理只为本进程订阅了的主题缓冲消息。再 `UseFeature` 一个显式 Provider 就顶替它：`RabbitMQMessagingFeature` 只提供队列，`KafkaMessagingFeature` 同时提供队列与流。两个显式 Provider 同时提供 `messaging.queue` 时能力求解报 `KERNEL_CAPABILITY_004`，用 `XiHan:Providers:messaging.queue` 写清单 ID（`xihan.messaging.rabbitmq`、`xihan.messaging.kafka`）指定一个，规则见[能力图谱与 Provider](/startup/capabilities)。

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

连接配置（`XiHan:Messaging:RabbitMQ:Configuration`）缺失时装配失败，启动时连不上代理时启动失败（`MESSAGING_UNAVAILABLE_001`），不退回内存代理。两个 Provider 的配置与行为见[队列与流](/messaging/streams)。

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

框架约定的扩展属性（常量在 `EnvelopeExtensions`）：

| 扩展 | 常量 | 投递时的作用 |
| --- | --- | --- |
| `xihantenant` | `Tenant` | 进入 Baggage `xihan.tenant`；装配了多租户 Provider 时 Tenant 阶段据此解析租户 |
| `xihanprincipal` | `Principal` | 进入 Baggage `xihan.principal`；只是发起者的 ID，不是凭据，处理器的调用仍是匿名主体 |
| `xihancorrelation` | `Correlation` | 进入 Baggage `xihan.correlation` |
| `xihanlocale` | `Locale` | 进入 Baggage `xihan.locale`，处理器里 `key.L()` 按这个区域取文案 |
| `traceparent` | `TraceParent` | W3C Trace Context，成为处理器调用的追踪 ID、父跨度与采样标志 |
| `xihankey` | `PartitionKey` | 流能力的分区键，见[队列与流](/messaging/streams) |

幂等键 `xihan.idempotency-key` 不进入消息：派发不写幂等键，带上它会让同一请求发出的几条消息与同一条消息的重试被幂等中间件当成同一次调用。

主题名满足 `^[a-z0-9][a-z0-9._-]{0,127}$`（`TopicName.IsValid` / `Validate`）；死信目标是主题名加 `DeadLetterSuffix`（缺省 `.dlq`）。线上编码是 CloudEvents 结构化 JSON（`CloudEventsFormat`，`application/cloudevents+json`）。

## 最小示例

下面的 `ShippingFeature` 同时放发布方与订阅方：`ShipmentNotifier` 把一批发货记录批量发到 `shipments` 主题，`OrderShippedHandler` 订阅这个主题、解码数据并记日志。清单声明依赖 `messaging.queue`，换 Provider 不改这里。

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

public sealed record OrderShipped(Guid OrderId, string Carrier);

[JsonSerializable(typeof(OrderShipped))]
internal sealed partial class ShippingJsonContext : JsonSerializerContext;

public sealed class ShipmentNotifier(IMessagePublisher publisher, TimeProvider clock)
{
    public const string Topic = "shipments";

    public async ValueTask<Result> NotifyAsync(IReadOnlyList<OrderShipped> shipments, InvocationContext context)
    {
        var envelopes = shipments.Select(shipped => new MessageEnvelope
        {
            Id = Guid.CreateVersion7().ToString("N"),
            Source = "/shipping",
            Type = "shipping.order-shipped",
            Time = clock.GetUtcNow(),
            Subject = $"order/{shipped.OrderId:N}",
            Data = JsonSerializer.SerializeToUtf8Bytes(shipped, ShippingJsonContext.Default.OrderShipped),
        }).ToList();

        // 一次批量发布；租户、关联 ID 与追踪由发布者从当前调用补齐；逐项结果与入参一一对应，返回第一个失败
        foreach (var result in await publisher.PublishManyAsync(Topic, envelopes, context.CancellationToken))
        {
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success();
    }
}

public sealed class OrderShippedHandler(ILogger<OrderShippedHandler> logger) : IMessageHandler
{
    public ValueTask<Result> HandleAsync(MessageDelivery delivery, InvocationContext context, CancellationToken cancellationToken)
    {
        if (JsonSerializer.Deserialize(delivery.Envelope.Data.Span, ShippingJsonContext.Default.OrderShipped) is not { } shipped)
        {
            // 抛出异常与返回失败一样算投递失败：代理重试到上限后进死信
            throw new InvalidOperationException("The shipping.order-shipped event has no data.");
        }

        // 信封的 xihantenant 已在 Tenant 阶段解析成当前租户，这次调用里的仓储按它过滤
        logger.LogInformation("Order {OrderId} shipped for tenant {TenantId} (attempt {Attempt}).", shipped.OrderId, context.GetSecurity().Tenant?.Id, delivery.Attempt);
        return ValueTask.FromResult(Result.Success());
    }
}

public sealed class ShippingFeature : IFeature
{
    public Manifest Manifest { get; } = new()
    {
        Id = "acme.shipping",
        Package = "Acme.Shipping",
        Version = "1.0.0",
        Primitives = 1,
        Stability = Stability.Experimental,
        Kind = ManifestKind.Capability,
        Requires =
        [
            new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
            new CapabilityRequirement(MessagingCapabilities.Queue, MessagingCapabilities.Version),
        ],
        Entry = typeof(ShippingFeature).FullName,
    };

    public void Configure(FeatureContext context)
    {
        // 发布者是作用域服务，依赖它的通知器也登记成作用域，在操作或处理器里调用
        context.Services.AddScoped<ShipmentNotifier>();
        // 订阅：同一主题可以订阅多个处理器，按登记顺序执行
        context.Services.AddMessageHandler<OrderShippedHandler>(ShipmentNotifier.Topic);
    }
}
```

在 `AddXiHan` 里 `UseFeature<ShippingFeature>()` 后，在某个操作里注入 `ShipmentNotifier` 并调用 `NotifyAsync`，日志里随即出现 `OrderShippedHandler` 的记录，`attempt` 为 1，租户与操作调用的一致，追踪里处理器的 Span 是发布调用的子 Span。处理器抛出异常时同一条消息再到达两次（`attempt` 为 2、3），然后进死信。领域事件不需要手写信封，见[领域事件与发件箱](/messaging/domain-events)。

## 发布消息

应用经 `IMessagePublisher` 发布，它是作用域服务（缺省 `DirectPublisher` 直接交给代理；装配了发件箱时是 `OutboxPublisher`）。`IMessageBroker` 是 Provider 实现的传输，应用不直接调用。

| 方法 | 结果 |
| --- | --- |
| `PublishAsync(topic, envelope, ct)` | `Result` |
| `PublishManyAsync(topic, envelopes, ct)` | 与入参等长、顺序一致的 `IReadOnlyList<Result>`；单条是它的特例 |

批量发布遵循批量优先约定：同一主题内保持入参顺序，空集合不触代理，重复的 `Id` 照发（去重是消费端的事）；主题不合法时每一项都是 `MESSAGING_VALIDATION_001`；内存代理容量不足时整批返回 `MESSAGING_UNAVAILABLE_002`，不写入半批；代理不可达时整批抛出携带 `MESSAGING_UNAVAILABLE_001` 的 `FrameworkException`。

发布到没人订阅的主题照常成功：内存代理与 RabbitMQ 丢弃消息并记 Debug 日志，Kafka 的消息进主题、按保留期清理。调度、工作流等框架事件发到应用不一定订阅的主题，这是常态。

在调用内发布时（操作、消息处理器、作业里），`DirectPublisher` 与 `OutboxPublisher` 先从当前调用（`IInvocationAccessor`）给每个信封补上缺失的扩展：`traceparent` 取当前 Activity 或调用的追踪 ID、父跨度与采样标志，`xihantenant` 取安全上下文的租户，`xihanprincipal` 取已认证主体的 ID，`xihancorrelation` 与 `xihanlocale` 取同名 Baggage。信封上已有的扩展保持不变，要换成别的值就自己 `WithExtension`；不在调用内（单例的后台循环、自己建的作用域）发布时不补。在单例的后台服务里发布时先 `CreateAsyncScope()` 再取发布者。

## 订阅与处理

处理器实现 `IMessageHandler`，用 `AddMessageHandler<THandler>(topic)` 订阅；处理器注册为作用域服务，每条消息在新作用域里解析。`MessageDelivery` 带主题（`Topic`）、信封（`Envelope`）、第几次尝试（`Attempt`，从 1 起）与订阅方式（`Mode`）。派发器 `MessageDispatcher` 为每条消息开新的作用域，用信封的扩展属性构造调用上下文（四个 `xihan*` 扩展进 Baggage，`traceparent` 给出追踪 ID、父跨度与采样标志，调用来源的协议是 `InvocationProtocols.Messaging`、目标是主题），然后经[执行管道](/startup/pipeline)的全部阶段执行，按订阅顺序依次执行该主题、与投递同一订阅方式的处理器，第一个失败即为调用结果。

所以处理器里能用的东西与操作处理器一样：装配了多租户的 Provider 时 `context.GetSecurity().Tenant` 是信封带来的租户，仓储自动按租户过滤；Transaction 阶段开着工作单元，处理器里的写入与发布的消息随它提交或回滚。中间件里可以用 `MessageDispatcher.GetDelivery(context)` 取当前投递。代理在应用进入服务状态之前不派发。消息没有凭据，Authenticate 阶段得到的是匿名主体；派发不写幂等键，Idempotency 阶段直接放行。

## 订阅方式

订阅登记带订阅方式 `SubscriptionMode`：

| 方式 | 谁收到 | 失败到上限 | 适合 |
| --- | --- | --- | --- |
| `Competing`（缺省） | 订阅了这个主题的实例里只有一个收到 | 进死信 | 业务事件、领域事件、只该处理一次的工作 |
| `Broadcast` | 每个在线实例各收到一份 | 丢弃并记 Warning 日志 | 断开本实例上的连接、清本地缓存这类每个节点都要做的事 |

```csharp
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.Handlers;

// 竞争（缺省）：每条消息只有一个实例的处理器执行
context.Services.AddMessageHandler<OrderPlacedHandler>("orders");

// 广播：每个实例都执行，各自断开连在自己身上的会话连接
context.Services.AddMessageHandler<SessionRevokedHandler>("sessions.revoked", SubscriptionMode.Broadcast);
```

同一主题可以两种方式都订阅：订阅表按（主题，方式）分组，代理各消费一份，一条消息到达一个实例的竞争订阅与广播订阅是两次独立的投递，各自计尝试次数、各自重试，派发器只执行与投递同一方式的处理器。

广播的语义边界：

- 尽力而为：只有订阅时在线、与代理连着的实例收到；不在线、重启中、断开期间发布的消息收不到，上线后也不回放历史，Kafka 的重放也只投给竞争订阅。要不丢就用竞争订阅。
- 没有死信也不共享去重：在本实例内按 `MaxAttempts` 重试，到上限或无法解码时丢弃并记 Warning 日志；代理重投时同一实例可能收到重复的投递。

各 Provider 用实例标识 `MessagingInstance.Id` 区分广播订阅，标识的配置与唯一性要求见[队列与流](/messaging/streams)。

## 重试与死信

处理器返回失败或抛出异常都算投递失败。代理重试到 `XiHan:Messaging:MaxAttempts`（缺省 3，含首次），竞争订阅仍失败就进死信，广播订阅丢弃并记日志；重试之间不等待。内存代理把失败的消息以下一次尝试号重新入队到队尾，死信进该主题的有界列表 `DeadLetters(topic)`；RabbitMQ 与 Kafka 的重试与死信队列、死信主题见[队列与流](/messaging/streams)。

::: warning 处理器必须能承受重复投递
投递是至少一次：重试、重放、代理重投与发件箱的崩溃恢复都会让同一条消息再到达处理器。派发不经过幂等键，去重由处理器按信封的 `Id` + `Source` 自理。
:::

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Messaging:MaxAttempts` | 正整数 | 3 | 一条消息最多投递次数（含首次） |
| `XiHan:Messaging:DeadLetterSuffix` | 字符串 | `.dlq` | 死信主题 / 队列后缀 |
| `XiHan:Messaging:InstanceId` | 字符串 | 不写，每次启动生成 | 本实例在代理上的标识，广播订阅用；见[队列与流](/messaging/streams) |
| `XiHan:Messaging:InMemory:MaxQueuedMessages` | 正整数 | 10000 | 内存代理每个主题（及其死信列表）的条目上限 |

非法值在装配时抛 `ArgumentException`。发件箱的 `XiHan:Messaging:Outbox:*` 见[领域事件与发件箱](/messaging/domain-events)，RabbitMQ 与 Kafka 的配置节见[队列与流](/messaging/streams)。

## 健康项与错误码

内存代理的健康项 `messaging.inmemory.capacity` 在任一主题的队列排满时转为 Degraded（`MESSAGING_UNAVAILABLE_002`）；RabbitMQ 与 Kafka 的连接健康项见[队列与流](/messaging/streams)。

| 错误码 | 含义 |
| --- | --- |
| `MESSAGING_VALIDATION_001` | 主题不合法，发布者逐项返回 |
| `MESSAGING_VALIDATION_002` | 信封不合法或无法解码 |
| `MESSAGING_VALIDATION_003` | 领域事件类型没有用 `AddDomainEvent` 登记 |
| `MESSAGING_VALIDATION_004` | 领域事件类型重复登记或登记表已满（装配时抛出） |
| `MESSAGING_UNAVAILABLE_001` | 代理不可达或操作失败；发布时以 `FrameworkException` 抛出，重放时作为失败结果返回 |
| `MESSAGING_UNAVAILABLE_002` | 内存代理的主题队列已满 |
| `MESSAGING_UNEXPECTED_001` | 处理器抛出未处理异常，代理记为一次失败的投递 |

## 常见问题

### 发布到没人订阅的主题会报错吗

不会，发布成功。内存代理与 RabbitMQ 丢弃消息并记 Debug 日志，Kafka 把消息留在主题里直到保留期结束。框架自己的调度、工作流事件就是这样发出的，应用订阅与否都行。

### 处理器一定要幂等吗

要。至少一次投递意味着重试、重放、代理重投都会让同一条消息再到达，而消息派发不经过幂等键。按信封的 `Id` + `Source` 记录已处理的消息，或把处理逻辑写成天然幂等的（按主键 upsert、状态机转移）。

### 为什么消息不带幂等键

一个请求可能发出多条消息，一条消息又可能被重试多次，如果幂等键跟着消息走，这些调用都会被当成同一次调用而被去重或回放。所以派发不写幂等键，去重交给处理器。

## 相关包与下一步

- 包参考：[Messaging.Abstractions](/packages/messaging-abstractions)、[Messaging.InMemory](/packages/messaging-inmemory)、[Messaging.RabbitMQ](/packages/messaging-rabbitmq)、[Messaging.Kafka](/packages/messaging-kafka)
- 处理器的调用上下文、Baggage 与追踪见[调用上下文](/startup/invocation-context)与[日志与可观测性](/common/observability)；租户解析见[租户解析与隔离](/multi-tenancy/resolution)
- Provider 要通过的一致性套件见[一致性套件与门禁](/testing/conformance)
- 下一步：[领域事件与发件箱](/messaging/domain-events)，让聚合里记录的事件随事务一起发出
