// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Models;

/// <summary>
/// 任务相关枚举的数值契约测试
/// </summary>
/// <remarks>
/// JobStatus / JobTriggerType / JobPriority 会随 JobHistory、JobInstance 一起落库并跨版本读取，
/// 数值一旦漂移历史数据就会被读成另一种语义，因此这里把每个成员的底层数值锁死。
/// </remarks>
public class JobEnumValueTests
{
    /// <summary>
    /// 任务状态的数值不得漂移
    /// </summary>
    [Theory]
    [InlineData(JobStatus.Pending, 0)]
    [InlineData(JobStatus.Running, 1)]
    [InlineData(JobStatus.Succeeded, 2)]
    [InlineData(JobStatus.Failed, 3)]
    [InlineData(JobStatus.Canceled, 4)]
    [InlineData(JobStatus.Paused, 5)]
    public void JobStatus_MemberValues_AreStable(JobStatus status, int expected)
    {
        Assert.Equal(expected, (int)status);
    }

    /// <summary>
    /// 任务状态成员集合完整，新增成员需显式评估持久化影响
    /// </summary>
    [Fact]
    public void JobStatus_HasExactlySixMembers()
    {
        Assert.Equal(6, Enum.GetValues<JobStatus>().Length);
    }

    /// <summary>
    /// 触发类型的数值不得漂移
    /// </summary>
    [Theory]
    [InlineData(JobTriggerType.Cron, 0)]
    [InlineData(JobTriggerType.Interval, 1)]
    [InlineData(JobTriggerType.Delay, 2)]
    [InlineData(JobTriggerType.Manual, 3)]
    public void JobTriggerType_MemberValues_AreStable(JobTriggerType triggerType, int expected)
    {
        Assert.Equal(expected, (int)triggerType);
    }

    /// <summary>
    /// 触发类型成员集合完整
    /// </summary>
    [Fact]
    public void JobTriggerType_HasExactlyFourMembers()
    {
        Assert.Equal(4, Enum.GetValues<JobTriggerType>().Length);
    }

    /// <summary>
    /// 优先级的数值不得漂移，且按低到高单调递增
    /// </summary>
    [Theory]
    [InlineData(JobPriority.Low, 0)]
    [InlineData(JobPriority.Normal, 1)]
    [InlineData(JobPriority.High, 2)]
    [InlineData(JobPriority.Critical, 3)]
    public void JobPriority_MemberValues_AreStable(JobPriority priority, int expected)
    {
        Assert.Equal(expected, (int)priority);
    }

    /// <summary>
    /// 优先级可直接比较大小，紧急高于高、高于普通、高于低
    /// </summary>
    [Fact]
    public void JobPriority_IsOrderedFromLowToCritical()
    {
        Assert.True(JobPriority.Low < JobPriority.Normal);
        Assert.True(JobPriority.Normal < JobPriority.High);
        Assert.True(JobPriority.High < JobPriority.Critical);
    }

    /// <summary>
    /// 优先级成员集合完整
    /// </summary>
    [Fact]
    public void JobPriority_HasExactlyFourMembers()
    {
        Assert.Equal(4, Enum.GetValues<JobPriority>().Length);
    }

    /// <summary>
    /// 枚举默认值即为各自的语义起点，未显式赋值的字段不会落入未知语义
    /// </summary>
    [Fact]
    public void DefaultValues_MapToSemanticOrigins()
    {
        Assert.Equal(JobStatus.Pending, default(JobStatus));
        Assert.Equal(JobTriggerType.Cron, default(JobTriggerType));
        Assert.Equal(JobPriority.Low, default(JobPriority));
    }
}
