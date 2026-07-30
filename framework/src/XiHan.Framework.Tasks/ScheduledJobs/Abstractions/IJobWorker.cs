// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Abstractions;

/// <summary>
/// 任务执行接口
/// </summary>
public interface IJobWorker
{
    /// <summary>
    /// 执行任务
    /// </summary>
    /// <param name="context">执行上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务结果</returns>
    Task<JobResult> ExecuteAsync(IJobContext context, CancellationToken cancellationToken = default);
}
