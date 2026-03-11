#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BotMessageDataKeys
// Guid:daa27d39-1359-423d-9100-9134edb3531d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 17:44:03
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Bot.Consts;

/// <summary>
/// 消息 Data 键名常量
/// </summary>
public static class BotMessageDataKeys
{
    /// <summary>
    /// 策略名称
    /// </summary>
    public const string Strategy = "Strategy";

    /// <summary>
    /// 钉钉链接消息
    /// </summary>
    public const string DingTalkLink = "DingTalk.Link";

    /// <summary>
    /// 钉钉任务卡片
    /// </summary>
    public const string DingTalkActionCard = "DingTalk.ActionCard";

    /// <summary>
    /// 钉钉菜单卡片
    /// </summary>
    public const string DingTalkFeedCard = "DingTalk.FeedCard";

    /// <summary>
    /// 飞书富文本
    /// </summary>
    public const string LarkPost = "Lark.Post";

    /// <summary>
    /// 飞书消息卡片
    /// </summary>
    public const string LarkInterActive = "Lark.InterActive";

    /// <summary>
    /// 飞书图片
    /// </summary>
    public const string LarkImage = "Lark.Image";

    /// <summary>
    /// 企业微信图文
    /// </summary>
    public const string WeComNews = "WeCom.News";

    /// <summary>
    /// 企业微信图片
    /// </summary>
    public const string WeComImage = "WeCom.Image";

    /// <summary>
    /// 企业微信文件
    /// </summary>
    public const string WeComFile = "WeCom.File";

    /// <summary>
    /// 企业微信语音
    /// </summary>
    public const string WeComVoice = "WeCom.Voice";

    /// <summary>
    /// 企业微信文本通知模版卡片
    /// </summary>
    public const string WeComTemplateCardTextNotice = "WeCom.TemplateCardTextNotice";

    /// <summary>
    /// 企业微信图文展示模版卡片
    /// </summary>
    public const string WeComTemplateCardNewsNotice = "WeCom.TemplateCardNewsNotice";

    /// <summary>
    /// 邮件收件人
    /// </summary>
    public const string EmailTo = "Email.To";

    /// <summary>
    /// 邮件抄送
    /// </summary>
    public const string EmailCc = "Email.Cc";

    /// <summary>
    /// 邮件密送
    /// </summary>
    public const string EmailBcc = "Email.Bcc";

    /// <summary>
    /// 邮件是否 HTML 正文
    /// </summary>
    public const string EmailIsBodyHtml = "Email.IsBodyHtml";

    /// <summary>
    /// Telegram 会话 ID
    /// </summary>
    public const string TelegramChatId = "Telegram.ChatId";

    /// <summary>
    /// Telegram 解析模式
    /// </summary>
    public const string TelegramParseMode = "Telegram.ParseMode";
}
