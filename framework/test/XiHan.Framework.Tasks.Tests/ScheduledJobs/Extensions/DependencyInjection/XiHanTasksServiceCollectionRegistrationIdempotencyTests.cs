// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Extensions.DependencyInjection;
using XiHan.Framework.Tasks.ScheduledJobs.Hosting;
using XiHan.Framework.Tasks.ScheduledJobs.Models;
using XiHan.Framework.Tasks.ScheduledJobs.Pipeline;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Extensions.DependencyInjection;

/// <summary>
/// XiHanTasksServiceCollectionExtensions 重复注册幂等性测试
/// </summary>
/// <remarks>
/// AddXiHanTasks 在真实装配里可能被调用两次：XiHanTasksModule 会调一次，业务侧为了传配置委托往往
/// 还会显式再调一次。核心服务本来就是 TryAddSingleton 幂等的，但五个内置中间件原来走的是普通
/// AddSingleton，叠加后会得到 10 条注册，整条执行管道被跑两遍。
/// 断言打在 ServiceDescriptor 上而不是解析实例：中间件的依赖（日志、分布式锁）会牵出整棵图，
/// 描述符断言更聚焦也更稳；也不断言"描述符总数恰好是 N"，避免把 DI 框架实现细节固化进来。
/// </remarks>
public class XiHanTasksServiceCollectionRegistrationIdempotencyTests
{
    /// <summary>
    /// 内置中间件的既定顺序，顺序即洋葱层次
    /// </summary>
    private static readonly Type[] BuiltInMiddlewares =
    [
        typeof(LoggingMiddleware),
        typeof(TimeoutMiddleware),
        typeof(LockMiddleware),
        typeof(RetryMiddleware),
        typeof(MetricsMiddleware)
    ];

    /// <summary>
    /// 重复调用时五个内置中间件仍各只有一条注册，且顺序不变
    /// </summary>
    [Fact]
    public void AddXiHanTasks_CalledTwice_KeepsBuiltInMiddlewaresSingleAndOrdered()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddXiHanTasks();
        services.AddXiHanTasks();

        Assert.Equal(BuiltInMiddlewares, MiddlewareImplementationTypes(services));
    }

    /// <summary>
    /// 调用三次同样只留一份，幂等不是"只挡第二次"
    /// </summary>
    [Fact]
    public void AddXiHanTasks_CalledThreeTimes_StillKeepsOneRegistrationPerMiddleware()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddXiHanTasks();
        services.AddXiHanTasks();
        services.AddXiHanTasks();

        Assert.Equal(BuiltInMiddlewares, MiddlewareImplementationTypes(services));
    }

    /// <summary>
    /// 模块装配走配置重载、业务侧再显式调一次委托重载，两条入口叠加同样只留一份
    /// </summary>
    [Fact]
    public void AddXiHanTasks_MixingConfigurationAndDelegateOverloads_StillRegistersEachMiddlewareOnce()
    {
        IServiceCollection services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddXiHanTasks(configuration);
        services.AddXiHanTasks(options => options.HistoryRetentionDays = 3);

        Assert.Equal(BuiltInMiddlewares, MiddlewareImplementationTypes(services));
    }

    /// <summary>
    /// 重复调用时托管服务也只有一条，调度器不会被启停两遍
    /// </summary>
    [Fact]
    public void AddXiHanTasks_CalledTwice_KeepsHostedServiceSingle()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddXiHanTasks();
        services.AddXiHanTasks();

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IHostedService));
        Assert.Equal(typeof(JobHostedService), descriptor.ImplementationType);
    }

    /// <summary>
    /// 幂等只针对内置中间件：调用方自己追加的中间件照常保留，且排在内置件之后
    /// </summary>
    [Fact]
    public void AddXiHanTasks_WithCustomMiddleware_KeepsItAlongsideBuiltInOnes()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddXiHanTasks();
        services.AddSingleton<IJobMiddleware, RecordingJobMiddleware>();
        services.AddXiHanTasks();

        var expected = BuiltInMiddlewares.Append(typeof(RecordingJobMiddleware)).ToList();

        Assert.Equal(expected, MiddlewareImplementationTypes(services));
    }

    /// <summary>
    /// 单次调用的注册结果不受影响：五个中间件按既定顺序各一条
    /// </summary>
    [Fact]
    public void AddXiHanTasks_CalledOnce_RegistersEachMiddlewareExactlyOnce()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddXiHanTasks();

        Assert.Equal(BuiltInMiddlewares, MiddlewareImplementationTypes(services));
    }

    /// <summary>
    /// 中间件均以单例生命周期注册
    /// </summary>
    [Fact]
    public void AddXiHanTasks_RegistersMiddlewaresAsSingletons()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddXiHanTasks();

        Assert.All(
            services.Where(item => item.ServiceType == typeof(IJobMiddleware)),
            item => Assert.Equal(ServiceLifetime.Singleton, item.Lifetime));
    }

    /// <summary>
    /// 取出当前注册的中间件实现类型，保持注册顺序
    /// </summary>
    private static List<Type> MiddlewareImplementationTypes(IServiceCollection services)
    {
        return [.. services
            .Where(item => item.ServiceType == typeof(IJobMiddleware))
            .Select(item => item.ImplementationType!)];
    }

    /// <summary>
    /// 调用方自备的中间件实现，只用于验证注册不被幂等逻辑吞掉
    /// </summary>
    public sealed class RecordingJobMiddleware : IJobMiddleware
    {
        /// <summary>
        /// 执行中间件逻辑
        /// </summary>
        public Task<JobResult> InvokeAsync(IJobContext context, JobExecutionDelegate next)
        {
            return next(context);
        }
    }
}
