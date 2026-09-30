// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Models;
using XiHan.Framework.Timing;

namespace XiHan.Framework.Tasks.BackgroundJobs.Management;

/// <summary>
/// 后台作业管理服务默认实现
/// </summary>
/// <remarks>
/// 流程：授权 → 判断存储是否支持作业管理 → 委派存储 → 审计。
/// 授权器拒绝返回 <see cref="BackgroundJobManagementStatus.Denied"/>，存储不支持作业管理返回
/// <see cref="BackgroundJobManagementStatus.NotSupported"/>，两者同样审计。
/// 调用方令牌在开始时已取消则抛出 <see cref="OperationCanceledException"/>，不调用授权器与存储，也不审计。
/// 授权器抛出的异常原样传播给调用方，此时不调用存储也不审计。
/// 存储抛出的异常原样传播给调用方，此时不审计。
/// 审计不接收调用方令牌；审计器抛出的异常记 Warning 日志，不改变返回结果。
/// </remarks>
/// <param name="store">后台作业存储</param>
/// <param name="authorizer">管理授权器</param>
/// <param name="auditor">管理审计器</param>
/// <param name="clock">时钟</param>
/// <param name="logger">日志器</param>
public sealed class BackgroundJobManagementService(
    IBackgroundJobStore store,
    IBackgroundJobManagementAuthorizer authorizer,
    IBackgroundJobManagementAuditor auditor,
    IClock clock,
    ILogger<BackgroundJobManagementService> logger) : IBackgroundJobManagementService
{
    /// <summary>
    /// 重试已放弃的作业
    /// </summary>
    /// <param name="jobId">作业标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    /// <remarks>授权器或存储抛出的异常原样传播，且不审计。</remarks>
    public Task<BackgroundJobManagementResult> RetryAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(BackgroundJobManagementOperation.Retry, jobId, store.RetryAbandonedAsync, cancellationToken);
    }

    /// <summary>
    /// 请求取消作业（协作取消，不保证强制终止）
    /// </summary>
    /// <param name="jobId">作业标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    /// <remarks>授权器或存储抛出的异常原样传播，且不审计。</remarks>
    public Task<BackgroundJobManagementResult> RequestCancellationAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(BackgroundJobManagementOperation.Cancel, jobId, store.RequestCancellationAsync, cancellationToken);
    }

    /// <summary>
    /// 授权、委派存储并审计
    /// </summary>
    /// <param name="operation">管理操作</param>
    /// <param name="jobId">作业标识</param>
    /// <param name="storeOperation">存储侧的管理方法</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    private async Task<BackgroundJobManagementResult> ExecuteAsync(
        BackgroundJobManagementOperation operation,
        Guid jobId,
        Func<Guid, CancellationToken, Task<BackgroundJobManagementStatus>> storeOperation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        BackgroundJobManagementStatus status;
        if (!await authorizer.IsAuthorizedAsync(operation, jobId, cancellationToken))
        {
            status = BackgroundJobManagementStatus.Denied;
        }
        else if (!store.SupportsJobManagement)
        {
            status = BackgroundJobManagementStatus.NotSupported;
        }
        else
        {
            status = await storeOperation(jobId, cancellationToken);
        }

        await AuditAsync(new BackgroundJobManagementAuditEntry(jobId, operation, status, clock.Now));
        return new BackgroundJobManagementResult(jobId, operation, status);
    }

    /// <summary>
    /// 调用审计器，审计异常记 Warning 日志
    /// </summary>
    /// <param name="entry">审计记录</param>
    /// <returns>任务</returns>
    private async Task AuditAsync(BackgroundJobManagementAuditEntry entry)
    {
        try
        {
            await auditor.AuditAsync(entry, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "后台作业管理审计失败：{Operation} {JobId} -> {Status}", entry.Operation, entry.JobId, entry.Status);
        }
    }
}
