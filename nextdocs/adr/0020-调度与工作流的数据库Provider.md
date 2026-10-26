# ADR-0020 调度与工作流的数据库 Provider：自实现 Quartz 能力、工作流能力补齐

## 状态

已采纳（RFC-0013 / RFC-0021 修订）。

## 日期

2026-10-26

## 背景

用户于 2026-10-26 要求做 `Scheduling.Database`，并且"不引入 Quartz，自己实现它的这些功能"；随后要求"`Workflow.Database` 也实现，并且 Workflow 的功能要全，和主流的 Workflow 框架能力对比不能缺功能"。

此前的调度子系统（RFC-0013 初版）只有"启动时登记的作业 + 单个计划 + 领导租约下的单进程轮询"：没有持久化的作业存储、不能在运行时增删作业、一个作业只有一个触发器、cron 只有 5 段 UTC、没有 misfire 策略、暂停、运行历史与集群分发。与 Quartz 对比的差距清单见会话记录。工作流子系统（RFC-0021 初版）已有图定义、15 个内置活动、用户任务三种完成策略、子流程、重试与补偿、挂起 / 恢复 / 取消 / 终止，但与 Elsa 3、Camunda / Flowable、Temporal、Workflow Core 对比缺：数据库 Provider、信号 / 定时触发式启动、事件网关、外部任务、HTTP 活动、显式补偿活动与故障补偿、输出映射、实例级超时、跳转、迁移、改变量、保留期清理、认领 / 释放 / 退回 / 催办、提醒、审批阈值、延迟启动与幂等启动。

## 备选

1. 调度：引入 Quartz.NET 作为 `Scheduling.Quartz` Provider，把 `IJobScheduler` 架在 `IScheduler` 上。
2. 调度：把引擎留在各 Provider 里（内存一份、数据库一份），各自实现 misfire、并发、恢复。
3. 调度：引擎进抽象包，Provider 只实现作业存储 `IJobStore`（与工作流"引擎在抽象包、Provider 只做存储"同构），集群分发用存储的原子抢占（数据库并发戳 CAS）。
4. 工作流：数据库 Provider 用调用方的工作单元（存储在请求事务内写）。
5. 工作流：数据库 Provider 的存储与锁是单例，每个操作在自己的管道调用（Transaction 阶段）里提交。
6. 工作流：`Http` / `Script` 活动继续不进框架。
7. 工作流：加 `Http` 活动（`WorkflowHttpClient`，应用可替换），`Script` 仍不进框架（表达式语言 + `SetVariable` 覆盖，脚本引擎是第三方依赖）。

## 决策

- **取 3**。`Scheduling.Abstractions` 新增 `IJobStore`（作业、触发器、运行历史、节点；`AcquireDueAsync` 原子抢占、`TryBeginRunAsync` 独占开始、`CompleteRunAsync` 释放与解除阻塞、`RecoverNodesAsync` 失联恢复）与引擎 `JobSchedulerEngine`（每周期：心跳 → 领导租约 → 领导者维护（失联恢复、历史清理）→ 取消请求 → 本地触发器 → 抢占到期触发器并运行）。作业与触发器分离（`JobDefinition.Triggers`、`JobTriggerDefinition`：计划、数据、优先级、misfire 策略），计划扩成 `Manual` / `Every(interval, repeatCount)` / `FromCron(expr, timeZoneId)` / `Once(at)` 加 `StartAt` / `EndAt` / `Calendar`，文本往返持久化；cron 重写为 Quartz 文法超集（5 / 6 / 7 段、`?`、`L`、`W`、`#`、名字、时区与夏令时），星期保留 0/7 = 周日的 Unix 约定；日历 `IJobCalendar`（`Holiday` / `Weekly` / `Daily` / `Cron` / `Composite`）按名登记；misfire 三策略；`IJobScheduler` 全批量：`Schedule` / `Unschedule` / `Pause` / `Resume`（作业与触发器两级）/ `Trigger`（本地等待）/ `Enqueue`（集群任一节点）/ `CancelRuns` / `Status` / `Runs`。`LeaderOnly` 取反改名 `RunOnEveryInstance`；`JobDispatcher` 按已登记的作业类型键派发（`AddJobType<T>()`），运行时登记的作业引用类型键而不是类型。`Scheduling.InMemory` 只剩 `InMemoryJobStore`（有界历史）与进程内选举；不共享的存储下集群唯一的作业只在领导者上运行。`Scheduling.Database`：五张表经仓储读写，抢占与租约接管是并发戳 CAS，每个存储操作一个事务；节点心跳与失联恢复由领导者执行，`RequestsRecovery` 的运行以 `Recovery` 触发重新入队。`Scheduling.Quartz` 从计划里删除。
- **取 5**。仓储只能在管道 Transaction 阶段用；若用调用方的工作单元，实例锁会在请求事务提交之前释放，其他节点读到旧状态。所以 `Workflow.Database` 的三个存储、实例锁、轮询领导权都是单例并各自在新作用域 + `App.InvokeAsync` 里执行，返回即提交；实例锁是租约行（并发戳 CAS 接管过期租约、持有期间续约）；轮询只在持有 `workflow.poller` 租约的节点上进行。同刻排序用 `Sequence` 列（UUID v7 同毫秒不单调）；`DateTimeOffset?` 条件只写范围比较（SqlSugar 对 `!= null` 的翻译不可用）。
- **取 7**。工作流新增：`Start` 的 `SignalName`（信号启动）与 `Cron` / `TimeZone`（定时启动：定义级 `DefinitionTimer` 书签，`InstanceId` = 定义 ID，发布时排、到期由轮询启动并再排、停用 / 归档 / 删除时删）；活动 `EventGateway`、`Compensate`、`Http`、`ExternalTask`；`Delay` 的 `Until` / `Cron`；节点 `OutputMappings`；定义 `CompensateOnFault`、`Timeout` + `TimeoutAction`（`InstanceTimeout` 书签）；`WorkflowStartRequest.StartAt`（开始节点挂起到该时刻）与 `ReusePolicy`；引擎 `SetVariables` / `Jump` / `Migrate` / `Purge`（`RetentionDays` 时由轮询器按 `HousekeepingIntervalSeconds` 自动清理）；任务服务 `Query` / `Claim` / `Release` / `Return`（以跳转实现）/ `Urge`，`UserTask` 的 `ApprovalThreshold`（`All` 下按人数或百分比）、`Priority` / `DueAt` / `FormKey` / `ReminderAfterSeconds`（`Reminder` 书签由引擎处理）；`IWorkflowExternalTaskService`（领取加锁、完成、失败重试、延长锁，Camunda 外部任务模式）；活动描述符 `ActivityDescriptor`（设计器用）；`WorkflowInstanceQuery` 加多状态 / 发起人 / 时间范围 / 结束时间 / 分页与 `CountAsync`。`Workflow.Abstractions` 引用 `Scheduling.Abstractions` 取 cron（同环契约包，允许）。
- 新错误码：`SCHEDULING_NOTFOUND_002`（作业类型未登记）、`_003`（触发器不存在）、`_004`（运行不存在或已结束）、`SCHEDULING_VALIDATION_001`、`SCHEDULING_CONFLICT_003`（运行被取消）、`SCHEDULING_UNAVAILABLE_003` / `_004`；`WORKFLOW_CONFLICT_005`（同关联已有未结束实例）、`WORKFLOW_DENIED_002`（外部任务锁）。
- 明确不做：BPMN XML 导入（设计器层面的事，定义 JSON 已能往返）、脚本活动（第三方脚本引擎）、Quartz 的 `JobListener` 接口（以 `scheduling.*` 事件消息替代）。

## 理由

- 把引擎放进抽象包让"内存"与"数据库"两个 Provider 的 misfire、并发、恢复语义只有一份实现与一份套件（`JobSchedulerConformance` 12 例 + `JobStoreConformance` 5 例 + `LeaderElectionConformance`），与工作流子系统的分层一致；自实现而不引入 Quartz 让契约不被 Quartz 的 `JobKey` / `TriggerKey` / `JobDataMap` 类型和它的 ADO 作业存储表结构绑死，并且天然 AOT 可用、与内核管道同构。
- 数据库 Provider 每操作一个事务牺牲了"一次波次写回原子性"（内存实现本来也不是原子的），换来跨节点的正确互斥；这是实例锁语义的前提。
- 工作流补齐的每项能力都有主流引擎的对应物（Elsa 的触发器与活动描述符、Camunda 的外部任务 / 事件网关 / 实例迁移 / 修改 / 补偿抛出事件 / 历史清理、Flowable 的认领与截止 / 提醒、Temporal 的 WorkflowIdReusePolicy 与延迟启动、国内 OA 的退回 / 催办 / 会签阈值），且都落在已有的"节点 + 书签 + 波次"模型上，没有新的执行模型。

## 影响

- `Scheduling.Abstractions` 公开面大改（Preview）：`JobDefinition` 去掉 `Schedule` / `LeaderOnly`、加 `Triggers` / `Data` / `RunOnEveryInstance` / `RequestsRecovery`；`IJobScheduler` 的 `Status` / `Statuses` 改异步并增加 9 个方法；`JobDispatcher.DispatchAsync` 多一个类型参数；`InMemoryJobScheduler` / `InMemorySchedulingOptions` 的轮询与租约配置移到 `XiHan:Scheduling`。调用方迁移：`LeaderOnly = false` → `RunOnEveryInstance = true`，`scheduler.Status(name)` → `await scheduler.StatusAsync(name, ct)`。
- 新包 `Scheduling.Database`、`Workflow.Database`（都依赖 `persistence.relational@1`，SQLite 下通过全部套件）；`Scheduling.Conformance` 新增 `JobStoreConformance`；`Workflow.Conformance` 新增 `WorkflowTaskConformance` 与引擎套件 9 个新用例。
- 04 表：`Scheduling.Database` 与 `Workflow.Database` 改为 P5 已完成，`Scheduling.Quartz` 删除；05 计划新增 P5-11。
