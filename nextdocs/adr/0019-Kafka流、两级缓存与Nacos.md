# ADR-0019 流能力定义与三个 Provider：`Messaging.Kafka`、`Caching.Hybrid`、`Configuration.Nacos`

## 状态

已采纳（RFC-0009 / RFC-0006 / RFC-0014 修订）。

## 日期

2026-10-26

## 背景

用户于 2026-10-26 要求先做 `Messaging.Kafka`、`Caching.Hybrid`、`Configuration.Nacos` 三个 Provider。此前 [ADR-0018](0018-P5-09能力补齐.md) 按 BasicApp 的生产配置判定「不补 Kafka」，本决定撤销该条；`messaging.stream@1` 自 RFC-0009 起只是预留的能力 ID，没有契约；`Caching.Hybrid` 在总体方案里是「L1 内存 / L2 分布式，HybridCache 作为 Provider」的一句话；`Configuration.Nacos` 是 RFC-0014 预告的第一个远程配置 Provider。

## 备选

1. 流能力只是「用 Kafka 实现队列」，不定义新契约。
2. 流能力 = 队列语义 + 分区键有序 + 消费组位点 + 按时刻重放（`IMessageReplayer.ReplayAsync`）。
3. 两级缓存架在 `Microsoft.Extensions.Caching.Hybrid` 的 `HybridCache` 上。
4. 两级缓存自己实现：L1 进程内对象引用（有界 LRU），L2 是被选中的 `ISharedCache`。
5. Nacos 用官方 / 社区 SDK（`nacos-sdk-csharp`）。
6. Nacos 直接用 HTTP 配置接口（v1，Nacos 2.x / 3.x 兼容），`HttpClient` 实现。

## 决策

- **取 2**。没有契约的能力 ID 没有意义；分区键用信封扩展属性 `xihankey`（与 `xihantenant` 等同一文法），重放是独立消费位点的有意重复投递，经完整管道、按同样的重试与死信规则。Kafka 同时提供 `messaging.queue@1` 与 `messaging.stream@1`：生产者幂等、`acks=all`，批量发布用客户端原生批量后一次等待全部报告；每个订阅主题一个消费者，重试在原位（保持分区顺序），投递完成后提交位点；无法解码与重试耗尽的进死信主题。新增 `MessageStreamConformance`（同键有序、按时刻重放），继承自 `MessageBrokerConformance`，Provider 只需继承一次。
- **取 4**。`HybridCache` 是类型化门面、没有「取」只有「取或建」、序列化器与标签模型都是它自己的；套在我们的 `ICache` 上要绕开它的契约，不如自己写一个两级门面（约 200 行）。`Caching.Hybrid` 提供 `caching.local@1`（显式 Provider，顶替内存实现的默认）、依赖 `caching.distributed@1`（Redis、Remote 或单实例下的内存实现），读 L1 → L2 → 回填 L1，写与删先 L2 后 L1；L1 存活时间不超过 `LocalTimeToLive`，别的实例的写入最长在这个窗口后可见；不做跨实例失效广播（需要更强一致的键直接用 `ISharedCache`）。
- **取 6**。SDK 带入自己的配置模型、监听线程与 gRPC 依赖；RFC-0014 的契约只需要「取整份文档 + 带版本的长轮询」，v1 的 `/nacos/v1/cs/configs` 与 `/configs/listener` 两个接口就够，版本用内容 MD5 与 Nacos 的监听约定一致。有用户名密码时走 `/nacos/v1/auth/login` 取令牌并在到期前续。格式支持 JSON（展平）与 properties，不合法的文档是新错误码 `CONFIGURATION_VALIDATION_001`。
- 三个包都是 Ring 3 的显式 Provider（`role: provider`），不进任何画像；Kafka 与 Nacos 的一致性测试在设置 `XIHAN_TEST_KAFKA` / `XIHAN_TEST_NACOS` 时对真实服务运行，Hybrid 的测试零外部基础设施。

## 理由

- 流语义里最常被依赖的是「同键有序」与「能重放」，两条都能在 Kafka 上零成本落地，又不把 Kafka 特有的分区、位点 API 泄漏进契约。
- 两级缓存的收益来自读路径少一次网络往返；一致性窗口显式写进选项比隐式的失效广播更可预期。
- 不依赖 SDK 让 Nacos 包的依赖面只有 `HttpClient`，与 `Scheduling.Kubernetes` 直接调 API Server 的做法一致。

## 影响

- `Messaging.Abstractions` 新增 `EnvelopeExtensions.PartitionKey` 与 `IMessageReplayer`；`Messaging.Conformance` 新增 `MessageStreamConformance`。
- `Configuration.Abstractions` 新增 `CONFIGURATION_VALIDATION_001` 与消息键 `configuration.document.invalid`（10 种区域）。
- 新包 `Messaging.Kafka`（`Confluent.Kafka`，非 AOT）、`Caching.Hybrid`（无第三方依赖）、`Configuration.Nacos`（无第三方依赖）。
- ADR-0018 里「不补 `Messaging.Kafka`」一条作废；04 表的 `Messaging.Kafka`、`Caching.Hybrid`、`Configuration.Nacos` 改为 P5 已完成。
