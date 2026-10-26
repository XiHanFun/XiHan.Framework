# XiHan.Framework.Scheduling.InMemory

内存调度（Ring 3，默认实现，`role: default`）。提供 `scheduling.jobs@1`（进程内作业存储 `InMemoryJobStore` + 抽象包的 `JobSchedulerEngine`）与 `scheduling.leader@1`（进程内租约）（RFC-0013）。

```csharp
builder.Services.AddJob<NightlyCleanup>("cleanup.nightly", JobSchedule.FromCron("0 3 * * *"));
builder.AddXiHan(xihan => xihan.UseFeature<InMemorySchedulingFeature>());
```

```json
{ "XiHan": { "Scheduling": { "PollIntervalSeconds": 1, "MaxConcurrentRuns": 32, "MisfireThresholdSeconds": 60, "HistoryRetentionDays": 7, "InMemory": { "MaxRuns": 1000 } } } }
```

## 规则

- 作业、触发器、运行历史与节点都在内存里，读出来的是深拷贝；运行历史最多 `MaxRuns` 条，超出丢弃最旧的已结束记录（全部在运行中时 → `SCHEDULING_UNAVAILABLE_004`）。
- 存储不跨实例共享：集群唯一的作业（`RunOnEveryInstance = false`）只在持有领导租约的实例上按计划运行；`RunOnEveryInstance` 的作业每个实例各自运行。另有显式 Provider 提供 `scheduling.leader`（如 `Scheduling.Kubernetes`）时，引擎用那个选举。
- 引擎的语义（misfire、并发阻塞、暂停与恢复、动态登记、取消、历史）见 `Scheduling.Abstractions`；进程重启后运行时登记的作业与历史都丢失，需要持久化换 `Scheduling.Database`。
- 停止时等待进行中的运行结束并释放租约。健康项 `scheduling.inmemory.leader`。

## 依赖方向

依赖 `XiHan.Framework.Scheduling.Abstractions`。

## 状态

P5：可用，通过 `Scheduling.Conformance`（调度器、存储、领导选举三个套件）。
