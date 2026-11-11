# XiHan.Framework.Messaging.RabbitMQ

`messaging.queue@1` 的 RabbitMQ Provider，基于 RabbitMQ.Client 7：发布到一个 direct 交换机，订阅方为每个主题声明持久队列并绑定，手动确认、重试与死信队列。适用于多实例部署下的工作队列。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Messaging.RabbitMQ` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `messaging.queue@1`（`role: provider`）；依赖 `kernel.hosting@1` |
| 特性入口 | `RabbitMQMessagingFeature`（清单 ID `xihan.messaging.rabbitmq`） |
| 配置节 | `XiHan:Messaging:RabbitMQ`、`XiHan:Messaging`；可选绑定 `queue`（类型 `queue`） |
| 依赖 | `XiHan.Framework.Messaging.Abstractions`；`RabbitMQ.Client` |
| AOT | 不兼容（`IsAotCompatible=false`，RabbitMQ.Client 未声明 AOT 兼容） |
| 稳定性 | Stable |

## 概述

`RabbitMQMessageBroker` 实现 `IMessageBroker`。装配后它顶替内存代理，处理器、发布者与发件箱的代码不变。

- 发布方只声明交换机（ADR-0079）：消息发到持久的 direct 交换机 `Exchange`（缺省 `xihan`），路由键 `<QueuePrefix><topic>`（前缀缺省 `xihan.`）。发布方不声明队列。
- 队列只由订阅方声明：订阅方启动时声明持久队列 `<QueuePrefix><topic>`，以同名路由键绑定到交换机。订阅方第一次启动之后，它离线期间的消息留在队列里；第一次启动之前发布的消息没有队列可进。
- 消息持久化投递，`content_type` 为 `application/cloudevents+json`，`message_id` 为事件 ID，消息体是 CloudEvents 结构化 JSON。
- 发布共用一条发布通道，用信号量串行化；通道开启发布确认并由客户端跟踪，消息以 `mandatory` 发出，代理确认后发布才返回成功，代理拒绝（nack）返回 `MESSAGING_UNAVAILABLE_001`。批量发布在这条通道上连续写出整批（一次持锁），再一次等待全部确认，结果逐项对应。
- 没有订阅者：消息被退回（路由键上没有队列绑定）时丢弃，发布照常成功（批量逐项成功），记一条 Debug 日志，与内存代理一致；发件箱不为它重试，也不记进死信。
- 广播订阅（[订阅方式](/messaging/pubsub)）：每个实例声明独占、非持久、随连接删除的队列 `<QueuePrefix><topic>@<InstanceId>`，以主题的路由键绑定到交换机，交换机给竞争队列与每个实例的广播队列各一份。实例与代理断开时队列随连接删除（客户端自动恢复连接时重新声明），断开期间的消息收不到；重试定向发回本实例的广播队列，到上限或无法解码时确认掉并记 Warning 日志，不进死信。实例下线不留下队列。队列名里的实例标识缺省每次启动生成；配了 `XiHan:Messaging:InstanceId` 时取配置值，两个同时在线的实例配了同一个值，后启动的那个声明同名独占队列被代理拒绝（`RESOURCE_LOCKED`），启动失败（`MESSAGING_UNAVAILABLE_001`），见[订阅方式](/messaging/pubsub)。
- 每个订阅（主题加方式）一个通道与一个消费者，预取 `PrefetchCount`（缺省 16），手动确认，单线程派发。
- 处理失败：尝试次数（消息头 `x-xihan-attempt`）小于 `MaxAttempts` 时经默认交换机重新发布到所消费队列的队尾，否则发布到死信队列 `<QueuePrefix><topic><DeadLetterSuffix>`（由死信的一方声明为持久队列并绑定到交换机，即主题加后缀的竞争队列）；重新发布同样等确认，确认了才确认原消息，未被确认、被退回（队列已不存在）或发布失败时把原消息 nack 并重新入队，由 RabbitMQ 再投递。无法解码的消息不重试，原样进死信队列（同样等确认）。
- 停止时取消消费者；正在处理的消息不确认，连接关闭后由 RabbitMQ 重新投递。

启动时先建立连接与发布通道、再开始消费，连不上时启动失败；停止时先停健康探针与消费，再释放连接。

## 何时使用

- 多实例部署的工作队列：至少一次、按主题分摊给各实例的消费者；每个实例都要处理的通知用广播订阅。
- 需要同一主题内严格有序、或按时刻重放：用 [Messaging.Kafka](./messaging-kafka)（重试在原位，不打乱顺序）。
- 单进程、开发与测试：[Messaging.InMemory](./messaging-inmemory)。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Messaging.RabbitMQ --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[快速上手](/getting-started/installation)。

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Messaging.RabbitMQ;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<RabbitMQMessagingFeature>());
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Messaging:RabbitMQ:Configuration` | 字符串 | — | AMQP URI（`amqp://` 或 `amqps://`）；有绑定 `queue` 时被绑定的主值覆盖，两者都没有时装配失败；绑定的属性 `username` / `password` 覆盖 URI 里的用户名与密码（连接时设置，不改写 URI，特殊字符不必转义） |
| `XiHan:Messaging:RabbitMQ:QueuePrefix` | 字符串 | `xihan.` | 队列名与路由键的前缀 |
| `XiHan:Messaging:RabbitMQ:Exchange` | 字符串 | `xihan` | 发布用的 direct 交换机，不能为空、不能以 `amq.` 开头；发布方与订阅方要一致 |
| `XiHan:Messaging:RabbitMQ:PrefetchCount` | 1–65535 | 16 | 每个消费者的预取数 |
| `XiHan:Messaging:RabbitMQ:HealthProbeIntervalSeconds` | 正数 | 15 | 健康项的重探间隔，也是单次探测的超时 |
| `XiHan:Messaging:MaxAttempts` | 正整数 | 3 | 最多投递次数（含首次） |
| `XiHan:Messaging:DeadLetterSuffix` | 字符串 | `.dlq` | 死信队列后缀 |
| `XiHan:Messaging:InstanceId` | 字符串 | 不写，每次启动生成 | 广播队列名里的实例标识；必须每个同时在线的实例唯一，重复时后启动的实例启动失败 |

```json
{
  "XiHan": {
    "Messaging": {
      "MaxAttempts": 5,
      "RabbitMQ": {
        "Configuration": "amqp://guest:guest@localhost:5672",
        "QueuePrefix": "orders.",
        "PrefetchCount": 32
      }
    }
  }
}
```

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `RabbitMQMessagingFeature` | 特性入口（`IHostedFeature`）；常量 `ConnectionBinding`（`queue`）、`HealthName`（`messaging.rabbitmq.connection`） |
| `RabbitMQMessagingOptions` | `Configuration`、`QueuePrefix`、`Exchange`、`PrefetchCount`、`HealthProbeInterval`；`FromConfiguration(configuration, binding)` |
| `RabbitMQMessageBroker` | 代理；连接与健康探测由特性在内部调用 |

## 使用示例

处理器与内存代理下完全相同；下面的处理器对外部依赖暂时不可用的情况返回失败，交给 RabbitMQ 重试，并在最后一次尝试时记一条日志（之后进死信队列 `xihan.payments.dlq`）：

```csharp
using Microsoft.Extensions.Logging;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Primitives;

// 应用自己的下游网关：成功，或带错误码的失败
public interface IPaymentGateway
{
    ValueTask<Result> ForwardAsync(MessageEnvelope envelope, CancellationToken cancellationToken);
}

public sealed class PaymentCapturedHandler(IPaymentGateway gateway, MessagingOptions options, ILogger<PaymentCapturedHandler> logger) : IMessageHandler
{
    public async ValueTask<Result> HandleAsync(MessageDelivery delivery, InvocationContext context, CancellationToken cancellationToken)
    {
        var outcome = await gateway.ForwardAsync(delivery.Envelope, cancellationToken);
        if (outcome.IsFailure && delivery.Attempt >= options.MaxAttempts)
        {
            logger.LogError("Payment event {MessageId} goes to the dead-letter queue after {Attempt} attempts.", delivery.Envelope.Id, delivery.Attempt);
        }

        // 返回失败即交给代理：未到上限重新发布到队尾，到了上限进死信队列
        return outcome;
    }
}
```

```csharp
context.Services.AddMessageHandler<PaymentCapturedHandler>("payments");
```

`MessagingOptions` 由 `MessagingRegistration.Configure` 注册为单例，可以注入。

## 行为与限制

- 连接与通道异常抛出携带 `MESSAGING_UNAVAILABLE_001` 的 `FrameworkException`，代理拒绝的发布返回同一错误码的失败结果（批量逐项）；没有订阅者的发布成功并丢弃；单条发布遇到不合法主题抛 `ArgumentException`（发布者已先校验，代理收到不合法主题是装配错误）。
- 投递是至少一次：失败时先重新发布再确认原消息，极端情况下会重复投递；处理器按 `Id` + `Source` 去重。
- 重试排到队尾，同一队列后面的消息先于重试被处理；重试之间不等待。
- 发布成功表示代理已确认收下（持久队列上的持久消息已落盘），或者没有任何订阅者、消息已丢弃。
- 交换机被外部删除时发布通道以通道错误关闭，健康项转为 Degraded，要重启实例重新声明。
- 测试：发布确认、拓扑与重新发布的确认用打桩的连接与通道覆盖，不需要 RabbitMQ；一致性套件与拓扑用例需要真实 RabbitMQ，设置环境变量 `XIHAN_TEST_RABBITMQ`（如 `amqp://guest:guest@localhost:5672`）后运行，未设置时跳过。

| 健康项 | 状态 |
| --- | --- |
| `messaging.rabbitmq.connection` | 启动后按 `HealthProbeIntervalSeconds` 在后台检查连接与发布通道是否打开并缓存结果；断开时 Degraded（`MESSAGING_UNAVAILABLE_001`，`reason` 是代理给出的关闭原因），应用仍在服务 |

## 升级说明

- 滚动升级：发布方已是新版本、订阅方仍是旧版本的那段时间里，新版本发出的消息没有绑定可走，按「没有订阅者」丢弃。先升级订阅方，或让一个新版本的订阅实例先启动一次建好绑定；同一个应用的实例既发布又订阅，第一个新版本实例启动即建好绑定。
- 没有订阅者的主题队列（如 `xihan.scheduling.job.started` 等 `scheduling.job.*`）：收不到新消息，框架也不删除它们，已经堆积的消息留在原处。确认没有消费者后由运维删除：`rabbitmqctl list_queues name messages consumers` 找出 `consumers` 为 0 的框架主题队列，再 `rabbitmqctl delete_queue <队列>`（或在管理界面删除）。
- 订阅方每次启动都声明队列并绑定到交换机：已有的同名队列照常可用，积压照常消费。发布方与订阅方的 `QueuePrefix`、`Exchange` 要一致，否则消息到不了订阅方的队列。
- 凭据不进日志与错误消息。
- 希望订阅方上线之前就留存消息的部署，由运维预先声明持久队列 `<QueuePrefix><topic>` 并以同名路由键绑定到交换机。

## 相关

- [消息与事件](/messaging/pubsub)
- [Messaging.Abstractions](./messaging-abstractions)、[Messaging.InMemory](./messaging-inmemory)、[Messaging.Kafka](./messaging-kafka)、[Messaging.Redis](./messaging-redis)
- [部署与打包](/deployment/pack)：绑定 `queue` 的来源
- 规范：RFC-0009《消息、CloudEvents 信封与发件箱》
