#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EmailSendingService
// Guid:${guid}
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/17 18:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Tasks.BackgroundServices;
using XiHan.Framework.Utils.Diagnostics.RetryPolicys;

namespace XiHan.Framework.Tasks.Tests.BackgroundServices;

/// <summary>
/// 邮件发送后台服务示例
/// 演示如何使用 XiHanBackgroundServiceBase 实现具体的业务逻辑
/// </summary>
public class EmailSendingService : XiHanBackgroundServiceBase<EmailSendingService>
{
    private readonly IEmailSender _emailSender;
    private readonly ConcurrentQueue<EmailTaskItem> _emailQueue = new();

    /// <summary>
    /// 构造函数 - 基础版本
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">配置选项</param>
    /// <param name="emailSender">邮件发送器</param>
    public EmailSendingService(
        ILogger<EmailSendingService> logger, 
        IOptions<XiHanBackgroundServiceOptions> options,
        IEmailSender emailSender)
        : base(logger, options)
    {
        _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
        
        // 预填充一些测试邮件
        InitializeTestEmails();
    }

    /// <summary>
    /// 构造函数 - 完整版本（支持自定义配置和重试策略）
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">配置选项</param>
    /// <param name="emailSender">邮件发送器</param>
    /// <param name="dynamicConfig">动态配置管理器</param>
    /// <param name="retryPolicy">重试策略</param>
    public EmailSendingService(
        ILogger<EmailSendingService> logger,
        IOptions<XiHanBackgroundServiceOptions> options,
        IEmailSender emailSender,
        IDynamicServiceConfig? dynamicConfig = null,
        RetryPolicy? retryPolicy = null)
        : base(logger, options, dynamicConfig, retryPolicy)
    {
        _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
        
        // 预填充一些测试邮件
        InitializeTestEmails();
    }

    /// <summary>
    /// 批量获取邮件发送任务
    /// </summary>
    /// <param name="maxCount">最大获取数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>邮件任务列表</returns>
    protected override async Task<IEnumerable<IBackgroundTaskItem>> FetchWorkItemsAsync(int maxCount, CancellationToken cancellationToken)
    {
        // 模拟从数据库或消息队列获取邮件任务
        await Task.Delay(100, cancellationToken); // 模拟查询延迟

        var result = new List<IBackgroundTaskItem>();

        // 从内存队列获取邮件任务（实际应用中可能从 Redis、RabbitMQ、数据库等获取）
        for (var i = 0; i < maxCount && _emailQueue.TryDequeue(out var emailTask); i++)
        {
            result.Add(emailTask);
        }

        // 模拟从外部系统接收新邮件任务
        if (result.Count == 0 && Random.Shared.Next(0, 100) < 15) // 15% 概率生成新任务
        {
            var newEmail = GenerateRandomEmail();
            result.Add(newEmail);
            Logger.LogDebug("生成新邮件任务: {TaskId} -> {To}", newEmail.TaskId, newEmail.To);
        }

        if (result.Count > 0)
        {
            Logger.LogDebug("批量获取到 {Count} 个邮件发送任务", result.Count);
        }

        return result;
    }

    /// <summary>
    /// 处理单个邮件发送任务
    /// </summary>
    /// <param name="item">任务项</param>
    /// <param name="cancellationToken">取消令牌</param>
    protected override async Task ProcessItemAsync(IBackgroundTaskItem item, CancellationToken cancellationToken)
    {
        if (item is not EmailTaskItem emailTask)
        {
            throw new InvalidOperationException($"不支持的任务类型: {item.GetType()}");
        }

        Logger.LogInformation("开始发送邮件 {TaskId}: {To} - {Subject}", 
            emailTask.TaskId, emailTask.To, emailTask.Subject);

        try
        {
            // 根据优先级调整处理时间
            var processingTime = emailTask.Priority switch
            {
                EmailPriority.High => Random.Shared.Next(500, 1500),    // 高优先级: 0.5-1.5秒
                EmailPriority.Normal => Random.Shared.Next(1000, 3000), // 普通优先级: 1-3秒
                EmailPriority.Low => Random.Shared.Next(2000, 5000),    // 低优先级: 2-5秒
                _ => Random.Shared.Next(1000, 3000)
            };

            // 模拟邮件发送过程
            await _emailSender.SendEmailAsync(emailTask, cancellationToken);
            await Task.Delay(processingTime, cancellationToken);

            // 模拟发送失败的情况
            if (Random.Shared.Next(0, 100) < 10) // 10% 失败率
            {
                throw new EmailSendingException($"SMTP 服务器连接失败: {emailTask.To}");
            }

            Logger.LogInformation("邮件 {TaskId} 发送成功: {To}", emailTask.TaskId, emailTask.To);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Logger.LogWarning("邮件 {TaskId} 发送被取消", emailTask.TaskId);
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "邮件 {TaskId} 发送失败: {To} - {Subject}", 
                emailTask.TaskId, emailTask.To, emailTask.Subject);
            throw; // 重新抛出异常，由重试策略处理
        }
    }

    /// <summary>
    /// 邮件发送最终失败时的处理
    /// </summary>
    /// <param name="item">失败的任务项</param>
    /// <param name="exception">异常信息</param>
    protected override void OnTaskFailed(IBackgroundTaskItem item, Exception exception)
    {
        if (item is EmailTaskItem emailTask)
        {
            Logger.LogError(exception, "邮件 {TaskId} 最终发送失败，已重试 {RetryCount} 次: {To} - {Subject}",
                emailTask.TaskId, emailTask.RetryCount, emailTask.To, emailTask.Subject);

            // 这里可以实现失败邮件的处理逻辑：
            // 1. 记录到死信队列
            // 2. 发送告警通知给管理员
            // 3. 保存到数据库的失败记录表
            // 4. 写入日志文件等
            SaveFailedEmail(emailTask, exception);
        }

        base.OnTaskFailed(item, exception);
    }

    /// <summary>
    /// 添加邮件到发送队列
    /// </summary>
    /// <param name="to">收件人</param>
    /// <param name="subject">邮件主题</param>
    /// <param name="body">邮件内容</param>
    /// <param name="priority">邮件优先级</param>
    /// <returns>任务Id</returns>
    public string QueueEmail(string to, string subject, string body, EmailPriority priority = EmailPriority.Normal)
    {
        var emailTask = new EmailTaskItem(to, subject, body)
        {
            Priority = priority
        };

        _emailQueue.Enqueue(emailTask);
        Logger.LogDebug("邮件已加入发送队列: {TaskId} -> {To}", emailTask.TaskId, emailTask.To);

        return emailTask.TaskId;
    }

    /// <summary>
    /// 获取队列中的邮件数量
    /// </summary>
    /// <returns>待发送邮件数量</returns>
    public int GetQueuedEmailCount()
    {
        return _emailQueue.Count;
    }

    /// <summary>
    /// 初始化测试邮件
    /// </summary>
    private void InitializeTestEmails()
    {
        var testEmails = new[]
        {
            new EmailTaskItem("user1@example.com", "欢迎注册", "欢迎您注册我们的服务！") { Priority = EmailPriority.High },
            new EmailTaskItem("user2@example.com", "密码重置", "您的密码重置链接已生成。") { Priority = EmailPriority.High },
            new EmailTaskItem("user3@example.com", "每日报告", "今日系统运行报告。") { Priority = EmailPriority.Normal },
            new EmailTaskItem("user4@example.com", "促销活动", "最新促销活动通知。") { Priority = EmailPriority.Low },
            new EmailTaskItem("user5@example.com", "账单提醒", "您的月度账单已生成。") { Priority = EmailPriority.Normal },
        };

        foreach (var email in testEmails)
        {
            _emailQueue.Enqueue(email);
        }

        Logger.LogInformation("已初始化 {Count} 个测试邮件", testEmails.Length);
    }

    /// <summary>
    /// 生成随机测试邮件
    /// </summary>
    /// <returns>随机邮件任务</returns>
    private EmailTaskItem GenerateRandomEmail()
    {
        var recipients = new[] { "test1@example.com", "test2@example.com", "test3@example.com" };
        var subjects = new[] { "系统通知", "重要提醒", "活动邀请", "安全警告" };
        var priorities = Enum.GetValues<EmailPriority>();

        var to = recipients[Random.Shared.Next(recipients.Length)];
        var subject = subjects[Random.Shared.Next(subjects.Length)];
        var priority = priorities[Random.Shared.Next(priorities.Length)];
        var body = $"这是一封 {priority} 优先级的测试邮件，发送时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}";

        return new EmailTaskItem(to, subject, body) { Priority = priority };
    }

    /// <summary>
    /// 保存失败的邮件信息
    /// </summary>
    /// <param name="emailTask">失败的邮件任务</param>
    /// <param name="exception">异常信息</param>
    private void SaveFailedEmail(EmailTaskItem emailTask, Exception exception)
    {
        // 实际应用中这里应该：
        // 1. 写入数据库失败记录表
        // 2. 发送到死信队列
        // 3. 记录详细的错误日志
        // 4. 可能需要通知管理员

        Logger.LogWarning("保存失败邮件记录: {TaskId} - {To} - {Error}", 
            emailTask.TaskId, emailTask.To, exception.Message);
    }
}

/// <summary>
/// 邮件发送器接口
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// 发送邮件
    /// </summary>
    /// <param name="emailTask">邮件任务</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SendEmailAsync(EmailTaskItem emailTask, CancellationToken cancellationToken);
}

/// <summary>
/// 邮件发送异常
/// </summary>
public class EmailSendingException : Exception
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message">异常消息</param>
    public EmailSendingException(string message) : base(message)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="innerException">内部异常</param>
    public EmailSendingException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
