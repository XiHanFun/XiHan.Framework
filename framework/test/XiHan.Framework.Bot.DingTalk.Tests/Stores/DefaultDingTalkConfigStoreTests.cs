// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.DingTalk.Options;
using XiHan.Framework.Bot.DingTalk.Stores;

namespace XiHan.Framework.Bot.DingTalk.Tests.Stores;

/// <summary>
/// 默认钉钉配置存储测试
/// </summary>
/// <remarks>
/// 默认实现是"选项监视器直通"，契约有三条容易被误解的地方，逐条锁死：
/// 一是它永远不返回 null（未配置时返回全默认选项，"未配置"的判定被推给提供者的 AccessToken 校验）；
/// 二是它返回的是监视器缓存的同一个实例，不做防御性拷贝；
/// 三是它没有任何 IO，取消令牌只是形参，不会触发取消。
/// 这里不手写 IOptionsMonitor 替身，直接用真实容器构造，避免替身与真实实现语义偏离。
/// </remarks>
public class DefaultDingTalkConfigStoreTests
{
    /// <summary>
    /// 返回选项监视器的当前值
    /// </summary>
    [Fact]
    public async Task GetAsync_ReturnsMonitorCurrentValue()
    {
        var services = new ServiceCollection();
        services.Configure<DingTalkOptions>(options =>
        {
            options.AccessToken = "access-token-value";
            options.Secret = "SECsecretvalue";
            options.KeyWord = "告警";
        });

        using var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<DingTalkOptions>>();
        var store = new DefaultDingTalkConfigStore(monitor);

        var options = await store.GetAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(options);
        Assert.Same(monitor.CurrentValue, options);
        Assert.Equal("access-token-value", options.AccessToken);
        Assert.Equal("SECsecretvalue", options.Secret);
        Assert.Equal("告警", options.KeyWord);
    }

    /// <summary>
    /// 未写入任何配置时返回全默认选项而不是 null
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenNothingConfigured_ReturnsDefaultOptionsInsteadOfNull()
    {
        var services = new ServiceCollection();
        services.AddOptions();

        using var provider = services.BuildServiceProvider();
        var store = new DefaultDingTalkConfigStore(provider.GetRequiredService<IOptionsMonitor<DingTalkOptions>>());

        var options = await store.GetAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(options);
        Assert.True(options.Enabled);
        Assert.Equal(string.Empty, options.AccessToken);
        Assert.Equal("https://oapi.dingtalk.com/robot/send", options.WebHookUrl);
    }

    /// <summary>
    /// 多次读取返回监视器缓存的同一实例
    /// </summary>
    [Fact]
    public async Task GetAsync_CalledRepeatedly_ReturnsSameCachedInstance()
    {
        var services = new ServiceCollection();
        services.Configure<DingTalkOptions>(options => options.AccessToken = "access-token-value");

        using var provider = services.BuildServiceProvider();
        var store = new DefaultDingTalkConfigStore(provider.GetRequiredService<IOptionsMonitor<DingTalkOptions>>());

        var first = await store.GetAsync(TestContext.Current.CancellationToken);
        var second = await store.GetAsync(TestContext.Current.CancellationToken);

        Assert.Same(first, second);
    }

    /// <summary>
    /// 读取是纯内存操作，任务同步完成
    /// </summary>
    [Fact]
    public async Task GetAsync_CompletesSynchronously()
    {
        var services = new ServiceCollection();
        services.AddOptions();

        using var provider = services.BuildServiceProvider();
        var store = new DefaultDingTalkConfigStore(provider.GetRequiredService<IOptionsMonitor<DingTalkOptions>>());

        var task = store.GetAsync(TestContext.Current.CancellationToken);

        Assert.True(task.IsCompletedSuccessfully);
        Assert.NotNull(await task);
    }

    /// <summary>
    /// 传入已取消的令牌不会抛出，因为默认实现不做任何 IO
    /// </summary>
    [Fact]
    public async Task GetAsync_WithAlreadyCancelledToken_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddOptions();

        using var provider = services.BuildServiceProvider();
        var store = new DefaultDingTalkConfigStore(provider.GetRequiredService<IOptionsMonitor<DingTalkOptions>>());

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var options = await store.GetAsync(cancellation.Token);

        Assert.NotNull(options);
    }
}
