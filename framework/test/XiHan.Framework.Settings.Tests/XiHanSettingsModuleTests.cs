// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Security;
using XiHan.Framework.Settings.Definitions;
using XiHan.Framework.Settings.Options;
using XiHan.Framework.Settings.Stores;
using XiHan.Framework.Settings.Tests.Fakes;

namespace XiHan.Framework.Settings.Tests;

/// <summary>
/// 曦寒框架设置模块测试
/// </summary>
/// <remarks>
/// 模块的两段装配可以脱离完整的模块化启动流程单独驱动：
/// <c>PreConfigureServices</c> 只挂一个服务注册钩子，把实现了 <see cref="ISettingDefinitionProvider"/>
/// 的实现类型自动收进选项；<c>ConfigureServices</c> 从服务集合里取配置并转调设置服务扩展。
/// 这里手动触发注册钩子来验证自动收集逻辑，而不是去跑整个宿主启动。
/// </remarks>
public class XiHanSettingsModuleTests
{
    /// <summary>
    /// 模块声明依赖安全模块——用户级设置依赖当前用户抽象来自安全模块
    /// </summary>
    [Fact]
    public void XiHanSettingsModule_DependsOnSecurityModule()
    {
        var attributes = typeof(XiHanSettingsModule)
            .GetCustomAttributes<DependsOnAttribute>(false)
            .ToArray();

        var dependedTypes = attributes.SelectMany(x => x.GetDependedTypes()).ToArray();

        Assert.Contains(typeof(XiHanSecurityModule), dependedTypes);
    }

    /// <summary>
    /// 模块是标准的曦寒模块
    /// </summary>
    [Fact]
    public void XiHanSettingsModule_IsXiHanModule()
    {
        Assert.IsAssignableFrom<XiHanModule>(new XiHanSettingsModule());
    }

    /// <summary>
    /// 预配置阶段挂上的注册钩子，只把设置定义提供者收进选项
    /// </summary>
    [Fact]
    public void PreConfigureServices_CollectsOnlyDefinitionProvidersFromRegistrationHook()
    {
        var services = new ServiceCollection();
        new XiHanSettingsModule().PreConfigureServices(new ServiceConfigurationContext(services));

        foreach (var action in services.GetRegistrationActionList())
        {
            action(new OnServiceRegistredContext(typeof(ISettingDefinitionProvider), typeof(AlphaSettingDefinitionProvider)));
            action(new OnServiceRegistredContext(typeof(ISettingDefinitionProvider), typeof(BetaSettingDefinitionProvider)));
            action(new OnServiceRegistredContext(typeof(ISettingStore), typeof(NullSettingStore)));
        }

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<XiHanSettingOptions>>().Value;

        Assert.Contains(typeof(AlphaSettingDefinitionProvider), options.DefinitionProviders);
        Assert.Contains(typeof(BetaSettingDefinitionProvider), options.DefinitionProviders);
        Assert.DoesNotContain(typeof(NullSettingStore), options.DefinitionProviders);
    }

    /// <summary>
    /// 没有任何实现类型被注册时，定义提供者列表保持为空
    /// </summary>
    [Fact]
    public void PreConfigureServices_WhenNothingRegistered_LeavesDefinitionProvidersEmpty()
    {
        var services = new ServiceCollection();
        new XiHanSettingsModule().PreConfigureServices(new ServiceConfigurationContext(services));

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<XiHanSettingOptions>>().Value;

        Assert.Empty(options.DefinitionProviders);
    }

    /// <summary>
    /// 同一个定义提供者被重复注册时不会在选项里出现两次
    /// </summary>
    [Fact]
    public void PreConfigureServices_DeduplicatesDefinitionProviders()
    {
        var services = new ServiceCollection();
        new XiHanSettingsModule().PreConfigureServices(new ServiceConfigurationContext(services));

        foreach (var action in services.GetRegistrationActionList())
        {
            action(new OnServiceRegistredContext(typeof(ISettingDefinitionProvider), typeof(AlphaSettingDefinitionProvider)));
            action(new OnServiceRegistredContext(typeof(ISettingDefinitionProvider), typeof(AlphaSettingDefinitionProvider)));
        }

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<XiHanSettingOptions>>().Value;

        Assert.Single(options.DefinitionProviders);
    }

    /// <summary>
    /// 服务配置阶段登记四个内置值提供者并绑定 Aes 选项
    /// </summary>
    [Fact]
    public void ConfigureServices_RegistersValueProvidersAndBindsAesOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{XiHanAesOptions.SectionName}:Key"] = "module-key"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        new XiHanSettingsModule().ConfigureServices(new ServiceConfigurationContext(services));

        using var serviceProvider = services.BuildServiceProvider();
        Assert.Equal(4, serviceProvider.GetRequiredService<IOptions<XiHanSettingOptions>>().Value.ValueProviders.Count);
        Assert.Equal("module-key", serviceProvider.GetRequiredService<IOptions<XiHanAesOptions>>().Value.Key);
    }
}
