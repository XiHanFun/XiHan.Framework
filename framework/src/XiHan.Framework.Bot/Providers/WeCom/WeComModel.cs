#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WeComModel
// Guid:479a9594-c7fb-42e7-bc6a-2c51c7e58b28
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/05 01:08:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel;
using System.Text.Json.Serialization;

namespace XiHan.Framework.Bot.Providers.WeCom;

#region 基本类型

/// <summary>
/// 文本类型
/// </summary>
public class WeComText : WeComAt
{
    /// <summary>
    /// 消息文本，最长不超过2048个字节，必须是utf8编码
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 文档类型
/// </summary>
public class WeComMarkdown
{
    /// <summary>
    /// Markdown格式的消息，最长不超过4096个字节，必须是utf8编码
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 图片类型，base64编码前最大不能超过2M，支持JPG、PNG格式
/// </summary>
public class WeComImage
{
    /// <summary>
    /// 图片内容(base64编码前)的md5值
    /// </summary>
    [JsonPropertyName("md5")]
    public string Md5 { get; set; } = string.Empty;

    /// <summary>
    /// 图片内容的base64编码
    /// </summary>
    [JsonPropertyName("base64")]
    public string Base64 { get; set; } = string.Empty;
}

/// <summary>
/// 图文类型
/// </summary>
public class WeComNews
{
    /// <summary>
    /// 图文消息，一个图文消息支持1到8条图文
    /// </summary>
    [JsonPropertyName("articles")]
    public List<WeComArticle>? Articles { get; set; }
}

/// <summary>
/// 文件类型
/// </summary>
public class WeComFile
{
    /// <summary>
    /// 文件id，通过下文的文件上传接口获取
    /// </summary>
    [JsonPropertyName("media_id")]
    public string MediaId { get; set; } = string.Empty;
}

/// <summary>
/// 语音类型
/// </summary>
public class WeComVoice
{
    /// <summary>
    /// 文件id，通过下文的文件上传接口获取
    /// </summary>
    [JsonPropertyName("media_id")]
    public string MediaId { get; set; } = string.Empty;
}

/// <summary>
/// 文本通知模版卡片内容
/// </summary>
public class WeComTemplateCardTextNotice
{
    /// <summary>
    /// 模版卡片的模版类型
    /// 文本通知模版卡片的类型为text_notice
    /// </summary>
    [JsonPropertyName("card_type")]
    public string CardType { get; set; } = string.Empty;

    /// <summary>
    /// 卡片来源样式信息，非必填，不需要来源样式可不填写
    /// </summary>
    [JsonPropertyName("source")]
    public WeComSource? Source { get; set; }

    /// <summary>
    /// 模版卡片的主要内容，包括一级标题和标题辅助信息
    /// </summary>
    [JsonPropertyName("main_title")]
    public WeComMainTitle? MainTitle { get; set; }

    /// <summary>
    /// 关键数据样式，非必填
    /// </summary>
    [JsonPropertyName("emphasis_content")]
    public WeComEmphasisContent? EmphasisContent { get; set; }

    /// <summary>
    /// 引用文献样式，建议不与关键数据共用
    /// </summary>
    [JsonPropertyName("quote_area")]
    public WeComQuoteArea? QuoteArea { get; set; }

    /// <summary>
    /// 二级普通文本，非必填，建议不超过112个字
    /// 模版卡片主要内容的一级标题main_title.title和二级普通文本sub_title_text必须有一项填写
    /// </summary>
    [JsonPropertyName("sub_title_text")]
    public string? SubTitleText { get; set; }

    /// <summary>
    /// 二级标题+文本列表，该字段可为空数组，但有数据的话需确认对应字段是否必填，列表长度不超过6
    /// </summary>
    [JsonPropertyName("horizontal_content_list")]
    public List<WeComHorizontalContent>? HorizontalContents { get; set; }

    /// <summary>
    /// 跳转指引样式的列表，该字段可为空数组，但有数据的话需确认对应字段是否必填，列表长度不超过3
    /// </summary>
    [JsonPropertyName("jump_list")]
    public List<WeComJump>? Jumps { get; set; }

    /// <summary>
    /// 整体卡片的点击跳转事件，text_notice模版卡片中该字段为必填项
    /// </summary>
    [JsonPropertyName("card_action")]
    public WeComCardAction CardAction { get; set; } = new();
}

/// <summary>
/// 图文展示模版卡片内容
/// </summary>
public class WeComTemplateCardNewsNotice
{
    /// <summary>
    /// 模版卡片的模版类型
    /// 图文展示模版卡片的类型为news_notice
    /// </summary>
    [JsonPropertyName("card_type")]
    public string CardType { get; set; } = string.Empty;

    /// <summary>
    /// 卡片来源样式信息，非必填，不需要来源样式可不填写
    /// </summary>
    [JsonPropertyName("source")]
    public WeComSource? Source { get; set; }

    /// <summary>
    /// 模版卡片的主要内容，包括一级标题和标题辅助信息
    /// </summary>
    [JsonPropertyName("main_title")]
    public WeComMainTitle? MainTitle { get; set; }

    /// <summary>
    /// 图片样式
    /// </summary>
    [JsonPropertyName("card_image")]
    public WeComCardImage CardImage { get; set; } = new();

    /// <summary>
    /// 左图右文样式
    /// </summary>
    [JsonPropertyName("image_text_area")]
    public WeComImageTextArea? ImageTextArea { get; set; }

    /// <summary>
    /// 引用文献样式，建议不与关键数据共用
    /// </summary>
    [JsonPropertyName("quote_area")]
    public WeComQuoteArea? QuoteArea { get; set; }

    /// <summary>
    /// 卡片二级垂直内容，该字段可为空数组，但有数据的话需确认对应字段是否必填，列表长度不超过4
    /// </summary>
    [JsonPropertyName("vertical_content_list")]
    public List<WeComVerticalContent>? VerticalContents { get; set; }

    /// <summary>
    /// 二级标题+文本列表，该字段可为空数组，但有数据的话需确认对应字段是否必填，列表长度不超过6
    /// </summary>
    [JsonPropertyName("horizontal_content_list")]
    public List<WeComHorizontalContent>? HorizontalContents { get; set; }

    /// <summary>
    /// 跳转指引样式的列表，该字段可为空数组，但有数据的话需确认对应字段是否必填，列表长度不超过3
    /// </summary>
    [JsonPropertyName("jump_list")]
    public List<WeComJump>? Jumps { get; set; }

    /// <summary>
    /// 整体卡片的点击跳转事件，text_notice模版卡片中该字段为必填项
    /// </summary>
    [JsonPropertyName("card_action")]
    public WeComCardAction CardAction { get; set; } = new();
}

#endregion 基本类型

#region 辅助类

/// <summary>
/// @指定人(被@人的手机号和被@人的用户 userid 如非群内成员则会被自动过滤)
/// </summary>
public class WeComAt
{
    /// <summary>
    /// 被@的用户微信号，@all表示提醒所有人 示例：["wangling","@all"]
    /// </summary>
    [JsonPropertyName("mentioned_list")]
    public List<string>? Mentions { set; get; }

    /// <summary>s
    /// 被@的手机号，@all表示提醒所有人 示例：["13800001111","@all"]
    /// </summary>
    [JsonPropertyName("mentioned_mobile_list")]
    public List<string>? MentionedMobiles { set; get; }
}

/// <summary>
/// 文章
/// </summary>
public class WeComArticle
{
    /// <summary>
    /// 标题，不超过128个字节，超过会自动截断
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 描述，非必填，不超过512个字节，超过会自动截断
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// 点击后跳转的链接
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 图文消息的图片链接，非必填，支持JPG、PNG格式，较好的效果为大图 1068*455，小图150*150
    /// </summary>
    [JsonPropertyName("picurl")]
    public string? PicUrl { get; set; }
}

/// <summary>
/// 卡片来源样式信息，不需要来源样式可不填写
/// </summary>
public class WeComSource
{
    /// <summary>
    /// 来源图片的url，非必填
    /// </summary>
    [JsonPropertyName("icon_url")]
    public string? IconUrl { get; set; }

    /// <summary>
    /// 来源图片的描述，非必填，建议不超过13个字
    /// </summary>
    [JsonPropertyName("desc")]
    public string? Desc { get; set; }

    /// <summary>
    /// 来源文字的颜色，非必填，目前支持：0(默认) 灰色，1 黑色，2 红色，3 绿色
    /// </summary>
    [JsonPropertyName("desc_color")]
    public int? DescColor { get; set; } = 0;
}

/// <summary>
/// 模版卡片的主要内容
/// </summary>
public class WeComMainTitle
{
    /// <summary>
    /// 一级标题，非必填，建议不超过26个字
    /// 模版卡片主要内容的一级标题main_title.title和二级普通文本sub_title_text必须有一项填写
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// 标题辅助信息，非必填，建议不超过30个字
    /// </summary>
    [JsonPropertyName("desc")]
    public string? Desc { get; set; }
}

/// <summary>
/// 关键数据样式
/// </summary>
public class WeComEmphasisContent
{
    /// <summary>
    /// 关键数据样式的数据内容，非必填，建议不超过10个字
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// 关键数据样式的数据描述内容，非必填，建议不超过15个字
    /// </summary>
    [JsonPropertyName("desc")]
    public string? Desc { get; set; }
}

/// <summary>
/// 引用文献样式，建议不与关键数据共用
/// </summary>
public class WeComQuoteArea
{
    /// <summary>
    /// 引用文献样式区域点击事件，0或不填代表没有点击事件，1 代表跳转url，2 代表跳转小程序
    /// </summary>
    [JsonPropertyName("type")]
    public int? Type { get; set; } = 0;

    /// <summary>
    /// 点击跳转的url，quote_area.type是1时必填
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// 点击跳转的小程序的appid，quote_area.type是2时必填
    /// </summary>
    [JsonPropertyName("appid")]
    public string? AppId { get; set; }

    /// <summary>
    /// 点击跳转的小程序的pagepath，quote_area.type是2时选填
    /// </summary>
    [JsonPropertyName("pagepath")]
    public string? PagePath { get; set; }

    /// <summary>
    /// 引用文献样式的标题
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// 引用文献样式的引用文案
    /// </summary>
    [JsonPropertyName("quote_text")]
    public string? QuoteText { get; set; }
}

/// <summary>
/// 二级标题+文本
/// </summary>
public class WeComHorizontalContent
{
    /// <summary>
    /// 链接类型，0或不填代表是普通文本，1 代表跳转url，2 代表下载附件，3 代表@员工
    /// </summary>
    [JsonPropertyName("type")]
    public int? Type { get; set; }

    /// <summary>
    /// 二级标题，建议不超过5个字
    /// </summary>
    [JsonPropertyName("keyname")]
    public string KeyName { get; set; } = string.Empty;

    /// <summary>
    /// 二级文本，如果horizontal_content_list.type是2，该字段代表文件名称(要包含文件类型)，建议不超过26个字
    /// </summary>
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    /// <summary>
    /// 链接跳转的url，horizontal_content_list.type是1时必填
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// 附件的media_id，horizontal_content_list.type是2时必填
    /// </summary>
    [JsonPropertyName("media_id")]
    public string? MediaId { get; set; }

    /// <summary>
    /// 被@的成员的userid，horizontal_content_list.type是3时必填
    /// </summary>
    [JsonPropertyName("userid")]
    public string? UserId { get; set; }
}

/// <summary>
/// 跳转指引样式
/// </summary>
public class WeComJump
{
    /// <summary>
    /// 跳转链接类型，0或不填代表不是链接，1 代表跳转url，2 代表跳转小程序
    /// </summary>
    [JsonPropertyName("type")]
    public int? Type { get; set; } = 0;

    /// <summary>
    /// 跳转链接样式的文案内容，建议不超过13个字
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 跳转链接的url，jump_list.type是1时必填
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// 跳转链接的小程序的appid，jump_list.type是2时必填
    /// </summary>
    [JsonPropertyName("appid")]
    public string? AppId { get; set; }

    /// <summary>
    /// 跳转链接的小程序的pagepath，jump_list.type是2时选填
    /// </summary>
    [JsonPropertyName("pagepath")]
    public string? PagePath { get; set; }
}

/// <summary>
/// 整体卡片的点击跳转事件，text_notice模版卡片中该字段为必填项
/// </summary>
public class WeComCardAction
{
    /// <summary>
    /// 卡片跳转类型，1 代表跳转url，2 代表打开小程序。text_notice模版卡片中该字段取值范围为[1,2]
    /// </summary>
    [JsonPropertyName("type")]
    public int Type { get; set; } = 1;

    /// <summary>
    /// 跳转事件的url，card_action.type是1时必填
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// 跳转事件的小程序的appid，card_action.type是2时必填
    /// </summary>
    [JsonPropertyName("appid")]
    public string? AppId { get; set; }

    /// <summary>
    /// 跳转事件的小程序的pagepath，card_action.type是2时选填
    /// </summary>
    [JsonPropertyName("pagepath")]
    public string? PagePath { get; set; }
}

/// <summary>
/// 图片样式
/// </summary>
public class WeComCardImage
{
    /// <summary>
    /// 图片的url
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 图片的宽高比，宽高比要小于2.25，大于1.3，不填该参数默认1.3
    /// </summary>
    [JsonPropertyName("aspect_ratio")]
    public float? AspectRatio { get; set; }
}

/// <summary>
/// 左图右文样式
/// </summary>
public class WeComImageTextArea
{
    /// <summary>
    /// 左图右文样式区域点击事件，0或不填代表没有点击事件，1 代表跳转url，2 代表跳转小程序
    /// </summary>
    [JsonPropertyName("type")]
    public int? Type { get; set; } = 0;

    /// <summary>
    /// 点击跳转的url，image_text_area.type是1时必填
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// 点击跳转的小程序的appid，必须是与当前应用关联的小程序，image_text_area.type是2时必填
    /// </summary>
    [JsonPropertyName("appid")]
    public string? AppId { get; set; }

    /// <summary>
    /// 点击跳转的小程序的pagepath，image_text_area.type是2时选填
    /// </summary>
    [JsonPropertyName("pagepath")]
    public string? PagePath { get; set; }

    /// <summary>
    /// 左图右文样式的标题
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// 左图右文样式的描述
    /// </summary>
    [JsonPropertyName("desc")]
    public string? Desc { get; set; }

    /// <summary>
    /// 左图右文样式的图片url
    /// </summary>
    [JsonPropertyName("image_url")]
    public string ImageUrl { get; set; } = string.Empty;
}

/// <summary>
/// 卡片二级垂直内容，该字段可为空数组，但有数据的话需确认对应字段是否必填，列表长度不超过4
/// </summary>
public class WeComVerticalContent
{
    /// <summary>
    /// 卡片二级标题，建议不超过26个字
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 二级普通文本，建议不超过112个字
    /// </summary>
    [JsonPropertyName("desc")]
    public string? Desc { get; set; }
}

#endregion 辅助类

#region 枚举

/// <summary>
/// 消息类型枚举
/// </summary>
public enum WeComMsgTypeEnum
{
    /// <summary>
    /// 文本类型
    /// </summary>
    [Description("text")]
    Text,

    /// <summary>
    /// 文档类型
    /// </summary>
    [Description("markdown")]
    Markdown,

    /// <summary>
    /// 图片类型
    /// </summary>
    [Description("image")]
    Image,

    /// <summary>
    /// 图文类型
    /// </summary>
    [Description("news")]
    News,

    /// <summary>
    /// 文件类型
    /// </summary>
    [Description("file")]
    File,

    /// <summary>
    /// 语音类型
    /// </summary>
    [Description("voice")]
    Voice,

    /// <summary>
    /// 模版卡片类型
    /// </summary>
    [Description("template_card")]
    TemplateCard
}

/// <summary>
/// 消息类型枚举
/// </summary>
public enum WeComTemplateCardType
{
    /// <summary>
    /// 文本通知类型，属于模版卡片类型
    /// </summary>
    [Description("text_notice")]
    TextNotice,

    /// <summary>
    /// 图文展示类型，属于模版卡片类型
    /// </summary>
    [Description("news_notice")]
    NewsNotice
}

/// <summary>
/// 文件上传类型枚举
/// </summary>
public enum WeComUploadType
{
    /// <summary>
    /// 文件类型
    /// </summary>
    [Description("file")]
    File,

    /// <summary>
    /// 语音类型
    /// </summary>
    [Description("voice")]
    Voice
}

#endregion
