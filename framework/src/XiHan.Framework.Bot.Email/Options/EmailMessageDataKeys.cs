#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EmailMessageDataKeys
// Guid:6a367f5c-d80e-441d-be06-125711718865
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
