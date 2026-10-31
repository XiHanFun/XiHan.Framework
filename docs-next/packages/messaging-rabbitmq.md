# XiHan.Framework.Messaging.RabbitMQ

`messaging.queue@1` 的 RabbitMQ Provider，基于 RabbitMQ.Client 7：每个主题一个持久队列，手动确认、重试与死信队列。适用于多实例部署下的工作队列。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Messaging.RabbitMQ` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `messaging.queue@1`（`role: provider`）；依赖 `kernel.hosting@1` |
| 特性入口 | `RabbitMQMessagingFeature`（清单 ID `xihan.messaging.rabbitmq`） |
| 配置节 | `XiHan:Messaging:RabbitMQ`、`XiHan:Messaging`；可选绑定 `queue`（类型 `queue`） |
| 依赖 | `XiHan.Framework.Messaging.Abstractions`；`RabbitMQ.Client` 7.2.2 |
| AOT | 不兼容（`IsAotCompatible=false`，RabbitMQ.Client 未声明 AOT 兼容） |
| 稳定性 | Preview |

## 概述

`RabbitMQMessageBroker` 实现 `IMessageBroker`。装配后它顶替内存代理，处理器、发布者与发件箱的代码不变。

- 主题映射为持久队列 `<QueuePrefix><topic>`（前缀缺省 `xihan.`），经默认交换机直接路由；队列在第一次发布或消费时声明。
- 消息持久化投递，`content_type` 为 `application/cloudevents+json`，`message_id` 为事件 ID，消息体是 CloudEvents 结构化 JSON。
- 发布共用一条发布通道，用信号量串行化；批量发布在这条通道上连续发布，一次持锁、一次声明队列。
- 每个订阅主题一个通道与一个消费者，预取 `PrefetchCount`（缺省 16），手动确认，单线程派发。
- 处理失败：尝试次数（消息头 `x-xihan-attempt`）小于 `MaxAttempts` 时重新发布到队尾，否则发布到死信队列 `<QueuePrefix><topic><DeadLetterSuffix>`，之后都确认原消息；无法解码的消息不重试，原样进死信队列。
- 停止时取消消费者；正在处理的消息不确认，连接关闭后由 RabbitMQ 重新投递。

启动时先建立连接与发布通道、再开始消费，连不上时启动失败；停止时先停健康探针与消费，再释放连接。

## 何时使用

- 多实例部署的工作队列：至少一次、按主题分摊给各实例的消费者。
- 需要同一主题内严格有序、或按时刻重放：用 [Messaging.Kafka](./messaging-kafka)（重试在原位，不打乱顺序）。
- 单进程、开发与测试：[Messaging.InMemory](./messaging-inmemory)。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Messaging.RabbitMQ" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。

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
| `XiHan:Messaging:RabbitMQ:Configuration` | 字符串 | — | AMQP URI（`amqp://` 或 `amqps://`）；优先于绑定 `queue` 的值，两者都没有时装配失败 |
| `XiHan:Messaging:RabbitMQ:QueuePrefix` | 字符串 | `xihan.` | 队列名前缀 |
| `XiHan:Messaging:RabbitMQ:PrefetchCount` | 1–65535 | 16 | 每个消费者的预取数 |
| `XiHan:Messaging:RabbitMQ:HealthProbeIntervalSeconds` | 正数 | 15 | 健康项的重探间隔，也是单次探测的超时 |
| `XiHan:Messaging:MaxAttempts` | 正整数 | 3 | 最多投递次数（含首次） |
| `XiHan:Messaging:DeadLetterSuffix` | 字符串 | `.dlq` | 死信队列后缀 |

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
| `RabbitMQMessagingOptions` | `Configuration`、`QueuePrefix`、`PrefetchCount`、`HealthProbeInterval`；`FromConfiguration(configuration, binding)` |
| `RabbitMQMessageBroker` | 代理；`ConnectAsync`、`IsConnected`、`CheckConnection()`（健康项用） |

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

- 连接、通道与发布异常抛出携带 `MESSAGING_UNAVAILABLE_001` 的 `FrameworkException`；单条发布遇到不合法主题抛 `ArgumentException`（发布者已先校验，代理收到不合法主题是装配错误）。
- 投递是至少一次：失败时先重新发布再确认原消息，极端情况下会重复投递；处理器按 `Id` + `Source` 去重。
- 重试排到队尾，同一队列后面的消息先于重试被处理；重试之间不等待。
- 发布通道没有开启发布确认，发布成功表示消息已写出到连接。
- 测试：一致性套件需要真实 RabbitMQ，设置环境变量 `XIHAN_TEST_RABBITMQ`（如 `amqp://guest:guest@localhost:5672`）后运行，未设置时跳过。

| 健康项 | 状态 |
| --- | --- |
| `messaging.rabbitmq.connection` | 启动后按 `HealthProbeIntervalSeconds` 在后台检查连接与发布通道是否打开并缓存结果；断开时 Degraded（`MESSAGING_UNAVAILABLE_001`，`reason` 是代理给出的关闭原因），应用仍在服务 |

## 相关

- [消息与事件](../guide/messaging)
- [Messaging.Abstractions](./messaging-abstractions)、[Messaging.InMemory](./messaging-inmemory)、[Messaging.Kafka](./messaging-kafka)
- [部署与打包](../guide/deployment)：绑定 `queue` 的来源
- 规范：RFC-0009《消息、CloudEvents 信封与发件箱》
