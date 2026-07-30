// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Bot.Email.Options;

/// <summary>
/// 邮件消息 Data 键名常量
/// </summary>
public static class EmailMessageDataKeys
{
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
}
