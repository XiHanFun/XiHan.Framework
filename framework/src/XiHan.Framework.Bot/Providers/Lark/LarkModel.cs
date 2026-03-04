#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LarkModel
// Guid:9fcd68c0-c102-49c9-9ca4-73a6e58c3832
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/05 01:08:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel;
using System.Text.Json.Serialization;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Bot.Providers.Lark;

#region 基本类型

/// <summary>
/// 文本类型
/// </summary>
public class LarkText
{
    /// <summary>
    /// 消息文本
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// 富文本类型
/// </summary>
public class LarkPost
{
    /// <summary>
    /// 消息标题
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { set; get; } = string.Empty;

    /// <summary>
    /// 消息内容
    /// </summary>
    [JsonPropertyName("content")]
    public List<List<IPostTag>> Content { get; set; } = [];
}

/// <summary>
/// 图片类型
/// </summary>
public class LarkImage
{
    /// <summary>
    /// 图片的唯一标识
    /// </summary>
    [JsonPropertyName("image_key")]
    public string ImageKey { set; get; } = string.Empty;
}

/// <summary>
/// 消息卡片类型
/// </summary>
public class LarkInterActive
{
    /// <summary>
    /// 头部
    /// </summary>
    [JsonPropertyName("header")]
    public InterActiveHeader Header { set; get; } = new InterActiveHeader();

    /// <summary>
    /// 节点
    /// </summary>
    [JsonPropertyName("elements")]
    public List<object> Elements { set; get; } = [];
}

#endregion 基本类型

#region 辅助类

/// <summary>
/// 标签接口
/// </summary>
public interface ITag
{
    /// <summary>
    /// 标签类型
    /// </summary>
    string Tag { get; set; }
}

#region Post

/// <summary>
/// Post标签接口
/// </summary>
public interface IPostTag : ITag;

/// <summary>
/// 文本标签
/// </summary>
public class TagText : IPostTag
{
    /// <summary>
    /// 标签类型
    /// </summary>
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = LarkPostTagEnum.Text.GetDescription();

    /// <summary>
    /// 消息文本
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// 超链接标签
/// </summary>
public class TagA : IPostTag
{
    /// <summary>
    /// 标签类型
    /// </summary>
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = LarkPostTagEnum.A.GetDescription();

    /// <summary>
    /// 消息文本
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 消息链接
    /// </summary>
    [JsonPropertyName("href")]
    public string Href { get; set; } = string.Empty;
}

/// <summary>
/// @ 标签
/// </summary>
public class TagAt : IPostTag
{
    /// <summary>
    /// 标签类型
    /// </summary>
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = LarkPostTagEnum.At.GetDescription();

    /// <summary>
    /// 用户ID
    /// </summary>
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// 用户名称
    /// </summary>
    [JsonPropertyName("user_name")]
    public string UserName { get; set; } = string.Empty;
}

/// <summary>
/// 图片标签
/// </summary>
public class TagImg : IPostTag
{
    /// <summary>
    /// 标签类型
    /// </summary>
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = LarkPostTagEnum.Image.GetDescription();

    /// <summary>
    /// 图片的唯一标识
    /// </summary>
    [JsonPropertyName("image_key")]
    public string ImageKey { set; get; } = string.Empty;
}

#endregion

#region InterActive

/// <summary>
/// InterActive标签接口
/// </summary>
public interface IInterActiveTag : ITag;

/// <summary>
/// 头部
/// </summary>
public class InterActiveHeader
{
    /// <summary>
    /// 标题
    /// </summary>
    [JsonPropertyName("title")]
    public TagTitleOrText Title { set; get; } = new TagTitleOrText();
}

/// <summary>
/// 头部或内容标签
/// </summary>
public class TagTitleOrText : IInterActiveTag
{
    /// <summary>
    /// 标签类型
    /// </summary>
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = LarkInterActiveTagEnum.PlainText.GetDescription();

    /// <summary>
    /// 内容
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Markdown标签
/// </summary>
public class TagMarkdown : IInterActiveTag
{
    /// <summary>
    /// 标签类型
    /// </summary>
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = LarkInterActiveTagEnum.Markdown.GetDescription();

    /// <summary>
    /// 内容
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 容器标签
/// </summary>
public class TagDiv : IInterActiveTag
{
    /// <summary>
    /// 标签类型
    /// </summary>
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = LarkInterActiveTagEnum.Div.GetDescription();

    /// <summary>
    /// 内容
    /// </summary>
    [JsonPropertyName("text")]
    public TagMarkdown Text { set; get; } = new TagMarkdown();
}

/// <summary>
/// 动作标签
/// </summary>
public class TagAction : IInterActiveTag
{
    /// <summary>
    /// 标签类型
    /// </summary>
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = LarkInterActiveTagEnum.Action.GetDescription();

    /// <summary>
    /// 内容
    /// </summary>
    [JsonPropertyName("actions")]
    public List<TagButton> Actions { get; set; } = [];
}

/// <summary>
/// 按钮标签
/// </summary>
public class TagButton : IInterActiveTag
{
    /// <summary>
    /// 标签类型
    /// </summary>
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = LarkInterActiveTagEnum.Button.GetDescription();

    /// <summary>
    /// 内容
    /// </summary>
    [JsonPropertyName("text")]
    public TagMarkdown Text { set; get; } = new TagMarkdown();

    /// <summary>
    /// 链接
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { set; get; } = string.Empty;

    /// <summary>
    /// 类型
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { set; get; } = "defult";

    /// <summary>
    /// 值
    /// </summary>
    [JsonPropertyName("value")]
    public object Value { set; get; } = new object();
}

#endregion

#endregion 辅助类

#region 枚举

/// <summary>
/// 消息类型枚举
/// </summary>
public enum LarkMsgTypeEnum
{
    /// <summary>
    /// 文本类型
    /// </summary>
    [Description("text")]
    Text,

    /// <summary>
    /// 富文本类型
    /// </summary>
    [Description("post")]
    Post,

    /// <summary>
    /// 图片类型
    /// </summary>
    [Description("image")]
    Image,

    /// <summary>
    /// 消息卡片类型
    /// </summary>
    [Description("interactive")]
    InterActive
}

/// <summary>
/// Post标签类型枚举
/// </summary>
public enum LarkPostTagEnum
{
    /// <summary>
    /// 文本标签
    /// </summary>
    [Description("text")]
    Text,

    /// <summary>
    /// 超链接标签
    /// </summary>
    [Description("a")]
    A,

    /// <summary>
    /// @ 标签
    /// </summary>
    [Description("at")]
    At,

    /// <summary>
    /// 图片标签
    /// </summary>
    [Description("img")]
    Image
}

/// <summary>
/// InterActive标签类型枚举
/// </summary>
public enum LarkInterActiveTagEnum
{
    /// <summary>
    /// Markdown
    /// </summary>
    [Description("lark_md")]
    Markdown,

    /// <summary>
    /// 明文
    /// </summary>
    [Description("plain_text")]
    PlainText,

    /// <summary>
    /// 容器
    /// </summary>
    [Description("div")]
    Div,

    /// <summary>
    /// 按钮
    /// </summary>
    [Description("button")]
    Button,

    /// <summary>
    /// 动作
    /// </summary>
    [Description("action")]
    Action
}

#endregion
