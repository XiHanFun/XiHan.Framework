// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Models;

/// <summary>
/// JobInstance 运行时实例默认值测试
/// </summary>
/// <remarks>
/// 实例唯一标识由模型自己生成，执行器与存储都直接用它做主键，必须保证格式稳定且不重复。
/// </remarks>
public class JobInstanceTests
{
    /// <summary>
    /// 新建实例处于等待执行状态，时间戳与错误信息均未填充
    /// </summary>
    [Fact]
    public void Constructor_Default_IsPendingWithEmptyTimestamps()
    {
        var instance = new JobInstance();

        Assert.Equal(JobStatus.Pending, instance.Status);
        Assert.Equal(string.Empty, instance.JobName);
        Assert.Null(instance.StartedAt);
        Assert.Null(instance.CompletedAt);
        Assert.Null(instance.DurationMilliseconds);
        Assert.Null(instance.TenantId);
        Assert.Null(instance.Parameters);
        Assert.Null(instance.ErrorMessage);
        Assert.Null(instance.StackTrace);
        Assert.Equal(0, instance.RetryCount);
        Assert.Null(instance.ExecutionNode);
        Assert.Null(instance.TraceId);
        Assert.Equal(JobTriggerType.Cron, instance.TriggerType);
    }

    /// <summary>
    /// 实例唯一标识自动生成为 32 位无连字符的十六进制串
    /// </summary>
    [Fact]
    public void InstanceId_IsGeneratedAsThirtyTwoCharHex()
    {
        var instance = new JobInstance();

        Assert.Equal(32, instance.InstanceId.Length);
        Assert.True(instance.InstanceId.All(character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f')));
    }

    /// <summary>
    /// 不同实例的唯一标识互不重复
    /// </summary>
    [Fact]
    public void InstanceId_IsUniqueAcrossInstances()
    {
        var ids = Enumerable.Range(0, 500).Select(_ => new JobInstance().InstanceId).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    /// <summary>
    /// 唯一标识可被调用方覆盖，以便对接外部编号体系
    /// </summary>
    [Fact]
    public void InstanceId_CanBeOverridden()
    {
        var instance = new JobInstance { InstanceId = "external-id" };

        Assert.Equal("external-id", instance.InstanceId);
    }

    /// <summary>
    /// 执行结果相关字段可被逐一填充，互不牵连
    /// </summary>
    [Fact]
    public void ExecutionFields_AreIndependentlyAssignable()
    {
        var completedAt = new DateTimeOffset(2024, 6, 12, 8, 0, 0, TimeSpan.Zero);

        var instance = new JobInstance
        {
            Status = JobStatus.Failed,
            StartedAt = completedAt.AddSeconds(-5),
            CompletedAt = completedAt,
            DurationMilliseconds = 5000,
            ErrorMessage = "失败原因",
            StackTrace = "堆栈",
            RetryCount = 2,
            ExecutionNode = "node-1",
            TraceId = "trace-1"
        };

        Assert.Equal(JobStatus.Failed, instance.Status);
        Assert.Equal(completedAt.AddSeconds(-5), instance.StartedAt);
        Assert.Equal(completedAt, instance.CompletedAt);
        Assert.Equal(5000L, instance.DurationMilliseconds);
        Assert.Equal("失败原因", instance.ErrorMessage);
        Assert.Equal("堆栈", instance.StackTrace);
        Assert.Equal(2, instance.RetryCount);
        Assert.Equal("node-1", instance.ExecutionNode);
        Assert.Equal("trace-1", instance.TraceId);
    }
}
