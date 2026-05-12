#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OutboxMessage
// Guid:c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/05/12 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Messaging.Models;

/// <summary>
/// 发件箱消息状态
/// </summary>
public enum OutboxMessageStatus
{
    /// <summary>
    /// 待发送
    /// </summary>
    Pending = 0,

    /// <summary>
    /// 已发送
    /// </summary>
    Sent = 1,

    /// <summary>
    /// 发送失败
    /// </summary>
    Failed = 2
}

/// <summary>
/// 发件箱消息
/// </summary>
public record OutboxMessage
{
    /// <summary>
    /// 消息唯一标识
    /// </summary>
    public string MessageId { get; init; } = string.Empty;

    /// <summary>
    /// 消息信封序列化JSON
    /// </summary>
    public string EnvelopeJson { get; init; } = string.Empty;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 消息状态
    /// </summary>
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// 最后一次错误信息
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; init; } = 3;
}
