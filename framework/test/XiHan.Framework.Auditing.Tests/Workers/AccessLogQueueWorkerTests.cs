// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Auditing.Options;
using XiHan.Framework.Auditing.Tests.Fakes;
using XiHan.Framework.Auditing.Workers;
using XiHan.Framework.Auditing.Writers;

namespace XiHan.Framework.Auditing.Tests.Workers;

/// <summary>
/// 访问日志队列消费者测试
/// </summary>
/// <remarks>
/// 后台消费者有四条必须成立的性质：
/// 队列未启用时直接退出（不能空转消费）、每条记录恰好写一次（批量冲刷后要清空批次，否则会重复落库）、
/// 写入器未注册时静默跳过（应用侧没实现落库不能拖垮 host）、停止时冲刷未满批的剩余记录（优雅停止不丢日志）。
/// 这里用剧本化队列替身驱动，<c>Drained</c> 让「记录已进批次但尚未落盘」这一时刻可确定性观测，不依赖 sleep。
/// </remarks>
public class AccessLogQueueWorkerTests
{
    /// <summary>
    /// 队列未启用时消费者直接结束，不去枚举队列
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenQueueDisabled_DoesNotConsumeQueue()
    {
        var writer = new RecordingAccessLogWriter();
        var queue = new ScriptedLogQueue<AccessLogRecord>(
            new[] { new AccessLogRecord { TraceId = "t1" } },
            blockAfterDrain: false);

        using var provider = BuildProvider(writer);
        using var worker = CreateWorker(queue, provider, new XiHanAuditingLogQueueOptions
        {
            EnableAccessLogQueue = false
        });

        await worker.StartAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(worker.ExecuteTask);
        await worker.ExecuteTask!;

        Assert.Equal(0, queue.DequeueCallCount);
        Assert.Empty(writer.Records);
    }

    /// <summary>
    /// 队列启用时全部记录按顺序交给作用域内解析出的写入器
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenQueueEnabled_WritesEveryRecordInOrder()
    {
        var writer = new RecordingAccessLogWriter();
        var queue = new ScriptedLogQueue<AccessLogRecord>(
            new[]
            {
                new AccessLogRecord { TraceId = "t1" },
                new AccessLogRecord { TraceId = "t2" },
                new AccessLogRecord { TraceId = "t3" }
            },
            blockAfterDrain: false);

        using var provider = BuildProvider(writer);
        using var worker = CreateWorker(queue, provider, new XiHanAuditingLogQueueOptions
        {
            EnableAccessLogQueue = true,
            BatchSize = 10,
            BatchDelayMilliseconds = 60_000
        });

        await worker.StartAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(worker.ExecuteTask);
        await worker.ExecuteTask!;

        Assert.Equal(1, queue.DequeueCallCount);
        Assert.Equal(3, writer.Records.Count);
        Assert.Equal("t1", writer.Records[0].TraceId);
        Assert.Equal("t2", writer.Records[1].TraceId);
        Assert.Equal("t3", writer.Records[2].TraceId);
    }

    /// <summary>
    /// 批量大小为 1 时逐条冲刷，且每条只写一次（冲刷后批次必须被清空）
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenBatchSizeIsOne_WritesEachRecordExactlyOnce()
    {
        var writer = new RecordingAccessLogWriter();
        var queue = new ScriptedLogQueue<AccessLogRecord>(
            new[]
            {
                new AccessLogRecord { TraceId = "t1" },
                new AccessLogRecord { TraceId = "t2" },
                new AccessLogRecord { TraceId = "t3" }
            },
            blockAfterDrain: false);

        using var provider = BuildProvider(writer);
        using var worker = CreateWorker(queue, provider, new XiHanAuditingLogQueueOptions
        {
            EnableAccessLogQueue = true,
            BatchSize = 1,
            BatchDelayMilliseconds = 60_000
        });

        await worker.StartAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(worker.ExecuteTask);
        await worker.ExecuteTask!;

        Assert.Equal(3, writer.Records.Count);
        Assert.Equal(3, writer.Records.Select(record => record.TraceId).Distinct().Count());
    }

    /// <summary>
    /// 应用侧未注册写入器时静默跳过，不抛异常拖垮宿主
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenWriterNotRegistered_CompletesWithoutThrowing()
    {
        var queue = new ScriptedLogQueue<AccessLogRecord>(
            new[]
            {
                new AccessLogRecord { TraceId = "t1" },
                new AccessLogRecord { TraceId = "t2" }
            },
            blockAfterDrain: false);

        using var provider = new ServiceCollection().BuildServiceProvider();
        using var worker = CreateWorker(queue, provider, new XiHanAuditingLogQueueOptions
        {
            EnableAccessLogQueue = true,
            BatchSize = 10,
            BatchDelayMilliseconds = 60_000
        });

        await worker.StartAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(worker.ExecuteTask);
        await worker.ExecuteTask!;

        Assert.Equal(1, queue.DequeueCallCount);
    }

    /// <summary>
    /// 停止时把尚未达到批量阈值的剩余记录冲刷落库
    /// </summary>
    [Fact]
    public async Task StopAsync_WhenPendingBatchNotFull_FlushesRemainingRecords()
    {
        var writer = new RecordingAccessLogWriter();
        var queue = new ScriptedLogQueue<AccessLogRecord>(
            new[]
            {
                new AccessLogRecord { TraceId = "t1" },
                new AccessLogRecord { TraceId = "t2" }
            },
            blockAfterDrain: true);

        using var provider = BuildProvider(writer);
        using var worker = CreateWorker(queue, provider, new XiHanAuditingLogQueueOptions
        {
            EnableAccessLogQueue = true,
            BatchSize = 100,
            BatchDelayMilliseconds = 60_000
        });

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await queue.Drained;

        // 两条都还压在批次里：批量阈值与时间阈值都未触发
        Assert.Empty(writer.Records);

        await worker.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, writer.Records.Count);
        Assert.Equal("t1", writer.Records[0].TraceId);
        Assert.Equal("t2", writer.Records[1].TraceId);
    }

    private static ServiceProvider BuildProvider(IAccessLogWriter writer)
    {
        var services = new ServiceCollection();
        services.AddScoped<IAccessLogWriter>(_ => writer);
        return services.BuildServiceProvider();
    }

    private static AccessLogQueueWorker CreateWorker(
        ScriptedLogQueue<AccessLogRecord> queue,
        IServiceProvider provider,
        XiHanAuditingLogQueueOptions options)
    {
        return new AccessLogQueueWorker(
            queue,
            provider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<AccessLogQueueWorker>.Instance);
    }
}
