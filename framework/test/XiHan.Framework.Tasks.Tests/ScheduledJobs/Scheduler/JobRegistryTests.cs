// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Models;
using XiHan.Framework.Tasks.ScheduledJobs.Scheduler;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Scheduler;

/// <summary>
/// JobRegistry 任务注册表测试
/// </summary>
/// <remarks>
/// 注册表以任务名为唯一键，底层是并发字典；这里覆盖增删查、同名覆盖、参数校验与并发注册。
/// </remarks>
public class JobRegistryTests
{
    /// <summary>
    /// 并发用例的兜底超时
    /// </summary>
    private const int TimeoutMilliseconds = 60_000;

    /// <summary>
    /// 新建注册表为空
    /// </summary>
    [Fact]
    public void Constructor_Default_IsEmpty()
    {
        var registry = new JobRegistry();

        Assert.Equal(0, registry.Count);
        Assert.Empty(registry.GetAllJobs());
    }

    /// <summary>
    /// 注册后可按名称取回同一个任务定义实例
    /// </summary>
    [Fact]
    public void Register_ThenGetJob_ReturnsSameInstance()
    {
        var registry = new JobRegistry();
        var jobInfo = CreateJobInfo("job-alpha");

        registry.Register(jobInfo);

        Assert.Same(jobInfo, registry.GetJob("job-alpha"));
        Assert.True(registry.Exists("job-alpha"));
        Assert.Equal(1, registry.Count);
    }

    /// <summary>
    /// 同名重复注册覆盖旧定义而不是新增一条
    /// </summary>
    [Fact]
    public void Register_WithSameName_ReplacesPreviousDefinition()
    {
        var registry = new JobRegistry();
        var first = CreateJobInfo("job-alpha");
        var second = CreateJobInfo("job-alpha");
        second.Description = "第二版";

        registry.Register(first);
        registry.Register(second);

        Assert.Equal(1, registry.Count);
        Assert.Same(second, registry.GetJob("job-alpha"));
        Assert.Equal("第二版", registry.GetJob("job-alpha")!.Description);
    }

    /// <summary>
    /// 注册 null 定义时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void Register_WhenJobInfoIsNull_ThrowsArgumentNullException()
    {
        var registry = new JobRegistry();

        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
    }

    /// <summary>
    /// 任务名为空或空白时拒绝注册
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_WhenJobNameIsBlank_ThrowsArgumentException(string jobName)
    {
        var registry = new JobRegistry();
        var jobInfo = CreateJobInfo(jobName);

        var exception = Assert.Throws<ArgumentException>(() => registry.Register(jobInfo));

        Assert.Equal("jobInfo", exception.ParamName);
    }

    /// <summary>
    /// 取消注册已存在的任务返回 true，并从注册表中移除
    /// </summary>
    [Fact]
    public void Unregister_WhenJobExists_RemovesAndReturnsTrue()
    {
        var registry = new JobRegistry();
        registry.Register(CreateJobInfo("job-alpha"));

        Assert.True(registry.Unregister("job-alpha"));
        Assert.False(registry.Exists("job-alpha"));
        Assert.Null(registry.GetJob("job-alpha"));
        Assert.Equal(0, registry.Count);
    }

    /// <summary>
    /// 取消注册不存在的任务返回 false 且不抛异常
    /// </summary>
    [Fact]
    public void Unregister_WhenJobMissing_ReturnsFalse()
    {
        var registry = new JobRegistry();

        Assert.False(registry.Unregister("not-exists"));
    }

    /// <summary>
    /// 查询不存在的任务返回 null 而不是抛异常
    /// </summary>
    [Fact]
    public void GetJob_WhenJobMissing_ReturnsNull()
    {
        var registry = new JobRegistry();

        Assert.Null(registry.GetJob("not-exists"));
        Assert.False(registry.Exists("not-exists"));
    }

    /// <summary>
    /// 获取全部任务返回快照，后续注册不会影响已取出的列表
    /// </summary>
    [Fact]
    public void GetAllJobs_ReturnsSnapshotUnaffectedByLaterRegistration()
    {
        var registry = new JobRegistry();
        registry.Register(CreateJobInfo("job-alpha"));

        var snapshot = registry.GetAllJobs();
        registry.Register(CreateJobInfo("job-beta"));

        Assert.Single(snapshot);
        Assert.Equal(2, registry.GetAllJobs().Count);
    }

    /// <summary>
    /// 任务名区分大小写，视为两个不同任务
    /// </summary>
    [Fact]
    public void Register_WithDifferentCasing_TreatsNamesAsDistinct()
    {
        var registry = new JobRegistry();

        registry.Register(CreateJobInfo("Job-Alpha"));
        registry.Register(CreateJobInfo("job-alpha"));

        Assert.Equal(2, registry.Count);
    }

    /// <summary>
    /// 多线程并发注册不同任务时不丢数据、不重复
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Register_UnderConcurrentWriters_KeepsEveryJob()
    {
        var registry = new JobRegistry();
        const int JobCount = 200;

        var tasks = Enumerable.Range(0, JobCount)
            .Select(index => Task.Run(() => registry.Register(CreateJobInfo($"job-{index}"))))
            .ToArray();

        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Equal(JobCount, registry.Count);
        Assert.Equal(JobCount, registry.GetAllJobs().Select(job => job.JobName).Distinct().Count());
    }

    /// <summary>
    /// 多线程并发注册同名任务时最终只保留一条
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Register_UnderConcurrentWritersWithSameName_KeepsSingleEntry()
    {
        var registry = new JobRegistry();

        var tasks = Enumerable.Range(0, 100)
            .Select(_ => Task.Run(() => registry.Register(CreateJobInfo("job-shared"))))
            .ToArray();

        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Equal(1, registry.Count);
        Assert.NotNull(registry.GetJob("job-shared"));
    }

    /// <summary>
    /// 构造一个最小可用的任务定义
    /// </summary>
    private static JobInfo CreateJobInfo(string jobName)
    {
        return new JobInfo
        {
            JobName = jobName,
            JobType = typeof(JobRegistryTests),
            TriggerType = JobTriggerType.Manual
        };
    }
}
