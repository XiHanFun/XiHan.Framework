# XiHan.Framework.Messaging.InMemory

## 关于

`XiHan.Framework.Messaging.InMemory` 是消息的默认实现：每个主题一条有界通道的内存代理。只在单进程内有效，进程退出即丢失，适合开发、测试与单实例；多实例部署换 RabbitMQ 或 Kafka。

## 主要特性

* 每个主题一个消费循环，按入队顺序投递
* 失败重新入队，超过上限进有界死信列表
* 通道排满时发布返回 `MESSAGING_UNAVAILABLE_002` 并让健康项 Degraded

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Messaging.InMemory --prerelease
```

### 使用

`Profiles.Standard` 已装配本包；不用组合包时单独装配：

```csharp
builder.AddXiHan(xihan => xihan.UseFeature<InMemoryMessagingFeature>());
```

```json
{ "XiHan": { "Messaging": { "MaxAttempts": 3, "InMemory": { "MaxQueuedMessages": 10000 } } } }
```

发布到没人订阅的主题时消息直接丢弃，发布照常成功。

## 主要类型

此库提供的主要类型：

* `InMemoryMessagingFeature`：特性入口
* `InMemoryMessageBroker`：内存代理
* `InMemoryMessagingOptions`：通道容量等选项

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/messaging-inmemory)

## 相关包

* 消息契约：[XiHan.Framework.Messaging.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Messaging.Abstractions)
* RabbitMQ 实现：[XiHan.Framework.Messaging.RabbitMQ](https://www.nuget.org/packages/XiHan.Framework.Messaging.RabbitMQ)
* Kafka 实现：[XiHan.Framework.Messaging.Kafka](https://www.nuget.org/packages/XiHan.Framework.Messaging.Kafka)

## 反馈与贡献

`XiHan.Framework.Messaging.InMemory` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
