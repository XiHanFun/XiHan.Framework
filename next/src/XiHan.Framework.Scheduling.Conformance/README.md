# XiHan.Framework.Scheduling.Conformance

调度一致性套件（RFC-0013）。Provider 的测试项目按提供的能力继承下列抽象类并实现工厂方法，全部用例随之执行：

- `JobSchedulerConformance`：实现 `StartAsync(configure, configuration)` 启动一个装配了被测调度器的宿主，返回 `JobSchedulerHost(scheduler, poll, host)`。回调 `configure` 里套件登记 `RecordingJob`、`RecordingJobLog` 与本用例的时钟，要在 Provider 装配之前应用（`TestHost` 的服务回调即是）；`poll` 执行一个轮询周期并等本周期起的运行连同收尾结束，即 `JobSchedulerEngine.PollOnceAsync`。Provider 把轮询周期设得很长（如 `XiHan:Scheduling:PollIntervalSeconds = 3600`），让后台循环不动作。用例覆盖手动触发经管道运行并记录结果、未登记的作业、间隔计划反复运行、不允许并发时的冲突、作业异常翻译为失败、状态报告、失败重试（成功即止、次数用尽即止、每实例作业在本实例重试）、执行时限超时与取消不重试、重试策略与时限的运行时修改和恢复（ADR-0060）。`RecordingJob` 认运行数据里的 `failBeforeAttempt` 与 `delayMilliseconds`。
- `JobStoreConformance`：实现 `CreateAsync()`，返回 `(IJobStore Store, IAsyncDisposable Host)`；用例覆盖作业写入 / 查找 / 列表 / 替换 / 级联删除与快照语义、触发器全字段往返、到期抢占跨节点原子且按到期时刻与优先级排序、独占运行与阻塞（`BlockTriggerAsync`）/ 完成解除 / 查询 / 取消 / 清理、节点心跳与失联恢复。
- `LeaderElectionConformance`：实现 `CreateElection(clock)`，选举与租约按传入的时钟判定到期；用例推进时钟而不是等待真实时间，覆盖获取、被占用时拒绝、释放后可再获取、过期后可被他人获取且原持有者续约失败、续约延长到期。

```csharp
public sealed class InMemoryJobSchedulerConformanceTests : JobSchedulerConformance
{
    protected override async Task<JobSchedulerHost> StartAsync(Action<IServiceCollection> configure, IEnumerable<KeyValuePair<string, string?>>? configuration = null)
    {
        var settings = new List<KeyValuePair<string, string?>> { new("XiHan:Scheduling:PollIntervalSeconds", "3600") };
        settings.AddRange(configuration ?? []);
        var host = await TestHost.StartAsync(static b => b.UseFeature<InMemorySchedulingFeature>(), settings, configure);
        var engine = host.Services.GetRequiredService<JobSchedulerEngine>();
        return new JobSchedulerHost(engine, engine.PollOnceAsync, host);
    }
}
```

## 规则

- 调度器用例由用例驱动时间（ADR-0074）：套件登记一个只由用例推进的时钟（`FakeTimeProvider`），引擎的到期判定、领导租约、作业的停留（`RecordingJobLog.Delay`、`delayMilliseconds`）与执行时限都按它计时。用例推进时钟、经 `JobSchedulerHost.PollAsync` 轮询一次再断言，不靠后台轮询循环，也不按墙钟等待（没有 `Task.Delay`、没有按条件轮询的等待），结果与机器快慢、负载无关。
- 要等作业开始（手动触发正在运行、轮询起的运行停在时钟上）时，`RecordingJobLog.WaitAsync` 由作业的记录唤醒；`WaitTimeout`（缺省 10 秒）只是防止用例挂死的上限。
- 作业要等时钟推进才结束时（停留、执行时限），先发起轮询或触发，等作业开始，再推进时钟，最后等调用完成。

## 依赖方向

依赖 `XiHan.Framework.Scheduling.Abstractions`、xunit 的断言与扩展核心包，以及提供手动时钟的 `Microsoft.Extensions.TimeProvider.Testing`。

## 状态

P3：可用。
