// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Messaging.Models;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// 消息调度器
/// </summary>
public interface IMessageDispatcher
{
    /// <summary>
    /// 分发消息到指定通道
    /// </summary>
    /// <param name="envelope">消息信封</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分发结果集合</returns>
    Task<IReadOnlyList<MessageSendResult>> DispatchAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default);
}
