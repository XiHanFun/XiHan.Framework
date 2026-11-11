# XiHan.Framework.Messaging.Abstractions

消息子系统的抽象包：CloudEvents 信封、发布者与代理契约、处理器订阅、经内核管道的派发、领域事件派发与事务性发件箱。能力 `messaging.queue@1` 与 `messaging.stream@1` 由 Provider 提供；发布或订阅消息、写消息 Provider 时引用它。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Messaging.Abstractions` |
| 环层 | Ring 2（抽象） |
| 能力 | 定义 `messaging.queue@1`、`messaging.stream@1`（`MessagingCapabilities`），本包不提供；`OutboxFeature` 依赖 `kernel.hosting@1`、`messaging.queue@1`、`persistence.relational@1` |
| 特性入口 | `OutboxFeature`（清单 ID `xihan.messaging.outbox`，可选） |
| 配置节 | `XiHan:Messaging`（`MaxAttempts`、`DeadLetterSuffix`、`InstanceId`，由 Provider 读取）、`XiHan:Messaging:Outbox` |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Persistence.Abstractions`、`XiHan.Framework.Localization.Abstractions`；`Microsoft.Extensions.Configuration.Abstractions`、`Microsoft.Extensions.DependencyInjection.Abstractions`、`Microsoft.Extensions.Logging.Abstractions` |
| AOT | 兼容 |
| 稳定性 | Stable；`IMessageReplayer` 与领域事件类型（`DomainEventRegistry`、`DomainEventRegistration`、`DomainEventDispatcher`）为 Preview |

## 概述

本包把消息定义成跨进程的调用：

- 信封 `MessageEnvelope` 是 CloudEvents 1.0，`CloudEventsFormat` 做结构化 JSON 编解码（`application/cloudevents+json`）。
- 应用经 `IMessagePublisher` 发布，Provider 实现 `IMessageBroker`（发布、按订阅消费、至少一次、重试、死信）；流 Provider 另实现 `IMessageReplayer`。
- 处理器实现 `IMessageHandler`，用 `AddMessageHandler<THandler>(topic, mode)` 订阅；订阅方式 `SubscriptionMode` 缺省 `Competing`（订阅了主题的实例里只有一个收到），`Broadcast` 是每个在线实例各收到一份（尽力而为、不进死信，[订阅方式](/messaging/pubsub)）。`MessageDispatcher` 为每条消息开新作用域、从扩展属性构造调用上下文、经完整管道执行与投递同一方式的处理器。
- Provider 的特性先注册自己的 `IMessageBroker`，再调用 `MessagingRegistration.Configure(context)` 注册选项、派发器、`MessagingRuntime` 与缺省的 `DirectPublisher`，启停时调用 `MessagingRuntime.StartAsync` / `StopAsync`。
- 领域事件：`AddDomainEvent<TEvent>` 登记事件到主题与 CloudEvents `type`，`DomainEventDispatcher` 实现持久化抽象的 `IDomainEventDispatcher`，在 Transaction 阶段提交前把聚合的事件发成信封。
- 发件箱：`OutboxFeature` 把发布者换成 `OutboxPublisher`，有活动工作单元时写发件箱表，由 `OutboxDispatcher` 在提交后投递。

## 何时使用

- 特性发布或订阅消息：引用本包，在清单里声明 `messaging.queue@1`；运行时用 [Messaging.InMemory](./messaging-inmemory)（随 `Profiles.Standard` 装配）、[Messaging.RabbitMQ](./messaging-rabbitmq)、[Messaging.Kafka](./messaging-kafka) 或 [Messaging.Redis](./messaging-redis)。
- 需要事件与业务数据同事务：再装配本包的 `OutboxFeature`。
- 写新的代理 Provider：实现 `IMessageBroker`，复用 `MessagingRegistration` 与 `MessagingRuntime`，并通过 [Messaging.Conformance](./conformance) 的 `MessageBrokerConformance`（流 Provider 还有 `MessageStreamConformance`）。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Messaging.Abstractions --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[快速上手](/getting-started/installation)。订阅写在特性的 `Configure` 里，或直接写在 `builder.Services` 上；发件箱按需装配：

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Messaging.Abstractions.Outbox;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<OutboxFeature>());
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Messaging:MaxAttempts` | 正整数 | 3 | 一条消息最多投递次数（含首次），超过进死信 |
| `XiHan:Messaging:DeadLetterSuffix` | 字符串 | `.dlq` | 死信主题后缀 |
| `XiHan:Messaging:InstanceId` | 字符串 | 不写，每次启动生成 | 本实例在代理上的标识（广播订阅的队列名与消费组名用它）；1 到 64 个字母、数字、`.`、`_`、`-`，字母或数字开头，不合文法装配失败；必须每个同时在线的实例唯一 |
| `XiHan:Messaging:Outbox:PollIntervalSeconds` | 正数 | 1 | 发件箱投递循环的轮询间隔 |
| `XiHan:Messaging:Outbox:BatchSize` | 1 到 1000 的整数 | 100 | 发件箱每轮最多投递条数，也是每轮最多删除的已投递行数；按它分页查发件箱表，不能超过查询的取出上限 `EntityQuery.DefaultMaxTake` |
| `XiHan:Messaging:Outbox:MaxAttempts` | 正整数 | 10 | 一条消息最多尝试投递的次数（含首次），用尽进死信 |
| `XiHan:Messaging:Outbox:RetryDelaySeconds` | 正数 | 5 | 第一次失败后的重试间隔，之后每失败一次翻倍 |
| `XiHan:Messaging:Outbox:MaxRetryDelaySeconds` | 正数 | 300 | 重试间隔的上限 |
| `XiHan:Messaging:Outbox:DispatchedRetentionSeconds` | 非负数 | 86400 | 投递成功的行保留多久，0 为投递成功即删除 |

```json
{ "XiHan": { "Messaging": { "MaxAttempts": 5, "Outbox": { "PollIntervalSeconds": 0.5 } } } }
```

非法值在装配时抛 `ArgumentException`。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `MessageEnvelope` | CloudEvents 信封：`Id`、`Source`、`Type`（必填）、`Time`、`Subject`、`DataContentType`（缺省 `application/json`）、`Data`、`Extensions`；`WithExtension`、`Extension`、`IsJsonData` |
| `EnvelopeExtensions` | 扩展属性名常量 `Tenant`（`xihantenant`）、`Principal`（`xihanprincipal`）、`Correlation`（`xihancorrelation`）、`Locale`（`xihanlocale`）、`TraceParent`、`PartitionKey`（`xihankey`）；扩展与 Baggage 键的对应 `BaggageMappings`；`IsValidName` |
| `TopicName` | 主题文法 `^[a-z0-9][a-z0-9._-]{0,127}$`；`IsValid`、`Validate`、`MaxLength`（128） |
| `CloudEventsFormat` | 结构化 JSON 编解码：`Encode`、`Decode`（返回 `Result<MessageEnvelope>`）、`ContentType` |
| `IMessagePublisher`、`DirectPublisher` | 应用发布入口（作用域服务）：`PublishAsync`、`PublishManyAsync`，主题不合法逐项返回 `MESSAGING_VALIDATION_001`；在调用内发布时从当前调用补齐缺失的扩展 |
| `IMessageBroker` | Provider 实现的传输：发布、`StartAsync(subscriptions, dispatch, ct)`（每个订阅一份消费）、`StopAsync` |
| `IMessageReplayer` | 流能力：`ReplayAsync(topic, since, ct)` 按时刻重放 |
| `IMessageHandler`、`MessageDelivery`、`MessageDispatch` | 处理器契约；一次投递的主题、信封、尝试号与订阅方式（`Mode`）；代理调用的派发委托 |
| `MessageSubscriptions`、`MessageSubscription`、`SubscriptionMode` | 按（主题，方式）分组的订阅表（`All`、`Topics`、`HandlersFor(topic, mode)`、`Add(topic, handlerType, mode)`）；一个订阅；订阅方式 `Competing` / `Broadcast` |
| `MessageDispatcher` | 把投递变成一次内核调用，`GetDelivery(context)` 取当前投递 |
| `MessagingServiceCollectionExtensions` | `AddMessageHandler<THandler>(topic, mode = Competing)`、`AddDomainEvent<TEvent>(topic, type, info, subject)`、`GetMessageSubscriptions`、`GetDomainEventRegistry` |
| `MessagingOptions`、`MessagingInstance`、`MessagingRegistration`、`MessagingRuntime` | 公共选项（`InstanceId` 只有配置值，没有配置为空）；本实例标识（`Id`，单例，每个宿主一个：配置了 `XiHan:Messaging:InstanceId` 取配置值，否则构造时生成，广播订阅的队列名与消费组名里带它，必须每个同时在线的实例唯一，见[订阅方式](/messaging/pubsub)）；Provider 共用的装配；启停消费 |
| `DomainEventRegistry`、`DomainEventRegistration`、`DomainEventDispatcher` | 领域事件登记表（容量 `DefaultCapacity` 10000）、一种事件的映射（`ToEnvelope`）、派发器 |
| `OutboxFeature`、`OutboxPublisher`、`OutboxDispatcher`、`OutboxMessage`、`OutboxOptions` | 事务性发件箱：特性、发布者、投递循环（`RunOnceAsync`、`RunAsync`）、实体（`NextAttemptAt`、`DispatchedAt`、`DeadLetteredAt`、`Attempts`、`LastError`）、选项（含尝试上限、退避与保留期，`RetryDelayAfter(attempts)`） |
| `MessagingCapabilities`、`MessagingErrors`、`MessagingMessages` | 能力 ID 与版本、错误码、消息键与消息目录 |

## 使用示例

在中间件里区分消息投递与普通请求，给消息调用打上主题与尝试号（`MessageDispatcher.GetDelivery` 只在消息投递的调用里有值）：

```csharp
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Primitives;

public sealed class DeliveryLoggingMiddleware(ILogger<DeliveryLoggingMiddleware> logger) : IPipelineMiddleware
{
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        if (MessageDispatcher.GetDelivery(context).TryGetValue(out var delivery) && delivery.Attempt > 1)
        {
            logger.LogWarning("Retrying {MessageId} on {Topic}, attempt {Attempt}.", delivery.Envelope.Id, delivery.Topic, delivery.Attempt);
        }

        await nextStep(context);
    }
}
```

中间件在特性的 `Configure` 里用 `context.AddMiddleware<DeliveryLoggingMiddleware>(PipelineStage.Observe)` 登记，见[执行管道](/startup/pipeline)。发布、订阅、领域事件与发件箱的完整示例见[消息与事件](/messaging/pubsub)。

## 行为与限制

- 处理器返回失败或抛出异常都算投递失败；同一主题、同一方式的多个处理器按订阅顺序执行，第一个失败即为结果；同一处理器类型以同一方式重复订阅同一主题只记一次。没有处理器的订阅直接成功。竞争订阅失败到上限进死信，广播订阅丢弃。
- 派发不写幂等键，重试必须重新执行处理器；去重由处理器按 `Id` + `Source` 自理。
- 派发器按 `EnvelopeExtensions.BaggageMappings` 把 `xihantenant`、`xihanprincipal`、`xihancorrelation`、`xihanlocale` 放进 Baggage，`traceparent` 成为追踪 ID、父跨度与采样标志（上游「不采样」原样保留）；幂等键不进入消息。
- `DirectPublisher` 与 `OutboxPublisher` 在调用内发布时从当前调用补齐缺失的扩展：`traceparent` 取当前追踪与采样标志（与调用同一 trace 的当前 Activity 优先），租户与主体先看安全上下文再看 Baggage，关联与区域取 Baggage；信封已有的扩展不变，不在调用内时不补。
- `PublishManyAsync`：结果与入参等长同序，空集合不触代理，主题不合法时每一项都是 `MESSAGING_VALIDATION_001`；代理不可达整批抛出 `MESSAGING_UNAVAILABLE_001`。
- 领域事件映射成信封时：`id` 为 UUID v7、`source` 为 `/{topic}`、`time` 为事件发生时间、扩展属性 `xihantenant` 取当前租户，追踪、主体、关联与区域由发布者补齐；全部发布成功后清空缓冲，装配了发件箱时与业务写入同事务。
- 实例标识必须每个同时在线的实例唯一：重复时 Kafka 的广播退化成竞争，RabbitMQ 后启动的实例启动失败。
- 发件箱一次发布抛出时每次记一条 Warning 日志。
- 领域事件：未登记的事件类型让调用以 `MESSAGING_VALIDATION_003` 结束；重复登记或超过 10000 种时 `AddDomainEvent` 抛 `FrameworkException`（`MESSAGING_VALIDATION_004`）；同一主题的事件一次批量发布，任一失败即回滚。
- 发件箱：投递至少一次；按创建时间每轮取 `BatchSize` 条到期的消息；首次投递按主题批量发布，重试逐条单独发布；失败（代理返回失败，或代理抛出，调用方取消除外）按指数退避重试，尝试达到 `MaxAttempts` 或载荷无法解码记 `DeadLetteredAt` 进死信、不再投递，退避中与死信的消息不占批次；一次发布抛出时这次调用的每条计一次尝试、本轮停止发布，余下的留到下一轮（ADR-0108）；宿主停止的一轮回滚、不计尝试；投递成功的行保留 `DispatchedRetention`（缺省 1 天）后删除，死信行不自动删除（ADR-0064）。`OutboxMessage` 需要在关系型 Provider 的模型里有表（`Topic` 长 128、`LastError` 长文本、`NextAttemptAt` 与 `DeadLetteredAt` 可空）；实体上的 `[EntityIndex]` 声明索引 `IX_OutboxMessage_DiAt_DeLeAt_CrAt`（`(DispatchedAt, DeadLetteredAt, CreatedAt)`，取到期消息与保留期清理共用，ADR-0072）。

| 错误码 | 含义 |
| --- | --- |
| `MESSAGING_VALIDATION_001` | 主题不合法 |
| `MESSAGING_VALIDATION_002` | 信封不合法或无法解码 |
| `MESSAGING_VALIDATION_003` | 领域事件类型未登记 |
| `MESSAGING_VALIDATION_004` | 领域事件类型重复登记或登记表已满（以异常抛出） |
| `MESSAGING_UNAVAILABLE_001` | 代理不可达或操作失败 |
| `MESSAGING_UNAVAILABLE_002` | 队列已满 |
| `MESSAGING_UNEXPECTED_001` | 处理器抛出未处理异常（代理侧记录） |

## 相关

- [消息与事件](/messaging/pubsub)
- [Messaging.InMemory](./messaging-inmemory)、[Messaging.RabbitMQ](./messaging-rabbitmq)、[Messaging.Kafka](./messaging-kafka)、[Messaging.Redis](./messaging-redis)、[Protocols.CloudEvents](./protocols-cloudevents)
- [Persistence.Abstractions](./persistence-abstractions)：工作单元与 `IDomainEventDispatcher` 端口
- [Conformance 一致性套件](./conformance)
- 规范：RFC-0009《消息、CloudEvents 信封与发件箱》；ADR-0012、ADR-0019、ADR-0025
