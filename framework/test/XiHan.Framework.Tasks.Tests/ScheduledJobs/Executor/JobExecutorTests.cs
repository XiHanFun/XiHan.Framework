// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Executor;
using XiHan.Framework.Tasks.ScheduledJobs.Models;
using XiHan.Framework.Tasks.ScheduledJobs.Store;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Executor;

/// <summary>
/// JobExecutor 任务执行器测试
/// </summary>
/// <remarks>
/// 执行器负责：落库实例 → 开作用域 → 反射造任务体 → 串中间件 → 回写状态与历史。
/// 用真实的 InMemoryJobStore 作为协作者，方便直接读回历史断言字段映射；
/// 任务体全部是同步返回的假实现，不引入任何等待。
/// </remarks>
public class JobExecutorTests
{
    /// <summary>
    /// 构造函数对每个依赖都做非空校验
    /// </summary>
    [Fact]
    public void Constructor_WhenAnyDependencyIsNull_ThrowsArgumentNullException()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var logger = NullLogger<JobExecutor>.Instance;
        var store = new InMemoryJobStore();
        var middlewares = new List<IJobMiddleware>();

        Assert.Throws<ArgumentNullException>(() => new JobExecutor(null!, logger, store, middlewares));
        Assert.Throws<ArgumentNullException>(() => new JobExecutor(serviceProvider, null!, store, middlewares));
        Assert.Throws<ArgumentNullException>(() => new JobExecutor(serviceProvider, logger, null!, middlewares));
        Assert.Throws<ArgumentNullException>(() => new JobExecutor(serviceProvider, logger, store, null!));
    }

    /// <summary>
    /// 任务体成功时回写成功状态、耗时与完成时间
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenWorkerSucceeds_MarksInstanceSucceeded()
    {
        var store = new InMemoryJobStore();
        var executor = CreateExecutor(store);
        var instance = CreateInstance(typeof(SucceedingWorker));

        var result = await executor.ExecuteAsync(instance, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(JobStatus.Succeeded, result.Status);
        Assert.Equal(JobStatus.Succeeded, instance.Status);
        Assert.NotNull(instance.StartedAt);
        Assert.NotNull(instance.CompletedAt);
        Assert.NotNull(instance.DurationMilliseconds);
        Assert.True(instance.DurationMilliseconds >= 0);
        Assert.Null(instance.ErrorMessage);
    }

    /// <summary>
    /// 执行过程中实例先落库，结束后状态被回写
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_PersistsInstanceAndUpdatesItsStatus()
    {
        var store = new InMemoryJobStore();
        var executor = CreateExecutor(store);
        var instance = CreateInstance(typeof(SucceedingWorker));

        await executor.ExecuteAsync(instance, null, TestContext.Current.CancellationToken);

        var persisted = await store.GetJobInstanceAsync(instance.InstanceId);
        Assert.NotNull(persisted);
        Assert.Equal(JobStatus.Succeeded, persisted!.Status);
    }

    /// <summary>
    /// 成功执行也会留下一条执行历史，关键字段来自实例与结果
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenWorkerSucceeds_WritesHistoryWithMappedFields()
    {
        var store = new InMemoryJobStore();
        var executor = CreateExecutor(store);
        var instance = CreateInstance(typeof(SucceedingWorker));
        instance.TenantId = 66L;
        instance.TraceId = "trace-x";
        instance.ExecutionNode = "node-1";

        await executor.ExecuteAsync(instance, null, TestContext.Current.CancellationToken);

        var histories = await store.GetJobHistoryAsync(instance.JobName, 1, 10);
        var history = Assert.Single(histories);
        Assert.Equal(instance.InstanceId, history.InstanceId);
        Assert.Equal(instance.JobName, history.JobName);
        Assert.Equal(JobStatus.Succeeded, history.Status);
        Assert.True(history.IsSuccess);
        Assert.Equal(JobTriggerType.Manual, history.TriggerType);
        Assert.Equal(66L, history.TenantId);
        Assert.Equal("trace-x", history.TraceId);
        Assert.Equal("node-1", history.ExecutionNode);
        Assert.Equal(instance.CompletedAt, history.CompletedAt);
        Assert.Null(history.ParametersJson);
    }

    /// <summary>
    /// 实例带参数时历史里落下参数的 JSON 快照
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenInstanceHasParameters_SerializesThemIntoHistory()
    {
        var store = new InMemoryJobStore();
        var executor = CreateExecutor(store);
        var instance = CreateInstance(typeof(SucceedingWorker));
        instance.Parameters = new Dictionary<string, object?> { ["batchSize"] = 100 };

        await executor.ExecuteAsync(instance, instance.Parameters, TestContext.Current.CancellationToken);

        var histories = await store.GetJobHistoryAsync(instance.JobName, 1, 10);
        var history = Assert.Single(histories);
        Assert.False(string.IsNullOrWhiteSpace(history.ParametersJson));
    }

    /// <summary>
    /// 任务体返回失败结果时回写失败状态与错误信息
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenWorkerReturnsFailure_MarksInstanceFailed()
    {
        var store = new InMemoryJobStore();
        var executor = CreateExecutor(store);
        var instance = CreateInstance(typeof(FailingWorker));

        var result = await executor.ExecuteAsync(instance, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(JobStatus.Failed, result.Status);
        Assert.Equal(JobStatus.Failed, instance.Status);
        Assert.Equal("业务校验未通过", instance.ErrorMessage);

        var histories = await store.GetJobHistoryAsync(instance.JobName, 1, 10);
        var history = Assert.Single(histories);
        Assert.False(history.IsSuccess);
        Assert.Equal("业务校验未通过", history.ErrorMessage);
    }

    /// <summary>
    /// 任务体抛异常时由管道兜底转换为失败结果，异常不冒泡到调用方
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenWorkerThrows_ReturnsFailureInsteadOfPropagating()
    {
        var store = new InMemoryJobStore();
        var executor = CreateExecutor(store);
        var instance = CreateInstance(typeof(ThrowingWorker));

        var result = await executor.ExecuteAsync(instance, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(JobStatus.Failed, result.Status);
        Assert.Contains("任务体炸了", result.ErrorMessage!, StringComparison.Ordinal);
        Assert.NotNull(instance.StackTrace);
    }

    /// <summary>
    /// 任务类型不是任务体时，失败信息要能指认问题类型，而不是抛出裸异常
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenJobTypeIsNotWorker_ReturnsFailureWithDiagnosticMessage()
    {
        var store = new InMemoryJobStore();
        var executor = CreateExecutor(store);
        var instance = CreateInstance(typeof(NotAWorker));

        var result = await executor.ExecuteAsync(instance, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(JobStatus.Failed, instance.Status);
        Assert.Contains("无法创建任务实例", result.ErrorMessage!, StringComparison.Ordinal);
        Assert.Contains(nameof(NotAWorker), result.ErrorMessage!, StringComparison.Ordinal);
    }

    /// <summary>
    /// 造不出任务体时同样要留下失败历史，保证可排障
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenJobTypeIsNotWorker_StillWritesFailureHistory()
    {
        var store = new InMemoryJobStore();
        var executor = CreateExecutor(store);
        var instance = CreateInstance(typeof(NotAWorker));

        await executor.ExecuteAsync(instance, null, TestContext.Current.CancellationToken);

        var histories = await store.GetJobHistoryAsync(instance.JobName, 1, 10);
        var history = Assert.Single(histories);
        Assert.False(history.IsSuccess);
        Assert.Equal(JobStatus.Failed, history.Status);
    }

    /// <summary>
    /// 注册的中间件按顺序串进执行管道
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_AppliesRegisteredMiddlewaresInOrder()
    {
        var trace = new List<string>();
        var store = new InMemoryJobStore();
        var executor = new JobExecutor(
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<JobExecutor>.Instance,
            store,
            [new TracingMiddleware("outer", trace), new TracingMiddleware("inner", trace)]);

        await executor.ExecuteAsync(CreateInstance(typeof(SucceedingWorker)), null, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "outer-in", "inner-in", "inner-out", "outer-out" }, trace);
    }

    /// <summary>
    /// 重试次数取自上下文的尝试次数减一，由重试中间件写入
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RecordsRetryCountFromContextAttemptCount()
    {
        var store = new InMemoryJobStore();
        var executor = new JobExecutor(
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<JobExecutor>.Instance,
            store,
            [new AttemptCountStampingMiddleware(3)]);
        var instance = CreateInstance(typeof(SucceedingWorker));

        await executor.ExecuteAsync(instance, null, TestContext.Current.CancellationToken);

        Assert.Equal(2, instance.RetryCount);

        var histories = await store.GetJobHistoryAsync(instance.JobName, 1, 10);
        Assert.Equal(2, Assert.Single(histories).RetryCount);
    }

    /// <summary>
    /// 任务体从作用域内的服务提供者解析依赖，而不是根提供者
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_GivesWorkerAScopedServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedProbe>();
        var rootProvider = services.BuildServiceProvider();
        var store = new InMemoryJobStore();
        var executor = new JobExecutor(rootProvider, NullLogger<JobExecutor>.Instance, store, []);

        ProbeCapturingWorker.Reset();
        var result = await executor.ExecuteAsync(CreateInstance(typeof(ProbeCapturingWorker)), null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.NotNull(ProbeCapturingWorker.ResolvedFromContext);
        Assert.NotSame(rootProvider, ProbeCapturingWorker.CapturedProvider);
    }

    /// <summary>
    /// 调用方传入的参数被送进执行上下文
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_PassesParametersIntoContext()
    {
        var store = new InMemoryJobStore();
        var executor = CreateExecutor(store);
        var parameters = new Dictionary<string, object?> { ["mode"] = "full" };

        ContextCapturingWorker.Reset();
        await executor.ExecuteAsync(CreateInstance(typeof(ContextCapturingWorker)), parameters, TestContext.Current.CancellationToken);

        Assert.NotNull(ContextCapturingWorker.CapturedContext);
        Assert.Same(parameters, ContextCapturingWorker.CapturedContext!.Parameters);
    }

    /// <summary>
    /// 调用方传入的取消令牌一路透传到任务体
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_PassesCancellationTokenIntoContext()
    {
        var store = new InMemoryJobStore();
        var executor = CreateExecutor(store);
        using var cts = new CancellationTokenSource();

        ContextCapturingWorker.Reset();
        await executor.ExecuteAsync(CreateInstance(typeof(ContextCapturingWorker)), null, cts.Token);

        Assert.NotNull(ContextCapturingWorker.CapturedContext);
        Assert.Equal(cts.Token, ContextCapturingWorker.CapturedContext!.CancellationToken);
    }

    /// <summary>
    /// 组装一个不带中间件的执行器
    /// </summary>
    private static JobExecutor CreateExecutor(InMemoryJobStore store)
    {
        return new JobExecutor(
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<JobExecutor>.Instance,
            store,
            []);
    }

    /// <summary>
    /// 构造一个指向指定任务类型的实例
    /// </summary>
    private static JobInstance CreateInstance(Type jobType)
    {
        var jobName = $"job-{Guid.NewGuid():N}";
        var jobInfo = new JobInfo
        {
            JobName = jobName,
            JobType = jobType,
            TriggerType = JobTriggerType.Manual
        };

        return new JobInstance
        {
            JobName = jobName,
            JobInfo = jobInfo,
            TriggerType = JobTriggerType.Manual,
            TraceId = Guid.NewGuid().ToString("N"),
            ExecutionNode = "test-node"
        };
    }

    /// <summary>
    /// 总是成功的任务体
    /// </summary>
    public sealed class SucceedingWorker : IJobWorker
    {
        /// <summary>
        /// 执行任务
        /// </summary>
        public Task<JobResult> ExecuteAsync(IJobContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(JobResult.Success("ok"));
        }
    }

    /// <summary>
    /// 总是返回失败结果的任务体
    /// </summary>
    public sealed class FailingWorker : IJobWorker
    {
        /// <summary>
        /// 执行任务
        /// </summary>
        public Task<JobResult> ExecuteAsync(IJobContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(JobResult.Failure("业务校验未通过"));
        }
    }

    /// <summary>
    /// 总是抛异常的任务体
    /// </summary>
    public sealed class ThrowingWorker : IJobWorker
    {
        /// <summary>
        /// 执行任务
        /// </summary>
        public Task<JobResult> ExecuteAsync(IJobContext context, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("任务体炸了");
        }
    }

    /// <summary>
    /// 记录上下文的任务体
    /// </summary>
    public sealed class ContextCapturingWorker : IJobWorker
    {
        /// <summary>
        /// 最近一次捕获的上下文
        /// </summary>
        public static IJobContext? CapturedContext { get; private set; }

        /// <summary>
        /// 清空捕获结果
        /// </summary>
        public static void Reset()
        {
            CapturedContext = null;
        }

        /// <summary>
        /// 执行任务
        /// </summary>
        public Task<JobResult> ExecuteAsync(IJobContext context, CancellationToken cancellationToken = default)
        {
            CapturedContext = context;
            return Task.FromResult(JobResult.Success());
        }
    }

    /// <summary>
    /// 从上下文的服务提供者解析依赖的任务体
    /// </summary>
    public sealed class ProbeCapturingWorker : IJobWorker
    {
        /// <summary>
        /// 从上下文解析出的探针
        /// </summary>
        public static ScopedProbe? ResolvedFromContext { get; private set; }

        /// <summary>
        /// 上下文暴露的服务提供者
        /// </summary>
        public static IServiceProvider? CapturedProvider { get; private set; }

        /// <summary>
        /// 清空捕获结果
        /// </summary>
        public static void Reset()
        {
            ResolvedFromContext = null;
            CapturedProvider = null;
        }

        /// <summary>
        /// 执行任务
        /// </summary>
        public Task<JobResult> ExecuteAsync(IJobContext context, CancellationToken cancellationToken = default)
        {
            CapturedProvider = context.ServiceProvider;
            ResolvedFromContext = context.ServiceProvider.GetService(typeof(ScopedProbe)) as ScopedProbe;
            return Task.FromResult(JobResult.Success());
        }
    }

    /// <summary>
    /// 作用域内可解析的探针服务
    /// </summary>
    public sealed class ScopedProbe
    {
        /// <summary>
        /// 实例唯一标识
        /// </summary>
        public string Id { get; } = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// 不实现任务体接口的类型，用于验证反射失败路径
    /// </summary>
    public sealed class NotAWorker
    {
    }

    /// <summary>
    /// 记录进出顺序的中间件
    /// </summary>
    private sealed class TracingMiddleware : IJobMiddleware
    {
        private readonly string _name;
        private readonly List<string> _trace;

        /// <summary>
        /// 构造函数
        /// </summary>
        public TracingMiddleware(string name, List<string> trace)
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
    /// 模拟重试中间件写入尝试次数
    /// </summary>
    private sealed class AttemptCountStampingMiddleware : IJobMiddleware
    {
        private readonly int _attemptCount;

        /// <summary>
        /// 构造函数
        /// </summary>
        public AttemptCountStampingMiddleware(int attemptCount)
        {
            _attemptCount = attemptCount;
        }

        /// <summary>
        /// 执行中间件逻辑
        /// </summary>
        public Task<JobResult> InvokeAsync(IJobContext context, JobExecutionDelegate next)
        {
            context.AttemptCount = _attemptCount;
            return next(context);
        }
    }
}
