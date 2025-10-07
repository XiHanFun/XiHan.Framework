#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JobInfo.cs
// Guid:1ab32ead-b84b-4e83-8144-5e559873e7fe
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 16:10:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Tasks.ScheduledJobs.Models;

/// <summary>
/// 任务静态定义信息
/// </summary>
public class JobInfo
{
    /// <summary>
    /// 任务名称（唯一标识）
    /// </summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// 任务描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 任务类型
    /// </summary>
    public Type JobType { get; set; } = null!;

    /// <summary>
    /// 触发类型
    /// </summary>
    public JobTriggerType TriggerType { get; set; }

    /// <summary>
    /// Cron 表达式（当 TriggerType 为 Cron 时使用）
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// 执行间隔（当 TriggerType 为 Interval 时使用）
    /// </summary>
    public TimeSpan? Interval { get; set; }

    /// <summary>
    /// 延迟时间（当 TriggerType 为 Delay 时使用）
    /// </summary>
    public TimeSpan? Delay { get; set; }

    /// <summary>
    /// 优先级
    /// </summary>
    public JobPriority Priority { get; set; } = JobPriority.Normal;

    /// <summary>
    /// 是否允许并发执行
    /// </summary>
    public bool AllowConcurrent { get; set; } = true;

    /// <summary>
    /// 超时时间（毫秒）
    /// </summary>
    public int TimeoutMilliseconds { get; set; } = 300000; // 默认5分钟

    /// <summary>
    /// 重试策略
    /// </summary>
    public JobRetryPolicy RetryPolicy { get; set; } = JobRetryPolicy.Default;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 默认参数
    /// </summary>
    public IDictionary<string, object?>? DefaultParameters { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTimeOffset? ModifiedAt { get; set; }
}
