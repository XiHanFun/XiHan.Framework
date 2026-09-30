# XiHan.Framework.EventBus.SqlSugar

## 概述

`XiHan.Framework.EventBus` 的收发件箱 SqlSugar 持久化提供程序。默认的 `DefaultEventOutbox` / `DefaultEventInbox` 是进程内实现：发件箱的事件与业务数据不在同一事务、进程退出即丢；收件箱的去重与待处理事件都在内存里，多实例下同一事件会被每个实例各处理一次。本包把两者落到数据库。

## 核心能力

- 发件箱实体 `sys_event_outbox` 与 `OutgoingEventInfo` 的双向映射
- 发件箱入箱写在调用方工作单元已登记的那个库上；在未完成的事务型工作单元内入箱时与业务数据同一事务（发件箱何时被写入见「配置与约定 / 发件箱」）
- 发送端遍历当前布局的全部库，单个库不可达时只跳过该库
- 收件箱实体 `sys_event_inbox` 与 `IncomingEventInfo` 的双向映射
- 收件箱按消息标识去重：入箱前先查，唯一索引兜住多实例同时入箱的竞态
- 收发件箱的领取都是多实例互斥：条件抢占 + 领取超时释放，不依赖任何数据库方言特性
- 已处理与已丢弃的收件箱记录保留一段时间后清理，保留期即去重窗口
- 表结构由 `DbInitializer` 在应用启动时创建，**必须开启** `XiHan:Data:SqlSugarCore` 下的 `EnableDbInitialization` 与 `EnableTableInitialization`（均默认 `false`）

## 依赖关系

依赖 `XiHan.Framework.EventBus`（收发件箱契约）与 `XiHan.Framework.Data`（SqlSugar 数据访问）。

## 配置与约定

表名 `sys_` 前缀、全小写下划线，不分表；列名 Pascal_Snake_Case；主键为事件自身的 `Guid` 标识，非自增。

两张表都不是分表，没有 SqlSugar 插入时自动建表的兜底，因此未开启建表初始化时首次入箱即抛「表不存在」：发件箱会使所在业务事务一同失败，收件箱会使该条消息消费失败并由 broker 重投。自行维护表结构时按本包实体的列定义建表，收件箱还需建出 `Dedup_Key` 列上的唯一索引 `ux_sys_event_inbox_dedup_key`。

配置节 `XiHan:EventBus:SqlSugar`：

| 配置项 | 默认值 | 说明 |
| --- | --- | --- |
| `ClaimTimeout` | `00:05:00` | 领取超时，超过该时长仍未完结的已领取记录可被重新领取，收发件箱共用；必须大于零，启动时校验 |
| `InboxRetentionPeriod` | `7.00:00:00` | 收件箱已处理与已丢弃记录的保留期，超过该时长的记录被清理 |

本包把 `IEventOutbox` 与 `IEventInbox` 的注册生命周期都由单例改为作用域——两者都经作用域内的 `ISqlSugarClientResolver` 取连接。框架内没有构造函数注入这两个接口的地方。

### 发件箱

发件箱只在调用方于**未完成的工作单元内**以 `onUnitOfWorkComplete: false` 发布、且配置了至少一个 `Outboxes` 时才会被写入；工作单元为事务型时事件行与业务数据同一个事务，非事务型时各自提交。按默认的 `onUnitOfWorkComplete: true` 发布时，工作单元先提交、再发布缓冲的分布式事件，此时已没有当前工作单元，`AddToOutboxAsync` 返回 `false`，事件**绕过发件箱直接发出**——本包对这条路径不起作用。

投递语义为**至少一次**：宿主在投递成功后才删除记录，若进程在投递与删除之间退出，记录会被重新领取并再次投递，消费端需幂等。

删除按库执行，某个库删除失败时只记录日志并跳过，不会抛给调用方；该库上的记录留在原地，之后每次轮询都会被重新领取、重新投递，直到该库恢复为止。

事件行的落库由当前工作单元已登记的连接决定：恰好一个时写该库，一个都没有时写当前库，多于一个时抛 `InvalidOperationException`。因此业务代码应**先写业务数据、后发布事件**——反过来会让事件落在主库而业务落在模块库，两者不在同一个事务里，且不会报错。

领取配额在当前布局的各库间平均分配：每库最多领取 `maxCount` 除以库数的整数商，且每库至少领取 1 条；库数超过 `maxCount` 时，单次领取的总量等于库数。

发件箱表由 `[TableInitialization(IncludeModuleConnections = true)]` 声明进入所有库，主库与每个模块库都会建出 `sys_event_outbox`。

从旧版本升级的既有部署，本版本会开始把事件行按业务落库位置路由进各模块库，因此升级前须确保 `sys_event_outbox` 在每个模块库中已存在——开启上述两个自动建表选项，或手工在各模块库中建出该表。二者都没做时，第一次向该模块库写入的事件会在建表失败（缺表）时报错，且这次失败发生在业务事务内部。

主库与静态配置的模块库在 `SqlSugarScope` 构建时一并建连，进程重启后不存在「主库先建连、模块库延后建连」的时间差，待发事件不会因此暂时无法被领取。

发送循环运行在无租户上下文的后台作用域，只遍历默认布局；当前租户使用独立库时，入箱会抛 `InvalidOperationException`。

### 收件箱

收件箱的全部读写都切换到无租户上下文，落在宿主布局的主库；`sys_event_inbox` 只在平台主库建表，不进模块库与租户独立库。库隔离租户收到的事件也存在这里。

去重键是消息标识，不是事件标识——框架在每次收到消息时都会生成新的事件标识。没有消息标识的消息不参与去重，每次都入箱、都处理。

去重窗口等于保留期：已处理记录清理之后，同一消息再来会被当作新消息处理。处理器仍须幂等。

标记已处理、已丢弃只改状态、不删记录；清理只删除已处理或已丢弃、且完结时刻早于保留期的记录，待处理与已领取的记录无论多旧都保留。宿主每轮轮询都会调用一次清理。

重试次数由宿主服务在进程内累计并传入，本包只把它记在 `Retry_Count` 列上；是否丢弃由宿主按 `XiHan:EventBus:EventBoxes:MaxInboxRetryCount` 判定。多实例轮流领到同一条失败事件、或进程重启后，计数从 1 重来。

宿主逐条顺序处理一批事件。整批处理耗时超过 `ClaimTimeout` 时，尾部记录会被其他实例重新领取并处理，因此 `ClaimTimeout` 须大于单批最长处理时间。

消息标识最长 256 个字符，更长的消息标识在严格模式的数据库上入箱失败。

## 使用方式

在应用启动模块上声明依赖 `XiHanSqlSugarEventBusModule`。

## 扩展点

需要自定义存储行为时，实现 `XiHan.Framework.EventBus.Abstractions.Distributed` 下的 `IEventOutbox` / `IEventInbox` 并在 DI 中 `Replace`，同时把 `XiHanDistributedEventBusOptions.Outboxes` / `Inboxes` 的 `ImplementationType` 指向自己的类型。

## 目录结构

```
Entities/                        收发件箱实体
Mapping/                         契约与实体的双向映射
Options/                         存储配置
Outbox/                          发件箱实现
Inbox/                           收件箱实现
Extensions/DependencyInjection/  服务注册扩展
```
