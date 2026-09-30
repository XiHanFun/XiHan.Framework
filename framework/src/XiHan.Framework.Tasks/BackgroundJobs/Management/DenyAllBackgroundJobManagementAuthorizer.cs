// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Models;

namespace XiHan.Framework.Tasks.BackgroundJobs.Management;

/// <summary>
/// 拒绝全部管理操作的默认授权器
/// </summary>
public sealed class DenyAllBackgroundJobManagementAuthorizer : IBackgroundJobManagementAuthorizer
{
    /// <summary>
    /// 判断当前调用方是否可执行该管理操作（一律拒绝）
    /// </summary>
    /// <param name="operation">管理操作</param>
    /// <param name="jobId">作业标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>false</returns>
    public Task<bool> IsAuthorizedAsync(BackgroundJobManagementOperation operation, Guid jobId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
}
