# XiHan.Framework.Messaging.Kafka

## 关于

`XiHan.Framework.Messaging.Kafka` 是消息的 Kafka 提供程序，同时提供队列与流：顶替内存代理，并支持按分区键有序、按消费组保存位点、按时刻重放。

## 主要特性

* 队列语义：竞争订阅在同一消费组分摊分区，广播订阅用本实例独占的消费组
* 按分区键有序，`IMessageReplayer` 按时刻重放
* 位点在投递完成后才提交，至少一次
* 失败原位重试，仍失败发布到死信主题

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Messaging.Kafka --prerelease
```

### 使用

注册后顶替内存代理，必需配置是引导地址：

```csharp
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<KafkaMessagingFeature>());
```

```json
{ "XiHan": { "Messaging": { "Kafka": { "BootstrapServers": "localhost:9092", "GroupId": "orders", "TopicPrefix": "xihan.", "Properties": { "security.protocol": "SASL_SSL", "sasl.mechanism": "PLAIN", "sasl.username": "<user>", "sasl.password": "<password>" } } } } }
```

`Properties` 原样透传给 librdkafka。引导地址也可以来自资源绑定 `stream`。关闭了自动建主题的集群要预建主题，清单见概念文档。

## 主要类型

此库提供的主要类型：

* `KafkaMessagingFeature`：特性入口
* `KafkaMessagingOptions`：引导地址、消费组、主题前缀等
* `KafkaMessageBroker`：同时实现 `IMessageBroker` 与 `IMessageReplayer`

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/messaging-kafka)

## 相关包

* 消息契约：[XiHan.Framework.Messaging.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Messaging.Abstractions)
* 内存实现：[XiHan.Framework.Messaging.InMemory](https://www.nuget.org/packages/XiHan.Framework.Messaging.InMemory)
* RabbitMQ 实现：[XiHan.Framework.Messaging.RabbitMQ](https://www.nuget.org/packages/XiHan.Framework.Messaging.RabbitMQ)

## 反馈与贡献

`XiHan.Framework.Messaging.Kafka` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
