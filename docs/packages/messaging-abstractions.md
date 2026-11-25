# XiHan.Framework.Messaging.Abstractions

消息子系统的抽象包：CloudEvents 信封、发布者与代理契约、处理器订阅、经内核管道的派发、领域事件派发与事务性发件箱。能力 `messaging.queue@1` 与 `messaging.stream@1` 由 Provider 提供；发布或订阅消息、写消息 Provider 时引用它。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Messaging.Abstractions` |
| 环层 | Ring 2（抽象） |
| 能力 | 定义 `messaging.queue@1`、`messaging.stream@1`（`MessagingCapabilities`），本包不提供；`OutboxFeature` 依赖 `kernel.hosting@1`、`messaging.queue@1`、`persistence.relational@1` |
| 特性入口 | `OutboxFeature`（清单 ID `xihan.messaging.outbox`，可选） |
| 配置节 | `XiHan:Messaging`（`MaxAttempts`、`DeadLetterSuffix`、`InstanceId`，由 Provider 读取）、`XiHan:Messaging:Outbox` |
| 依赖 | `XiHan.Framework.Kernel`、`XiHan.Framework.Persistence.Abstractions`、`XiHan.Framework.Localization.Abstractions`、`XiHan.Framework.MultiTenancy.Abstractions`（发件箱投递覆盖分片库）；`Microsoft.Extensions.Configuration.Abstractions`、`Microsoft.Extensions.DependencyInjection.Abstractions`、`Microsoft.Extensions.Logging.Abstractions` |
| AOT | 兼容 |
| 稳定性 | Stable；`IMessageReplayer` 与发件箱（`Outbox` 命名空间）为 Preview |

## 概述

本包把消息定义成跨进程的调用：

- 信封 `MessageEnvelope` 是 CloudEvents 1.0，`CloudEventsFormat` 做结构化 JSON 编解码（`application/cloudevents+json`）。
- 应用经 `IMessagePublisher` 发布，Provider 实现 `IMessageBroker`（发布、按订阅消费、至少一次、重试、死信）；流 Provider 另实现 `IMessageReplayer`。
- 处理器实现 `IMessageHandler`，用 `AddMessageHandler<THandler>(topic, mode)` 订阅；订阅方式 `SubscriptionMode` 缺省 `Competing`（订阅了主题的实例里只有一个收到），`Broadcast` 是每个在线实例各收到一份（尽力而为、不进死信，[订阅方式](/messaging/pubsub)）。`MessageDispatcher` 为每条消息开新作用域、从扩展属性构造调用上下文、经完整管道执行与投递同一方式的处理器。
- Provider 的特性先注册自己的 `IMessageBroker`，再调用 `MessagingRegistration.Configure(context)` 注册选项、派发器、`MessagingRuntime` 与缺省的 `DirectPublisher`，启停时调用 `MessagingRuntime.StartAsync` / `StopAsync`。
- 领域事件：`AddDomainEvent<TEvent>` 登记事件到主题与 CloudEvents `type`（最多 10000 种），本包实现持久化抽象的 `IDomainEventDispatcher`（登记表、映射与派发器是内部类型），在 Transaction 阶段提交前把聚合的事件发成信封。
- 发件箱：`OutboxFeature` 把发布者换成 `OutboxPublisher`，有活动工作单元时写发件箱表，由 `OutboxDispatcher` 在提交后投递。

## 何时使用

- 特性发布或订阅消息：引用本包，在清单里声明 `messaging.queue@1`；运行时用 [Messaging.InMemory](./messaging-inmemory)（随 `Profiles.Standard` 装配）、[Messaging.RabbitMQ](./messaging-rabbitmq)、[Messaging.Kafka](./messaging-kafka) 或 [Messaging.Redis](./messaging-redis)。
- 需要事件与业务数据同事务：再装配本包的 `OutboxFeature`。
- 写新的代理 Provider：实现 `IMessageBroker`，复用 `MessagingRegistration` 与 `MessagingRuntime`，收到消息后用 `MessageDeliveryAttempt.RunAsync` 投递一次、按结论的 `Disposition` 做自己的确认、重投与死信，并通过 [Messaging.Conformance](./conformance) 的 `MessageBrokerConformance`（流 Provider 还有 `MessageStreamConformance`）。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Messaging.Abstractions --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[环境与安装](/getting-started/installation)。订阅写在特性的 `Configure` 里，或直接写在 `builder.Services` 上；发件箱按需装配，接在[第一个应用](/getting-started/first-app)的 `Program.cs` 上：

```csharp
using XiHan.Framework.Messaging.Abstractions.Outbox;

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
| `XiHan:Messaging:Outbox:MaxRetryDelaySeconds` | 正数 | 300 | 重试间隔的上限：翻倍到它之后一直是它，对任何尝试次数都是（`OutboxOptions.RetryDelayAfter`） |
| `XiHan:Messaging:Outbox:DispatchedRetentionSeconds` | 非负数 | 86400 | 投递成功的行保留多久，0 为投递成功即删除 |
| `XiHan:Messaging:Outbox:IncludeShards` | 布尔 | `true` | 装配了租户分片时也投递各分片库里的发件箱行；SqlSugar 把发件箱行留在默认库，可设为 `false` 省去多余的查询 |
| `XiHan:Messaging:Outbox:ShardRefreshIntervalSeconds` | 正数 | 60 | 分片库清单多久从租户目录重读一次，新建的分片库最迟在这之后纳入投递 |
| `XiHan:Messaging:Outbox:HealthProbeIntervalSeconds` | 正数 | 15 | 健康项 `messaging.outbox.backlog` 的探测间隔，也是单次探测的超时 |
| `XiHan:Messaging:Outbox:HealthMaxPending` | 非负整数 | 1000 | 未投递的行数（未投递成功、未进死信，含退避中的，各库相加）超过它时健康项以 `MESSAGING_UNAVAILABLE_004` 降级 |
| `XiHan:Messaging:Outbox:HealthMaxPendingAgeSeconds` | 正数 | 300 | 最老一条未投递的行从创建起等过这么久时健康项以 `MESSAGING_UNAVAILABLE_004` 降级 |
| `XiHan:Messaging:Outbox:HealthMaxDeadLettered` | 非负整数 | 0 | 死信行数超过它时健康项以 `MESSAGING_UNAVAILABLE_005` 降级；缺省 0，有死信就降级 |

```json
{ "XiHan": { "Messaging": { "MaxAttempts": 5, "Outbox": { "PollIntervalSeconds": 0.5 } } } }
```

非法值在装配时抛 `ArgumentException`。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `MessageEnvelope` | CloudEvents 信封：`Id`、`Source`、`Type`（必填）、`Time`、`Subject`、`DataSchema`（CloudEvents 的 `dataschema`，`null` 是没有，空串或只有空白抛 `ArgumentException`，不核对 URI 语法，ADR-0305）、`DataContentType`（缺省 `application/json`）、`Data`、`Extensions`；`WithExtension`、`Extension`、`IsJsonData` |
| `EnvelopeExtensions` | 扩展属性名常量 `Tenant`（`xihantenant`）、`Principal`（`xihanprincipal`）、`Correlation`（`xihancorrelation`）、`Locale`（`xihanlocale`）、`TraceParent`、`PartitionKey`（`xihankey`）；扩展与 Baggage 键的对应 `BaggageMappings`；保留名 `ReservedNames`（CloudEvents 的规范属性名与结构化 JSON 的成员名，按序数升序的只读列表）；`IsValidName`（小写字母与数字、1 到 20 个字符，且不是保留名）。`MessageEnvelope` 的 `Extensions` 与 `WithExtension` 遇到保留名抛 `ArgumentException` |
| `TopicName` | 主题文法 `^[a-z0-9][a-z0-9._-]{0,127}$`；`IsValid`、`Validate`、`MaxLength`（128） |
| `CloudEventsFormat` | 结构化 JSON 编解码：`Encode`、`Decode`（返回 `Result<MessageEnvelope>`）、`ContentType`。`Decode` 先过内核 `JsonText.FindProblem` 的共同检查，再按 RFC-0009「信封」读，规则见下文「行为与限制」；`Encode` 写完自己再过一遍检查，读不回来时抛出 `MESSAGING_VALIDATION_002` |
| `IMessagePublisher`、`DirectPublisher` | 应用发布入口（作用域服务）：`PublishAsync`、`PublishManyAsync`，主题不合法逐项返回 `MESSAGING_VALIDATION_001`；在调用内发布时从当前调用补齐缺失的扩展 |
| `IMessageBroker` | Provider 实现的传输：发布、`StartAsync(subscriptions, dispatch, ct)`（每个订阅一份消费）、`StopAsync` |
| `IMessageReplayer` | 流能力：`ReplayAsync(topic, since, ct)` 按时刻重放 |
| `IMessageHandler`、`MessageDelivery`、`MessageDispatch` | 处理器契约 `HandleAsync(delivery, context)`（取消与截止时间在 `context.CancellationToken` 上，不另收令牌）；一次投递的主题、信封、尝试号与订阅方式（`Mode`）；代理调用的派发委托 |
| `MessageSubscriptions`、`MessageSubscription`、`SubscriptionMode` | 按（主题，方式）分组的订阅表（`All`、`Topics`、`HandlersFor(topic, mode)`、`Add(topic, handlerType, mode)`）；一个订阅；订阅方式 `Competing` / `Broadcast` |
| `MessageDispatcher` | 把投递变成一次内核调用，`FindDelivery(context)` 取当前投递 |
| `MessageDeliveryAttempt` | 代理一侧的一次投递尝试：`RunAsync(dispatch, delivery, options, app, logger, ct)` 等应用进入服务状态、派发一次并给出结论；`Exhausted(delivery, error, logger)` 给出用完尝试次数的结论（竞争进死信、广播丢弃）并记日志；`Undecodable(subscription, error, logger)`（Preview）给出无法解码的消息的结论，去向相同，不派发、不重试，也不等应用进入服务状态 |
| `MessageDeliveryOutcome`、`DeliveryDisposition` | 结论与了结方式：`Complete`（确认）、`Retry`（以下一次尝试号重投）、`DeadLetter`（竞争订阅用完次数）、`Discard`（广播订阅用完次数）、`Abandon`（被停止打断，不了结、不算失败，也是 `default`） |
| `MessagingServiceCollectionExtensions` | `AddMessageHandler<THandler>(topic, mode = Competing)`、`AddDomainEvent<TEvent>(topic, type, info, subject)`、`GetMessageSubscriptions`、`GetDomainEventRegistry` |
| `MessagingOptions`、`MessagingInstance`、`MessagingRegistration`、`MessagingRuntime` | 公共选项（`InstanceId` 只有配置值，没有配置为空）；本实例标识（`Id`，单例，每个宿主一个：配置了 `XiHan:Messaging:InstanceId` 取配置值，否则构造时生成，广播订阅的队列名与消费组名里带它，必须每个同时在线的实例唯一，见[订阅方式](/messaging/pubsub)）；Provider 共用的装配；启停消费 |
| `OutboxFeature`、`OutboxPublisher`、`OutboxDispatcher`、`OutboxMessage`、`OutboxOptions` | 事务性发件箱：特性（常量 `HealthName` = `messaging.outbox.backlog`）、发布者、投递循环（`RunOnceAsync`、`RunAsync`）、实体（`NextAttemptAt`、`DispatchedAt`、`DeadLetteredAt`、`Attempts`、`LastError`）、选项（含尝试上限、退避、保留期与健康阈值，`RetryDelayAfter(attempts)`） |
| `OutboxDeadLetters` | 死信重投（单例，由 `OutboxFeature` 登记）：`RedriveManyAsync(ids, ct)` 把死信行置回待投递，逐项结果与入参同序 |
| `MessagingCapabilities`、`MessagingErrors`、`MessagingMessages` | 能力 ID 与版本、错误码、消息键与消息目录 |

## 使用示例

在中间件里区分消息投递与普通请求，给消息调用打上主题与尝试号（`MessageDispatcher.FindDelivery` 只在消息投递的调用里有值）：

```csharp
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Primitives;

public sealed class DeliveryLoggingMiddleware(ILogger<DeliveryLoggingMiddleware> logger) : IPipelineMiddleware
{
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        if (MessageDispatcher.FindDelivery(context).TryGetValue(out var delivery) && delivery.Attempt > 1)
        {
            logger.LogWarning("Retrying {MessageId} on {Topic}, attempt {Attempt}.", delivery.Envelope.Id, delivery.Topic, delivery.Attempt);
        }

        await nextStep(context);
    }
}
```

中间件在特性的 `Configure` 里用 `context.AddMiddleware<DeliveryLoggingMiddleware>(PipelineStage.Observe)` 登记，见[执行管道](/startup/pipeline)。发布、订阅、领域事件与发件箱的完整示例见[消息与事件](/messaging/pubsub)。

## 行为与限制

- 信封的读取（RFC-0009「信封」，ADR-0283）：`CloudEventsFormat.Decode` 先过 JSON 文本的共同检查，任何一层重复的成员（含 `data` 内部）、孤立的代理项转义都不合法，`messaging.envelope.invalid_json` 的 `reason` 是出错的路径（语法错误为 `$`，重复的成员为它的路径，如 `$.id`）；`time` 按 `TimestampText` 读，必须有 `T`、秒与偏移，读出 UTC 时刻、比 100 纳秒细的小数截断，不合文法即 `messaging.envelope.invalid_time`；`data` 与 `data_base64` 同时出现是 `messaging.envelope.data_conflict`；带 `data_base64` 而不写 `datacontenttype` 时内容类型是 `application/octet-stream`；扩展属性的值是字符串、数字或布尔（数字与布尔保留 JSON 原文），`null` 视为没有这个扩展，对象与数组是 `messaging.envelope.invalid_extension_value`；扩展属性名不合文法或是保留名是 `messaging.envelope.invalid_extension`；`dataschema` 是规范属性（ADR-0305），读成 `DataSchema`、不进扩展属性，空串或只有空白是 `messaging.envelope.invalid`，不是字符串是 `messaging.envelope.invalid_json`（`reason` 为 `$.dataschema`），`Encode` 有则写同名成员、没有则不写。这些都是 `MESSAGING_VALIDATION_002`，经代理收到时按无法解码的消息了结。`Encode` 写出带重复成员或孤立代理项的 JSON 数据、超过 64 层的文档时抛出携带 `MESSAGING_VALIDATION_002` 的 `FrameworkException`。
- 处理器返回失败或抛出异常都算投递失败；同一主题、同一方式的多个处理器按订阅顺序执行，第一个失败即为结果；同一处理器类型以同一方式重复订阅同一主题只记一次。没有处理器的订阅直接成功。竞争订阅失败到上限进死信，广播订阅丢弃。
- 被代理的停止信号打断的投递不算一次失败（ADR-0261）：不计尝试、不重试、不进死信，原消息留给代理重新投递，再到达时的尝试号不因这次放弃而增加，最后一次尝试上被打断的消息照样再处理一次（ADR-0284）；靠代理一侧的计数（投递次数、重投次数）得出尝试号的自写 Provider 要在放弃时把这一次从计数里退掉，来不及退的（进程被强行终止）照旧计入。取消只认停止信号，处理器自己的超时照常是一次失败。原位重试的代理（Kafka、Redis）每次尝试前都等应用进入服务状态，排空期间不接着重试。
- 被管道拒绝的派发不计尝试（ADR-0275）：应用开始排空的那一刻，派发遇到管道入口抛出的 `KERNEL_HOSTING_004`（处理器没有执行）时，投递等 10 毫秒后回到等待，应用再进入服务状态时以原尝试号重新派发，停止信号到来即放弃。只认派发抛出的这个错误码，处理器自己返回的同码失败照常算一次失败；同一次尝试里第 10 次被拒绝按派发抛出的异常计一次失败。
- 无法解码的消息没有信封，不是一次投递：不派发、不重试，竞争订阅把收到的字节原样写进死信，广播订阅丢弃，各记一条 Warning；自写的代理用 `MessageDeliveryAttempt.Undecodable` 取结论，按 `Disposition` 翻译成自己的动作。
- 派发抛出的异常记为 `MESSAGING_UNEXPECTED_001`，`reason` 是异常类型全名（框架异常是它的错误码），完整异常记一条 Warning。
- 派发不写幂等键，重试必须重新执行处理器；去重由处理器按 `Id` + `Source` 自理。
- 派发器按 `EnvelopeExtensions.BaggageMappings` 把 `xihantenant`、`xihanprincipal`、`xihancorrelation`、`xihanlocale` 放进 Baggage，`traceparent` 成为追踪 ID、父跨度与采样标志（上游「不采样」原样保留）；幂等键不进入消息。
- `DirectPublisher` 与 `OutboxPublisher` 在调用内发布时从当前调用补齐缺失的扩展：`traceparent` 取当前追踪与采样标志（与调用同一 trace 的当前 Activity 优先），租户与主体先看安全上下文再看 Baggage，关联与区域取 Baggage；信封已有的扩展不变，不在调用内时不补。
- `PublishManyAsync`：结果与入参等长同序，空集合不触代理，主题不合法时每一项都是 `MESSAGING_VALIDATION_001`；代理不可达整批抛出 `MESSAGING_UNAVAILABLE_001`。
- 领域事件映射成信封时：`id` 为 UUID v7、`source` 为 `/{topic}`、`time` 为事件发生时间、扩展属性 `xihantenant` 取当前租户，追踪、主体、关联与区域由发布者补齐；全部发布成功后清空缓冲，装配了发件箱时与业务写入同事务。
- 实例标识必须每个同时在线的实例唯一：重复时 Kafka 的广播退化成竞争，RabbitMQ 后启动的实例启动失败。
- 发件箱一次发布抛出时每次记一条 Warning 日志。
- 领域事件：未登记的事件类型让调用以 `MESSAGING_VALIDATION_003` 结束；重复登记或超过 10000 种时 `AddDomainEvent` 抛 `FrameworkException`（`MESSAGING_VALIDATION_004`）；同一主题的事件一次批量发布，任一失败即回滚。
- 发件箱：投递至少一次；按创建时间每轮取 `BatchSize` 条到期的消息；首次投递按主题批量发布，重试逐条单独发布；失败（代理返回失败，或代理抛出，调用方取消除外）按指数退避重试，尝试达到 `MaxAttempts` 或载荷无法解码记 `DeadLetteredAt` 进死信、不再投递，退避中与死信的消息不占批次；一次发布抛出时这次调用的每条计一次尝试、本轮停止发布，余下的留到下一轮（ADR-0108）；宿主停止的一轮回滚、不计尝试；投递成功的行保留 `DispatchedRetention`（缺省 1 天）后删除，死信行不自动删除（ADR-0064），排除原因后经 `OutboxDeadLetters` 重投。`OutboxMessage` 需要在关系型 Provider 的模型里有表（`Topic` 长 128、`LastError` 长文本、`NextAttemptAt` 与 `DeadLetteredAt` 可空）；实体上的 `[EntityIndex]` 声明索引 `IX_OutboxMessage_DiAt_DeLeAt_CrAt`（`(DispatchedAt, DeadLetteredAt, CreatedAt)`，取到期消息与保留期清理共用，ADR-0072）。
- 死信重投（ADR-0272）：`OutboxDeadLetters.RedriveManyAsync(ids)` 的逐项结果与入参等长同序。死信行把 `DeadLetteredAt` 置空、`NextAttemptAt` 设为现在、`Attempts` 归零，`LastError` 保留到下一次投递的结果覆盖它，下一轮与首次投递的消息一样按主题成批发出；哪个库里都没有的行是 `MESSAGING_NOTFOUND_001`；不在死信里的行（待投递、退避中或已投递）不改动，是 `MESSAGING_CONFLICT_001`；重复的标识看见前一次的效果；空集合不触存储。覆盖的库与投递循环相同，按默认库、各分片库的顺序查找，全部标识有了着落即停。一个库的存储失败不挡其他库，还有标识没有着落时抛出（多个库失败抛出 `AggregateException`），否则照常返回、只记一条 Warning。
- 发件箱的健康项 `messaging.outbox.backlog`（ADR-0272）：每个探测间隔在默认库与每个分片库（`IncludeShards`）上各开一个只读工作单元，数未投递的行数与最老一条的创建时间、死信行数，各库相加。超过阈值时条目 Degraded：积压是 `MESSAGING_UNAVAILABLE_004`，死信是 `MESSAGING_UNAVAILABLE_005`，都超过时报告积压；恰好等于阈值不算。任何一个库读不出（连不上、没有发件箱表、探测超时）时条目以 `KERNEL_UNAVAILABLE_005` 降级，不报告不完整的数字。条目的数据是 `pending`、`oldestPendingSeconds`、`deadLettered`、`databases`。条目降级时应用报告 Degraded，照常接收调用，就绪探针不受影响。SqlSugar 把发件箱行留在默认库，装配了租户分片时把 `IncludeShards` 设为 `false`，否则默认库的行按库数重复计数。

## 错误码

常量在 `MessagingErrors`：

| 错误码 | 含义 |
| --- | --- |
| `MESSAGING_VALIDATION_001` | 主题不合法，发布者逐项返回 |
| `MESSAGING_VALIDATION_002` | 信封不合法或无法解码 |
| `MESSAGING_VALIDATION_003` | 领域事件类型没有用 `AddDomainEvent` 登记，调用以它结束并回滚 |
| `MESSAGING_VALIDATION_004` | 领域事件类型重复登记或登记表已满（装配时以 `FrameworkException` 抛出） |
| `MESSAGING_UNAVAILABLE_001` | 代理不可达或操作失败；发布时以 `FrameworkException` 抛出，重放时作为失败结果返回 |
| `MESSAGING_UNAVAILABLE_002` | 队列已满：内存代理的主题通道排满时发布返回（批量整批返回，不写入半批） |
| `MESSAGING_UNAVAILABLE_004` | 发件箱的积压超过健康阈值：未投递的行数或最老一条的等待时间（健康项 `messaging.outbox.backlog`） |
| `MESSAGING_UNAVAILABLE_005` | 发件箱的死信行数超过健康阈值（健康项 `messaging.outbox.backlog`） |
| `MESSAGING_NOTFOUND_001` | 发件箱消息不存在：重投死信时哪个库里都没有这一行 |
| `MESSAGING_CONFLICT_001` | 发件箱消息不在死信里：重投只对死信行，待投递、退避中或已投递的行不改动 |
| `MESSAGING_UNEXPECTED_001` | 处理器抛出未处理异常，代理记为一次失败的投递 |

错误怎样变成 HTTP 状态码与 Problem Details 见[结果与错误处理](/web/errors)。

## 相关

- [消息与事件](/messaging/pubsub)
- [Messaging.InMemory](./messaging-inmemory)、[Messaging.RabbitMQ](./messaging-rabbitmq)、[Messaging.Kafka](./messaging-kafka)、[Messaging.Redis](./messaging-redis)、[Protocols.CloudEvents](./protocols-cloudevents)
- [Persistence.Abstractions](./persistence-abstractions)：工作单元与 `IDomainEventDispatcher` 端口
- [Conformance 一致性套件](./conformance)
- 规范：RFC-0009《消息、CloudEvents 信封与发件箱》；ADR-0012、ADR-0019、ADR-0025、ADR-0272、ADR-0275
