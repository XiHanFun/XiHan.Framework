# XiHan.Framework.EventBus.SqlSugar

> 事件收发件箱的 SqlSugar 持久化提供程序：发件箱落库并按业务所在库落点；收件箱按消息标识去重；两者的领取都是多实例互斥。发件箱何时被写入有前提条件，见「发件箱入箱」。

- **NuGet**：`XiHan.Framework.EventBus.SqlSugar`
- **模块类**：`XiHanSqlSugarEventBusModule`
- **所在层**：基础设施层
- **关键依赖**：[EventBus](./eventbus)（收发件箱契约）、[Data](./data)（SqlSugar 客户端、工作单元连接登记、建表）

## 概述

[EventBus](./eventbus) 实现了完整的收发件箱骨架：发件箱在工作单元内入箱，后台服务轮询取出、投递、删除；收件箱在收到消息时入箱，后台服务轮询取出、调用处理器、标记结果。但它的 `IEventOutbox` / `IEventInbox` 默认实现都是进程内字典——事件与业务数据不在同一事务，进程退出即丢，去重与待处理事件跨实例也不共享。

本包把两者落到数据表，补上这些保证：

- **发件箱入箱与业务同事务（有前提）**：在未完成的事务型工作单元内以 `onUnitOfWorkComplete: false` 发布时，事件行与业务数据同一个事务、同一个库。按默认方式发布时发件箱不被使用，见「发件箱入箱」
- **收件箱去重**：同一消息被 broker 重复投递，只处理一次（在保留期内）
- **多实例领取互斥**：N 个实例同时轮询，同一条记录只会被一个实例领走

## 何时使用

- 用了分布式事件总线，且要求「业务成功才发事件、业务失败绝不发事件」
- 消费端要求同一消息不被重复处理，且应用以多实例部署
- 已在用 [Data](./data)，希望收发件箱与业务数据走同一套连接

不需要本包的场景：单实例、事件可丢、或事件与业务数据本就不要求一致。

## 安装与启用

```bash
dotnet add package XiHan.Framework.EventBus.SqlSugar
```

在启动模块上声明依赖：

```csharp
[DependsOn(typeof(XiHanSqlSugarEventBusModule))]
public class YourAppModule : XiHanModule
{
}
```

建表需要开启 [Data](./data) 的库初始化与建表初始化，**两者默认都是关闭的**：

```json
{
  "XiHan": {
    "Data": {
      "SqlSugarCore": {
        "EnableDbInitialization": true,
        "EnableTableInitialization": true
      }
    }
  }
}
```

两张表都不是分表，没有 SqlSugar 插入时自动建表的兜底。未开启建表初始化又没有手工建表时，首次入箱即抛「表不存在」：发件箱会使所在业务事务一同失败，收件箱会使该条消息消费失败并由 broker 重投。

## 表结构

两张表都**不分表**。

### `sys_event_outbox`

发件箱是短命队列，投递成功即删除。主库与每个模块库都会建出这张表（`[TableInitialization(IncludeModuleConnections = true)]`）。

| 列 | 类型 | 说明 |
| --- | --- | --- |
| `Basic_Id` | `Guid`，主键，非自增 | 事件唯一标识，直接取 `OutgoingEventInfo.Id` |
| `Row_Version` | `long` | 并发标识 |
| `Event_Name` | `string(256)`，非空 | 事件名 |
| `Event_Data` | `byte[]`，非空 | 序列化后的事件数据 |
| `Created_Time` | `DateTimeOffset`，非空 | 事件创建时间 |
| `Extra_Properties` | 大文本，可空 | 扩展属性的 JSON |
| `Status` | `int`，非空 | 0 待发送，1 已领取 |
| `Claim_Token` | `string(64)`，可空 | 领取令牌 |
| `Claim_Time` | `DateTimeOffset`，可空 | 领取时刻，用于超时释放 |

### `sys_event_inbox`

收件箱处理完不删除，保留一段时间承担去重。只在平台主库建表（`[TableInitialization(Target = DbInitializationTarget.Platform)]`）。

| 列 | 类型 | 说明 |
| --- | --- | --- |
| `Basic_Id` | `Guid`，主键，非自增 | 事件唯一标识，直接取 `IncomingEventInfo.Id` |
| `Row_Version` | `long` | 并发标识 |
| `Message_Id` | `string(256)`，可空 | 原始消息标识 |
| `Dedup_Key` | `string(256)`，非空，**唯一索引** | 去重键：有消息标识时等于它，没有时由事件标识生成 |
| `Event_Name` | `string(256)`，非空 | 事件名 |
| `Event_Data` | `byte[]`，非空 | 序列化后的事件数据 |
| `Created_Time` | `DateTimeOffset`，非空 | 入箱时刻 |
| `Extra_Properties` | 大文本，可空 | 扩展属性的 JSON |
| `Status` | `int`，非空 | 0 待处理，1 已领取，2 已处理，3 已丢弃 |
| `Retry_Count` | `int`，非空 | 重试次数 |
| `Next_Retry_Time` | `DateTimeOffset`，可空 | 早于该时刻不领取 |
| `Claim_Token` | `string(64)`，可空 | 领取令牌 |
| `Claim_Time` | `DateTimeOffset`，可空 | 领取时刻，用于超时释放 |
| `Handled_Time` | `DateTimeOffset`，可空 | 进入已处理或已丢弃的时刻，保留期由此起算 |

索引：`ux_sys_event_inbox_dedup_key`（`Dedup_Key`，唯一）、`ix_sys_event_inbox_status`（`Status, Created_Time`）。

两张表的主键都用事件自身的 `Guid` 而非雪花 `long`：契约按 `Guid` 定位记录。该 `Guid` 由框架的顺序 Guid 生成器产出，不会造成索引碎片。

## 工作原理

### 发件箱入箱

发件箱只在调用方于**未完成的工作单元内**以 `onUnitOfWorkComplete: false` 发布、且配置了至少一个 `Outboxes` 时才会被写入；工作单元为事务型时事件行与业务数据同一个事务，非事务型时各自提交。按默认的 `onUnitOfWorkComplete: true` 发布时，工作单元先提交、再发布缓冲的分布式事件，此时已没有当前工作单元，`AddToOutboxAsync` 返回 `false`，事件**绕过发件箱直接发出**——本包对这条路径不起作用。

`DistributedEventBusBase` 在工作单元的作用域内解析发件箱实现，因此 `SqlSugarEventOutbox` 注册为 `Scoped`。事件行的落库由当前工作单元已登记的连接决定：

| 已登记的连接 | 落点 |
| --- | --- |
| 0 个 | 当前库 |
| 1 个 | 该库，与业务数据同一个事务 |
| 多于 1 个 | 抛 `InvalidOperationException` |

当前租户使用独立于平台的数据库布局（库隔离）时，入箱在解析落点之前就抛 `InvalidOperationException`。判断方式是比较当前租户与平台（`ICurrentTenant.Change(null)`）下 `GetCurrentLayoutConfigIds()` 的主库。

### 收件箱入箱与去重

收件箱在收到消息时入箱，此时没有业务工作单元，也没有「业务所在的库」。`SqlSugarEventInbox` 的全部读写都切换到无租户上下文，落在宿主布局的主库——与同样无租户上下文的处理循环读写同一个库。

去重分两层：

1. 入箱前 `ExistsByMessageIdAsync` 按消息标识查重，命中即视为已处理
2. 两个实例同时收到同一消息、都查不到时，`Dedup_Key` 的唯一索引只放行一个；另一个的插入失败后，本包按去重键回查，确认已在库就按重复处理，否则原样抛出

去重键是消息标识而不是事件标识——框架在每次收到消息时都会生成新的事件标识。

### 领取

宿主的发送循环与处理循环都没有分布式锁。去重由本包的领取实现完成，分三步，全部使用方言无关的表达式 API：

1. 查出可领取记录的主键，按创建时间升序取一批
2. 条件 `UPDATE` 抢占这批主键，`WHERE` 里重复可领取条件
3. 按本次令牌取回真正抢到的记录

第 2 步的每行 `UPDATE` 是原子的，两个并发领取者只有一个能把某行从可领取改成已领取。候选全被抢走时另选一批重试，最多三轮。

可领取条件：发件箱是「待发送，或已领取但超时」；收件箱是「待处理且下次重试时刻为空或已到，或已领取但超时」。

发件箱的领取遍历当前布局的全部库（主库与已建连的模块库），每库配额为 `maxCount` 除以库数、至少 1 条，单个库不可达时记录日志并跳过。收件箱只有一个库，不遍历。

不使用 `FOR UPDATE SKIP LOCKED`、`UPDATE ... LIMIT` 等方言特性——本框架是通用类库，代价是多一次往返。

### 超时释放

`Claim_Time` 早于「当前时刻 − 领取超时」的已领取记录重新变为可领取，避免实例异常退出导致记录永久滞留。

### 收件箱的状态流转与清理

| 方法 | 结果 |
| --- | --- |
| `MarkAsProcessedAsync` | 已处理，写入 `Handled_Time`，**不删除** |
| `MarkAsDiscardAsync` | 已丢弃，写入 `Handled_Time`，**不删除** |
| `RetryLaterAsync` | 回到待处理，清空领取信息，记录重试次数与下次重试时刻 |
| `DeleteOldEventsAsync` | 删除已处理或已丢弃、且 `Handled_Time` 早于保留期的记录 |

重试次数由宿主服务在进程内累计，达到 `MaxInboxRetryCount` 时由宿主调用 `MarkAsDiscardAsync`。

## 配置

配置节 `XiHan:EventBus:SqlSugar`（`XiHanSqlSugarEventBoxOptions.SectionName`）。

| 配置项 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `ClaimTimeout` | `TimeSpan` | `00:05:00` | 领取超时，超过该时长仍未完结的已领取记录可被重新领取，收发件箱共用；必须大于零，启动时校验 |
| `InboxRetentionPeriod` | `TimeSpan` | `7.00:00:00` | 收件箱已处理与已丢弃记录的保留期，也是去重窗口 |

轮询间隔、批量大小、收件箱最大重试次数与重试延迟属于 [EventBus](./eventbus) 的 `XiHan:EventBus:EventBoxes`，本包不改动。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `XiHanSqlSugarEventBusModule` | 模块类，声明依赖即启用 |
| `SqlSugarEventOutbox` | `IEventOutbox` 的 SqlSugar 实现，注册为 `Scoped` |
| `SqlSugarEventInbox` | `IEventInbox` 的 SqlSugar 实现，注册为 `Scoped` |
| `SysEventOutbox` / `SysEventInbox` | 收发件箱实体 |
| `EventOutboxMapper` / `EventInboxMapper` | 契约与实体的双向映射 |
| `XiHanSqlSugarEventBoxOptions` | 领取超时与收件箱保留期配置 |

## 注意事项与最佳实践

- **投递与处理都是至少一次**。发件箱在投递成功后才删除记录；收件箱在处理成功后才标记已处理。进程在两步之间退出，记录会被重新领取。处理器必须幂等。
- **先写业务数据、后发布事件**。发件箱按工作单元已登记的连接决定落点；先发布再写业务，事件会落在主库而业务落在模块库，两者不在同一个事务里，且不会报错。
- **一个工作单元只写一个库**。登记了多个连接时发件箱入箱抛异常，出路是拆小工作单元。
- **收件箱的去重窗口等于保留期**。清理之后同一消息再来会被当作新消息处理；没有消息标识的消息不参与去重。
- **`ClaimTimeout` 要大于单批最长处理时间**。宿主逐条顺序处理一批事件，整批超时后尾部记录会被其他实例重新领取。
- **收件箱的重试计数在进程内**。多实例轮流领到同一条失败事件、或进程重启后，计数从 1 重来，实际重试次数可能超过 `MaxInboxRetryCount`。
- **`IEventOutbox` / `IEventInbox` 的生命周期由单例改为作用域**。框架内没有构造函数注入这两个接口的地方，不会产生被捕获依赖；应用若自行把它们注入单例，需要改为从作用域解析。
- **`GetWaitingEventsAsync` 是领取不是查询**。调用后记录已被标记为已领取，不要在别处当作只读查询复用。
- **`filter` 参数未支持**。传入非空值会抛 `NotSupportedException`，而不是静默忽略。
- **租户独立库下入箱会抛异常**。发件箱的发送循环只遍历平台布局，写入租户独立库的事件永远不会被投递，因此入箱时若当前租户的数据库布局与平台布局的主库不同，`EnqueueAsync` 抛 `InvalidOperationException`（fail-closed）。字段隔离与行隔离的租户与平台共用主库，不受影响；收件箱的全部记录都在宿主布局主库。
- **发件箱投递失败要等满 `ClaimTimeout` 才重试，且没有次数上限**。投递失败的记录保持已领取状态，直到领取超时才会被重新领取；发件箱没有重试计数，会一直重试下去。
- **配额 `maxCount / 库数` 在积压集中于单一库时会压低吞吐**。每库领取量按库数均分，积压全在一个库里时，单次领取量只有 `maxCount` 除以库数。
- **业务实体在模块库又开启 `EnableDiffLog` 时，不要在同一工作单元内以 `onUnitOfWorkComplete: false` 发布分布式事件**。差异日志写入器经 `GetCurrentClient()` 把主库也登记进工作单元，登记变成两个库（业务模块库加主库），发件箱入箱随即抛「登记了多个数据库连接」的异常。避开方式：事件改在工作单元完成时发布（`onUnitOfWorkComplete: true`，此时不经过发件箱），或该类实体不开差异日志。

## 扩展点 / 自定义

需要完全自定义存储行为时，实现 `IEventOutbox` / `IEventInbox` 并在 DI 中 `Replace`，同时把 `XiHanDistributedEventBusOptions.Outboxes` / `Inboxes` 的 `ImplementationType` 指向自己的类型。

## 依赖模块

- [EventBus](./eventbus)：收发件箱契约与后台循环
- [Data](./data)：SqlSugar 客户端解析、工作单元连接登记、建表初始化

## 相关模块

- [EventBus.RabbitMQ](./eventbus-rabbitmq) / [EventBus.Kafka](./eventbus-kafka) / [EventBus.Redis](./eventbus-redis)：投递用的 Broker 提供程序，与本包正交——本包管「事件怎么存」，它们管「事件怎么发出去、怎么收进来」
- [Uow](./uow)：发件箱入箱所参与的工作单元
- [Auditing](./auditing)：审计日志，其差异日志写入器与发件箱入箱的连接登记有冲突，见上文注意事项
