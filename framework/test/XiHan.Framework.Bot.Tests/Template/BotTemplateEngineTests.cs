// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Bot.Enums;
using XiHan.Framework.Bot.Options;
using XiHan.Framework.Bot.Template;
using XiHan.Framework.Bot.Tests.Fakes;
using XiHan.Framework.Templating.Services;

namespace XiHan.Framework.Bot.Tests.Template;

/// <summary>
/// <see cref="BotTemplateEngine"/> 测试
/// </summary>
/// <remarks>
/// 引擎自己不做占位符替换，它的职责是：查模板表、开作用域取 <c>ITemplateService</c>、
/// 把内容与标题分别送去渲染、把模板的扩展数据复制一份挂到消息上。
/// 所以断言全部落在这四件事上，渲染语义由替身固定。
/// </remarks>
public class BotTemplateEngineTests
{
    /// <summary>
    /// 模板未配置时抛出并带上模板名
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenTemplateNotConfigured_Throws()
    {
        var engine = CreateEngine(new XiHanBotOptions(), out _);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.RenderAsync("missing"));

        Assert.Contains("missing", exception.Message);
        Assert.Contains("is not configured", exception.Message);
    }

    /// <summary>
    /// 按名称查模板时大小写不敏感
    /// </summary>
    [Fact]
    public async Task RenderAsync_TemplateLookupIsCaseInsensitive()
    {
        var options = new XiHanBotOptions();
        options.AddTemplate(new BotTemplate { Name = "Alert", Content = "hello" });
        var engine = CreateEngine(options, out _);

        var message = await engine.RenderAsync("alert");

        Assert.Equal("hello", message.Content);
    }

    /// <summary>
    /// 模板实例为 null 时抛出参数空异常
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenTemplateNull_Throws()
    {
        var engine = CreateEngine(new XiHanBotOptions(), out _);

        await Assert.ThrowsAsync<ArgumentNullException>(() => engine.RenderAsync((BotTemplate)null!));
    }

    /// <summary>
    /// 模板内容原样送去渲染，渲染结果作为消息内容
    /// </summary>
    [Fact]
    public async Task RenderAsync_PassesTemplateContentAndModelToTemplateService()
    {
        var engine = CreateEngine(new XiHanBotOptions(), out var templateService);
        var template = new BotTemplate { Content = "磁盘 {{Disk}} 使用率 {{Usage}}" };
        var model = new { Disk = "C", Usage = 91 };

        var message = await engine.RenderAsync(template, model);

        Assert.Single(templateService.RenderedSources);
        Assert.Equal("磁盘 {{Disk}} 使用率 {{Usage}}", templateService.RenderedSources[0]);
        Assert.Same(model, templateService.LastModel);
        Assert.Equal("磁盘 C 使用率 91", message.Content);
    }

    /// <summary>
    /// 模型里没有的占位符保持原样，不会被抹成空串
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenModelMissesVariable_KeepsPlaceholderUntouched()
    {
        var engine = CreateEngine(new XiHanBotOptions(), out _);
        var template = new BotTemplate { Content = "{{Known}} 与 {{Unknown}}" };

        var message = await engine.RenderAsync(template, new { Known = "有值" });

        Assert.Equal("有值 与 {{Unknown}}", message.Content);
    }

    /// <summary>
    /// 模型为 null 时内容原样返回
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenModelNull_KeepsContentAsIs()
    {
        var engine = CreateEngine(new XiHanBotOptions(), out var templateService);
        var template = new BotTemplate { Content = "{{Disk}}" };

        var message = await engine.RenderAsync(template);

        Assert.Equal("{{Disk}}", message.Content);
        Assert.Null(templateService.LastModel);
    }

    /// <summary>
    /// 标题非空时同样送去渲染
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenTitlePresent_RendersTitleToo()
    {
        var engine = CreateEngine(new XiHanBotOptions(), out var templateService);
        var template = new BotTemplate { Title = "{{Level}} 告警", Content = "{{Level}} 正文" };

        var message = await engine.RenderAsync(template, new { Level = "严重" });

        Assert.Equal(2, templateService.RenderCount);
        Assert.Equal("严重 告警", message.Title);
        Assert.Equal("严重 正文", message.Content);
    }

    /// <summary>
    /// 标题为 null 时不额外渲染，消息标题保持 null
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenTitleNull_SkipsTitleRendering()
    {
        var engine = CreateEngine(new XiHanBotOptions(), out var templateService);
        var template = new BotTemplate { Content = "正文" };

        var message = await engine.RenderAsync(template);

        Assert.Equal(1, templateService.RenderCount);
        Assert.Null(message.Title);
    }

    /// <summary>
    /// 标题为空白时不额外渲染，原样保留空白
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenTitleBlank_SkipsTitleRendering()
    {
        var engine = CreateEngine(new XiHanBotOptions(), out var templateService);
        var template = new BotTemplate { Title = "   ", Content = "正文" };

        var message = await engine.RenderAsync(template);

        Assert.Equal(1, templateService.RenderCount);
        Assert.Equal("   ", message.Title);
    }

    /// <summary>
    /// 消息类型来自模板类型
    /// </summary>
    [Fact]
    public async Task RenderAsync_CopiesTemplateType()
    {
        var engine = CreateEngine(new XiHanBotOptions(), out _);

        var message = await engine.RenderAsync(new BotTemplate { Content = "x", Type = BotMessageType.Card });

        Assert.Equal(BotMessageType.Card, message.Type);
    }

    /// <summary>
    /// 扩展数据被复制到消息上，且与模板不共享同一个字典实例
    /// </summary>
    [Fact]
    public async Task RenderAsync_CopiesTemplateDataIntoNewDictionary()
    {
        var engine = CreateEngine(new XiHanBotOptions(), out _);
        var template = new BotTemplate { Content = "x" };
        template.Data["Strategy"] = "Failover";

        var message = await engine.RenderAsync(template);

        Assert.NotSame(template.Data, message.Data);
        Assert.Equal("Failover", message.Data["strategy"]);

        message.Data["Extra"] = 1;

        Assert.False(template.Data.ContainsKey("Extra"));
    }

    /// <summary>
    /// 模板扩展数据为 null 时消息拿到空字典而不是 null
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenTemplateDataNull_ProducesEmptyDictionary()
    {
        var engine = CreateEngine(new XiHanBotOptions(), out _);
        var template = new BotTemplate { Content = "x", Data = null! };

        var message = await engine.RenderAsync(template);

        Assert.NotNull(message.Data);
        Assert.Empty(message.Data);
    }

    /// <summary>
    /// 每次渲染产出独立的消息实例
    /// </summary>
    [Fact]
    public async Task RenderAsync_ProducesNewMessageEachTime()
    {
        var engine = CreateEngine(new XiHanBotOptions(), out _);
        var template = new BotTemplate { Content = "x" };

        var first = await engine.RenderAsync(template);
        var second = await engine.RenderAsync(template);

        Assert.NotSame(first, second);
    }

    private static BotTemplateEngine CreateEngine(XiHanBotOptions options, out FakeTemplateService templateService)
    {
        templateService = new FakeTemplateService();
        var services = new ServiceCollection();
        services.AddSingleton<ITemplateService>(templateService);
        var provider = services.BuildServiceProvider();

        return new BotTemplateEngine(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new TestOptionsWrapper<XiHanBotOptions>(options));
    }
}
