// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Models;

namespace XiHan.Framework.ProviderContractTests.Tasks;

/// <summary>
/// 后台作业存储提供方契约
/// </summary>
/// <remarks>
/// 派生类实现 <see cref="CreateFixtureAsync"/> 即获得全部用例；用例只经 <see cref="IBackgroundJobStore"/> 观察行为。
/// </remarks>
public abstract class BackgroundJobStoreContract
{
    /// <summary>
    /// 创建本用例专用的夹具
    /// </summary>
    /// <returns>夹具</returns>
    protected abstract Task<IProviderContractFixture<IBackgroundJobStore>> CreateFixtureAsync();

    /// <summary>
    /// 插入后按标识查回的字段一致
    /// </summary>
    [Fact]
    public async Task 插入后按标识查回字段一致()
    {
        await using var fixture = await CreateFixtureAsync();
        var store = await fixture.CreateClientAsync();
        var now = Now(fixture);
        var job = NewJob(now.AddMinutes(-1), "app");
        job.TenantId = 1001;
        job.TryCount = 2;
        job.LastTryTime = now.AddMinutes(-2);
        job.Priority = BackgroundJobPriority.High;
        job.JobArgs = "{\"id\":1}";
        await store.InsertAsync(job);

        var found = await store.FindAsync(job.Id);

        Assert.NotNull(found);
        Assert.Equal(job.ApplicationName, found.ApplicationName);
        Assert.Equal(job.TenantId, found.TenantId);
        Assert.Equal(job.JobName, found.JobName);
        Assert.Equal(job.JobArgs, found.JobArgs);
        Assert.Equal(job.TryCount, found.TryCount);
        Assert.Equal(job.CreationTime, found.CreationTime);
        Assert.Equal(job.NextTryTime, found.NextTryTime);
        Assert.Equal(job.LastTryTime, found.LastTryTime);
        Assert.Equal(job.IsAbandoned, found.IsAbandoned);
        Assert.Equal(job.Priority, found.Priority);
    }

    /// <summary>
    /// 查找不存在的作业返回空
    /// </summary>
    [Fact]
    public async Task 查找不存在的作业返回空()
    {
        await using var fixture = await CreateFixtureAsync();
        var store = await fixture.CreateClientAsync();

        Assert.Null(await store.FindAsync(Guid.NewGuid()));
    }

    /// <summary>
    /// 只取回到期、未放弃且应用名匹配的作业
    /// </summary>
    [Fact]
    public async Task 只取回到期未放弃且应用名匹配的作业()
    {
        await using var fixture = await CreateFixtureAsync();
        var store = await fixture.CreateClientAsync();
        var now = Now(fixture);
        var due = NewJob(now.AddMinutes(-1), "app");
        var future = NewJob(now.AddHours(1), "app");
        var abandoned = NewJob(now.AddMinutes(-1), "app");
        abandoned.IsAbandoned = true;
        var otherApplication = NewJob(now.AddMinutes(-1), "other");
        foreach (var job in new[] { due, future, abandoned, otherApplication })
        {
            await store.InsertAsync(job);
        }

        var waiting = await store.GetWaitingJobsAsync("app", 10);

        Assert.Equal([due.Id], waiting.Select(item => item.Id));
    }

    /// <summary>
    /// 按优先级降序、重试次数升序、下次执行时间升序取回
    /// </summary>
    [Fact]
    public async Task 按优先级降序重试次数升序下次执行时间升序取回()
    {
        await using var fixture = await CreateFixtureAsync();
        var store = await fixture.CreateClientAsync();
        var now = Now(fixture);
        var low = NewJob(now.AddMinutes(-3), null, BackgroundJobPriority.Low);
        var retried = NewJob(now.AddMinutes(-1), null, BackgroundJobPriority.High);
        retried.TryCount = 1;
        var later = NewJob(now.AddMinutes(-1), null, BackgroundJobPriority.High);
        var earlier = NewJob(now.AddMinutes(-2), null, BackgroundJobPriority.High);
        foreach (var job in new[] { low, retried, later, earlier })
        {
            await store.InsertAsync(job);
        }

        var waiting = await store.GetWaitingJobsAsync(null, 10);

        Assert.Equal([earlier.Id, later.Id, retried.Id, low.Id], waiting.Select(item => item.Id));
    }

    /// <summary>
    /// 取回数量不超过上限
    /// </summary>
    [Fact]
    public async Task 取回数量不超过上限()
    {
        await using var fixture = await CreateFixtureAsync();
        var store = await fixture.CreateClientAsync();
        var now = Now(fixture);
        for (var index = 0; index < 3; index++)
        {
            await store.InsertAsync(NewJob(now.AddMinutes(-1 - index), null));
        }

        var waiting = await store.GetWaitingJobsAsync(null, 2);

        Assert.Equal(2, waiting.Count);
    }

    /// <summary>
    /// 删除后查不到且不再取回
    /// </summary>
    [Fact]
    public async Task 删除后查不到且不再取回()
    {
        await using var fixture = await CreateFixtureAsync();
        var store = await fixture.CreateClientAsync();
        var job = NewJob(Now(fixture).AddMinutes(-1), null);
        await store.InsertAsync(job);
        await store.GetWaitingJobsAsync(null, 10);

        await store.DeleteAsync(job.Id);
        await ContractRequirements.ReleaseClaimsAsync(fixture);

        Assert.Null(await store.FindAsync(job.Id));
        Assert.Empty(await store.GetWaitingJobsAsync(null, 10));
    }

    /// <summary>
    /// 失败回写后到下次执行时间才重新取回，且带回写的字段
    /// </summary>
    [Fact]
    public async Task 失败回写后到下次执行时间才重新取回()
    {
        await using var fixture = await CreateFixtureAsync();
        ContractRequirements.Require(fixture.Capabilities, ProviderCapabilities.ControllableTime);
        var store = await fixture.CreateClientAsync();
        var now = Now(fixture);
        await store.InsertAsync(NewJob(now.AddMinutes(-1), null));
        var claimed = Assert.Single(await store.GetWaitingJobsAsync(null, 10));

        var update = Copy(claimed);
        update.TryCount = 1;
        update.LastTryTime = now;
        update.NextTryTime = now.AddMinutes(1);
        await store.UpdateAsync(update);
        var beforeDue = await store.GetWaitingJobsAsync(null, 10);
        fixture.AdvanceTime(TimeSpan.FromMinutes(2));
        var afterDue = await store.GetWaitingJobsAsync(null, 10);

        Assert.Empty(beforeDue);
        var again = Assert.Single(afterDue);
        Assert.Equal(claimed.Id, again.Id);
        Assert.Equal(1, again.TryCount);
    }

    /// <summary>
    /// 放弃的作业不再取回
    /// </summary>
    [Fact]
    public async Task 放弃的作业不再取回()
    {
        await using var fixture = await CreateFixtureAsync();
        var store = await fixture.CreateClientAsync();
        await store.InsertAsync(NewJob(Now(fixture).AddMinutes(-1), null));
        var claimed = Assert.Single(await store.GetWaitingJobsAsync(null, 10));

        var update = Copy(claimed);
        update.IsAbandoned = true;
        await store.UpdateAsync(update);
        await ContractRequirements.ReleaseClaimsAsync(fixture);

        Assert.Empty(await store.GetWaitingJobsAsync(null, 10));
    }

    /// <summary>
    /// 新建的客户端读得到其他客户端写入的作业
    /// </summary>
    [Fact]
    public async Task 新建客户端读得到其他客户端写入的作业()
    {
        await using var fixture = await CreateFixtureAsync();
        ContractRequirements.Require(fixture.Capabilities, ProviderCapabilities.Persistence);
        var writer = await fixture.CreateClientAsync();
        var job = NewJob(Now(fixture).AddMinutes(-1), null);
        await writer.InsertAsync(job);

        var reader = await fixture.CreateClientAsync();

        Assert.NotNull(await reader.FindAsync(job.Id));
        Assert.Equal([job.Id], (await reader.GetWaitingJobsAsync(null, 10)).Select(item => item.Id));
    }

    /// <summary>
    /// 一个客户端领取的作业不会被其他客户端领到
    /// </summary>
    [Fact]
    public async Task 一个客户端领取的作业不会被其他客户端领到()
    {
        await using var fixture = await CreateFixtureAsync();
        ContractRequirements.Require(fixture.Capabilities, ProviderCapabilities.ExclusiveClaim);
        var first = await fixture.CreateClientAsync();
        var second = await fixture.CreateClientAsync();
        var now = Now(fixture);
        for (var index = 0; index < 3; index++)
        {
            await first.InsertAsync(NewJob(now.AddMinutes(-1 - index), null));
        }

        var claimed = await first.GetWaitingJobsAsync(null, 10);
        var stolen = await second.GetWaitingJobsAsync(null, 10);

        Assert.Equal(3, claimed.Count);
        Assert.Empty(stolen);
    }

    /// <summary>
    /// 租约过期后其他客户端可重新领取
    /// </summary>
    [Fact]
    public async Task 租约过期后其他客户端可重新领取()
    {
        await using var fixture = await CreateFixtureAsync();
        ContractRequirements.Require(fixture.Capabilities, ProviderCapabilities.ExclusiveClaim | ProviderCapabilities.ClaimExpiry);
        var first = await fixture.CreateClientAsync();
        var second = await fixture.CreateClientAsync();
        var job = NewJob(Now(fixture).AddMinutes(-1), null);
        await first.InsertAsync(job);
        await first.GetWaitingJobsAsync(null, 10);

        await fixture.ExpireClaimsAsync();
        var reclaimed = await second.GetWaitingJobsAsync(null, 10);

        Assert.Equal([job.Id], reclaimed.Select(item => item.Id));
    }

    /// <summary>
    /// 多个客户端并发领取时作业不重复且全部领到
    /// </summary>
    [Fact]
    public async Task 并发领取时作业不重复且全部领到()
    {
        const int jobCount = 120;
        const int workerCount = 6;

        await using var fixture = await CreateFixtureAsync();
        ContractRequirements.Require(fixture.Capabilities, ProviderCapabilities.ConcurrentStorage | ProviderCapabilities.ExclusiveClaim);
        var writer = await fixture.CreateClientAsync();
        var now = Now(fixture);
        for (var index = 0; index < jobCount; index++)
        {
            await writer.InsertAsync(NewJob(now.AddMinutes(-10).AddSeconds(index), null));
        }

        var clients = new List<IBackgroundJobStore>();
        for (var index = 0; index < workerCount; index++)
        {
            clients.Add(await fixture.CreateClientAsync());
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        var workers = clients.Select(client => Task.Run(async () =>
        {
            var claimedIds = new List<Guid>();
            for (var round = 0; round <= jobCount; round++)
            {
                var batch = await client.GetWaitingJobsAsync(null, 10);
                if (batch.Count == 0)
                {
                    return claimedIds;
                }

                claimedIds.AddRange(batch.Select(item => item.Id));
            }

            Assert.Fail("并发领取未在预期轮数内取空，提供方可能没有标记已领取的记录。");
            return claimedIds;
        }, cancellationToken));

        var claimed = (await Task.WhenAll(workers)).SelectMany(item => item).ToList();

        Assert.Distinct(claimed);
        Assert.Equal(jobCount, claimed.Count);
    }

    private static DateTime Now(IProviderContractFixture<IBackgroundJobStore> fixture)
    {
        return ContractRequirements.TruncateToSeconds(fixture.UtcNow);
    }

    private static BackgroundJobInfo Copy(BackgroundJobInfo source)
    {
        return new BackgroundJobInfo
        {
            Id = source.Id,
            ApplicationName = source.ApplicationName,
            TenantId = source.TenantId,
            JobName = source.JobName,
            JobArgs = source.JobArgs,
            TryCount = source.TryCount,
            CreationTime = source.CreationTime,
            NextTryTime = source.NextTryTime,
            LastTryTime = source.LastTryTime,
            IsAbandoned = source.IsAbandoned,
            Priority = source.Priority
        };
    }

    private static BackgroundJobInfo NewJob(
        DateTime nextTryTime,
        string? applicationName,
        BackgroundJobPriority priority = BackgroundJobPriority.Normal)
    {
        return new BackgroundJobInfo
        {
            Id = Guid.NewGuid(),
            ApplicationName = applicationName,
            JobName = "Order.Close",
            JobArgs = "{}",
            CreationTime = nextTryTime.AddMinutes(-5),
            NextTryTime = nextTryTime,
            Priority = priority
        };
    }
}
