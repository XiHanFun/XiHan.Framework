# XiHan.Framework.Messaging.Redis

`messaging.queue@1` 的 Redis Streams Provider，基于 StackExchange.Redis：每个主题一条流，竞争订阅用消费组，广播订阅每个实例一个组，处理成功才确认，超时未确认的条目由别的实例回收。适用于只有 Redis、没有 RabbitMQ 或 Kafka 的多实例部署。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Messaging.Redis` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `messaging.queue@1`（`role: provider`），不提供 `messaging.stream@1`；依赖 `kernel.hosting@1` |
| 特性入口 | `RedisMessagingFeature`（清单 ID `xihan.messaging.redis`） |
| 配置节 | `XiHan:Messaging:Redis`、`XiHan:Messaging`；可选绑定 `queue`（类型 `cache:distributed`） |
| 依赖 | `XiHan.Framework.Messaging.Abstractions`；`StackExchange.Redis`（与 `Caching.Redis` 同一个中央包版本） |
| Redis | 6.2 或更高（用到 `XAUTOCLAIM` 与 `XGROUP CREATECONSUMER`） |
| AOT | 不兼容（`IsAotCompatible=false`，StackExchange.Redis 未声明 AOT 兼容） |
| 稳定性 | Preview |

## 概述

`RedisMessageBroker` 实现 `IMessageBroker`。装配后它顶替内存代理，处理器、发布者与发件箱的代码不变（ADR-0130）。

- 键：每个主题一条流 `<KeyPrefix><topic>`（前缀缺省 `xihan:messaging:`），条目字段 `envelope` 是 CloudEvents 结构化 JSON。死信流是 `<KeyPrefix><topic><DeadLetterSuffix>`，订阅主题 `<topic>.dlq` 就能消费死信。
- 流只由订阅方建立：订阅方启动时建消费组（带 `MKSTREAM`，从 `$` 开始）与自己的消费者，发布用 `NOMKSTREAM`。所以订阅方第一次启动之前发布的消息不投递；之后订阅方离线期间的消息留在流里。
- 发布：`XADD ... NOMKSTREAM MAXLEN ~ <MaxLength>`，Redis 应答了条目 ID 才算成功。流不存在（没有订阅者）时消息丢弃、发布照常成功，记一条 Debug 日志，与内存代理、RabbitMQ 一致。批量发布在一个 `MULTI` / `EXEC` 事务里，一次往返，整批写入或整批不写入，主题内保持入参顺序。
- 消费：竞争订阅都在消费组 `ConsumerGroup`（缺省 `xihan`）里，组内的实例分摊条目；广播订阅用本实例独占的组 `<ConsumerGroup>@<InstanceId>`。每个订阅（主题加方式）一个消费循环，`XREADGROUP` 每次最多取 `BatchSize` 条，单线程依次派发，成功即 `XACK`。没取到就等 `PollIntervalSeconds` 再取：StackExchange.Redis 的多路复用连接不支持阻塞读，消费是轮询。
- 重试：处理失败在原位立即重试到 `MaxAttempts`，不重新入流。每次重试前先把条目 `XCLAIM` 给自己，消费组的待确认列表因此记下投递次数，尝试号就是它：实例崩溃后接手的实例接着计数。
- 死信：竞争订阅到上限时写进死信流（另带字段 `attempt` 与 `error`），写入得到应答后才确认原条目；广播订阅到上限时确认掉并记 Warning，不进死信。无法解码的条目不重试：竞争订阅原样进死信流，广播订阅丢弃。
- 回收：每个消费循环每过 `min(ClaimIdleTimeSeconds, 10)` 秒维护一次，用 `XAUTOCLAIM` 认领组里空闲超过 `ClaimIdleTimeSeconds` 的条目，按投递次数接着投递，已超过 `MaxAttempts` 的直接进死信。这些条目来自崩溃或停止时没处理完的实例，以及死信写入、确认失败的投递。
- 整理与清理：维护时还会做三件事，组与消费者的数量、流的长度都不会无界增长。
  - 删除竞争组里空闲超时、没有待确认条目的消费者：实例标识每次启动不同，重启会留下旧消费者。
  - 删除没有消费者或消费者全部空闲超时的 `<ConsumerGroup>@*` 广播组：实例崩溃会留下旧组。
  - 按全部组的下限 `XTRIM MINID ~`，去掉全部组都确认过的条目，流里只留积压。
- 停止时取消消费循环。正在处理的条目不确认，留给别的实例回收。之后清理本实例的痕迹：
  - 广播订阅删除自己的组，流上不剩组时连流一起删除。
  - 竞争订阅在没有待确认条目时删除自己的消费者。

启动时先连接 Redis、为全部订阅建好组再开始消费，连不上时启动失败（`MESSAGING_UNAVAILABLE_001`），不退回内存代理；停止时先停健康探针与消费，再关闭连接。

## 何时使用

- 只有 Redis 的多实例部署：工作队列至少一次、按主题分摊给各实例；每个实例都要处理的通知用广播订阅。
- 已经有 RabbitMQ：用 [Messaging.RabbitMQ](./messaging-rabbitmq)，持久性与运维工具更完整。
- 需要同一分区键有序、或按时刻重放：用 [Messaging.Kafka](./messaging-kafka)。竞争组把条目轮流分给组内的消费者，本包满足不了同键有序，所以不提供 `messaging.stream@1`。
- 单进程、开发与测试：[Messaging.InMemory](./messaging-inmemory)。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Messaging.Redis --prerelease
```

当前是预览版，安装要带 `--prerelease`；从源码或本地包源引用见[快速上手](/getting-started/installation)。

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Messaging.Redis;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);

builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<RedisMessagingFeature>());
```

与 `RabbitMQMessagingFeature` 或 `KafkaMessagingFeature` 同时装配时能力求解报 `KERNEL_CAPABILITY_004`，用 `XiHan:Providers:messaging.queue` 写 `xihan.messaging.redis` 指定它。

## 配置

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `XiHan:Messaging:Redis:Configuration` | 字符串 | — | StackExchange.Redis 连接字符串；有绑定 `queue` 时被绑定的主值覆盖，两者都没有时装配失败；绑定的属性 `username` / `password` 覆盖连接字符串里的 `user` / `password`（连接时设置，不改写字符串） |
| `XiHan:Messaging:Redis:KeyPrefix` | 字符串 | `xihan:messaging:` | 流键的前缀；发布方与订阅方要一致 |
| `XiHan:Messaging:Redis:ConsumerGroup` | 字符串 | `xihan` | 竞争订阅的消费组名，文法同实例标识（字母或数字开头、最多 64 个字符、不含 `@`）；只有订阅方用到，要分摊同一主题的实例要一致 |
| `XiHan:Messaging:Redis:BatchSize` | 1–1000 | 10 | 每次最多取的条目数 |
| `XiHan:Messaging:Redis:PollIntervalSeconds` | 正数 | 0.2 | 没取到新条目时再取之前的等待 |
| `XiHan:Messaging:Redis:ClaimIdleTimeSeconds` | 至少 0.001 | 60 | 待确认条目空闲多久后可被回收，也是清理空闲消费者与遗留广播组的阈值 |
| `XiHan:Messaging:Redis:MaxLength` | 正整数 | 100000 | 每条流（主题与死信）的近似长度上限 |
| `XiHan:Messaging:Redis:HealthProbeIntervalSeconds` | 正数 | 15 | 健康项的重探间隔，也是单次探测的超时 |
| `XiHan:Messaging:MaxAttempts` | 正整数 | 3 | 最多投递次数（含首次） |
| `XiHan:Messaging:DeadLetterSuffix` | 字符串 | `.dlq` | 死信流后缀 |
| `XiHan:Messaging:InstanceId` | 字符串 | 不写，每次启动生成 | 消费者名与广播组名里的实例标识；必须每个同时在线的实例唯一，重复时广播静默退化成竞争 |

```json
{
  "XiHan": {
    "Messaging": {
      "MaxAttempts": 5,
      "Redis": {
        "Configuration": "localhost:6379",
        "ConsumerGroup": "orders-api",
        "ClaimIdleTimeSeconds": 120
      }
    }
  }
}
```

`ClaimIdleTimeSeconds` 必须大于一批条目的最长处理时间（`BatchSize` 乘处理器的最长耗时）。条目在被取到时就记进本消费者的待确认列表；处理得比这更久，别的实例会把还在处理的条目认领去，同一条消息被处理两次。处理器慢时调大它，或者调小 `BatchSize`。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `RedisMessagingFeature` | 特性入口（`IHostedFeature`）；常量 `ConnectionBinding`（`queue`）、`HealthName`（`messaging.redis.connection`） |
| `RedisMessagingOptions` | `Configuration`、`UserName`、`Password`、`KeyPrefix`、`ConsumerGroup`、`BatchSize`、`PollInterval`、`ClaimIdleTime`、`MaxLength`、`HealthProbeInterval`；`FromConfiguration(configuration, binding)`；`ToString()` 遮蔽连接字符串与凭据 |
| `RedisMessageBroker` | 代理；连接与健康探测由特性在内部调用 |

## 使用示例

处理器与内存代理下完全相同。下面的服务把死信流里的消息重新发回原主题，修复处理器的缺陷之后用：死信流就是主题 `<topic>.dlq` 的流，这里直接用 StackExchange.Redis 读取。

```csharp
using StackExchange.Redis;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;

public sealed class DeadLetterReplayer(IConnectionMultiplexer redis, IMessagePublisher publisher)
{
    // 读出死信流里最早的 count 条，解码后批量发回原主题，成功后删掉这些死信
    public async ValueTask<Result> ReplayAsync(string topic, int count, CancellationToken cancellationToken)
    {
        var database = redis.GetDatabase();
        var key = $"xihan:messaging:{topic}.dlq";
        var entries = await database.StreamRangeAsync(key, count: count);
        var envelopes = entries.Select(static e => CloudEventsFormat.Decode((byte[])e["envelope"]!))
            .Where(static r => r.IsSuccess)
            .Select(static r => r.ValueOrThrow())
            .ToList();

        foreach (var result in await publisher.PublishManyAsync(topic, envelopes, cancellationToken))
        {
            if (result.IsFailure)
            {
                return result;
            }
        }

        await database.StreamDeleteAsync(key, [.. entries.Select(static e => e.Id)]);
        return Result.Success();
    }
}
```

`IConnectionMultiplexer` 由应用自己登记，本包不把内部的连接登记进容器。

## 行为与限制

- 连接或超时异常抛出携带 `MESSAGING_UNAVAILABLE_001` 的 `FrameworkException`，消息只写框架自己的文字，客户端的异常在 `InnerException`；没有订阅者的发布成功并丢弃；单条发布遇到不合法主题抛 `ArgumentException`（发布者已先校验，代理收到不合法主题是装配错误）。
- 投递是至少一次。下面几种情况会重复投递，处理器按 `Id` + `Source` 去重：
  - 处理成功但确认失败。
  - 死信写入成功但确认失败。
  - 处理时间超过 `ClaimIdleTimeSeconds`，条目被别的实例认领。
- 重试在原位、之间不等待；同一个消费循环里后面的条目等这条重试完。组内有多个实例时条目轮流分给它们，主题内没有全局顺序。
- 消费者落后超过 `MaxLength` 时，最旧的条目在投递前被 `MAXLEN ~` 裁掉，这些消息丢失；回收时发现待确认的条目被裁掉会记 Warning。按最长停机期间的积压设 `MaxLength`。
- 持久性取决于 Redis 的部署：发布成功只表示主节点收下了条目，框架不等副本，主从切换时刚写入的条目可能丢失；Redis 重启后数据是否还在取决于 AOF / RDB。
- 消息用的 Redis 不能淘汰流的键：`maxmemory-policy` 要是 `noeviction` 或 `volatile-*`（流的键没有过期时间，不会被淘汰）。连接时检查到 `allkeys-*` 记 Warning。缓存用的 Redis 常配 `allkeys-lru`，所以本包不读缓存的 `cache` 绑定；要共用一台 Redis 时让 `queue` 与 `cache` 两个绑定指向它，并按消息的要求配淘汰策略。
- 组被外部删除或 Redis 丢了数据（`NOGROUP`）时，消费循环重新建组，从 `$` 开始，这期间写入的条目不再投递。
- 实例崩溃留下的广播组，要等还订阅这个主题的实例在维护时删除；没有实例再订阅这个主题时它会一直留着，流也留着，最多 `MaxLength` 条。
- 两个同时在线的实例配了同一个 `InstanceId`，它们共用同一个广播组，广播静默退化成竞争（每条只有一个实例收到）。
- 测试：选项与装配的用例不需要 Redis；一致性套件与流上的行为用例（死信、回收、清理、整理与裁剪）需要真实 Redis，设置环境变量 `XIHAN_TEST_REDIS`（如 `localhost:6379`，与 `Caching.Redis` 的测试同一个变量）后运行，未设置时跳过。

| 健康项 | 状态 |
| --- | --- |
| `messaging.redis.connection` | 启动后按 `HealthProbeIntervalSeconds` 在后台 `PING` 并缓存结果；连不上或超时时 Degraded，应用仍在服务 |

## 相关

- [发布与订阅](/messaging/pubsub)、[队列与流](/messaging/streams)
- [Messaging.Abstractions](./messaging-abstractions)、[Messaging.InMemory](./messaging-inmemory)、[Messaging.RabbitMQ](./messaging-rabbitmq)、[Messaging.Kafka](./messaging-kafka)、[Caching.Redis](./caching-redis)
- [资源绑定](/configuration/bindings)：绑定 `queue` 的来源
- 规范：RFC-0009《消息、CloudEvents 信封与发件箱》
