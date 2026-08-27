// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Security.Users;
using XiHan.Framework.Settings.Definitions;
using XiHan.Framework.Settings.Events;
using XiHan.Framework.Settings.Options;
using XiHan.Framework.Settings.Stores;
using XiHan.Framework.Settings.Tests.Fakes;

namespace XiHan.Framework.Settings.Tests;

/// <summary>
/// 设置管理器测试
/// </summary>
/// <remarks>
/// 覆盖四条主链路：
/// 1) 定义解析——运行时手动追加的定义优先于定义提供者汇总表，未定义直接失败；
/// 2) 读取链——按定义自带的提供者顺序取第一个非 null 值并短路，全落空再回退全局存储、最后回退默认值；
/// 3) 写入链——按作用域解析出 (提供者名, 提供者键)，空值走删除、非空走写入，最后广播变更事件；
/// 4) 加密——密钥未配置时 fail-closed 直接拒绝，配置后加解密可往返。
/// 读取侧只用默认的应用级作用域，用户/租户级读取的行为差异见交付报告中的疑似缺陷条目。
/// </remarks>
public class SettingManagerTests
{
    /// <summary>
    /// 读取未定义的设置直接失败
    /// </summary>
    [Fact]
    public async Task GetOrNullAsync_WhenSettingNotDefined_ThrowsXiHanException()
    {
        using var serviceProvider = CreateServiceProvider();
        var manager = CreateManager(new FakeSettingStore(), new FakeSettingDefinitionManager(), serviceProvider);

        var exception = await Assert.ThrowsAsync<XiHanException>(async () => await manager.GetOrNullAsync("Not.Defined"));

        Assert.Contains("Not.Defined", exception.Message);
        Assert.Contains("is not defined", exception.Message);
    }

    /// <summary>
    /// 运行时追加的定义可以立刻被解析
    /// </summary>
    [Fact]
    public async Task AddDefinition_MakesSettingResolvable()
    {
        using var serviceProvider = CreateServiceProvider();
        var manager = CreateManager(new FakeSettingStore(), new FakeSettingDefinitionManager(), serviceProvider);

        manager.AddDefinition(new SettingDefinition("Runtime.Added", "runtime-default"));

        Assert.Equal("runtime-default", await manager.GetOrNullAsync("Runtime.Added"));
    }

    /// <summary>
    /// 同名定义重复追加直接失败
    /// </summary>
    [Fact]
    public void AddDefinition_WhenNameAlreadyAdded_ThrowsXiHanException()
    {
        using var serviceProvider = CreateServiceProvider();
        var manager = CreateManager(new FakeSettingStore(), new FakeSettingDefinitionManager(), serviceProvider);
        manager.AddDefinition(new SettingDefinition("Dup"));

        var exception = Assert.Throws<XiHanException>(() => manager.AddDefinition(new SettingDefinition("Dup")));

        Assert.Contains("Dup", exception.Message);
    }

    /// <summary>
    /// 同名时运行时追加的定义压过定义提供者汇总表里的那一份
    /// </summary>
    [Fact]
    public async Task GetOrNullAsync_PrefersManuallyAddedDefinition()
    {
        using var serviceProvider = CreateServiceProvider();
        var definitionManager = new FakeSettingDefinitionManager(new SettingDefinition("Shared", "from-provider-table"));
        var manager = CreateManager(new FakeSettingStore(), definitionManager, serviceProvider);

        manager.AddDefinition(new SettingDefinition("Shared", "from-runtime"));

        Assert.Equal("from-runtime", await manager.GetOrNullAsync("Shared"));
    }

    /// <summary>
    /// 提供者链取第一个非 null 值并短路，后续提供者不再被调用
    /// </summary>
    [Fact]
    public async Task GetOrNullAsync_TakesFirstNonNullProviderAndShortCircuits()
    {
        var first = new FakeSettingValueProvider("P1", null);
        var second = new FakeSettingValueProvider("P2", "from-p2");
        var third = new FakeSettingValueProvider("P3", "from-p3");
        var definition = new SettingDefinition("Chained", "the-default")
            .AddProvider(first)
            .AddProvider(second)
            .AddProvider(third);
        var store = new FakeSettingStore();
        using var serviceProvider = CreateServiceProvider();
        var manager = CreateManager(store, new FakeSettingDefinitionManager(definition), serviceProvider);

        var value = await manager.GetOrNullAsync("Chained");

        Assert.Equal("from-p2", value);
        Assert.Equal(1, first.GetOrNullCallCount);
        Assert.Equal(1, second.GetOrNullCallCount);
        Assert.Equal(0, third.GetOrNullCallCount);
        Assert.Empty(store.GetOrNullCalls);
    }

    /// <summary>
    /// 提供者返回空串同样算命中，不再继续回退
    /// </summary>
    /// <remarks>
    /// 空串与 null 在这里语义不同：null 表示"该层没有配置"，空串表示"该层显式配置成了空"。
    /// </remarks>
    [Fact]
    public async Task GetOrNullAsync_WhenProviderReturnsEmptyString_DoesNotFallBack()
    {
        var definition = new SettingDefinition("Emptied", "the-default")
            .AddProvider(new FakeSettingValueProvider("P1", string.Empty));
        var store = new FakeSettingStore();
        store.Seed("Emptied", "G", null, "from-store");
        using var serviceProvider = CreateServiceProvider();
        var manager = CreateManager(store, new FakeSettingDefinitionManager(definition), serviceProvider);

        var value = await manager.GetOrNullAsync("Emptied");

        Assert.Equal(string.Empty, value);
        Assert.Empty(store.GetOrNullCalls);
    }

    /// <summary>
    /// 提供者全部落空时回退到全局存储
    /// </summary>
    [Fact]
    public async Task GetOrNullAsync_WhenAllProvidersReturnNull_FallsBackToGlobalStore()
    {
        var provider = new FakeSettingValueProvider("P1", null);
        var definition = new SettingDefinition("Fallback", "the-default").AddProvider(provider);
        var store = new FakeSettingStore();
        store.Seed("Fallback", "G", null, "from-store");
        using var serviceProvider = CreateServiceProvider();
        var manager = CreateManager(store, new FakeSettingDefinitionManager(definition), serviceProvider);

        var value = await manager.GetOrNullAsync("Fallback");

        Assert.Equal("from-store", value);
        var call = Assert.Single(store.GetOrNullCalls);
        Assert.Equal("Fallback", call.Name);
        Assert.Equal("G", call.ProviderName);
        Assert.Null(call.ProviderKey);
    }

    /// <summary>
    /// 全局存储也没有时回退到定义的默认值
    /// </summary>
    [Fact]
    public async Task GetOrNullAsync_WhenStoreHasNoValue_FallsBackToDefaultValue()
    {
        var definition = new SettingDefinition("Fallback", "the-default");
        using var serviceProvider = CreateServiceProvider();
        var manager = CreateManager(new FakeSettingStore(), new FakeSettingDefinitionManager(definition), serviceProvider);

        Assert.Equal("the-default", await manager.GetOrNullAsync("Fallback"));
    }

    /// <summary>
    /// 存储中的值优先于定义的默认值
    /// </summary>
    [Fact]
    public async Task GetOrNullAsync_StoreValueWinsOverDefaultValue()
    {
        var definition = new SettingDefinition("Overridden", "the-default");
        var store = new FakeSettingStore();
        store.Seed("Overridden", "G", null, "from-store");
        using var serviceProvider = CreateServiceProvider();
        var manager = CreateManager(store, new FakeSettingDefinitionManager(definition), serviceProvider);

        Assert.Equal("from-store", await manager.GetOrNullAsync("Overridden"));
    }

    /// <summary>
    /// 提供者、存储、默认值三层都落空时返回 null
    /// </summary>
    [Fact]
    public async Task GetOrNullAsync_WhenEverySourceIsEmpty_ReturnsNull()
    {
        var definition = new SettingDefinition("Nothing");
        using var serviceProvider = CreateServiceProvider();
        var manager = CreateManager(new FakeSettingStore(), new FakeSettingDefinitionManager(definition), serviceProvider);

        Assert.Null(await manager.GetOrNullAsync("Nothing"));
    }

    /// <summary>
    /// 写入未定义的设置直接失败
    /// </summary>
    [Fact]
    public async Task SetValueAsync_WhenSettingNotDefined_ThrowsXiHanException()
    {
        var store = new FakeSettingStore();
        using var serviceProvider = CreateServiceProvider();
        var manager = CreateManager(store, new FakeSettingDefinitionManager(), serviceProvider);

        var exception = await Assert.ThrowsAsync<XiHanException>(async () => await manager.SetValueAsync("Not.Defined", "value"));

        Assert.Contains("Not.Defined", exception.Message);
        Assert.Empty(store.SetCalls);
    }

    /// <summary>
    /// 校验函数拒绝时抛异常，且不落存储、不广播事件
    /// </summary>
    [Fact]
    public async Task SetValueAsync_WhenValidatorRejects_ThrowsWithoutWritingOrRaisingEvent()
    {
        var definition = new SettingDefinition("Validated", validator: value => value == "ok");
        var store = new FakeSettingStore();
        using var serviceProvider = CreateServiceProvider();
        var manager = CreateManager(store, new FakeSettingDefinitionManager(definition), serviceProvider);
        var raised = 0;
        manager.OnSettingChanged += (_, _) => raised++;

        var exception = await Assert.ThrowsAsync<XiHanException>(async () => await manager.SetValueAsync("Validated", "bad"));

        Assert.Contains("Validated", exception.Message);
        Assert.Empty(store.SetCalls);
        Assert.Empty(store.DeleteCalls);
        Assert.Equal(0, raised);
    }

    /// <summary>
    /// 校验函数通过时正常写入
    /// </summary>
    [Fact]
    public async Task SetValueAsync_WhenValidatorAccepts_WritesValue()
    {
        var definition = new SettingDefinition("Validated", validator: value => value == "ok");
        var store = new FakeSettingStore();
        using var serviceProvider = CreateServiceProvider();
        var manager = CreateManager(store, new FakeSettingDefinitionManager(definition), serviceProvider);

        await manager.SetValueAsync("Validated", "ok");

        var call = Assert.Single(store.SetCalls);
        Assert.Equal("ok", call.Value);
    }

    /// <summary>
    /// 应用级写入落到全局提供者且不带提供者键
    /// </summary>
    [Fact]
    public async Task SetValueAsync_WithApplicationScope_WritesToGlobalProvider()
    {
        var store = new FakeSettingStore();
        using var serviceProvider = CreateServiceProvider(new FakeCurrentUser(userId: 42, tenantId: 9));
        var manager = CreateManager(store, new FakeSettingDefinitionManager(new SettingDefinition("Plain")), serviceProvider);

        await manager.SetValueAsync("Plain", "value", SettingScope.Application);

        var call = Assert.Single(store.SetCalls);
        Assert.Equal("Plain", call.Name);
        Assert.Equal("value", call.Value);
        Assert.Equal("G", call.ProviderName);
        Assert.Null(call.ProviderKey);
    }

    /// <summary>
    /// 用户级与会话级写入都落到用户提供者，提供者键取当前用户标识
    /// </summary>
    /// <param name="scope">作用域</param>
    [Theory]
    [InlineData(SettingScope.User)]
    [InlineData(SettingScope.Session)]
    public async Task SetValueAsync_WithUserOrSessionScope_WritesToUserProviderKeyedByUserId(SettingScope scope)
    {
        var store = new FakeSettingStore();
        using var serviceProvider = CreateServiceProvider(new FakeCurrentUser(userId: 42, tenantId: 9));
        var manager = CreateManager(store, new FakeSettingDefinitionManager(new SettingDefinition("Plain")), serviceProvider);

        await manager.SetValueAsync("Plain", "value", scope);

        var call = Assert.Single(store.SetCalls);
        Assert.Equal("U", call.ProviderName);
        Assert.Equal("42", call.ProviderKey);
    }

    /// <summary>
    /// 租户级写入落到租户提供者，提供者键取当前租户标识
    /// </summary>
    [Fact]
    public async Task SetValueAsync_WithTenantScope_WritesToTenantProviderKeyedByTenantId()
    {
        var store = new FakeSettingStore();
        using var serviceProvider = CreateServiceProvider(new FakeCurrentUser(userId: 42, tenantId: 9));
        var manager = CreateManager(store, new FakeSettingDefinitionManager(new SettingDefinition("Plain")), serviceProvider);

        await manager.SetValueAsync("Plain", "value", SettingScope.Tenant);

        var call = Assert.Single(store.SetCalls);
        Assert.Equal("T", call.ProviderName);
        Assert.Equal("9", call.ProviderKey);
    }

    /// <summary>
    /// 无用户上下文时拒绝写入用户级与会话级设置
    /// </summary>
    /// <param name="scope">作用域</param>
    [Theory]
    [InlineData(SettingScope.User)]
    [InlineData(SettingScope.Session)]
    public async Task SetValueAsync_WithUserOrSessionScope_WhenNoCurrentUser_Throws(SettingScope scope)
    {
        var store = new FakeSettingStore();
        using var serviceProvider = CreateServiceProvider();
        var manager = CreateManager(store, new FakeSettingDefinitionManager(new SettingDefinition("Plain")), serviceProvider);

        var exception = await Assert.ThrowsAsync<XiHanException>(async () => await manager.SetValueAsync("Plain", "value", scope));

        Assert.Contains("用户", exception.Message);
        Assert.Empty(store.SetCalls);
    }

    /// <summary>
    /// 无租户上下文时拒绝写入租户级设置
    /// </summary>
    [Fact]
    public async Task SetValueAsync_WithTenantScope_WhenNoTenant_Throws()
    {
        var store = new FakeSettingStore();
        using var serviceProvider = CreateServiceProvider(new FakeCurrentUser(userId: 42));
        var manager = CreateManager(store, new FakeSettingDefinitionManager(new SettingDefinition("Plain")), serviceProvider);

        var exception = await Assert.ThrowsAsync<XiHanException>(async () => await manager.SetValueAsync("Plain", "value", SettingScope.Tenant));

        Assert.Contains("租户", exception.Message);
        Assert.Empty(store.SetCalls);
    }

    /// <summary>
    /// 空白值等价于清除该设置，走删除而不是写入
    /// </summary>
    /// <param name="value">待写入的值</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetValueAsync_WhenValueIsNullOrWhiteSpace_DeletesInsteadOfWriting(string? value)
    {
        var store = new FakeSettingStore();
        using var serviceProvider = CreateServiceProvider();
        var manager = CreateManager(store, new FakeSettingDefinitionManager(new SettingDefinition("Plain")), serviceProvider);

        await manager.SetValueAsync("Plain", value);

        Assert.Empty(store.SetCalls);
        var call = Assert.Single(store.DeleteCalls);
        Assert.Equal("Plain", call.Name);
        Assert.Equal("G", call.ProviderName);
        Assert.Null(call.ProviderKey);
    }

    /// <summary>
    /// 写入成功后广播变更事件，携带名称、作用域与新值
    /// </summary>
    [Fact]
    public async Task SetValueAsync_RaisesSettingChangedEventWithSenderAndPayload()
    {
        var store = new FakeSettingStore();
        using var serviceProvider = CreateServiceProvider();
        var manager = CreateManager(store, new FakeSettingDefinitionManager(new SettingDefinition("Plain")), serviceProvider);
        object? sender = null;
        SettingChangedEventArgs? captured = null;
        manager.OnSettingChanged += (eventSender, args) =>
        {
            sender = eventSender;
            captured = args;
        };

        await manager.SetValueAsync("Plain", "value", SettingScope.Application);

        Assert.Same(manager, sender);
        Assert.NotNull(captured);
        Assert.Equal("Plain", captured!.Name);
        Assert.Equal(SettingScope.Application, captured.Scope);
        Assert.Equal("value", captured.NewValue);
    }

    /// <summary>
    /// 清除设置同样广播变更事件
    /// </summary>
    [Fact]
    public async Task SetValueAsync_WhenValueCleared_StillRaisesSettingChangedEvent()
    {
        var store = new FakeSettingStore();
        using var serviceProvider = CreateServiceProvider();
        var manager = CreateManager(store, new FakeSettingDefinitionManager(new SettingDefinition("Plain")), serviceProvider);
        SettingChangedEventArgs? captured = null;
        manager.OnSettingChanged += (_, args) => captured = args;

        await manager.SetValueAsync("Plain", null);

        Assert.NotNull(captured);
        Assert.Null(captured!.NewValue);
    }

    /// <summary>
    /// 加密设置写入后落库的是密文，再读回来是明文
    /// </summary>
    [Fact]
    public async Task SetValueAsync_ThenGetOrNullAsync_RoundTripsEncryptedValue()
    {
        var definition = new SettingDefinition("Secret", isEncrypted: true);
        var store = new FakeSettingStore();
        using var serviceProvider = CreateServiceProvider();
        var manager = CreateManager(store, new FakeSettingDefinitionManager(definition), serviceProvider, aesKey: "unit-test-aes-key");

        await manager.SetValueAsync("Secret", "p@ssw0rd");

        var persisted = store.Values[FakeSettingStore.BuildKey("Secret", "G", null)];
        Assert.NotNull(persisted);
        Assert.NotEqual("p@ssw0rd", persisted);
        Assert.Equal("p@ssw0rd", await manager.GetOrNullAsync("Secret"));
    }

    /// <summary>
    /// 未标记加密的设置原样落库
    /// </summary>
    [Fact]
    public async Task SetValueAsync_WhenNotEncrypted_PersistsPlainText()
    {
        var store = new FakeSettingStore();
        using var serviceProvider = CreateServiceProvider();
        var manager = CreateManager(store, new FakeSettingDefinitionManager(new SettingDefinition("Plain")), serviceProvider, aesKey: "unit-test-aes-key");

        await manager.SetValueAsync("Plain", "p@ssw0rd");

        Assert.Equal("p@ssw0rd", store.Values[FakeSettingStore.BuildKey("Plain", "G", null)]);
    }

    /// <summary>
    /// 加密设置在未配置密钥时拒绝写入，并在消息里指明配置节
    /// </summary>
    /// <remarks>
    /// 这是刻意的 fail-closed：绝不能退回内置占位密钥，否则密文可被任何人解开。
    /// </remarks>
    [Fact]
    public async Task SetValueAsync_WhenEncryptedAndAesKeyMissing_ThrowsXiHanException()
    {
        var definition = new SettingDefinition("Secret", isEncrypted: true);
        var store = new FakeSettingStore();
        using var serviceProvider = CreateServiceProvider();
        var manager = CreateManager(store, new FakeSettingDefinitionManager(definition), serviceProvider);

        var exception = await Assert.ThrowsAsync<XiHanException>(async () => await manager.SetValueAsync("Secret", "p@ssw0rd"));

        Assert.Contains(XiHanAesOptions.SectionName, exception.Message);
        Assert.Empty(store.SetCalls);
    }

    /// <summary>
    /// 加密设置在未配置密钥时拒绝读取
    /// </summary>
    [Fact]
    public async Task GetOrNullAsync_WhenEncryptedAndAesKeyMissing_ThrowsXiHanException()
    {
        var definition = new SettingDefinition("Secret", isEncrypted: true);
        var store = new FakeSettingStore();
        store.Seed("Secret", "G", null, "any-cipher-text");
        using var serviceProvider = CreateServiceProvider();
        var manager = CreateManager(store, new FakeSettingDefinitionManager(definition), serviceProvider);

        var exception = await Assert.ThrowsAsync<XiHanException>(async () => await manager.GetOrNullAsync("Secret"));

        Assert.Contains(XiHanAesOptions.SectionName, exception.Message);
    }

    /// <summary>
    /// 清除加密设置时不触发加密，因此没有密钥也能安全清除
    /// </summary>
    [Fact]
    public async Task SetValueAsync_WhenEncryptedValueIsEmpty_SkipsEncryptionAndDeletes()
    {
        var definition = new SettingDefinition("Secret", isEncrypted: true);
        var store = new FakeSettingStore();
        using var serviceProvider = CreateServiceProvider();
        var manager = CreateManager(store, new FakeSettingDefinitionManager(definition), serviceProvider);

        await manager.SetValueAsync("Secret", string.Empty);

        Assert.Empty(store.SetCalls);
        Assert.Single(store.DeleteCalls);
    }

    /// <summary>
    /// 加密设置在存储为空时回退到定义的默认值，且不做解密
    /// </summary>
    [Fact]
    public async Task GetOrNullAsync_WhenEncryptedAndNothingStored_ReturnsNullWithoutDecrypting()
    {
        var definition = new SettingDefinition("Secret", isEncrypted: true);
        using var serviceProvider = CreateServiceProvider();
        var manager = CreateManager(new FakeSettingStore(), new FakeSettingDefinitionManager(definition), serviceProvider);

        Assert.Null(await manager.GetOrNullAsync("Secret"));
    }

    /// <summary>
    /// 分组查询只返回该分组下的定义，且合并了运行时追加的定义
    /// </summary>
    [Fact]
    public void GetGroupSettings_ReturnsOnlyDefinitionsInThatGroup()
    {
        var definitionManager = new FakeSettingDefinitionManager(
            new SettingDefinition("Alpha", group: "GroupA"),
            new SettingDefinition("Beta", group: "GroupB"));
        using var serviceProvider = CreateServiceProvider();
        var manager = CreateManager(new FakeSettingStore(), definitionManager, serviceProvider);
        manager.AddDefinition(new SettingDefinition("Gamma", group: "GroupA"));

        var groupA = manager.GetGroupSettings("GroupA").ToArray();

        Assert.Equal(2, groupA.Length);
        Assert.Contains(groupA, x => x.Name == "Alpha");
        Assert.Contains(groupA, x => x.Name == "Gamma");
        Assert.DoesNotContain(groupA, x => x.Name == "Beta");
    }

    /// <summary>
    /// 分组不存在时返回空集合
    /// </summary>
    [Fact]
    public void GetGroupSettings_WhenGroupUnknown_ReturnsEmpty()
    {
        var definitionManager = new FakeSettingDefinitionManager(new SettingDefinition("Alpha", group: "GroupA"));
        using var serviceProvider = CreateServiceProvider();
        var manager = CreateManager(new FakeSettingStore(), definitionManager, serviceProvider);

        Assert.Empty(manager.GetGroupSettings("Missing"));
    }

    /// <summary>
    /// 读取全部设置值时合并两类定义，同名以运行时追加的为准
    /// </summary>
    [Fact]
    public async Task GetAllValuesAsync_MergesDefinitionsAndPrefersManualOnConflict()
    {
        var definitionManager = new FakeSettingDefinitionManager(
            new SettingDefinition("Alpha", "alpha-default"),
            new SettingDefinition("Shared", "from-provider-table"));
        using var serviceProvider = CreateServiceProvider();
        var manager = CreateManager(new FakeSettingStore(), definitionManager, serviceProvider);
        manager.AddDefinition(new SettingDefinition("Shared", "from-runtime"));
        manager.AddDefinition(new SettingDefinition("Gamma", "gamma-default"));

        var values = await manager.GetAllValuesAsync(SettingScope.Application);

        Assert.Equal(3, values.Count);
        Assert.Equal("alpha-default", values.Single(x => x.Name == "Alpha").Value);
        Assert.Equal("from-runtime", values.Single(x => x.Name == "Shared").Value);
        Assert.Equal("gamma-default", values.Single(x => x.Name == "Gamma").Value);
    }

    /// <summary>
    /// 没有任何定义时读取全部设置值返回空列表
    /// </summary>
    [Fact]
    public async Task GetAllValuesAsync_WhenNoDefinitions_ReturnsEmptyList()
    {
        using var serviceProvider = CreateServiceProvider();
        var manager = CreateManager(new FakeSettingStore(), new FakeSettingDefinitionManager(), serviceProvider);

        Assert.Empty(await manager.GetAllValuesAsync(SettingScope.Application));
    }

    /// <summary>
    /// 设置管理器实现管理器接口并按作用域依赖登记
    /// </summary>
    [Fact]
    public void SettingManager_IsScopedSettingManager()
    {
        Assert.True(typeof(ISettingManager).IsAssignableFrom(typeof(SettingManager)));
        Assert.True(typeof(IScopedDependency).IsAssignableFrom(typeof(SettingManager)));
    }

    /// <summary>
    /// 构造设置管理器
    /// </summary>
    /// <param name="store">设置存储</param>
    /// <param name="definitionManager">设置定义管理器</param>
    /// <param name="serviceProvider">服务提供者</param>
    /// <param name="aesKey">加密密钥，空串表示未配置</param>
    /// <returns>设置管理器</returns>
    private static SettingManager CreateManager(
        FakeSettingStore store,
        ISettingDefinitionManager definitionManager,
        IServiceProvider serviceProvider,
        string aesKey = "")
    {
        return new SettingManager(
            NullLogger<SettingManager>.Instance,
            store,
            serviceProvider,
            definitionManager,
            new OptionsWrapper<XiHanAesOptions>(new XiHanAesOptions { Key = aesKey }));
    }

    /// <summary>
    /// 构造只含当前用户的最小容器
    /// </summary>
    /// <param name="currentUser">当前用户，null 表示容器里没有用户上下文</param>
    /// <returns>服务提供者</returns>
    private static ServiceProvider CreateServiceProvider(ICurrentUser? currentUser = null)
    {
        var services = new ServiceCollection();
        if (currentUser is not null)
        {
            services.AddSingleton(currentUser);
        }

        return services.BuildServiceProvider();
    }
}
