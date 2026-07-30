// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Tasks.ScheduledJobs.Models;

/// <summary>
/// 任务优先级
/// </summary>
public enum JobPriority
{
    /// <summary>
    /// 低优先级
    /// </summary>
    Low = 0,

    /// <summary>
    /// 普通优先级
    /// </summary>
    Normal = 1,

    /// <summary>
    /// 高优先级
    /// </summary>
    High = 2,

    /// <summary>
    /// 紧急优先级
    /// </summary>
    Critical = 3
}
