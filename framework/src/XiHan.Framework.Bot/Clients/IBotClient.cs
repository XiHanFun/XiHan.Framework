#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IBotClient
// Guid:ba79409a-7034-44e2-bee3-60a6aa47ce95
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 17:43:25
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Bot.Models;

namespace XiHan.Framework.Bot.Clients;

/// <summary>
/// Bot 客户端入口
/// </summary>
public interface IBotClient
{
    /// <summary>
    /// 向所有提供者发送消息
    /// </summary>
    Task SendAsync(BotMessage message);

    /// <summary>
    /// 向指定渠道/提供者发送消息
    /// </summary>
    Task SendAsync(BotMessage message, params string[] channels);

    /// <summary>
    /// 按模板名称发送
    /// </summary>
    Task SendTemplateAsync(string templateName, object? model = null, params string[] channels);

    /// <summary>
    /// 批量发送消息
    /// </summary>
    Task SendBatchAsync(IEnumerable<BotMessage> messages, params string[] channels);

    /// <summary>
    /// 延迟发送消息
    /// </summary>
    Task SendDelayedAsync(BotMessage message, TimeSpan delay, params string[] channels);
}
