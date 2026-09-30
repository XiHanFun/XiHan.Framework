// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs.Models;

namespace XiHan.Framework.Tasks.BackgroundJobs.Abstractions;

/// <summary>
/// 后台作业管理服务（界面由应用提供）
/// </summary>
/// <remarks>
/// 每次操作先经 <see cref="IBackgroundJobManagementAuthorizer"/> 授权，再交给存储执行，
/// 结果（含 <see cref="BackgroundJobManagementStatus.Denied"/> 与 <see cref="BackgroundJobManagementStatus.NotSupported"/>）
/// 交给 <see cref="IBackgroundJobManagementAuditor"/> 审计。
/// </remarks>
public interface IBackgroundJobManagementService
{
    /// <summary>
    /// 重试已放弃的作业
    /// </summary>
    /// <param name="jobId">作业标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<BackgroundJobManagementResult> RetryAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 请求取消作业（协作取消，不保证强制终止）
    /// </summary>
    /// <param name="jobId">作业标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<BackgroundJobManagementResult> RequestCancellationAsync(Guid jobId, CancellationToken cancellationToken = default);
}
