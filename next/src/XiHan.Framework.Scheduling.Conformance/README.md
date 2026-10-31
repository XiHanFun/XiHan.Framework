# XiHan.Framework.Scheduling.Conformance

调度一致性套件（RFC-0013）。Provider 的测试项目继承两个抽象类并实现工厂方法，全部用例随之执行：

- `JobSchedulerConformance`：实现 `StartAsync(configure)` 启动一个装配了被测调度器的宿主（回调里套件会登记 `RecordingJob` 与 `RecordingJobLog`）；用例覆盖手动触发经管道运行并记录结果、未登记的作业、间隔计划反复运行、不允许并发时的冲突、作业异常翻译为失败、状态报告、失败重试（成功即止、次数用尽即止、每实例作业在本实例重试）、执行时限超时与取消不重试、重试策略与时限的运行时修改和恢复（ADR-0060）。`RecordingJob` 认运行数据里的 `failBeforeAttempt` 与 `delayMilliseconds`。
- `LeaderElectionConformance`：实现 `CreateElection(clock)`，选举与租约按传入的时钟判定到期；用例推进时钟而不是等待真实时间，覆盖获取、被占用时拒绝、释放后可再获取、过期后可被他人获取且原持有者续约失败、续约延长到期。

```csharp
public sealed class InMemoryJobSchedulerConformanceTests : JobSchedulerConformance
{
    protected override async Task<JobSchedulerHost> StartAsync(Action<IServiceCollection> configure)
    {
        var host = await TestHost.StartAsync(b => b.UseFeature<InMemorySchedulingFeature>(), [...], configure);
        return new JobSchedulerHost(host.Services.GetRequiredService<IJobScheduler>(), host);
    }
}
```

## 依赖方向

依赖 `XiHan.Framework.Scheduling.Abstractions` 与 xunit 的断言与扩展核心包。

## 状态

P3：可用。
