// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Tasks.ScheduledJobs.Models;

/// <summary>
/// 任务触发类型
/// </summary>
public enum JobTriggerType
{
    /// <summary>
    /// Cron 表达式触发
    /// </summary>
    Cron = 0,

    /// <summary>
    /// 固定间隔触发
    /// </summary>
    Interval = 1,

    /// <summary>
    /// 延时触发（一次性）
    /// </summary>
    Delay = 2,

    /// <summary>
    /// 手动触发
    /// </summary>
    Manual = 3
}
