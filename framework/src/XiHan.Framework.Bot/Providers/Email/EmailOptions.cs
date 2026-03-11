#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EmailOptions
// Guid:2dd34969-3cba-4f78-a411-4471c2d704c9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 17:48:44
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Bot.Providers.Email;

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
