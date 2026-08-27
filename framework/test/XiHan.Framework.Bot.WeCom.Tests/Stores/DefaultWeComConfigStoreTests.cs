// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Bot.WeCom.Abstractions;
using XiHan.Framework.Bot.WeCom.Options;
using XiHan.Framework.Bot.WeCom.Stores;
using XiHan.Framework.Bot.WeCom.Tests.Fakes;

namespace XiHan.Framework.Bot.WeCom.Tests.Stores;

/// <summary>
/// <see cref="DefaultWeComConfigStore"/> 默认配置存储测试
/// </summary>
/// <remarks>
/// 该实现是 <c>IOptionsMonitor.CurrentValue</c> 的薄封装，契约只有两条：
/// 读的是「当前值」而不是构造期快照；以及它是 TryAdd 语义下可被应用层实现顶掉的默认兜底。
/// </remarks>
public class DefaultWeComConfigStoreTests
{
    /// <summary>
    /// 返回监视器的当前值实例
    /// </summary>
    [Fact]
    public async Task GetAsync_ReturnsMonitorCurrentValue()
    {
        var options = new WeComOptions { Key = "k1" };
        var store = new DefaultWeComConfigStore(new FakeWeComOptionsMonitor(options));

        var actual = await store.GetAsync(TestContext.Current.CancellationToken);

        Assert.Same(options, actual);
    }

    /// <summary>
    /// 配置热更新后再次读取拿到新值
    /// </summary>
    /// <remarks>
    /// 若实现缓存了构造期的值，配置中心改了 Key 之后机器人会一直用旧 Key 发包，这条正是防这个。
    /// </remarks>
    [Fact]
    public async Task GetAsync_AfterCurrentValueChanged_ReturnsNewValue()
    {
        var monitor = new FakeWeComOptionsMonitor(new WeComOptions { Key = "old" });
        var store = new DefaultWeComConfigStore(monitor);

        var before = await store.GetAsync(TestContext.Current.CancellationToken);
        monitor.CurrentValue = new WeComOptions { Key = "new" };
        var after = await store.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal("old", before!.Key);
        Assert.Equal("new", after!.Key);
    }

    /// <summary>
    /// 在真实容器中解析时读到 Configure 写入的配置
    /// </summary>
    [Fact]
    public async Task GetAsync_FromRealContainer_ReturnsConfiguredOptions()
    {
        var services = new ServiceCollection();
        services.Configure<WeComOptions>(options =>
        {
            options.Key = "container-key";
            options.WebHookUrl = "https://proxy.internal/webhook/send";
            options.Enabled = false;
        });
        services.AddSingleton<IWeComConfigStore, DefaultWeComConfigStore>();

        await using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IWeComConfigStore>();

        var actual = await store.GetAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(actual);
        Assert.Equal("container-key", actual!.Key);
        Assert.Equal("https://proxy.internal/webhook/send", actual.WebHookUrl);
        Assert.False(actual.Enabled);
    }

    /// <summary>
    /// 未做任何配置时返回选项默认值而不是 null
    /// </summary>
    /// <remarks>
    /// 提供者把 null 当「未配置」处理，默认实现走的是 IOptionsMonitor，永远拿得到默认实例，
    /// 因此真正的「未配置」判定落在 Key 为空这条分支上。
    /// </remarks>
    [Fact]
    public async Task GetAsync_WithoutConfiguration_ReturnsDefaultOptionsInstance()
    {
        var services = new ServiceCollection();
        services.AddOptions<WeComOptions>();
        services.AddSingleton<IWeComConfigStore, DefaultWeComConfigStore>();

        await using var provider = services.BuildServiceProvider();
        var actual = await provider.GetRequiredService<IWeComConfigStore>().GetAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(actual);
        Assert.True(actual!.Enabled);
        Assert.Equal(string.Empty, actual.Key);
    }
}
