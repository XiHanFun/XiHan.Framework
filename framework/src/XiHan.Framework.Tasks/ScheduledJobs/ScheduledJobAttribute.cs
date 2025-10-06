#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ScheduledJobAttribute
// Guid:7b8e9f10-d4a5-4c67-9e72-6f5a8d9c0e21
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/6 22:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Tasks.ScheduledJobs;

/// <summary>
/// 调度任务特性
/// 用于标记和配置调度任务
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class ScheduledJobAttribute : Attribute
{
    /// <summary>
    /// 任务名称（默认使用类名）
    /// </summary>
    public string? JobName { get; set; }

    /// <summary>
    /// 任务分组（默认为 "Default"）
    /// </summary>
    public string JobGroup { get; set; } = "Default";

    /// <summary>
    /// Cron 表达式
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// 任务描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 是否在启动时立即执行一次
    /// </summary>
    public bool StartImmediately { get; set; }

    /// <summary>
    /// 是否自动启动（默认为 true）
    /// </summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>
    /// 任务优先级（数值越大优先级越高，默认为 5）
    /// </summary>
    public int Priority { get; set; } = 5;
}
