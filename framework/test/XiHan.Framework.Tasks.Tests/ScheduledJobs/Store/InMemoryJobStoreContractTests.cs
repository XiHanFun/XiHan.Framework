// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Models;
using XiHan.Framework.Tasks.ScheduledJobs.Store;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Store;

/// <summary>
/// InMemoryJobStore 参数校验与写入语义测试
/// </summary>
/// <remarks>
/// 与 ScheduledJobs/InMemoryJobStoreTests 互补：那边覆盖历史清理、分页排序与运行中实例筛选，
/// 这里补齐参数校验、实例覆盖写、状态回写对完成时间的影响，以及并发写入。
/// </remarks>
public class InMemoryJobStoreContractTests
{
    /// <summary>
    /// 并发用例的兜底超时
    /// </summary>
    private const int TimeoutMilliseconds = 60_000;

    /// <summary>
    /// 保存 null 实例时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public async Task SaveJobInstanceAsync_WhenInstanceIsNull_ThrowsArgumentNullException()
    {
        var store = new InMemoryJobStore();

        await Assert.ThrowsAsync<ArgumentNullException>(() => store.SaveJobInstanceAsync(null!));
    }

    /// <summary>
    /// 保存 null 历史时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public async Task SaveJobHistoryAsync_WhenHistoryIsNull_ThrowsArgumentNullException()
    {
        var store = new InMemoryJobStore();

        await Assert.ThrowsAsync<ArgumentNullException>(() => store.SaveJobHistoryAsync(null!));
    }

    /// <summary>
    /// 保存后按实例唯一标识取回同一个对象
    /// </summary>
    [Fact]
    public async Task SaveJobInstanceAsync_ThenGetJobInstanceAsync_ReturnsSameInstance()
    {
        var store = new InMemoryJobStore();
        var instance = CreateInstance("job-a", JobStatus.Pending);

        await store.SaveJobInstanceAsync(instance);

        Assert.Same(instance, await store.GetJobInstanceAsync(instance.InstanceId));
    }

    /// <summary>
    /// 同一实例唯一标识重复保存时后写覆盖前写
    /// </summary>
    [Fact]
    public async Task SaveJobInstanceAsync_WithSameInstanceId_OverwritesPrevious()
    {
        var store = new InMemoryJobStore();
        var first = CreateInstance("job-a", JobStatus.Pending);
        var second = CreateInstance("job-a", JobStatus.Running);
        second.InstanceId = first.InstanceId;

        await store.SaveJobInstanceAsync(first);
        await store.SaveJobInstanceAsync(second);

        Assert.Same(second, await store.GetJobInstanceAsync(first.InstanceId));
    }

    /// <summary>
    /// 查询不存在的实例返回 null
    /// </summary>
    [Fact]
    public async Task GetJobInstanceAsync_WhenInstanceUnknown_ReturnsNull()
    {
        var store = new InMemoryJobStore();

        Assert.Null(await store.GetJobInstanceAsync("not-exists"));
    }

    /// <summary>
    /// 更新不存在实例的状态是空操作，不抛异常
    /// </summary>
    [Fact]
    public async Task UpdateJobStatusAsync_WhenInstanceUnknown_DoesNothing()
    {
        var store = new InMemoryJobStore();

        await store.UpdateJobStatusAsync("not-exists", JobStatus.Succeeded);

        Assert.Null(await store.GetJobInstanceAsync("not-exists"));
    }

    /// <summary>
    /// 更新为终态时补齐完成时间
    /// </summary>
    [Theory]
    [InlineData(JobStatus.Succeeded)]
    [InlineData(JobStatus.Failed)]
    [InlineData(JobStatus.Canceled)]
    public async Task UpdateJobStatusAsync_WithTerminalStatus_StampsCompletedAt(JobStatus status)
    {
        var store = new InMemoryJobStore();
        var instance = CreateInstance("job-a", JobStatus.Running);
        await store.SaveJobInstanceAsync(instance);

        await store.UpdateJobStatusAsync(instance.InstanceId, status);

        Assert.Equal(status, instance.Status);
        Assert.NotNull(instance.CompletedAt);
    }

    /// <summary>
    /// 更新为非终态时不写完成时间
    /// </summary>
    [Theory]
    [InlineData(JobStatus.Pending)]
    [InlineData(JobStatus.Running)]
    [InlineData(JobStatus.Paused)]
    public async Task UpdateJobStatusAsync_WithNonTerminalStatus_LeavesCompletedAtNull(JobStatus status)
    {
        var store = new InMemoryJobStore();
        var instance = CreateInstance("job-a", JobStatus.Pending);
        await store.SaveJobInstanceAsync(instance);

        await store.UpdateJobStatusAsync(instance.InstanceId, status);

        Assert.Equal(status, instance.Status);
        Assert.Null(instance.CompletedAt);
    }

    /// <summary>
    /// 历史记录没有唯一标识时自动补一个，避免整批历史互相覆盖
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveJobHistoryAsync_WhenHistoryIdIsBlank_GeneratesOne(string historyId)
    {
        var store = new InMemoryJobStore();
        var first = CreateHistory("job-a", DateTimeOffset.UtcNow.AddMinutes(-2));
        first.HistoryId = historyId;
        var second = CreateHistory("job-a", DateTimeOffset.UtcNow.AddMinutes(-1));
        second.HistoryId = historyId;

        await store.SaveJobHistoryAsync(first);
        await store.SaveJobHistoryAsync(second);

        Assert.False(string.IsNullOrWhiteSpace(first.HistoryId));
        Assert.NotEqual(first.HistoryId, second.HistoryId);
        Assert.Equal(2, (await store.GetJobHistoryAsync("job-a", 1, 10)).Count);
    }

    /// <summary>
    /// 同一历史唯一标识重复保存时后写覆盖前写
    /// </summary>
    [Fact]
    public async Task SaveJobHistoryAsync_WithSameHistoryId_OverwritesPrevious()
    {
        var store = new InMemoryJobStore();
        var first = CreateHistory("job-a", DateTimeOffset.UtcNow.AddMinutes(-2));
        var second = CreateHistory("job-a", DateTimeOffset.UtcNow.AddMinutes(-1));
        second.HistoryId = first.HistoryId;

        await store.SaveJobHistoryAsync(first);
        await store.SaveJobHistoryAsync(second);

        var histories = await store.GetJobHistoryAsync("job-a", 1, 10);
        Assert.Same(second, Assert.Single(histories));
    }

    /// <summary>
    /// 查询历史时任务名为空白抛出 ArgumentException
    /// </summary>
    [Fact]
    public async Task GetJobHistoryAsync_WhenJobNameIsWhitespace_ThrowsArgumentException()
    {
        var store = new InMemoryJobStore();

        await Assert.ThrowsAsync<ArgumentException>(() => store.GetJobHistoryAsync("   "));
    }

    /// <summary>
    /// 查询历史时任务名为 null 抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public async Task GetJobHistoryAsync_WhenJobNameIsNull_ThrowsArgumentNullException()
    {
        var store = new InMemoryJobStore();

        await Assert.ThrowsAsync<ArgumentNullException>(() => store.GetJobHistoryAsync(null!));
    }

    /// <summary>
    /// 页码小于 1 时抛出 ArgumentOutOfRangeException 并指明参数名
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetJobHistoryAsync_WhenPageIndexIsInvalid_ThrowsArgumentOutOfRangeException(int pageIndex)
    {
        var store = new InMemoryJobStore();

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => store.GetJobHistoryAsync("job-a", pageIndex));

        Assert.Equal("pageIndex", exception.ParamName);
    }

    /// <summary>
    /// 页大小小于 1 时抛出 ArgumentOutOfRangeException 并指明参数名
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task GetJobHistoryAsync_WhenPageSizeIsInvalid_ThrowsArgumentOutOfRangeException(int pageSize)
    {
        var store = new InMemoryJobStore();

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => store.GetJobHistoryAsync("job-a", 1, pageSize));

        Assert.Equal("pageSize", exception.ParamName);
    }

    /// <summary>
    /// 翻到第二页时返回剩余记录，越界页返回空集合
    /// </summary>
    [Fact]
    public async Task GetJobHistoryAsync_WithPaging_ReturnsRemainderAndEmptyBeyondEnd()
    {
        var store = new InMemoryJobStore();
        var now = DateTimeOffset.UtcNow;
        await store.SaveJobHistoryAsync(CreateHistory("job-a", now.AddMinutes(-3)));
        await store.SaveJobHistoryAsync(CreateHistory("job-a", now.AddMinutes(-2)));
        await store.SaveJobHistoryAsync(CreateHistory("job-a", now.AddMinutes(-1)));

        var secondPage = await store.GetJobHistoryAsync("job-a", 2, 2);
        var beyondEnd = await store.GetJobHistoryAsync("job-a", 9, 2);

        Assert.Single(secondPage);
        Assert.Empty(beyondEnd);
    }

    /// <summary>
    /// 查询没有历史的任务返回空集合而不是 null
    /// </summary>
    [Fact]
    public async Task GetJobHistoryAsync_WhenJobHasNoHistory_ReturnsEmptyList()
    {
        var store = new InMemoryJobStore();

        var histories = await store.GetJobHistoryAsync("job-without-history");

        Assert.NotNull(histories);
        Assert.Empty(histories);
    }

    /// <summary>
    /// 查询运行中实例时任务名为空白抛出 ArgumentException
    /// </summary>
    [Fact]
    public async Task GetRunningInstancesAsync_WhenJobNameIsWhitespace_ThrowsArgumentException()
    {
        var store = new InMemoryJobStore();

        await Assert.ThrowsAsync<ArgumentException>(() => store.GetRunningInstancesAsync(" "));
    }

    /// <summary>
    /// 状态被回写为终态后就不再算作运行中实例
    /// </summary>
    [Fact]
    public async Task GetRunningInstancesAsync_AfterStatusUpdated_ExcludesFinishedInstance()
    {
        var store = new InMemoryJobStore();
        var instance = CreateInstance("job-a", JobStatus.Running);
        await store.SaveJobInstanceAsync(instance);
        Assert.Single(await store.GetRunningInstancesAsync("job-a"));

        await store.UpdateJobStatusAsync(instance.InstanceId, JobStatus.Succeeded);

        Assert.Empty(await store.GetRunningInstancesAsync("job-a"));
    }

    /// <summary>
    /// 保留天数为 0 时清掉所有早于当前时刻的历史
    /// </summary>
    [Fact]
    public async Task CleanupHistoryAsync_WithZeroRetention_RemovesEverythingBeforeNow()
    {
        var store = new InMemoryJobStore();
        await store.SaveJobHistoryAsync(CreateHistory("job-a", DateTimeOffset.UtcNow.AddSeconds(-5)));

        await store.CleanupHistoryAsync(0);

        Assert.Empty(await store.GetJobHistoryAsync("job-a", 1, 10));
    }

    /// <summary>
    /// 清理不影响任务实例，只作用于历史记录
    /// </summary>
    [Fact]
    public async Task CleanupHistoryAsync_DoesNotTouchInstances()
    {
        var store = new InMemoryJobStore();
        var instance = CreateInstance("job-a", JobStatus.Running);
        await store.SaveJobInstanceAsync(instance);
        await store.SaveJobHistoryAsync(CreateHistory("job-a", DateTimeOffset.UtcNow.AddDays(-30)));

        await store.CleanupHistoryAsync(1);

        Assert.NotNull(await store.GetJobInstanceAsync(instance.InstanceId));
        Assert.Empty(await store.GetJobHistoryAsync("job-a", 1, 10));
    }

    /// <summary>
    /// 多线程并发写入实例与历史时不丢数据
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task SaveAsync_UnderConcurrentWriters_KeepsEveryRecord()
    {
        var store = new InMemoryJobStore();
        const int WriterCount = 100;
        var now = DateTimeOffset.UtcNow;

        var tasks = Enumerable.Range(0, WriterCount)
            .Select(index => Task.Run(async () =>
            {
                await store.SaveJobInstanceAsync(CreateInstance("job-hot", JobStatus.Running));
                await store.SaveJobHistoryAsync(CreateHistory("job-hot", now.AddSeconds(-index)));
            }))
            .ToArray();

        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Equal(WriterCount, (await store.GetRunningInstancesAsync("job-hot")).Count);
        Assert.Equal(WriterCount, (await store.GetJobHistoryAsync("job-hot", 1, WriterCount * 2)).Count);
    }

    /// <summary>
    /// 构造一条执行历史
    /// </summary>
    private static JobHistory CreateHistory(string jobName, DateTimeOffset startedAt)
    {
        return new JobHistory
        {
            JobName = jobName,
            StartedAt = startedAt,
            Status = JobStatus.Succeeded,
            IsSuccess = true,
            TriggerType = JobTriggerType.Manual,
            InstanceId = Guid.NewGuid().ToString("N")
        };
    }

    /// <summary>
    /// 构造一个任务实例
    /// </summary>
    private static JobInstance CreateInstance(string jobName, JobStatus status)
    {
        return new JobInstance
        {
            JobName = jobName,
            Status = status,
            TriggerType = JobTriggerType.Manual,
            JobInfo = new JobInfo
            {
                JobName = jobName,
                JobType = typeof(InMemoryJobStoreContractTests),
                TriggerType = JobTriggerType.Manual
            }
        };
    }
}
