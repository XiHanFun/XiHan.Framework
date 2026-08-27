// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.MultiTenancy.Tests.Fakes;
using XiHan.Framework.Settings.Definitions;
using XiHan.Framework.Settings.Providers;

namespace XiHan.Framework.MultiTenancy.Tests;

/// <summary>
/// 租户设置值提供器的测试
/// </summary>
/// <remarks>
/// 提供器自身不做任何取值逻辑，它的全部职责是「用什么键去问设置存储」，
/// 因此断言重点全部落在传给存储的三元组上：提供者名必须固定为 T，
/// 提供者键按「租户名称优先、回退到租户唯一标识、都没有则为 null」的顺序取。
/// 只断言返回值无法区分这几条分支，所以替身把调用参数原样记录下来供断言。
/// </remarks>
public class TenantSettingValueProviderTests
{
    /// <summary>
    /// 提供者名称常量不漂移
    /// </summary>
    /// <remarks>
    /// 这个值会跟着设置一起落库（作为 ProviderName 列），改动即等于历史数据全部失配。
    /// </remarks>
    [Fact]
    public void ProviderName_IsStable()
    {
        Assert.Equal("T", TenantSettingValueProvider.ProviderName);
    }

    /// <summary>
    /// 实例名称取自提供者名称常量
    /// </summary>
    [Fact]
    public void Name_ReturnsProviderName()
    {
        var provider = CreateProvider(new FakeSettingStore(), new FakeCurrentTenant());

        Assert.Equal(TenantSettingValueProvider.ProviderName, provider.Name);
    }

    /// <summary>
    /// 继承自设置值提供器基类并实现提供器契约
    /// </summary>
    [Fact]
    public void Type_DerivesFromSettingValueProvider()
    {
        var provider = CreateProvider(new FakeSettingStore(), new FakeCurrentTenant());

        Assert.IsAssignableFrom<SettingValueProvider>(provider);
        Assert.IsAssignableFrom<ISettingValueProvider>(provider);
    }

    /// <summary>
    /// 有租户名称时用名称作为提供者键
    /// </summary>
    [Fact]
    public async Task GetOrNullAsync_WithTenantName_UsesNameAsProviderKey()
    {
        var store = new FakeSettingStore();
        var currentTenant = new FakeCurrentTenant { Id = 7L, Name = "xihan" };
        var provider = CreateProvider(store, currentTenant);
        store.Seed("App.Theme", TenantSettingValueProvider.ProviderName, "xihan", "dark");

        var value = await provider.GetOrNullAsync(new SettingDefinition("App.Theme"));

        Assert.Equal("dark", value);
        var call = Assert.Single(store.GetOrNullCalls);
        Assert.Equal("App.Theme", call.Name);
        Assert.Equal("T", call.ProviderName);
        Assert.Equal("xihan", call.ProviderKey);
    }

    /// <summary>
    /// 租户名称为空或空白时回退到租户唯一标识
    /// </summary>
    /// <remarks>
    /// 空白字符串是配置绑定里最常见的「看起来有值其实没值」，必须和 null 一样触发回退。
    /// </remarks>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetOrNullAsync_WhenTenantNameBlank_FallsBackToTenantId(string? tenantName)
    {
        var store = new FakeSettingStore();
        var currentTenant = new FakeCurrentTenant { Id = 7L, Name = tenantName };
        var provider = CreateProvider(store, currentTenant);

        await provider.GetOrNullAsync(new SettingDefinition("App.Theme"));

        var call = Assert.Single(store.GetOrNullCalls);
        Assert.Equal("7", call.ProviderKey);
    }

    /// <summary>
    /// 既无租户名称也无租户唯一标识时提供者键为 null
    /// </summary>
    [Fact]
    public async Task GetOrNullAsync_WithoutTenant_UsesNullProviderKey()
    {
        var store = new FakeSettingStore();
        var provider = CreateProvider(store, new FakeCurrentTenant());

        await provider.GetOrNullAsync(new SettingDefinition("App.Theme"));

        var call = Assert.Single(store.GetOrNullCalls);
        Assert.Null(call.ProviderKey);
        Assert.Equal("T", call.ProviderName);
    }

    /// <summary>
    /// 存储中没有对应值时返回 null
    /// </summary>
    [Fact]
    public async Task GetOrNullAsync_WhenValueMissing_ReturnsNull()
    {
        var store = new FakeSettingStore();
        var provider = CreateProvider(store, new FakeCurrentTenant { Id = 7L, Name = "xihan" });

        var value = await provider.GetOrNullAsync(new SettingDefinition("App.Theme"));

        Assert.Null(value);
    }

    /// <summary>
    /// 提供者键在每次调用时重新取，跟随当前租户切换
    /// </summary>
    /// <remarks>
    /// 提供器是瞬时的但仍可能在同一个作用域内被复用；把租户键缓存下来会导致切换租户后仍读到上一个租户的设置。
    /// </remarks>
    [Fact]
    public async Task GetOrNullAsync_AfterTenantSwitch_UsesNewProviderKey()
    {
        var store = new FakeSettingStore();
        var currentTenant = new FakeCurrentTenant { Id = 1L, Name = "tenant-one" };
        var provider = CreateProvider(store, currentTenant);
        var setting = new SettingDefinition("App.Theme");

        await provider.GetOrNullAsync(setting);

        using (currentTenant.Change(2L, "tenant-two"))
        {
            await provider.GetOrNullAsync(setting);
        }

        await provider.GetOrNullAsync(setting);

        Assert.Equal(3, store.GetOrNullCalls.Count);
        Assert.Equal("tenant-one", store.GetOrNullCalls[0].ProviderKey);
        Assert.Equal("tenant-two", store.GetOrNullCalls[1].ProviderKey);
        Assert.Equal("tenant-one", store.GetOrNullCalls[2].ProviderKey);
    }

    /// <summary>
    /// 批量查询按原顺序透传全部设置名称
    /// </summary>
    [Fact]
    public async Task GetAllAsync_PassesEverySettingNameInOrder()
    {
        var store = new FakeSettingStore();
        var provider = CreateProvider(store, new FakeCurrentTenant { Id = 7L, Name = "xihan" });
        store.Seed("App.Theme", TenantSettingValueProvider.ProviderName, "xihan", "dark");

        var values = await provider.GetAllAsync(
        [
            new SettingDefinition("App.Theme"),
            new SettingDefinition("App.Language"),
            new SettingDefinition("App.TimeZone")
        ]);

        var call = Assert.Single(store.GetAllCalls);
        Assert.Equal(3, call.Names.Length);
        Assert.Equal("App.Theme", call.Names[0]);
        Assert.Equal("App.Language", call.Names[1]);
        Assert.Equal("App.TimeZone", call.Names[2]);
        Assert.Equal("T", call.ProviderName);
        Assert.Equal("xihan", call.ProviderKey);

        Assert.Equal(3, values.Count);
        Assert.Equal("App.Theme", values[0].Name);
        Assert.Equal("dark", values[0].Value);
        Assert.Null(values[1].Value);
        Assert.Null(values[2].Value);
    }

    /// <summary>
    /// 批量查询传入空集合时依然只发一次查询且名称数组为空
    /// </summary>
    [Fact]
    public async Task GetAllAsync_WithEmptySettings_QueriesWithEmptyNameArray()
    {
        var store = new FakeSettingStore();
        var provider = CreateProvider(store, new FakeCurrentTenant { Id = 7L, Name = "xihan" });

        var values = await provider.GetAllAsync([]);

        var call = Assert.Single(store.GetAllCalls);
        Assert.Empty(call.Names);
        Assert.Empty(values);
    }

    /// <summary>
    /// 创建租户设置值提供器
    /// </summary>
    /// <param name="settingStore">设置存储替身</param>
    /// <param name="currentTenant">当前租户替身</param>
    /// <returns>租户设置值提供器</returns>
    private static TenantSettingValueProvider CreateProvider(FakeSettingStore settingStore, FakeCurrentTenant currentTenant)
    {
        return new TenantSettingValueProvider(settingStore, currentTenant);
    }
}
