// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Models;

namespace XiHan.Framework.Bot.Template;

/// <summary>
/// Bot 模板引擎抽象
/// </summary>
public interface IBotTemplateEngine
{
    /// <summary>
    /// 按名称渲染模板
    /// </summary>
    Task<BotMessage> RenderAsync(string templateName, object? model = null);

    /// <summary>
    /// 渲染模板实例
    /// </summary>
    Task<BotMessage> RenderAsync(BotTemplate template, object? model = null);
}
