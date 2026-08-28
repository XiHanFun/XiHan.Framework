// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Enums;
using XiHan.Framework.Bot.Extensions;
using XiHan.Framework.Bot.Options;
using XiHan.Framework.Bot.Template;

namespace XiHan.Framework.Bot.Tests.Extensions;

/// <summary>
/// <see cref="BotBuilder"/> 测试
/// </summary>
/// <remarks>
/// 构建器只是把配置动作排进 <c>IServiceCollection</c>，真正生效要等选项被解析出来，
/// 所以每个用例都走一遍真实容器再取 <c>IOptions&lt;XiHanBotOptions&gt;</c> 断言，而不是直接查内部状态。
/// </remarks>
public class BotBuilderTests
{
    /// <summary>
    /// 构建器持有传入的服务集合
    /// </summary>
    [Fact]
    public void Services_IsTheGivenCollection()
    {
        var services = new ServiceCollection();

        Assert.Same(services, new BotBuilder(services).Services);
    }

    /// <summary>
    /// 各配置方法返回自身以支持链式调用
    /// </summary>
    [Fact]
    public void FluentMethods_ReturnSameInstance()
    {
        var builder = new BotBuilder(new ServiceCollection());

        Assert.Same(builder, builder.Configure(_ => { }));
        Assert.Same(builder, builder.AddChannel("ops", "DingTalk"));
        Assert.Same(builder, builder.AddTemplate(new BotTemplate { Name = "alert", Content = "x" }));
    }

    /// <summary>
    /// Configure 的改动会体现在解析出来的选项上
    /// </summary>
    [Fact]
    public void Configure_AppliesToResolvedOptions()
    {
        var services = new ServiceCollection();

        new BotBuilder(services).Configure(options =>
        {
            options.DefaultStrategy = BotStrategyNames.Failover;
            options.RetryCount = 7;
            options.ThrowWhenNoProvider = true;
        });

        var resolved = Resolve(services);

        Assert.Equal(BotStrategyNames.Failover, resolved.DefaultStrategy);
        Assert.Equal(7, resolved.RetryCount);
        Assert.True(resolved.ThrowWhenNoProvider);
    }

    /// <summary>
    /// 多次 Configure 按注册顺序叠加
    /// </summary>
    [Fact]
    public void Configure_CalledTwice_AppliesInOrder()
    {
        var services = new ServiceCollection();

        new BotBuilder(services)
            .Configure(options => options.RetryCount = 2)
            .Configure(options => options.RetryCount = 9);

        Assert.Equal(9, Resolve(services).RetryCount);
    }

    /// <summary>
    /// 添加渠道后可在选项里按名称取回
    /// </summary>
    [Fact]
    public void AddChannel_RegistersChannel()
    {
        var services = new ServiceCollection();

        new BotBuilder(services).AddChannel("ops", BotProviderNames.DingTalk, BotProviderNames.Lark);

        var channels = Resolve(services).Channels;

        Assert.True(channels.ContainsKey("ops"));
        Assert.Equal(2, channels["ops"].Providers.Count);
        Assert.Equal("ops", channels["ops"].Name);
    }

    /// <summary>
    /// 提供者名两端空白被裁剪，纯空白项被丢弃
    /// </summary>
    [Fact]
    public void AddChannel_TrimsAndDropsBlankProviders()
    {
        var services = new ServiceCollection();

        new BotBuilder(services).AddChannel("ops", "  DingTalk  ", "   ", string.Empty, "Lark");

        var providers = Resolve(services).Channels["ops"].Providers;

        Assert.Equal(2, providers.Count);
        Assert.Equal("DingTalk", providers[0]);
        Assert.Equal("Lark", providers[1]);
    }

    /// <summary>
    /// 不给提供者时渠道映射为空列表而不是 null
    /// </summary>
    [Fact]
    public void AddChannel_WithoutProviders_ProducesEmptyList()
    {
        var services = new ServiceCollection();

        new BotBuilder(services).AddChannel("ops");

        var channel = Resolve(services).Channels["ops"];

        Assert.NotNull(channel.Providers);
        Assert.Empty(channel.Providers);
    }

    /// <summary>
    /// 同名渠道后注册的覆盖先注册的
    /// </summary>
    [Fact]
    public void AddChannel_SameName_LastOneWins()
    {
        var services = new ServiceCollection();

        new BotBuilder(services)
            .AddChannel("ops", BotProviderNames.DingTalk)
            .AddChannel("ops", BotProviderNames.Lark, BotProviderNames.WeCom);

        var channels = Resolve(services).Channels;

        Assert.Single(channels);
        Assert.Equal(2, channels["ops"].Providers.Count);
    }

    /// <summary>
    /// 添加模板后可在选项里按名称取回，且模板内容与类型完整保留
    /// </summary>
    [Fact]
    public void AddTemplate_RegistersTemplate()
    {
        var services = new ServiceCollection();
        var template = new BotTemplate
        {
            Name = "alert",
            Title = "{{Level}}",
            Content = "{{Message}}",
            Type = BotMessageType.Card
        };

        new BotBuilder(services).AddTemplate(template);

        var templates = Resolve(services).Templates;

        Assert.True(templates.ContainsKey("ALERT"));
        Assert.Same(template, templates["alert"]);
        Assert.Equal(BotMessageType.Card, templates["alert"].Type);
    }

    /// <summary>
    /// 模板名为空白时在解析选项的那一刻抛出
    /// </summary>
    [Fact]
    public void AddTemplate_WhenNameBlank_ThrowsOnResolve()
    {
        var services = new ServiceCollection();

        new BotBuilder(services).AddTemplate(new BotTemplate { Name = "  ", Content = "x" });

        Assert.Throws<ArgumentException>(() => { _ = Resolve(services); });
    }

    private static XiHanBotOptions Resolve(IServiceCollection services)
    {
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<XiHanBotOptions>>().Value;
    }
}
