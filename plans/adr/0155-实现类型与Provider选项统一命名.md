# ADR-0155 实现类型与 Provider 选项统一命名、订阅表装配后只读

## 状态

已采纳。

## 日期

2026-11-11

## 背景

API 审查第三节列出几处命名与只读语义的不一致：

- `Caching.Hybrid.HybridCache` 与 BCL 的 `Microsoft.Extensions.Caching.Hybrid.HybridCache` 同名，两个命名空间同时 using 时报 CS0104。
- `Localization.InMemory.MessageLocalizer` 不带机制前缀，与 `DatabaseMessageLocalizer` 不对称；包名规则要求实现按机制命名。
- 三个消息 Provider 的选项不同形：
  - 用户名：RabbitMQ 是 `UserName`，Redis 消息与 Redis 缓存是 `User`，Nacos 是 `Username`。
  - 消费组：Kafka 是 `GroupId`，Redis 消息是 `GroupName`。
- `MessageSubscriptions` 是 `Stable`，但「装配后只读」没有执行：
  - `All` 与 `HandlersFor` 交出内部的 `List`，可以被强转后修改。
  - 应用启动后仍可 `Add`，而订阅表不是线程安全的，运行中的消费与派发会看到不一致的表。

## 决策

1. **实现类型按机制命名**：
   - `HybridCache` 改为 `HybridObjectCache`，与 `InMemoryObjectCache` 对称。
   - `MessageLocalizer` 改为 `InMemoryMessageLocalizer`。
2. **Provider 选项同名**：
   - 用户名统一为 `UserName`，涉及 `RedisCachingOptions`、`RedisMessagingOptions` 与 `NacosOptions`，与 RabbitMQ 一致，也合 .NET 对复合词的写法。
   - 消费组统一为 `ConsumerGroup`，涉及 Kafka 的 `GroupId` 与 Redis 消息的 `GroupName`，配置键随之改为 `XiHan:Messaging:Kafka:ConsumerGroup` 与 `XiHan:Messaging:Redis:ConsumerGroup`。
   - 绑定里的凭据属性仍是 `username` / `password`，不变。
3. **订阅表装配后只读**：
   - 消息运行时与派发器构造时冻结订阅表，此后 `Add` 抛 `InvalidOperationException`。
   - `All` 与 `HandlersFor` 交出只读视图。
   - 订阅照旧在特性的 `Configure` 里经 `AddMessageHandler` 登记。

## 影响

- 源码迁移：
  - `HybridCache` → `HybridObjectCache`，判断 `ICache` 是不是两级缓存的写法改用新类型名。
  - `MessageLocalizer` → `InMemoryMessageLocalizer`。
  - 选项初始化器里的 `User` / `Username` → `UserName`，`GroupId` / `GroupName` → `ConsumerGroup`。
- 配置迁移：
  - `XiHan:Messaging:Kafka:GroupId` → `XiHan:Messaging:Kafka:ConsumerGroup`。
  - `XiHan:Messaging:Redis:GroupName` → `XiHan:Messaging:Redis:ConsumerGroup`。
  - `XiHan:Configuration:Nacos:Username` → `XiHan:Configuration:Nacos:UserName`。
  - 消费组的值不变，已有的位点与待确认条目照常接续。
- 启动后再登记订阅的代码改为在 `Configure` 里登记；只读取订阅表的代码不用改。
- `MessageSubscriptions` 是 `Stable`，其余是 `Preview`；GA 前修改，不需要登记豁免（ADR-0131）。
