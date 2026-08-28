// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Auditing.Options;
using XiHan.Framework.Auditing.Pipelines;
using XiHan.Framework.Auditing.Tests.Fakes;

namespace XiHan.Framework.Auditing.Tests.Pipelines;

/// <summary>
/// 访问日志管道测试
/// </summary>
/// <remarks>
/// 管道自身不做业务，价值全在编排选择上，共三条互斥分支：
/// 未启用队列＝同步交给写入器；启用队列且满时丢弃＝只 <c>TryEnqueue</c> 一次；启用队列且满时等待＝<c>EnqueueAsync</c> 反压。
/// 另有两条容易写错的取消语义：写入器永远拿 <c>CancellationToken.None</c>（请求取消也要把审计写完），
/// 入队被调用方取消时静默吞掉（不污染主流程），但非取消异常必须向上抛。
/// </remarks>
public class AccessLogPipelineTests
{
    /// <summary>
    /// 未启用队列时记录直接交给写入器，完全不碰队列
    /// </summary>
    [Fact]
    public async Task WriteAsync_WhenQueueDisabled_WritesThroughWriter()
    {
        var writer = new RecordingAccessLogWriter();
        var queue = new RecordingLogQueue<AccessLogRecord>();
        var pipeline = CreatePipeline(writer, queue, new XiHanAuditingLogQueueOptions());
        var record = new AccessLogRecord { TraceId = "trace-1" };

        await pipeline.WriteAsync(record, TestContext.Current.CancellationToken);

        Assert.Same(record, Assert.Single(writer.Records));
        Assert.Equal(0, queue.TryEnqueueCallCount);
        Assert.Equal(0, queue.EnqueueAsyncCallCount);
    }

    /// <summary>
    /// 未启用队列时写入器拿到的是不可取消的令牌，请求取消不影响审计落地
    /// </summary>
    [Fact]
    public async Task WriteAsync_WhenQueueDisabled_DoesNotForwardCallerTokenToWriter()
    {
        var writer = new RecordingAccessLogWriter();
        var queue = new RecordingLogQueue<AccessLogRecord>();
        var pipeline = CreatePipeline(writer, queue, new XiHanAuditingLogQueueOptions());
        using var cts = new CancellationTokenSource();

        await pipeline.WriteAsync(new AccessLogRecord(), cts.Token);

        Assert.False(Assert.Single(writer.Tokens).CanBeCanceled);
    }

    /// <summary>
    /// 启用队列且满时丢弃时走非阻塞入队，不再触碰写入器
    /// </summary>
    [Fact]
    public async Task WriteAsync_WhenQueueEnabledAndDropOnFull_UsesNonBlockingEnqueue()
    {
        var writer = new RecordingAccessLogWriter();
        var queue = new RecordingLogQueue<AccessLogRecord>();
        var pipeline = CreatePipeline(writer, queue, new XiHanAuditingLogQueueOptions
        {
            EnableAccessLogQueue = true,
            DropOnFull = true
        });
        var record = new AccessLogRecord { TraceId = "trace-2" };

        await pipeline.WriteAsync(record, TestContext.Current.CancellationToken);

        Assert.Same(record, Assert.Single(queue.Enqueued));
        Assert.Equal(1, queue.TryEnqueueCallCount);
        Assert.Equal(0, queue.EnqueueAsyncCallCount);
        Assert.Empty(writer.Records);
    }

    /// <summary>
    /// 满时丢弃策略下队列已满则静默丢弃，绝不退化成等待，也不回落到同步写入
    /// </summary>
    [Fact]
    public async Task WriteAsync_WhenQueueFullAndDropOnFull_DropsSilently()
    {
        var writer = new RecordingAccessLogWriter();
        var queue = new RecordingLogQueue<AccessLogRecord> { TryEnqueueResult = false };
        var pipeline = CreatePipeline(writer, queue, new XiHanAuditingLogQueueOptions
        {
            EnableAccessLogQueue = true,
            DropOnFull = true
        });

        await pipeline.WriteAsync(new AccessLogRecord { TraceId = "trace-3" }, TestContext.Current.CancellationToken);

        Assert.Empty(queue.Enqueued);
        Assert.Equal(1, queue.TryEnqueueCallCount);
        Assert.Equal(0, queue.EnqueueAsyncCallCount);
        Assert.Empty(writer.Records);
    }

    /// <summary>
    /// 启用队列且不丢弃时走等待式入队，并把调用方令牌透传给队列以便反压可被取消
    /// </summary>
    [Fact]
    public async Task WriteAsync_WhenQueueEnabledAndNoDrop_AwaitsEnqueueWithCallerToken()
    {
        var writer = new RecordingAccessLogWriter();
        var queue = new RecordingLogQueue<AccessLogRecord>();
        var pipeline = CreatePipeline(writer, queue, new XiHanAuditingLogQueueOptions
        {
            EnableAccessLogQueue = true,
            DropOnFull = false
        });
        var record = new AccessLogRecord { TraceId = "trace-4" };
        using var cts = new CancellationTokenSource();

        await pipeline.WriteAsync(record, cts.Token);

        Assert.Same(record, Assert.Single(queue.Enqueued));
        Assert.Equal(1, queue.EnqueueAsyncCallCount);
        Assert.Equal(0, queue.TryEnqueueCallCount);
        Assert.Equal(cts.Token, queue.LastEnqueueAsyncToken);
        Assert.Empty(writer.Records);
    }

    /// <summary>
    /// 调用方已取消导致入队取消时静默吞掉，不把日志的取消异常抛回主流程
    /// </summary>
    [Fact]
    public async Task WriteAsync_WhenCallerCanceledDuringEnqueue_SwallowsCancellation()
    {
        var writer = new RecordingAccessLogWriter();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var queue = new RecordingLogQueue<AccessLogRecord>
        {
            EnqueueAsyncException = new OperationCanceledException(cts.Token)
        };
        var pipeline = CreatePipeline(writer, queue, new XiHanAuditingLogQueueOptions
        {
            EnableAccessLogQueue = true,
            DropOnFull = false
        });

        await pipeline.WriteAsync(new AccessLogRecord(), cts.Token);

        Assert.Empty(queue.Enqueued);
        Assert.Empty(writer.Records);
    }

    /// <summary>
    /// 入队抛出非取消异常时向上传播，不允许被取消捕获块吃掉
    /// </summary>
    [Fact]
    public async Task WriteAsync_WhenEnqueueFailsWithNonCancellation_Propagates()
    {
        var writer = new RecordingAccessLogWriter();
        var queue = new RecordingLogQueue<AccessLogRecord>
        {
            EnqueueAsyncException = new InvalidOperationException("队列已损坏")
        };
        var pipeline = CreatePipeline(writer, queue, new XiHanAuditingLogQueueOptions
        {
            EnableAccessLogQueue = true,
            DropOnFull = false
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => pipeline.WriteAsync(new AccessLogRecord(), TestContext.Current.CancellationToken));

        Assert.Equal("队列已损坏", exception.Message);
    }

    private static AccessLogPipeline CreatePipeline(
        RecordingAccessLogWriter writer,
        RecordingLogQueue<AccessLogRecord> queue,
        XiHanAuditingLogQueueOptions options)
    {
        return new AccessLogPipeline(
            writer,
            queue,
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<AccessLogPipeline>.Instance);
    }
}
