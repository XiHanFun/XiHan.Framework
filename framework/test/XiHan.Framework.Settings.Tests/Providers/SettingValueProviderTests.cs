// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Settings.Definitions;
using XiHan.Framework.Settings.Providers;
using XiHan.Framework.Settings.Stores;
using XiHan.Framework.Settings.Tests.Fakes;

namespace XiHan.Framework.Settings.Tests.Providers;

/// <summary>
/// 设置值提供者基类测试
/// </summary>
/// <remarks>
/// 基类本身是抽象的，这里写一个最小具体子类来验证"构造时注入的设置存储原样交给子类"，
/// 并顺带锁死四个内置提供者的名称互不重复——提供者名是存储层的分区键，重名等于串数据。
/// </remarks>
public class SettingValueProviderTests
{
    /// <summary>
    /// 注入的设置存储原样暴露给子类
    /// </summary>
    [Fact]
    public void Ctor_ExposesInjectedStoreToDerivedProvider()
    {
        var store = new FakeSettingStore();

        var provider = new ProbeSettingValueProvider(store);

        Assert.Same(store, provider.ExposedStore);
        Assert.Equal("PROBE", provider.Name);
    }

    /// <summary>
    /// 子类可以直接借助基类持有的存储完成读取
    /// </summary>
    [Fact]
    public async Task DerivedProvider_CanReadThroughInheritedStore()
    {
        var store = new FakeSettingStore();
        store.Seed("Foo", "PROBE", null, "probe-value");
        var provider = new ProbeSettingValueProvider(store);

        var value = await provider.GetOrNullAsync(new SettingDefinition("Foo"));

        Assert.Equal("probe-value", value);
    }

    /// <summary>
    /// 子类的批量读取同样走基类持有的存储
    /// </summary>
    [Fact]
    public async Task DerivedProvider_CanReadAllThroughInheritedStore()
    {
        var store = new FakeSettingStore();
        var provider = new ProbeSettingValueProvider(store);

        var values = await provider.GetAllAsync([new SettingDefinition("Foo"), new SettingDefinition("Bar")]);

        Assert.Equal(new[] { "Foo", "Bar" }, values.Select(x => x.Name).ToArray());
        var call = Assert.Single(store.GetAllCalls);
        Assert.Equal("PROBE", call.ProviderName);
    }

    /// <summary>
    /// 基类实现值提供者接口并按瞬时依赖登记
    /// </summary>
    [Fact]
    public void SettingValueProvider_IsTransientValueProvider()
    {
        Assert.True(typeof(ISettingValueProvider).IsAssignableFrom(typeof(SettingValueProvider)));
        Assert.True(typeof(ITransientDependency).IsAssignableFrom(typeof(SettingValueProvider)));
        Assert.True(typeof(SettingValueProvider).IsAbstract);
    }

    /// <summary>
    /// 四个内置提供者的名称两两不同
    /// </summary>
    [Fact]
    public void BuiltInProviders_HaveDistinctNames()
    {
        var store = new FakeSettingStore();
        var configuration = new ConfigurationBuilder().Build();

        var names = new[]
        {
            new DefaultValueSettingValueProvider(store).Name,
            new ConfigurationSettingValueProvider(configuration).Name,
            new GlobalSettingValueProvider(store).Name,
            new UserSettingValueProvider(store, new FakeCurrentUser()).Name
        };

        Assert.Equal(new[] { "D", "C", "G", "U" }, names);
        Assert.Equal(names.Length, names.Distinct().Count());
    }

    /// <summary>
    /// 用于验证基类行为的最小具体子类
    /// </summary>
    private sealed class ProbeSettingValueProvider : SettingValueProvider
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="settingStore">设置存储</param>
        public ProbeSettingValueProvider(ISettingStore settingStore)
            : base(settingStore)
        {
        }

        /// <summary>
        /// 名称
        /// </summary>
        public override string Name => "PROBE";

        /// <summary>
        /// 把受保护的设置存储暴露出来，供断言注入是否原样落位
        /// </summary>
        public ISettingStore ExposedStore => SettingStore;

        /// <summary>
        /// 获取设置值
        /// </summary>
        /// <param name="setting">设置定义</param>
        /// <returns>设置值</returns>
        public override Task<string?> GetOrNullAsync(SettingDefinition setting)
        {
            return SettingStore.GetOrNullAsync(setting.Name, Name, null);
        }

        /// <summary>
        /// 获取所有设置值
        /// </summary>
        /// <param name="settings">设置定义数组</param>
        /// <returns>设置值列表</returns>
        public override Task<List<SettingValue>> GetAllAsync(SettingDefinition[] settings)
        {
            return SettingStore.GetAllAsync(settings.Select(x => x.Name).ToArray(), Name, null);
        }
    }
}
