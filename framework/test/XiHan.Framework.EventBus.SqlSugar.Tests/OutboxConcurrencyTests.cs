// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.SqlSugar.Entities;
using XiHan.Framework.EventBus.SqlSugar.Options;
using XiHan.Framework.EventBus.SqlSugar.Outbox;

namespace XiHan.Framework.EventBus.SqlSugar.Tests;

/// <summary>
/// 发件箱并发领取测试，需要真实数据库
/// </summary>
/// <remarks>
/// 地址取环境变量 <c>XIHAN_TEST_MYSQL</c>，未设置时整类跳过。
/// </remarks>
[Collection("MySqlEventOutbox")]
public class OutboxConcurrencyTests
{
    private const string SkipReason = "未设置 XIHAN_TEST_MYSQL，跳过真实数据库并发测试。";

    private static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable("XIHAN_TEST_MYSQL");

    /// <summary>
    /// 并发领取时同一条记录只会被一个调用方领到
    /// </summary>
    [Fact]
    public async Task 并发领取时记录不重复()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), SkipReason);

        const int eventCount = 200;
        const int workerCount = 8;

        using var setupClient = CreateClient();
        setupClient.CodeFirst.InitTables(typeof(SysEventOutbox));
        await setupClient.Deleteable<SysEventOutbox>().ExecuteCommandAsync();

        var setupOutbox = CreateOutbox(setupClient);
        var baseTime = DateTime.UtcNow.AddMinutes(-10);
        for (var index = 0; index < eventCount; index++)
        {
            await setupOutbox.EnqueueAsync(
                new OutgoingEventInfo(Guid.NewGuid(), "Order.Created", [1], baseTime.AddSeconds(index)));
        }

        var workers = Enumerable.Range(0, workerCount).Select(_ => Task.Run(async () =>
        {
            using var client = CreateClient();
            var outbox = CreateOutbox(client);
            var claimedIds = new List<Guid>();

            while (true)
            {
                var batch = await outbox.GetWaitingEventsAsync(10);
                if (batch.Count == 0)
                {
                    break;
                }

                claimedIds.AddRange(batch.Select(item => item.Id));
            }

            return claimedIds;
        })).ToArray();

        var results = await Task.WhenAll(workers);

        var allClaimed = results.SelectMany(ids => ids).ToList();

        Assert.Equal(allClaimed.Count, allClaimed.Distinct().Count());
        Assert.Equal(eventCount, allClaimed.Count);

        await setupClient.Deleteable<SysEventOutbox>().ExecuteCommandAsync();
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

    private static SqlSugarEventOutbox CreateOutbox(SqlSugarClient client)
    {
        var clients = new Dictionary<string, SqlSugarClient>(StringComparer.Ordinal)
        {
            [OutboxTestContext.MainConfigId] = client
        };
        var resolver = new StubClientResolver(clients, [OutboxTestContext.MainConfigId], OutboxTestContext.MainConfigId);

        return new SqlSugarEventOutbox(
            resolver,
            new FakeCurrentTenant(),
            new AsyncLocalSqlSugarOutboxConnectionScope(),
            [],
            Microsoft.Extensions.Options.Options.Create(new XiHanSqlSugarEventBoxOptions
            {
                ClaimTimeout = TimeSpan.FromMinutes(5)
            }),
            NullLogger<SqlSugarEventOutbox>.Instance);
    }
}
