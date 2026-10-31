# XiHan.Framework.Messaging.Kafka

`messaging.queue@1` 与 `messaging.stream@1` 的 Kafka Provider，基于 Confluent.Kafka：幂等生产者、按消费组提交位点、分区键有序、按时刻重放、重试与死信主题。适用于需要同键有序、可重放事件流的多实例部署。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Messaging.Kafka` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `messaging.queue@1`、`messaging.stream@1`（`role: provider`）；依赖 `kernel.hosting@1` |
| 特性入口 | `KafkaMessagingFeature`（清单 ID `xihan.messaging.kafka`） |
| 配置节 | `XiHan:Messaging:Kafka`、`XiHan:Messaging`；可选绑定 `stream`（类型 `stream`） |
| 依赖 | `XiHan.Framework.Messaging.Abstractions`；`Confluent.Kafka` 2.16.0（自带 librdkafka） |
| AOT | 不兼容（`IsAotCompatible=false`，librdkafka 是本机库） |
| 稳定性 | Preview |

## 概述

`KafkaMessageBroker` 同时实现 `IMessageBroker` 与 `IMessageReplayer`。特性只为被选中的能力绑定接口：选中 `messaging.queue` 时登记 `IMessageBroker`，选中 `messaging.stream` 时登记 `IMessageReplayer`。

- 主题映射为 `<TopicPrefix><topic>`（前缀缺省 `xihan.`）。消息值是 CloudEvents 结构化 JSON，头 `content-type` 为 `application/cloudevents+json`、`x-xihan-attempt` 为尝试次数。
- 消息键取信封扩展属性 `xihankey`（`EnvelopeExtensions.PartitionKey`）：同键进同一分区，按发布顺序投递；没有键时由 Kafka 分配分区，只在主题只有一个分区时全局有序。
- 生产者幂等（`EnableIdempotence`）且 `acks=all`。批量发布连续交给客户端本地队列、合并成批次发出，再一次等待全部投递报告，任一失败整批抛出 `MESSAGING_UNAVAILABLE_001`。
- 每个订阅主题一个消费者，属于同一消费组 `GroupId`（缺省 `xihan`），没有已提交位点时从最早位点开始（订阅前发布的消息也会收到），单线程按分区顺序派发。
- 处理失败在原位重试到 `MaxAttempts`，不重新入队，分区内顺序不变；仍失败就发布到死信主题 `<TopicPrefix><topic><DeadLetterSuffix>`。无法解码的消息直接进死信。
- 投递完成（成功或进死信）后才提交位点；停止时未完成的消息在重启后重新投递，仍是至少一次。

启动时向集群取一次元数据，连不上即启动失败（`MESSAGING_UNAVAILABLE_001`）；停止时先停消费，再释放生产者。

## 何时使用

- 同一业务对象的事件必须按顺序处理（订单、账户）：给信封设分区键。
- 需要在修复处理器后重放历史消息：`IMessageReplayer.ReplayAsync`。
- 只要工作队列、不在意顺序：[Messaging.RabbitMQ](./messaging-rabbitmq) 也可以；单进程与测试用 [Messaging.InMemory](./messaging-inmemory)。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Messaging.Kafka" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Messaging.Kafka;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

// Kafka 顶替内存代理提供 messaging.queue，并提供 messaging.stream
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<KafkaMessagingFeature>());
```

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Messaging:Kafka:BootstrapServers` | 字符串 | — | 引导地址，逗号分隔的 `host:port`；优先于绑定 `stream` 的值，两者都没有时装配失败 |
| `XiHan:Messaging:Kafka:GroupId` | 字符串 | `xihan` | 消费组，同一组的实例分摊分区 |
| `XiHan:Messaging:Kafka:TopicPrefix` | 字符串 | `xihan.` | 主题前缀 |
| `XiHan:Messaging:Kafka:HealthProbeIntervalSeconds` | 正数 | 15 | 健康项的重探间隔，也是单次探测的超时 |
| `XiHan:Messaging:Kafka:Properties:<key>` | 字符串 | — | 原样透传给 librdkafka（生产者与消费者共用），如 `security.protocol`、`sasl.mechanism`、`sasl.username`、`ssl.ca.location` |
| `XiHan:Messaging:MaxAttempts` | 正整数 | 3 | 最多投递次数（含首次） |
| `XiHan:Messaging:DeadLetterSuffix` | 字符串 | `.dlq` | 死信主题后缀 |

```json
{
  "XiHan": {
    "Messaging": {
      "Kafka": {
        "BootstrapServers": "localhost:9092",
        "GroupId": "orders",
        "Properties": {
          "security.protocol": "SASL_SSL",
          "sasl.mechanism": "PLAIN",
          "sasl.username": "dev-user",
          "sasl.password": "dev-password"
        }
      }
    }
  }
}
```

凭据只作开发示例，生产环境从密钥来源注入，见[配置与选项](../guide/configuration)。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `KafkaMessagingFeature` | 特性入口（`IHostedFeature`）；常量 `ConnectionBinding`（`stream`）、`HealthName`（`messaging.kafka.connection`） |
| `KafkaMessagingOptions` | `BootstrapServers`、`GroupId`、`TopicPrefix`、`Properties`、`HealthProbeInterval`；`FromConfiguration(configuration, binding)` |
| `KafkaMessageBroker` | 代理与重放器；`ConnectAsync`、`IsConnected`、`ProbeAsync`（健康项用）、`ReplayAsync` |

## 使用示例

按账户 ID 设分区键，保证同一账户的流水按发布顺序被处理；处理器的逻辑出错修复后，从某个时刻重放：

```csharp
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;

public sealed class LedgerStream(IMessagePublisher publisher, IMessageReplayer replayer)
{
    public const string Topic = "ledger";

    public ValueTask<IReadOnlyList<Result>> AppendAsync(string accountId, IReadOnlyList<MessageEnvelope> entries, CancellationToken cancellationToken)
        => publisher.PublishManyAsync(Topic, [.. entries.Select(e => e.WithExtension(EnvelopeExtensions.PartitionKey, accountId))], cancellationToken);

    // 调用会等到重放读完调用时的高水位才返回
    public ValueTask<Result> ReplaySinceAsync(DateTimeOffset since, CancellationToken cancellationToken)
        => replayer.ReplayAsync(Topic, since, cancellationToken);
}
```

## 行为与限制

- 重放：`ReplayAsync(topic, since)` 用一个新的独立消费组（`<GroupId>.replay.<随机后缀>`），从各分区该时刻的位点读到调用时的高水位，经同样的派发、重试与死信规则再投递一遍；不提交位点、不影响正常消费；重放期间新到的消息由正常消费投递。主题不存在或该时刻之后没有消息时成功返回；代理还没启动或集群报错时返回 `MESSAGING_UNAVAILABLE_001`。
- 原位重试会阻塞所在分区：一条消息重试期间，同分区后面的消息等待；重试之间不等待。
- 重放与重试都是有意的重复投递，处理器按 `Id` + `Source` 去重。
- 测试：一致性套件（含 `MessageStreamConformance` 的同键有序与按时刻重放）需要真实 Kafka，设置环境变量 `XIHAN_TEST_KAFKA`（如 `localhost:9092`）后运行，未设置时跳过；本地可以用单节点 KRaft 容器：

```bash
docker run -d -p 9092:9092 apache/kafka
```

| 健康项 | 状态 |
| --- | --- |
| `messaging.kafka.connection` | 启动后按 `HealthProbeIntervalSeconds` 在后台以生产者的连接描述集群（从属管理客户端，不另开连接）并缓存结果；集群不可达或超时时 Degraded（`KERNEL_UNAVAILABLE_005`），应用仍在服务 |

## 相关

- [消息与事件](../guide/messaging#队列与流)
- [Messaging.Abstractions](./messaging-abstractions)、[Messaging.RabbitMQ](./messaging-rabbitmq)、[Messaging.InMemory](./messaging-inmemory)
- [部署与打包](../guide/deployment)：绑定 `stream` 的来源
- 规范：RFC-0009《消息、CloudEvents 信封与发件箱》「流」；ADR-0019
