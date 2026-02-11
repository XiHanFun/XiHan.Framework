#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JobRetryPolicy
// Guid:175a6436-418a-45c0-96a5-e6e1e5e0eda2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 16:53:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Tasks.ScheduledJobs.Models;

/// <summary>
/// 任务重试策略
/// </summary>
public class JobRetryPolicy
{
    /// <summary>
    /// 默认策略
    /// </summary>
    public static JobRetryPolicy Default => new();

    /// <summary>
    /// 无重试策略
    /// </summary>
    public static JobRetryPolicy None => new() { MaxRetryCount = 0 };

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>
    /// 重试间隔（毫秒）
    /// </summary>
    public int RetryIntervalMilliseconds { get; set; } = 1000;

    /// <summary>
    /// 是否使用指数退避
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// 退避倍数
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// 最大重试间隔（毫秒）
    /// </summary>
    public int MaxRetryIntervalMilliseconds { get; set; } = 60000;

    /// <summary>
    /// 计算重试延迟
    /// </summary>
    /// <param name="attemptNumber">尝试次数（从1开始）</param>
    /// <returns>延迟时间</returns>
    public TimeSpan CalculateDelay(int attemptNumber)
    {
        if (!UseExponentialBackoff)
        {
            return TimeSpan.FromMilliseconds(RetryIntervalMilliseconds);
        }

        var delay = RetryIntervalMilliseconds * Math.Pow(BackoffMultiplier, attemptNumber - 1);
        return TimeSpan.FromMilliseconds(Math.Min(delay, MaxRetryIntervalMilliseconds));
    }
}
