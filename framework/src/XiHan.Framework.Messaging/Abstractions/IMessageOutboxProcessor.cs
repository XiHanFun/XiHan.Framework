#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IMessageOutboxProcessor
// Guid:b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/05/12 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// 消息发件箱处理器
/// </summary>
public interface IMessageOutboxProcessor
{
    /// <summary>
    /// 处理发件箱中的待发送消息
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task ProcessAsync(CancellationToken cancellationToken = default);
}
