// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Tasks.BackgroundJobs.Models;

/// <summary>
/// 后台作业管理操作结果状态
/// </summary>
public enum BackgroundJobManagementStatus
{
    /// <summary>
    /// 已放弃的作业重新进入待执行
    /// </summary>
    Rescheduled = 0,

    /// <summary>
    /// 未在执行的作业已取消
    /// </summary>
    Cancelled = 1,

    /// <summary>
    /// 执行中的作业已登记取消请求，由持有租约的 Worker 协作停止
    /// </summary>
    CancellationRequested = 2,

    /// <summary>
    /// 作业状态已满足请求，未做任何变更
    /// </summary>
    NoChange = 3,

    /// <summary>
    /// 作业不存在
    /// </summary>
    NotFound = 4,

    /// <summary>
    /// 授权器拒绝
    /// </summary>
    Denied = 5,

    /// <summary>
    /// 当前存储不支持作业管理
    /// </summary>
    NotSupported = 6
}
