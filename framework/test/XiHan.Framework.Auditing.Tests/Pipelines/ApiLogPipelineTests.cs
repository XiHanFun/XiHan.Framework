// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Auditing.Options;
using XiHan.Framework.Auditing.Pipelines;
using XiHan.Framework.Auditing.Tests.Fakes;

namespace XiHan.Framework.Auditing.Tests.Pipelines;

/// <summary>
/// 接口日志管道测试
/// </summary>
/// <remarks>
/// 接口日志由 <c>EnableApiLogQueue</c> 单独控制，通常是开放平台流量最大的一路，
/// 因此「满时丢弃 / 满时反压」两条分支的取舍必须可验证。
/// </remarks>
public class ApiLogPipelineTests
{
    /// <summary>
    /// 未启用队列时记录直接交给写入器，完全不碰队列
    /// </summary>
    [Fact]
    public async Task WriteAsync_WhenQueueDisabled_WritesThroughWriter()
    {
        var writer = new RecordingApiLogWriter();
        var queue = new RecordingLogQueue<ApiLogRecord>();
        var pipeline = CreatePipeline(writer, queue, new XiHanAuditingLogQueueOptions());
        var record = new ApiLogRecord { TraceId = "trace-1" };

        await pipeline.WriteAsync(record, TestContext.Current.CancellationToken);

        Assert.Same(record, Assert.Single(writer.Records));
        Assert.Equal(0, queue.TryEnqueueCallCount);
        Assert.Equal(0, queue.EnqueueAsyncCallCount);
    }

    /// <summary>
    /// 未启用队列时写入器拿到的是不可取消的令牌
    /// </summary>
    [Fact]
    public async Task WriteAsync_WhenQueueDisabled_DoesNotForwardCallerTokenToWriter()
    {
        var writer = new RecordingApiLogWriter();
        var queue = new RecordingLogQueue<ApiLogRecord>();
        var pipeline = CreatePipeline(writer, queue, new XiHanAuditingLogQueueOptions());
        using var cts = new CancellationTokenSource();

        await pipeline.WriteAsync(new ApiLogRecord(), cts.Token);

        Assert.False(Assert.Single(writer.Tokens).CanBeCanceled);
    }

    /// <summary>
    /// 启用队列且满时丢弃时走非阻塞入队，不再触碰写入器
    /// </summary>
    [Fact]
    public async Task WriteAsync_WhenQueueEnabledAndDropOnFull_UsesNonBlockingEnqueue()
    {
        var writer = new RecordingApiLogWriter();
        var queue = new RecordingLogQueue<ApiLogRecord>();
        var pipeline = CreatePipeline(writer, queue, new XiHanAuditingLogQueueOptions
        {
            EnableApiLogQueue = true,
            DropOnFull = true
        });
        var record = new ApiLogRecord { TraceId = "trace-2" };

        await pipeline.WriteAsync(record, TestContext.Current.CancellationToken);

        Assert.Same(record, Assert.Single(queue.Enqueued));
        Assert.Equal(1, queue.TryEnqueueCallCount);
        Assert.Equal(0, queue.EnqueueAsyncCallCount);
        Assert.Empty(writer.Records);
    }

    /// <summary>
    /// 满时丢弃策略下队列已满则静默丢弃，绝不退化成等待
    /// </summary>
    [Fact]
    public async Task WriteAsync_WhenQueueFullAndDropOnFull_DropsSilently()
    {
        var writer = new RecordingApiLogWriter();
        var queue = new RecordingLogQueue<ApiLogRecord> { TryEnqueueResult = false };
        var pipeline = CreatePipeline(writer, queue, new XiHanAuditingLogQueueOptions
        {
            EnableApiLogQueue = true,
            DropOnFull = true
        });

        await pipeline.WriteAsync(new ApiLogRecord { TraceId = "trace-3" }, TestContext.Current.CancellationToken);

        Assert.Empty(queue.Enqueued);
        Assert.Equal(1, queue.TryEnqueueCallCount);
        Assert.Equal(0, queue.EnqueueAsyncCallCount);
        Assert.Empty(writer.Records);
    }

    /// <summary>
    /// 启用队列且不丢弃时走等待式入队，并把调用方令牌透传给队列
    /// </summary>
    [Fact]
    public async Task WriteAsync_WhenQueueEnabledAndNoDrop_AwaitsEnqueueWithCallerToken()
    {
        var writer = new RecordingApiLogWriter();
        var queue = new RecordingLogQueue<ApiLogRecord>();
        var pipeline = CreatePipeline(writer, queue, new XiHanAuditingLogQueueOptions
        {
            EnableApiLogQueue = true,
            DropOnFull = false
        });
        var record = new ApiLogRecord { TraceId = "trace-4" };
        using var cts = new CancellationTokenSource();

        await pipeline.WriteAsync(record, cts.Token);

        Assert.Same(record, Assert.Single(queue.Enqueued));
        Assert.Equal(1, queue.EnqueueAsyncCallCount);
        Assert.Equal(0, queue.TryEnqueueCallCount);
        Assert.Equal(cts.Token, queue.LastEnqueueAsyncToken);
        Assert.Empty(writer.Records);
    }

    /// <summary>
    /// 调用方已取消导致入队取消时静默吞掉
    /// </summary>
    [Fact]
    public async Task WriteAsync_WhenCallerCanceledDuringEnqueue_SwallowsCancellation()
    {
        var writer = new RecordingApiLogWriter();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var queue = new RecordingLogQueue<ApiLogRecord>
        {
            EnqueueAsyncException = new OperationCanceledException(cts.Token)
        };
        var pipeline = CreatePipeline(writer, queue, new XiHanAuditingLogQueueOptions
        {
            EnableApiLogQueue = true,
            DropOnFull = false
        });

        await pipeline.WriteAsync(new ApiLogRecord(), cts.Token);

        Assert.Empty(queue.Enqueued);
        Assert.Empty(writer.Records);
    }

    /// <summary>
    /// 入队抛出非取消异常时向上传播
    /// </summary>
    [Fact]
    public async Task WriteAsync_WhenEnqueueFailsWithNonCancellation_Propagates()
    {
        var writer = new RecordingApiLogWriter();
        var queue = new RecordingLogQueue<ApiLogRecord>
        {
            EnqueueAsyncException = new InvalidOperationException("队列已损坏")
        };
        var pipeline = CreatePipeline(writer, queue, new XiHanAuditingLogQueueOptions
        {
            EnableApiLogQueue = true,
            DropOnFull = false
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => pipeline.WriteAsync(new ApiLogRecord(), TestContext.Current.CancellationToken));

        Assert.Equal("队列已损坏", exception.Message);
    }

    private static ApiLogPipeline CreatePipeline(
        RecordingApiLogWriter writer,
        RecordingLogQueue<ApiLogRecord> queue,
        XiHanAuditingLogQueueOptions options)
    {
        return new ApiLogPipeline(
            writer,
            queue,
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<ApiLogPipeline>.Instance);
    }
}
