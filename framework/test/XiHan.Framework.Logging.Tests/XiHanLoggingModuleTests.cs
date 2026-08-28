// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Logging.Options;
using XiHan.Framework.Logging.Services;

namespace XiHan.Framework.Logging.Tests;

/// <summary>
/// 曦寒日志模块测试
/// </summary>
/// <remarks>
/// 模块有两个阶段：PreConfigureServices 只铺默认选项，ConfigureServices 才做真正的服务登记，
/// 且后者强依赖容器里已有 IConfiguration。这两点决定了模块在模块化启动链里的可组合性，逐个验证。
/// </remarks>
public class XiHanLoggingModuleTests
{
    /// <summary>
    /// 日志模块是标准的曦寒模块
    /// </summary>
    /// <remarks>
    /// 模块发现按基类筛选，基类一旦换掉，模块会在启动期被静默跳过，所有日志配置随之失效。
    /// </remarks>
    [Fact]
    public void Module_DerivesFromXiHanModule()
    {
        Assert.True(typeof(XiHanLoggingModule).IsSubclassOf(typeof(XiHanModule)));
    }

    /// <summary>
    /// 预配置阶段铺好启用开关与最小级别默认值
    /// </summary>
    [Fact]
    public void PreConfigureServices_SeedsEnabledFlagAndInformationLevel()
    {
        IServiceCollection services = new ServiceCollection();

        new XiHanLoggingModule().PreConfigureServices(new ServiceConfigurationContext(services));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanLoggingOptions>>().Value;

        Assert.True(options.IsEnabled);
        Assert.Equal(LogLevel.Information, options.MinimumLevel);
    }

    /// <summary>
    /// 预配置阶段不登记任何日志服务
    /// </summary>
    /// <remarks>
    /// 预配置阶段跑在全部模块的正式配置之前，此时抢注服务会让宿主失去覆盖机会。
    /// </remarks>
    [Fact]
    public void PreConfigureServices_DoesNotRegisterLoggingServices()
    {
        IServiceCollection services = new ServiceCollection();

        new XiHanLoggingModule().PreConfigureServices(new ServiceConfigurationContext(services));

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IXiHanLoggerFactory));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ILogContext));
    }

    /// <summary>
    /// 配置阶段缺少配置对象时显式失败
    /// </summary>
    [Fact]
    public void ConfigureServices_WithoutConfiguration_ThrowsXiHanException()
    {
        IServiceCollection services = new ServiceCollection();
        var context = new ServiceConfigurationContext(services);

        Assert.Throws<XiHanException>(() => new XiHanLoggingModule().ConfigureServices(context));
    }

    /// <summary>
    /// 配置阶段登记完整的日志服务集合
    /// </summary>
    [Fact]
    public void ConfigureServices_WithConfiguration_RegistersLoggingServices()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        new XiHanLoggingModule().ConfigureServices(new ServiceConfigurationContext(services));

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IXiHanLoggerFactory));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IXiHanLogger));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IStructuredLogger));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IPerformanceLogger));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ILogContext));
    }

    /// <summary>
    /// 配置阶段把配置节里的日志选项绑定进来
    /// </summary>
    [Fact]
    public void ConfigureServices_WithConfiguration_BindsOptionsFromSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XiHan:Logging:MinimumLevel"] = "Error",
                ["XiHan:Logging:EnableRequestLogging"] = "false"
            })
            .Build();

        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        new XiHanLoggingModule().ConfigureServices(new ServiceConfigurationContext(services));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanLoggingOptions>>().Value;

        Assert.Equal(LogLevel.Error, options.MinimumLevel);
        Assert.False(options.EnableRequestLogging);
    }

    /// <summary>
    /// 预配置的默认值会被配置节里的显式取值覆盖
    /// </summary>
    /// <remarks>
    /// 模块化启动是「先预配置铺底、后配置阶段覆盖」，顺序反了会导致 appsettings 写了也不生效。
    /// </remarks>
    [Fact]
    public void ConfigureServices_AfterPreConfigure_LetsConfigurationWin()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XiHan:Logging:MinimumLevel"] = "Critical"
            })
            .Build();

        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        var context = new ServiceConfigurationContext(services);
        var module = new XiHanLoggingModule();

        module.PreConfigureServices(context);
        module.ConfigureServices(context);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanLoggingOptions>>().Value;

        Assert.Equal(LogLevel.Critical, options.MinimumLevel);
    }
}
