# XiHan.Framework.Messaging.InMemory

内存消息代理。清单 `role: default`，提供 `messaging.queue@1`。只在单进程内有效：进程退出即丢失，多实例部署换 `Messaging.RabbitMQ`。

## 语义

- 每个主题一条有界通道（`XiHan:Messaging:InMemory:MaxQueuedMessages`，默认 10000），满时发布返回 `MESSAGING_UNAVAILABLE_002`。
- 只为被订阅的主题（本进程以任一订阅方式登记了处理器）建通道：首次发布或消费时创建、不回收；通道数不超过订阅表里的主题数（订阅表在装配时定好、之后只读），不随发布增长，所以不另设主题数上限（ADR-0082 删除了原来的 `MaxTopics`）。诊断方法 `QueuedCount` / `DeliveredCount` / `DeadLetters` 不创建主题。
- 发布到没人订阅的主题：消息直接丢弃，发布照常成功（批量逐项成功），不建通道，记一条 Debug 日志。Debug 而不是 Warning：调度、工作流等框架事件照常发布到应用不一定订阅的主题，这是常态，按 Warning 记会让每次作业运行都刷日志；主题名写错时打开本类别的 Debug 日志排查（ADR-0074）。
- 订阅方式（RFC-0009「订阅方式」）：单进程就是一个实例，一条消息给主题的每种订阅方式各入队一份投递（各占通道的一个位置），竞争订阅与广播订阅各收到一次、各自重试；没有跨进程的语义。
- 每个主题一个消费循环，按入队顺序逐条投递；失败按 `XiHan:Messaging:MaxAttempts` 重新入队（排到队尾，只给失败的那种方式），超过后竞争投递进入该主题的死信列表（`DeadLetters(topic)`，同样有界，满时丢弃最旧），广播投递丢弃并记 Warning 日志。`DeliveredCount` 每种方式各算一次。
- 启动前发布到被订阅主题的消息留在通道里，启动后投递；停止时取消消费并等待进行中的投递结束。
- 健康项 `messaging.inmemory.capacity`（代理自己实现，同步计算）：任一主题的通道排满即 Degraded（`MESSAGING_UNAVAILABLE_002`，`topic` 是按序数顺序的第一个满主题），否则 Ready。

## 依赖方向

依赖 `XiHan.Framework.Messaging.Abstractions`。

## 状态

P2：可用，通过 `Messaging.Conformance` 套件。
