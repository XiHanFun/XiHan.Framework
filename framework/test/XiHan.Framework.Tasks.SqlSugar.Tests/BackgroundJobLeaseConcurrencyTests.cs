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
/// 后台作业按令牌回写的并发测试，需要真实数据库
/// </summary>
/// <remarks>
/// 地址取环境变量 <c>XIHAN_TEST_MYSQL</c>，未设置时整类跳过。
/// </remarks>
public class BackgroundJobLeaseConcurrencyTests
{
    private const string SkipReason = "未设置 XIHAN_TEST_MYSQL，跳过真实数据库并发测试。";

    private static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable("XIHAN_TEST_MYSQL");

    /// <summary>
    /// 旧令牌与新令牌并发完成时只有新令牌命中
    /// </summary>
    [Fact(Timeout = 120000)]
    public async Task 旧令牌与新令牌并发完成时只有新令牌命中()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), SkipReason);

        const int jobCount = 50;
        var leaseTimeout = TimeSpan.FromMinutes(5);
        var now = DateTime.UtcNow;
        var applicationName = $"lease-{Guid.NewGuid():N}";

        using var setupClient = CreateClient();
        setupClient.CodeFirst.InitTables(typeof(SysBackgroundJob));

        var (firstStore, firstProvider) = CreateStore(setupClient, new FakeClock(now), leaseTimeout);
        using var secondClient = CreateClient();
        var (secondStore, secondProvider) = CreateStore(secondClient, new FakeClock(now + leaseTimeout + TimeSpan.FromMinutes(1)), leaseTimeout);

        try
        {
            for (var index = 0; index < jobCount; index++)
            {
                await firstStore.InsertAsync(new BackgroundJobInfo
                {
                    Id = Guid.NewGuid(),
                    ApplicationName = applicationName,
                    JobName = "Order.Close",
                    JobArgs = "{}",
                    CreationTime = now.AddMinutes(-10),
                    NextTryTime = now.AddMinutes(-10).AddSeconds(index)
                });
            }

            var stale = await firstStore.GetWaitingJobsAsync(applicationName, jobCount);
            var fresh = await secondStore.GetWaitingJobsAsync(applicationName, jobCount);
            Assert.Equal(jobCount, stale.Count);
            Assert.Equal(jobCount, fresh.Count);

            var freshById = fresh.ToDictionary(job => job.Id);

            var attempts = stale.SelectMany(job => new[]
            {
                Task.Run(() => CompleteOnNewClientAsync(ToLease(job), now, leaseTimeout)),
                Task.Run(() => CompleteOnNewClientAsync(ToLease(freshById[job.Id]), now, leaseTimeout))
            }).ToArray();

            var results = await Task.WhenAll(attempts);

            for (var index = 0; index < jobCount; index++)
            {
                Assert.False(results[index * 2]);
                Assert.True(results[(index * 2) + 1]);
            }

            Assert.Equal(0, await setupClient.Queryable<SysBackgroundJob>()
                .Where(item => item.ApplicationName == applicationName)
                .CountAsync());
        }
        finally
        {
            await setupClient.Deleteable<SysBackgroundJob>()
                .Where(item => item.ApplicationName == applicationName)
                .ExecuteCommandAsync();
            firstProvider.Dispose();
            secondProvider.Dispose();
        }
    }

    private static async Task<bool> CompleteOnNewClientAsync(BackgroundJobLease lease, DateTime now, TimeSpan leaseTimeout)
    {
        using var client = CreateClient();
        var (store, provider) = CreateStore(client, new FakeClock(now), leaseTimeout);
        using (provider)
        {
            return await store.TryCompleteAsync(lease);
        }
    }

    private static BackgroundJobLease ToLease(BackgroundJobInfo job)
    {
        return new BackgroundJobLease(job.Id, job.ClaimToken!, job.LeaseExpiresAt!.Value);
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

    private static (SqlSugarBackgroundJobStore Store, ServiceProvider Provider) CreateStore(
        SqlSugarClient client,
        FakeClock clock,
        TimeSpan leaseTimeout)
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
            clock,
            Microsoft.Extensions.Options.Options.Create(new XiHanTasksSqlSugarOptions
            {
                BackgroundJobLeaseTimeout = leaseTimeout
            }));

        return (store, provider);
    }
}
