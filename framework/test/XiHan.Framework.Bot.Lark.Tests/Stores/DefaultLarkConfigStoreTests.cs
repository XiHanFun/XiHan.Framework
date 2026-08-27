// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Lark.Abstractions;
using XiHan.Framework.Bot.Lark.Options;
using XiHan.Framework.Bot.Lark.Stores;

namespace XiHan.Framework.Bot.Lark.Tests.Stores;

/// <summary>
/// 默认飞书配置存储测试
/// </summary>
/// <remarks>
/// 默认实现只是 IOptionsMonitor 的薄包装，契约有三条：永不返回 null、不做 I/O（取消令牌无副作用）、
/// 同一次配置周期内返回同一实例。这里直接用真实的 OptionsMonitor 而不是手写替身，
/// 免得替身把「缓存同一实例」这条真实语义测没了。
/// </remarks>
public class DefaultLarkConfigStoreTests
{
    /// <summary>
    /// 已配置选项时返回配置后的值
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenOptionsConfigured_ReturnsConfiguredValue()
    {
        using var provider = BuildProvider(options =>
        {
            options.AccessToken = "token-1";
            options.Secret = "secret-1";
            options.KeyWord = "告警";
            options.Enabled = false;
        });
        var store = new DefaultLarkConfigStore(provider.GetRequiredService<IOptionsMonitor<LarkOptions>>());

        var options = await store.GetAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(options);
        Assert.Equal("token-1", options.AccessToken);
        Assert.Equal("secret-1", options.Secret);
        Assert.Equal("告警", options.KeyWord);
        Assert.False(options.Enabled);
    }

    /// <summary>
    /// 未注册任何配置委托时返回默认选项而不是 null
    /// </summary>
    /// <remarks>
    /// 接口注释允许返回 null 表示未配置，但默认实现走 IOptionsMonitor，永远拿得到实例；
    /// LarkBotProvider 的「未配置」判定因此落在 Enabled / AccessToken 上，这条语义必须固定。
    /// </remarks>
    [Fact]
    public async Task GetAsync_WhenNothingConfigured_ReturnsDefaultOptions()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        using var provider = services.BuildServiceProvider();
        var store = new DefaultLarkConfigStore(provider.GetRequiredService<IOptionsMonitor<LarkOptions>>());

        var options = await store.GetAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(options);
        Assert.True(options.Enabled);
        Assert.Equal(string.Empty, options.AccessToken);
    }

    /// <summary>
    /// 连续两次获取返回同一实例
    /// </summary>
    [Fact]
    public async Task GetAsync_CalledTwice_ReturnsSameInstance()
    {
        using var provider = BuildProvider(options => options.AccessToken = "token-1");
        var store = new DefaultLarkConfigStore(provider.GetRequiredService<IOptionsMonitor<LarkOptions>>());

        var first = await store.GetAsync(TestContext.Current.CancellationToken);
        var second = await store.GetAsync(TestContext.Current.CancellationToken);

        Assert.Same(first, second);
    }

    /// <summary>
    /// 取消令牌已取消时依然同步完成
    /// </summary>
    /// <remarks>
    /// 默认实现不做 I/O，不应该因为令牌状态改变行为；这条同时守住「不要偷偷加远程读取」。
    /// </remarks>
    [Fact]
    public async Task GetAsync_WhenTokenAlreadyCancelled_StillCompletes()
    {
        using var provider = BuildProvider(options => options.AccessToken = "token-1");
        var store = new DefaultLarkConfigStore(provider.GetRequiredService<IOptionsMonitor<LarkOptions>>());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var options = await store.GetAsync(cts.Token);

        Assert.NotNull(options);
        Assert.Equal("token-1", options.AccessToken);
    }

    /// <summary>
    /// 返回的任务同步完成，不引入异步调度
    /// </summary>
    [Fact]
    public void GetAsync_Always_ReturnsCompletedTask()
    {
        using var provider = BuildProvider(options => options.AccessToken = "token-1");
        var store = new DefaultLarkConfigStore(provider.GetRequiredService<IOptionsMonitor<LarkOptions>>());

        var task = store.GetAsync(CancellationToken.None);

        Assert.True(task.IsCompletedSuccessfully);
    }

    /// <summary>
    /// 默认实现满足配置存储抽象
    /// </summary>
    [Fact]
    public void Store_Always_ImplementsConfigStoreAbstraction()
    {
        Assert.True(typeof(DefaultLarkConfigStore).IsAssignableTo(typeof(ILarkConfigStore)));
    }

    /// <summary>
    /// 构建带飞书选项的服务提供者
    /// </summary>
    private static ServiceProvider BuildProvider(Action<LarkOptions> configure)
    {
        var services = new ServiceCollection();
        services.Configure(configure);
        return services.BuildServiceProvider();
    }
}
