#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanBackgroundServiceOptions
// Guid:df2a1a95-9b0b-49f5-bdb1-9c1c11aa6940
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/17 15:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Tasks.BackgroundServices;

/// <summary>
/// 后台服务配置选项
/// </summary>
public class XiHanBackgroundServiceOptions
{
    /// <summary>
    /// 最大并发任务数
    /// </summary>
    public int MaxConcurrentTasks { get; set; } = 5;

    /// <summary>
    /// 队列为空时的等待时间（毫秒）
    /// </summary>
    public int IdleDelayMilliseconds { get; set; } = 1000;

    /// <summary>
    /// 是否启用重试机制
    /// </summary>
    public bool EnableRetry { get; set; } = true;

    /// <summary>
    /// 是否启用任务超时控制
    /// </summary>
    public bool EnableTaskTimeout { get; set; } = false;

    /// <summary>
    /// 任务失败重试次数
    /// </summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>
    /// 重试延迟时间（毫秒）
    /// </summary>
    public int RetryDelayMilliseconds { get; set; } = 5000;

    /// <summary>
    /// 任务执行超时时间（毫秒），0表示不超时
    /// </summary>
    public int TaskTimeoutMilliseconds { get; set; } = 0;

    /// <summary>
    /// 服务停止时等待任务完成的超时时间（毫秒）
    /// </summary>
    public int ShutdownTimeoutMilliseconds { get; set; } = 30000;
}
