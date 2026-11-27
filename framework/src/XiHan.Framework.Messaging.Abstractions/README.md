# XiHan.Framework.Messaging.Abstractions

## 关于

`XiHan.Framework.Messaging.Abstractions` 定义消息契约：CloudEvents 信封、发布者与代理接口、处理器订阅、经内核管道的派发、领域事件与事务性发件箱。不含代理实现，由内存、RabbitMQ、Kafka 提供程序提供。

## 主要特性

* CloudEvents 1.0 信封，租户、主体、关联、区域与追踪自动补齐
* 竞争订阅与广播订阅，失败重试后进死信
* 领域事件按主题批量发布
* 按 CloudEvents `type` 登记数据模式（`AddMessageDataSchema`，或配置节 `XiHan:Messaging:DataSchemas` 的数组，每项 `Type` 与 `DataSchema`），领域事件、调度与工作流发布的信封带上 `dataschema`
* 事务性发件箱，与业务写入同事务；积压与死信有健康项，死信可以批量重投

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

处理器返回失败或抛出异常都算投递失败，重试到 `XiHan:Messaging:MaxAttempts`（默认 3）后竞争订阅进死信、广播订阅丢弃。宿主停止打断的处理器不算一次失败，消息之后重新投递；应用开始排空的那一刻被管道拒绝的派发（处理器没有执行）同样不算。消息派发不写幂等键，去重由处理器按消息 ID 自理。

发件箱特性带健康项 `messaging.outbox.backlog`：每 `XiHan:Messaging:Outbox:HealthProbeIntervalSeconds`（默认 15）秒在默认库与各分片库上数一遍发件箱行，未投递的行数超过 `HealthMaxPending`（默认 1000）、最老的一条等过 `HealthMaxPendingAgeSeconds`（默认 300）或死信行数超过 `HealthMaxDeadLettered`（默认 0，即有死信就降级）时条目降级，读不出时同样降级；条目的数据里有 `pending`、`oldestPendingSeconds`、`deadLettered` 与 `databases`。

死信行（尝试次数用尽或载荷无法解码）不再投递。排除了原因之后用 `OutboxDeadLetters` 重投，行回到待投递、尝试次数从头算，由投递循环在下一轮发出：

```csharp
var results = await deadLetters.RedriveManyAsync(ids, cancellationToken);   // 逐项结果与入参同序
```

不存在的行是 `MESSAGING_NOTFOUND_001`，不在死信里的行（待投递、退避中或已经投递）是 `MESSAGING_CONFLICT_001`；存储失败时抛出。

写代理提供程序时，收到一条消息后用 `MessageDeliveryAttempt.RunAsync` 投递一次，再按结论的 `Disposition` 做自己的确认、重投或死信：

```csharp
var outcome = await MessageDeliveryAttempt.RunAsync(dispatch, delivery, messagingOptions, app, logger, stopping);
switch (outcome.Disposition)
{
    case DeliveryDisposition.Complete: /* 确认 */ break;
    case DeliveryDisposition.Retry: /* 以下一次尝试号重新投递 */ break;
    case DeliveryDisposition.DeadLetter: /* 写进死信，得到确认后再确认原消息 */ break;
    case DeliveryDisposition.Discard: /* 广播投递到上限：确认掉，不进死信 */ break;
    default: /* Abandon：被停止打断，不确认，留给代理重新投递 */ break;
}
```

收到的消息无法解码时不派发，用 `MessageDeliveryAttempt.Undecodable(subscription, decoded.Error, logger)` 取了结方式：竞争订阅是 `DeadLetter`（把收到的字节原样写进死信），广播订阅是 `Discard`，日志已经记过。

## 主要类型

此库提供的主要类型：

* `MessageEnvelope`：CloudEvents 信封，`DataSchema` 承载可选的 `dataschema`（非空字符串，不核对 URI 语法），四个代理都原样收发；`Subject` 给了就不能是空串（CloudEvents 规定出现时必须非空）
* `CloudEventsFormat`：信封的结构化 JSON 编码。读取先过 JSON 文本的共同检查（重复的成员不合法），`time` 只认带 `T`、秒与偏移的 RFC 3339 时刻并读成 UTC，`data` 与 `data_base64` 不能同时出现，扩展属性的值是字符串、数字或布尔；写不出读得回来的文档时 `Encode` 抛出，规范属性与扩展属性的字符串里有孤立的代理项、JSON 数据的字符串里有不合法的 UTF-8 字节也一样（不写成 U+FFFD）
* `MessageDataSchemas`：按 `type` 登记的数据模式，由 `services.AddMessageDataSchema(type, dataSchema)` 与配置节 `XiHan:Messaging:DataSchemas`（`MessagingOptions.DataSchemas`，消息 Provider 装配时登记；环境变量写成 `XiHan__Messaging__DataSchemas__0__Type`）填充；取值照 `MessageEnvelope.DataSchema`，另拒孤立的代理项，同一个 `type` 登记另一个值抛出（配置与代码之间也一样，不论谁先登记），配置项缺 `Type` 或 `DataSchema` 时启动失败，最多 10000 个 `type`；只作用于框架构造的信封
* `IMessagePublisher`：发布入口，支持批量
* `IMessageHandler`、`SubscriptionMode`：处理器与订阅方式
* `IMessageBroker`：代理接口，提供程序实现它
* `MessageDeliveryAttempt`、`MessageDeliveryOutcome`、`DeliveryDisposition`：代理一侧的一次投递尝试与它的了结方式
* `OutboxFeature`、`OutboxOptions`：事务性发件箱
* `OutboxDeadLetters`：发件箱死信的批量重投

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/messaging-abstractions)

## 相关包

* 内存实现：[XiHan.Framework.Messaging.InMemory](https://www.nuget.org/packages/XiHan.Framework.Messaging.InMemory)
* RabbitMQ 实现：[XiHan.Framework.Messaging.RabbitMQ](https://www.nuget.org/packages/XiHan.Framework.Messaging.RabbitMQ)
* Kafka 实现：[XiHan.Framework.Messaging.Kafka](https://www.nuget.org/packages/XiHan.Framework.Messaging.Kafka)
* 一致性套件：[XiHan.Framework.Messaging.Conformance](https://www.nuget.org/packages/XiHan.Framework.Messaging.Conformance)

## 反馈与贡献

`XiHan.Framework.Messaging.Abstractions` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
