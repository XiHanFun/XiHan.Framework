// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Models;

namespace XiHan.Framework.Tasks.BackgroundJobs.Management;

/// <summary>
/// 以 Information 级别日志记录管理操作的默认审计器
/// </summary>
/// <param name="logger">日志器</param>
public sealed class LoggingBackgroundJobManagementAuditor(ILogger<LoggingBackgroundJobManagementAuditor> logger) : IBackgroundJobManagementAuditor
{
    /// <summary>
    /// 记录一次管理操作的结果
    /// </summary>
    /// <param name="entry">审计记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task AuditAsync(BackgroundJobManagementAuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        logger.LogInformation(
            "后台作业管理操作：{Operation} {JobId} -> {Status}（{OccurredAt}）",
            entry.Operation, entry.JobId, entry.Status, entry.OccurredAt);
        return Task.CompletedTask;
    }
}
