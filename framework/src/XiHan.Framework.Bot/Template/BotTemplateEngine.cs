// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly XiHanBotOptions _options;

    /// <summary>
    /// 创建模板引擎
    /// </summary>
    public BotTemplateEngine(IServiceScopeFactory serviceScopeFactory, IOptions<XiHanBotOptions> options)
    {
        _serviceScopeFactory = serviceScopeFactory;
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
        using var scope = _serviceScopeFactory.CreateScope();
        var templateService = scope.ServiceProvider.GetRequiredService<ITemplateService>();

        var content = await templateService.RenderAsync(template.Content, model);
        var title = template.Title;

        if (!string.IsNullOrWhiteSpace(title))
        {
            title = await templateService.RenderAsync(title, model);
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
