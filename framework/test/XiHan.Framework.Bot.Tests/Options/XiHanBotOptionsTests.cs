// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Enums;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Options;
using XiHan.Framework.Bot.Template;

namespace XiHan.Framework.Bot.Tests;

/// <summary>
/// <see cref="XiHanBotOptions"/> 测试
/// </summary>
/// <remarks>
/// 默认值决定"什么都不配"时的运行形态：广播、失败继续、无提供者不抛、重试三次、限流五条、不做环境过滤。
/// 这组默认值被 README 与各子包文档引用，改动属于破坏性变更。
/// </remarks>
public class XiHanBotOptionsTests
{
    /// <summary>
    /// 默认值符合"开箱即用且不抛异常"的设计
    /// </summary>
    [Fact]
    public void Defaults_AreOpenAndForgiving()
    {
        var options = new XiHanBotOptions();

        Assert.Equal(BotStrategyNames.Broadcast, options.DefaultStrategy);
        Assert.True(options.ContinueOnError);
        Assert.False(options.ThrowWhenNoProvider);
        Assert.Equal(3, options.RetryCount);
        Assert.Equal(TimeSpan.FromSeconds(1), options.RetryDelay);
        Assert.Equal(5, options.RateLimitPerSecond);
        Assert.True(options.EnableLoggingPipeline);
        Assert.True(options.EnableRetryPipeline);
        Assert.True(options.EnableRateLimitPipeline);
        Assert.False(options.EnableEnvironmentFilter);
        Assert.Empty(options.AllowedEnvironments);
        Assert.Empty(options.Channels);
        Assert.Empty(options.Templates);
    }

    /// <summary>
    /// 添加渠道后可按名称取回，且名称两端空白被裁剪
    /// </summary>
    [Fact]
    public void AddChannel_TrimsNameAndStores()
    {
        var options = new XiHanBotOptions();

        options.AddChannel(new BotChannel { Name = "  ops  ", Providers = ["DingTalk"] });

        Assert.True(options.Channels.ContainsKey("ops"));
        Assert.Single(options.Channels["ops"].Providers);
    }

    /// <summary>
    /// 渠道名称大小写不敏感
    /// </summary>
    [Fact]
    public void AddChannel_LookupIsCaseInsensitive()
    {
        var options = new XiHanBotOptions();

        options.AddChannel(new BotChannel { Name = "Ops", Providers = ["Lark"] });

        Assert.True(options.Channels.ContainsKey("OPS"));
        Assert.True(options.Channels.ContainsKey("ops"));
    }

    /// <summary>
    /// 同名渠道再次添加时整体替换而非追加
    /// </summary>
    [Fact]
    public void AddChannel_SameName_Replaces()
    {
        var options = new XiHanBotOptions();

        options.AddChannel(new BotChannel { Name = "ops", Providers = ["DingTalk"] });
        options.AddChannel(new BotChannel { Name = "ops", Providers = ["Lark", "WeCom"] });

        Assert.Single(options.Channels);
        Assert.Equal(2, options.Channels["ops"].Providers.Count);
        Assert.Contains("Lark", options.Channels["ops"].Providers);
    }

    /// <summary>
    /// 渠道为 null 时抛出参数空异常
    /// </summary>
    [Fact]
    public void AddChannel_WhenNull_Throws()
    {
        var options = new XiHanBotOptions();

        Assert.Throws<ArgumentNullException>(() => options.AddChannel(null!));
    }

    /// <summary>
    /// 渠道名称为空白时抛出参数异常
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddChannel_WhenNameBlank_Throws(string name)
    {
        var options = new XiHanBotOptions();

        var exception = Assert.Throws<ArgumentException>(() => options.AddChannel(new BotChannel { Name = name }));

        Assert.Contains("Channel name is required.", exception.Message);
        Assert.Equal("channel", exception.ParamName);
    }

    /// <summary>
    /// 添加模板后可按名称取回，且名称两端空白被裁剪
    /// </summary>
    [Fact]
    public void AddTemplate_TrimsNameAndStores()
    {
        var options = new XiHanBotOptions();

        options.AddTemplate(new BotTemplate { Name = "  alert  ", Content = "hello" });

        Assert.True(options.Templates.ContainsKey("alert"));
        Assert.Equal("hello", options.Templates["ALERT"].Content);
    }

    /// <summary>
    /// 同名模板再次添加时整体替换
    /// </summary>
    [Fact]
    public void AddTemplate_SameName_Replaces()
    {
        var options = new XiHanBotOptions();

        options.AddTemplate(new BotTemplate { Name = "alert", Content = "v1" });
        options.AddTemplate(new BotTemplate { Name = "alert", Content = "v2", Type = BotMessageType.Card });

        Assert.Single(options.Templates);
        Assert.Equal("v2", options.Templates["alert"].Content);
        Assert.Equal(BotMessageType.Card, options.Templates["alert"].Type);
    }

    /// <summary>
    /// 模板为 null 时抛出参数空异常
    /// </summary>
    [Fact]
    public void AddTemplate_WhenNull_Throws()
    {
        var options = new XiHanBotOptions();

        Assert.Throws<ArgumentNullException>(() => options.AddTemplate(null!));
    }

    /// <summary>
    /// 模板名称为空白时抛出参数异常
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddTemplate_WhenNameBlank_Throws(string name)
    {
        var options = new XiHanBotOptions();

        var exception = Assert.Throws<ArgumentException>(() => options.AddTemplate(new BotTemplate { Name = name }));

        Assert.Contains("Template name is required.", exception.Message);
        Assert.Equal("template", exception.ParamName);
    }
}
