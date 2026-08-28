// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Bot.DingTalk.Models;

namespace XiHan.Framework.Bot.DingTalk.Tests.Models;

/// <summary>
/// 钉钉消息体模型序列化测试
/// </summary>
/// <remarks>
/// 这些模型是直接 POST 给钉钉网关的请求体，字段名由钉钉协议规定而不是由 C# 属性名规定。
/// 钉钉在不同消息类型里对"链接"字段的大小写并不一致（link 用 picUrl/messageUrl，
/// feedCard 用 picURL/messageURL，actionCard 用 singleURL，按钮用 actionURL），
/// 这类不一致最容易在重构时被"顺手统一"掉，改完编译照样通过、发出去却被钉钉判为非法参数，
/// 所以逐个字段名锁死，并额外验证一次 JSON 往返。
/// </remarks>
public class DingTalkModelTests
{
    /// <summary>
    /// 文本消息体只有 content 一个字段
    /// </summary>
    [Fact]
    public void DingTalkText_Serializes_WithContentField()
    {
        var json = JsonSerializer.Serialize(new DingTalkText { Content = "构建失败" });

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("构建失败", root.GetProperty("content").GetString());
        Assert.Single(root.EnumerateObject());
    }

    /// <summary>
    /// 文本内容默认空串，避免序列化出 null
    /// </summary>
    [Fact]
    public void DingTalkText_Content_DefaultsToEmpty()
    {
        Assert.Equal(string.Empty, new DingTalkText().Content);
    }

    /// <summary>
    /// 链接消息体字段名为 title/text/picUrl/messageUrl
    /// </summary>
    [Fact]
    public void DingTalkLink_Serializes_WithLowerCaseUrlFieldNames()
    {
        var link = new DingTalkLink
        {
            Title = "发布通知",
            Text = "版本 1.0.0 已发布",
            PicUrl = "https://example.invalid/logo.png",
            MessageUrl = "https://example.invalid/release/1"
        };

        var json = JsonSerializer.Serialize(link);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("发布通知", root.GetProperty("title").GetString());
        Assert.Equal("版本 1.0.0 已发布", root.GetProperty("text").GetString());
        Assert.Equal("https://example.invalid/logo.png", root.GetProperty("picUrl").GetString());
        Assert.Equal("https://example.invalid/release/1", root.GetProperty("messageUrl").GetString());
        Assert.Equal(4, root.EnumerateObject().Count());
    }

    /// <summary>
    /// 链接消息体 JSON 往返不丢字段
    /// </summary>
    [Fact]
    public void DingTalkLink_RoundTrip_PreservesEveryField()
    {
        var link = new DingTalkLink
        {
            Title = "发布通知",
            Text = "版本 1.0.0 已发布",
            PicUrl = "https://example.invalid/logo.png",
            MessageUrl = "https://example.invalid/release/1"
        };

        var restored = JsonSerializer.Deserialize<DingTalkLink>(JsonSerializer.Serialize(link));

        Assert.NotNull(restored);
        Assert.Equal(link.Title, restored.Title);
        Assert.Equal(link.Text, restored.Text);
        Assert.Equal(link.PicUrl, restored.PicUrl);
        Assert.Equal(link.MessageUrl, restored.MessageUrl);
    }

    /// <summary>
    /// 文档消息体字段名为 title/text
    /// </summary>
    [Fact]
    public void DingTalkMarkdown_Serializes_WithTitleAndTextFields()
    {
        var markdown = new DingTalkMarkdown
        {
            Title = "巡检报告",
            Text = "### 巡检报告\n- 全部正常"
        };

        var json = JsonSerializer.Serialize(markdown);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("巡检报告", root.GetProperty("title").GetString());
        Assert.Equal("### 巡检报告\n- 全部正常", root.GetProperty("text").GetString());
        Assert.Equal(2, root.EnumerateObject().Count());
    }

    /// <summary>
    /// 任务卡片默认按钮竖直排列且未设置任何按钮方案
    /// </summary>
    [Fact]
    public void DingTalkActionCard_Defaults_AreVerticalAndButtonless()
    {
        var card = new DingTalkActionCard();

        Assert.Equal(string.Empty, card.Title);
        Assert.Equal(string.Empty, card.Text);
        Assert.Null(card.SingleTitle);
        Assert.Null(card.SingleUrl);
        Assert.Equal("0", card.BtnOrientation);
        Assert.Null(card.Btns);
    }

    /// <summary>
    /// 任务卡片单按钮方案字段名为 singleTitle/singleURL
    /// </summary>
    [Fact]
    public void DingTalkActionCard_SingleButton_SerializesWithUpperCaseUrlFieldName()
    {
        var card = new DingTalkActionCard
        {
            Title = "审批请求",
            Text = "请审批发布单",
            SingleTitle = "查看详情",
            SingleUrl = "https://example.invalid/approval/1"
        };

        var json = JsonSerializer.Serialize(card);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("审批请求", root.GetProperty("title").GetString());
        Assert.Equal("请审批发布单", root.GetProperty("text").GetString());
        Assert.Equal("查看详情", root.GetProperty("singleTitle").GetString());
        Assert.Equal("https://example.invalid/approval/1", root.GetProperty("singleURL").GetString());
        Assert.Equal("0", root.GetProperty("btnOrientation").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("btns").ValueKind);
    }

    /// <summary>
    /// 任务卡片多按钮方案字段名为 btns/title/actionURL
    /// </summary>
    [Fact]
    public void DingTalkActionCard_MultipleButtons_SerializeWithActionUrlFieldName()
    {
        var card = new DingTalkActionCard
        {
            Title = "审批请求",
            Text = "请审批发布单",
            BtnOrientation = "1",
            Btns =
            [
                new() { Title = "同意", ActionUrl = "https://example.invalid/approve" },
                new() { Title = "驳回", ActionUrl = "https://example.invalid/reject" }
            ]
        };

        var json = JsonSerializer.Serialize(card);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("1", root.GetProperty("btnOrientation").GetString());

        var buttons = root.GetProperty("btns");

        Assert.Equal(JsonValueKind.Array, buttons.ValueKind);
        Assert.Equal(2, buttons.GetArrayLength());
        Assert.Equal("同意", buttons[0].GetProperty("title").GetString());
        Assert.Equal("https://example.invalid/approve", buttons[0].GetProperty("actionURL").GetString());
        Assert.Equal("驳回", buttons[1].GetProperty("title").GetString());
        Assert.Equal("https://example.invalid/reject", buttons[1].GetProperty("actionURL").GetString());
    }

    /// <summary>
    /// 按钮信息默认值为空串
    /// </summary>
    [Fact]
    public void DingTalkBtnInfo_Defaults_AreEmptyStrings()
    {
        var button = new DingTalkBtnInfo();

        Assert.Equal(string.Empty, button.Title);
        Assert.Equal(string.Empty, button.ActionUrl);
    }

    /// <summary>
    /// 菜单卡片链接列表默认缺省
    /// </summary>
    [Fact]
    public void DingTalkFeedCard_Links_DefaultsToNull()
    {
        Assert.Null(new DingTalkFeedCard().Links);
    }

    /// <summary>
    /// 菜单卡片链接字段名为 title/picURL/messageURL
    /// </summary>
    [Fact]
    public void DingTalkFeedCard_Serializes_WithUpperCaseUrlFieldNames()
    {
        var feedCard = new DingTalkFeedCard
        {
            Links =
            [
                new()
                {
                    Title = "构建日志",
                    PicUrl = "https://example.invalid/build.png",
                    MessageUrl = "https://example.invalid/build/1"
                }
            ]
        };

        var json = JsonSerializer.Serialize(feedCard);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var links = root.GetProperty("links");

        Assert.Equal(JsonValueKind.Array, links.ValueKind);
        Assert.Equal(1, links.GetArrayLength());
        Assert.Equal("构建日志", links[0].GetProperty("title").GetString());
        Assert.Equal("https://example.invalid/build.png", links[0].GetProperty("picURL").GetString());
        Assert.Equal("https://example.invalid/build/1", links[0].GetProperty("messageURL").GetString());
    }

    /// <summary>
    /// @ 信息默认不指定任何人也不 @ 所有人
    /// </summary>
    [Fact]
    public void DingTalkAt_Defaults_MentionNobody()
    {
        var at = new DingTalkAt();

        Assert.Null(at.AtMobiles);
        Assert.Null(at.AtUserIds);
        Assert.False(at.IsAtAll);
    }

    /// <summary>
    /// @ 信息字段名为 atMobiles/atUserIds/isAtAll
    /// </summary>
    [Fact]
    public void DingTalkAt_Serializes_WithProtocolFieldNames()
    {
        var at = new DingTalkAt
        {
            AtMobiles = ["13800000000"],
            AtUserIds = ["user01"],
            IsAtAll = true
        };

        var json = JsonSerializer.Serialize(at);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("13800000000", root.GetProperty("atMobiles")[0].GetString());
        Assert.Equal("user01", root.GetProperty("atUserIds")[0].GetString());
        Assert.True(root.GetProperty("isAtAll").GetBoolean());
        Assert.Equal(3, root.EnumerateObject().Count());
    }

    /// <summary>
    /// @ 信息 JSON 往返不丢集合内容
    /// </summary>
    [Fact]
    public void DingTalkAt_RoundTrip_PreservesMentions()
    {
        var at = new DingTalkAt
        {
            AtMobiles = ["13800000000", "13900000000"],
            IsAtAll = false
        };

        var restored = JsonSerializer.Deserialize<DingTalkAt>(JsonSerializer.Serialize(at));

        Assert.NotNull(restored);
        Assert.Equal(new[] { "13800000000", "13900000000" }, restored.AtMobiles);
        Assert.Null(restored.AtUserIds);
        Assert.False(restored.IsAtAll);
    }
}
