#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MockEmailSender
// Guid:${guid}
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/17 18:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Tasks.Tests.BackgroundServices;

/// <summary>
/// 模拟邮件发送器实现
/// 用于演示和测试，实际应用中应该使用真实的 SMTP 客户端
/// </summary>
public class MockEmailSender : IEmailSender
{
    private readonly ILogger<MockEmailSender> _logger;
    private readonly List<SentEmail> _sentEmails = [];
    private readonly object _lock = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public MockEmailSender(ILogger<MockEmailSender> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 模拟发送邮件
    /// </summary>
    /// <param name="emailTask">邮件任务</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task SendEmailAsync(EmailTaskItem emailTask, CancellationToken cancellationToken)
    {
        _logger.LogDebug("正在发送邮件到 SMTP 服务器: {To}", emailTask.To);

        // 模拟网络延迟
        await Task.Delay(Random.Shared.Next(200, 800), cancellationToken);

        // 模拟 SMTP 连接和认证
        _logger.LogDebug("SMTP 连接建立成功，开始发送邮件内容");

        // 模拟邮件内容传输
        await Task.Delay(Random.Shared.Next(100, 300), cancellationToken);

        // 记录已发送的邮件
        lock (_lock)
        {
            _sentEmails.Add(new SentEmail
            {
                TaskId = emailTask.TaskId,
                To = emailTask.To,
                Subject = emailTask.Subject,
                Body = emailTask.Body,
                Priority = emailTask.Priority,
                SentAt = DateTime.UtcNow
            });
        }

        _logger.LogDebug("邮件发送到 SMTP 服务器成功: {To} - {Subject}", emailTask.To, emailTask.Subject);
    }

    /// <summary>
    /// 获取已发送的邮件列表
    /// </summary>
    /// <returns>已发送邮件列表</returns>
    public List<SentEmail> GetSentEmails()
    {
        lock (_lock)
        {
            return [.. _sentEmails];
        }
    }

    /// <summary>
    /// 获取已发送邮件数量
    /// </summary>
    /// <returns>已发送邮件数量</returns>
    public int GetSentEmailCount()
    {
        lock (_lock)
        {
            return _sentEmails.Count;
        }
    }

    /// <summary>
    /// 清空发送记录
    /// </summary>
    public void ClearSentEmails()
    {
        lock (_lock)
        {
            _sentEmails.Clear();
        }
    }
}

/// <summary>
/// 已发送邮件记录
/// </summary>
public class SentEmail
{
    /// <summary>
    /// 任务唯一标识
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// 收件人
    /// </summary>
    public string To { get; set; } = string.Empty;

    /// <summary>
    /// 邮件主题
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// 邮件内容
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// 邮件优先级
    /// </summary>
    public EmailPriority Priority { get; set; }

    /// <summary>
    /// 发送时间
    /// </summary>
    public DateTime SentAt { get; set; }
}
