// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Abstractions;

/// <summary>
/// 任务执行器接口
/// </summary>
public interface IJobExecutor
{
    /// <summary>
    /// 执行任务
    /// </summary>
    /// <param name="jobInstance">任务实例</param>
    /// <param name="parameters">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果</returns>
    Task<JobResult> ExecuteAsync(JobInstance jobInstance, IDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default);
}
