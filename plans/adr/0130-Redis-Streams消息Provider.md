# ADR-0130 Redis Streams 消息 Provider：推翻 ADR-0018「Redis 不作消息 Provider」

## 状态

已采纳（`spec/rfcs/0009-messaging.md`、`spec/rfcs/0016-packaging.md`、`spec/rfcs/0014-configuration.md` 随之修订）。

## 日期

2026-11-10

## 背景

ADR-0018 取 9 时写了「不补 Kafka，Redis 也不作消息 Provider」，理由是 BasicApp 生产用 RabbitMQ。Kafka 那一半已由 ADR-0019 撤销，Redis 这一半还在。现状是：

- 5.x 的 `messaging.queue@1` 只有三个实现：`Messaging.InMemory`（单进程）、`Messaging.RabbitMQ`、`Messaging.Kafka`。
- `Caching.Redis` 里的 Redis Pub/Sub 只用于缓存的跨实例失效（ADR-0055 的 `<KeyPrefix>__changes` 频道），不是消息代理。
- 3.x 有 Redis 事件总线，5.x 重写时没有带过来。

所以只有 Redis、没有 RabbitMQ 或 Kafka 的部署（小型应用、只开了一个 Redis 的环境）多实例时没有跨进程消息：内存代理每个进程一份，发件箱、领域事件与框架事件都出不了进程。用户要求把 Redis 作为可选的显式 Provider 补回来，默认实现仍是内存代理。

## 备选

1. 维持 ADR-0018，不做 Redis 消息 Provider。
2. Redis Pub/Sub：发出即忘，订阅方不在线就收不到，没有确认与重投。
3. Redis List 做可靠队列（`LMOVE` 到处理中列表）：没有消费组，广播要每个实例一个列表、发布方得知道有哪些实例；阻塞读会占住 StackExchange.Redis 的多路复用连接。
4. Redis Streams 加消费组：`XADD` / `XREADGROUP` / `XACK`，未确认的条目留在消费组的待确认列表（PEL）里，`XAUTOCLAIM` 回收超时未确认的条目。
5. 重试的做法：
   - a. 像 RabbitMQ 一样把失败的条目重新 `XADD` 到流尾。
   - b. 只靠 `XAUTOCLAIM`：失败不确认，等超时后被回收再投。
   - c. 在原位立即重试，每次重试前把条目 `XCLAIM` 给自己，让 PEL 记下投递次数。
6. 容量：`XADD MAXLEN ~` 近似裁剪；或者满了返回 `MESSAGING_UNAVAILABLE_002`。
7. 绑定：复用 `Caching.Redis` 的 `cache`；或者用 `queue`、类型 `cache:distributed`。
8. 同时提供 `messaging.stream@1`。

## 决策

取 4、5c、6 的近似裁剪加已确认条目的整理、7 的 `queue`，不做 8。

### 包与清单

- 新包 `Messaging.Redis`（Ring 3，`Preview`，非 AOT）。入口 `RedisMessagingFeature`，清单 ID `xihan.messaging.redis`，角色 `provider`。只提供 `messaging.queue@1`，依赖 `kernel.hosting@1`。
- Redis 客户端用 `Caching.Redis` 已在用的 StackExchange.Redis，版本取中央包版本。两个包各自建连接，不共享。
- 要求 Redis 6.2 或更高：用到 `XAUTOCLAIM` 与 `XGROUP CREATECONSUMER`。
- 健康项 `messaging.redis.connection`：按 `HealthProbeIntervalSeconds`（缺省 15）周期 `PING`。
- 启动时连接，连不上就启动失败（`MESSAGING_UNAVAILABLE_001`），不退回内存代理。之后断线由客户端自动重连：发布抛出携带 `MESSAGING_UNAVAILABLE_001` 的 `FrameworkException`，消费循环记日志、等 5 秒再取。

### 键与条目

- 每个主题一条流，键 `<KeyPrefix><topic>`，`KeyPrefix` 缺省 `xihan:messaging:`。
- 死信流键 `<KeyPrefix><topic><DeadLetterSuffix>`，也就是主题加后缀那个主题的流。订阅 `<topic>.dlq` 就能消费死信，与 RabbitMQ、Kafka 一样。
- 条目字段 `envelope` 是 CloudEvents 结构化 JSON。死信条目另带 `attempt`（最后一次的尝试号）与 `error`（错误码，不带消息文字，ADR-0115）。

### 发布

- `XADD <key> NOMKSTREAM MAXLEN ~ <MaxLength> * envelope <bytes>`。Redis 回了条目 ID 才算发布成功。
- 流只由订阅方建立：订阅方建消费组时带 `MKSTREAM`，发布用 `NOMKSTREAM`。键不存在说明没有订阅者，这时 `XADD` 返回空，消息丢弃、发布照常成功（批量逐项成功），记一条 Debug 日志。语义与内存代理（ADR-0074）、RabbitMQ（ADR-0079）一致：调度、工作流的框架事件发到应用不一定订阅的主题，不会在 Redis 里占着内存。
- 批量发布在一个 `MULTI` / `EXEC` 事务里连续 `XADD`：一次往返，整批要么都写入要么都没写入，主题内保持入参顺序。连接或超时异常让整批抛出。

### 消费

- 竞争订阅用消费组 `GroupName`（缺省 `xihan`）。广播订阅每个实例一个消费组 `<GroupName>@<InstanceId>`，`InstanceId` 即 `MessagingInstance.Id`。消费者名都是实例标识。`GroupName` 的文法与实例标识相同，不含 `@`，所以竞争组的名字里一定没有 `@`，广播组的名字里恰好一个。
- 两种组都从 `$` 建，订阅前的消息不投递，与 RabbitMQ 一致。启动时用一段 Lua 原子地建组（`MKSTREAM`）并建消费者，组已存在时只建消费者。建组在 `StartAsync` 返回前完成，启动后立即发布的消息不会漏。
- 每个订阅一个消费循环，单线程按顺序派发：
  - `XREADGROUP ... COUNT <BatchSize> STREAMS <key> >`，`BatchSize` 缺省 10。
  - 没取到就等 `PollIntervalSeconds`（缺省 0.2）再取，取到了就接着取。StackExchange.Redis 的多路复用连接不支持 `BLOCK`，所以是轮询，空闲时每个订阅每个周期一次往返。
- 处理成功就 `XACK`。

### 重试与死信

- 处理器返回失败或抛出异常，在原位立即重试到 `MaxAttempts`。每次重试前先 `XCLAIM <id>` 给自己（最小空闲 0）。这样 PEL 的投递次数加一、空闲时间清零，尝试号就是 PEL 的投递次数：进程中途崩溃后，别的实例接手时接着计数。条目已被裁剪或已不在 PEL 里时 `XCLAIM` 什么也拿不到，这次投递就此结束，记 Warning。
- 到了上限：
  - 竞争订阅：`XADD` 到死信流（`MKSTREAM`，同样 `MAXLEN ~`），然后 `XACK`。
  - 广播订阅：`XACK` 并记 Warning，不进死信。
- 无法解码的条目不重试：竞争订阅原样进死信流，广播订阅确认掉并记日志。
- 停止期间处理器抛出取消或返回失败（管道把取消变成失败结果），这次投递不重试、不进死信，条目留在 PEL 里，由别的实例回收后按投递次数接着投递。
- 死信写入或 `XACK` 失败时不确认，条目留在 PEL 里，由下面的回收接手。极端情况下死信会写两遍，这是至少一次的代价，RabbitMQ 也是这样。

### 回收

- 每个消费循环每过 `min(ClaimIdleTimeSeconds, 10)` 秒做一次维护。维护时用 `XAUTOCLAIM` 把消费组里空闲超过 `ClaimIdleTimeSeconds`（缺省 60）的条目认领给自己，一直扫到游标回到 `0-0`。这些条目来自崩溃或停止时没处理完的实例，以及死信写入、`XACK` 失败的投递。
- 认领到的条目按 PEL 的投递次数作为尝试号，超过 `MaxAttempts` 的直接进死信（广播订阅则丢弃），不再派发。
- `XAUTOCLAIM` 报告已删除的 ID 说明待确认的条目在确认前就被裁剪了，记 Warning。
- `ClaimIdleTimeSeconds` 必须大于一批条目的最长处理时间（`BatchSize` 乘处理器的最长耗时），否则还在处理的条目会被别的实例认领去重复处理。

### 组与消费者的清理

实例标识每次启动都不同（ADR-0085），所以每次重启都会给竞争组加一个消费者、给广播加一个组。不清理的话两者都随重启无界增长。

- 停止时：
  - 竞争订阅：自己没有待确认条目才删除自己这个消费者（`XGROUP DELCONSUMER` 会连同待确认条目一起删掉，有待确认条目时留给别人回收）。
  - 广播订阅：用 Lua 原子地删除自己的组，删完流上一个组也不剩时连流一起删除。流上没有组时，之后写入的条目没有人会收到，因为新组都从 `$` 建。
- 每次维护：
  - 删除竞争组里空闲超过 `ClaimIdleTimeSeconds`、没有待确认条目的其他消费者。
  - 删除名字以 `<GroupName>@` 开头、不是自己的广播组：没有消费者，或者全部消费者都空闲超过 `ClaimIdleTimeSeconds`。删除也走上面那段 Lua。还在线的实例每个轮询周期都会读一次，空闲时间不会超过阈值。

### 容量

- 每条流都用 `MAXLEN ~ <MaxLength>`（缺省 100000）近似裁剪，主题流与死信流都是。流的数量不超过被订阅的主题数（加上它们的死信流）。
- 流里留着已确认的条目，只靠 `MAXLEN` 的话，被订阅的主题会一直占满 `MaxLength` 条。所以每次维护再用一段 Lua，按每个组的下限整理流：组有待确认条目时下限是最早一条的 ID，没有时是该组最后投递的 ID。取全部组下限里最小的，`XTRIM MINID ~` 到那里。这样健康的主题只留着积压，`MaxLength` 就是积压的上限。
- 消费者落后超过 `MaxLength` 时，最旧的条目在投递前就被裁掉，这些消息丢失，与 Kafka 的保留期同理。部署时按最长停机期间的积压设 `MaxLength`。

### 绑定

- 绑定 `queue`，类型 `cache:distributed`，可选。名字表示角色（消息队列），类型表示资源（一个 Redis 服务），所以从 RabbitMQ 换到 Redis 时绑定名不变、类型与值换掉。
- 主值覆盖 `XiHan:Messaging:Redis:Configuration`。属性 `username`、`password` 覆盖连接字符串里的 `user`、`password`：连接时按选项设置，不改写字符串。
- `PrintMembers` 遮蔽 `Configuration`、`User`、`Password`（RFC-0014，ADR-0090）。

### 运行要求

- 消息用的 Redis 不能淘汰流的键：`maxmemory-policy` 要是 `noeviction`，或者 `volatile-*`（流的键没有过期时间，不会被淘汰）。
- 连接时读一次 `INFO memory`。策略是 `allkeys-*` 时记 Warning；`INFO` 被禁用时跳过。
- 发布成功只表示主节点收下了条目，框架不发 `WAIT` 等副本。持久化（AOF / RDB）与复制由部署决定：主从切换时刚写入、还没复制的条目会丢。要更强的持久性用 RabbitMQ 的仲裁队列或 Kafka。

## 理由

- **不维持 ADR-0018**：「BasicApp 用 RabbitMQ」只说明 BasicApp 不需要，不能说明框架不需要。只有 Redis 的部署没有跨进程消息，发件箱也就无从谈起。新包是可选的显式 Provider，不进组合包，默认实现仍是内存代理，不影响零外部基础设施的承诺。
- **Streams 而不是 Pub/Sub 或 List**：只有 Streams 原生给出 `messaging.queue@1` 需要的三样东西：消费组（竞争与广播都能表达）、PEL 加 `XACK`（至少一次）、`XAUTOCLAIM`（崩溃恢复）。Pub/Sub 是至多一次。List 要自己做处理中列表的恢复，广播要发布方知道有哪些实例。
- **原位重试，不重新入流**：一条流上挂着全部组，重新 `XADD` 会让每个组（别的实例的广播组、竞争组）再收一遍，不能像 RabbitMQ 那样定向发回所消费的队列。只靠 `XAUTOCLAIM` 重试的话，重试间隔被绑死在「处理器最长耗时」这个超时上，与另两个 Provider「重试之间不等待」也不一致。原位重试前 `XCLAIM` 给自己，尝试次数落在 Redis 里，崩溃后接着算；空闲时间清零，重试期间不会被别的实例认领。
- **近似裁剪而不是满了拒绝**：流里留着已确认的条目，长度不等于积压，拿它判「满」会误报。另外一个已经下线的竞争组会把整理的下限钉住，流长期停在上限：按「满了拒绝」的话，这个主题的发布对所有人都会失败；近似裁剪只让这个死掉的组丢消息，活着的组照常消费。Redis 8.2 的 `ACKED` 裁剪模式可以只裁全部组都确认过的条目，但 Compose 与 CI 用的是 Redis 7，要求 8.2 太早。
- **整理已确认的条目**：Redis 的数据在内存里。不整理的话，每个被订阅的主题常驻 `MaxLength` 条，缺省值就只能压得很小，消费者停机一会儿就丢消息。整理之后内存只跟着积压走，`MaxLength` 可以给得宽。
- **流只由订阅方建立**：没人订阅的主题不建流，框架事件不会每个主题常驻 `MaxLength` 条。这是从 RabbitMQ 的教训（ADR-0079）来的：没有订阅者就没有地方堆积。
- **绑定不复用 `cache`**：缓存用的 Redis 常配 `allkeys-lru`，会淘汰流的键；共用一个绑定的话，绑定的主值会同时覆盖两个配置节，部署方没法让消息与缓存走不同的 Redis。用自己的绑定，部署方要共用同一台 Redis 时让两个绑定指向它就行，这是显式的选择。
- **不提供流能力**：竞争组把条目轮流分给组内的消费者，同一分区键的消息可能同时在两个实例上处理，满足不了 RFC-0009「流」第 1 条的同键有序。`XRANGE` 能按时刻重放，但没有分区有序就不是 `messaging.stream@1`。要流能力用 Kafka。

## 影响

- 新包 `XiHan.Framework.Messaging.Redis`：
  - `RedisMessagingFeature`、`RedisMessagingOptions`、`RedisMessageBroker`。
  - 选项配置节 `XiHan:Messaging:Redis`：`Configuration`、`KeyPrefix`、`GroupName`、`BatchSize`、`PollIntervalSeconds`、`ClaimIdleTimeSeconds`、`MaxLength`、`HealthProbeIntervalSeconds`。
- 新测试项目 `XiHan.Framework.Messaging.Redis.Tests`：
  - 设置了 `XIHAN_TEST_REDIS` 时对真实 Redis 跑 `MessageBrokerConformance`，以及回收、死信、组清理、整理与裁剪的用例。
  - 选项与装配的用例不需要 Redis。
- 没有新错误码与消息键：沿用 `MESSAGING_UNAVAILABLE_001` 与 `messaging.broker.unavailable`。
- 规范修订：
  - RFC-0009 增加 Provider 说明与订阅方式里的 Redis 一条。
  - RFC-0016 的绑定对照表加一行。
  - RFC-0014 的凭据遮蔽列表加 `RedisMessagingOptions`。
- 同步修改：
  - 架构测试的连接串字段表与引用。
  - `xihan` 离线知识库（包与特性索引重新生成，配置节索引加 `XiHan:Messaging:Redis`）。
  - 文档站的发布与订阅、队列与流、包参考页。
  - README 与更新日志。
- 已知限制：
  - 消费是轮询，空闲时有 `PollIntervalSeconds` 的延迟与相应的往返。
  - 持久性取决于 Redis 的持久化与复制。
  - 消费者落后超过 `MaxLength` 时丢最旧的消息。
  - 实例崩溃后留下的广播组，要等还订阅这个主题的实例在维护时删除；没有实例再订阅这个主题时它会一直留着，占用最多 `MaxLength` 条。
