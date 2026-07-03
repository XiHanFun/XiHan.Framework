#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EmailModel
// Guid:1e770e70-0a2a-4f0c-8537-9c1bce1966ef
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/05 01:08:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Net.Mail;
using System.Text;

namespace XiHan.Framework.Bot.Email.Models;

/// <summary>
/// EmailFromModel
/// </summary>
public class EmailFromModel
{
    /// <summary>
    /// 服务器
    /// </summary>
    public string SmtpHost { get; set; } = string.Empty;

    /// <summary>
    /// 服务器端口
    /// </summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>
    /// SSL
    /// </summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>
    /// 发自邮箱
    /// </summary>
    public string FromMail { get; set; } = string.Empty;

    /// <summary>
    /// 发自密码
    /// </summary>
    public string FromPassword { get; set; } = string.Empty;

    /// <summary>
    /// SMTP 认证登录名（多数服务商即发件邮箱；为空则不进行认证）
    /// </summary>
    public string FromUserName { get; set; } = string.Empty;

    /// <summary>
    /// 发件人显示名称（收件箱中展示的名字；为空时回退为发件邮箱）
    /// </summary>
    public string FromName { get; set; } = string.Empty;

    /// <summary>
    /// 内容编码
    /// </summary>
    public Encoding Coding { get; set; } = Encoding.UTF8;

    /// <summary>
    /// 是否接受无效/自签 TLS 证书（默认 false，按系统默认校验；仅开发环境针对自签 SMTP 放开，生产务必为 false）
    /// </summary>
    public bool AcceptInvalidCertificate { get; set; }
}

/// <summary>
/// EmailToModel
/// </summary>
public class EmailToModel
{
    /// <summary>
    /// 发送主题
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// 发送内容
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// 是否网页形式
    /// </summary>
    public bool IsBodyHtml { get; set; } = true;

    /// <summary>
    /// 接收者邮箱
    /// </summary>
    public List<string> ToMail { get; set; } = [];

    /// <summary>
    /// 抄送给邮箱
    /// </summary>
    public List<string> CcMail { get; set; } = [];

    /// <summary>
    /// 密送给邮箱
    /// </summary>
    public List<string> BccMail { get; set; } = [];

    /// <summary>
    /// 附件
    /// </summary>
    public List<Attachment> AttachmentsPath { get; set; } = [];
}
