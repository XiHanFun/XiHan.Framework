#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BotChannel
// Guid:602bfe0e-6207-4338-81a9-4bab76317fa1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 17:43:40
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Bot.Models;

/// <summary>
/// Bot 渠道定义
/// </summary>
public class BotChannel
{
    /// <summary>
    /// 渠道名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 映射到该渠道的提供者名称列表
    /// </summary>
    public List<string> Providers { get; set; } = [];

    /// <summary>
    /// 可选描述
    /// </summary>
    public string? Description { get; set; }
}
