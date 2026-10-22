# XiHan.Framework.Scheduling.Abstractions

调度契约（Ring 2，RFC-0013）。能力 `scheduling.jobs@1`（作业）与 `scheduling.leader@1`（领导选举）由 Provider 提供；默认实现 `Scheduling.InMemory`。

| 契约 | 用途 |
| --- | --- |
| `JobName`、`JobSchedule`、`CronExpression`、`JobDefinition` | 作业名（主题文法）、计划（手动 / 间隔 / 5 段 cron，UTC）、定义（类型、`LeaderOnly`、`AllowConcurrent`） |
| `IJob`、`AddJob<TJob>(name, schedule)`、`JobRegistry` | 作业是作用域服务；登记表 |
| `JobDispatcher` | 每次运行在新作用域、新上下文（关联 ID = 运行 ID）里经完整内核管道执行；异常翻译为 `SCHEDULING_UNEXPECTED_001` |
| `IJobScheduler`、`JobStatus`、`JobOutcome` | 批量触发（逐项结果）、状态（下一次运行、最近一次结果、是否运行中） |
| `ILeaderElection`、`LeaderLease` | 租约式领导选举：获取、续约、释放、过期 |

```csharp
builder.Services.AddJob<NightlyCleanup>("cleanup.nightly", JobSchedule.FromCron("0 3 * * *"));
builder.Services.AddJob<HealthProbe>("probe", JobSchedule.Every(TimeSpan.FromSeconds(30)));
var outcome = await scheduler.TriggerAsync("cleanup.nightly", ct);      // 立即运行并等待结果
```

未登记 → `SCHEDULING_NOTFOUND_001`；运行中且不允许并发 → `SCHEDULING_CONFLICT_001`；租约被占用 → `SCHEDULING_CONFLICT_002`。

## 依赖方向

依赖 `XiHan.Framework.Kernel` 与 `XiHan.Framework.Localization.Abstractions`。被 `Scheduling.InMemory`、`Scheduling.Conformance` 引用。

## 状态

P3：可用。
