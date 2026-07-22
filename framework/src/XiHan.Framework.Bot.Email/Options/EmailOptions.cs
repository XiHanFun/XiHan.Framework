// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Email.Models;

namespace XiHan.Framework.Bot.Email.Options;

/// <summary>
/// 邮件提供者配置
/// </summary>
public class EmailOptions
{
    /// <summary>
    /// 是否启用该提供者
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 发件人配置
    /// </summary>
    public EmailFromModel From { get; set; } = new();

    /// <summary>
    /// 默认收件人列表
    /// </summary>
    public List<string> To { get; set; } = [];

    /// <summary>
    /// 默认抄送列表
    /// </summary>
    public List<string> Cc { get; set; } = [];

    /// <summary>
    /// 默认密送列表
    /// </summary>
    public List<string> Bcc { get; set; } = [];

    /// <summary>
    /// 是否使用 HTML 正文
    /// </summary>
    public bool IsBodyHtml { get; set; } = true;
}
