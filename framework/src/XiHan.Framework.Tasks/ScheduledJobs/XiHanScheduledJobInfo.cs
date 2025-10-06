#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanScheduledJobInfo
// Guid:3d4e5f6a-7b8c-9d0e-1f2a-3b4c5d6e7f8a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/6 22:32:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Tasks.ScheduledJobs;

/// <summary>
/// 调度任务信息
/// </summary>
public class XiHanScheduledJobInfo
{
    /// <summary>
    /// 任务名称
    /// </summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// 任务分组
    /// </summary>
    public string JobGroup { get; set; } = string.Empty;

    /// <summary>
    /// 任务类型
    /// </summary>
    public string JobType { get; set; } = string.Empty;

    /// <summary>
    /// Cron 表达式
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// 任务描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 任务状态
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// 上次执行时间
    /// </summary>
    public DateTimeOffset? PreviousFireTime { get; set; }

    /// <summary>
    /// 下次执行时间
    /// </summary>
    public DateTimeOffset? NextFireTime { get; set; }

    /// <summary>
    /// 任务优先级
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// 已执行次数
    /// </summary>
    public int ExecutionCount { get; set; }
}
