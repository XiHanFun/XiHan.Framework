#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BotClient
// Guid:8fa684fa-d5f1-41fa-94c7-3156c47e9974
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 17:44:57
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Template;

namespace XiHan.Framework.Bot.Clients;

/// <summary>
/// Bot 客户端
/// </summary>
public class BotClient : IBotClient
{
    private readonly BotDispatcher _dispatcher;
    private readonly IBotTemplateEngine _templateEngine;

    /// <summary>
    /// 创建客户端
    /// </summary>
    public BotClient(BotDispatcher dispatcher, IBotTemplateEngine templateEngine)
    {
        _dispatcher = dispatcher;
        _templateEngine = templateEngine;
    }

    /// <summary>
    /// 向所有提供者发送消息
    /// </summary>
    public Task SendAsync(BotMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return _dispatcher.DispatchAsync(message);
    }

    /// <summary>
    /// 向指定渠道/提供者发送消息
    /// </summary>
    public Task SendAsync(BotMessage message, params string[] channels)
    {
        ArgumentNullException.ThrowIfNull(message);
        return _dispatcher.DispatchAsync(message, channels);
    }

    /// <summary>
    /// 按模板名称发送
    /// </summary>
    public async Task SendTemplateAsync(string templateName, object? model = null, params string[] channels)
    {
        var message = await _templateEngine.RenderAsync(templateName, model);
        await SendAsync(message, channels);
    }

    /// <summary>
    /// 批量发送消息
    /// </summary>
    public async Task SendBatchAsync(IEnumerable<BotMessage> messages, params string[] channels)
    {
        ArgumentNullException.ThrowIfNull(messages);
        foreach (var message in messages)
        {
            await SendAsync(message, channels);
        }
    }

    /// <summary>
    /// 延迟发送消息
    /// </summary>
    public async Task SendDelayedAsync(BotMessage message, TimeSpan delay, params string[] channels)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay);
        }

        await SendAsync(message, channels);
    }
}
