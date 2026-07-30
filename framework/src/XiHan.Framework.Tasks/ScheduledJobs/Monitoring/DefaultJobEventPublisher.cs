// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Monitoring;

/// <summary>
/// 默认任务事件发布者（空实现）
/// </summary>
public class DefaultJobEventPublisher : IJobEventPublisher
{
    /// <summary>
    /// 发布任务开始事件
    /// </summary>
    /// <param name="jobInstance">任务实例</param>
    /// <returns>任务结果</returns>
    /// <returns></returns>
    public Task PublishJobStartedAsync(JobInstance jobInstance)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 发布任务完成事件
    /// </summary>
    /// <param name="jobInstance">任务实例</param>
    /// <param name="result">任务结果</param>
    /// <returns>任务结果</returns>
    public Task PublishJobCompletedAsync(JobInstance jobInstance, JobResult result)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 发布任务失败事件
    /// </summary>
    /// <param name="jobInstance">任务实例</param>
    /// <param name="exception">异常</param>
    /// <returns>任务结果</returns>
    public Task PublishJobFailedAsync(JobInstance jobInstance, Exception exception)
    {
        return Task.CompletedTask;
    }
}
