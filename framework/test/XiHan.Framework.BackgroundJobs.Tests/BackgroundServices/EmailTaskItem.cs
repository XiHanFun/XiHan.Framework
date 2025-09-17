#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EmailTaskItem
// Guid:${guid}
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/17 18:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.BackgroundJobs.BackgroundServices.Examples;

/// <summary>
/// 邮件发送任务项示例
/// </summary>
public class EmailTaskItem : IBackgroundTaskItem
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="to">收件人</param>
    /// <param name="subject">邮件主题</param>
    /// <param name="body">邮件内容</param>
    public EmailTaskItem(string to, string subject, string body)
    {
        TaskId = Guid.NewGuid().ToString();
        To = to;
        Subject = subject;
        Body = body;
        CreatedAt = DateTime.UtcNow;
        RetryCount = 0;
    }

    /// <inheritdoc />
    public string TaskId { get; }

    /// <inheritdoc />
    public object? Data => this;

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; }

    /// <inheritdoc />
    public int RetryCount { get; set; }

    /// <summary>
    /// 收件人邮箱
    /// </summary>
    public string To { get; }

    /// <summary>
    /// 邮件主题
    /// </summary>
    public string Subject { get; }

    /// <summary>
    /// 邮件内容
    /// </summary>
    public string Body { get; }

    /// <summary>
    /// 邮件优先级
    /// </summary>
    public EmailPriority Priority { get; set; } = EmailPriority.Normal;
}

/// <summary>
/// 邮件优先级枚举
/// </summary>
public enum EmailPriority
{
    /// <summary>
    /// 低优先级
    /// </summary>
    Low = 0,

    /// <summary>
    /// 普通优先级
    /// </summary>
    Normal = 1,

    /// <summary>
    /// 高优先级
    /// </summary>
    High = 2
}
