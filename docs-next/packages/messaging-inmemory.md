# XiHan.Framework.Messaging.InMemory

消息的默认实现：进程内的内存代理，每个主题一条有界通道、单消费者顺序投递、重试与死信，提供 `messaging.queue@1`。适用于单进程部署、开发与测试，进程退出即丢失未投递的消息。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Messaging.InMemory` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `messaging.queue@1`（`role: default`）；依赖 `kernel.hosting@1` |
| 特性入口 | `InMemoryMessagingFeature`（清单 ID `xihan.messaging.inmemory`） |
| 配置节 | `XiHan:Messaging:InMemory`、`XiHan:Messaging` |
| 依赖 | `XiHan.Framework.Messaging.Abstractions`；无第三方依赖 |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

`InMemoryMessageBroker` 实现 `IMessageBroker`，并且自己就是健康项的贡献者：

- 每个主题在首次发布或消费时创建一条有界通道（容量 `MaxQueuedMessages`）。发布不等待：通道满时立即返回 `MESSAGING_UNAVAILABLE_002`。批量发布先按剩余容量整批判定，装不下就一条都不写。
- 应用启动时为每个订阅了的主题起一个消费循环，按入队顺序逐条交给 `MessageDispatcher`；应用进入服务状态之前不投递，启动前发布的消息留在通道里，启动后投递。
- 失败按 `XiHan:Messaging:MaxAttempts` 以下一次尝试号重新入队到队尾；达到上限、或重新入队时通道已满，进入该主题的死信列表。死信列表同样有界，超过 `MaxQueuedMessages` 时丢弃最旧的。
- 停止时取消消费循环，把正在处理的那条放回通道，并等待循环结束。

特性注册 `InMemoryMessageBroker` 与 `IMessageBroker`，再经 `MessagingRegistration.Configure` 注册派发器、运行时与缺省的 `DirectPublisher`；启动时开始消费，停止时结束。

## 何时使用

- 单进程部署、开发与测试；`Profiles.Standard` 已经装配它。
- 多实例部署或需要持久化的消息：换 [Messaging.RabbitMQ](./messaging-rabbitmq) 或 [Messaging.Kafka](./messaging-kafka)；处理器与发布代码不变。
- 需要按分区键有序或按时刻重放：本包不提供 `messaging.stream`，用 Kafka。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Messaging.InMemory" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。`Profiles.Standard` 已经装配本包；不用组合包时单独装配：

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Messaging.InMemory;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseFeature<InMemoryMessagingFeature>());
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Messaging:InMemory:MaxQueuedMessages` | 正整数 | 10000 | 每个主题的通道容量，也是该主题死信列表的容量 |
| `XiHan:Messaging:MaxAttempts` | 正整数 | 3 | 最多投递次数（含首次） |

```json
{ "XiHan": { "Messaging": { "MaxAttempts": 5, "InMemory": { "MaxQueuedMessages": 50000 } } } }
```

本包不使用 `DeadLetterSuffix`：死信留在内存列表里，不发布到死信主题。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `InMemoryMessagingFeature` | 特性入口（`IHostedFeature`）；常量 `HealthName` 为 `messaging.inmemory.capacity` |
| `InMemoryMessagingOptions` | `MaxQueuedMessages`；`FromConfiguration` |
| `InMemoryMessageBroker` | 内存代理；诊断方法 `QueuedCount(topic)`、`DeliveredCount(topic)`、`DeadLetters(topic)`，健康检查 `Check()` |

## 使用示例

测试里检查某个主题的死信，确认处理器的失败按预期进了死信列表：

```csharp
using XiHan.Framework.Messaging.InMemory;

public sealed class DeadLetterProbe(InMemoryMessageBroker broker)
{
    // 按进入死信的顺序返回事件 ID 与最后一次尝试号
    public IReadOnlyList<(string Id, int Attempt)> Of(string topic)
        => [.. broker.DeadLetters(topic).Select(static d => (d.Envelope.Id, d.Attempt))];

    public bool IsDrained(string topic) => broker.QueuedCount(topic) == 0;
}
```

`InMemoryMessageBroker` 作为具体类型注册在容器里，可以直接注入。

## 行为与限制

- 只在单进程内有效：进程退出丢失通道与死信里的全部消息，多实例之间不共享。
- 只消费订阅了的主题；发布到没有订阅的主题时消息一直留在通道里，排满后发布返回 `MESSAGING_UNAVAILABLE_002`。
- 每个主题一个消费者，处理器串行执行；重试排到队尾，同一主题后面的消息先于重试被处理。
- 重试之间不等待；处理器抛出的异常记为 `MESSAGING_UNEXPECTED_001`，与返回失败同样计一次尝试。

| 健康项 | 状态 |
| --- | --- |
| `messaging.inmemory.capacity` | 任一主题的通道排满时 Degraded（`MESSAGING_UNAVAILABLE_002`，`topic` 是按序数顺序的第一个满主题），否则 Ready；同步计算 |

## 相关

- [消息与事件](../guide/messaging)
- [Messaging.Abstractions](./messaging-abstractions)、[Messaging.RabbitMQ](./messaging-rabbitmq)、[Messaging.Kafka](./messaging-kafka)
- [Profiles.Standard](./profiles-standard)
- 规范：RFC-0009《消息、CloudEvents 信封与发件箱》
