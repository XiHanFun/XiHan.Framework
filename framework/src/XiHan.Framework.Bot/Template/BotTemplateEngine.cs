#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BotTemplateEngine
// Guid:c881d74f-c451-499c-943c-376d9749da1e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 17:46:02
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Options;
using XiHan.Framework.Templating.Services;

namespace XiHan.Framework.Bot.Template;

/// <summary>
/// Bot 模板引擎
/// </summary>
public class BotTemplateEngine : IBotTemplateEngine
{
    private readonly ITemplateService _templateService;
    private readonly XiHanBotOptions _options;

    /// <summary>
    /// 创建模板引擎
    /// </summary>
    public BotTemplateEngine(ITemplateService templateService, IOptions<XiHanBotOptions> options)
    {
        _templateService = templateService;
        _options = options.Value;
    }

    /// <summary>
    /// 按名称渲染模板
    /// </summary>
    public Task<BotMessage> RenderAsync(string templateName, object? model = null)
    {
        if (!_options.Templates.TryGetValue(templateName, out var template))
        {
            throw new InvalidOperationException($"Bot template '{templateName}' is not configured.");
        }

        return RenderAsync(template, model);
    }

    /// <summary>
    /// 渲染模板实例
    /// </summary>
    public async Task<BotMessage> RenderAsync(BotTemplate template, object? model = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        var content = await _templateService.RenderAsync(template.Content, model);
        var title = template.Title;

        if (!string.IsNullOrWhiteSpace(title))
        {
            title = await _templateService.RenderAsync(title, model);
        }

        var data = template.Data is null
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object?>(template.Data, StringComparer.OrdinalIgnoreCase);

        var message = new BotMessage
        {
            Title = title,
            Content = content,
            Type = template.Type,
            Data = data
        };

        return message;
    }
}
