// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs.Models;

namespace XiHan.Framework.Tasks.BackgroundJobs.Abstractions;

/// <summary>
/// 后台作业管理审计器
/// </summary>
/// <remarks>
/// 默认实现写日志；应用侧在 DI 中注册自己的实现以持久化审计记录。
/// </remarks>
public interface IBackgroundJobManagementAuditor
{
    /// <summary>
    /// 记录一次管理操作的结果
    /// </summary>
    /// <param name="entry">审计记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task AuditAsync(BackgroundJobManagementAuditEntry entry, CancellationToken cancellationToken = default);
}
