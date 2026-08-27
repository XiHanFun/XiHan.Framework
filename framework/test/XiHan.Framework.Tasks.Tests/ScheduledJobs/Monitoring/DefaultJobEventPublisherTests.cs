// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Models;
using XiHan.Framework.Tasks.ScheduledJobs.Monitoring;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Monitoring;

/// <summary>
/// DefaultJobEventPublisher 默认事件发布者测试
/// </summary>
/// <remarks>
/// 默认实现是空发布者，契约只有两条：立即完成、绝不抛异常（包括传入退化参数时），
/// 否则它作为兜底实现会把整条执行链拖垮。
/// </remarks>
public class DefaultJobEventPublisherTests
{
    /// <summary>
    /// 发布开始事件立即完成
    /// </summary>
    [Fact]
    public void PublishJobStartedAsync_CompletesSynchronously()
    {
        IJobEventPublisher publisher = new DefaultJobEventPublisher();

        var task = publisher.PublishJobStartedAsync(CreateInstance());

        Assert.True(task.IsCompletedSuccessfully);
    }

    /// <summary>
    /// 发布完成事件立即完成
    /// </summary>
    [Fact]
    public void PublishJobCompletedAsync_CompletesSynchronously()
    {
        IJobEventPublisher publisher = new DefaultJobEventPublisher();

        var task = publisher.PublishJobCompletedAsync(CreateInstance(), JobResult.Success());

        Assert.True(task.IsCompletedSuccessfully);
    }

    /// <summary>
    /// 发布失败事件立即完成
    /// </summary>
    [Fact]
    public void PublishJobFailedAsync_CompletesSynchronously()
    {
        IJobEventPublisher publisher = new DefaultJobEventPublisher();

        var task = publisher.PublishJobFailedAsync(CreateInstance(), new InvalidOperationException("炸了"));

        Assert.True(task.IsCompletedSuccessfully);
    }

    /// <summary>
    /// 传入退化参数时也不抛异常，兜底实现必须足够钝
    /// </summary>
    [Fact]
    public async Task PublishMethods_WithNullArguments_DoNotThrow()
    {
        IJobEventPublisher publisher = new DefaultJobEventPublisher();

        await publisher.PublishJobStartedAsync(null!);
        await publisher.PublishJobCompletedAsync(null!, null!);
        await publisher.PublishJobFailedAsync(null!, null!);
    }

    /// <summary>
    /// 构造一个最小可用的任务实例
    /// </summary>
    private static JobInstance CreateInstance()
    {
        var jobInfo = new JobInfo
        {
            JobName = "event-job",
            JobType = typeof(DefaultJobEventPublisherTests),
            TriggerType = JobTriggerType.Manual
        };

        return new JobInstance
        {
            JobName = jobInfo.JobName,
            JobInfo = jobInfo,
            TriggerType = JobTriggerType.Manual
        };
    }
}
