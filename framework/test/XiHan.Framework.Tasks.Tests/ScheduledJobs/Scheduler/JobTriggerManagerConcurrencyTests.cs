// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Scheduler;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Scheduler;

/// <summary>
/// JobTriggerManager 并发写入测试
/// </summary>
/// <remarks>
/// 触发计数原来写在 ConcurrentDictionary.AddOrUpdate 的 updateValueFactory 里：该委托在竞争下可能被
/// 重复执行，且 TriggerCount++ 本身不是原子操作。调度定时器回调与手动触发会同时进入这条路径，
/// 计数一旦丢失，CompositeJobScheduler.ShouldFire 的 RepeatCount 上限判断就会失准。
/// 这里用大批并发写者把竞争压出来：断言只在全部写者结束后读，不依赖任何时序假设。
/// </remarks>
public class JobTriggerManagerConcurrencyTests
{
    /// <summary>
    /// 并发用例的兜底超时
    /// </summary>
    private const int TimeoutMilliseconds = 60_000;

    /// <summary>
    /// 并发等待预算
    /// </summary>
    private static readonly TimeSpan WaitBudget = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 固定基准时间，避免依赖真实时钟
    /// </summary>
    private static readonly DateTimeOffset BaseTime = new(2024, 6, 12, 8, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 多线程并发记录同一任务的触发时，计数一次都不能丢
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RecordTrigger_UnderConcurrentWriters_DoesNotLoseCounts()
    {
        var manager = new JobTriggerManager();
        const int WriterCount = 500;

        var writers = Enumerable.Range(0, WriterCount)
            .Select(offset => Task.Run(() => manager.RecordTrigger("job-hot", BaseTime.AddSeconds(offset))))
            .ToArray();

        await Task.WhenAll(writers).WaitAsync(WaitBudget, TestContext.Current.CancellationToken);

        var state = manager.GetTriggerState("job-hot");
        Assert.NotNull(state);
        Assert.Equal((long)WriterCount, state!.TriggerCount);
        Assert.Single(manager.GetAllTriggerStates());
    }

    /// <summary>
    /// 并发首次记录时只会建出一份状态，计数从 1 起算且不重复计入
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RecordTrigger_WhenManyWritersRaceOnFirstRecord_CreatesSingleState()
    {
        var manager = new JobTriggerManager();
        const int WriterCount = 200;

        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writers = Enumerable.Range(0, WriterCount)
            .Select(_ => Task.Run(async () =>
            {
                await barrier.Task;
                manager.RecordTrigger("job-cold", BaseTime);
            }))
            .ToArray();

        barrier.SetResult();
        await Task.WhenAll(writers).WaitAsync(WaitBudget, TestContext.Current.CancellationToken);

        var state = manager.GetTriggerState("job-cold");
        Assert.NotNull(state);
        Assert.Equal("job-cold", state!.JobName);
        Assert.Equal((long)WriterCount, state.TriggerCount);
        Assert.Equal(BaseTime, state.LastFireTime);
        Assert.Single(manager.GetAllTriggerStates());
    }

    /// <summary>
    /// 记录触发与重排下次触发时间并发进行时，两类写入互不吞掉对方
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RecordTriggerAndUpdateNextFireTime_Concurrently_KeepBothWrites()
    {
        var manager = new JobTriggerManager();
        const int RoundCount = 300;
        var nextFireTime = BaseTime.AddHours(1);

        var recorders = Enumerable.Range(0, RoundCount)
            .Select(_ => Task.Run(() => manager.RecordTrigger("job-hot", BaseTime)));
        var reschedulers = Enumerable.Range(0, RoundCount)
            .Select(_ => Task.Run(() => manager.UpdateNextFireTime("job-hot", nextFireTime)));

        await Task.WhenAll(recorders.Concat(reschedulers)).WaitAsync(WaitBudget, TestContext.Current.CancellationToken);

        var state = manager.GetTriggerState("job-hot");
        Assert.NotNull(state);
        Assert.Equal((long)RoundCount, state!.TriggerCount);
        Assert.Equal(BaseTime, state.LastFireTime);
        Assert.Equal(nextFireTime, state.NextFireTime);
        Assert.Single(manager.GetAllTriggerStates());
    }

    /// <summary>
    /// 并发写入不同任务时各自独立计数，互不串扰
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RecordTrigger_ForDifferentJobsConcurrently_KeepsCountsIsolated()
    {
        var manager = new JobTriggerManager();
        const int WriterCountPerJob = 200;

        var writers = Enumerable.Range(0, WriterCountPerJob)
            .SelectMany(_ => new[]
            {
                Task.Run(() => manager.RecordTrigger("job-a", BaseTime)),
                Task.Run(() => manager.RecordTrigger("job-b", BaseTime))
            });

        await Task.WhenAll(writers).WaitAsync(WaitBudget, TestContext.Current.CancellationToken);

        Assert.Equal((long)WriterCountPerJob, manager.GetTriggerState("job-a")!.TriggerCount);
        Assert.Equal((long)WriterCountPerJob, manager.GetTriggerState("job-b")!.TriggerCount);
        Assert.Equal(2, manager.GetAllTriggerStates().Count);
    }

    /// <summary>
    /// 并发暂停与恢复不会把状态写坏，也不会凭空多出状态条目
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task PauseAndResume_Concurrently_LeaveStateIntact()
    {
        var manager = new JobTriggerManager();
        manager.UpdateNextFireTime("job-hot", BaseTime);

        var togglers = Enumerable.Range(0, 400)
            .Select(index => Task.Run(() =>
            {
                if (index % 2 == 0)
                {
                    manager.PauseJob("job-hot");
                }
                else
                {
                    manager.ResumeJob("job-hot");
                }
            }));

        await Task.WhenAll(togglers).WaitAsync(WaitBudget, TestContext.Current.CancellationToken);

        var state = manager.GetTriggerState("job-hot");
        Assert.NotNull(state);
        Assert.Equal(BaseTime, state!.NextFireTime);
        Assert.Equal(0L, state.TriggerCount);
        Assert.Single(manager.GetAllTriggerStates());
    }
}
