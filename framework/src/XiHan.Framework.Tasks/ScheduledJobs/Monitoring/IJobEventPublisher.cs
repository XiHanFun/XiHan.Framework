#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IJobEventPublisher
// Guid:6411452e-210d-4a94-8394-5fe9968dcc40
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 15:00:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
