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
/// 登录日志队列消费者测试
/// </summary>
public class LoginLogQueueWorkerTests
{
    /// <summary>
    /// 队列未启用时消费者直接结束，不去枚举队列
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenQueueDisabled_DoesNotConsumeQueue()
    {
        var writer = new RecordingLoginLogWriter();
        var queue = new ScriptedLogQueue<LoginLogRecord>(
            new[] { new LoginLogRecord { TraceId = "t1" } },
            blockAfterDrain: false);

        using var provider = BuildProvider(writer);
        using var worker = CreateWorker(queue, provider, new XiHanAuditingLogQueueOptions
        {
            EnableLoginLogQueue = false
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
        var writer = new RecordingLoginLogWriter();
        var queue = new ScriptedLogQueue<LoginLogRecord>(
            new[]
            {
                new LoginLogRecord { TraceId = "t1" },
                new LoginLogRecord { TraceId = "t2" },
                new LoginLogRecord { TraceId = "t3" }
            },
            blockAfterDrain: false);

        using var provider = BuildProvider(writer);
        using var worker = CreateWorker(queue, provider, new XiHanAuditingLogQueueOptions
        {
            EnableLoginLogQueue = true,
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
    /// 批量大小为 1 时逐条冲刷，且每条只写一次
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenBatchSizeIsOne_WritesEachRecordExactlyOnce()
    {
        var writer = new RecordingLoginLogWriter();
        var queue = new ScriptedLogQueue<LoginLogRecord>(
            new[]
            {
                new LoginLogRecord { TraceId = "t1" },
                new LoginLogRecord { TraceId = "t2" },
                new LoginLogRecord { TraceId = "t3" }
            },
            blockAfterDrain: false);

        using var provider = BuildProvider(writer);
        using var worker = CreateWorker(queue, provider, new XiHanAuditingLogQueueOptions
        {
            EnableLoginLogQueue = true,
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
        var queue = new ScriptedLogQueue<LoginLogRecord>(
            new[]
            {
                new LoginLogRecord { TraceId = "t1" },
                new LoginLogRecord { TraceId = "t2" }
            },
            blockAfterDrain: false);

        using var provider = new ServiceCollection().BuildServiceProvider();
        using var worker = CreateWorker(queue, provider, new XiHanAuditingLogQueueOptions
        {
            EnableLoginLogQueue = true,
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
        var writer = new RecordingLoginLogWriter();
        var queue = new ScriptedLogQueue<LoginLogRecord>(
            new[]
            {
                new LoginLogRecord { TraceId = "t1" },
                new LoginLogRecord { TraceId = "t2" }
            },
            blockAfterDrain: true);

        using var provider = BuildProvider(writer);
        using var worker = CreateWorker(queue, provider, new XiHanAuditingLogQueueOptions
        {
            EnableLoginLogQueue = true,
            BatchSize = 100,
            BatchDelayMilliseconds = 60_000
        });

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await queue.Drained;

        Assert.Empty(writer.Records);

        await worker.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, writer.Records.Count);
        Assert.Equal("t1", writer.Records[0].TraceId);
        Assert.Equal("t2", writer.Records[1].TraceId);
    }

    private static ServiceProvider BuildProvider(ILoginLogWriter writer)
    {
        var services = new ServiceCollection();
        services.AddScoped<ILoginLogWriter>(_ => writer);
        return services.BuildServiceProvider();
    }

    private static LoginLogQueueWorker CreateWorker(
        ScriptedLogQueue<LoginLogRecord> queue,
        IServiceProvider provider,
        XiHanAuditingLogQueueOptions options)
    {
        return new LoginLogQueueWorker(
            queue,
            provider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<LoginLogQueueWorker>.Instance);
    }
}
