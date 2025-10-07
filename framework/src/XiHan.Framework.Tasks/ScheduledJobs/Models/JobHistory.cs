#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JobHistory
// Guid:3daf1a26-cb51-41a5-a2a3-c6c29d62a883
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 15:38:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Tasks.ScheduledJobs.Models;

/// <summary>
/// 任务执行历史记录
/// </summary>
public class JobHistory
{
    /// <summary>
    /// 历史记录ID
    /// </summary>
    public string HistoryId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 实例ID
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// 任务名称
    /// </summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// 执行状态
    /// </summary>
    public JobStatus Status { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// 执行耗时（毫秒）
    /// </summary>
    public long? DurationMilliseconds { get; set; }

    /// <summary>
    /// 触发类型
    /// </summary>
    public JobTriggerType TriggerType { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 堆栈跟踪
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// 执行节点
    /// </summary>
    public string? ExecutionNode { get; set; }

    /// <summary>
    /// 追踪ID
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// 执行参数（JSON格式）
    /// </summary>
    public string? ParametersJson { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remarks { get; set; }
}
