// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Tasks.ScheduledJobs.Models;
using XiHan.Framework.Tasks.SqlSugar.Entities;
using XiHan.Framework.Tasks.SqlSugar.Mapping;

namespace XiHan.Framework.Tasks.SqlSugar.Tests;

/// <summary>
/// 定时任务映射测试
/// </summary>
public class JobStoreMapperTests
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 任务实例往返后字段一致
    /// </summary>
    [Fact]
    public void 任务实例往返后字段一致()
    {
        var scheduledAt = new DateTimeOffset(2030, 1, 1, 8, 0, 0, TimeSpan.FromHours(8));
        var instance = new JobInstance
        {
            InstanceId = "instance-1",
            JobName = "Report.Daily",
            JobInfo = new JobInfo
            {
                JobName = "Report.Daily",
                JobType = typeof(JobStoreMapperTests),
                TriggerType = JobTriggerType.Cron
            },
            Status = JobStatus.Failed,
            ScheduledAt = scheduledAt,
            StartedAt = scheduledAt.AddSeconds(1),
            CompletedAt = scheduledAt.AddSeconds(5),
            DurationMilliseconds = 4000,
            TriggerType = JobTriggerType.Cron,
            TenantId = 42,
            Parameters = new Dictionary<string, object?> { ["orderId"] = 1 },
            ErrorMessage = "模拟失败。",
            StackTrace = "at Report.Daily",
            RetryCount = 2,
            ExecutionNode = "node-1",
            TraceId = "trace-1"
        };

        var restored = JobStoreMapper.ToJobInstance(JobStoreMapper.ToEntity(instance, GracePeriod));

        Assert.Equal("instance-1", restored.InstanceId);
        Assert.Equal("Report.Daily", restored.JobName);
        Assert.Equal(JobStatus.Failed, restored.Status);
        Assert.Equal(scheduledAt, restored.ScheduledAt);
        Assert.Equal(TimeSpan.Zero, restored.ScheduledAt.Offset);
        Assert.Equal(instance.StartedAt, restored.StartedAt);
        Assert.Equal(instance.CompletedAt, restored.CompletedAt);
        Assert.Equal(4000L, restored.DurationMilliseconds);
        Assert.Equal(JobTriggerType.Cron, restored.TriggerType);
        Assert.Equal(42L, restored.TenantId);
        Assert.Equal("模拟失败。", restored.ErrorMessage);
        Assert.Equal("at Report.Daily", restored.StackTrace);
        Assert.Equal(2, restored.RetryCount);
        Assert.Equal("node-1", restored.ExecutionNode);
        Assert.Equal("trace-1", restored.TraceId);
        Assert.Equal("Report.Daily", restored.JobInfo.JobName);
        Assert.Equal(typeof(JobStoreMapperTests), restored.JobInfo.JobType);
        Assert.Equal(42L, restored.JobInfo.TenantId);

        Assert.NotNull(restored.Parameters);
        var orderId = Assert.IsType<JsonElement>(restored.Parameters["orderId"]);
        Assert.Equal(1, orderId.GetInt32());
    }

    /// <summary>
    /// 运行中实例的截止时刻为开始时间加超时再加宽限
    /// </summary>
    [Fact]
    public void 运行中实例的截止时刻为开始时间加超时再加宽限()
    {
        var startedAt = new DateTimeOffset(2030, 1, 1, 8, 0, 0, TimeSpan.FromHours(8));
        var instance = new JobInstance
        {
            JobName = "Report.Daily",
            JobInfo = new JobInfo { JobName = "Report.Daily", TimeoutMilliseconds = 60000 },
            Status = JobStatus.Running,
            ScheduledAt = startedAt.AddMinutes(-10),
            StartedAt = startedAt
        };

        var entity = JobStoreMapper.ToEntity(instance, GracePeriod);

        Assert.Equal(new DateTime(2030, 1, 1, 0, 2, 0, DateTimeKind.Utc), entity.RunningDeadline);
    }

    /// <summary>
    /// 非运行状态不设截止时刻
    /// </summary>
    [Fact]
    public void 非运行状态不设截止时刻()
    {
        var instance = new JobInstance
        {
            JobName = "Report.Daily",
            JobInfo = new JobInfo { JobName = "Report.Daily" },
            Status = JobStatus.Succeeded,
            StartedAt = DateTimeOffset.UtcNow
        };

        Assert.Null(JobStoreMapper.ToEntity(instance, GracePeriod).RunningDeadline);
    }

    /// <summary>
    /// 超时关闭时截止时刻为无限远
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void 超时关闭时截止时刻为无限远(int timeoutMilliseconds)
    {
        var instance = new JobInstance
        {
            JobName = "Report.NoTimeout",
            JobInfo = new JobInfo { JobName = "Report.NoTimeout", TimeoutMilliseconds = timeoutMilliseconds },
            Status = JobStatus.Running,
            StartedAt = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };

        Assert.Equal(JobStoreMapper.UnboundedRunningDeadline, JobStoreMapper.ToEntity(instance, GracePeriod).RunningDeadline);
    }

    /// <summary>
    /// 参数无法序列化时存为空
    /// </summary>
    [Fact]
    public void 参数无法序列化时存为空()
    {
        var instance = new JobInstance
        {
            JobName = "Report.Daily",
            JobInfo = new JobInfo { JobName = "Report.Daily" },
            Parameters = new Dictionary<string, object?> { ["type"] = typeof(int) }
        };

        Assert.Null(JobStoreMapper.ToEntity(instance, GracePeriod).ParametersJson);
    }

    /// <summary>
    /// 无法解析的任务类型不赋值
    /// </summary>
    [Fact]
    public void 无法解析的任务类型不赋值()
    {
        var entity = new SysJobInstance("instance-1")
        {
            JobName = "Report.Daily",
            JobTypeName = "Not.Exists.Job, Not.Exists"
        };

        var restored = JobStoreMapper.ToJobInstance(entity);

        Assert.Null(restored.JobInfo.JobType);
    }

    /// <summary>
    /// 执行历史往返后字段一致
    /// </summary>
    [Fact]
    public void 执行历史往返后字段一致()
    {
        var startedAt = new DateTimeOffset(2030, 1, 1, 8, 0, 0, TimeSpan.FromHours(8));
        var history = new JobHistory
        {
            HistoryId = "history-1",
            InstanceId = "instance-1",
            JobName = "Report.Daily",
            Status = JobStatus.Succeeded,
            StartedAt = startedAt,
            CompletedAt = startedAt.AddSeconds(3),
            DurationMilliseconds = 3000,
            TenantId = 42,
            TriggerType = JobTriggerType.Manual,
            IsSuccess = true,
            ErrorMessage = null,
            StackTrace = null,
            RetryCount = 1,
            ExecutionNode = "node-1",
            TraceId = "trace-1",
            ParametersJson = "{\"orderId\":1}",
            Remarks = "手动触发"
        };

        var restored = JobStoreMapper.ToJobHistory(JobStoreMapper.ToEntity(history));

        Assert.Equal("history-1", restored.HistoryId);
        Assert.Equal("instance-1", restored.InstanceId);
        Assert.Equal("Report.Daily", restored.JobName);
        Assert.Equal(JobStatus.Succeeded, restored.Status);
        Assert.Equal(startedAt, restored.StartedAt);
        Assert.Equal(TimeSpan.Zero, restored.StartedAt.Offset);
        Assert.Equal(history.CompletedAt, restored.CompletedAt);
        Assert.Equal(3000L, restored.DurationMilliseconds);
        Assert.Equal(42L, restored.TenantId);
        Assert.Equal(JobTriggerType.Manual, restored.TriggerType);
        Assert.True(restored.IsSuccess);
        Assert.Equal(1, restored.RetryCount);
        Assert.Equal("node-1", restored.ExecutionNode);
        Assert.Equal("trace-1", restored.TraceId);
        Assert.Equal("{\"orderId\":1}", restored.ParametersJson);
        Assert.Equal("手动触发", restored.Remarks);
    }
}
