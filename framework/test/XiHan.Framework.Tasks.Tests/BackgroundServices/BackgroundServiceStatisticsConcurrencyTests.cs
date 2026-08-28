// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundServices;

namespace XiHan.Framework.Tasks.Tests.BackgroundServices;

/// <summary>
/// 后台服务统计信息并发计数测试
/// </summary>
/// <remarks>
/// 统计对象是从基类的并发任务体里被调用的：<c>RecordTaskStarted</c> 在 <c>Task.Run</c> 任务体开头、
/// <c>RecordTaskCompleted</c> 在它的 finally 里，同一时刻可能有 <c>MaxConcurrentTasks</c> 个线程一起进来。
/// 运行中任务数曾经是普通的 <c>++</c> / <c>--</c>（读-改-写三步），并发下会丢失更新——
/// 这类缺陷单线程用例一条也照不出来，必须真的并发压一遍。
/// </remarks>
public class BackgroundServiceStatisticsConcurrencyTests
{
    /// <summary>
    /// 兜底超时
    /// </summary>
    private const int TimeoutMilliseconds = 60_000;

    /// <summary>
    /// 并发记录任务开始时不丢更新，运行中任务数等于实际调用次数
    /// </summary>
    /// <returns>任务</returns>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RecordTaskStarted_UnderConcurrency_DoesNotLoseUpdates()
    {
        const int workers = 8;
        const int perWorker = 20_000;

        var statistics = new BackgroundServiceStatistics();

        await Task.WhenAll(Enumerable.Range(0, workers).Select(_ => Task.Run(
            () =>
            {
                for (var i = 0; i < perWorker; i++)
                {
                    statistics.RecordTaskStarted();
                }
            },
            TestContext.Current.CancellationToken)));

        Assert.Equal(workers * perWorker, statistics.CurrentRunningTasks);
    }

    /// <summary>
    /// 并发的开始与完成成对出现时，运行中任务数最终精确回到零
    /// </summary>
    /// <returns>任务</returns>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RecordTaskStartedAndCompleted_UnderConcurrency_ReturnsToZero()
    {
        const int workers = 8;
        const int perWorker = 2_000;

        var statistics = new BackgroundServiceStatistics();

        await Task.WhenAll(Enumerable.Range(0, workers).Select(worker => Task.Run(
            () =>
            {
                for (var i = 0; i < perWorker; i++)
                {
                    statistics.RecordTaskStarted();
                    statistics.RecordTaskCompleted($"w{worker}-{i}", 1, true);
                }
            },
            TestContext.Current.CancellationToken)));

        Assert.Equal(0, statistics.CurrentRunningTasks);
        Assert.Equal(workers * perWorker, statistics.TotalTasksProcessed);
    }

    /// <summary>
    /// 并发计数不影响成功率口径：成功与失败各占一半时成功率是 50%
    /// </summary>
    /// <returns>任务</returns>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RecordTaskCompleted_UnderConcurrency_KeepsSuccessRateExact()
    {
        const int workers = 8;
        const int perWorker = 1_000;

        var statistics = new BackgroundServiceStatistics();

        await Task.WhenAll(Enumerable.Range(0, workers).Select(worker => Task.Run(
            () =>
            {
                for (var i = 0; i < perWorker; i++)
                {
                    statistics.RecordTaskStarted();
                    statistics.RecordTaskCompleted($"w{worker}-{i}", 1, i % 2 == 0);
                }
            },
            TestContext.Current.CancellationToken)));

        Assert.Equal(0, statistics.CurrentRunningTasks);
        Assert.Equal(workers * perWorker / 2, statistics.TotalTasksProcessed);
        Assert.Equal(workers * perWorker / 2, statistics.TotalTasksFailed);
        Assert.Equal(50d, statistics.SuccessRate);
    }
}
