# XiHan.Framework.Messaging.Redis

## 关于

`XiHan.Framework.Messaging.Redis` 是消息的 Redis Streams 提供程序：顶替内存代理，让只有 Redis、没有 RabbitMQ 或 Kafka 的部署也能在多个实例之间收发消息。只提供 `messaging.queue@1`，不提供流能力。

## 主要特性

* 每个主题一条 Redis 流，竞争订阅用消费组，广播订阅每个实例一个组
* 至少一次：处理成功才 `XACK`，失败在原位重试，到上限进死信流
* 停止或应用排空期间不重试、不进死信，条目留给回收；被停止打断的那一次不计尝试，回收之后仍是原来的尝试号，别的实例下一次维护就接手，不用等满回收阈值
* 收到停止信号之后不再读取新条目，没读走的留在流里由别的实例读到
* 超时未确认的条目由 `XAUTOCLAIM` 回收，实例崩溃后由别的实例接着投递
* 流长度以 `MAXLEN ~` 封顶，已被全部组确认的条目定期整理掉
* 没人订阅的主题不建流，消息丢弃

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Messaging.Redis --prerelease
```

### 使用

注册后顶替内存代理，必需配置是 StackExchange.Redis 连接字符串：

```csharp
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<RedisMessagingFeature>());
```

```json
{ "XiHan": { "Messaging": { "Redis": { "Configuration": "localhost:6379", "KeyPrefix": "xihan:messaging:", "ConsumerGroup": "xihan" } } } }
```

连接也可以来自资源绑定 `queue`（类型 `cache:distributed`）。`KeyPrefix` 有孤立的代理项时启动失败（ADR-0321）。需要 Redis 6.2 或更高，`maxmemory-policy` 要是 `noeviction` 或 `volatile-*`：会淘汰任意键的策略可能把流连同未投递的消息一起删掉。发布方与订阅方的 `KeyPrefix`、`ConsumerGroup` 要一致；订阅方第一次启动之前发布的消息不投递。

## 主要类型

此库提供的主要类型：

* `RedisMessagingFeature`：特性入口
* `RedisMessagingOptions`：连接、键前缀、消费组、批量、轮询、回收阈值与长度上限
* `RedisMessageBroker`：`IMessageBroker` 的实现

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/messaging-redis)

## 相关包

* 消息契约：[XiHan.Framework.Messaging.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Messaging.Abstractions)
* 内存实现：[XiHan.Framework.Messaging.InMemory](https://www.nuget.org/packages/XiHan.Framework.Messaging.InMemory)
* RabbitMQ 实现：[XiHan.Framework.Messaging.RabbitMQ](https://www.nuget.org/packages/XiHan.Framework.Messaging.RabbitMQ)
* Kafka 实现：[XiHan.Framework.Messaging.Kafka](https://www.nuget.org/packages/XiHan.Framework.Messaging.Kafka)

## 反馈与贡献

`XiHan.Framework.Messaging.Redis` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
