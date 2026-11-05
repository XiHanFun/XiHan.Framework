# XiHan.Framework.Messaging.Abstractions

## 关于

`XiHan.Framework.Messaging.Abstractions` 定义消息契约：CloudEvents 信封、发布者与代理接口、处理器订阅、经内核管道的派发、领域事件与事务性发件箱。不含代理实现，由内存、RabbitMQ、Kafka 提供程序提供。

## 主要特性

* CloudEvents 1.0 信封，租户、主体、关联、区域与追踪自动补齐
* 竞争订阅与广播订阅，失败重试后进死信
* 领域事件按主题批量发布
* 事务性发件箱，与业务写入同事务

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Messaging.Abstractions --prerelease
```

### 使用

一般由实现包带入；写处理器、发布消息或接入发件箱时直接使用：

```csharp
builder.Services.AddMessageHandler<OrderPlacedHandler>("orders");   // 竞争：只有一个实例收到
builder.Services.AddMessageHandler<SessionRevokedHandler>("sessions.revoked", SubscriptionMode.Broadcast);   // 广播：每个实例都收到

builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<OutboxFeature>());
```

处理器返回失败或抛出异常都算投递失败，重试到 `XiHan:Messaging:MaxAttempts`（默认 3）后竞争订阅进死信。消息派发不写幂等键，去重由处理器按消息 ID 自理。

## 主要类型

此库提供的主要类型：

* `MessageEnvelope`：CloudEvents 信封
* `IMessagePublisher`：发布入口，支持批量
* `IMessageHandler`、`SubscriptionMode`：处理器与订阅方式
* `IMessageBroker`：代理接口，提供程序实现它
* `OutboxFeature`、`OutboxOptions`：事务性发件箱

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/messaging-abstractions)

## 相关包

* 内存实现：[XiHan.Framework.Messaging.InMemory](https://www.nuget.org/packages/XiHan.Framework.Messaging.InMemory)
* RabbitMQ 实现：[XiHan.Framework.Messaging.RabbitMQ](https://www.nuget.org/packages/XiHan.Framework.Messaging.RabbitMQ)
* Kafka 实现：[XiHan.Framework.Messaging.Kafka](https://www.nuget.org/packages/XiHan.Framework.Messaging.Kafka)
* 一致性套件：[XiHan.Framework.Messaging.Conformance](https://www.nuget.org/packages/XiHan.Framework.Messaging.Conformance)

## 反馈与贡献

`XiHan.Framework.Messaging.Abstractions` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
