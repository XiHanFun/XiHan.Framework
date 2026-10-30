# XiHan.Framework.Scheduling.Abstractions

调度契约与引擎（Ring 2，RFC-0013，ADR-0020）。能力 `scheduling.jobs@1`（作业）与 `scheduling.leader@1`（领导选举）由 Provider 提供：引擎在本包，Provider 只实现作业存储 `IJobStore` 与领导选举；默认实现 `Scheduling.InMemory`，多实例换 `Scheduling.Database`。

| 契约 | 用途 |
| --- | --- |
| `JobName`、`TriggerKey`、`JobDefinition`、`JobTriggerDefinition` | 作业名（主题文法）；作业（类型、触发器、数据、`AllowConcurrent`、`RunOnEveryInstance`、`RequestsRecovery`）与触发器（计划、数据、优先级、misfire 策略） |
| `JobSchedule`、`CronExpression`、`MisfirePolicy` | 计划：`Manual` / `Every(interval, repeatCount)` / `FromCron(expr, timeZoneId)` / `Once(at)`，可带 `StartAt` / `EndAt` / `Calendar`，文本往返；cron 为 Quartz 文法超集（5 / 6 / 7 段、`?`、`L`、`W`、`#`、名字、时区与夏令时；星期 0/7 = 周日） |
| `IJobCalendar`、`JobCalendars`、`HolidayCalendar` / `WeeklyCalendar` / `DailyCalendar` / `CronCalendar` / `CompositeCalendar`、`JobPlanner` | 排除日历按名登记（`AddJobCalendar`），推算下一次时跳过被排除的时刻 |
| `IJob`、`AddJob<TJob>(...)`、`AddJobType<TJob>()`、`JobRegistry`、`JobTypeRegistry` | 作业是作用域服务；代码登记的作业启动时写入存储；运行时登记的作业引用类型键；`JobTypeRegistry.Keys` 列出可选类型（ADR-0053） |
| `JobDispatcher`、`JobRun` | 每次运行在新作用域、新上下文（关联 ID = 运行 ID）里经完整内核管道执行；`Data` = 作业数据 ← 触发器数据 ← 手动数据；异常翻译为 `SCHEDULING_UNEXPECTED_001` |
| `IJobScheduler`、`JobStatus`、`JobTriggerStatus`、`JobRunRecord`、`JobRunQuery` | 全批量：登记 / 删除 / 暂停 / 恢复（作业与触发器两级）、本地触发并等待、入队给集群、取消运行、状态、运行历史（按作业、状态、时间、触发器、节点、有无错误筛选，`CountRunsAsync` 给总数）、`RestoreAsync` 把运行时改过的代码作业恢复为代码定义 |
| `IJobStore`、`JobRecord`、`TriggerRecord`、`SchedulerNodeRecord` | Provider 契约：原子抢占到期触发器、独占开始运行、完成释放并解除阻塞、历史与节点、失联恢复 |
| `JobSchedulerEngine`、`SchedulerOptions` | 引擎（Provider 装配并启停）：轮询周期、节点 ID、并发上限、misfire 阈值、租约、节点失联、维护周期、历史保留 |
| `ILeaderElection`、`LeaderLease` | 租约式领导选举：获取、续约、释放、过期 |
| `SchedulingTopics`、`SchedulingEventData` | 装配了消息能力时发布 `scheduling.job.*` / `scheduling.run.*` 事件 |

```csharp
builder.Services.AddJob<NightlyCleanup>("cleanup.nightly", JobSchedule.FromCron("0 0 3 * * *", "Asia/Shanghai"));
builder.Services.AddJobCalendar("holidays", new HolidayCalendar([new DateOnly(2026, 10, 1)]));
builder.Services.AddJob<Report>(new JobDefinition("report", typeof(Report))
{
    Triggers = [new JobTriggerDefinition("weekly", JobSchedule.FromCron("0 0 9 * * MON") with { Calendar = "holidays" }) { MisfirePolicy = MisfirePolicy.RunOnce }],
    Data = new JsonObject { ["format"] = "pdf" },
    RequestsRecovery = true,
});

// 运行时登记（持久化在存储里，集群其他节点下个周期看到）
await scheduler.ScheduleAsync(new JobDefinition("tenant-42.sync", JobSchedule.Every(TimeSpan.FromMinutes(10)), typeof(TenantSync)) { Data = new JsonObject { ["tenant"] = "42" } }, ct);
await scheduler.PauseAsync("tenant-42.sync", ct);
var outcome = await scheduler.TriggerAsync("cleanup.nightly", ct);      // 本实例立即运行并等待结果
var runs = await scheduler.RunsAsync(new JobRunQuery { Job = "report", Limit = 20 }, ct);
var failed = await scheduler.CountRunsAsync(new JobRunQuery { Job = "report", HasError = true }, ct);
```

代码登记的作业被运行时 `ScheduleAsync` 改过后来源是 `Overridden`：启动同步时代码定义没变（指纹 `CodeFingerprint` 相同）就保留运行时的修改，代码改了以代码为准并记警告；`RestoreAsync` 随时恢复为代码定义（ADR-0053）。

未登记 → `SCHEDULING_NOTFOUND_001`；类型未登记 → `_002`；触发器不存在 → `_003`；运行不存在 → `_004`；没有代码定义（不能恢复）→ `_005`；定义不合法 → `SCHEDULING_VALIDATION_001`；运行中且不允许并发 → `SCHEDULING_CONFLICT_001`；租约被占用 → `_002`；运行被取消 → `_003`；存储不可达 → `SCHEDULING_UNAVAILABLE_003`。

## 依赖方向

依赖 `XiHan.Framework.Kernel`、`XiHan.Framework.Localization.Abstractions`、`XiHan.Framework.Messaging.Abstractions`。被 `Scheduling.InMemory`、`Scheduling.Database`、`Scheduling.Kubernetes`、`Scheduling.Conformance`、`Workflow.Abstractions`（cron）引用。

## 状态

P5：可用（Preview）。
