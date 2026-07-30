// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
