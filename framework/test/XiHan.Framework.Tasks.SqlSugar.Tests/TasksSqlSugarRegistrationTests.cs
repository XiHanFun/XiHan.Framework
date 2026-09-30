// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs;
using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Store;
using XiHan.Framework.Tasks.SqlSugar.BackgroundJobs;
using XiHan.Framework.Tasks.SqlSugar.Clients;
using XiHan.Framework.Tasks.SqlSugar.Extensions.DependencyInjection;
using XiHan.Framework.Tasks.SqlSugar.ScheduledJobs;
using XiHan.Framework.Timing;

namespace XiHan.Framework.Tasks.SqlSugar.Tests;

/// <summary>
/// 任务 SqlSugar 存储注册测试
/// </summary>
public class TasksSqlSugarRegistrationTests
{
    /// <summary>
    /// 后台作业存储被顶替为单例
    /// </summary>
    [Fact]
    public void 后台作业存储被顶替为单例()
    {
        var services = new ServiceCollection();
        services.TryAddSingleton<IBackgroundJobStore, DefaultBackgroundJobStore>();

        services.AddXiHanTasksSqlSugar(new ConfigurationBuilder().Build());

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IBackgroundJobStore));
        Assert.Equal(typeof(SqlSugarBackgroundJobStore), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    /// <summary>
    /// 定时任务存储被顶替为单例
    /// </summary>
    [Fact]
    public void 定时任务存储被顶替为单例()
    {
        var services = new ServiceCollection();
        services.TryAddSingleton<IJobStore, DefaultJobStore>();

        services.AddXiHanTasksSqlSugar(new ConfigurationBuilder().Build());

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IJobStore));
        Assert.Equal(typeof(SqlSugarJobStore), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    /// <summary>
    /// 客户端访问器注册为单例
    /// </summary>
    [Fact]
    public void 客户端访问器注册为单例()
    {
        var services = new ServiceCollection();

        services.AddXiHanTasksSqlSugar(new ConfigurationBuilder().Build());

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(TasksHostClientAccessor));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    /// <summary>
    /// 校验作用域时可从根容器解析后台作业存储
    /// </summary>
    [Fact]
    public void 校验作用域时可从根容器解析后台作业存储()
    {
        using var provider = BuildValidatingProvider();

        Assert.IsType<SqlSugarBackgroundJobStore>(provider.GetRequiredService<IBackgroundJobStore>());
    }

    /// <summary>
    /// 校验作用域时可从根容器解析定时任务存储
    /// </summary>
    [Fact]
    public void 校验作用域时可从根容器解析定时任务存储()
    {
        using var provider = BuildValidatingProvider();

        Assert.IsType<SqlSugarJobStore>(provider.GetRequiredService<IJobStore>());
    }

    private static ServiceProvider BuildValidatingProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock>(new FakeClock(TasksTestContext.BaseTime));
        services.AddTransient<ICurrentTenant>(_ => new CurrentTenant(AsyncLocalCurrentTenantAccessor.Instance));
        services.AddScoped<ISqlSugarClientResolver>(_ => throw new InvalidOperationException("解析存储时不应触达数据库。"));

        services.AddXiHanTasksSqlSugar(new ConfigurationBuilder().Build());

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }
}
