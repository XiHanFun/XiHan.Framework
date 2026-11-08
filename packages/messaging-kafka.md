来源：https://framework.docs.xihanfun.com/packages/messaging-kafka

# XiHan.Framework.Messaging.Kafka

`messaging.queue@1` 与 `messaging.stream@1` 的 Kafka Provider，基于 Confluent.Kafka：幂等生产者、按消费组提交位点、分区键有序、按时刻重放、重试与死信主题。适用于需要同键有序、可重放事件流的多实例部署。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Messaging.Kafka` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `messaging.queue@1`、`messaging.stream@1`（`role: provider`）；依赖 `kernel.hosting@1` |
| 特性入口 | `KafkaMessagingFeature`（清单 ID `xihan.messaging.kafka`） |
| 配置节 | `XiHan:Messaging:Kafka`、`XiHan:Messaging`；可选绑定 `stream`（类型 `stream`） |
| 依赖 | `XiHan.Framework.Messaging.Abstractions`；`Confluent.Kafka`（自带 librdkafka） |
| AOT | 不兼容（`IsAotCompatible=false`，librdkafka 是本机库） |
| 稳定性 | Preview |

## 概述

`KafkaMessageBroker` 同时实现 `IMessageBroker` 与 `IMessageReplayer`。特性只为被选中的能力绑定接口：选中 `messaging.queue` 时登记 `IMessageBroker`，选中 `messaging.stream` 时登记 `IMessageReplayer`。

- 主题映射为 `<TopicPrefix><topic>`（前缀缺省 `xihan.`）。消息值是 CloudEvents 结构化 JSON，头 `content-type` 为 `application/cloudevents+json`、`x-xihan-attempt` 为尝试次数。
- 消息键取信封扩展属性 `xihankey`（`EnvelopeExtensions.PartitionKey`）：同键进同一分区，按发布顺序投递；没有键时由 Kafka 分配分区，只在主题只有一个分区时全局有序。
- 生产者幂等（`EnableIdempotence`）且 `acks=all`。批量发布连续交给客户端本地队列、合并成批次发出，再一次等待全部投递报告，任一失败整批抛出 `MESSAGING_UNAVAILABLE_001`。
- 每个订阅（主题加方式）一个消费者，单线程按分区顺序派发。竞争订阅属于同一消费组 `GroupId`（缺省 `xihan`），没有已提交位点时从最早位点开始（订阅前发布的消息也会收到）。
- 广播订阅（[订阅方式](/messaging/pubsub)）用本实例独占的消费组 `<GroupId>@<InstanceId>`（组内只有本实例、分到全部分区），从最新位点开始、不提交位点：消费组空了不留位点，重启时没有已提交的位点，不回放历史（实例标识缺省每次启动生成，是新的消费组；配了 `XiHan:Messaging:InstanceId` 时组名不变，但从未提交过位点，见[订阅方式](/messaging/pubsub)）。分区分配是异步的，启动后要等消费组分到分区（通常几秒）才开始收；重试耗尽或无法解码时丢弃并记 Warning 日志，不发布死信。按前缀授权消费组的集群，`GroupId` 前缀的 ACL 同样覆盖广播消费组。
- 处理失败在原位重试到 `MaxAttempts`，不重新入队，分区内顺序不变；竞争订阅仍失败就发布到死信主题 `<TopicPrefix><topic><DeadLetterSuffix>`。无法解码的消息直接进死信。
- 竞争订阅投递完成（成功或进死信）后才提交位点；停止时未完成的消息在重启后重新投递，仍是至少一次。
- 死信发布没有落盘或失败（或投递因其他异常没有完成）时不提交，消费者回退到这条消息，等 `RedeliveryDelaySeconds` 后重新投递（处理器重新执行），期间这个分区不往后读；消费者本身出错同样等一会儿再取。消费循环只在停止时退出，每次重投记 Error 日志。

启动时向集群取一次元数据，连不上即启动失败（`MESSAGING_UNAVAILABLE_001`）；停止时先停消费，再释放生产者。

## 何时使用

- 同一业务对象的事件必须按顺序处理（订单、账户）：给信封设分区键。
- 需要在修复处理器后重放历史消息：`IMessageReplayer.ReplayAsync`。
- 只要工作队列、不在意顺序：[Messaging.RabbitMQ](./messaging-rabbitmq) 也可以；单进程与测试用 [Messaging.InMemory](./messaging-inmemory)。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Messaging.Kafka --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[快速上手](/getting-started/installation)。

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
| `XiHan:Messaging:Kafka:BootstrapServers` | 字符串 | — | 引导地址，逗号分隔的 `host:port`；有绑定 `stream` 时被绑定的主值覆盖，两者都没有时装配失败。绑定的属性 `security.protocol`、`sasl.mechanism`、`sasl.username`、`sasl.password` 各自覆盖 `Properties` 里的同名键，其余绑定属性不透传 |
| `XiHan:Messaging:Kafka:GroupId` | 字符串 | `xihan` | 消费组，同一组的实例分摊分区 |
| `XiHan:Messaging:Kafka:TopicPrefix` | 字符串 | `xihan.` | 主题前缀 |
| `XiHan:Messaging:Kafka:HealthProbeIntervalSeconds` | 正数 | 15 | 健康项的重探间隔，也是单次探测的超时 |
| `XiHan:Messaging:Kafka:RedeliveryDelaySeconds` | 正数 | 5 | 投递没有完成（死信发布失败等）时重新投递前的等待 |
| `XiHan:Messaging:Kafka:Properties:<key>` | 字符串 | — | 原样透传给 librdkafka（生产者与消费者共用），如 `security.protocol`、`sasl.mechanism`、`sasl.username`、`ssl.ca.location` |
| `XiHan:Messaging:MaxAttempts` | 正整数 | 3 | 最多投递次数（含首次） |
| `XiHan:Messaging:DeadLetterSuffix` | 字符串 | `.dlq` | 死信主题后缀 |
| `XiHan:Messaging:InstanceId` | 字符串 | 不写，每次启动生成 | 广播消费组名里的实例标识；必须每个同时在线的实例唯一，重复时广播退化成竞争 |

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

凭据只作开发示例，生产环境从密钥来源注入，见[配置与选项](/configuration/options)。

## 主题

框架不建主题，也不管理分区数与副本数。主题由谁建出来取决于集群的 `auto.create.topics.enable`：

- 开启（Kafka 缺省）：发布或订阅到不存在的主题时由 broker 按集群缺省的分区数与副本数建出来。没人订阅的主题里的消息只留到保留期（`retention.ms`，缺省 7 天），没有消费组就没有积压（ADR-0079）。
- 关闭（生产集群常见）：主题不存在是部署错误，框架不替你建，也不当作「没有订阅者」丢弃——那样主题名或 `TopicPrefix` 写错也会被吞成一条 Debug 日志，消息无声丢失（ADR-0085）。表现是：
  - 发布在客户端判定主题不存在后失败（`MESSAGING_UNAVAILABLE_001`，librdkafka 的 `topic.metadata.propagation.max.ms`，缺省 30 秒），批量发布整批抛出；直接发布的把异常抛给发布方，经发件箱的按发件箱规则计入尝试、退避重试、到上限进死信（ADR-0108：发件箱遇到抛出时若整轮回滚，这条消息与同批的会一直卡住）。
  - 订阅的消费者每次取消息都报主题不存在，记 Warning 日志后继续等，主题建好之后才开始收。
  - 死信主题不存在时死信发布失败，消息回退重投（`RedeliveryDelaySeconds`），这个分区停在原地。

关闭自动建主题的集群要在部署时预先建好下面这些主题，名字都带 `TopicPrefix`（缺省 `xihan.`）：

| 主题 | 什么时候需要 |
| --- | --- |
| 应用发布与订阅的主题 | 总是 |
| 被竞争订阅的主题加 `DeadLetterSuffix`（缺省 `.dlq`） | 有竞争订阅时；广播订阅不发布死信 |
| `scheduling.job.scheduled`、`scheduling.job.unscheduled`、`scheduling.job.paused`、`scheduling.job.resumed`、`scheduling.run.started`、`scheduling.run.succeeded`、`scheduling.run.failed`、`scheduling.run.canceled`、`scheduling.run.lost` | 装配了调度（`SchedulingTopics`，[调度事件](/scheduling/jobs)），没人订阅也要建 |
| `workflow.instance.started`、`.completed`、`.faulted`、`.canceled`、`.terminated`、`.suspended`、`.resumed`、`.jumped`、`.migrated`、`.timedout`；`workflow.node.executing`、`.executed`、`.faulted`；`workflow.task.created`、`.completed`、`.transferred`、`.claimed`、`.released`、`.returned`、`.urged`、`.reminder`；`workflow.external.created`、`.completed`、`.failed`；`workflow.custom` | 装配了工作流（`WorkflowTopics`，[工作流事件](/workflow/definitions)），没人订阅也要建 |

重放用临时消费组读已有的主题，不建主题；广播订阅只多出消费组，不多出主题。用 Kafka 自带的脚本建主题（分区数与副本数按集群定）：

```bash
for topic in scheduling.job.scheduled scheduling.job.unscheduled scheduling.job.paused scheduling.job.resumed \
             scheduling.run.started scheduling.run.succeeded scheduling.run.failed scheduling.run.canceled scheduling.run.lost; do
  kafka-topics.sh --bootstrap-server kafka:9092 --create --if-not-exists \
    --topic "xihan.$topic" --partitions 3 --replication-factor 3
done
```

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

- 重放：`ReplayAsync(topic, since)` 只投给竞争订阅，用一个新的独立消费组（`<GroupId>.replay.<随机后缀>`），从各分区该时刻的位点读到调用时的高水位，经同样的派发、重试与死信规则再投递一遍；不提交位点、不影响正常消费；重放期间新到的消息由正常消费投递。主题不存在或该时刻之后没有消息时成功返回；代理还没启动、集群报错或死信发布没有得到确认时返回 `MESSAGING_UNAVAILABLE_001`。
- 原位重试会阻塞所在分区：一条消息重试期间，同分区后面的消息等待；重试之间不等待。
- 重放与重试都是有意的重复投递，处理器按 `Id` + `Source` 去重。
- 主题不存在（ADR-0079、ADR-0085）：集群开着自动建主题时由 broker 建出，没人订阅的主题里的消息只留到保留期；关了自动建主题时发布约 30 秒后失败、订阅一直等、死信发布回退重投，要预建主题，清单见[主题](#主题)。
- 测试：重投路径（`KafkaRedeliveryTests`）与广播（`KafkaBroadcastTests`）用打桩的生产者与消费者覆盖，不需要 Kafka。
- 测试：一致性套件（含 `MessageStreamConformance` 的同键有序与按时刻重放）需要真实 Kafka，设置环境变量 `XIHAN_TEST_KAFKA`（如 `localhost:9092`）后运行，未设置时跳过；本地可以用单节点 KRaft 容器：

```bash
docker run -d -p 9092:9092 apache/kafka
```

| 健康项 | 状态 |
| --- | --- |
| `messaging.kafka.connection` | 启动后按 `HealthProbeIntervalSeconds` 在后台以生产者的连接描述集群（从属管理客户端，不另开连接）并缓存结果；集群不可达或超时时 Degraded（`KERNEL_UNAVAILABLE_005`），应用仍在服务 |

## 相关

- [消息与事件](/messaging/streams)
- [Messaging.Abstractions](./messaging-abstractions)、[Messaging.RabbitMQ](./messaging-rabbitmq)、[Messaging.InMemory](./messaging-inmemory)
- [部署与打包](/deployment/pack)：绑定 `stream` 的来源
- 规范：RFC-0009《消息、CloudEvents 信封与发件箱》「流」；ADR-0019
