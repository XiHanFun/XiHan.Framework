// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Settings.Definitions;
using XiHan.Framework.Settings.Providers;
using XiHan.Framework.Settings.Tests.Fakes;

namespace XiHan.Framework.Settings.Tests.Providers;

/// <summary>
/// 默认值设置值提供者测试
/// </summary>
/// <remarks>
/// 这是提供者链的兜底层：只读设置定义自带的默认值，任何情况下都不应该去碰设置存储。
/// </remarks>
public class DefaultValueSettingValueProviderTests
{
    /// <summary>
    /// 提供者名称常量保持稳定
    /// </summary>
    /// <remarks>
    /// 提供者名会作为存储层的 providerName 列落库，改动等于让历史数据全部失联。
    /// </remarks>
    [Fact]
    public void ProviderName_IsStable()
    {
        Assert.Equal("D", DefaultValueSettingValueProvider.ProviderName);
        Assert.Equal("D", new DefaultValueSettingValueProvider(new FakeSettingStore()).Name);
    }

    /// <summary>
    /// 单项读取直接返回设置定义的默认值
    /// </summary>
    [Fact]
    public async Task GetOrNullAsync_ReturnsDefinitionDefaultValue()
    {
        var provider = new DefaultValueSettingValueProvider(new FakeSettingStore());

        var value = await provider.GetOrNullAsync(new SettingDefinition("Foo", "the-default"));

        Assert.Equal("the-default", value);
    }

    /// <summary>
    /// 定义没有默认值时返回 null，交由上层继续兜底
    /// </summary>
    [Fact]
    public async Task GetOrNullAsync_WhenDefinitionHasNoDefaultValue_ReturnsNull()
    {
        var provider = new DefaultValueSettingValueProvider(new FakeSettingStore());

        var value = await provider.GetOrNullAsync(new SettingDefinition("Foo"));

        Assert.Null(value);
    }

    /// <summary>
    /// 读取默认值不会触碰设置存储
    /// </summary>
    [Fact]
    public async Task GetOrNullAsync_DoesNotTouchSettingStore()
    {
        var store = new FakeSettingStore();
        var provider = new DefaultValueSettingValueProvider(store);

        await provider.GetOrNullAsync(new SettingDefinition("Foo", "the-default"));

        Assert.Empty(store.GetOrNullCalls);
        Assert.Empty(store.GetAllCalls);
    }

    /// <summary>
    /// 批量读取逐条映射为"名称 + 默认值"，顺序与入参一致
    /// </summary>
    [Fact]
    public async Task GetAllAsync_MapsEveryDefinitionToItsDefaultValue()
    {
        var store = new FakeSettingStore();
        var provider = new DefaultValueSettingValueProvider(store);
        var settings = new[]
        {
            new SettingDefinition("Foo", "foo-default"),
            new SettingDefinition("Bar"),
            new SettingDefinition("Baz", "baz-default")
        };

        var values = await provider.GetAllAsync(settings);

        Assert.Equal(new[] { "Foo", "Bar", "Baz" }, values.Select(x => x.Name).ToArray());
        Assert.Equal(new[] { "foo-default", null, "baz-default" }, values.Select(x => x.Value).ToArray());
        Assert.Empty(store.GetAllCalls);
    }

    /// <summary>
    /// 入参为空数组时返回空列表
    /// </summary>
    [Fact]
    public async Task GetAllAsync_WhenNoSettings_ReturnsEmptyList()
    {
        var provider = new DefaultValueSettingValueProvider(new FakeSettingStore());

        var values = await provider.GetAllAsync([]);

        Assert.Empty(values);
    }
}
