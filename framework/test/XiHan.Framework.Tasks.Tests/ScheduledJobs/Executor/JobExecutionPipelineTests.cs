// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Executor;
using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Executor;

/// <summary>
/// JobExecutionPipeline 执行管道测试
/// </summary>
/// <remarks>
/// 管道是"洋葱模型"：先注册的中间件在最外层。这里锁死调用顺序、短路能力，
/// 以及最内层对任务体异常的兜底转换（取消 → Canceled，其余异常 → Failure）。
/// </remarks>
public class JobExecutionPipelineTests
{
    /// <summary>
    /// 服务提供者为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void Constructor_WhenServiceProviderIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new JobExecutionPipeline(null!));
    }

    /// <summary>
    /// 添加中间件返回同一个管道实例，支持链式注册
    /// </summary>
    [Fact]
    public void Use_ReturnsSamePipelineInstance()
    {
        var pipeline = CreatePipeline();
        var middleware = new RecordingMiddleware("A", []);

        Assert.Same(pipeline, pipeline.Use(middleware));
    }

    /// <summary>
    /// 没有中间件时直接执行任务体并原样返回结果
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithoutMiddleware_ReturnsWorkerResult()
    {
        var pipeline = CreatePipeline();
        var expected = JobResult.Success("payload");

        var result = await pipeline.ExecuteAsync(CreateContext(), new DelegatingJobWorker(_ => Task.FromResult(expected)));

        Assert.Same(expected, result);
    }

    /// <summary>
    /// 先注册的中间件在最外层，请求按注册顺序进入、按逆序返回
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithMultipleMiddlewares_RunsInOnionOrder()
    {
        var trace = new List<string>();
        var pipeline = CreatePipeline()
            .Use(new RecordingMiddleware("A", trace))
            .Use(new RecordingMiddleware("B", trace))
            .Use(new RecordingMiddleware("C", trace));

        await pipeline.ExecuteAsync(CreateContext(), new DelegatingJobWorker(_ =>
        {
            trace.Add("worker");
            return Task.FromResult(JobResult.Success());
        }));

        Assert.Equal(
            new[] { "A-in", "B-in", "C-in", "worker", "C-out", "B-out", "A-out" },
            trace);
    }

    /// <summary>
    /// 中间件可以短路：不调用 next 时任务体完全不执行
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenMiddlewareShortCircuits_WorkerIsNeverInvoked()
    {
        var invoked = false;
        var shortCircuitResult = JobResult.Failure("被中间件拦截");
        var pipeline = CreatePipeline().Use(new ShortCircuitMiddleware(shortCircuitResult));

        var result = await pipeline.ExecuteAsync(CreateContext(), new DelegatingJobWorker(_ =>
        {
            invoked = true;
            return Task.FromResult(JobResult.Success());
        }));

        Assert.False(invoked);
        Assert.Same(shortCircuitResult, result);
    }

    /// <summary>
    /// 任务体抛普通异常时转换为失败结果，并保留原异常对象
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenWorkerThrows_ConvertsToFailureResult()
    {
        var pipeline = CreatePipeline();
        var boom = new InvalidOperationException("数据源不可用");

        var result = await pipeline.ExecuteAsync(CreateContext(), new DelegatingJobWorker(_ => throw boom));

        Assert.False(result.IsSuccess);
        Assert.Equal(JobStatus.Failed, result.Status);
        Assert.Same(boom, result.Exception);
        Assert.Contains("数据源不可用", result.ErrorMessage!, StringComparison.Ordinal);
    }

    /// <summary>
    /// 任务体因取消而中止时转换为取消结果，而不是失败结果
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenWorkerIsCanceled_ReturnsCanceledResult()
    {
        var pipeline = CreatePipeline();

        var result = await pipeline.ExecuteAsync(CreateContext(), new DelegatingJobWorker(_ => throw new OperationCanceledException()));

        Assert.False(result.IsSuccess);
        Assert.Equal(JobStatus.Canceled, result.Status);
        Assert.Null(result.Exception);
    }

    /// <summary>
    /// 中间件自身抛出的异常不被兜底吞掉，交给上层执行器处理
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenMiddlewareThrows_PropagatesToCaller()
    {
        var pipeline = CreatePipeline().Use(new ThrowingMiddleware());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => pipeline.ExecuteAsync(CreateContext(), new DelegatingJobWorker(_ => Task.FromResult(JobResult.Success()))));
    }

    /// <summary>
    /// 任务体拿到的是上下文自带的取消令牌
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_PassesContextCancellationTokenToWorker()
    {
        using var cts = new CancellationTokenSource();
        var context = CreateContext(cts.Token);
        var observed = CancellationToken.None;

        await CreatePipeline().ExecuteAsync(context, new DelegatingJobWorker(token =>
        {
            observed = token;
            return Task.FromResult(JobResult.Success());
        }));

        Assert.Equal(cts.Token, observed);
    }

    /// <summary>
    /// Build 出来的委托可以重复执行，管道本身无状态残留
    /// </summary>
    [Fact]
    public async Task Build_ReturnedDelegate_CanBeInvokedRepeatedly()
    {
        var count = 0;
        var pipeline = CreatePipeline();
        var invoke = pipeline.Build(new DelegatingJobWorker(_ =>
        {
            count++;
            return Task.FromResult(JobResult.Success());
        }));

        await invoke(CreateContext());
        await invoke(CreateContext());

        Assert.Equal(2, count);
    }

    /// <summary>
    /// 同一个管道多次 Build 得到等价的委托，中间件不会被重复串接
    /// </summary>
    [Fact]
    public async Task Build_CalledTwice_ProducesEquivalentPipelines()
    {
        var trace = new List<string>();
        var pipeline = CreatePipeline().Use(new RecordingMiddleware("A", trace));

        await pipeline.Build(new DelegatingJobWorker(_ => Task.FromResult(JobResult.Success())))(CreateContext());
        Assert.Equal(new[] { "A-in", "A-out" }, trace);

        trace.Clear();
        await pipeline.Build(new DelegatingJobWorker(_ => Task.FromResult(JobResult.Success())))(CreateContext());
        Assert.Equal(new[] { "A-in", "A-out" }, trace);
    }

    /// <summary>
    /// 创建一个带真实服务提供者的空管道
    /// </summary>
    private static JobExecutionPipeline CreatePipeline()
    {
        return new JobExecutionPipeline(new ServiceCollection().BuildServiceProvider());
    }

    /// <summary>
    /// 创建一个最小可用的执行上下文
    /// </summary>
    private static JobExecutionContext CreateContext(CancellationToken cancellationToken = default)
    {
        var jobInfo = new JobInfo
        {
            JobName = "pipeline-job",
            JobType = typeof(JobExecutionPipelineTests),
            TriggerType = JobTriggerType.Manual
        };

        var instance = new JobInstance
        {
            JobName = jobInfo.JobName,
            JobInfo = jobInfo,
            TriggerType = JobTriggerType.Manual
        };

        return new JobExecutionContext(instance, null, new ServiceCollection().BuildServiceProvider(), cancellationToken);
    }

    /// <summary>
    /// 用委托驱动的假任务体
    /// </summary>
    private sealed class DelegatingJobWorker : IJobWorker
    {
        private readonly Func<CancellationToken, Task<JobResult>> _body;

        /// <summary>
        /// 构造函数
        /// </summary>
        public DelegatingJobWorker(Func<CancellationToken, Task<JobResult>> body)
        {
            _body = body;
        }

        /// <summary>
        /// 执行任务
        /// </summary>
        public Task<JobResult> ExecuteAsync(IJobContext context, CancellationToken cancellationToken = default)
        {
            return _body(cancellationToken);
        }
    }

    /// <summary>
    /// 记录进出顺序的假中间件
    /// </summary>
    private sealed class RecordingMiddleware : IJobMiddleware
    {
        private readonly string _name;
        private readonly List<string> _trace;

        /// <summary>
        /// 构造函数
        /// </summary>
        public RecordingMiddleware(string name, List<string> trace)
        {
            _name = name;
            _trace = trace;
        }

        /// <summary>
        /// 执行中间件逻辑
        /// </summary>
        public async Task<JobResult> InvokeAsync(IJobContext context, JobExecutionDelegate next)
        {
            _trace.Add($"{_name}-in");
            var result = await next(context);
            _trace.Add($"{_name}-out");
            return result;
        }
    }

    /// <summary>
    /// 不调用 next 的短路中间件
    /// </summary>
    private sealed class ShortCircuitMiddleware : IJobMiddleware
    {
        private readonly JobResult _result;

        /// <summary>
        /// 构造函数
        /// </summary>
        public ShortCircuitMiddleware(JobResult result)
        {
            _result = result;
        }

        /// <summary>
        /// 执行中间件逻辑
        /// </summary>
        public Task<JobResult> InvokeAsync(IJobContext context, JobExecutionDelegate next)
        {
            return Task.FromResult(_result);
        }
    }

    /// <summary>
    /// 直接抛异常的中间件
    /// </summary>
    private sealed class ThrowingMiddleware : IJobMiddleware
    {
        /// <summary>
        /// 执行中间件逻辑
        /// </summary>
        public Task<JobResult> InvokeAsync(IJobContext context, JobExecutionDelegate next)
        {
            throw new InvalidOperationException("中间件炸了");
        }
    }
}
