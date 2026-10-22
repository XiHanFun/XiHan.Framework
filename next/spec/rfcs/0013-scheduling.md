# RFC-0013 调度与领导选举

> 状态：已采纳 | 日期：2026-10-23 | 对应 ADR：— | 参考实现：`XiHan.Framework.Scheduling.Abstractions`、`XiHan.Framework.Scheduling.InMemory` | 一致性：`XiHan.Framework.Scheduling.Conformance`

## 摘要

调度子系统把"按计划运行的工作"做成与请求同构的调用：作业是作用域服务，每次运行在新作用域、新上下文里经完整内核管道执行；调度器只负责"何时运行"与"谁来运行"（领导选举）。Provider 实现两个能力。

## 能力

| 能力 | 含义 | 默认实现 |
| --- | --- | --- |
| `scheduling.jobs@1` | 作业登记、按计划运行、手动触发、状态，`IJobScheduler` | `Scheduling.InMemory`（单进程轮询） |
| `scheduling.leader@1` | 租约式领导选举，`ILeaderElection` | `Scheduling.InMemory`（进程内租约；多实例换 `Scheduling.Database` / `.Kubernetes`） |

## 作业

- 名字 `JobName`：`^[a-z0-9][a-z0-9._-]{0,127}$`（与主题文法相同）；领导选举的资源名同文法。
- 定义 `JobDefinition`：名字、计划、作业类型、`LeaderOnly`（默认 `true`：只在持有领导租约的实例上按计划运行）、`AllowConcurrent`（默认 `false`：上一次未结束时跳过本次、手动触发返回 `SCHEDULING_CONFLICT_001`）。
- 计划 `JobSchedule`：`Manual`（只手动触发）、`Every(interval)`（上一次完成后再等一个间隔）、`FromCron("m h dom mon dow")`（5 段 cron，UTC，支持 `*`、列表、范围、步长；日与周同时限定时取并集）。
- 登记：`services.AddJob<TJob>(name, schedule)`，作业是作用域服务；同名重复登记是编程错误。
- 运行：`JobDispatcher` 为每次运行开新作用域，构造上下文（关联 ID = 运行 ID，运行信息可由 `JobDispatcher.GetRun(context)` 取得），经完整管道执行 `IJob.RunAsync`；返回失败或抛出异常都算失败（异常翻译为 `SCHEDULING_UNEXPECTED_001`）。不写幂等键。
- `IJobScheduler`：`Trigger(names[])` 立即运行并等待结果（逐项结果，不受领导限制），`Status(name)` / `Statuses()`（下一次运行时间、最近一次运行的起止与结果、是否运行中）。未登记 → `SCHEDULING_NOTFOUND_001`。
- 调度器在应用进入服务状态之前不按计划运行；停止时等待进行中的运行结束。

## 领导选举

- `ILeaderElection.TryAcquire(resource, ttl)`：空闲（或租约已过期）时授予 `LeaderLease`；被持有返回 `SCHEDULING_CONFLICT_002`。
- `LeaderLease.Renew()` 延长 `ttl`；`Release()` 立即放弃；过期后他人可获取，原持有者续约失败。不可重入。
- 默认实现的调度器以资源 `XiHan:Scheduling:InMemory:LeaseResource`（默认 `scheduling.jobs`）竞争租约，每个轮询周期续约。

## 一致性

Provider 必须继承 `JobSchedulerConformance` 与 `LeaderElectionConformance` 并通过全部用例。过期用例用真实时间等待。

## 错误码

见 `conventions/error-codes.md` 中 `SCHEDULING_*`。
