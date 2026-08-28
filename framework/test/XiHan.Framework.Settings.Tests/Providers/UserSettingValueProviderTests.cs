// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Settings.Definitions;
using XiHan.Framework.Settings.Providers;
using XiHan.Framework.Settings.Tests.Fakes;

namespace XiHan.Framework.Settings.Tests.Providers;

/// <summary>
/// 用户设置值提供者测试
/// </summary>
/// <remarks>
/// 用户层是提供者链里唯一带上下文依赖的一环：匿名上下文必须整体短路，一次存储查询都不能发出，
/// 否则会以空 providerKey 命中别人的数据。
/// </remarks>
public class UserSettingValueProviderTests
{
    /// <summary>
    /// 提供者名称常量保持稳定
    /// </summary>
    [Fact]
    public void ProviderName_IsStable()
    {
        Assert.Equal("U", UserSettingValueProvider.ProviderName);
        Assert.Equal("U", new UserSettingValueProvider(new FakeSettingStore(), new FakeCurrentUser()).Name);
    }

    /// <summary>
    /// 匿名上下文下单项读取直接返回 null 且不查询存储
    /// </summary>
    [Fact]
    public async Task GetOrNullAsync_WhenNoCurrentUser_ReturnsNullWithoutQueryingStore()
    {
        var store = new FakeSettingStore();
        store.Seed("Foo", "U", null, "should-not-be-read");
        var provider = new UserSettingValueProvider(store, new FakeCurrentUser());

        var value = await provider.GetOrNullAsync(new SettingDefinition("Foo"));

        Assert.Null(value);
        Assert.Empty(store.GetOrNullCalls);
    }

    /// <summary>
    /// 有用户上下文时以用户标识作为提供者键查询存储
    /// </summary>
    [Fact]
    public async Task GetOrNullAsync_WhenUserPresent_UsesUserIdAsProviderKey()
    {
        var store = new FakeSettingStore();
        store.Seed("Foo", "U", "42", "user-value");
        var provider = new UserSettingValueProvider(store, new FakeCurrentUser(userId: 42));

        var value = await provider.GetOrNullAsync(new SettingDefinition("Foo"));

        Assert.Equal("user-value", value);
        var call = Assert.Single(store.GetOrNullCalls);
        Assert.Equal("Foo", call.Name);
        Assert.Equal("U", call.ProviderName);
        Assert.Equal("42", call.ProviderKey);
    }

    /// <summary>
    /// 用户没有该项设置时返回 null
    /// </summary>
    [Fact]
    public async Task GetOrNullAsync_WhenUserHasNoValue_ReturnsNull()
    {
        var provider = new UserSettingValueProvider(new FakeSettingStore(), new FakeCurrentUser(userId: 42));

        var value = await provider.GetOrNullAsync(new SettingDefinition("Foo", "the-default"));

        Assert.Null(value);
    }

    /// <summary>
    /// 匿名上下文下批量读取给每个设置补一条空值条目，且不查询存储
    /// </summary>
    [Fact]
    public async Task GetAllAsync_WhenNoCurrentUser_ReturnsNullValuedEntriesWithoutQueryingStore()
    {
        var store = new FakeSettingStore();
        var provider = new UserSettingValueProvider(store, new FakeCurrentUser());
        var settings = new[] { new SettingDefinition("Foo", "foo-default"), new SettingDefinition("Bar") };

        var values = await provider.GetAllAsync(settings);

        Assert.Equal(new[] { "Foo", "Bar" }, values.Select(x => x.Name).ToArray());
        Assert.All(values, x => Assert.Null(x.Value));
        Assert.Empty(store.GetAllCalls);
    }

    /// <summary>
    /// 有用户上下文时批量读取带上用户标识查询存储
    /// </summary>
    [Fact]
    public async Task GetAllAsync_WhenUserPresent_QueriesStoreWithUserId()
    {
        var store = new FakeSettingStore();
        store.Seed("Foo", "U", "7", "foo-user-value");
        var provider = new UserSettingValueProvider(store, new FakeCurrentUser(userId: 7));
        var settings = new[] { new SettingDefinition("Foo"), new SettingDefinition("Bar") };

        var values = await provider.GetAllAsync(settings);

        var call = Assert.Single(store.GetAllCalls);
        Assert.Equal(new[] { "Foo", "Bar" }, call.Names);
        Assert.Equal("U", call.ProviderName);
        Assert.Equal("7", call.ProviderKey);
        Assert.Equal(new[] { "foo-user-value", null }, values.Select(x => x.Value).ToArray());
    }

    /// <summary>
    /// 不同用户的提供者键互相隔离
    /// </summary>
    [Fact]
    public async Task GetOrNullAsync_KeepsDifferentUsersIsolated()
    {
        var store = new FakeSettingStore();
        store.Seed("Foo", "U", "1", "value-of-user-1");
        store.Seed("Foo", "U", "2", "value-of-user-2");

        var first = await new UserSettingValueProvider(store, new FakeCurrentUser(userId: 1)).GetOrNullAsync(new SettingDefinition("Foo"));
        var second = await new UserSettingValueProvider(store, new FakeCurrentUser(userId: 2)).GetOrNullAsync(new SettingDefinition("Foo"));

        Assert.Equal("value-of-user-1", first);
        Assert.Equal("value-of-user-2", second);
    }

    /// <summary>
    /// 用户提供者继承自设置值提供者基类
    /// </summary>
    [Fact]
    public void UserSettingValueProvider_DerivesFromSettingValueProvider()
    {
        Assert.IsAssignableFrom<SettingValueProvider>(new UserSettingValueProvider(new FakeSettingStore(), new FakeCurrentUser()));
    }
}
