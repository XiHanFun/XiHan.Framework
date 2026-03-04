#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DingTalkModel
// Guid:5d00cc16-5e63-4fd4-9052-54068c536acf
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/05 01:08:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel;
using System.Text.Json.Serialization;

namespace XiHan.Framework.Bot.Providers.DingTalk;

#region 基本类型

/// <summary>
/// 文本类型
/// </summary>
public class DingTalkText
{
    /// <summary>
    /// 消息文本
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 链接类型
/// </summary>
public class DingTalkLink
{
    /// <summary>
    /// 消息标题
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { set; get; } = string.Empty;

    /// <summary>
    /// 消息内容
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { set; get; } = string.Empty;

    /// <summary>
    /// 图片链接
    /// </summary>
    [JsonPropertyName("picUrl")]
    public string PicUrl { set; get; } = string.Empty;

    /// <summary>
    /// 点击消息跳转的链接
    /// </summary>
    [JsonPropertyName("messageUrl")]
    public string MessageUrl { set; get; } = string.Empty;
}

/// <summary>
/// 文档类型
/// </summary>
public class DingTalkMarkdown
{
    /// <summary>
    /// 首屏会话透出的展示内容
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { set; get; } = string.Empty;

    /// <summary>
    /// Markdown格式的消息
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { set; get; } = string.Empty;
}

/// <summary>
/// 任务卡片类型
/// 单个按钮方案：必须属性有 SingleTitle、SingleUrl
/// 多个按钮方案：必须属性有 BtnOrientation、Btns
/// 两种方案二选一，设置单个按钮方案后多个按钮方案会无效
/// </summary>
public class DingTalkActionCard
{
    /// <summary>
    /// 首屏会话透出的展示内容
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { set; get; } = string.Empty;

    /// <summary>
    /// Markdown格式的消息
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { set; get; } = string.Empty;

    /// <summary>
    /// 单个按钮方案：按钮标题
    /// </summary>
    [JsonPropertyName("singleTitle")]
    public string? SingleTitle { set; get; }

    /// <summary>
    /// 单个按钮方案：按钮触发的链接
    /// </summary>
    [JsonPropertyName("singleURL")]
    public string? SingleUrl { set; get; }

    /// <summary>
    /// 多个按钮方案：按钮排列，0-按钮竖直排列，1-按钮横向排列
    /// </summary>
    [JsonPropertyName("btnOrientation")]
    public string? BtnOrientation { set; get; } = "0";

    /// <summary>
    /// 多个按钮方案：按钮信息
    /// </summary>
    [JsonPropertyName("btns")]
    public List<DingTalkBtnInfo>? Btns { set; get; }
}

/// <summary>
/// 菜单卡片类型
/// </summary>
public class DingTalkFeedCard
{
    /// <summary>
    /// 链接列表
    /// </summary>
    [JsonPropertyName("links")]
    public List<DingTalkFeedCardLink>? Links { get; set; }
}

#endregion 基本类型

#region 辅助类

/// <summary>
/// 菜单卡片类型链接
/// </summary>
public class DingTalkFeedCardLink
{
    /// <summary>
    /// 消息标题
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { set; get; } = string.Empty;

    /// <summary>
    /// 图片链接
    /// </summary>
    [JsonPropertyName("picURL")]
    public string PicUrl { set; get; } = string.Empty;

    /// <summary>
    /// 点击消息跳转的链接
    /// </summary>
    [JsonPropertyName("messageURL")]
    public string MessageUrl { set; get; } = string.Empty;
}

/// <summary>
/// @指定人(被@人的手机号和被@人的用户 userid 如非群内成员则会被自动过滤)
/// </summary>
public class DingTalkAt
{
    /// <summary>
    /// 被@的手机号
    /// </summary>
    [JsonPropertyName("atMobiles")]
    public List<string>? AtMobiles { set; get; }

    /// <summary>
    /// 被@的用户ID
    /// </summary>
    [JsonPropertyName("atUserIds")]
    public List<string>? AtUserIds { set; get; }

    /// <summary>
    /// 是否@所有人(如要 @所有人为 true，反之用 false)
    /// </summary>
    [JsonPropertyName("isAtAll")]
    public bool IsAtAll { set; get; }
}

/// <summary>
/// 按钮信息
/// </summary>
public class DingTalkBtnInfo
{
    /// <summary>
    /// 按钮标题
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 动作触发的链接
    /// </summary>
    [JsonPropertyName("actionURL")]
    public string ActionUrl { get; set; } = string.Empty;
}

#endregion 辅助类

#region 枚举

/// <summary>
/// 消息类型枚举
/// </summary>
public enum DingTalkMsgTypeEnum
{
    /// <summary>
    /// 文本类型
    /// </summary>
    [Description("text")]
    Text,

    /// <summary>
    /// 链接类型
    /// </summary>
    [Description("link")]
    Link,

    /// <summary>
    /// 文档类型
    /// </summary>
    [Description("markdown")]
    Markdown,

    /// <summary>
    /// 任务卡片类型
    /// </summary>
    [Description("actionCard")]
    ActionCard,

    /// <summary>
    /// 菜单卡片类型
    /// </summary>
    [Description("feedCard")]
    FeedCard
}

#endregion
