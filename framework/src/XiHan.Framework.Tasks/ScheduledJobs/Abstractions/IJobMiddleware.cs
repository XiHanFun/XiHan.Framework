#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IJobMiddleware.cs
// Guid:b7ce47cd-b93f-47a1-b33d-006462ccd539
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 16:29:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Abstractions;

/// <summary>
/// 任务中间件接口
/// </summary>
public interface IJobMiddleware
{
    /// <summary>
    /// 执行中间件逻辑
    /// </summary>
    /// <param name="context">任务上下文</param>
    /// <param name="next">下一个中间件</param>
    /// <returns>执行结果</returns>
    Task<JobResult> InvokeAsync(IJobContext context, JobExecutionDelegate next);
}

/// <summary>
/// 任务执行委托
/// </summary>
/// <param name="context">任务上下文</param>
/// <returns>执行结果</returns>
public delegate Task<JobResult> JobExecutionDelegate(IJobContext context);
