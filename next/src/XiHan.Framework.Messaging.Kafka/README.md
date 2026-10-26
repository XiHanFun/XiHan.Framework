# XiHan.Framework.Messaging.Kafka

`messaging.queue@1` 与 `messaging.stream@1` 的 Kafka Provider（Ring 3，`role: provider`，RFC-0009「流」）。注册后顶替内存代理。

```csharp
builder.AddXiHan(x => x.UseFeature<KafkaMessagingFeature>());
```

```json
{ "XiHan": { "Messaging": { "Kafka": { "BootstrapServers": "localhost:9092", "GroupId": "orders", "TopicPrefix": "xihan.", "Properties": { "security.protocol": "SASL_SSL", "sasl.mechanism": "PLAIN", "sasl.username": "…", "sasl.password": "…" } } } } }
```

引导地址来自 `XiHan:Messaging:Kafka:BootstrapServers`，或绑定 `stream`（`stream`）的值；两者都没有时装配失败。`Properties:<key>` 原样透传给 librdkafka（生产者与消费者共用）。

## 语义

- 主题映射为 `<TopicPrefix><topic>`（前缀默认 `xihan.`）；消息值是 CloudEvents 结构化 JSON，头 `content-type` 为 `application/cloudevents+json`、`x-xihan-attempt` 为尝试次数；消息键取信封扩展属性 `xihankey`（分区键），没有时由 Kafka 分配分区——主题只有一个分区时全局有序，多分区的主题靠分区键换取同键有序。
- 生产者幂等且 `acks=all`；批量发布连续交给客户端本地队列、合并成批次发出，再一次等待全部投递报告，任一失败整批抛出 `MESSAGING_UNAVAILABLE_001`。
- 每个订阅主题一个消费者（同一消费组 `GroupId`，默认 `xihan`），从最早位点开始（订阅前发布的消息也收到），单线程按分区顺序派发；处理失败在原位重试到 `MaxAttempts`（不重新入队，顺序不变），仍失败发布到死信主题 `<topic><DeadLetterSuffix>`；无法解码的消息直接进死信。投递完成后才提交位点，停止时未完成的消息重启后重新投递（至少一次）。
- 重放 `IMessageReplayer.ReplayAsync(topic, since)`：用独立消费组从该时刻的位点读到调用时的高水位，经同样的派发、重试与死信规则再投递一遍，不提交位点、不影响正常消费；主题不存在或该时刻之后没有消息时成功返回。
- 启动时向集群取一次元数据，连不上即启动失败（`MESSAGING_UNAVAILABLE_001`）。
- 健康项 `messaging.kafka.connection`：启动后按 `XiHan:Messaging:Kafka:HealthProbeIntervalSeconds`（默认 15）在后台以生产者的连接描述集群（从属管理客户端，不另开连接）并缓存结果；集群不可达或超时条目 Degraded（`KERNEL_UNAVAILABLE_005`），应用仍在服务。
- 不满足 AOT 分析器（librdkafka 本机库）。

## 测试

一致性套件需要真实 Kafka：设置环境变量 `XIHAN_TEST_KAFKA`（如 `localhost:9092`）后运行，`MessageStreamConformance` 含队列套件全部用例与流的两个用例；未设置时跳过。本地 `docker run -d -p 9092:9092 apache/kafka` 即可（单节点 KRaft，自动建主题）。

## 依赖方向

依赖 `Messaging.Abstractions` 与 `Confluent.Kafka`（自带 librdkafka）。

## 状态

P5：可用，通过 `Messaging.Conformance` 的队列与流套件。
