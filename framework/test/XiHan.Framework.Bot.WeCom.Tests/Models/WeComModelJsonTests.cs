// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using XiHan.Framework.Bot.WeCom.Models;

namespace XiHan.Framework.Bot.WeCom.Tests.Models;

/// <summary>
/// 企业微信消息体 JSON 字段名契约测试
/// </summary>
/// <remarks>
/// 这些模型是直接序列化后当作请求体发给企业微信的，字段名由 <c>JsonPropertyName</c> 决定，
/// 属性改名不会编译报错但会让企业微信静默拒收，因此逐个字段锁死协议名而不是断言 CLR 属性名。
/// </remarks>
public class WeComModelJsonTests
{
    /// <summary>
    /// 文本消息的正文与两个 @ 列表使用协议字段名
    /// </summary>
    /// <remarks>
    /// mentioned_list 装 userid，mentioned_mobile_list 装手机号，两者语义不同不可互换。
    /// </remarks>
    [Fact]
    public void WeComText_SerializesProtocolFieldNames()
    {
        var text = new WeComText
        {
            Content = "巡检完成",
            Mentions = ["zhangsan", "@all"],
            MentionedMobiles = ["13800001111"]
        };

        var node = ToJson(text);

        Assert.Equal("巡检完成", node["content"]!.GetValue<string>());
        Assert.Equal(new[] { "zhangsan", "@all" }, ToStringArray(node["mentioned_list"]));
        Assert.Equal(new[] { "13800001111" }, ToStringArray(node["mentioned_mobile_list"]));
    }

    /// <summary>
    /// 文本消息可反序列化回强类型，字段映射双向可用
    /// </summary>
    [Fact]
    public void WeComText_Deserialize_MapsProtocolFieldNames()
    {
        const string Payload = """{"content":"hi","mentioned_list":["@all"],"mentioned_mobile_list":["13900002222"]}""";

        var text = JsonSerializer.Deserialize<WeComText>(Payload);

        Assert.NotNull(text);
        Assert.Equal("hi", text!.Content);
        Assert.Equal(new[] { "@all" }, text.Mentions?.ToArray());
        Assert.Equal(new[] { "13900002222" }, text.MentionedMobiles?.ToArray());
    }

    /// <summary>
    /// 文本消息默认不带任何 @ 列表
    /// </summary>
    [Fact]
    public void WeComText_Defaults_HaveNoMentions()
    {
        var text = new WeComText();

        Assert.Equal(string.Empty, text.Content);
        Assert.Null(text.Mentions);
        Assert.Null(text.MentionedMobiles);
    }

    /// <summary>
    /// 文档消息只有 content 一个协议字段
    /// </summary>
    [Fact]
    public void WeComMarkdown_SerializesContentField()
    {
        var markdown = new WeComMarkdown
        {
            Content = "## 标题"
        };

        var node = ToJson(markdown);

        Assert.Equal("## 标题", node["content"]!.GetValue<string>());
        Assert.Equal(string.Empty, new WeComMarkdown().Content);
    }

    /// <summary>
    /// 图片消息使用 md5 与 base64 字段名
    /// </summary>
    [Fact]
    public void WeComImage_SerializesMd5AndBase64()
    {
        var image = new WeComImage
        {
            Md5 = "5d41402abc4b2a76b9719d911017c592",
            Base64 = "aGVsbG8="
        };

        var node = ToJson(image);

        Assert.Equal("5d41402abc4b2a76b9719d911017c592", node["md5"]!.GetValue<string>());
        Assert.Equal("aGVsbG8=", node["base64"]!.GetValue<string>());
    }

    /// <summary>
    /// 图文消息的文章数组字段名为 articles，图片地址字段名为 picurl
    /// </summary>
    [Fact]
    public void WeComNews_SerializesArticlesWithPicUrl()
    {
        var news = new WeComNews
        {
            Articles =
            [
                new WeComArticle
                {
                    Title = "发布公告",
                    Description = "版本 1.2.3 已上线",
                    Url = "https://example.com/release",
                    PicUrl = "https://example.com/cover.png"
                }
            ]
        };

        var node = ToJson(news);
        var article = node["articles"]!.AsArray()[0]!;

        Assert.Equal("发布公告", article["title"]!.GetValue<string>());
        Assert.Equal("版本 1.2.3 已上线", article["description"]!.GetValue<string>());
        Assert.Equal("https://example.com/release", article["url"]!.GetValue<string>());
        Assert.Equal("https://example.com/cover.png", article["picurl"]!.GetValue<string>());
    }

    /// <summary>
    /// 文件与语音消息都使用 media_id 字段名
    /// </summary>
    [Fact]
    public void WeComFileAndVoice_SerializeMediaId()
    {
        var fileNode = ToJson(new WeComFile { MediaId = "FILE_MEDIA" });
        var voiceNode = ToJson(new WeComVoice { MediaId = "VOICE_MEDIA" });

        Assert.Equal("FILE_MEDIA", fileNode["media_id"]!.GetValue<string>());
        Assert.Equal("VOICE_MEDIA", voiceNode["media_id"]!.GetValue<string>());
        Assert.Equal(string.Empty, new WeComFile().MediaId);
        Assert.Equal(string.Empty, new WeComVoice().MediaId);
    }

    /// <summary>
    /// 文本通知模版卡片的各区块使用协议字段名
    /// </summary>
    [Fact]
    public void WeComTemplateCardTextNotice_SerializesProtocolFieldNames()
    {
        var card = new WeComTemplateCardTextNotice
        {
            CardType = "text_notice",
            Source = new WeComSource { IconUrl = "https://example.com/icon.png", Desc = "曦寒", DescColor = 2 },
            MainTitle = new WeComMainTitle { Title = "服务告警", Desc = "生产环境" },
            EmphasisContent = new WeComEmphasisContent { Title = "99%", Desc = "错误率" },
            QuoteArea = new WeComQuoteArea { Type = 1, Url = "https://example.com/detail", Title = "详情", QuoteText = "点此查看" },
            SubTitleText = "请立即处理",
            HorizontalContents =
            [
                new WeComHorizontalContent { Type = 3, KeyName = "负责人", Value = "张三", UserId = "zhangsan" }
            ],
            Jumps =
            [
                new WeComJump { Type = 1, Title = "查看", Url = "https://example.com/jump" }
            ],
            CardAction = new WeComCardAction { Type = 1, Url = "https://example.com/card" }
        };

        var node = ToJson(card);

        Assert.Equal("text_notice", node["card_type"]!.GetValue<string>());
        Assert.Equal("https://example.com/icon.png", node["source"]!["icon_url"]!.GetValue<string>());
        Assert.Equal(2, node["source"]!["desc_color"]!.GetValue<int>());
        Assert.Equal("服务告警", node["main_title"]!["title"]!.GetValue<string>());
        Assert.Equal("99%", node["emphasis_content"]!["title"]!.GetValue<string>());
        Assert.Equal("点此查看", node["quote_area"]!["quote_text"]!.GetValue<string>());
        Assert.Equal("请立即处理", node["sub_title_text"]!.GetValue<string>());
        Assert.Equal("负责人", node["horizontal_content_list"]!.AsArray()[0]!["keyname"]!.GetValue<string>());
        Assert.Equal("zhangsan", node["horizontal_content_list"]!.AsArray()[0]!["userid"]!.GetValue<string>());
        Assert.Equal("查看", node["jump_list"]!.AsArray()[0]!["title"]!.GetValue<string>());
        Assert.Equal("https://example.com/card", node["card_action"]!["url"]!.GetValue<string>());
    }

    /// <summary>
    /// 图文展示模版卡片的图片与左图右文区块使用协议字段名
    /// </summary>
    [Fact]
    public void WeComTemplateCardNewsNotice_SerializesProtocolFieldNames()
    {
        var card = new WeComTemplateCardNewsNotice
        {
            CardType = "news_notice",
            MainTitle = new WeComMainTitle { Title = "周报" },
            CardImage = new WeComCardImage { Url = "https://example.com/banner.png", AspectRatio = 1.5f },
            ImageTextArea = new WeComImageTextArea
            {
                Type = 1,
                Url = "https://example.com/area",
                Title = "本周概览",
                Desc = "一切正常",
                ImageUrl = "https://example.com/thumb.png"
            },
            VerticalContents =
            [
                new WeComVerticalContent { Title = "工单", Desc = "已全部关闭" }
            ],
            CardAction = new WeComCardAction { Type = 2, AppId = "wx123", PagePath = "/pages/index" }
        };

        var node = ToJson(card);

        Assert.Equal("news_notice", node["card_type"]!.GetValue<string>());
        Assert.Equal("https://example.com/banner.png", node["card_image"]!["url"]!.GetValue<string>());
        Assert.Equal(1.5f, node["card_image"]!["aspect_ratio"]!.GetValue<float>());
        Assert.Equal("https://example.com/thumb.png", node["image_text_area"]!["image_url"]!.GetValue<string>());
        Assert.Equal("工单", node["vertical_content_list"]!.AsArray()[0]!["title"]!.GetValue<string>());
        Assert.Equal("wx123", node["card_action"]!["appid"]!.GetValue<string>());
        Assert.Equal("/pages/index", node["card_action"]!["pagepath"]!.GetValue<string>());
    }

    /// <summary>
    /// 模版卡片的必填跳转事件有默认实例，默认跳转类型为 url
    /// </summary>
    /// <remarks>
    /// card_action 在企业微信是必填项，两个卡片模型都给了 new() 默认值，
    /// 默认 Type=1 表示跳 url，避免调用方漏填时报 40058。
    /// </remarks>
    [Fact]
    public void TemplateCards_CardAction_DefaultsToUrlJump()
    {
        Assert.Equal(1, new WeComCardAction().Type);
        Assert.NotNull(new WeComTemplateCardTextNotice().CardAction);
        Assert.NotNull(new WeComTemplateCardNewsNotice().CardAction);
        Assert.Equal(1, new WeComTemplateCardTextNotice().CardAction.Type);
        Assert.Equal(1, new WeComTemplateCardNewsNotice().CardAction.Type);
    }

    /// <summary>
    /// 可选样式区块的默认值为协议约定的 0(无点击事件)
    /// </summary>
    [Fact]
    public void OptionalAreas_DefaultTypes_MatchProtocolZero()
    {
        Assert.Equal(0, new WeComSource().DescColor);
        Assert.Equal(0, new WeComQuoteArea().Type);
        Assert.Equal(0, new WeComJump().Type);
        Assert.Equal(0, new WeComImageTextArea().Type);
        Assert.Null(new WeComHorizontalContent().Type);
        Assert.Null(new WeComCardImage().AspectRatio);
    }

    /// <summary>
    /// 文本通知模版卡片可从协议报文反序列化回强类型
    /// </summary>
    [Fact]
    public void WeComTemplateCardTextNotice_Deserialize_MapsProtocolFieldNames()
    {
        const string Payload = """
            {"card_type":"text_notice","main_title":{"title":"标题","desc":"辅助"},"sub_title_text":"正文","horizontal_content_list":[{"keyname":"k","value":"v"}],"jump_list":[{"type":1,"title":"跳转","url":"https://example.com"}],"card_action":{"type":1,"url":"https://example.com"}}
            """;

        var card = JsonSerializer.Deserialize<WeComTemplateCardTextNotice>(Payload);

        Assert.NotNull(card);
        Assert.Equal("text_notice", card!.CardType);
        Assert.Equal("标题", card.MainTitle!.Title);
        Assert.Equal("辅助", card.MainTitle.Desc);
        Assert.Equal("正文", card.SubTitleText);
        Assert.Equal("k", card.HorizontalContents![0].KeyName);
        Assert.Equal("跳转", card.Jumps![0].Title);
        Assert.Equal("https://example.com", card.CardAction.Url);
    }

    private static JsonNode ToJson(object value)
    {
        var node = JsonNode.Parse(JsonSerializer.Serialize(value, value.GetType()));
        Assert.NotNull(node);
        return node!;
    }

    private static string[] ToStringArray(JsonNode? node)
    {
        Assert.NotNull(node);
        return node!.AsArray().Select(item => item!.GetValue<string>()).ToArray();
    }
}
