#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IMessageDispatcher
// Guid:e2e6ab9f-c649-484c-a482-8dd4abcf5d48
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/04 14:55:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
