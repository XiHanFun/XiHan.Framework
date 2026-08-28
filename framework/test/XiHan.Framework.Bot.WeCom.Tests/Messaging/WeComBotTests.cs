// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Enums;
using XiHan.Framework.Bot.WeCom.Messaging;
using XiHan.Framework.Bot.WeCom.Models;
using XiHan.Framework.Bot.WeCom.Options;
using XiHan.Framework.Bot.WeCom.Tests.Fakes;

namespace XiHan.Framework.Bot.WeCom.Tests.Messaging;

/// <summary>
/// <see cref="WeComBot"/> 报文组装与结果判定测试
/// </summary>
/// <remarks>
/// 全程通过 <see cref="CapturingHttpService"/> 拦截出站请求，不产生任何真实网络流量。
/// 断言分三类：请求 URL（webhook 地址 + key 查询参数 + 上传的 type 查询参数）、
/// 请求体的企业微信协议字段名、以及 errcode/传输失败各分支的结果映射。
/// </remarks>
[Collection(WeComHttpCollection.Name)]
public class WeComBotTests
{
    private const string TestKey = "693a91f6-7xxx-4bc4-97a0-0ec2sifa5aaa";

    private const string ExpectedMessageUrl = $"https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key={TestKey}";

    private const string ExpectedUploadUrl = $"https://qyapi.weixin.qq.com/cgi-bin/webhook/upload_media?key={TestKey}";

    private readonly CapturingHttpService _http;

    /// <summary>
    /// 每个用例前复位替身的记录与预置响应
    /// </summary>
    public WeComBotTests()
    {
        _http = WeComHttpTestHost.Http;
        _http.Reset();
    }

    /// <summary>
    /// 文本消息发往 webhook 地址，报文为 msgtype=text + text.content
    /// </summary>
    [Fact]
    public async Task TextMessage_PostsTextEnvelopeToWebHookUrl()
    {
        var bot = CreateBot();

        var result = await bot.TextMessage(new WeComText { Content = "服务已恢复" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("发送成功；", Assert.IsType<string>(result.Data));
        Assert.Equal(ExpectedMessageUrl, _http.LastUrl);

        var body = _http.LastBodyAsJson();
        Assert.NotNull(body);
        Assert.Equal("text", body!["msgtype"]!.GetValue<string>());
        Assert.Equal("服务已恢复", body["text"]!["content"]!.GetValue<string>());
    }

    /// <summary>
    /// 文本消息的 @ 列表按协议字段名一起发出
    /// </summary>
    [Fact]
    public async Task TextMessage_WithMentions_SendsBothMentionLists()
    {
        var bot = CreateBot();
        var text = new WeComText
        {
            Content = "请处理",
            Mentions = ["zhangsan"],
            MentionedMobiles = ["13800001111"]
        };

        await bot.TextMessage(text, TestContext.Current.CancellationToken);

        var body = _http.LastBodyAsJson();
        Assert.NotNull(body);
        Assert.Equal("zhangsan", body!["text"]!["mentioned_list"]!.AsArray()[0]!.GetValue<string>());
        Assert.Equal("13800001111", body["text"]!["mentioned_mobile_list"]!.AsArray()[0]!.GetValue<string>());
    }

    /// <summary>
    /// 文档消息报文为 msgtype=markdown + markdown.content
    /// </summary>
    [Fact]
    public async Task MarkdownMessage_PostsMarkdownEnvelope()
    {
        var bot = CreateBot();

        var result = await bot.MarkdownMessage(new WeComMarkdown { Content = "# 标题" }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var body = _http.LastBodyAsJson();
        Assert.NotNull(body);
        Assert.Equal("markdown", body!["msgtype"]!.GetValue<string>());
        Assert.Equal("# 标题", body["markdown"]!["content"]!.GetValue<string>());
    }

    /// <summary>
    /// 图片消息报文为 msgtype=image + image.md5/base64
    /// </summary>
    [Fact]
    public async Task ImageMessage_PostsImageEnvelope()
    {
        var bot = CreateBot();
        var image = new WeComImage { Md5 = "5d41402abc4b2a76b9719d911017c592", Base64 = "aGVsbG8=" };

        await bot.ImageMessage(image, TestContext.Current.CancellationToken);

        var body = _http.LastBodyAsJson();
        Assert.NotNull(body);
        Assert.Equal("image", body!["msgtype"]!.GetValue<string>());
        Assert.Equal("5d41402abc4b2a76b9719d911017c592", body["image"]!["md5"]!.GetValue<string>());
        Assert.Equal("aGVsbG8=", body["image"]!["base64"]!.GetValue<string>());
    }

    /// <summary>
    /// 图文消息报文为 msgtype=news + news.articles
    /// </summary>
    [Fact]
    public async Task NewsMessage_PostsNewsEnvelope()
    {
        var bot = CreateBot();
        var news = new WeComNews
        {
            Articles =
            [
                new WeComArticle { Title = "发布公告", Url = "https://example.com/release" }
            ]
        };

        await bot.NewsMessage(news, TestContext.Current.CancellationToken);

        var body = _http.LastBodyAsJson();
        Assert.NotNull(body);
        Assert.Equal("news", body!["msgtype"]!.GetValue<string>());
        Assert.Equal("发布公告", body["news"]!["articles"]!.AsArray()[0]!["title"]!.GetValue<string>());
    }

    /// <summary>
    /// 文件消息报文为 msgtype=file + file.media_id
    /// </summary>
    [Fact]
    public async Task FileMessage_PostsFileEnvelope()
    {
        var bot = CreateBot();

        await bot.FileMessage(new WeComFile { MediaId = "FILE_MEDIA" }, TestContext.Current.CancellationToken);

        var body = _http.LastBodyAsJson();
        Assert.NotNull(body);
        Assert.Equal("file", body!["msgtype"]!.GetValue<string>());
        Assert.Equal("FILE_MEDIA", body["file"]!["media_id"]!.GetValue<string>());
    }

    /// <summary>
    /// 语音消息报文为 msgtype=voice + voice.media_id
    /// </summary>
    [Fact]
    public async Task VoiceMessage_PostsVoiceEnvelope()
    {
        var bot = CreateBot();

        await bot.VoiceMessage(new WeComVoice { MediaId = "VOICE_MEDIA" }, TestContext.Current.CancellationToken);

        var body = _http.LastBodyAsJson();
        Assert.NotNull(body);
        Assert.Equal("voice", body!["msgtype"]!.GetValue<string>());
        Assert.Equal("VOICE_MEDIA", body["voice"]!["media_id"]!.GetValue<string>());
    }

    /// <summary>
    /// 文本通知卡片会被强制改写 card_type 为 text_notice
    /// </summary>
    /// <remarks>
    /// 调用方即使填错 CardType 也必须被纠正，否则企业微信会按未知卡片类型拒收。
    /// </remarks>
    [Fact]
    public async Task TextNoticeMessage_OverwritesCardTypeToTextNotice()
    {
        var bot = CreateBot();
        var card = new WeComTemplateCardTextNotice
        {
            CardType = "wrong_type",
            MainTitle = new WeComMainTitle { Title = "服务告警" },
            CardAction = new WeComCardAction { Type = 1, Url = "https://example.com/card" }
        };

        await bot.TextNoticeMessage(card, TestContext.Current.CancellationToken);

        Assert.Equal("text_notice", card.CardType);
        var body = _http.LastBodyAsJson();
        Assert.NotNull(body);
        Assert.Equal("template_card", body!["msgtype"]!.GetValue<string>());
        Assert.Equal("text_notice", body["template_card"]!["card_type"]!.GetValue<string>());
        Assert.Equal("服务告警", body["template_card"]!["main_title"]!["title"]!.GetValue<string>());
    }

    /// <summary>
    /// 图文展示卡片会被强制改写 card_type 为 news_notice
    /// </summary>
    [Fact]
    public async Task NewsNoticeMessage_OverwritesCardTypeToNewsNotice()
    {
        var bot = CreateBot();
        var card = new WeComTemplateCardNewsNotice
        {
            CardType = "wrong_type",
            CardImage = new WeComCardImage { Url = "https://example.com/banner.png" },
            CardAction = new WeComCardAction { Type = 1, Url = "https://example.com/card" }
        };

        await bot.NewsNoticeMessage(card, TestContext.Current.CancellationToken);

        Assert.Equal("news_notice", card.CardType);
        var body = _http.LastBodyAsJson();
        Assert.NotNull(body);
        Assert.Equal("template_card", body!["msgtype"]!.GetValue<string>());
        Assert.Equal("news_notice", body["template_card"]!["card_type"]!.GetValue<string>());
        Assert.Equal("https://example.com/banner.png", body["template_card"]!["card_image"]!["url"]!.GetValue<string>());
    }

    /// <summary>
    /// 自定义端点配置生效，key 依然以查询参数拼接
    /// </summary>
    [Fact]
    public async Task CustomEndpoints_AreUsedWithKeyQueryParameter()
    {
        var bot = new WeComBot(new WeComOptions
        {
            Key = "abc",
            WebHookUrl = "https://proxy.internal/webhook/send",
            UploadUrl = "https://proxy.internal/webhook/upload_media"
        });

        await bot.TextMessage(new WeComText { Content = "x" }, TestContext.Current.CancellationToken);

        Assert.Equal("https://proxy.internal/webhook/send?key=abc", _http.LastUrl);
    }

    /// <summary>
    /// 传输层失败时把原始报文原样带回，便于排障
    /// </summary>
    [Fact]
    public async Task TextMessage_WhenTransportFails_ReturnsRawPayloadAsBadRequest()
    {
        _http.NextIsSuccess = false;
        _http.NextRawJson = "gateway timeout";
        var bot = CreateBot();

        var result = await bot.TextMessage(new WeComText { Content = "x" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(BotResultCodes.BadRequest, result.Code);
        Assert.Equal("gateway timeout", result.Message);
    }

    /// <summary>
    /// 报文解析不出对象时按请求错误处理
    /// </summary>
    [Fact]
    public async Task TextMessage_WhenResponseDataIsNull_ReturnsBadRequest()
    {
        _http.NextRawJson = "null";
        var bot = CreateBot();

        var result = await bot.TextMessage(new WeComText { Content = "x" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(BotResultCodes.BadRequest, result.Code);
    }

    /// <summary>
    /// 企业微信返回业务错误码时按请求错误处理
    /// </summary>
    [Fact]
    public async Task TextMessage_WhenWeComReturnsErrorCode_ReturnsBadRequest()
    {
        _http.NextRawJson = """{"errcode":93000,"errmsg":"invalid webhook url"}""";
        var bot = CreateBot();

        var result = await bot.TextMessage(new WeComText { Content = "x" }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(BotResultCodes.BadRequest, result.Code);
        Assert.Equal("发送失败；", result.Message);
    }

    /// <summary>
    /// 上传按类型追加 type 查询参数
    /// </summary>
    /// <param name="uploadType">上传类型</param>
    /// <param name="expectedSuffix">期望的查询参数后缀</param>
    [Theory]
    [InlineData(WeComUploadType.File, "&type=file")]
    [InlineData(WeComUploadType.Voice, "&type=voice")]
    public async Task UploadFile_AppendsTypeQueryParameter(WeComUploadType uploadType, string expectedSuffix)
    {
        var path = CreateTempFile("hello", out _);

        try
        {
            _http.NextRawJson = """{"errcode":0,"errmsg":"ok","type":"file","media_id":"MEDIA","created_at":"1380000000"}""";
            var bot = CreateBot();

            await using var stream = File.OpenRead(path);
            await bot.UploadFile(stream, uploadType, TestContext.Current.CancellationToken);

            Assert.Equal(ExpectedUploadUrl + expectedSuffix, _http.LastUrl);
            Assert.Same(stream, _http.LastBody);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// 未定义的上传类型不追加 type 查询参数
    /// </summary>
    [Fact]
    public async Task UploadFile_WithUndefinedUploadType_OmitsTypeQueryParameter()
    {
        var path = CreateTempFile("hello", out _);

        try
        {
            var bot = CreateBot();

            await using var stream = File.OpenRead(path);
            await bot.UploadFile(stream, (WeComUploadType)99, TestContext.Current.CancellationToken);

            Assert.Equal(ExpectedUploadUrl, _http.LastUrl);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// 上传请求带上文件名与文件长度请求头
    /// </summary>
    [Fact]
    public async Task UploadFile_SetsFileNameAndFileLengthHeaders()
    {
        var path = CreateTempFile("hello", out var fileName);

        try
        {
            var bot = CreateBot();

            await using var stream = File.OpenRead(path);
            await bot.UploadFile(stream, WeComUploadType.File, TestContext.Current.CancellationToken);

            Assert.NotNull(_http.LastOptions);
            Assert.True(_http.LastOptions!.Headers.TryGetValue("filename", out var headerFileName));
            Assert.EndsWith(fileName, headerFileName, StringComparison.Ordinal);
            Assert.Equal("5", _http.LastOptions!.Headers["filelength"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// 上传成功时返回携带 media_id 与素材类型的结果对象
    /// </summary>
    [Fact]
    public async Task UploadFile_WhenSucceeded_ReturnsUploadResultDto()
    {
        var path = CreateTempFile("hello", out _);

        try
        {
            _http.NextRawJson = """{"errcode":0,"errmsg":"ok","type":"file","media_id":"3a8asd892asd8asd","created_at":"1380000000"}""";
            var bot = CreateBot();

            await using var stream = File.OpenRead(path);
            var result = await bot.UploadFile(stream, WeComUploadType.File, TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            var uploadResult = Assert.IsType<WeComUploadResultDto>(result.Data);
            Assert.Equal("上传成功；", uploadResult.Message);
            Assert.Equal("file", uploadResult.Type);
            Assert.Equal("3a8asd892asd8asd", uploadResult.MediaId);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// 上传返回业务错误码时按请求错误处理
    /// </summary>
    [Fact]
    public async Task UploadFile_WhenWeComReturnsErrorCode_ReturnsBadRequest()
    {
        var path = CreateTempFile("hello", out _);

        try
        {
            _http.NextRawJson = """{"errcode":301002,"errmsg":"no upload permission"}""";
            var bot = CreateBot();

            await using var stream = File.OpenRead(path);
            var result = await bot.UploadFile(stream, WeComUploadType.File, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Equal(BotResultCodes.BadRequest, result.Code);
            Assert.Equal("上传失败；", result.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// 上传在传输层失败时把原始报文原样带回
    /// </summary>
    [Fact]
    public async Task UploadFile_WhenTransportFails_ReturnsRawPayloadAsBadRequest()
    {
        var path = CreateTempFile("hello", out _);

        try
        {
            _http.NextIsSuccess = false;
            _http.NextRawJson = "connection reset";
            var bot = CreateBot();

            await using var stream = File.OpenRead(path);
            var result = await bot.UploadFile(stream, WeComUploadType.File, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Equal("connection reset", result.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static WeComBot CreateBot()
    {
        return new WeComBot(new WeComOptions { Key = TestKey });
    }

    private static string CreateTempFile(string content, out string fileName)
    {
        fileName = $"xihan-wecom-{Guid.NewGuid():N}.txt";
        var path = Path.Combine(Path.GetTempPath(), fileName);
        File.WriteAllText(path, content);
        return path;
    }
}
