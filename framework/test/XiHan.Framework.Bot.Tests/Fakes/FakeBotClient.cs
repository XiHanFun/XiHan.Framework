// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Clients;
using XiHan.Framework.Bot.Models;

namespace XiHan.Framework.Bot.Tests;

/// <summary>
/// 手写的 <see cref="IBotClient"/> 替身
/// </summary>
/// <remarks>
/// 只记录调用参数并回放预置结果，用于验证 <c>BotAlertBuilder</c> 选择了哪个发送重载。
/// </remarks>
public sealed class FakeBotClient : IBotClient
{
    /// <summary>
    /// 预置返回结果
    /// </summary>
    public BotDispatchResult Result { get; set; } = BotDispatchResult.From(new[] { BotResult.Success(provider: "Fake") }, false);

    /// <summary>
    /// 最后一次发送的消息
    /// </summary>
    public BotMessage? LastMessage { get; private set; }

    /// <summary>
    /// 最后一次发送指定的渠道列表
    /// </summary>
    public IReadOnlyList<string>? LastChannels { get; private set; }

    /// <summary>
    /// 是否走了带渠道参数的重载
    /// </summary>
    public bool UsedChannelOverload { get; private set; }

    /// <summary>
    /// 发送次数
    /// </summary>
    public int SendCount { get; private set; }

    /// <summary>
    /// 向所有提供者发送消息
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task<BotDispatchResult> SendAsync(BotMessage message, CancellationToken cancellationToken = default)
    {
        SendCount++;
        LastMessage = message;
        LastChannels = null;
        UsedChannelOverload = false;
        return Task.FromResult(Result);
    }

    /// <summary>
    /// 向指定渠道发送消息
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="channels">渠道列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task<BotDispatchResult> SendAsync(BotMessage message, IReadOnlyList<string>? channels, CancellationToken cancellationToken = default)
    {
        SendCount++;
        LastMessage = message;
        LastChannels = channels;
        UsedChannelOverload = true;
        return Task.FromResult(Result);
    }

    /// <summary>
    /// 按模板名称发送
    /// </summary>
    /// <param name="templateName">模板名称</param>
    /// <param name="model">模板模型</param>
    /// <param name="channels">渠道列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task<BotDispatchResult> SendTemplateAsync(string templateName, object? model = null, IReadOnlyList<string>? channels = null, CancellationToken cancellationToken = default)
    {
        SendCount++;
        LastChannels = channels;
        return Task.FromResult(Result);
    }

    /// <summary>
    /// 批量发送
    /// </summary>
    /// <param name="messages">消息列表</param>
    /// <param name="channels">渠道列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task<IReadOnlyList<BotDispatchResult>> SendBatchAsync(IEnumerable<BotMessage> messages, IReadOnlyList<string>? channels = null, CancellationToken cancellationToken = default)
    {
        var results = messages.Select(_ =>
        {
            SendCount++;
            return Result;
        }).ToArray();
        LastChannels = channels;
        return Task.FromResult<IReadOnlyList<BotDispatchResult>>(results);
    }

    /// <summary>
    /// 延迟发送
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="delay">延迟时长</param>
    /// <param name="channels">渠道列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task<BotDispatchResult> SendDelayedAsync(BotMessage message, TimeSpan delay, IReadOnlyList<string>? channels = null, CancellationToken cancellationToken = default)
    {
        SendCount++;
        LastMessage = message;
        LastChannels = channels;
        return Task.FromResult(Result);
    }
}
