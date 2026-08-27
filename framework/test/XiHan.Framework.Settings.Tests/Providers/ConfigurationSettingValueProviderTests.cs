// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Settings.Definitions;
using XiHan.Framework.Settings.Providers;

namespace XiHan.Framework.Settings.Tests.Providers;

/// <summary>
/// 配置设置值提供者测试
/// </summary>
/// <remarks>
/// 该提供者把 appsettings 中的 <c>Settings:&lt;设置名&gt;</c> 映射成设置值，
/// 前缀属于对外配置契约，缺键必须安静返回 null 而不是抛异常。
/// </remarks>
public class ConfigurationSettingValueProviderTests
{
    /// <summary>
    /// 配置前缀与提供者名称常量保持稳定
    /// </summary>
    [Fact]
    public void Constants_AreStable()
    {
        Assert.Equal("Settings:", ConfigurationSettingValueProvider.ConfigurationNamePrefix);
        Assert.Equal("C", ConfigurationSettingValueProvider.ProviderName);
        Assert.Equal("C", new ConfigurationSettingValueProvider(BuildConfiguration()).Name);
    }

    /// <summary>
    /// 单项读取命中带前缀的配置键
    /// </summary>
    [Fact]
    public async Task GetOrNullAsync_ReadsPrefixedConfigurationKey()
    {
        var provider = new ConfigurationSettingValueProvider(BuildConfiguration(("Settings:Foo", "from-config")));

        var value = await provider.GetOrNullAsync(new SettingDefinition("Foo", "the-default"));

        Assert.Equal("from-config", value);
    }

    /// <summary>
    /// 配置里没有该键时返回 null，且不回落到定义的默认值
    /// </summary>
    [Fact]
    public async Task GetOrNullAsync_WhenKeyMissing_ReturnsNull()
    {
        var provider = new ConfigurationSettingValueProvider(BuildConfiguration(("Settings:Other", "x")));

        var value = await provider.GetOrNullAsync(new SettingDefinition("Foo", "the-default"));

        Assert.Null(value);
    }

    /// <summary>
    /// 不带前缀的同名配置键不会被误读
    /// </summary>
    [Fact]
    public async Task GetOrNullAsync_IgnoresUnprefixedKey()
    {
        var provider = new ConfigurationSettingValueProvider(BuildConfiguration(("Foo", "unprefixed")));

        var value = await provider.GetOrNullAsync(new SettingDefinition("Foo"));

        Assert.Null(value);
    }

    /// <summary>
    /// 支持带冒号分层的设置名
    /// </summary>
    [Fact]
    public async Task GetOrNullAsync_SupportsHierarchicalSettingName()
    {
        var provider = new ConfigurationSettingValueProvider(BuildConfiguration(("Settings:Mail:Host", "smtp.test")));

        var value = await provider.GetOrNullAsync(new SettingDefinition("Mail:Host"));

        Assert.Equal("smtp.test", value);
    }

    /// <summary>
    /// 批量读取逐条按前缀取值，缺键位置留 null，顺序与入参一致
    /// </summary>
    [Fact]
    public async Task GetAllAsync_MapsEveryDefinitionByPrefixedKey()
    {
        var provider = new ConfigurationSettingValueProvider(BuildConfiguration(
            ("Settings:Foo", "foo-value"),
            ("Settings:Baz", "baz-value")));
        var settings = new[]
        {
            new SettingDefinition("Foo"),
            new SettingDefinition("Bar"),
            new SettingDefinition("Baz")
        };

        var values = await provider.GetAllAsync(settings);

        Assert.Equal(new[] { "Foo", "Bar", "Baz" }, values.Select(x => x.Name).ToArray());
        Assert.Equal(new[] { "foo-value", null, "baz-value" }, values.Select(x => x.Value).ToArray());
    }

    /// <summary>
    /// 该提供者按瞬时依赖登记，且直接实现值提供者接口
    /// </summary>
    /// <remarks>
    /// 它不继承 <see cref="SettingValueProvider"/>——因为不依赖设置存储，只依赖配置。
    /// </remarks>
    [Fact]
    public void ConfigurationSettingValueProvider_IsTransientValueProvider()
    {
        Assert.True(typeof(ITransientDependency).IsAssignableFrom(typeof(ConfigurationSettingValueProvider)));
        Assert.True(typeof(ISettingValueProvider).IsAssignableFrom(typeof(ConfigurationSettingValueProvider)));
        Assert.False(typeof(SettingValueProvider).IsAssignableFrom(typeof(ConfigurationSettingValueProvider)));
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
