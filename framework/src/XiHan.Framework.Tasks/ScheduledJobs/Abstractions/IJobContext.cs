#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IJobContext
// Guid:37fa750f-aa28-41bd-bdb6-7aaf894da817
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 15:57:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Abstractions;

/// <summary>
/// 任务执行上下文接口
/// </summary>
public interface IJobContext
{
    /// <summary>
    /// 任务实例信息
    /// </summary>
    JobInstance JobInstance { get; }

    /// <summary>
    /// 任务参数
    /// </summary>
    IDictionary<string, object?> Parameters { get; }

    /// <summary>
    /// 服务提供者（用于依赖注入）
    /// </summary>
    IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 追踪标识
    /// </summary>
    string TraceId { get; }

    /// <summary>
    /// 任务执行开始时间
    /// </summary>
    DateTimeOffset StartedAt { get; }

    /// <summary>
    /// 尝试次数（含重试）
    /// </summary>
    int AttemptCount { get; set; }

    /// <summary>
    /// 取消令牌
    /// </summary>
    CancellationToken CancellationToken { get; }
}
