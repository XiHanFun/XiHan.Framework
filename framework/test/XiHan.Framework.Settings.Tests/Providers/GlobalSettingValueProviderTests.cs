// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Settings.Definitions;
using XiHan.Framework.Settings.Providers;
using XiHan.Framework.Settings.Tests.Fakes;

namespace XiHan.Framework.Settings.Tests.Providers;

/// <summary>
/// 全局设置值提供者测试
/// </summary>
/// <remarks>
/// 全局层不带提供者键，读取必须以 (设置名, "G", null) 这组固定参数打到存储层，
/// 这组参数与设置管理器写入应用级设置时使用的完全一致，读写必须对齐。
/// </remarks>
public class GlobalSettingValueProviderTests
{
    /// <summary>
    /// 提供者名称常量保持稳定
    /// </summary>
    [Fact]
    public void ProviderName_IsStable()
    {
        Assert.Equal("G", GlobalSettingValueProvider.ProviderName);
        Assert.Equal("G", new GlobalSettingValueProvider(new FakeSettingStore()).Name);
    }

    /// <summary>
    /// 单项读取以全局提供者名与空提供者键查询存储
    /// </summary>
    [Fact]
    public async Task GetOrNullAsync_QueriesStoreWithGlobalProviderAndNullKey()
    {
        var store = new FakeSettingStore();
        store.Seed("Foo", "G", null, "global-value");
        var provider = new GlobalSettingValueProvider(store);

        var value = await provider.GetOrNullAsync(new SettingDefinition("Foo", "the-default"));

        Assert.Equal("global-value", value);
        var call = Assert.Single(store.GetOrNullCalls);
        Assert.Equal("Foo", call.Name);
        Assert.Equal("G", call.ProviderName);
        Assert.Null(call.ProviderKey);
    }

    /// <summary>
    /// 存储层没有该项时返回 null，不回落到定义的默认值
    /// </summary>
    [Fact]
    public async Task GetOrNullAsync_WhenStoreHasNoValue_ReturnsNull()
    {
        var provider = new GlobalSettingValueProvider(new FakeSettingStore());

        var value = await provider.GetOrNullAsync(new SettingDefinition("Foo", "the-default"));

        Assert.Null(value);
    }

    /// <summary>
    /// 批量读取把全部设置名一次性传给存储层
    /// </summary>
    [Fact]
    public async Task GetAllAsync_PassesEverySettingNameToStore()
    {
        var store = new FakeSettingStore();
        store.Seed("Foo", "G", null, "foo-value");
        var provider = new GlobalSettingValueProvider(store);
        var settings = new[] { new SettingDefinition("Foo"), new SettingDefinition("Bar") };

        var values = await provider.GetAllAsync(settings);

        var call = Assert.Single(store.GetAllCalls);
        Assert.Equal(new[] { "Foo", "Bar" }, call.Names);
        Assert.Equal("G", call.ProviderName);
        Assert.Null(call.ProviderKey);
        Assert.Equal(new[] { "Foo", "Bar" }, values.Select(x => x.Name).ToArray());
        Assert.Equal(new[] { "foo-value", null }, values.Select(x => x.Value).ToArray());
    }

    /// <summary>
    /// 全局提供者继承自设置值提供者基类
    /// </summary>
    [Fact]
    public void GlobalSettingValueProvider_DerivesFromSettingValueProvider()
    {
        Assert.IsAssignableFrom<SettingValueProvider>(new GlobalSettingValueProvider(new FakeSettingStore()));
    }
}
