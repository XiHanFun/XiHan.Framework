// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs;
using XiHan.Framework.Tasks.BackgroundJobs.Models;
using XiHan.Framework.Tasks.Tests.BackgroundJobs.Fakes;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs;

/// <summary>
/// 默认后台作业执行器测试
/// </summary>
/// <remarks>
/// 执行器是"反射调用"这一层的唯一实现，它的对外契约是：
/// 无论失败发生在解析处理器、定位接口方法，还是作业体内部（同步抛 / 异步抛两条不同路径），
/// 都必须统一收敛成 <see cref="BackgroundJobExecutionException"/>，并把原始异常挂在 InnerException 上——
/// 因为 Worker 正是靠这个异常类型来区分"可退避重试"与"致命放弃"。
/// </remarks>
public class BackgroundJobExecuterTests
{
    /// <summary>
    /// 正常路径：解析出处理器并把参数原样传入
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenJobResolvable_InvokesHandlerWithArgs()
    {
        var job = new RecordingNamedArgsJob();
        var services = new ServiceCollection();
        services.AddSingleton(job);
        using var provider = services.BuildServiceProvider();

        var args = new NamedJobArgs { Value = "订单", Count = 9 };
        var context = new BackgroundJobExecutionContext(provider, typeof(RecordingNamedArgsJob), args);

        await CreateExecuter().ExecuteAsync(context);

        Assert.Single(job.Executed);
        Assert.Same(args, job.Executed[0]);
    }

    /// <summary>
    /// 上下文为 null 时抛出空引用参数异常
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenContextNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateExecuter().ExecuteAsync(null!));
    }

    /// <summary>
    /// 处理器未注册到容器时抛出执行异常，并提示可能缺少注册
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenJobNotRegistered_ThrowsExecutionException()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var context = new BackgroundJobExecutionContext(provider, typeof(RecordingNamedArgsJob), new NamedJobArgs());

        var exception = await Assert.ThrowsAsync<BackgroundJobExecutionException>(() => CreateExecuter().ExecuteAsync(context));

        Assert.Contains(typeof(RecordingNamedArgsJob).Name, exception.Message, StringComparison.Ordinal);
        Assert.Null(exception.InnerException);
    }

    /// <summary>
    /// 解析出来的对象没有实现作业接口时抛出执行异常
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenResolvedTypeIsNotJob_ThrowsExecutionException()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new NotABackgroundJob());
        using var provider = services.BuildServiceProvider();

        var context = new BackgroundJobExecutionContext(provider, typeof(NotABackgroundJob), new NamedJobArgs());

        var exception = await Assert.ThrowsAsync<BackgroundJobExecutionException>(() => CreateExecuter().ExecuteAsync(context));

        Assert.Contains("IAsyncBackgroundJob", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 作业体异步失败时包成执行异常并保留原始异常
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenHandlerFailsAsynchronously_WrapsOriginalException()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new AsyncThrowingJob());
        using var provider = services.BuildServiceProvider();

        var context = new BackgroundJobExecutionContext(provider, typeof(AsyncThrowingJob), new UnnamedJobArgs());

        var exception = await Assert.ThrowsAsync<BackgroundJobExecutionException>(() => CreateExecuter().ExecuteAsync(context));

        var inner = Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal("异步作业内部失败", inner.Message);
    }

    /// <summary>
    /// 作业体同步失败时（反射层抛 TargetInvocationException）同样解包出原始异常
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenHandlerFailsSynchronously_UnwrapsTargetInvocationException()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new SyncThrowingJob());
        using var provider = services.BuildServiceProvider();

        var context = new BackgroundJobExecutionContext(provider, typeof(SyncThrowingJob), new UnnamedJobArgs());

        var exception = await Assert.ThrowsAsync<BackgroundJobExecutionException>(() => CreateExecuter().ExecuteAsync(context));

        var inner = Assert.IsType<NotSupportedException>(exception.InnerException);
        Assert.Equal("同步作业立即失败", inner.Message);
    }

    /// <summary>
    /// 从作用域服务提供器解析处理器：换一个作用域应拿到该作用域自己的实例
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ResolvesHandlerFromContextServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<RecordingNamedArgsJob>();
        using var provider = services.BuildServiceProvider();

        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var executer = CreateExecuter();
        await executer.ExecuteAsync(new BackgroundJobExecutionContext(firstScope.ServiceProvider, typeof(RecordingNamedArgsJob), new NamedJobArgs()));
        await executer.ExecuteAsync(new BackgroundJobExecutionContext(secondScope.ServiceProvider, typeof(RecordingNamedArgsJob), new NamedJobArgs()));

        Assert.Single(firstScope.ServiceProvider.GetRequiredService<RecordingNamedArgsJob>().Executed);
        Assert.Single(secondScope.ServiceProvider.GetRequiredService<RecordingNamedArgsJob>().Executed);
    }

    /// <summary>
    /// 创建执行器（日志走空实现，避免用例依赖日志基础设施）
    /// </summary>
    /// <returns>执行器</returns>
    private static BackgroundJobExecuter CreateExecuter()
    {
        return new BackgroundJobExecuter(NullLogger<BackgroundJobExecuter>.Instance);
    }
}
