// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Tasks.ScheduledJobs.Models;

/// <summary>
/// 任务状态
/// </summary>
public enum JobStatus
{
    /// <summary>
    /// 等待执行
    /// </summary>
    Pending = 0,

    /// <summary>
    /// 运行中
    /// </summary>
    Running = 1,

    /// <summary>
    /// 执行成功
    /// </summary>
    Succeeded = 2,

    /// <summary>
    /// 执行失败
    /// </summary>
    Failed = 3,

    /// <summary>
    /// 已取消
    /// </summary>
    Canceled = 4,

    /// <summary>
    /// 已暂停
    /// </summary>
    Paused = 5
}
