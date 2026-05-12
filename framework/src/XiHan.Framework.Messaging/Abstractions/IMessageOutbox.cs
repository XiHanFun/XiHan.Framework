#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IMessageOutbox
// Guid:a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/05/12 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Messaging.Models;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// 消息发件箱
/// </summary>
public interface IMessageOutbox
{
    /// <summary>
    /// 将消息信封加入发件箱队列
    /// </summary>
    /// <param name="envelope">消息信封</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task EnqueueAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取待发送的消息列表
    /// </summary>
    /// <param name="batchSize">批次大小</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>待发送的发件箱消息列表</returns>
    Task<IReadOnlyList<OutboxMessage>> GetPendingMessagesAsync(int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// 标记消息为已发送
    /// </summary>
    /// <param name="messageId">消息ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task MarkAsSentAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 标记消息为发送失败
    /// </summary>
    /// <param name="messageId">消息ID</param>
    /// <param name="error">错误信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task MarkAsFailedAsync(string messageId, string error, CancellationToken cancellationToken = default);
}
