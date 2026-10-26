# XiHan.Framework.Messaging.InMemory

内存消息代理。清单 `role: default`，提供 `messaging.queue@1`。只在单进程内有效：进程退出即丢失，多实例部署换 `Messaging.RabbitMQ`。

## 语义

- 每个主题一条有界通道（`XiHan:Messaging:InMemory:MaxQueuedMessages`，默认 10000），满时发布返回 `MESSAGING_UNAVAILABLE_002`。
- 每个主题一个消费循环，按入队顺序逐条投递；失败按 `XiHan:Messaging:MaxAttempts` 重新入队（排到队尾），超过后进入该主题的死信列表（`DeadLetters(topic)`，同样有界，满时丢弃最旧）。
- 启动前发布的消息留在通道里，启动后投递；停止时取消消费并等待进行中的投递结束。
- 健康项 `messaging.inmemory.capacity`（代理自己实现，同步计算）：任一主题的通道排满即 Degraded（`MESSAGING_UNAVAILABLE_002`，`topic` 是按序数顺序的第一个满主题），否则 Ready。

## 依赖方向

依赖 `XiHan.Framework.Messaging.Abstractions`。

## 状态

P2：可用，通过 `Messaging.Conformance` 套件。
