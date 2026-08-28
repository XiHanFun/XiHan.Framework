// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Models;

namespace XiHan.Framework.Messaging.Tests.Fakes;

/// <summary>
/// 手写的消息发送器替身
/// </summary>
/// <remarks>
/// 本仓测试栈不引入 Mock 框架，通道匹配与发送行为一律通过构造参数注入的委托控制；
/// 同时记录调用轨迹，便于断言调度器「只调用第一个匹配发送器」「失败后是否继续」等编排契约。
/// </remarks>
internal sealed class FakeMessageSender : IMessageSender
{
    private readonly Func<string, bool> _canHandle;
    private readonly Func<MessageEnvelope, MessageRecipient, Task<MessageSendResult>> _handler;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="canHandle">通道匹配判定，默认匹配任意通道</param>
    /// <param name="handler">发送行为，默认返回成功结果</param>
    public FakeMessageSender(
        Func<string, bool>? canHandle = null,
        Func<MessageEnvelope, MessageRecipient, Task<MessageSendResult>>? handler = null)
    {
        _canHandle = canHandle ?? (_ => true);
        _handler = handler ?? ((_, _) => Task.FromResult(new MessageSendResult { IsSuccess = true }));
    }

    /// <summary>
    /// CanHandle 实际收到的通道参数，按调用顺序记录
    /// </summary>
    public List<string> CanHandleChannels { get; } = [];

    /// <summary>
    /// SendAsync 实际收到的接收地址，按调用顺序记录
    /// </summary>
    public List<string> SentAddresses { get; } = [];

    /// <summary>
    /// 最后一次 SendAsync 收到的取消令牌
    /// </summary>
    public CancellationToken LastCancellationToken { get; private set; }

    /// <summary>
    /// 是否支持指定通道
    /// </summary>
    /// <param name="channel">消息通道</param>
    /// <returns>是否支持</returns>
    public bool CanHandle(string channel)
    {
        CanHandleChannels.Add(channel);
        return _canHandle(channel);
    }

    /// <summary>
    /// 发送单条消息
    /// </summary>
    /// <param name="envelope">消息信封</param>
    /// <param name="recipient">接收人</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送结果</returns>
    public Task<MessageSendResult> SendAsync(MessageEnvelope envelope, MessageRecipient recipient, CancellationToken cancellationToken = default)
    {
        SentAddresses.Add(recipient.Address);
        LastCancellationToken = cancellationToken;
        return _handler(envelope, recipient);
    }
}
