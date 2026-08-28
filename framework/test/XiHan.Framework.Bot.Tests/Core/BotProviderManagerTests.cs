// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Options;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.Tests.Fakes;

namespace XiHan.Framework.Bot.Tests.Core;

/// <summary>
/// <see cref="BotProviderManager"/> 测试
/// </summary>
/// <remarks>
/// 解析规则有两层：先查渠道映射，查不到才把名字直接当提供者名；两层都空则返回空集合而不是回退到全量广播——
/// 这一点很关键，"指定了渠道但一个都没匹配上"绝不能变成"发给所有人"。
/// </remarks>
public class BotProviderManagerTests
{
    /// <summary>
    /// 未指定渠道时返回全部提供者
    /// </summary>
    [Fact]
    public void ResolveProviders_WhenChannelsNull_ReturnsAll()
    {
        var manager = CreateManager(new XiHanBotOptions(), "A", "B");

        var providers = manager.ResolveProviders(null);

        Assert.Equal(2, providers.Count);
    }

    /// <summary>
    /// 渠道列表为空集合时返回全部提供者
    /// </summary>
    [Fact]
    public void ResolveProviders_WhenChannelsEmpty_ReturnsAll()
    {
        var manager = CreateManager(new XiHanBotOptions(), "A", "B");

        var providers = manager.ResolveProviders([]);

        Assert.Equal(2, providers.Count);
    }

    /// <summary>
    /// GetAllProviders 返回注册的全部提供者
    /// </summary>
    [Fact]
    public void GetAllProviders_ReturnsRegisteredProviders()
    {
        var manager = CreateManager(new XiHanBotOptions(), "A", "B", "C");

        Assert.Equal(3, manager.GetAllProviders().Count);
    }

    /// <summary>
    /// 名称未命中渠道映射时按提供者名直接匹配
    /// </summary>
    [Fact]
    public void ResolveProviders_WhenNameIsProvider_MatchesProvider()
    {
        var manager = CreateManager(new XiHanBotOptions(), "A", "B");

        var providers = manager.ResolveProviders(["B"]);

        Assert.Single(providers);
        Assert.Equal("B", providers[0].Name);
    }

    /// <summary>
    /// 提供者名匹配大小写不敏感
    /// </summary>
    [Fact]
    public void ResolveProviders_ProviderNameMatchIsCaseInsensitive()
    {
        var manager = CreateManager(new XiHanBotOptions(), "DingTalk");

        var providers = manager.ResolveProviders(["dingtalk"]);

        Assert.Single(providers);
    }

    /// <summary>
    /// 渠道映射展开为多个提供者
    /// </summary>
    [Fact]
    public void ResolveProviders_WhenChannelMapped_ExpandsToProviders()
    {
        var options = new XiHanBotOptions();
        options.AddChannel(new BotChannel { Name = "ops", Providers = ["A", "C"] });
        var manager = CreateManager(options, "A", "B", "C");

        var providers = manager.ResolveProviders(["ops"]);

        Assert.Equal(2, providers.Count);
        Assert.Contains(providers, provider => provider.Name == "A");
        Assert.Contains(providers, provider => provider.Name == "C");
    }

    /// <summary>
    /// 渠道名匹配大小写不敏感
    /// </summary>
    [Fact]
    public void ResolveProviders_ChannelNameMatchIsCaseInsensitive()
    {
        var options = new XiHanBotOptions();
        options.AddChannel(new BotChannel { Name = "Ops", Providers = ["A"] });
        var manager = CreateManager(options, "A", "B");

        var providers = manager.ResolveProviders(["OPS"]);

        Assert.Single(providers);
        Assert.Equal("A", providers[0].Name);
    }

    /// <summary>
    /// 渠道名两端有空白时先裁剪再匹配
    /// </summary>
    [Fact]
    public void ResolveProviders_TrimsChannelName()
    {
        var options = new XiHanBotOptions();
        options.AddChannel(new BotChannel { Name = "ops", Providers = ["A"] });
        var manager = CreateManager(options, "A", "B");

        var providers = manager.ResolveProviders(["  ops  "]);

        Assert.Single(providers);
    }

    /// <summary>
    /// 渠道映射里的空白提供者名被忽略
    /// </summary>
    [Fact]
    public void ResolveProviders_IgnoresBlankProviderNamesInChannel()
    {
        var options = new XiHanBotOptions();
        options.AddChannel(new BotChannel { Name = "ops", Providers = [" ", "  A  ", string.Empty] });
        var manager = CreateManager(options, "A", "B");

        var providers = manager.ResolveProviders(["ops"]);

        Assert.Single(providers);
        Assert.Equal("A", providers[0].Name);
    }

    /// <summary>
    /// 渠道的提供者列表为 null 时该渠道被跳过
    /// </summary>
    [Fact]
    public void ResolveProviders_WhenChannelProvidersNull_SkipsChannel()
    {
        var options = new XiHanBotOptions();
        options.AddChannel(new BotChannel { Name = "ops", Providers = null! });
        var manager = CreateManager(options, "A", "B");

        var providers = manager.ResolveProviders(["ops"]);

        Assert.Empty(providers);
    }

    /// <summary>
    /// 渠道名全是空白时返回空集合而不是全量广播
    /// </summary>
    [Fact]
    public void ResolveProviders_WhenAllChannelsBlank_ReturnsEmpty()
    {
        var manager = CreateManager(new XiHanBotOptions(), "A", "B");

        var providers = manager.ResolveProviders(["   ", string.Empty]);

        Assert.Empty(providers);
    }

    /// <summary>
    /// 指定的名字一个都匹配不上时返回空集合
    /// </summary>
    [Fact]
    public void ResolveProviders_WhenNothingMatches_ReturnsEmpty()
    {
        var manager = CreateManager(new XiHanBotOptions(), "A", "B");

        var providers = manager.ResolveProviders(["NotExists"]);

        Assert.Empty(providers);
    }

    /// <summary>
    /// 渠道与提供者名混用时合并去重
    /// </summary>
    [Fact]
    public void ResolveProviders_MixedChannelAndProviderNames_AreDeduplicated()
    {
        var options = new XiHanBotOptions();
        options.AddChannel(new BotChannel { Name = "ops", Providers = ["A", "B"] });
        var manager = CreateManager(options, "A", "B", "C");

        var providers = manager.ResolveProviders(["ops", "A", "C"]);

        Assert.Equal(3, providers.Count);
    }

    /// <summary>
    /// 返回顺序跟随提供者的注册顺序，而不是请求渠道的书写顺序
    /// </summary>
    [Fact]
    public void ResolveProviders_KeepsRegistrationOrder()
    {
        var manager = CreateManager(new XiHanBotOptions(), "A", "B", "C");

        var providers = manager.ResolveProviders(["C", "A"]);

        Assert.Equal(2, providers.Count);
        Assert.Equal("A", providers[0].Name);
        Assert.Equal("C", providers[1].Name);
    }

    private static BotProviderManager CreateManager(XiHanBotOptions options, params string[] providerNames)
    {
        var providers = providerNames.Select(name => (IBotProvider)FakeBotProvider.AlwaysSuccess(name)).ToArray();
        return new BotProviderManager(providers, new TestOptionsWrapper<XiHanBotOptions>(options));
    }
}
