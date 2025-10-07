#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JobResult.cs
// Guid:464d1419-3ff9-47cc-a2b8-c3b5cd46dcde
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 17:14:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Tasks.ScheduledJobs.Models;

/// <summary>
/// 任务执行结果
/// </summary>
public class JobResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 执行状态
    /// </summary>
    public JobStatus Status { get; set; }

    /// <summary>
    /// 结果数据
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 异常对象
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// 执行耗时
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// 成功结果
    /// </summary>
    public static JobResult Success(object? data = null, TimeSpan? duration = null)
    {
        return new JobResult
        {
            IsSuccess = true,
            Status = JobStatus.Succeeded,
            Data = data,
            Duration = duration ?? TimeSpan.Zero
        };
    }

    /// <summary>
    /// 失败结果
    /// </summary>
    public static JobResult Failure(string errorMessage, Exception? exception = null, TimeSpan? duration = null)
    {
        return new JobResult
        {
            IsSuccess = false,
            Status = JobStatus.Failed,
            ErrorMessage = errorMessage,
            Exception = exception,
            Duration = duration ?? TimeSpan.Zero
        };
    }

    /// <summary>
    /// 取消结果
    /// </summary>
    public static JobResult Canceled(TimeSpan? duration = null)
    {
        return new JobResult
        {
            IsSuccess = false,
            Status = JobStatus.Canceled,
            ErrorMessage = "任务已取消",
            Duration = duration ?? TimeSpan.Zero
        };
    }
}
