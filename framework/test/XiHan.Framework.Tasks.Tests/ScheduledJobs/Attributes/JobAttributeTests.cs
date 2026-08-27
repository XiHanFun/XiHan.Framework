// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.Tasks.ScheduledJobs.Attributes;
using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Attributes;

/// <summary>
/// 任务声明式特性测试
/// </summary>
/// <remarks>
/// 这些特性是任务的声明式配置入口，JobSchedulerExtensions 的程序集扫描完全依赖它们的取值语义；
/// 同时 AttributeUsage 的三项设置（只能标在类上、不可重复、不继承）也属于对外契约。
/// </remarks>
public class JobAttributeTests
{
    /// <summary>
    /// 任务名称特性原样保留传入的名称
    /// </summary>
    [Fact]
    public void JobNameAttribute_KeepsProvidedName()
    {
        Assert.Equal("daily-report", new JobNameAttribute("daily-report").Name);
    }

    /// <summary>
    /// 任务描述特性原样保留传入的描述
    /// </summary>
    [Fact]
    public void JobDescriptionAttribute_KeepsProvidedDescription()
    {
        Assert.Equal("每日报表", new JobDescriptionAttribute("每日报表").Description);
    }

    /// <summary>
    /// 优先级特性原样保留传入的优先级
    /// </summary>
    [Theory]
    [InlineData(JobPriority.Low)]
    [InlineData(JobPriority.Normal)]
    [InlineData(JobPriority.High)]
    [InlineData(JobPriority.Critical)]
    public void JobPriorityAttribute_KeepsProvidedPriority(JobPriority priority)
    {
        Assert.Equal(priority, new JobPriorityAttribute(priority).Priority);
    }

    /// <summary>
    /// 超时特性原样保留传入的毫秒数
    /// </summary>
    [Fact]
    public void JobTimeoutAttribute_KeepsProvidedMilliseconds()
    {
        Assert.Equal(4500, new JobTimeoutAttribute(4500).TimeoutMilliseconds);
    }

    /// <summary>
    /// 并发特性默认允许并发，可显式关闭
    /// </summary>
    [Fact]
    public void JobConcurrentAttribute_DefaultsToAllowingConcurrency()
    {
        Assert.True(new JobConcurrentAttribute().AllowConcurrent);
        Assert.True(new JobConcurrentAttribute(true).AllowConcurrent);
        Assert.False(new JobConcurrentAttribute(false).AllowConcurrent);
    }

    /// <summary>
    /// 重试特性的默认值与 JobRetryPolicy 的默认值保持一致
    /// </summary>
    [Fact]
    public void JobRetryAttribute_DefaultsMatchRetryPolicyDefaults()
    {
        var attribute = new JobRetryAttribute();

        Assert.Equal(JobRetryPolicy.Default.MaxRetryCount, attribute.MaxRetryCount);
        Assert.Equal(JobRetryPolicy.Default.RetryIntervalMilliseconds, attribute.RetryIntervalMilliseconds);
        Assert.Equal(JobRetryPolicy.Default.UseExponentialBackoff, attribute.UseExponentialBackoff);
    }

    /// <summary>
    /// 重试特性的每一项都可被覆盖
    /// </summary>
    [Fact]
    public void JobRetryAttribute_EachSettingIsOverridable()
    {
        var attribute = new JobRetryAttribute
        {
            MaxRetryCount = 7,
            RetryIntervalMilliseconds = 250,
            UseExponentialBackoff = false
        };

        Assert.Equal(7, attribute.MaxRetryCount);
        Assert.Equal(250, attribute.RetryIntervalMilliseconds);
        Assert.False(attribute.UseExponentialBackoff);
    }

    /// <summary>
    /// 传字符串的调度特性表示 Cron 触发
    /// </summary>
    [Fact]
    public void JobScheduleAttribute_WithCronExpression_MeansCronTrigger()
    {
        var attribute = new JobScheduleAttribute("0 2 * * *");

        Assert.Equal(JobTriggerType.Cron, attribute.TriggerType);
        Assert.Equal("0 2 * * *", attribute.CronExpression);
        Assert.Equal(0, attribute.IntervalSeconds);
        Assert.Equal(0, attribute.DelaySeconds);
    }

    /// <summary>
    /// 传整数的调度特性表示固定间隔触发
    /// </summary>
    [Fact]
    public void JobScheduleAttribute_WithIntervalSeconds_MeansIntervalTrigger()
    {
        var attribute = new JobScheduleAttribute(90);

        Assert.Equal(JobTriggerType.Interval, attribute.TriggerType);
        Assert.Equal(90, attribute.IntervalSeconds);
        Assert.Null(attribute.CronExpression);
    }

    /// <summary>
    /// 无参调度特性表示只能手动触发
    /// </summary>
    [Fact]
    public void JobScheduleAttribute_WithoutArguments_MeansManualTrigger()
    {
        var attribute = new JobScheduleAttribute();

        Assert.Equal(JobTriggerType.Manual, attribute.TriggerType);
        Assert.Null(attribute.CronExpression);
        Assert.Equal(0, attribute.IntervalSeconds);
        Assert.Equal(0, attribute.DelaySeconds);
    }

    /// <summary>
    /// 延时秒数可通过命名参数补充设置
    /// </summary>
    [Fact]
    public void JobScheduleAttribute_DelaySeconds_IsSettableViaInitializer()
    {
        var attribute = new JobScheduleAttribute(90) { DelaySeconds = 15 };

        Assert.Equal(15, attribute.DelaySeconds);
        Assert.Equal(90, attribute.IntervalSeconds);
    }

    /// <summary>
    /// 触发类型只由构造函数决定，不提供公开写入口，避免与实际配置字段错配
    /// </summary>
    [Fact]
    public void JobScheduleAttribute_TriggerType_IsReadOnly()
    {
        var property = typeof(JobScheduleAttribute).GetProperty(nameof(JobScheduleAttribute.TriggerType));

        Assert.NotNull(property);
        Assert.Null(property!.SetMethod);
    }

    /// <summary>
    /// 全部任务特性都只能标注在类上、不可重复、不继承
    /// </summary>
    [Theory]
    [InlineData(typeof(JobNameAttribute))]
    [InlineData(typeof(JobDescriptionAttribute))]
    [InlineData(typeof(JobScheduleAttribute))]
    [InlineData(typeof(JobRetryAttribute))]
    [InlineData(typeof(JobConcurrentAttribute))]
    [InlineData(typeof(JobTimeoutAttribute))]
    [InlineData(typeof(JobPriorityAttribute))]
    public void JobAttributes_TargetClassOnlyWithoutMultipleOrInheritance(Type attributeType)
    {
        var usage = attributeType.GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Class, usage!.ValidOn);
        Assert.False(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }

    /// <summary>
    /// 全部任务特性都派生自 Attribute，可被反射读取
    /// </summary>
    [Theory]
    [InlineData(typeof(JobNameAttribute))]
    [InlineData(typeof(JobDescriptionAttribute))]
    [InlineData(typeof(JobScheduleAttribute))]
    [InlineData(typeof(JobRetryAttribute))]
    [InlineData(typeof(JobConcurrentAttribute))]
    [InlineData(typeof(JobTimeoutAttribute))]
    [InlineData(typeof(JobPriorityAttribute))]
    public void JobAttributes_DeriveFromAttribute(Type attributeType)
    {
        Assert.True(typeof(Attribute).IsAssignableFrom(attributeType));
    }
}
