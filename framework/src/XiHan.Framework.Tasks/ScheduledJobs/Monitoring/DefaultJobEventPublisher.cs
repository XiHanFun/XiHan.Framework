#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultJobEventPublisher
// Guid:00de98bd-f540-41a2-a7ea-020be8025a6a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/7 16:45:34
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Monitoring;

/// <summary>
/// 默认任务事件发布者（空实现）
/// </summary>
public class DefaultJobEventPublisher : IJobEventPublisher
{
    /// <inheritdoc />
    public Task PublishJobStartedAsync(JobInstance jobInstance)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task PublishJobCompletedAsync(JobInstance jobInstance, JobResult result)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task PublishJobFailedAsync(JobInstance jobInstance, Exception exception)
    {
        return Task.CompletedTask;
    }
}
