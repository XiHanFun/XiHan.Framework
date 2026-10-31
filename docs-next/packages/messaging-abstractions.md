# XiHan.Framework.Messaging.Abstractions

消息子系统的抽象包：CloudEvents 信封、发布者与代理契约、处理器订阅、经内核管道的派发、领域事件派发与事务性发件箱。能力 `messaging.queue@1` 与 `messaging.stream@1` 由 Provider 提供；发布或订阅消息、写消息 Provider 时引用它。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Messaging.Abstractions` |
| 环层 | Ring 2（抽象） |
| 能力 | 定义 `messaging.queue@1`、`messaging.stream@1`（`MessagingCapabilities`），本包不提供；`OutboxFeature` 依赖 `kernel.hosting@1`、`messaging.queue@1`、`persistence.relational@1` |
| 特性入口 | `OutboxFeature`（清单 ID `xihan.messaging.outbox`，可选） |
| 配置节 | `XiHan:Messaging`（`MaxAttempts`、`DeadLetterSuffix`，由 Provider 读取）、`XiHan:Messaging:Outbox` |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Persistence.Abstractions`、`XiHan.Framework.Localization.Abstractions`；`Microsoft.Extensions.Configuration.Abstractions`、`Microsoft.Extensions.DependencyInjection.Abstractions`、`Microsoft.Extensions.Logging.Abstractions` 10.0.13 |
| AOT | 兼容 |
| 稳定性 | Stable；`IMessageReplayer` 与领域事件类型（`DomainEventRegistry`、`DomainEventRegistration`、`DomainEventDispatcher`）为 Preview |

## 概述

本包把消息定义成跨进程的调用：

- 信封 `MessageEnvelope` 是 CloudEvents 1.0，`CloudEventsFormat` 做结构化 JSON 编解码（`application/cloudevents+json`）。
- 应用经 `IMessagePublisher` 发布，Provider 实现 `IMessageBroker`（发布、按主题消费、至少一次、重试、死信）；流 Provider 另实现 `IMessageReplayer`。
- 处理器实现 `IMessageHandler`，用 `AddMessageHandler<THandler>(topic)` 订阅；`MessageDispatcher` 为每条消息开新作用域、从扩展属性构造调用上下文、经完整管道执行处理器。
- Provider 的特性先注册自己的 `IMessageBroker`，再调用 `MessagingRegistration.Configure(context)` 注册选项、派发器、`MessagingRuntime` 与缺省的 `DirectPublisher`，启停时调用 `MessagingRuntime.StartAsync` / `StopAsync`。
- 领域事件：`AddDomainEvent<TEvent>` 登记事件到主题与 CloudEvents `type`，`DomainEventDispatcher` 实现持久化抽象的 `IDomainEventDispatcher`，在 Transaction 阶段提交前把聚合的事件发成信封。
- 发件箱：`OutboxFeature` 把发布者换成 `OutboxPublisher`，有活动工作单元时写发件箱表，由 `OutboxDispatcher` 在提交后投递。

## 何时使用

- 特性发布或订阅消息：引用本包，在清单里声明 `messaging.queue@1`；运行时用 [Messaging.InMemory](./messaging-inmemory)（随 `Profiles.Standard` 装配）、[Messaging.RabbitMQ](./messaging-rabbitmq) 或 [Messaging.Kafka](./messaging-kafka)。
- 需要事件与业务数据同事务：再装配本包的 `OutboxFeature`。
- 写新的代理 Provider：实现 `IMessageBroker`，复用 `MessagingRegistration` 与 `MessagingRuntime`，并通过 [Messaging.Conformance](./conformance) 的 `MessageBrokerConformance`（流 Provider 还有 `MessageStreamConformance`）。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Messaging.Abstractions" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。订阅写在特性的 `Configure` 里，或直接写在 `builder.Services` 上；发件箱按需装配：

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
| `XiHan:Messaging:Outbox:PollIntervalSeconds` | 正数 | 1 | 发件箱投递循环的轮询间隔 |
| `XiHan:Messaging:Outbox:BatchSize` | 正整数 | 100 | 发件箱每轮最多投递条数 |

```json
{ "XiHan": { "Messaging": { "MaxAttempts": 5, "Outbox": { "PollIntervalSeconds": 0.5 } } } }
```

非法值在装配时抛 `ArgumentException`。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `MessageEnvelope` | CloudEvents 信封：`Id`、`Source`、`Type`（必填）、`Time`、`Subject`、`DataContentType`（缺省 `application/json`）、`Data`、`Extensions`；`WithExtension`、`Extension`、`IsJsonData` |
| `EnvelopeExtensions` | 扩展属性名常量 `Tenant`（`xihantenant`）、`Correlation`（`xihancorrelation`）、`TraceParent`、`PartitionKey`（`xihankey`）；`IsValidName` |
| `TopicName` | 主题文法 `^[a-z0-9][a-z0-9._-]{0,127}$`；`IsValid`、`Validate`、`MaxLength`（128） |
| `CloudEventsFormat` | 结构化 JSON 编解码：`Encode`、`Decode`（返回 `Result<MessageEnvelope>`）、`ContentType` |
| `IMessagePublisher`、`DirectPublisher` | 应用发布入口（作用域服务）：`PublishAsync`、`PublishManyAsync`，主题不合法逐项返回 `MESSAGING_VALIDATION_001` |
| `IMessageBroker` | Provider 实现的传输：发布、`StartAsync(topics, dispatch, ct)`、`StopAsync` |
| `IMessageReplayer` | 流能力：`ReplayAsync(topic, since, ct)` 按时刻重放 |
| `IMessageHandler`、`MessageDelivery`、`MessageDispatch` | 处理器契约；一次投递的主题、信封与尝试号；代理调用的派发委托 |
| `MessageSubscriptions`、`MessageDispatcher` | 主题到处理器类型的订阅表；把投递变成一次内核调用，`GetDelivery(context)` 取当前投递 |
| `MessagingServiceCollectionExtensions` | `AddMessageHandler<THandler>(topic)`、`AddDomainEvent<TEvent>(topic, type, info, subject)`、`GetMessageSubscriptions`、`GetDomainEventRegistry` |
| `MessagingOptions`、`MessagingRegistration`、`MessagingRuntime` | 公共选项、Provider 共用的装配、启停消费 |
| `DomainEventRegistry`、`DomainEventRegistration`、`DomainEventDispatcher` | 领域事件登记表（容量 `DefaultCapacity` 10000）、一种事件的映射（`ToEnvelope`）、派发器 |
| `OutboxFeature`、`OutboxPublisher`、`OutboxDispatcher`、`OutboxMessage`、`OutboxOptions` | 事务性发件箱：特性、发布者、投递循环（`RunOnceAsync`、`RunAsync`）、实体、选项 |
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

中间件在特性的 `Configure` 里用 `context.AddMiddleware<DeliveryLoggingMiddleware>(PipelineStage.Observe)` 登记，见[执行管道](../guide/pipeline)。发布、订阅、领域事件与发件箱的完整示例见[消息与事件](../guide/messaging)。

## 行为与限制

- 处理器返回失败或抛出异常都算投递失败；同一主题的多个处理器按订阅顺序执行，第一个失败即为结果；同一处理器类型重复订阅同一主题只记一次。没有处理器的主题直接成功。
- 派发不写幂等键，重试必须重新执行处理器；去重由处理器按 `Id` + `Source` 自理。
- 派发器只把 `xihantenant`、`xihancorrelation` 放进 Baggage，`traceparent` 成为追踪 ID 与父跨度；发布者不自动从当前调用补这些扩展属性。
- `PublishManyAsync`：结果与入参等长同序，空集合不触代理，主题不合法时每一项都是 `MESSAGING_VALIDATION_001`；代理不可达整批抛出 `MESSAGING_UNAVAILABLE_001`。
- 领域事件：未登记的事件类型让调用以 `MESSAGING_VALIDATION_003` 结束；重复登记或超过 10000 种时 `AddDomainEvent` 抛 `FrameworkException`（`MESSAGING_VALIDATION_004`）；同一主题的事件一次批量发布，任一失败即回滚。
- 发件箱：投递至少一次；按创建时间每轮取 `BatchSize` 条未投递的消息；没有尝试上限，失败的消息每轮重试；投递成功的行不删除。`OutboxMessage` 需要在关系型 Provider 的模型里有表。

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

- [消息与事件](../guide/messaging)
- [Messaging.InMemory](./messaging-inmemory)、[Messaging.RabbitMQ](./messaging-rabbitmq)、[Messaging.Kafka](./messaging-kafka)、[Protocols.CloudEvents](./protocols-cloudevents)
- [Persistence.Abstractions](./persistence-abstractions)：工作单元与 `IDomainEventDispatcher` 端口
- [Conformance 一致性套件](./conformance)
- 规范：RFC-0009《消息、CloudEvents 信封与发件箱》；ADR-0012、ADR-0019、ADR-0025
