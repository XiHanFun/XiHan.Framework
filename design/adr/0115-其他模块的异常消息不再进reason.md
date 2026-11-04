# ADR-0115 其他模块的异常消息不再进 reason

## 状态

已采纳（`spec/rfcs/0002-manifest.md`、`0007-multitenancy.md`、`0009-messaging.md`、`0012-objectstorage.md`、`0015-auditing.md`、`0018-wasm-plugins.md`、`0021-workflow.md`、`spec/conventions/error-codes.md` 随之修订）。修订 ADR-0108 决策 2 的前提与 RFC-0021「活动抛出的异常消息放在 `reason`」的约定。

## 日期

2026-11-04

本 ADR 记录第二十八批回灌（`ah-batch-backlog.md`，用户定「两类都改」）的决定。

## 背景

ADR-0111 到 ADR-0114 改完 AI 之后，审查了 `framework/src` 里 83 处把异常消息写进错误的地方（`ag-audit.md`）：12 处会把外部系统的消息带给调用方，8 处只到管理端或健康快照，其余 63 处保留。

判断依据是 `ProblemResponse` 把 `error.Message` 与全部标量细节写进响应，不分错误类型，而消息模板大多含 `{reason}`。

有一处是已有决定的前提失效：ADR-0108 让发件箱对 `FrameworkException` 照常写消息，理由是「消息是框架自己写的」，而 Kafka 与 RabbitMQ 的 `FrameworkException` 拼进了代理的异常消息。

## 决策

1. 下面这些位置，`reason` 只写异常类型的全名，错误码、消息键、参数名都不变：
   - 对象存储：`FileSystemBlobStore`、`S3BlobStore`、`BlobStores`、`S3ConditionalWriteGate`、`S3ConditionalWriteProbe`；
   - 多租户：`TenantStoreShardResolver`；
   - 审计：`DatabaseAuditTrail.QueryAsync`；
   - 工作流：`ActivityExecutor` 的活动抛出、`HttpActivity` 的连接失败；
   - CloudEvents：`CloudEventsWebhookPublisher`；
   - 消息：`KafkaMessageBroker.ReplayAsync`；
   - 健康项：`PeriodicHealthContributor`、`BlobStores` 的探针。
2. 完整异常进日志：
   - 调用方发起的操作，每次失败一条 Warning；
   - 周期探测，同一种失败第一次记 Warning，之后记 Debug，向量库那条（ADR-0111）的同一个理由。
3. Kafka 与 RabbitMQ 的 `FrameworkException` 只写框架自己的文字，代理异常放在 `InnerException`；Kafka 投递报告的失败写错误码，原文进日志。发件箱对 `FrameworkException` 照常写消息的做法因此成立，ADR-0108 的其余决定不变。
4. Wasm 崩溃的细节 `trap` 只写陷阱种类（如 `MemoryOutOfBounds`）或异常类型，原文进日志。沙箱耗尽的 `reason` 是框架自己写的文字，不变。
5. S3 批量删除里单个键的失败只写 S3 的错误码。这里没有异常对象，错误码足够区分，原文不进错误。

## 备选

1. **只改会到调用方的 12 处**：发件箱的前提继续失效，健康项与 ADR-0111 不一致。用户否决。
2. **加一个公共脱敏器，在 `ProblemResponse` 统一去掉 `reason`**：`reason` 里也有框架自己写的有用文字（校验、配额），响应层分不出来；而且记录与事件不经过响应层。否决。
3. **按异常类型白名单放行消息**：同一种异常的消息内容不固定（`HttpRequestException` 有时带主机），名单挡不住。否决。

## 影响

- 工作流的故障文案会变：`WorkflowInstance.Fault` 的 `reason` 是类型全名，BasicApp 的 `FaultMessage` 随之变化。要让调用方看到说明，活动返回失败，不靠抛异常。
- 公开签名：`FileSystemBlobStore`、`FileSystemBlobStoreProvider`、`S3BlobStore`、`BlobStores`、`TenantStoreShardResolver`、`CloudEventsWebhookPublisher` 的构造增加 `ILoggerFactory`。经容器解析不受影响。
- 第三类的 63 处不改，清单见 `ag-audit.md`。
