// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs.Models;

namespace XiHan.Framework.Tasks.BackgroundJobs.Abstractions;

/// <summary>
/// 后台作业管理授权器
/// </summary>
/// <remarks>
/// 默认实现拒绝全部操作；应用侧在 DI 中注册自己的实现以放行。
/// </remarks>
public interface IBackgroundJobManagementAuthorizer
{
    /// <summary>
    /// 判断当前调用方是否可执行该管理操作
    /// </summary>
    /// <param name="operation">管理操作</param>
    /// <param name="jobId">作业标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否授权</returns>
    Task<bool> IsAuthorizedAsync(BackgroundJobManagementOperation operation, Guid jobId, CancellationToken cancellationToken = default);
}
