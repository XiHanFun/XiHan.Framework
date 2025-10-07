#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IJobExecutor
// Guid:2fcb82fa-b05a-4e02-945a-7bb8daf41574
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 16:38:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
