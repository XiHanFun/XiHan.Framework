// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Bot.Lark.Models;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Bot.Lark.Tests.Models;

/// <summary>
/// 飞书消息模型与标签枚举测试
/// </summary>
/// <remarks>
/// 这里断言的是「飞书开放平台线上协议」：msg_type 取值、标签 tag 取值、JSON 字段名。
/// 这些都是跨进程契约，改一个字符就会被飞书判为请求体格式错误（9499），因此按字面量锁死。
/// 富文本 Content 的元素声明类型是接口，直接序列化 LarkPost 会丢子类字段，
/// 所以只覆盖 LarkBot 实际使用的 List&lt;List&lt;object&gt;&gt; 投影形态。
/// </remarks>
public class LarkModelTests
{
    /// <summary>
    /// 文本消息默认值与 JSON 字段名
    /// </summary>
    [Fact]
    public void LarkText_Always_SerializesToTextField()
    {
        Assert.Equal(string.Empty, new LarkText().Text);
        Assert.Equal("{\"text\":\"hi\"}", JsonSerializer.Serialize(new LarkText { Text = "hi" }));
    }

    /// <summary>
    /// 文本消息可完成 JSON 往返
    /// </summary>
    [Fact]
    public void LarkText_WhenRoundTripped_KeepsText()
    {
        var restored = JsonSerializer.Deserialize<LarkText>("{\"text\":\"hello lark\"}");

        Assert.NotNull(restored);
        Assert.Equal("hello lark", restored.Text);
    }

    /// <summary>
    /// 富文本消息默认值与 JSON 字段名
    /// </summary>
    [Fact]
    public void LarkPost_Always_HasEmptyTitleAndContent()
    {
        var post = new LarkPost();

        Assert.Equal(string.Empty, post.Title);
        Assert.NotNull(post.Content);
        Assert.Empty(post.Content);
        Assert.Equal("{\"title\":\"\",\"content\":[]}", JsonSerializer.Serialize(post));
    }

    /// <summary>
    /// 图片消息默认值与 JSON 字段名
    /// </summary>
    [Fact]
    public void LarkImage_Always_SerializesToImageKeyField()
    {
        Assert.Equal(string.Empty, new LarkImage().ImageKey);
        Assert.Equal("{\"image_key\":\"img_v2_abc\"}", JsonSerializer.Serialize(new LarkImage { ImageKey = "img_v2_abc" }));
    }

    /// <summary>
    /// 图片消息可完成 JSON 往返
    /// </summary>
    [Fact]
    public void LarkImage_WhenRoundTripped_KeepsImageKey()
    {
        var restored = JsonSerializer.Deserialize<LarkImage>("{\"image_key\":\"img_v2_abc\"}");

        Assert.NotNull(restored);
        Assert.Equal("img_v2_abc", restored.ImageKey);
    }

    /// <summary>
    /// 消息卡片默认结构可直接序列化，标题节点不为空
    /// </summary>
    /// <remarks>
    /// LarkBot.InterActiveMessage 会无条件访问 Header.Title.Content 拼关键字，
    /// 默认实例必须保证这条链路上没有 null，否则关键字场景直接空引用。
    /// </remarks>
    [Fact]
    public void LarkInterActive_Always_HasNonNullHeaderTitleChain()
    {
        var card = new LarkInterActive();

        Assert.NotNull(card.Header);
        Assert.NotNull(card.Header.Title);
        Assert.Equal(string.Empty, card.Header.Title.Content);
        Assert.NotNull(card.Elements);
        Assert.Empty(card.Elements);
        Assert.Equal("{\"header\":{\"title\":{\"tag\":\"plain_text\",\"content\":\"\"}},\"elements\":[]}", JsonSerializer.Serialize(card));
    }

    /// <summary>
    /// 消息卡片元素以运行时类型序列化
    /// </summary>
    /// <remarks>
    /// Elements 声明为 List&lt;object&gt;，System.Text.Json 对 object 走运行时类型，
    /// 因此塞进去的 TagDiv 能完整落到线上载荷里。
    /// </remarks>
    [Fact]
    public void LarkInterActive_WhenElementsFilled_SerializesRuntimeType()
    {
        var card = new LarkInterActive();
        card.Elements.Add(new TagDiv
        {
            Text = new TagMarkdown { Content = "body" }
        });

        var json = JsonSerializer.Serialize(card);

        using var document = JsonDocument.Parse(json);
        var element = document.RootElement.GetProperty("elements")[0];

        Assert.Equal("div", element.GetProperty("tag").GetString());
        Assert.Equal("lark_md", element.GetProperty("text").GetProperty("tag").GetString());
        Assert.Equal("body", element.GetProperty("text").GetProperty("content").GetString());
    }

    /// <summary>
    /// 富文本标签默认 tag 与飞书取值一致
    /// </summary>
    [Fact]
    public void PostTags_Always_HaveProtocolDefaultTags()
    {
        Assert.Equal("text", new TagText().Tag);
        Assert.Equal("a", new TagA().Tag);
        Assert.Equal("at", new TagAt().Tag);
        Assert.Equal("img", new TagImg().Tag);
    }

    /// <summary>
    /// 富文本标签字段名与飞书取值一致
    /// </summary>
    [Fact]
    public void PostTags_Always_SerializeProtocolFieldNames()
    {
        Assert.Equal("{\"tag\":\"text\",\"text\":\"line-1\"}", JsonSerializer.Serialize(new TagText { Text = "line-1" }));
        Assert.Equal("{\"tag\":\"at\",\"user_id\":\"ou_1\",\"user_name\":\"tom\"}", JsonSerializer.Serialize(new TagAt { UserId = "ou_1", UserName = "tom" }));
        Assert.Equal("{\"tag\":\"img\",\"image_key\":\"img_v2_abc\"}", JsonSerializer.Serialize(new TagImg { ImageKey = "img_v2_abc" }));
    }

    /// <summary>
    /// 超链接标签同时输出 text 与 href
    /// </summary>
    [Fact]
    public void TagA_Always_SerializesTextAndHref()
    {
        var json = JsonSerializer.Serialize(new TagA { Text = "doc", Href = "https://open.feishu.cn/" });

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("a", root.GetProperty("tag").GetString());
        Assert.Equal("doc", root.GetProperty("text").GetString());
        Assert.Equal("https://open.feishu.cn/", root.GetProperty("href").GetString());
    }

    /// <summary>
    /// 所有富文本标签都实现标签接口
    /// </summary>
    [Fact]
    public void PostTags_Always_ImplementPostTagContract()
    {
        var tags = new IPostTag[]
        {
            new TagText(),
            new TagA(),
            new TagAt(),
            new TagImg()
        };

        Assert.All(tags, tag => Assert.False(string.IsNullOrWhiteSpace(tag.Tag)));
        Assert.True(typeof(IPostTag).IsAssignableTo(typeof(ITag)));
        Assert.True(typeof(IInterActiveTag).IsAssignableTo(typeof(ITag)));
    }

    /// <summary>
    /// 消息卡片标签默认 tag 与飞书取值一致
    /// </summary>
    [Fact]
    public void InterActiveTags_Always_HaveProtocolDefaultTags()
    {
        Assert.Equal("plain_text", new TagTitleOrText().Tag);
        Assert.Equal("lark_md", new TagMarkdown().Tag);
        Assert.Equal("div", new TagDiv().Tag);
        Assert.Equal("button", new TagButton().Tag);
        Assert.Equal("action", new TagAction().Tag);
    }

    /// <summary>
    /// 消息卡片标签字段名与飞书取值一致
    /// </summary>
    [Fact]
    public void InterActiveTags_Always_SerializeProtocolFieldNames()
    {
        Assert.Equal("{\"tag\":\"plain_text\",\"content\":\"title\"}", JsonSerializer.Serialize(new TagTitleOrText { Content = "title" }));
        Assert.Equal("{\"tag\":\"lark_md\",\"content\":\"card body\"}", JsonSerializer.Serialize(new TagMarkdown { Content = "card body" }));
        Assert.Equal("{\"tag\":\"div\",\"text\":{\"tag\":\"lark_md\",\"content\":\"\"}}", JsonSerializer.Serialize(new TagDiv()));
        Assert.Equal("{\"tag\":\"action\",\"actions\":[]}", JsonSerializer.Serialize(new TagAction()));
    }

    /// <summary>
    /// 动作标签承载按钮集合
    /// </summary>
    [Fact]
    public void TagAction_WhenButtonsAdded_SerializesButtonList()
    {
        var action = new TagAction();
        action.Actions.Add(new TagButton
        {
            Text = new TagMarkdown { Content = "open" },
            Url = "https://open.feishu.cn/"
        });

        var json = JsonSerializer.Serialize(action);

        using var document = JsonDocument.Parse(json);
        var button = document.RootElement.GetProperty("actions")[0];

        Assert.Equal("button", button.GetProperty("tag").GetString());
        Assert.Equal("open", button.GetProperty("text").GetProperty("content").GetString());
        Assert.Equal("https://open.feishu.cn/", button.GetProperty("url").GetString());
        Assert.True(button.TryGetProperty("value", out _));
    }

    /// <summary>
    /// 按钮标签默认样式必须是飞书允许的取值
    /// </summary>
    /// <remarks>
    /// 疑似缺陷：源码 Models/LarkModel.cs 里 TagButton.Type 默认值写成了 "defult"（拼写错误），
    /// 飞书按钮 type 只接受 default / primary / danger。这里按协议正确语义断言，不迁就现状。
    /// </remarks>
    [Fact]
    public void TagButton_Default_TypeIsProtocolDefault()
    {
        Assert.Equal("default", new TagButton().Type);
    }

    /// <summary>
    /// 按钮标签的其余默认值不为 null
    /// </summary>
    [Fact]
    public void TagButton_Default_HasNonNullTextAndValue()
    {
        var button = new TagButton();

        Assert.NotNull(button.Text);
        Assert.Equal(string.Empty, button.Url);
        Assert.NotNull(button.Value);
    }

    /// <summary>
    /// 消息类型枚举描述即飞书 msg_type 线上取值
    /// </summary>
    [Fact]
    public void LarkMsgTypeEnum_Descriptions_MatchWireValues()
    {
        Assert.Equal("text", LarkMsgTypeEnum.Text.GetDescription());
        Assert.Equal("post", LarkMsgTypeEnum.Post.GetDescription());
        Assert.Equal("image", LarkMsgTypeEnum.Image.GetDescription());
        Assert.Equal("interactive", LarkMsgTypeEnum.InterActive.GetDescription());
    }

    /// <summary>
    /// 富文本标签枚举描述即飞书 tag 线上取值
    /// </summary>
    [Fact]
    public void LarkPostTagEnum_Descriptions_MatchWireValues()
    {
        Assert.Equal("text", LarkPostTagEnum.Text.GetDescription());
        Assert.Equal("a", LarkPostTagEnum.A.GetDescription());
        Assert.Equal("at", LarkPostTagEnum.At.GetDescription());
        Assert.Equal("img", LarkPostTagEnum.Image.GetDescription());
    }

    /// <summary>
    /// 消息卡片标签枚举描述即飞书 tag 线上取值
    /// </summary>
    [Fact]
    public void LarkInterActiveTagEnum_Descriptions_MatchWireValues()
    {
        Assert.Equal("lark_md", LarkInterActiveTagEnum.Markdown.GetDescription());
        Assert.Equal("plain_text", LarkInterActiveTagEnum.PlainText.GetDescription());
        Assert.Equal("div", LarkInterActiveTagEnum.Div.GetDescription());
        Assert.Equal("button", LarkInterActiveTagEnum.Button.GetDescription());
        Assert.Equal("action", LarkInterActiveTagEnum.Action.GetDescription());
    }

    /// <summary>
    /// 三个标签枚举的成员数量固定
    /// </summary>
    [Fact]
    public void TagEnums_Always_HaveFixedMemberCount()
    {
        Assert.Equal(4, Enum.GetValues<LarkMsgTypeEnum>().Length);
        Assert.Equal(4, Enum.GetValues<LarkPostTagEnum>().Length);
        Assert.Equal(5, Enum.GetValues<LarkInterActiveTagEnum>().Length);
    }

    /// <summary>
    /// 富文本内容投影成 object 列表后能完整落到线上载荷
    /// </summary>
    /// <remarks>
    /// 这是 LarkBot.PostMessage 的真实形态：先把 IPostTag 拆解成 List&lt;List&lt;object&gt;&gt;，
    /// 再交给序列化器，这样 System.Text.Json 才会按运行时类型输出子类字段。
    /// </remarks>
    [Fact]
    public void PostContent_WhenProjectedToObjectList_SerializesFullTagPayload()
    {
        var content = new List<List<object>>
        {
            new List<object>
            {
                new TagText { Text = "line-1" },
                new TagA { Text = "doc", Href = "https://open.feishu.cn/" }
            }
        };

        var json = JsonSerializer.Serialize(content);

        using var document = JsonDocument.Parse(json);
        var line = document.RootElement[0];

        Assert.Equal(2, line.GetArrayLength());
        Assert.Equal("text", line[0].GetProperty("tag").GetString());
        Assert.Equal("line-1", line[0].GetProperty("text").GetString());
        Assert.Equal("a", line[1].GetProperty("tag").GetString());
        Assert.Equal("https://open.feishu.cn/", line[1].GetProperty("href").GetString());
    }
}
