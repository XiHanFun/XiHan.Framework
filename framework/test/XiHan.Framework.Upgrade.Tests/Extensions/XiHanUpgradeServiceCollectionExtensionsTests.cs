// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Upgrade.Abstractions;
using XiHan.Framework.Upgrade.Extensions;
using XiHan.Framework.Upgrade.Models;
using XiHan.Framework.Upgrade.Options;
using XiHan.Framework.Upgrade.Services;

namespace XiHan.Framework.Upgrade.Tests;

/// <summary>
/// 升级服务注册扩展测试
/// </summary>
/// <remarks>
/// 注册契约有两个关键点：默认实现能被解析出来（且生命周期正确），
/// 以及全部用 TryAdd 注册——应用层先注册的自定义实现不能被框架默认实现顶掉。
/// </remarks>
public class XiHanUpgradeServiceCollectionExtensionsTests
{
    /// <summary>
    /// 默认实现全部可解析，且解析出的是框架内置类型
    /// </summary>
    [Fact]
    public void AddXiHanUpgrade_RegistersDefaultImplementations()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        Assert.IsType<FileSystemUpgradeScriptProvider>(scopedProvider.GetRequiredService<IUpgradeScriptProvider>());
        Assert.IsType<InMemoryUpgradeVersionStore>(scopedProvider.GetRequiredService<IUpgradeVersionStore>());
        Assert.IsType<InMemoryUpgradeLockProvider>(scopedProvider.GetRequiredService<IUpgradeLockProvider>());
        Assert.IsType<DefaultUpgradeTenantProvider>(scopedProvider.GetRequiredService<IUpgradeTenantProvider>());
        Assert.IsType<DefaultUpgradeMigrationExecutor>(scopedProvider.GetRequiredService<IUpgradeMigrationExecutor>());
        Assert.IsType<UpgradeStatusService>(scopedProvider.GetRequiredService<IUpgradeStatusService>());
        Assert.IsType<UpgradeEngine>(scopedProvider.GetRequiredService<IUpgradeEngine>());
        Assert.IsType<UpgradeCoordinator>(scopedProvider.GetRequiredService<IUpgradeCoordinator>());
        Assert.IsType<DefaultUpgradeMaintenanceModeManager>(scopedProvider.GetRequiredService<IUpgradeMaintenanceModeManager>());
        Assert.IsType<NullUpgradeFileUpdater>(scopedProvider.GetRequiredService<IUpgradeFileUpdater>());
        Assert.IsType<NullRollingRestartCoordinator>(scopedProvider.GetRequiredService<IRollingRestartCoordinator>());
    }

    /// <summary>
    /// 各服务的生命周期符合设计：锁/脚本/协调器是单例，版本存储与引擎按请求作用域
    /// </summary>
    [Fact]
    public void AddXiHanUpgrade_UsesExpectedLifetimes()
    {
        var services = new ServiceCollection();
        services.AddXiHanUpgrade(new ConfigurationBuilder().Build());

        Assert.Equal(ServiceLifetime.Singleton, DescriptorOf(services, typeof(IUpgradeScriptProvider)).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, DescriptorOf(services, typeof(IUpgradeVersionStore)).Lifetime);
        Assert.Equal(ServiceLifetime.Singleton, DescriptorOf(services, typeof(IUpgradeLockProvider)).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, DescriptorOf(services, typeof(IUpgradeTenantProvider)).Lifetime);
        Assert.Equal(ServiceLifetime.Singleton, DescriptorOf(services, typeof(IUpgradeMigrationExecutor)).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, DescriptorOf(services, typeof(IUpgradeStatusService)).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, DescriptorOf(services, typeof(IUpgradeEngine)).Lifetime);
        Assert.Equal(ServiceLifetime.Singleton, DescriptorOf(services, typeof(IUpgradeCoordinator)).Lifetime);
        Assert.Equal(ServiceLifetime.Singleton, DescriptorOf(services, typeof(IUpgradeMaintenanceModeManager)).Lifetime);
        Assert.Equal(ServiceLifetime.Singleton, DescriptorOf(services, typeof(IUpgradeFileUpdater)).Lifetime);
        Assert.Equal(ServiceLifetime.Singleton, DescriptorOf(services, typeof(IRollingRestartCoordinator)).Lifetime);
    }

    /// <summary>
    /// 升级锁是进程内共享单例，版本存储每个作用域一个实例
    /// </summary>
    [Fact]
    public void AddXiHanUpgrade_LockIsSharedWhileVersionStoreIsScoped()
    {
        using var provider = BuildProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        Assert.Same(
            firstScope.ServiceProvider.GetRequiredService<IUpgradeLockProvider>(),
            secondScope.ServiceProvider.GetRequiredService<IUpgradeLockProvider>());
        Assert.NotSame(
            firstScope.ServiceProvider.GetRequiredService<IUpgradeVersionStore>(),
            secondScope.ServiceProvider.GetRequiredService<IUpgradeVersionStore>());
        Assert.Same(
            firstScope.ServiceProvider.GetRequiredService<IUpgradeVersionStore>(),
            firstScope.ServiceProvider.GetRequiredService<IUpgradeVersionStore>());
    }

    /// <summary>
    /// 选项从 XiHan:Upgrade 配置节绑定
    /// </summary>
    [Fact]
    public void AddXiHanUpgrade_BindsOptionsFromSection()
    {
        using var provider = BuildProvider(settings: new Dictionary<string, string?>
        {
            ["XiHan:Upgrade:MinSupportVersion"] = "2.1.0",
            ["XiHan:Upgrade:LockResourceKey"] = "MyUpgrade",
            ["XiHan:Upgrade:EnableFileUpdate"] = "true"
        });

        var upgradeOptions = provider.GetRequiredService<IOptions<XiHanUpgradeOptions>>().Value;

        Assert.Equal("2.1.0", upgradeOptions.MinSupportVersion);
        Assert.Equal("MyUpgrade", upgradeOptions.LockResourceKey);
        Assert.True(upgradeOptions.EnableFileUpdate);
    }

    /// <summary>
    /// 已经注册过的实现不会被默认实现覆盖（TryAdd 语义）
    /// </summary>
    [Fact]
    public void AddXiHanUpgrade_DoesNotOverrideExistingRegistrations()
    {
        using var provider = BuildProvider(services =>
        {
            services.AddSingleton<IUpgradeMigrationExecutor, CustomMigrationExecutor>();
            services.AddSingleton<IUpgradeFileUpdater, CustomFileUpdater>();
        });

        Assert.IsType<CustomMigrationExecutor>(provider.GetRequiredService<IUpgradeMigrationExecutor>());
        Assert.IsType<CustomFileUpdater>(provider.GetRequiredService<IUpgradeFileUpdater>());
    }

    /// <summary>
    /// 扩展方法返回同一个服务集合实例，支持链式调用
    /// </summary>
    [Fact]
    public void AddXiHanUpgrade_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddXiHanUpgrade(new ConfigurationBuilder().Build());

        Assert.Same(services, returned);
    }

    /// <summary>
    /// 追加脚本提供者时与内置提供者共存，引擎可以拿到全部提供者
    /// </summary>
    [Fact]
    public void AddUpgradeScriptProvider_AppendsProviderAlongsideBuiltIn()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IApplicationInfoAccessor>(new FakeApplicationInfoAccessor());
        services.AddXiHanUpgrade(new ConfigurationBuilder().Build());

        var returned = services.AddUpgradeScriptProvider<CustomScriptProvider>();

        using var provider = services.BuildServiceProvider();
        var scriptProviders = provider.GetServices<IUpgradeScriptProvider>().ToList();

        Assert.Same(services, returned);
        Assert.Equal(2, scriptProviders.Count);
        Assert.Contains(scriptProviders, item => item is FileSystemUpgradeScriptProvider);
        Assert.Contains(scriptProviders, item => item is CustomScriptProvider);
    }

    /// <summary>
    /// 取出指定服务类型的唯一注册描述
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="serviceType">服务类型</param>
    /// <returns>服务描述</returns>
    private static ServiceDescriptor DescriptorOf(IServiceCollection services, Type serviceType)
    {
        return Assert.Single(services, descriptor => descriptor.ServiceType == serviceType);
    }

    /// <summary>
    /// 构建注册了升级服务的服务提供器
    /// </summary>
    /// <param name="configure">额外的注册动作（在 AddXiHanUpgrade 之前执行）</param>
    /// <param name="settings">内存配置项</param>
    /// <returns>服务提供器</returns>
    private static ServiceProvider BuildProvider(
        Action<IServiceCollection>? configure = null,
        Dictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings ?? [])
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IApplicationInfoAccessor>(new FakeApplicationInfoAccessor());
        configure?.Invoke(services);
        services.AddXiHanUpgrade(configuration);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 应用层自定义迁移执行器
    /// </summary>
    public sealed class CustomMigrationExecutor : IUpgradeMigrationExecutor
    {
        /// <summary>
        /// 执行迁移脚本
        /// </summary>
        /// <param name="sql">脚本内容</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        public Task ExecuteAsync(string sql, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 应用层自定义文件更新器
    /// </summary>
    public sealed class CustomFileUpdater : IUpgradeFileUpdater
    {
        /// <summary>
        /// 替换程序文件
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        public Task ApplyAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 应用层自定义脚本提供者
    /// </summary>
    public sealed class CustomScriptProvider : IUpgradeScriptProvider
    {
        /// <summary>
        /// 获取升级脚本列表
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>脚本列表</returns>
        public Task<IReadOnlyList<UpgradeScript>> GetScriptsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<UpgradeScript>>([new UpgradeScript("9.9.9", "custom.sql", "custom.sql")]);
        }
    }
}
