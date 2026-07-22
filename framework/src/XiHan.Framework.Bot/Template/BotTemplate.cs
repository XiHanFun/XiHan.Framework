// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Enums;

namespace XiHan.Framework.Bot.Template;

/// <summary>
/// Bot 模板定义
/// </summary>
public class BotTemplate
{
    /// <summary>
    /// 模板名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 模板标题
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 模板内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 消息类型
    /// </summary>
    public BotMessageType Type { get; set; } = BotMessageType.Markdown;

    /// <summary>
    /// 扩展数据
    /// </summary>
    public Dictionary<string, object?> Data { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
