// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Tasks.BackgroundJobs.Models;

/// <summary>
/// 后台作业管理操作结果
/// </summary>
/// <param name="JobId">作业标识</param>
/// <param name="Operation">管理操作</param>
/// <param name="Status">结果状态</param>
public sealed record BackgroundJobManagementResult(Guid JobId, BackgroundJobManagementOperation Operation, BackgroundJobManagementStatus Status)
{
    /// <summary>
    /// 本次操作是否改变了作业状态
    /// </summary>
    public bool IsChanged => Status is BackgroundJobManagementStatus.Rescheduled
        or BackgroundJobManagementStatus.Cancelled
        or BackgroundJobManagementStatus.CancellationRequested;
}
