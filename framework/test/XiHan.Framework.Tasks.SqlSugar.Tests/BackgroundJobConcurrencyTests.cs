// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.Tasks.BackgroundJobs.Models;
using XiHan.Framework.Tasks.SqlSugar.BackgroundJobs;
using XiHan.Framework.Tasks.SqlSugar.Clients;
using XiHan.Framework.Tasks.SqlSugar.Entities;
using XiHan.Framework.Tasks.SqlSugar.Options;

namespace XiHan.Framework.Tasks.SqlSugar.Tests;

/// <summary>
/// 后台作业并发领取测试，需要真实数据库
/// </summary>
/// <remarks>
/// 地址取环境变量 <c>XIHAN_TEST_MYSQL</c>，未设置时整类跳过。
/// </remarks>
[Collection(Contracts.MySqlTestCollection.Name)]
public class BackgroundJobConcurrencyTests
{
    private const string SkipReason = "未设置 XIHAN_TEST_MYSQL，跳过真实数据库并发测试。";

    private static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable("XIHAN_TEST_MYSQL");

    /// <summary>
    /// 并发领取时同一个作业只会被一个调用方领到
    /// </summary>
    [Fact]
    public async Task 并发领取时作业不重复()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), SkipReason);

        const int jobCount = 200;
        const int workerCount = 8;

        var now = DateTime.UtcNow;

        using var setupClient = CreateClient();
        setupClient.CodeFirst.InitTables(typeof(SysBackgroundJob));
        await setupClient.Deleteable<SysBackgroundJob>().ExecuteCommandAsync();

        var (setupStore, setupProvider) = CreateStore(setupClient, now);
        using (setupProvider)
        {
            for (var index = 0; index < jobCount; index++)
            {
                await setupStore.InsertAsync(new BackgroundJobInfo
                {
                    Id = Guid.NewGuid(),
                    JobName = "Order.Close",
                    JobArgs = "{}",
                    CreationTime = now.AddMinutes(-10),
                    NextTryTime = now.AddMinutes(-10).AddSeconds(index)
                });
            }
        }

        var workers = Enumerable.Range(0, workerCount).Select(_ => Task.Run(async () =>
        {
            using var client = CreateClient();
            var (store, provider) = CreateStore(client, now);
            using (provider)
            {
                var claimedIds = new List<Guid>();

                while (true)
                {
                    var batch = await store.GetWaitingJobsAsync(null, 10);
                    if (batch.Count == 0)
                    {
                        break;
                    }

                    claimedIds.AddRange(batch.Select(item => item.Id));
                }

                return claimedIds;
            }
        })).ToArray();

        var results = await Task.WhenAll(workers);

        var allClaimed = results.SelectMany(ids => ids).ToList();

        Assert.Equal(allClaimed.Count, allClaimed.Distinct().Count());
        Assert.Equal(jobCount, allClaimed.Count);

        await setupClient.Deleteable<SysBackgroundJob>().ExecuteCommandAsync();
    }

    private static SqlSugarClient CreateClient()
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = ConnectionString,
            DbType = DbType.MySql,
            IsAutoCloseConnection = true
        });
    }

    private static (SqlSugarBackgroundJobStore Store, ServiceProvider Provider) CreateStore(SqlSugarClient client, DateTime now)
    {
        var currentTenant = new CurrentTenant(AsyncLocalCurrentTenantAccessor.Instance);
        var resolver = new StubClientResolver(client, currentTenant);

        var services = new ServiceCollection();
        services.AddScoped<ISqlSugarClientResolver>(_ => resolver);
        var provider = services.BuildServiceProvider();

        var accessor = new TasksHostClientAccessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            currentTenant);

        var store = new SqlSugarBackgroundJobStore(
            accessor,
            new FakeClock(now),
            Microsoft.Extensions.Options.Options.Create(new XiHanTasksSqlSugarOptions
            {
                BackgroundJobLeaseTimeout = TimeSpan.FromMinutes(5)
            }));

        return (store, provider);
    }
}
