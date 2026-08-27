// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Extensions;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.WeCom.Abstractions;
using XiHan.Framework.Bot.WeCom.Extensions;
using XiHan.Framework.Bot.WeCom.Messaging;
using XiHan.Framework.Bot.WeCom.Options;
using XiHan.Framework.Bot.WeCom.Stores;
using XiHan.Framework.Bot.WeCom.Tests.Fakes;

namespace XiHan.Framework.Bot.WeCom.Tests.Extensions;

/// <summary>
/// <see cref="BotBuilderWeComExtensions"/> 构建器扩展测试
/// </summary>
/// <remarks>
/// UseWeCom 是链式装配的入口，契约有四条：返回同一个构建器以便继续链式调用、
/// 写入选项、以 TryAddEnumerable 注册提供者、以 TryAdd 注册默认配置存储（应用层实现可顶掉）。
/// </remarks>
public class BotBuilderWeComExtensionsTests
{
    /// <summary>
    /// 返回同一个构建器实例以支持链式调用
    /// </summary>
    [Fact]
    public void UseWeCom_ReturnsSameBuilder()
    {
        var builder = new BotBuilder(new ServiceCollection());

        var returned = builder.UseWeCom(options => options.Key = "k");

        Assert.Same(builder, returned);
    }

    /// <summary>
    /// 注册企业微信提供者与默认配置存储
    /// </summary>
    [Fact]
    public void UseWeCom_RegistersProviderAndDefaultConfigStore()
    {
        var services = new ServiceCollection();
        new BotBuilder(services).UseWeCom(options => options.Key = "k");

        using var provider = services.BuildServiceProvider();

        Assert.IsType<DefaultWeComConfigStore>(provider.GetRequiredService<IWeComConfigStore>());
        var botProvider = Assert.IsType<WeComBotProvider>(provider.GetServices<IBotProvider>().Single());
        Assert.Equal("WeCom", botProvider.Name);
    }

    /// <summary>
    /// 配置委托写入选项系统
    /// </summary>
    [Fact]
    public void UseWeCom_AppliesConfigureDelegate()
    {
        var services = new ServiceCollection();
        new BotBuilder(services).UseWeCom(options =>
        {
            options.Key = "builder-key";
            options.UploadUrl = "https://proxy.internal/webhook/upload_media";
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<WeComOptions>>().Value;

        Assert.Equal("builder-key", options.Key);
        Assert.Equal("https://proxy.internal/webhook/upload_media", options.UploadUrl);
    }

    /// <summary>
    /// 重复调用不会注册出第二个企业微信提供者
    /// </summary>
    [Fact]
    public void UseWeCom_CalledTwice_DoesNotDuplicateProvider()
    {
        var services = new ServiceCollection();
        var builder = new BotBuilder(services);

        builder.UseWeCom(options => options.Key = "k1");
        builder.UseWeCom(options => options.Key = "k2");

        using var provider = services.BuildServiceProvider();

        Assert.Single(provider.GetServices<IBotProvider>());
    }

    /// <summary>
    /// 应用层已注册的配置存储不会被默认实现覆盖
    /// </summary>
    [Fact]
    public void UseWeCom_WhenCustomConfigStoreRegistered_KeepsCustomImplementation()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWeComConfigStore>(new FakeWeComConfigStore(new WeComOptions()));

        new BotBuilder(services).UseWeCom(options => options.Key = "k");

        using var provider = services.BuildServiceProvider();

        Assert.IsType<FakeWeComConfigStore>(provider.GetRequiredService<IWeComConfigStore>());
    }

    /// <summary>
    /// 构建器为空时抛出参数空异常
    /// </summary>
    [Fact]
    public void UseWeCom_WhenBuilderIsNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => BotBuilderWeComExtensions.UseWeCom(null!, _ => { }));

        Assert.Equal("builder", exception.ParamName);
    }

    /// <summary>
    /// 配置委托为空时抛出参数空异常
    /// </summary>
    [Fact]
    public void UseWeCom_WhenConfigureIsNull_ThrowsArgumentNullException()
    {
        var builder = new BotBuilder(new ServiceCollection());

        var exception = Assert.Throws<ArgumentNullException>(() => builder.UseWeCom(null!));

        Assert.Equal("configure", exception.ParamName);
    }
}
