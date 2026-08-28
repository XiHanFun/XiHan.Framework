// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json.Nodes;
using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Enums;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.WeCom.Messaging;
using XiHan.Framework.Bot.WeCom.Models;
using XiHan.Framework.Bot.WeCom.Options;
using XiHan.Framework.Bot.WeCom.Tests.Fakes;

namespace XiHan.Framework.Bot.WeCom.Tests.Messaging;

/// <summary>
/// <see cref="WeComBotProvider"/> 路由与短路逻辑测试
/// </summary>
/// <remarks>
/// 提供者的职责是「统一消息 → 企业微信消息类型」的路由：先做配置短路，再按消息类型挑负载，
/// 挑不到专属负载时一律退化成文本。这里对每条分支都验证最终发出的报文形态，
/// 出站请求由 <see cref="CapturingHttpService"/> 拦截，不产生真实网络流量。
/// </remarks>
[Collection(WeComHttpCollection.Name)]
public class WeComBotProviderTests
{
    private readonly CapturingHttpService _http;

    /// <summary>
    /// 每个用例前复位替身的记录与预置响应
    /// </summary>
    public WeComBotProviderTests()
    {
        _http = WeComHttpTestHost.Http;
        _http.Reset();
    }

    /// <summary>
    /// 提供者名称使用统一常量，供渠道映射按名匹配
    /// </summary>
    [Fact]
    public void Name_IsWeComProviderConstant()
    {
        var provider = new WeComBotProvider(new FakeWeComConfigStore(new WeComOptions()));

        Assert.Equal("WeCom", provider.Name);
        Assert.Equal(BotProviderNames.WeCom, provider.Name);
    }

    /// <summary>
    /// 未配置时直接短路返回请求错误，不发出任何请求
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenOptionsMissing_ShortCircuitsWithoutRequest()
    {
        var provider = new WeComBotProvider(new FakeWeComConfigStore(null));
        var message = new BotMessage { Content = "x" };

        var result = await provider.SendAsync(message, CreateContext(message));

        Assert.False(result.IsSuccess);
        Assert.Equal(BotResultCodes.BadRequest, result.Code);
        Assert.Equal("WeCom provider is not configured or disabled.", result.Message);
        Assert.Equal(BotProviderNames.WeCom, result.Provider);
        Assert.Equal(0, _http.CallCount);
    }

    /// <summary>
    /// 配置被禁用时直接短路返回请求错误，不发出任何请求
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenDisabled_ShortCircuitsWithoutRequest()
    {
        var options = new WeComOptions { Enabled = false, Key = "key" };
        var provider = new WeComBotProvider(new FakeWeComConfigStore(options));
        var message = new BotMessage { Content = "x" };

        var result = await provider.SendAsync(message, CreateContext(message));

        Assert.False(result.IsSuccess);
        Assert.Equal("WeCom provider is not configured or disabled.", result.Message);
        Assert.Equal(0, _http.CallCount);
    }

    /// <summary>
    /// Key 为空白时直接短路返回请求错误，不发出任何请求
    /// </summary>
    /// <param name="key">空白形态的 Key</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendAsync_WhenKeyIsBlank_ShortCircuitsWithoutRequest(string key)
    {
        var options = new WeComOptions { Key = key };
        var provider = new WeComBotProvider(new FakeWeComConfigStore(options));
        var message = new BotMessage { Content = "x" };

        var result = await provider.SendAsync(message, CreateContext(message));

        Assert.False(result.IsSuccess);
        Assert.Equal("WeCom key is required.", result.Message);
        Assert.Equal(BotProviderNames.WeCom, result.Provider);
        Assert.Equal(0, _http.CallCount);
    }

    /// <summary>
    /// 读取配置时透传上下文的取消令牌
    /// </summary>
    [Fact]
    public async Task SendAsync_PassesContextCancellationTokenToConfigStore()
    {
        using var cts = new CancellationTokenSource();
        var store = new FakeWeComConfigStore(null);
        var provider = new WeComBotProvider(store);
        var message = new BotMessage { Content = "x" };
        var context = new BotContext(message, [], cts.Token);

        await provider.SendAsync(message, context);

        Assert.Equal(1, store.CallCount);
        Assert.Equal(cts.Token, store.LastCancellationToken);
    }

    /// <summary>
    /// 纯文本消息发出 text 报文，结果标注提供者名称
    /// </summary>
    [Fact]
    public async Task SendAsync_TextMessage_SendsTextEnvelopeTaggedWithProvider()
    {
        var provider = CreateProvider();
        var message = new BotMessage { Content = "服务已恢复", Type = BotMessageType.Text };

        var result = await provider.SendAsync(message, CreateContext(message));

        Assert.True(result.IsSuccess);
        Assert.Equal(BotProviderNames.WeCom, result.Provider);
        Assert.Equal(1, _http.CallCount);

        var body = _http.LastBodyAsJson();
        Assert.NotNull(body);
        Assert.Equal("text", body!["msgtype"]!.GetValue<string>());
        Assert.Equal("服务已恢复", body["text"]!["content"]!.GetValue<string>());
    }

    /// <summary>
    /// 提及列表同时填入 mentioned_list 与 mentioned_mobile_list
    /// </summary>
    /// <remarks>
    /// 统一消息模型只有一份 Mentions，无法区分 userid 与手机号，
    /// 提供者的做法是两个协议字段都填同一份，交给企业微信按群成员过滤。
    /// </remarks>
    [Fact]
    public async Task SendAsync_WithMentions_FillsBothMentionLists()
    {
        var provider = CreateProvider();
        var message = new BotMessage
        {
            Content = "请处理",
            Mentions = ["13800001111", "@all"]
        };

        await provider.SendAsync(message, CreateContext(message));

        var body = _http.LastBodyAsJson();
        Assert.NotNull(body);
        Assert.Equal(new[] { "13800001111", "@all" }, ToStringArray(body!["text"]!["mentioned_list"]));
        Assert.Equal(new[] { "13800001111", "@all" }, ToStringArray(body["text"]!["mentioned_mobile_list"]));
    }

    /// <summary>
    /// 无提及时不写入任何 @ 字段
    /// </summary>
    [Fact]
    public async Task SendAsync_WithoutMentions_LeavesMentionListsNull()
    {
        var provider = CreateProvider();
        var message = new BotMessage { Content = "x" };

        await provider.SendAsync(message, CreateContext(message));

        var body = _http.LastBodyAsJson();
        Assert.NotNull(body);
        Assert.Null(body!["text"]!["mentioned_list"]);
        Assert.Null(body["text"]!["mentioned_mobile_list"]);
    }

    /// <summary>
    /// Markdown 消息发出 markdown 报文
    /// </summary>
    [Fact]
    public async Task SendAsync_MarkdownMessage_SendsMarkdownEnvelope()
    {
        var provider = CreateProvider();
        var message = new BotMessage { Content = "# 标题", Type = BotMessageType.Markdown };

        await provider.SendAsync(message, CreateContext(message));

        var body = _http.LastBodyAsJson();
        Assert.NotNull(body);
        Assert.Equal("markdown", body!["msgtype"]!.GetValue<string>());
        Assert.Equal("# 标题", body["markdown"]!["content"]!.GetValue<string>());
    }

    /// <summary>
    /// 图片消息携带企业微信图片负载时发出 image 报文
    /// </summary>
    [Fact]
    public async Task SendAsync_ImageMessage_WithImagePayload_SendsImageEnvelope()
    {
        var provider = CreateProvider();
        var message = new BotMessage { Content = "x", Type = BotMessageType.Image };
        message.Data[WeComMessageDataKeys.WeComImage] = new WeComImage { Md5 = "MD5", Base64 = "B64" };

        await provider.SendAsync(message, CreateContext(message));

        var body = _http.LastBodyAsJson();
        Assert.NotNull(body);
        Assert.Equal("image", body!["msgtype"]!.GetValue<string>());
        Assert.Equal("MD5", body["image"]!["md5"]!.GetValue<string>());
    }

    /// <summary>
    /// 图片消息缺少专属负载时退化为文本报文
    /// </summary>
    /// <remarks>
    /// 这是刻意的降级：宁可发一条文本，也不要静默丢消息。
    /// </remarks>
    [Fact]
    public async Task SendAsync_ImageMessage_WithoutImagePayload_FallsBackToText()
    {
        var provider = CreateProvider();
        var message = new BotMessage { Content = "降级正文", Type = BotMessageType.Image };

        await provider.SendAsync(message, CreateContext(message));

        var body = _http.LastBodyAsJson();
        Assert.NotNull(body);
        Assert.Equal("text", body!["msgtype"]!.GetValue<string>());
        Assert.Equal("降级正文", body["text"]!["content"]!.GetValue<string>());
    }

    /// <summary>
    /// 负载类型与键名不匹配时同样退化为文本报文
    /// </summary>
    [Fact]
    public async Task SendAsync_ImageMessage_WithMismatchedPayloadType_FallsBackToText()
    {
        var provider = CreateProvider();
        var message = new BotMessage { Content = "降级正文", Type = BotMessageType.Image };
        message.Data[WeComMessageDataKeys.WeComImage] = new WeComFile { MediaId = "M" };

        await provider.SendAsync(message, CreateContext(message));

        var body = _http.LastBodyAsJson();
        Assert.NotNull(body);
        Assert.Equal("text", body!["msgtype"]!.GetValue<string>());
    }

    /// <summary>
    /// 消息 Data 的键名匹配忽略大小写
    /// </summary>
    /// <remarks>
    /// <c>BotMessage.Data</c> 用的是忽略大小写的比较器，调用方大小写写错也应命中专属负载。
    /// </remarks>
    [Fact]
    public async Task SendAsync_DataKeyLookup_IsCaseInsensitive()
    {
        var provider = CreateProvider();
        var message = new BotMessage { Content = "x", Type = BotMessageType.File };
        message.Data["wecom.file"] = new WeComFile { MediaId = "FILE_MEDIA" };

        await provider.SendAsync(message, CreateContext(message));

        var body = _http.LastBodyAsJson();
        Assert.NotNull(body);
        Assert.Equal("file", body!["msgtype"]!.GetValue<string>());
        Assert.Equal("FILE_MEDIA", body["file"]!["media_id"]!.GetValue<string>());
    }

    /// <summary>
    /// 文件消息携带企业微信文件负载时发出 file 报文
    /// </summary>
    [Fact]
    public async Task SendAsync_FileMessage_WithFilePayload_SendsFileEnvelope()
    {
        var provider = CreateProvider();
        var message = new BotMessage { Content = "x", Type = BotMessageType.File };
        message.Data[WeComMessageDataKeys.WeComFile] = new WeComFile { MediaId = "FILE_MEDIA" };

        await provider.SendAsync(message, CreateContext(message));

        var body = _http.LastBodyAsJson();
        Assert.NotNull(body);
        Assert.Equal("file", body!["msgtype"]!.GetValue<string>());
        Assert.Equal("FILE_MEDIA", body["file"]!["media_id"]!.GetValue<string>());
    }

    /// <summary>
    /// 卡片消息携带文本通知负载时发出 text_notice 模版卡片
    /// </summary>
    [Fact]
    public async Task SendAsync_CardMessage_WithTextNoticePayload_SendsTextNoticeCard()
    {
        var provider = CreateProvider();
        var message = new BotMessage { Content = "x", Type = BotMessageType.Card };
        message.Data[WeComMessageDataKeys.WeComTemplateCardTextNotice] = new WeComTemplateCardTextNotice
        {
            MainTitle = new WeComMainTitle { Title = "服务告警" },
            CardAction = new WeComCardAction { Type = 1, Url = "https://example.com/card" }
        };

        await provider.SendAsync(message, CreateContext(message));

        var body = _http.LastBodyAsJson();
        Assert.NotNull(body);
        Assert.Equal("template_card", body!["msgtype"]!.GetValue<string>());
        Assert.Equal("text_notice", body["template_card"]!["card_type"]!.GetValue<string>());
    }

    /// <summary>
    /// 卡片消息只带图文展示负载时发出 news_notice 模版卡片
    /// </summary>
    [Fact]
    public async Task SendAsync_CardMessage_WithNewsNoticePayload_SendsNewsNoticeCard()
    {
        var provider = CreateProvider();
        var message = new BotMessage { Content = "x", Type = BotMessageType.Card };
        message.Data[WeComMessageDataKeys.WeComTemplateCardNewsNotice] = new WeComTemplateCardNewsNotice
        {
            CardImage = new WeComCardImage { Url = "https://example.com/banner.png" },
            CardAction = new WeComCardAction { Type = 1, Url = "https://example.com/card" }
        };

        await provider.SendAsync(message, CreateContext(message));

        var body = _http.LastBodyAsJson();
        Assert.NotNull(body);
        Assert.Equal("template_card", body!["msgtype"]!.GetValue<string>());
        Assert.Equal("news_notice", body["template_card"]!["card_type"]!.GetValue<string>());
    }

    /// <summary>
    /// 两种卡片负载同时存在时优先发文本通知卡片
    /// </summary>
    [Fact]
    public async Task SendAsync_CardMessage_WithBothPayloads_PrefersTextNotice()
    {
        var provider = CreateProvider();
        var message = new BotMessage { Content = "x", Type = BotMessageType.Card };
        message.Data[WeComMessageDataKeys.WeComTemplateCardTextNotice] = new WeComTemplateCardTextNotice();
        message.Data[WeComMessageDataKeys.WeComTemplateCardNewsNotice] = new WeComTemplateCardNewsNotice();

        await provider.SendAsync(message, CreateContext(message));

        var body = _http.LastBodyAsJson();
        Assert.NotNull(body);
        Assert.Equal("text_notice", body!["template_card"]!["card_type"]!.GetValue<string>());
    }

    /// <summary>
    /// 链接消息携带图文负载时发出 news 报文
    /// </summary>
    [Fact]
    public async Task SendAsync_LinkMessage_WithNewsPayload_SendsNewsEnvelope()
    {
        var provider = CreateProvider();
        var message = new BotMessage { Content = "x", Type = BotMessageType.Link };
        message.Data[WeComMessageDataKeys.WeComNews] = new WeComNews
        {
            Articles =
            [
                new WeComArticle { Title = "发布公告", Url = "https://example.com/release" }
            ]
        };

        await provider.SendAsync(message, CreateContext(message));

        var body = _http.LastBodyAsJson();
        Assert.NotNull(body);
        Assert.Equal("news", body!["msgtype"]!.GetValue<string>());
        Assert.Equal("发布公告", body["news"]!["articles"]!.AsArray()[0]!["title"]!.GetValue<string>());
    }

    /// <summary>
    /// 远端返回业务错误码时结果为请求错误且带提供者名称
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenWeComReturnsErrorCode_ReturnsBadRequestTaggedWithProvider()
    {
        _http.NextRawJson = """{"errcode":93000,"errmsg":"invalid webhook url"}""";
        var provider = CreateProvider();
        var message = new BotMessage { Content = "x" };

        var result = await provider.SendAsync(message, CreateContext(message));

        Assert.False(result.IsSuccess);
        Assert.Equal(BotResultCodes.BadRequest, result.Code);
        Assert.Equal("发送失败；", result.Message);
        Assert.Equal(BotProviderNames.WeCom, result.Provider);
    }

    private static WeComBotProvider CreateProvider()
    {
        return new WeComBotProvider(new FakeWeComConfigStore(new WeComOptions { Key = "unit-test-key" }));
    }

    private static BotContext CreateContext(BotMessage message)
    {
        return new BotContext(message, [], TestContext.Current.CancellationToken);
    }

    private static string[] ToStringArray(JsonNode? node)
    {
        Assert.NotNull(node);
        return [.. node!.AsArray().Select(item => item!.GetValue<string>())];
    }
}
