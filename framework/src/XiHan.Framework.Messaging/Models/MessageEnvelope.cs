// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Messaging.Models;

/// <summary>
/// 消息信封
/// </summary>
public class MessageEnvelope
{
    /// <summary>
    /// 消息唯一标识
    /// </summary>
    public string MessageId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 消息通道（如：email/sms/site）
    /// </summary>
    public string Channel { get; set; } = "default";

    /// <summary>
    /// 租户ID
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// 发送者ID
    /// </summary>
    public string? SenderId { get; set; }

    /// <summary>
    /// 主题
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// 内容
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// 模板编码
    /// </summary>
    public string? TemplateCode { get; set; }

    /// <summary>
    /// 模板参数
    /// </summary>
    public Dictionary<string, string?> TemplateParams { get; set; } = [];

    /// <summary>
    /// 扩展元数据
    /// </summary>
    public Dictionary<string, string?> Metadata { get; set; } = [];

    /// <summary>
    /// 接收人集合
    /// </summary>
    public IReadOnlyList<MessageRecipient> Recipients { get; set; } = [];

    /// <summary>
    /// 计划发送时间
    /// </summary>
    public DateTimeOffset? ScheduledTime { get; set; }

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTimeOffset? ExpireTime { get; set; }

    /// <summary>
    /// 关联追踪ID
    /// </summary>
    public string? CorrelationId { get; set; }
}
