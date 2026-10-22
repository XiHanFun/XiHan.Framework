# XiHan.Framework.Scheduling.InMemory

内存调度（Ring 3，默认实现，`role: default`）。提供 `scheduling.jobs@1` 与 `scheduling.leader@1`（RFC-0013）。

```csharp
builder.Services.AddJob<NightlyCleanup>("cleanup.nightly", JobSchedule.FromCron("0 3 * * *"));
builder.AddXiHan(xihan => xihan.UseFeature<InMemorySchedulingFeature>());
```

```json
{ "XiHan": { "Scheduling": { "InMemory": { "PollIntervalSeconds": 1, "LeaseResource": "scheduling.jobs", "LeaseTimeToLiveSeconds": 30 } } } }
```

## 规则

- 轮询调度器：每个 `PollInterval`（默认 1 秒）续约或竞争领导租约，然后把到期的作业交给 `JobDispatcher` 在后台运行（新作用域、经完整管道）；`LeaderOnly` 的作业只在持有租约时按计划运行；应用未进入服务状态时不运行。
- 间隔计划在上一次完成后再等一个间隔；cron 计划按 UTC 推算下一刻度；不允许并发的作业上一次未结束时跳过本次。
- 手动触发同步运行并等待结果，不受租约限制；运行中且不允许并发 → `SCHEDULING_CONFLICT_001`。
- 领导选举是进程内租约：单实例下正确；多实例换 `Scheduling.Database` 或 `Scheduling.Kubernetes`（Lease）。另有显式 Provider 提供 `scheduling.leader` 时，本包的调度器用那个选举。
- 停止时等待进行中的运行结束并释放租约。健康项 `scheduling.inmemory.leader`。

## 依赖方向

依赖 `XiHan.Framework.Scheduling.Abstractions`。

## 状态

P3：可用，通过 `Scheduling.Conformance`。
