// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Enums;

namespace XiHan.Framework.Bot.Models;

/// <summary>
/// 统一 Bot 消息
/// </summary>
public class BotMessage
{
    /// <summary>
    /// 消息标题
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 消息内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 消息类型
    /// </summary>
    public BotMessageType Type { get; set; } = BotMessageType.Text;

    /// <summary>
    /// @ 提及列表
    /// </summary>
    public List<string> Mentions { get; set; } = [];

    /// <summary>
    /// 各提供者扩展数据
    /// </summary>
    public Dictionary<string, object?> Data { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
