#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IBotTemplateEngine
// Guid:efc5dff7-128f-4002-9a89-71e07069e1cc
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 18:15:43
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
