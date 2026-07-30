// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.RealTime.Models;

/// <summary>
/// 通知消息模型
/// </summary>
public class NotificationMessage
{
    /// <summary>
    /// 消息 ID
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 发送者 ID
    /// </summary>
    public string? SenderId { get; set; }

    /// <summary>
    /// 接收者 ID
    /// </summary>
    public string? ReceiverId { get; set; }

    /// <summary>
    /// 消息类型
    /// </summary>
    public string Type { get; set; } = "Info";

    /// <summary>
    /// 消息标题
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 消息内容
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// 消息数据
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 是否已读
    /// </summary>
    public bool IsRead { get; set; }
}
