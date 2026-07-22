// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Clients;
using XiHan.Framework.Bot.Enums;
using XiHan.Framework.Bot.Models;

namespace XiHan.Framework.Bot.Extensions;

/// <summary>
/// Bot 告警构建器
/// </summary>
public sealed class BotAlertBuilder
{
    private readonly IBotClient _client;
    private readonly BotMessage _message = new();
    private readonly List<string> _channels = [];

    /// <summary>
    /// 创建告警构建器
    /// </summary>
    public BotAlertBuilder(IBotClient client)
    {
        _client = client;
    }

    /// <summary>
    /// 设置标题
    /// </summary>
    public BotAlertBuilder Title(string title)
    {
        _message.Title = title;
        return this;
    }

    /// <summary>
    /// 设置内容
    /// </summary>
    public BotAlertBuilder Content(string content)
    {
        _message.Content = content;
        return this;
    }

    /// <summary>
    /// 设置消息类型
    /// </summary>
    public BotAlertBuilder Type(BotMessageType type)
    {
        _message.Type = type;
        return this;
    }

    /// <summary>
    /// 添加 @ 提及
    /// </summary>
    public BotAlertBuilder Mention(params string[] mentions)
    {
        _message.Mentions.AddRange(mentions);
        return this;
    }

    /// <summary>
    /// 指定渠道
    /// </summary>
    public BotAlertBuilder SendTo(params string[] channels)
    {
        _channels.AddRange(channels);
        return this;
    }

    /// <summary>
    /// 发送告警
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>调度聚合结果</returns>
    public Task<BotDispatchResult> SendAsync(CancellationToken cancellationToken = default)
    {
        return _channels.Count == 0
            ? _client.SendAsync(_message, cancellationToken)
            : _client.SendAsync(_message, _channels, cancellationToken);
    }
}
