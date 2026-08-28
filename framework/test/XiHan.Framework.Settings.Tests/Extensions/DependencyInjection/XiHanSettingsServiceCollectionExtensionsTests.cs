// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Settings.Extensions.DependencyInjection;
using XiHan.Framework.Settings.Options;
using XiHan.Framework.Settings.Providers;

namespace XiHan.Framework.Settings.Tests.Extensions.DependencyInjection;

/// <summary>
/// 曦寒设置服务集合扩展测试
/// </summary>
/// <remarks>
/// 这个扩展做两件事：登记四个内置值提供者的类型顺序（顺序即覆盖优先级：默认 → 配置 → 全局 → 用户），
/// 以及把 Aes 选项绑定到约定配置节。两者都是对外契约，用真实 <c>ServiceCollection</c> 走一遍解析验证。
/// </remarks>
public class XiHanSettingsServiceCollectionExtensionsTests
{
    /// <summary>
    /// 返回同一个服务集合以支持链式调用
    /// </summary>
    [Fact]
    public void AddXiHanSettings_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddXiHanSettings(BuildConfiguration());

        Assert.Same(services, returned);
    }

    /// <summary>
    /// 按"默认 → 配置 → 全局 → 用户"的顺序登记四个内置值提供者
    /// </summary>
    [Fact]
    public void AddXiHanSettings_RegistersBuiltInValueProvidersInPriorityOrder()
    {
        var services = new ServiceCollection();
        services.AddXiHanSettings(BuildConfiguration());
        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<XiHanSettingOptions>>().Value;

        Assert.Equal(4, options.ValueProviders.Count);
        Assert.Equal(typeof(DefaultValueSettingValueProvider), options.ValueProviders[0]);
        Assert.Equal(typeof(ConfigurationSettingValueProvider), options.ValueProviders[1]);
        Assert.Equal(typeof(GlobalSettingValueProvider), options.ValueProviders[2]);
        Assert.Equal(typeof(UserSettingValueProvider), options.ValueProviders[3]);
    }

    /// <summary>
    /// 不额外登记任何定义提供者
    /// </summary>
    /// <remarks>
    /// 定义提供者的收集由模块的 <c>PreConfigureServices</c> 通过注册钩子完成，不属于本扩展的职责。
    /// </remarks>
    [Fact]
    public void AddXiHanSettings_DoesNotRegisterDefinitionProviders()
    {
        var services = new ServiceCollection();
        services.AddXiHanSettings(BuildConfiguration());
        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<XiHanSettingOptions>>().Value;

        Assert.Empty(options.DefinitionProviders);
    }

    /// <summary>
    /// Aes 选项从约定配置节绑定
    /// </summary>
    [Fact]
    public void AddXiHanSettings_BindsAesOptionsFromConfigurationSection()
    {
        var configuration = BuildConfiguration(
            ($"{XiHanAesOptions.SectionName}:Key", "configured-key"),
            ($"{XiHanAesOptions.SectionName}:Iv", "configured-iv"));
        var services = new ServiceCollection();
        services.AddXiHanSettings(configuration);
        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<XiHanAesOptions>>().Value;

        Assert.Equal("configured-key", options.Key);
        Assert.Equal("configured-iv", options.Iv);
    }

    /// <summary>
    /// 配置节缺失时 Aes 选项保持空密钥，让加密路径 fail-closed
    /// </summary>
    [Fact]
    public void AddXiHanSettings_WhenAesSectionMissing_LeavesKeyEmpty()
    {
        var services = new ServiceCollection();
        services.AddXiHanSettings(BuildConfiguration(("Unrelated:Key", "x")));
        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<XiHanAesOptions>>().Value;

        Assert.Equal(string.Empty, options.Key);
        Assert.Equal(string.Empty, options.Iv);
    }

    /// <summary>
    /// 登记的值提供者类型与各自的提供者名常量一一对应
    /// </summary>
    /// <remarks>
    /// 类型顺序决定覆盖优先级，提供者名决定落库分区，两者必须同时稳定才算契约不漂移。
    /// </remarks>
    [Fact]
    public void AddXiHanSettings_RegisteredProvidersMatchTheirProviderNames()
    {
        var services = new ServiceCollection();
        services.AddXiHanSettings(BuildConfiguration());
        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<XiHanSettingOptions>>().Value;

        Assert.Equal("D", DefaultValueSettingValueProvider.ProviderName);
        Assert.Equal("C", ConfigurationSettingValueProvider.ProviderName);
        Assert.Equal("G", GlobalSettingValueProvider.ProviderName);
        Assert.Equal("U", UserSettingValueProvider.ProviderName);
        Assert.All(options.ValueProviders, x => Assert.True(typeof(ISettingValueProvider).IsAssignableFrom(x)));
    }

    /// <summary>
    /// 用内存配置源构造配置对象
    /// </summary>
    /// <param name="entries">键值对</param>
    /// <returns>配置对象</returns>
    private static IConfiguration BuildConfiguration(params (string Key, string? Value)[] entries)
    {
        var data = new Dictionary<string, string?>();
        foreach (var entry in entries)
        {
            data[entry.Key] = entry.Value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }
}
