#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JobTriggerType.cs
// Guid:7d9973f2-7241-4589-b6c8-49e02caa8fd7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 16:38:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
