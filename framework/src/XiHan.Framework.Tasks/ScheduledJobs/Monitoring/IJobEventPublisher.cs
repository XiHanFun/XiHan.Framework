// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Monitoring;

/// <summary>
/// 任务事件发布者接口
/// </summary>
public interface IJobEventPublisher
{
    /// <summary>
    /// 发布任务开始事件
    /// </summary>
    Task PublishJobStartedAsync(JobInstance jobInstance);

    /// <summary>
    /// 发布任务完成事件
    /// </summary>
    Task PublishJobCompletedAsync(JobInstance jobInstance, JobResult result);

    /// <summary>
    /// 发布任务失败事件
    /// </summary>
    Task PublishJobFailedAsync(JobInstance jobInstance, Exception exception);
}
