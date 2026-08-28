// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Configuration;
using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Configuration;

/// <summary>
/// XiHanJobBuilder 构建器测试
/// </summary>
/// <remarks>
/// 构建器就是一层对 IServiceCollection 的语义封装，逐个方法验证"注册了什么服务、什么生命周期、
/// 返回值是否可链式"，用真实的 ServiceCollection 而不是替身。
/// </remarks>
public class XiHanJobBuilderTests
{
    /// <summary>
    /// 服务集合为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void Constructor_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new XiHanJobBuilder(null!));
    }

    /// <summary>
    /// 构建器原样暴露传入的服务集合
    /// </summary>
    [Fact]
    public void Services_ExposesTheSameCollection()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Same(services, new XiHanJobBuilder(services).Services);
    }

    /// <summary>
    /// 指定自定义存储时以单例注册到 IJobStore
    /// </summary>
    [Fact]
    public void UseStore_RegistersSingletonJobStore()
    {
        IServiceCollection services = new ServiceCollection();
        var builder = new XiHanJobBuilder(services);

        var returned = builder.UseStore<FakeJobStore>();

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IJobStore));
        Assert.Equal(typeof(FakeJobStore), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Same(builder, returned);
    }

    /// <summary>
    /// 指定自定义锁提供者时以单例注册到 IJobLockProvider
    /// </summary>
    [Fact]
    public void UseLockProvider_RegistersSingletonLockProvider()
    {
        IServiceCollection services = new ServiceCollection();
        var builder = new XiHanJobBuilder(services);

        var returned = builder.UseLockProvider<FakeJobLockProvider>();

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IJobLockProvider));
        Assert.Equal(typeof(FakeJobLockProvider), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Same(builder, returned);
    }

    /// <summary>
    /// 添加中间件是追加语义，多次调用会形成有序的中间件列表
    /// </summary>
    [Fact]
    public void AddMiddleware_AppendsInsteadOfReplacing()
    {
        IServiceCollection services = new ServiceCollection();
        var builder = new XiHanJobBuilder(services);

        builder.AddMiddleware<FirstFakeMiddleware>().AddMiddleware<SecondFakeMiddleware>();

        var descriptors = services.Where(item => item.ServiceType == typeof(IJobMiddleware)).ToList();
        Assert.Equal(2, descriptors.Count);
        Assert.Equal(typeof(FirstFakeMiddleware), descriptors[0].ImplementationType);
        Assert.Equal(typeof(SecondFakeMiddleware), descriptors[1].ImplementationType);
        Assert.All(descriptors, descriptor => Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime));
    }

    /// <summary>
    /// 添加任务时按具体类型以瞬时生命周期注册，便于每次执行拿到干净实例
    /// </summary>
    [Fact]
    public void AddJob_RegistersTransientConcreteType()
    {
        IServiceCollection services = new ServiceCollection();
        var builder = new XiHanJobBuilder(services);

        var returned = builder.AddJob<FakeJobWorker>();

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(FakeJobWorker));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
        Assert.Same(builder, returned);
    }

    /// <summary>
    /// 配置委托被应用到选项上，可通过 IOptions 解析回来
    /// </summary>
    [Fact]
    public void Configure_AppliesDelegateToResolvedOptions()
    {
        IServiceCollection services = new ServiceCollection();
        var builder = new XiHanJobBuilder(services);

        var returned = builder.Configure(options =>
        {
            options.Enabled = false;
            options.HistoryRetentionDays = 3;
            options.NodeName = "node-x";
        });

        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<IOptions<XiHanJobOptions>>().Value;

        Assert.False(resolved.Enabled);
        Assert.Equal(3, resolved.HistoryRetentionDays);
        Assert.Equal("node-x", resolved.NodeName);
        Assert.Same(builder, returned);
    }

    /// <summary>
    /// 多次配置按注册顺序叠加，后配置的覆盖前面的同名项
    /// </summary>
    [Fact]
    public void Configure_CalledTwice_AppliesInRegistrationOrder()
    {
        IServiceCollection services = new ServiceCollection();
        var builder = new XiHanJobBuilder(services);

        builder.Configure(options => options.HistoryRetentionDays = 3)
               .Configure(options => options.HistoryRetentionDays = 9);

        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<IOptions<XiHanJobOptions>>().Value;

        Assert.Equal(9, resolved.HistoryRetentionDays);
    }

    /// <summary>
    /// 完整链式配置后每一类服务都能各自解析出来
    /// </summary>
    [Fact]
    public void FluentChain_RegistersEveryConfiguredService()
    {
        IServiceCollection services = new ServiceCollection();

        new XiHanJobBuilder(services)
            .UseStore<FakeJobStore>()
            .UseLockProvider<FakeJobLockProvider>()
            .AddMiddleware<FirstFakeMiddleware>()
            .AddJob<FakeJobWorker>()
            .Configure(options => options.EnableMetrics = false);

        using var provider = services.BuildServiceProvider();

        Assert.IsType<FakeJobStore>(provider.GetRequiredService<IJobStore>());
        Assert.IsType<FakeJobLockProvider>(provider.GetRequiredService<IJobLockProvider>());
        Assert.IsType<FirstFakeMiddleware>(provider.GetRequiredService<IJobMiddleware>());
        Assert.NotNull(provider.GetRequiredService<FakeJobWorker>());
        Assert.False(provider.GetRequiredService<IOptions<XiHanJobOptions>>().Value.EnableMetrics);
    }

    /// <summary>
    /// 假任务存储
    /// </summary>
    public sealed class FakeJobStore : IJobStore
    {
        /// <summary>
        /// 保存任务实例
        /// </summary>
        public Task SaveJobInstanceAsync(JobInstance jobInstance)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 更新任务实例状态
        /// </summary>
        public Task UpdateJobStatusAsync(string instanceId, JobStatus status)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 保存执行历史
        /// </summary>
        public Task SaveJobHistoryAsync(JobHistory history)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 获取任务实例
        /// </summary>
        public Task<JobInstance?> GetJobInstanceAsync(string instanceId)
        {
            return Task.FromResult<JobInstance?>(null);
        }

        /// <summary>
        /// 获取执行历史
        /// </summary>
        public Task<IReadOnlyList<JobHistory>> GetJobHistoryAsync(string jobName, int pageIndex = 1, int pageSize = 20)
        {
            return Task.FromResult<IReadOnlyList<JobHistory>>(new List<JobHistory>());
        }

        /// <summary>
        /// 获取运行中的任务实例
        /// </summary>
        public Task<IReadOnlyList<JobInstance>> GetRunningInstancesAsync(string jobName)
        {
            return Task.FromResult<IReadOnlyList<JobInstance>>(new List<JobInstance>());
        }

        /// <summary>
        /// 清理过期历史
        /// </summary>
        public Task CleanupHistoryAsync(int retentionDays)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 假锁提供者
    /// </summary>
    public sealed class FakeJobLockProvider : IJobLockProvider
    {
        /// <summary>
        /// 尝试获取锁
        /// </summary>
        public Task<ILockToken?> TryAcquireLockAsync(string resourceKey, TimeSpan expiry, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ILockToken?>(null);
        }
    }

    /// <summary>
    /// 第一个假中间件
    /// </summary>
    public sealed class FirstFakeMiddleware : IJobMiddleware
    {
        /// <summary>
        /// 执行中间件逻辑
        /// </summary>
        public Task<JobResult> InvokeAsync(IJobContext context, JobExecutionDelegate next)
        {
            return next(context);
        }
    }

    /// <summary>
    /// 第二个假中间件
    /// </summary>
    public sealed class SecondFakeMiddleware : IJobMiddleware
    {
        /// <summary>
        /// 执行中间件逻辑
        /// </summary>
        public Task<JobResult> InvokeAsync(IJobContext context, JobExecutionDelegate next)
        {
            return next(context);
        }
    }

    /// <summary>
    /// 假任务体
    /// </summary>
    public sealed class FakeJobWorker : IJobWorker
    {
        /// <summary>
        /// 执行任务
        /// </summary>
        public Task<JobResult> ExecuteAsync(IJobContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(JobResult.Success());
        }
    }
}
