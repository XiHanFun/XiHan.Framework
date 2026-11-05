# XiHan.Framework.Messaging.RabbitMQ

## 关于

`XiHan.Framework.Messaging.RabbitMQ` 是消息的 RabbitMQ 提供程序：顶替内存代理，适用于多实例部署下的工作队列。

## 主要特性

* 发布到持久的 direct 交换机，订阅方声明持久队列
* 发布确认，手动确认，失败重试后进死信队列
* 广播订阅用每个实例独占的队列

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Messaging.RabbitMQ --prerelease
```

### 使用

注册后顶替内存代理，必需配置是 AMQP 连接串：

```csharp
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<RabbitMQMessagingFeature>());
```

```json
{ "XiHan": { "Messaging": { "RabbitMQ": { "Configuration": "amqp://<user>:<password>@localhost:5672", "Exchange": "xihan", "QueuePrefix": "xihan.", "PrefetchCount": 16 } } } }
```

连接也可以来自资源绑定 `queue`。发布方与订阅方的 `Exchange`、`QueuePrefix` 要一致；滚动升级时先升级订阅方。

## 主要类型

此库提供的主要类型：

* `RabbitMQMessagingFeature`：特性入口
* `RabbitMQMessagingOptions`：连接、交换机、队列前缀与预取
* `RabbitMQMessageBroker`：`IMessageBroker` 的实现

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/messaging-rabbitmq)

## 相关包

* 消息契约：[XiHan.Framework.Messaging.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Messaging.Abstractions)
* 内存实现：[XiHan.Framework.Messaging.InMemory](https://www.nuget.org/packages/XiHan.Framework.Messaging.InMemory)
* Kafka 实现：[XiHan.Framework.Messaging.Kafka](https://www.nuget.org/packages/XiHan.Framework.Messaging.Kafka)

## 反馈与贡献

`XiHan.Framework.Messaging.RabbitMQ` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
